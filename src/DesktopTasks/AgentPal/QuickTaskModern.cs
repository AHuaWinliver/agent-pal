// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AgentPal
{
    internal static class QuickTaskModern
    {
        internal const string RegistryPath = @"Software\agent-pal\ShellCommands";
        internal const string Marker = "agent-pal-native-shell-v1";
        internal const int MaximumBindings = 64;
        [DllImport("kernel32.dll")]private static extern bool IsWow64Process2(IntPtr process,out ushort processMachine,out ushort nativeMachine);
        internal static bool Supported
        {
            get { using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) { int build;if(!Environment.Is64BitOperatingSystem||key==null||!Int32.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build)||build<22000)return false;ushort process,native;return IsWow64Process2(new IntPtr(-1),out process,out native)&&native==0x8664; } }
        }
        internal static string PackageRoot { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shell"); } }
        internal static bool PackageAvailable { get { return File.Exists(Path.Combine(PackageRoot, "agent-pal.ContextMenu.msix")) && File.Exists(Path.Combine(PackageRoot, "RegisterShell.ps1")); } }
        internal static bool NeedsPreviewTrust
        {
            get
            {
                string file = Path.Combine(PackageRoot, "agent-pal-preview.cer"); if (!File.Exists(file)) return false;
                using (var certificate = new X509Certificate2(file))
                using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
                { store.Open(OpenFlags.ReadOnly); return store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count == 0; }
            }
        }
        internal static void Register(bool trustPreview)
        {
            if (!Supported) throw new InvalidOperationException(UiLanguage.Text("Windows 11 主菜单扩展需要 64 位 Windows 11。"));
            if (!PackageAvailable) throw new FileNotFoundException(UiLanguage.Text("缺少原生菜单扩展，请安装包含 shell 组件的新版安装包。"));
            RunRegistration("Register", trustPreview);
        }
        internal static void RunRegistration(string action, bool trustPreview)
        {
            string helper = Path.Combine(PackageRoot, "RegisterShell.ps1");
            if (!File.Exists(helper)) { if (action == "Remove") return; throw new FileNotFoundException(helper); }
            string report = Path.Combine(Program.StateDirectory, "shell-registration-" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(Program.StateDirectory);
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(powershell, "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(helper) +
                " -Action " + action + " -ApplicationRoot " + Quote(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + @"\.") +
                " -StateDirectory " + Quote(Program.StateDirectory.TrimEnd('\\') + @"\.") + " -Report " + Quote(report) +
                " -ExpectedUser " + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value +
                (trustPreview ? " -TrustPreviewCertificate" : "")) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            if (trustPreview) info.Verb = "runas";
            try
            {
                using (Process process = Process.Start(info))
                {
                    // Elevated deployment may require user interaction; do not kill it on a short timer.
                    process.WaitForExit();
                    var result = WorkshopFiles.Load<RegistrationResult>(report);
                    if (process.ExitCode != 0 || !result.Success) throw new InvalidOperationException(String.IsNullOrWhiteSpace(result.Error) ? UiLanguage.Text("原生菜单注册未完成，请查看系统授权或重试。") : result.Error);
                }
            }
            finally { if (File.Exists(report)) File.Delete(report); }
        }
        internal sealed class RegistrationResult { public bool Success=false; public string Error = ""; }
        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        internal static void ApplyMappings(RegistryKey root, string path, QuickTaskOptions options, string executable)
        {
            ValidateBindings(options);
            if(!Path.IsPathRooted(executable)||!File.Exists(executable))throw new FileNotFoundException(executable);
            using (RegistryKey existing = root.OpenSubKey(path))
                if (existing != null && Convert.ToString(existing.GetValue("Owner")) != Marker) throw new IOException("Native menu registry path belongs to another application.");
            // Build a complete replacement in memory before updating any active slot.
            var bindings = new List<KeyValuePair<QuickTaskCommand, QuickTaskBinding>>();
            foreach (QuickTaskCommand command in options.Commands) foreach (QuickTaskBinding pet in command.Companions) bindings.Add(new KeyValuePair<QuickTaskCommand, QuickTaskBinding>(command, pet));
            using (RegistryKey shell = root.CreateSubKey(path))
            {
                shell.SetValue("Owner", Marker); shell.SetValue("Executable", executable);
                shell.SetValue("Title",UiLanguage.Text("发送Agent Pal任务"));
                string applicationIcon=Path.Combine(Path.GetDirectoryName(executable),"agent-pal-brand-v2.ico");
                shell.SetValue("Icon",File.Exists(applicationIcon)?applicationIcon:executable+",0");
                // Disable all slots first so open menus cannot invoke partly rewritten bindings.
                foreach (string name in shell.GetSubKeyNames()) using (RegistryKey slot = shell.OpenSubKey(name, true)) { slot.SetValue("Folders", 0); slot.SetValue("Desktop", 0); }
                for (int i = 0; i < bindings.Count; i++)
                using (RegistryKey slot = shell.CreateSubKey(i.ToString("00")))
                {
                    QuickTaskCommand command = bindings[i].Key; QuickTaskBinding pet = bindings[i].Value;
                    slot.SetValue("Title", QuickTaskShell.Caption(command, pet)); slot.SetValue("Icon", pet.IconPath ?? ""); slot.SetValue("Executable", executable);
                    slot.SetValue("Command", command.Id); slot.SetValue("Companion", pet.Id);
                    slot.SetValue("Folders", options.Enabled && options.Folders ? 1 : 0); slot.SetValue("Desktop", options.Enabled && options.Desktop ? 1 : 0);
                }
            }
        }
        internal static void ValidateBindings(QuickTaskOptions options)
        {QuickTaskCommands.Validate(options);int count=0;foreach(QuickTaskCommand command in options.Commands)count+=command.Companions.Count;if(count>MaximumBindings)throw new ArgumentException(UiLanguage.Format("主菜单最多支持 {0} 个伙伴任务入口。",MaximumBindings));}
        internal static void RemoveMappings(RegistryKey root, string path, string expectedExecutable = null)
        {
            using (RegistryKey key = root.OpenSubKey(path))
            {
                if (key == null || Convert.ToString(key.GetValue("Owner")) != Marker) return;
                if (expectedExecutable != null && !String.Equals(Convert.ToString(key.GetValue("Executable")), expectedExecutable, StringComparison.OrdinalIgnoreCase)) return;
            }
            root.DeleteSubKeyTree(path, false);
        }
        internal static string ClassId(int slot, int context) { return "BC365B20-422D-4FEF-B90B-570000" + (slot * 3 + context + 1).ToString("X6"); }
        internal static string RegisteredExecutable(RegistryKey root,string path)
        {using(RegistryKey key=root.OpenSubKey(path))return key!=null&&Convert.ToString(key.GetValue("Owner"))==Marker?Convert.ToString(key.GetValue("Executable")):null;}
    }
}
