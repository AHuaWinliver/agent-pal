// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace AgentPal
{
    internal sealed partial class CharacterPackDraft : IDisposable
    {
        private readonly string root;
        private readonly string staging;
        private string packFolder;
        private bool preserveStaging;
        private bool installed;

        internal PetCharacter Character { get; private set; }
        internal string Version { get; private set; }
        internal Image Preview { get; private set; }
        internal bool ReplacesExisting { get { return Directory.Exists(Path.Combine(root, Character.Id)); } }

        internal Image ReadArtifact()
        {
            string file = Character.ArtifactResourceFile;
            return file == null ? null : CharacterPackCatalog.ReadArtifactPng(
                CharacterPackCatalog.ReadBytes(Path.Combine(packFolder, file), CharacterPackCatalog.MaxFrameBytes), true);
        }

        internal Image ReadPreviewFrame(bool left, int phase)
        {
            string state = left ? "running-left" : "running-right";
            string path = Path.Combine(packFolder, state, phase.ToString("00") + ".png");
            using (var source = Image.FromFile(path)) return new Bitmap(source);
        }

        private CharacterPackDraft(string root)
        {
            this.root = new CharacterPackCatalog(root).Root;
            staging = Path.Combine(this.root, ".import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
        }

        internal static CharacterPackDraft Prepare(string root, string id, string version,
            Action<string> writePack, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!CharacterPackCatalog.ValidId(id))
                throw new InvalidDataException(UiLanguage.Text("角色包中的角色 ID 无效或已被保留。"));
            var draft = new CharacterPackDraft(root);
            try
            {
                draft.packFolder = Path.Combine(draft.staging, id);
                Directory.CreateDirectory(draft.packFolder);
                writePack(draft.packFolder);
                cancellation.ThrowIfCancellationRequested();
                draft.Character = ReadVerified(draft.packFolder);
                draft.Version = version;
                cancellation.ThrowIfCancellationRequested();
                using (var source = Image.FromFile(Path.Combine(draft.packFolder, "idle", "00.png")))
                    draft.Preview = new Bitmap(source);
                return draft;
            }
            catch { draft.Dispose(); throw; }
        }

        internal static CharacterPackDraft FromZip(string root, string zipPath, CancellationToken cancellation)
        {
            using (var file = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length > CharacterPackCatalog.MaxPackBytes + 2 * 1024 * 1024)
                    throw new InvalidDataException(UiLanguage.Text("ZIP 角色包过大。"));
                using (var archive = new ZipArchive(file, ZipArchiveMode.Read, false))
                {
                    return ReadZipEntries(root, FindManifestPrefix(archive), archive.Entries, cancellation);
                }
            }
        }

        private static CharacterPackDraft ReadZipEntries(string root, string prefix, IList<ZipArchiveEntry> source, CancellationToken cancellation)
        {
            if (source.Count > 257) throw UnsupportedArchive();
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            var allowed = AllowedEntries();
            long totalBytes = 0;
            foreach (ZipArchiveEntry entry in source)
            {
                cancellation.ThrowIfCancellationRequested();
                string path = entry.FullName.Replace('\\', '/');
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw UnsupportedArchive();
                string relative = path.Substring(prefix.Length);
                string canonical;
                if (relative.Length == 0 && prefix.Length != 0) canonical = "";
                else if (!allowed.TryGetValue(relative, out canonical)) throw UnsupportedArchive();
                if (entries.ContainsKey(canonical)) throw UnsupportedArchive();
                entries.Add(canonical, entry);
                long limit = EntryLimit(canonical);
                if (entry.Length < 0 || entry.Length > limit)
                    throw new InvalidDataException(UiLanguage.Text("ZIP 角色包中的文件过大。"));
                totalBytes += entry.Length;
                if (totalBytes > CharacterPackCatalog.MaxPackBytes)
                    throw new InvalidDataException(UiLanguage.Text("ZIP 角色包过大。"));
            }

            ZipArchiveEntry manifestEntry;
            if (!entries.TryGetValue("character.json", out manifestEntry)) throw UnsupportedArchive();
            byte[] manifestBytes;
            using (var buffer = new MemoryStream())
            {
                CopyEntry(manifestEntry, buffer, CharacterPackCatalog.MaxManifestBytes, cancellation);
                manifestBytes = buffer.ToArray();
            }
            CharacterPackManifest manifest = CharacterPackCatalog.ParseManifest(manifestBytes);
            if (manifest == null || !CharacterPackCatalog.ValidId(manifest.id) ||
                (prefix.Length != 0 && !String.Equals(prefix, manifest.id + "/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(UiLanguage.Text("角色包中的角色 ID 无效或已被保留。"));
            return Prepare(root, manifest.id, manifest.version, delegate(string folder)
            {
                foreach (var pair in entries)
                {
                    string relative = pair.Key;
                    if (EntryLimit(relative) == 0) continue;
                    cancellation.ThrowIfCancellationRequested();
                    // Only canonical, allowlisted paths reach disk.
                    string target = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                        CopyEntry(pair.Value, output, EntryLimit(relative), cancellation);
                }
            }, cancellation);
        }

        internal PetCharacter Install(bool replace)
        {
            if (installed) throw new InvalidOperationException();
            string destination = Path.Combine(root, Character.Id);
            string backup = Path.Combine(staging, ".previous");
            CharacterPackCatalog.CheckPath(root);
            if (ReadVerified(packFolder).ResourceRevision != Character.ResourceRevision)
                throw new IOException(UiLanguage.Text("预览后资源已变化，请重新打开角色包。"));

            if (Directory.Exists(destination))
            {
                if (!replace) throw new IOException(UiLanguage.Text("同名角色已存在，请重新预览后选择是否替换。"));
                CharacterPackCatalog.CheckPath(destination);
                Directory.Move(destination, backup);
            }
            else
            {
                int count = 0;
                foreach (string folder in Directory.GetDirectories(root))
                    if (!Path.GetFileName(folder).StartsWith(".", StringComparison.Ordinal)) count++;
                if (count >= CharacterPackCatalog.MaxPacks)
                    throw new IOException(UiLanguage.Text("外置角色已达到 16 个，请先移出一个角色。"));
            }
            try { Directory.Move(packFolder, destination); }
            catch
            {
                if (Directory.Exists(backup))
                {
                    try { Directory.Move(backup, destination); }
                    catch (IOException) { preserveStaging = true; }
                    catch (UnauthorizedAccessException) { preserveStaging = true; }
                    if (preserveStaging)
                        throw new IOException(UiLanguage.Format("无法恢复原角色，原文件仍在 {0}", backup));
                }
                throw;
            }
            installed = true;
            Character.ResourceRoot = destination;
            return Character;
        }

        private static PetCharacter ReadVerified(string folder)
        {
            CharacterAnimationSet discarded;
            PetCharacter result = CharacterPackCatalog.ReadPack(folder, false, out discarded);
            discarded.Dispose();
            return result;
        }

        private static InvalidDataException UnsupportedArchive()
        {
            return new InvalidDataException(UiLanguage.Text("ZIP 应包含一个完整角色。发现多余文件、重复路径或不支持的目录结构。"));
        }

        private static string FindManifestPrefix(ZipArchive archive)
        {
            string prefix = null;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string[] parts = entry.FullName.Replace('\\', '/').Split('/');
                if (!String.Equals(parts[parts.Length - 1], "character.json", StringComparison.OrdinalIgnoreCase)) continue;
                if (prefix != null || parts.Length > 2 || (parts.Length == 2 && !CharacterPackCatalog.ValidId(parts[0].ToLowerInvariant())))
                    throw UnsupportedArchive();
                prefix = parts.Length == 1 ? "" : parts[0] + "/";
            }
            if (prefix == null) throw UnsupportedArchive();
            return prefix;
        }

        private static Dictionary<string, string> AllowedEntries()
        {
            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in new[] { "character.json", "README.md", "LICENSE.txt", CharacterPackCatalog.WingsFile, CharacterPackCatalog.PlatformFile })
                entries.Add(name, name);
            foreach (string state in CharacterPackCatalog.States)
            {
                entries.Add(state + "/", state + "/");
                for (int i = 0; i < CharacterPackCatalog.FrameCount(state); i++)
                {
                    string relative = state + "/" + i.ToString("00") + ".png";
                    entries.Add(relative, relative);
                }
            }
            return entries;
        }

        private static long EntryLimit(string relative)
        {
            if (relative.Length == 0 || relative.EndsWith("/", StringComparison.Ordinal)) return 0;
            return relative.EndsWith(".png", StringComparison.Ordinal) ? CharacterPackCatalog.MaxFrameBytes : CharacterPackCatalog.MaxManifestBytes;
        }

        private static void CopyEntry(ZipArchiveEntry entry, Stream output, long limit, CancellationToken cancellation)
        {
            using (Stream input = entry.Open())
            {
                byte[] buffer = new byte[64 * 1024];
                long size = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    size += read;
                    if (size > limit || size > entry.Length)
                        throw new InvalidDataException(UiLanguage.Text("ZIP 角色包中的文件过大。"));
                    output.Write(buffer, 0, read);
                }
                if (size != entry.Length) throw new InvalidDataException(UiLanguage.Text("ZIP 角色包不完整。"));
            }
        }

        public void Dispose()
        {
            if (Preview != null) { Preview.Dispose(); Preview = null; }
            if (preserveStaging || !Directory.Exists(staging)) return;
            try
            {
                // staging is an absolute, uniquely owned child of the checked root.
                CharacterPackCatalog.CheckPath(staging);
                Directory.Delete(staging, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
