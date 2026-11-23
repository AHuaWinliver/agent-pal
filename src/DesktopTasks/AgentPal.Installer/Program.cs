using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle(AgentPal.StartupRegistration.DisplayName + " 安装程序")]
[assembly: AssemblyProduct(AgentPal.StartupRegistration.DisplayName)]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AgentPal.Installer
{
    internal static class Program
    {
        internal const string AppName = AgentPal.StartupRegistration.DisplayName;
        internal const string Version = "1.0.0";
        private const string InstallRegistry = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
        private const string LegacyExe = "OrbitPet.exe";
        internal static string InstallDirectory = ResolveInstallDirectory();
        internal static string IntegrationNotice="";
        internal static bool DeferLaunch;
        internal static string PetExe { get { return Path.Combine(InstallDirectory, "agent-pal.exe"); } }

        private static string ResolveInstallDirectory()
        {
            string testRoot = Environment.GetEnvironmentVariable("AGENT_PAL_INSTALL_ROOT");
            if (!String.IsNullOrWhiteSpace(testRoot)) return Path.GetFullPath(testRoot);
            string own = AppDomain.CurrentDomain.BaseDirectory;
            if (Array.Exists(Environment.GetCommandLineArgs(), delegate(string value) { return value == "--uninstall"; }) && IsOwned(own)) return own;
            foreach (string name in new[] { AppName, StartupRegistration.PreviousPackageName, "OrbitPet" })
            {
                using (var installed = Registry.CurrentUser.OpenSubKey(InstallRegistry + name))
                {
                    string previous = installed == null ? null : installed.GetValue("InstallLocation") as string;
                    if (!String.IsNullOrWhiteSpace(previous) && IsOwned(previous)) return Path.GetFullPath(previous);
                }
            }
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (string name in new[] { "XianPal", "OrbitPet" })
            {
                string previous = Path.Combine(root, name);
                if (IsOwned(previous)) return previous;
            }
            return Path.Combine(root, "Programs", AppName);
        }

        private static bool IsOwned(string path)
        {
            foreach (var marker in new[] { new[] { ".agent-pal-install", "agent-pal:3" }, new[] { ".orbitpet-install", "OrbitPet:3" } })
            {
                string file = Path.Combine(path, marker[0]);
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0 && File.ReadAllText(file) == marker[1]) return true;
            }
            return false;
        }

        internal static void SelectInstallDirectory(string value)
        {
            string previous = InstallDirectory;
            InstallDirectory = Path.GetFullPath(value);
            try { ValidateInstallLocation(false); }
            catch { InstallDirectory = previous; throw; }
        }

        private static bool TestMode
        {
            get { return String.Equals(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE"), "1", StringComparison.Ordinal); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                for (int i = 0; i < args.Length; i++) if (args[i] == "--install-dir")
                {
                    if (i + 1 == args.Length) throw new ArgumentException("--install-dir requires a path.");
                    SelectInstallDirectory(args[++i]);
                }
            }
            catch (Exception error)
            {
                if (Array.Exists(args, delegate(string value) { return value == "--silent"; })) Console.Error.WriteLine(error.Message);
                else MessageBox.Show(error.Message, "AgentPal Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.ExitCode = 1; return;
            }
            if (Array.Exists(args, delegate(string value) { return value == "--uninstall"; }))
            {
                Application.EnableVisualStyles();
                Uninstall();
                return;
            }
            if (Array.Exists(args, delegate(string value) { return value == "--silent"; }))
            {
                try { Install(); Environment.ExitCode = 0; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(TestMode ? ex.ToString() : ex.GetBaseException().Message);
                    Environment.ExitCode = 1;
                }
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }

        internal static void Install(Action<int, string> progress = null)
        {
            IntegrationNotice="";
            Report(progress, 0, "Preparing installation…");
            ValidateInstallLocation(false);
            Directory.CreateDirectory(InstallDirectory);
            StopPet();
            Report(progress, 3, "Extracting files…");
            ExtractPayload(progress);
            Report(progress, 94, "Creating shortcuts and configuring integration…");
            File.WriteAllText(Path.Combine(InstallDirectory, ".agent-pal-install"), "agent-pal:3", new System.Text.UTF8Encoding(false));
            RegisterStartMenuShortcut();
            // Claude configuration is user-owned. Integration is scoped to each launched session.
            if (!TestMode)
            {
                RegisterStartup();
                RegisterUninstaller();
            }
            RemoveLegacyShortcut();
            if (!TestMode)
            {
                // Refresh existing bindings after an upgrade. Certificate consent is
                // obtained only in the menu settings, never by a silent install.
                using (Process menu = Process.Start(new ProcessStartInfo(PetExe, "--refresh-quick-commands") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = InstallDirectory }))
                {
                    if (!menu.WaitForExit(30000)) { try { menu.Kill(); } catch { } IntegrationNotice=UiLanguage.Text("右键菜单尚未完成注册，请在默认设置中保存并更新快捷任务。"); }
                    else if(menu.ExitCode!=0)IntegrationNotice=UiLanguage.Text(menu.ExitCode==3?"主菜单扩展等待系统授权，请在默认设置中保存并更新快捷任务。":"右键菜单尚未完成注册，请在默认设置中保存并更新快捷任务。");
                }
            }

            if (!TestMode && !DeferLaunch)
                Process.Start(new ProcessStartInfo(PetExe) { UseShellExecute = true });
            Report(progress, 100, "Installation complete.");
        }

        private static void Report(Action<int, string> progress, int percent, string stage)
        {
            if (progress != null) progress(percent, UiLanguage.Text(stage));
        }

        internal static bool HasClaudeCli()
        {
            string[] candidates = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd")
            };
            foreach (string candidate in candidates) if (File.Exists(candidate)) return true;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string entry in path.Split(Path.PathSeparator))
            {
                string clean = entry.Trim().Trim('"');
                if (File.Exists(Path.Combine(clean, "claude.exe")) || File.Exists(Path.Combine(clean, "claude.cmd"))) return true;
            }
            return false;
        }

        private static void ExtractPayload(Action<int, string> progress)
        {
            string staging = Path.Combine(InstallDirectory, ".payload-" + Guid.NewGuid().ToString("N"));
            string assets = Path.Combine(InstallDirectory, "assets");
            string scenes = Path.Combine(InstallDirectory,"scenes");
            string backup = Path.Combine(InstallDirectory, "asset-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string backupAssets = Path.Combine(backup, "assets");
            string backupScenes=Path.Combine(backup,"scenes");
            string backupExe = Path.Combine(backup, "agent-pal.exe");
            bool savedExe = false, movedOldAssets = false, promotedAssets = false;
            bool movedOldScenes=false,promotedScenes=false;
            Directory.CreateDirectory(staging);
            try
            {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream("AgentPalPayload.zip"))
            {
                if (resource == null) throw new InvalidOperationException("安装包缺少内嵌资源。");
                using (ZipArchive zip = new ZipArchive(resource, ZipArchiveMode.Read))
                {
                    long totalBytes = 0, copiedBytes = 0;
                    foreach (ZipArchiveEntry entry in zip.Entries) if (!String.IsNullOrEmpty(entry.Name)) totalBytes += entry.Length;
                    int lastPercent = 3;
                    byte[] buffer = new byte[81920];
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string destination = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                        string root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("安装包包含无效路径。");
                        if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                output.Write(buffer, 0, read); copiedBytes += read;
                                int percent = totalBytes == 0 ? 70 : 3 + (int)(67.0 * copiedBytes / totalBytes);
                                if (percent != lastPercent) { Report(progress, percent, "Extracting files…"); lastPercent = percent; }
                            }
                        }
                    }
                }
            }

            // Validate the clean payload before changing the current install.
            // Overlay extraction leaves old 08..23.png files behind when an
            // animation changes from 24 to 8 frames, so replace the whole tree.
            Report(progress, 70, "Validating animation resources…");
            ValidatePayload(staging);
            Report(progress, 78, "Checking existing files…");
            if (File.Exists(PetExe))
            {
                Directory.CreateDirectory(backup);
                File.Copy(PetExe, backupExe);
                savedExe = true;
            }
            string stagedAssets = Path.Combine(staging, "assets");
            if (!AssetTreesMatch(stagedAssets, assets))
            {
                if (Directory.Exists(assets))
                {
                    Directory.CreateDirectory(backup);
                    Directory.Move(assets, backupAssets);
                    movedOldAssets = true;
                }
                Directory.Move(stagedAssets, assets);
                promotedAssets = true;
            }
            // Scene artwork is a separate resource tree, not part of assets/. Promote
            // it transactionally too; top-level file copying otherwise silently drops it.
            Report(progress, 84, "Installing verified files…");
            string stagedScenes=Path.Combine(staging,"scenes");
            if(!AssetTreesMatch(stagedScenes,scenes))
            {
                if(Directory.Exists(scenes))
                {
                    if((File.GetAttributes(scenes)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("场景目录不能是目录链接。");
                    Directory.CreateDirectory(backup);Directory.Move(scenes,backupScenes);movedOldScenes=true;
                }
                Directory.Move(stagedScenes,scenes);promotedScenes=true;
            }
            foreach (string file in Directory.GetFiles(staging, "*", SearchOption.TopDirectoryOnly))
                File.Copy(file, Path.Combine(InstallDirectory, Path.GetFileName(file)), true);
            CopyShellSupport(Path.Combine(staging, "shell"), Path.Combine(InstallDirectory, "shell"));
            Report(progress, 88, "Verifying installed application…");
            ValidatePayload(InstallDirectory);
            string source = Assembly.GetExecutingAssembly().Location;
            File.Copy(source, Path.Combine(InstallDirectory, "Uninstall.exe"), true);
            }
            catch
            {
                if(promotedScenes&&Directory.Exists(scenes))Directory.Move(scenes,Path.Combine(staging,"failed-scenes"));
                if(movedOldScenes&&Directory.Exists(backupScenes))Directory.Move(backupScenes,scenes);
                // Only remove the newly promoted tree from the active path;
                // never delete the old assets. Keep their backup on success.
                if (promotedAssets && Directory.Exists(assets)) Directory.Move(assets, Path.Combine(staging, "failed-assets"));
                if (movedOldAssets && Directory.Exists(backupAssets)) Directory.Move(backupAssets, assets);
                if (savedExe) File.Copy(backupExe, PetExe, true);
                throw;
            }
            finally
            {
                string stagingFull = Path.GetFullPath(staging);
                string installRoot = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!stagingFull.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(stagingFull).StartsWith(".payload-", StringComparison.Ordinal))
                    throw new InvalidOperationException("临时目录路径无效，未执行清理。");
                try { if (Directory.Exists(stagingFull)) Directory.Delete(stagingFull, true); } catch { }
            }
        }

        private static void ValidatePayload(string directory)
        {
            if(!File.Exists(Path.Combine(directory,"assets","default-companions.json")))throw new InvalidOperationException("安装资源缺少默认角色索引。");
            string scene=Path.Combine(directory,"scenes","sect-hall-v2.png");
            if(!File.Exists(scene))throw new InvalidOperationException("安装资源缺少 Agent 工作区背景图。");
            using(Image image=Image.FromFile(scene))
                if(image.Width<320||image.Height<200||image.Width>8192||image.Height>8192)throw new InvalidOperationException("Agent 工作区背景图校验失败。");
            using (Process test = Process.Start(new ProcessStartInfo(Path.Combine(directory, "agent-pal.exe"), "--self-test") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory }))
            {
                if (!test.WaitForExit(30000))
                {
                    try { test.Kill(); } catch { }
                    throw new InvalidOperationException("动画资源校验超时。校验目录：" + directory);
                }
                if (test.ExitCode != 0)
                    throw new InvalidOperationException("动画资源校验失败（退出码 " + test.ExitCode + "）。校验目录：" + directory + "。旧资源备份位于安装目录 asset-backups 中。");
            }
        }

        private static void CopyShellSupport(string source, string target)
        {
            if (!Directory.Exists(source)) return;
            if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("The shell support directory cannot be a link.");
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length + 1), destination = Path.Combine(target, relative);
                string parent = Path.GetDirectoryName(destination), check = parent;
                while (check.Length >= target.Length)
                {
                    if (Directory.Exists(check) && (File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0) throw new IOException("Shell support paths cannot contain directory links.");
                    if (check == target) break; check = Path.GetDirectoryName(check);
                }
                Directory.CreateDirectory(parent);
                if(File.Exists(destination)&&(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new IOException("Shell support files cannot be links.");
                if (File.Exists(destination))
                {
                    using (var hash = SHA256.Create())
                    using (var original = File.OpenRead(file))
                    using (var installed = File.OpenRead(destination))
                        if (Convert.ToBase64String(hash.ComputeHash(original)) == Convert.ToBase64String(hash.ComputeHash(installed))) continue;
                }
                // Native DLL names include their content hash, avoiding in-use
                // DLL replacement. Identical already-installed files are retained.
                File.Copy(file, destination, true);
            }
        }

        private static bool AssetTreesMatch(string expected, string installed)
        {
            if (!Directory.Exists(installed)) return false;
            // Do not follow user-created directory links outside the install.
            if ((File.GetAttributes(installed) & FileAttributes.ReparsePoint) != 0) return false;
            string[] oldFiles = RegularAssetFiles(installed);
            if (oldFiles == null) return false;
            string[] newFiles = Directory.GetFiles(expected, "*", SearchOption.AllDirectories);
            if (oldFiles.Length != newFiles.Length) return false;
            using (SHA256 hash = SHA256.Create())
                foreach (string file in newFiles)
                {
                    string old = Path.Combine(installed, file.Substring(expected.Length + 1));
                    if (!File.Exists(old) || new FileInfo(old).Length != new FileInfo(file).Length) return false;
                    byte[] oldHash, newHash;
                    using (Stream stream = File.OpenRead(old)) oldHash = hash.ComputeHash(stream);
                    using (Stream stream = File.OpenRead(file)) newHash = hash.ComputeHash(stream);
                    if (!String.Equals(Convert.ToBase64String(oldHash), Convert.ToBase64String(newHash), StringComparison.Ordinal)) return false;
                }
            return true;
        }

        private static string[] RegularAssetFiles(string root)
        {
            List<string> files = new List<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                foreach (string entry in Directory.GetFileSystemEntries(pending.Pop()))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return null;
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else files.Add(entry);
                }
            }
            return files.ToArray();
        }

        private static void RegisterStartup()
        {
            AgentPal.StartupRegistration.SetEnabled(true, PetExe);
        }

        private static string StartMenuFolder
        {
            get
            {
                // Never touch the real Start menu from an isolated installer test.
                string programs = TestMode ? Path.Combine(InstallDirectory, "test-start-menu") : Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (String.IsNullOrWhiteSpace(programs)) throw new InvalidOperationException("无法取得当前用户的开始菜单目录。");
                return Path.Combine(programs, AppName);
            }
        }

        private static string StartMenuShortcut { get { return Path.Combine(StartMenuFolder, "AgentPal.lnk"); } }

        // Keep these declarations in Win32 vtable order, including unused methods.
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
            void GetIDList(out IntPtr itemList);
            void SetIDList(IntPtr itemList);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out ushort hotkey);
            void SetHotkey(ushort hotkey);
            void GetShowCmd(out int showCommand);
            void SetShowCmd(int showCommand);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, out int iconIndex);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int iconIndex);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
            void Resolve(IntPtr owner, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        private static IShellLinkW CreateShellLink()
        {
            return (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true));
        }

        private static bool ShortcutBelongsToPet(string path, string executable)
        {
            IShellLinkW shortcut = CreateShellLink();
            try
            {
                ((IPersistFile)shortcut).Load(path, 0);
                var targetPath = new StringBuilder(260);
                shortcut.GetPath(targetPath, targetPath.Capacity, IntPtr.Zero, 0);
                string target = targetPath.ToString();
                return !String.IsNullOrWhiteSpace(target) && String.Equals(Path.GetFullPath(target), Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase);
            }
            finally { Marshal.FinalReleaseComObject(shortcut); }
        }

        private static void RegisterStartMenuShortcut()
        {
            Directory.CreateDirectory(StartMenuFolder);
            IShellLinkW shortcut = null;
            string temporary = Path.Combine(StartMenuFolder, ".orbit-" + Guid.NewGuid().ToString("N") + ".lnk");
            try
            {
                if (File.Exists(StartMenuShortcut) && !ShortcutBelongsToPet(StartMenuShortcut, PetExe))
                    throw new InvalidOperationException("开始菜单中存在指向其他程序的同名快捷方式，未覆盖：" + StartMenuShortcut);
                shortcut = CreateShellLink();
                shortcut.SetPath(PetExe);
                shortcut.SetWorkingDirectory(InstallDirectory);
                shortcut.SetDescription("AgentPal · 连接本机 Claude Code CLI");
                shortcut.SetShowCmd(1);
                // Versioned file avoids reusing Explorer's stale transparent icon.
                string icon = Path.Combine(InstallDirectory, "agent-pal-brand-v2.ico");
                shortcut.SetIconLocation(File.Exists(icon) ? icon : PetExe, 0);
                ((IPersistFile)shortcut).Save(temporary, true);
                Marshal.FinalReleaseComObject(shortcut); shortcut = null;
                if (File.Exists(StartMenuShortcut)) File.Replace(temporary, StartMenuShortcut, null);
                else File.Move(temporary, StartMenuShortcut);
                string previousLink=Path.Combine(StartMenuFolder,"agent-pal.lnk");
                if(File.Exists(previousLink)&&ShortcutBelongsToPet(previousLink,PetExe))File.Delete(previousLink);
                previousLink=Path.Combine(Path.GetDirectoryName(StartMenuFolder),"agent-pal","agent-pal.lnk");
                if(File.Exists(previousLink)&&ShortcutBelongsToPet(previousLink,PetExe))File.Delete(previousLink);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static void RemoveStartMenuShortcut()
        {
            if (!File.Exists(StartMenuShortcut)) return;
            if (ShortcutBelongsToPet(StartMenuShortcut, PetExe)) File.Delete(StartMenuShortcut);
            if (Directory.Exists(StartMenuFolder) && Directory.GetFileSystemEntries(StartMenuFolder).Length == 0)
                Directory.Delete(StartMenuFolder, false);
        }

        private static void RemoveLegacyShortcut()
        {
            string folder = Path.Combine(Path.GetDirectoryName(StartMenuFolder), "仙伴 AI桌面伴侣");
            string file = Path.Combine(folder, "仙伴 AI桌面伴侣.lnk");
            if (File.Exists(file) && ShortcutBelongsToPet(file, Path.Combine(InstallDirectory, LegacyExe))) File.Delete(file);
            if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder, false);
        }

        private static void RegisterUninstaller()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(InstallRegistry + AppName))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", AppName);
                key.SetValue("InstallLocation", InstallDirectory);
                key.SetValue("DisplayIcon", PetExe);
                key.SetValue("UninstallString", "\"" + Path.Combine(InstallDirectory, "Uninstall.exe") + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            foreach(string previousName in new[]{StartupRegistration.PreviousPackageName,"OrbitPet"})
            {
            string legacyPath = InstallRegistry + previousName;
            bool sameInstallation;
            using (RegistryKey legacy = Registry.CurrentUser.OpenSubKey(legacyPath))
                sameInstallation = legacy != null && String.Equals(legacy.GetValue("InstallLocation") as string, InstallDirectory, StringComparison.OrdinalIgnoreCase);
            if (sameInstallation) Registry.CurrentUser.DeleteSubKeyTree(legacyPath, false);
            }
        }

        private static void StopPet()
        {
            foreach (string executable in new[] { PetExe, Path.Combine(InstallDirectory, LegacyExe) })
            foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
            {
                try
                {
                    // Never stop another installation merely because its process name matches.
                    if (!String.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase)) continue;
                    process.CloseMainWindow(); if (!process.WaitForExit(1500)) process.Kill();
                }
                catch { }
            }
        }

        private static void Uninstall()
        {
            DialogResult answer = MessageBox.Show("确定卸载 AgentPal 并移除启动项和快捷方式吗？", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                ValidateInstallLocation(true);
                StopPet();
                // Leave existing Claude settings and legacy hooks untouched.
                RemoveStartMenuShortcut();
                if(File.Exists(PetExe))
                {
                    using(Process cleanup=Process.Start(new ProcessStartInfo(PetExe,"--remove-quick-commands") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}))
                        if(!cleanup.WaitForExit(10000)||cleanup.ExitCode!=0)throw new IOException("Unable to remove this installation's folder context menus.");
                }
                AgentPal.StartupRegistration.SetEnabled(false, PetExe);
                Registry.CurrentUser.DeleteSubKeyTree(InstallRegistry + AppName, false);
                // Do not recursively delete a user-writable install tree. It may
                // contain user documents, old asset backups, or directory links.
                // Remove integration only and let the user review remaining files.
                MessageBox.Show("已移除开机启动和快捷方式；Claude 配置保持原样。\n为保留可能存在的用户文件，程序目录未自动删除，可在关闭本窗口后自行清理：\n" + InstallDirectory, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("卸载失败：\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ValidateInstallLocation(bool requireOwned)
        {
            string target = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] broad = { Path.GetPathRoot(target), profile,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
            foreach (string path in broad)
                if (!String.IsNullOrEmpty(path) && (String.Equals(target, Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                    || Path.GetFullPath(path).StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("安装目录过于宽泛，请选择独立应用目录。");
            for (DirectoryInfo cursor = new DirectoryInfo(target); cursor != null; cursor = cursor.Parent)
                if (cursor.Exists && (cursor.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("安装路径不能包含目录链接。");
            bool owned = IsOwned(target);
            if ((requireOwned || (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length > 0)) && !owned)
                throw new InvalidOperationException("此目录不属于本应用，请选择一个空目录。");
        }

    }

}
