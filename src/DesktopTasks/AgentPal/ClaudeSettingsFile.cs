// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if AGENT_PAL_LINUX
using JavaScriptSerializer = AgentPal.Linux.CompatibleJson;
#else
using System.Web.Script.Serialization;
#endif

namespace AgentPal
{
    internal static class ClaudeSettingsFile
    {
        private const int MaximumBytes = 2 * 1024 * 1024;

        internal static string CurrentPath(string characterId = null)
        {
            IntegrationPreferences preferences = IntegrationPreferences.Load();
            return Validate(preferences.EffectiveSettingsPathFor(characterId));
        }

        internal static string Validate(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            string absolute = Path.GetFullPath(path);
            Read(absolute);
            return absolute;
        }

        internal static Dictionary<string, object> Read(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return new Dictionary<string, object>();
            if (!String.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请选择 Claude Code 的 JSON 配置文件。");
            string json;
            using (FileStream file = File.OpenRead(path))
            {
                if (file.Length > MaximumBytes) throw new ArgumentException("Claude 配置文件不能超过 2 MiB。");
                using (var reader = new StreamReader(file, Encoding.UTF8)) json = reader.ReadToEnd();
            }
            Dictionary<string, object> settings;
            try { settings = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json); }
            catch (ArgumentException) { throw new ArgumentException("Claude 配置文件必须是有效的 JSON 对象。"); }
            catch (InvalidOperationException) { throw new ArgumentException("Claude 配置文件必须是有效的 JSON 对象。"); }
            if (settings == null) throw new ArgumentException("Claude 配置文件必须是有效的 JSON 对象。");
            object value;
            if (settings.TryGetValue("env", out value))
            {
                var environment = value as Dictionary<string, object>;
                if (environment == null) throw new ArgumentException("Claude 配置中的 env 必须是 JSON 对象。");
                foreach (string name in environment.Keys)
                    if (name.StartsWith("AGENT_PAL_PET_", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Claude 配置中的 env 不能定义agent-pal内部会话标记（AGENT_PAL_PET_）。");
            }
            return settings;
        }

        internal static string PowerShellSetup(string path, string command)
        {
            if (String.IsNullOrEmpty(path)) return "";
            // Windows PowerShell routes batch shims through cmd.exe. Expand
            // their path once inside quotes so & and % stay filename data.
            return "$agentPalSettings='" + path.Replace("'", "''") + "'; " +
                "$agentPalCli=Get-Command -Name '" + command.Replace("'", "''") + "' -ErrorAction Stop; " +
                "if($agentPalCli.CommandType -eq 'Alias'){$agentPalCli=$agentPalCli.ResolvedCommand}; " +
                "if($agentPalCli.CommandType -eq 'Application' -and $agentPalCli.Extension -in @('.cmd','.bat')){ " +
                "$env:AGENT_PAL_PET_SETTINGS_FILE=$agentPalSettings; $agentPalSettings='\"%AGENT_PAL_PET_SETTINGS_FILE%\"'}; ";
        }
    }
}
