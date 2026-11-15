// SPDX-License-Identifier: Apache-2.0
using System.Text;

namespace AgentPal.Linux;

internal static class LinuxStartup
{
    private const string Marker = "X-agent-pal-Managed=true";
    private const string LegacyMarker = "X-XianPal-Managed=true";
    internal static string EntryPath => Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") == "1"
        ? Path.Combine(Program.StateDirectory, "autostart", "agent-pal.desktop")
        : Path.Combine(Path.GetDirectoryName(NativePaths.UserDirectory("XDG_CONFIG_HOME", ".config")), "autostart", "agent-pal.desktop");

    private static string LegacyEntry(string path) => Path.Combine(Path.GetDirectoryName(path), "xianpal.desktop");
    private static bool Managed(string path, string marker) =>
        File.Exists(path) && new FileInfo(path).LinkTarget == null && File.ReadLines(path).Contains(marker);
    private static bool Active(string path, string marker) =>
        Managed(path, marker) && !File.ReadLines(path).Contains("Hidden=true");
    private static void RemoveLegacyEntry(string path)
    {
        string legacy = LegacyEntry(path);
        if (Managed(legacy, LegacyMarker)) File.Delete(legacy);
    }

    internal static bool Enabled => File.Exists(EntryPath)
        ? Active(EntryPath, Marker) : Active(LegacyEntry(EntryPath), LegacyMarker);

    // Desktop-entry values are unescaped before Exec's quoted arguments are parsed.
    // https://specifications.freedesktop.org/desktop-entry/latest/exec-variables.html
    internal static string ExecArgument(string value)
    {
        if (value.IndexOfAny(new[] { '\n', '\r', '\0' }) >= 0) throw new ArgumentException("A desktop entry path cannot contain line breaks.");
        string quoted = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%");
        return "\"" + quoted.Replace("\\", "\\\\") + "\"";
    }

    internal static string Entry(string command, IEnumerable<string> arguments, string icon)
    {
        if (command.Contains('=')) throw new ArgumentException("Desktop entry executable paths cannot contain '='.");
        string exec = string.Join(" ", new[] { command }.Concat(arguments).Select(ExecArgument));
        string iconValue = icon.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        return "[Desktop Entry]\nType=Application\nName=agent-pal\nName[zh_CN]=agent-pal\nComment=An animated desktop companion for Claude Code\nExec=" + exec +
            "\nIcon=" + iconValue + "\nTerminal=false\nCategories=Utility;\n" + Marker + "\n";
    }

    internal static void SetEnabled(bool enabled)
    {
        string path = EntryPath;
        if (File.Exists(path) && (new FileInfo(path).LinkTarget != null || !File.ReadAllText(path).Contains(Marker)))
            throw new IOException("An existing autostart entry is not managed by agent-pal.");
        if (!enabled) { if (File.Exists(path)) File.Delete(path); RemoveLegacyEntry(path); return; }
        var self = LinuxTerminal.SelfCommand();
        string content = Entry(self.Command, self.Arguments, Path.Combine(AppContext.BaseDirectory, "agent-pal.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        RemoveLegacyEntry(path);
    }

    internal static void RegisterInstallation(string directory)
    {
        string dataRoot = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") == "1" ? Path.Combine(Program.StateDirectory, "desktop-integration") :
            Path.GetDirectoryName(NativePaths.UserDirectory("XDG_DATA_HOME", ".local", "share"));
        string entry = Entry("/bin/sh", new[] { Path.Combine(directory, "launch.sh") }, Path.Combine(directory, "agent-pal.png"));
        string launcher = Path.Combine(dataRoot, "applications", "agent-pal.desktop");
        WriteManaged(launcher, entry);
        if (Enabled) WriteManaged(EntryPath, entry);
        RemoveLegacyEntry(launcher);
        RemoveLegacyEntry(EntryPath);
    }

    private static void WriteManaged(string path, string content)
    {
        if (File.Exists(path) && (new FileInfo(path).LinkTarget != null || !File.ReadAllText(path).Contains(Marker)))
            throw new IOException("An existing desktop entry is not managed by agent-pal.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
