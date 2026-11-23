// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private CharacterPackCatalog characterPackCatalog;
        private FileSystemWatcher characterPackWatcher;
        private readonly System.Windows.Forms.Timer characterPackTimer = new System.Windows.Forms.Timer();
        private CharacterPackScan pendingCharacterScan;
        private bool pendingCharacterReport, characterManualRefresh;
        private bool characterPackDialogOpen;
        private PetCharacter characterPackSelection;
        private int characterPackDirty, characterScanBusy;
        private long characterPackChangedAt;
        private long characterPackGeneration;
        private string[] characterPackErrors = new string[0];
        private readonly object characterPackDeliveryGate = new object();
        private CharacterPackScan deliveringCharacterScan;
        private volatile bool characterPackWatchingDisposed;

        internal static string CharacterPackDirectory
        {
            get
            {
                // Existing isolated tests must never discover or write the real user's packs.
                if (Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") == "1") return Path.Combine(Program.StateDirectory, "characters");
                string configured = Environment.GetEnvironmentVariable("AGENT_PAL_CHARACTER_DIR");
                if (!String.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string current = Path.Combine(root, "agent-pal", "Characters");
                string legacy = Path.Combine(root, "XianPal", "Characters");
                return !Directory.Exists(current) && Directory.Exists(legacy) ? legacy : current;
            }
        }

        private void ShowCompanionManager()
        {
            characterPackDialogOpen=true;
            try
            {
                using(var window=new CompanionManagerForm(()=>currentCharacterId,p=>SetCharacter(p.Id,true),DeleteImportedCompanion,RefreshTrailAppearance))window.ShowDialog(this);
            }
            finally{characterPackDialogOpen=false;RequestCharacterPackRefresh(false);}
        }
        private void DeleteImportedCompanion(PetCharacter companion)
        {
            CharacterPackRemoval.Remove(CharacterPackDirectory,companion);
            PetCharacter.RemoveExternalProfile(companion.Id);
            if(currentCharacterId==companion.Id)SetCharacter("orbit",true);
            UpdateCharacterChecks();RequestCharacterPackRefresh(false);
        }
        private void RefreshTrailAppearance()
        {
            trailColors=CompanionAppearance.Colors(currentCharacterId);
            trailOverlay.Configure(trailColors,trailStyle,trailOpacity);
        }

        private void InitializeCharacterPacks()
        {
            Disposed += delegate { DisposeCharacterPackWatching(); };
            PetCharacter.SetExternalProfiles(null);
            try
            {
                characterPackCatalog = new CharacterPackCatalog(CharacterPackDirectory);
                using (CharacterPackScan scan = characterPackCatalog.Scan(null, null))
                { PetCharacter.SetExternalProfiles(scan.Characters); characterPackErrors = scan.Errors; }
            }
            catch (Exception ex) { characterPackErrors = new[] { ex.Message }; }
        }

        private void StartCharacterPackWatching()
        {
            characterPackTimer.Interval = 1000;
            characterPackTimer.Tick += CharacterPackTimerTick;
            try { CreateCharacterWatcher(); }
            catch (Exception ex) { characterPackErrors = new[] { ex.Message }; }
            characterPackTimer.Start();
        }

        private void CreateCharacterWatcher()
        {
            if (characterPackWatcher != null) { characterPackWatcher.Dispose(); characterPackWatcher = null; }
            if (characterPackCatalog == null) characterPackCatalog = new CharacterPackCatalog(CharacterPackDirectory);
            Directory.CreateDirectory(characterPackCatalog.Root);
            characterPackWatcher = new FileSystemWatcher(characterPackCatalog.Root);
            characterPackWatcher.IncludeSubdirectories = true;
            characterPackWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;
            characterPackWatcher.Changed += CharacterPackChanged;
            characterPackWatcher.Created += CharacterPackChanged;
            characterPackWatcher.Deleted += CharacterPackChanged;
            characterPackWatcher.Renamed += CharacterPackChanged;
            characterPackWatcher.Error += delegate { RequestCharacterPackRefresh(false); };
            characterPackWatcher.EnableRaisingEvents = true;
        }

        private void CharacterPackChanged(object sender, FileSystemEventArgs e)
        {
            string root = ((FileSystemWatcher)sender).Path;
            var renamed = e as RenamedEventArgs;
            if (IsCharacterStagingPath(root, e.FullPath) &&
                (renamed == null || IsCharacterStagingPath(root, renamed.OldFullPath))) return;
            Interlocked.Exchange(ref characterPackChangedAt, DateTime.UtcNow.Ticks);
            Interlocked.Increment(ref characterPackGeneration);
            Interlocked.Exchange(ref characterPackDirty, 1);
        }

        private static bool IsCharacterStagingPath(string root, string path)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
        }

        private void RequestCharacterPackRefresh(bool report)
        {
            if (InvokeRequired) { RunOnUi(delegate { RequestCharacterPackRefresh(report); }); return; }
            characterManualRefresh |= report;
            Interlocked.Exchange(ref characterPackChangedAt, 0);
            Interlocked.Increment(ref characterPackGeneration);
            Interlocked.Exchange(ref characterPackDirty, 1);
        }

        private bool CanApplyCharacterPackUpdate()
        {
            return !characterPackDialogOpen && !dragging && reminderOverlay == null && fileEatOverlay == null && !fileWipeTimer.Enabled &&
                (ContextMenuStrip == null || !ContextMenuStrip.Visible) && !clickActionsMenu.Visible;
        }

        private void CharacterPackTimerTick(object sender, EventArgs e)
        {
            if (closingPet || IsDisposed) return;
            if (pendingCharacterScan != null)
            {
                if (!CanApplyCharacterPackUpdate()) return;
                CharacterPackScan ready = pendingCharacterScan; bool report = pendingCharacterReport;
                pendingCharacterScan = null; pendingCharacterReport = false;
                try
                {
                    if (ready.Generation==Interlocked.Read(ref characterPackGeneration) && (characterPackSelection == null || Object.ReferenceEquals(ready.RequestedSelection, characterPackSelection)))
                        ApplyCharacterPackScan(ready, report);
                    else characterManualRefresh |= report;
                }
                finally { ready.Dispose(); }
            }
            if (closingPet || IsDisposed || characterPackWatchingDisposed) return;
            if (Interlocked.CompareExchange(ref characterPackDirty, 0, 0) == 0 || characterScanBusy != 0 || !IsHandleCreated) return;
            long generation = Interlocked.Read(ref characterPackGeneration);
            long changed = Interlocked.Read(ref characterPackChangedAt);
            if (changed != 0 && DateTime.UtcNow.Ticks - changed < TimeSpan.FromMilliseconds(1500).Ticks) return;
            try { if (characterPackCatalog == null || characterPackWatcher == null || !Directory.Exists(characterPackCatalog.Root)) CreateCharacterWatcher(); }
            catch (Exception ex)
            {
                characterPackErrors = new[] { ex.Message }; Interlocked.Exchange(ref characterPackDirty, 0);
                if (characterManualRefresh) { characterManualRefresh = false; ShowCharacterPackReport(); } return;
            }
            Interlocked.Exchange(ref characterPackDirty, 0);
            if (Interlocked.Read(ref characterPackGeneration) != generation) { Interlocked.Exchange(ref characterPackDirty, 1); return; }
            bool showReport = characterManualRefresh; characterManualRefresh = false;
            Interlocked.Exchange(ref characterScanBusy, 1);
            string activeId = currentCharacterId, revision = loadedCharacterRevision;
            PetCharacter requestedSelection = characterPackSelection;
            ThreadPool.QueueUserWorkItem(delegate
            {
                CharacterPackScan scan = null;
                try
                {
                    scan = characterPackCatalog.Scan(activeId, revision);
                    scan.RequestedSelection = requestedSelection;
                    scan.Generation=generation;
                    CharacterPackScan completed = scan;
                    lock (characterPackDeliveryGate)
                    {
                        if (characterPackWatchingDisposed || closingPet || IsDisposed || !IsHandleCreated) { scan.Dispose(); Interlocked.Exchange(ref characterScanBusy, 0); return; }
                        deliveringCharacterScan = scan;
                        BeginInvoke((MethodInvoker)delegate
                        {
                            lock (characterPackDeliveryGate)
                            {
                                if (!Object.ReferenceEquals(deliveringCharacterScan, completed)) return;
                                deliveringCharacterScan = null;
                            }
                            Interlocked.Exchange(ref characterScanBusy, 0);
                            if (closingPet || IsDisposed) { completed.Dispose(); return; }
                            if (pendingCharacterScan != null) pendingCharacterScan.Dispose();
                            pendingCharacterScan = completed; pendingCharacterReport = showReport;
                        });
                    }
                }
                catch (Exception)
                {
                    lock (characterPackDeliveryGate) { if (Object.ReferenceEquals(deliveringCharacterScan, scan)) deliveringCharacterScan = null; }
                    if (scan != null) scan.Dispose(); Interlocked.Exchange(ref characterScanBusy, 0);
                }
            });
        }

        private void ApplyCharacterPackScan(CharacterPackScan scan, bool report)
        {
            PetCharacter old = PetCharacter.Find(currentCharacterId);
            PetCharacter.SetExternalProfiles(scan.Characters);
            var errors = new List<string>(scan.Errors);
            PetCharacter current = PetCharacter.Find(currentCharacterId);
            if (old.IsExternal && current.Id != currentCharacterId) SetCharacter("orbit", true);
            else if (current.UsesPackFrames && loadedCharacterRevision != current.ResourceRevision)
            {
                try
                {
                    CharacterAnimationSet prepared = scan.PreparedId == current.Id ? scan.PreparedAssets : null;
                    if (prepared != null) scan.PreparedAssets = null;
                    string previousState = state; int previousFrame = frameIndex;
                    LoadCharacterAnimations(current.Id, prepared);
                    // Resource refresh is not a new user interaction: keep the
                    // current route, task reaction, gesture deadline and gait phase.
                    state = animations.ContainsKey(previousState) ? previousState : "idle";
                    frameIndex = previousFrame % animations[state].Count;
                    Text = current.Name + " · " + Program.ProductName;
                    RefreshTrayIcon(); RefreshPetVisual();
                }
                catch (Exception ex)
                {
                    var restored = new List<PetCharacter>(scan.Characters);
                    for (int i = 0; i < restored.Count; i++) if (restored[i].Id == old.Id) restored[i] = old;
                    PetCharacter.SetExternalProfiles(restored.ToArray());
                    errors.Add(current.Id + "：更新未应用，保留当前已加载形象。" + ex.Message);
                }
            }
            characterPackErrors = errors.ToArray(); UpdateCharacterChecks();
            if (characterPackSelection != null && Object.ReferenceEquals(scan.RequestedSelection, characterPackSelection))
            {
                PetCharacter expected = characterPackSelection;
                characterPackSelection = null;
                PetCharacter selected = PetCharacter.Find(expected.Id);
                if (selected.IsExternal && selected.ResourceRevision == expected.ResourceRevision) SetCharacter(selected.Id, true);
                else MessageBox.Show(UiLanguage.Text("角色已写入资源目录，但未能切换，请查看刷新结果。"),
                    Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (report) ShowCharacterPackReport();
        }

        private void OpenCharacterPackImport()
        {
            if (characterPackDialogOpen || closingPet || IsDisposed) return;
            string title = UiLanguage.Text("导入角色包…");
            using (var picker = new OpenFileDialog
            {
                Title = title, Filter = UiLanguage.Text("角色包 (*.zip)|*.zip"),
                CheckFileExists = true, Multiselect = false, RestoreDirectory = true
            })
            {
                if (picker.ShowDialog() != DialogResult.OK) return;
                PreviewCharacterPackArchive(picker.FileName);
            }
        }

        internal void PreviewCharacterPackArchive(string archive)
        {
            if (characterPackDialogOpen || closingPet || IsDisposed) return;
            string root = CharacterPackDirectory;
            bool batch;
            try { batch = CharacterPackDraft.IsCollection(archive); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiLanguage.Text("无法准备角色包。"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (batch)
            {
                bool installed = false;
                characterPackDialogOpen = true;
                try
                {
                    using (var preview = new CharacterPackCollectionForm(root, archive))
                        installed = preview.ShowDialog() == DialogResult.OK;
                }
                finally { characterPackDialogOpen = false; }
                if (installed) RequestCharacterPackRefresh(false);
                return;
            }
            PreviewCharacterPack(UiLanguage.Text("导入角色包…"), UiLanguage.Text("正在检查 ZIP 角色包…"),
                delegate(CancellationToken token) { return CharacterPackDraft.FromZip(root, archive, token); });
        }

        private void OpenImageCharacterWizard()
        {
            if (characterPackDialogOpen || closingPet || IsDisposed) return;
            using (var wizard = new CharacterFromImagesForm())
                while (wizard.ShowDialog() == DialogResult.OK)
                    if (PreviewCreatedCharacter(wizard.CharacterName, wizard.FrontPath, wizard.LeftPath, wizard.RightPath,
                        wizard.ArtifactPath, wizard.ArtifactIsWings, wizard.CharacterDescription)) break;
        }

        internal bool PreviewCreatedCharacter(string name, string front, string left, string right,
            string artifact = null, bool wings = false, string description = null)
        {
            string root = CharacterPackDirectory;
            return PreviewCharacterPack(UiLanguage.Text("从图片创建…"), UiLanguage.Text("正在制作角色动作…"),
                delegate(CancellationToken token) { return CharacterPackFromImages.Create(root, name, front, left, right,
                    token, artifact, wings, description); });
        }

        private bool PreviewCharacterPack(string title, string status, Func<CancellationToken, CharacterPackDraft> prepare)
        {
            if (characterPackDialogOpen || closingPet || IsDisposed) return false;
            characterPackDialogOpen = true;
            bool installed = false;
            try
            {
                using (var preview = new CharacterPackPreviewForm(title, status, prepare))
                    if (preview.ShowDialog() == DialogResult.OK)
                    {
                        characterPackSelection = preview.InstalledCharacter;
                        installed = true;
                    }
            }
            finally { characterPackDialogOpen = false; }
            if (installed) RequestCharacterPackRefresh(false);
            return installed;
        }

        private string BuildCharacterPackReport()
        {
            var names = new List<string>();
            foreach (PetCharacter character in PetCharacter.Available)
                if (character.IsExternal) names.Add(character.Name + " (" + character.Id + ")");
            string message = UiLanguage.Text("角色资源目录：") + Environment.NewLine + CharacterPackDirectory;
            if (names.Count == 0 && characterPackErrors.Length == 0)
                message += Environment.NewLine + Environment.NewLine + UiLanguage.Text("请将包含 character.json 的完整角色文件夹复制到此目录，然后刷新角色资源。");
            if (names.Count > 0)
                message += Environment.NewLine + Environment.NewLine + UiLanguage.Text("可用外置角色：") + Environment.NewLine +
                    String.Join(Environment.NewLine, names) + Environment.NewLine + UiLanguage.Text("在“切换形象”中选择角色。");
            if (characterPackErrors.Length > 0)
                message += Environment.NewLine + Environment.NewLine + UiLanguage.Text("以下资源未能应用：") + Environment.NewLine +
                    String.Join(Environment.NewLine, characterPackErrors);
            return message;
        }

        private void ShowCharacterPackReport()
        {
            MessageBox.Show(BuildCharacterPackReport(), UiLanguage.Text("外置角色资源"), MessageBoxButtons.OK,
                characterPackErrors.Length == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void OpenCharacterPackDirectory()
        {
            try
            {
                Directory.CreateDirectory(CharacterPackDirectory);
                Process.Start(new ProcessStartInfo(CharacterPackDirectory) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void DisposeCharacterPackWatching()
        {
            if (characterPackWatchingDisposed) return;
            characterPackWatchingDisposed = true;
            characterPackTimer.Stop(); characterPackTimer.Dispose();
            if (characterPackWatcher != null) characterPackWatcher.Dispose();
            lock (characterPackDeliveryGate) { if (deliveringCharacterScan != null) { deliveringCharacterScan.Dispose(); deliveringCharacterScan = null; } }
            if (pendingCharacterScan != null) { pendingCharacterScan.Dispose(); pendingCharacterScan = null; }
        }
    }
}
