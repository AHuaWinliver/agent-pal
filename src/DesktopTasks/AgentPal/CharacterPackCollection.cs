using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace AgentPal
{
    internal sealed partial class CharacterPackDraft
    {
        private const long CollectionLimit = 512L * 1024 * 1024;

        internal static bool IsCollection(string zipPath)
        {
            using (var stream = File.OpenRead(zipPath))
            {
                if (stream.Length > CollectionLimit) throw CollectionTooLarge();
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    int manifests = 0;
                    foreach (var entry in archive.Entries)
                        if (String.Equals(Path.GetFileName(entry.FullName.Replace('\\', '/')), "character.json", StringComparison.OrdinalIgnoreCase)
                            && ++manifests > 1) return true;
                    return false;
                }
            }
        }

        internal sealed class Collection : IDisposable
        {
            private readonly string characterRoot;
            private readonly List<CharacterPackDraft> drafts = new List<CharacterPackDraft>();
            private readonly HashSet<string> replacements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private bool applied;

            private Collection(string root) { characterRoot = new CharacterPackCatalog(root).Root; }

            internal IEnumerable<CharacterPackDraft> Drafts { get { return drafts.AsReadOnly(); } }
            internal bool HasReplacements { get { return replacements.Count != 0; } }
            internal bool WillReplace(string id) { return replacements.Contains(id); }

            internal static Collection Prepare(string root, string zipPath, CancellationToken cancellation)
            {
                var collection = new Collection(root);
                try
                {
                    using (var stream = File.OpenRead(zipPath))
                    {
                        if (stream.Length > CollectionLimit) throw CollectionTooLarge();
                        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                        {
                            var groups = GroupEntries(archive, cancellation);
                            foreach (var group in groups)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                var draft = ReadZipEntries(collection.characterRoot, group.Key + "/", group.Value, cancellation);
                                collection.drafts.Add(draft);
                                if (draft.ReplacesExisting) collection.replacements.Add(draft.Character.Id);
                            }
                        }
                    }
                    collection.CheckCapacity();
                    return collection;
                }
                catch { collection.Dispose(); throw; }
            }

            private static SortedDictionary<string, List<ZipArchiveEntry>> GroupEntries(ZipArchive archive, CancellationToken cancellation)
            {
                if (archive.Entries.Count > 257 * CharacterPackCatalog.MaxPacks + 3) throw CollectionTooLarge();
                var groups = new SortedDictionary<string, List<ZipArchiveEntry>>(StringComparer.OrdinalIgnoreCase);
                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long size = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string path = entry.FullName.Replace('\\', '/');
                    if (!files.Add(path)) throw InvalidCollection();
                    if (entry.Length < 0 || entry.Length > CollectionLimit - size) throw CollectionTooLarge();
                    size += entry.Length;
                    int slash = path.IndexOf('/');
                    if (slash < 0)
                    {
                        if ((path != "README.md" && path != "LICENSE.txt" && path != "先读我.md") ||
                            entry.Length > CharacterPackCatalog.MaxManifestBytes) throw InvalidCollection();
                        continue;
                    }
                    string id = path.Substring(0, slash);
                    if (!CharacterPackCatalog.ValidId(id)) throw InvalidCollection();
                    List<ZipArchiveEntry> group;
                    if (!groups.TryGetValue(id, out group))
                    {
                        if (groups.Count >= CharacterPackCatalog.MaxPacks) throw CollectionTooLarge();
                        groups.Add(id, group = new List<ZipArchiveEntry>());
                    }
                    group.Add(entry);
                }
                if (groups.Count < 2) throw InvalidCollection();
                return groups;
            }

            private void CheckCapacity()
            {
                int count = drafts.Count;
                foreach (string path in Directory.GetDirectories(characterRoot))
                    if (!Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal) &&
                        !replacements.Contains(Path.GetFileName(path))) count++;
                if (count > CharacterPackCatalog.MaxPacks)
                    throw new IOException(UiLanguage.Text("安装后将超过 16 个外置角色，请先移出部分角色。"));
            }

            internal void Install(bool replaceConfirmed)
            {
                if (applied) throw new InvalidOperationException();
                if (HasReplacements && !replaceConfirmed) throw new IOException(UiLanguage.Text("请确认替换已有角色。"));
                CharacterPackCatalog.CheckPath(characterRoot);
                CheckCapacity();
                foreach (var draft in drafts)
                {
                    if (draft.ReplacesExisting != WillReplace(draft.Character.Id))
                        throw new IOException(UiLanguage.Text("已安装的角色发生变化，请重新打开合集。"));
                    if (draft.ReplacesExisting) CharacterPackCatalog.CheckPath(Path.Combine(characterRoot, draft.Character.Id));
                    if (ReadVerified(draft.packFolder).ResourceRevision != draft.Character.ResourceRevision)
                        throw new IOException(UiLanguage.Text("预览后资源已变化，请重新打开角色包。"));
                }

                var touched = new List<CharacterPackDraft>();
                var placed = new HashSet<CharacterPackDraft>();
                try
                {
                    foreach (var draft in drafts)
                    {
                        string destination = Path.Combine(characterRoot, draft.Character.Id);
                        if (WillReplace(draft.Character.Id)) Directory.Move(destination, BackupPath(draft));
                        touched.Add(draft);
                        Directory.Move(draft.packFolder, destination);
                        placed.Add(draft);
                    }
                }
                catch
                {
                    var recoveries = new List<string>();
                    for (int index = touched.Count - 1; index >= 0; index--)
                    {
                        var draft = touched[index];
                        string destination = Path.Combine(characterRoot, draft.Character.Id);
                        try
                        {
                            if (placed.Contains(draft)) Directory.Move(destination, draft.packFolder);
                            if (Directory.Exists(BackupPath(draft))) Directory.Move(BackupPath(draft), destination);
                        }
                        catch (IOException) { draft.preserveStaging = true; recoveries.Add(draft.staging); }
                        catch (UnauthorizedAccessException) { draft.preserveStaging = true; recoveries.Add(draft.staging); }
                    }
                    if (recoveries.Count != 0)
                        throw new IOException(UiLanguage.Format("无法完全恢复原角色，请检查保留的暂存目录：{0}", String.Join(", ", recoveries)));
                    throw;
                }
                applied = true;
                foreach (var draft in drafts)
                {
                    draft.installed = true;
                    draft.Character.ResourceRoot = Path.Combine(characterRoot, draft.Character.Id);
                }
            }

            private static string BackupPath(CharacterPackDraft draft) { return Path.Combine(draft.staging, ".previous"); }

            public void Dispose()
            {
                foreach (var draft in drafts) draft.Dispose();
                drafts.Clear();
            }
        }

        private static InvalidDataException InvalidCollection()
        { return new InvalidDataException(UiLanguage.Text("合集 ZIP 必须包含多个完整角色文件夹，不能包含额外文件或目录。")); }

        private static InvalidDataException CollectionTooLarge()
        { return new InvalidDataException(UiLanguage.Text("合集 ZIP 最多支持 16 个角色、512 MB。")); }
    }
}
