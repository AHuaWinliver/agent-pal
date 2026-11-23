using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
#if AGENT_PAL_LINUX
using JavaScriptSerializer = AgentPal.Linux.CompatibleJson;
#else
using System.Web.Script.Serialization;
#endif

namespace AgentPal
{
    // Optional side-channel for our own native CLI window. The CLI retains
    // its terminal handles; neither stdin nor its TUI output is redirected.
    internal static class NativeTaskHooks
    {
        internal static bool IsNativeRun { get { return !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_PAL_PET_NATIVE_RUN_LOG")); } }

#if !AGENT_PAL_LINUX
        // Never persist integration in a user's global/project Claude settings.
        // This private overlay lives beside one owned run and is passed with --settings.
        internal static string CreateSessionSettings(string selectedPath, string logPath)
        {
            string log=Path.GetFullPath(logPath);
            string owned=Path.GetFullPath(Path.Combine(Program.StateDirectory,"scheduled-task-runs"));
            if(!String.Equals(Path.GetDirectoryName(log),owned,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Native session settings must belong to an AgentPal run.");
            var selected=ClaudeSettingsFile.Read(selectedPath);
            object value;
            Dictionary<string,object> hooks;
            if(selected.TryGetValue("hooks",out value))
            {
                hooks=value as Dictionary<string,object>;
                if(hooks==null)throw new ArgumentException("Claude hooks must be a JSON object.");
            }
            else selected["hooks"]=hooks=new Dictionary<string,object>();
            foreach(string name in new[]{"SessionStart","UserPromptSubmit","PreToolUse","PostToolUse","PostToolUseFailure","PermissionRequest","Notification","MessageDisplay","Stop","StopFailure","SessionEnd"})
            {
                var groups=new List<object>();
                if(hooks.TryGetValue(name,out value))
                {
                    var existing=value as System.Collections.IEnumerable;
                    if(existing==null||value is string)throw new ArgumentException("Claude hook groups must be arrays.");
                    foreach(object group in existing)groups.Add(group);
                }
                var item=new Dictionary<string,object>();
                if(name=="Notification")item["matcher"]="permission_prompt|idle_prompt|agent_needs_input|elicitation_dialog";
                item["hooks"]=new object[]{new Dictionary<string,object>{{"type","command"},{"command",BuildHookCommand(name)},{"timeout",5}}};
                groups.Add(item);hooks[name]=groups.ToArray();
            }
            string output=log+".native-settings.json";
            if(!String.IsNullOrEmpty(selectedPath)&&String.Equals(Path.GetFullPath(selectedPath),output,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The selected settings cannot be the generated run overlay.");
            string json=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Serialize(selected);
            if(Encoding.UTF8.GetByteCount(json)>2*1024*1024)throw new ArgumentException("Native session settings exceed 2 MiB.");
            Directory.CreateDirectory(owned);
            File.WriteAllText(output,json,new UTF8Encoding(false));return output;
        }
        private static string BuildHookCommand(string eventName)
        {
            string executable=typeof(Program).Assembly.Location.Replace('\\','/').Replace("'","''");
            string script="$ProgressPreference='SilentlyContinue'; $OutputEncoding=New-Object Text.UTF8Encoding($false); "+
                "$reader=New-Object IO.StreamReader([Console]::OpenStandardInput(),(New-Object Text.UTF8Encoding($false))); "+
                "$json=$reader.ReadToEnd(); $json | & '"+executable+"' --hook "+eventName;
            return "powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        }
#endif

        internal static void Publish(string eventName, string input)
        {
            try
            {
                if (Environment.GetEnvironmentVariable("AGENT_PAL_PET_MANAGED_SESSION") != "1") return;
                string log = Path.GetFullPath(Environment.GetEnvironmentVariable("AGENT_PAL_PET_NATIVE_RUN_LOG"));
                string directory = Path.GetFullPath(Path.Combine(Program.StateDirectory, "scheduled-task-runs"));
                if (!String.Equals(Path.GetDirectoryName(log), directory,
#if AGENT_PAL_LINUX
                    StringComparison.Ordinal
#else
                    StringComparison.OrdinalIgnoreCase
#endif
                    )) return;
                string token = Environment.GetEnvironmentVariable("AGENT_PAL_PET_NATIVE_RUN_TOKEN");
                Guid id; if (!Guid.TryParseExact(token, "N", out id)) return;
                JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 8000000 };
                Dictionary<string, object> owner = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(log + ".interactive", Encoding.UTF8));
                if (Text(owner, "Token") != token || Text(owner, "TaskId") != Environment.GetEnvironmentVariable("AGENT_PAL_PET_INTERNAL_TASK_ID")) return;
                Dictionary<string, object> payload = json.Deserialize<Dictionary<string, object>>(input);
                string session = Text(payload, "session_id");
                // Child-agent hooks can inherit env vars but have a different
                // session. They must not impersonate this scheduled conversation.
                if (session != Text(owner, "SessionId")) return;
                using (Mutex gate = new Mutex(false, "Local\\OrbitNativeRun-" + token))
                {
                    bool acquired;
                    try { acquired = gate.WaitOne(750); } catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) return;
                    try
                    {
                        string status = "Claude CLI 交互中", reaction = "PostToolUse", output = "";
                        if (eventName == "SessionStart") { status = "Claude 原生 CLI 已连接"; output = "[原生 CLI 已连接]\r\n"; }
                        else if (eventName == "UserPromptSubmit")
                        {
                            status = "Claude 正在处理你的输入…"; reaction = "UserPromptSubmit";
                            File.WriteAllText(log + ".reply", "", new UTF8Encoding(false));
                            output = "\r\n[你] " + Text(payload, "prompt") + "\r\n";
                        }
                        else if (eventName == "MessageDisplay")
                        {
                            status = "Claude 正在回复…";
                            string delta = Text(payload, "delta");
                            File.AppendAllText(log + ".reply", delta, new UTF8Encoding(false)); output = delta;
                        }
                        else if (eventName == "PreToolUse")
                        {
                            status = "Claude 正在使用工具：" + Text(payload, "tool_name"); reaction = "PreToolUse";
                            object tool; payload.TryGetValue("tool_input", out tool);
                            output = "\r\n[工具] " + Text(payload, "tool_name") + "\r\n" + UiText.TailCodeUnits(tool == null ? "" : json.Serialize(tool), 2000) + "\r\n";
                        }
                        else if (eventName == "PostToolUse" || eventName == "PostToolUseFailure")
                        {
                            bool failed = eventName == "PostToolUseFailure";
                            status = failed ? "Claude 工具执行遇到问题" : "Claude 工具已返回";
                            reaction = failed ? "PostToolUseFailure" : "PostToolUse";
                            object response; payload.TryGetValue("tool_response", out response);
                            output = (failed ? "\r\n[工具失败] " + Text(payload, "error") : "\r\n[工具结果] " + (response == null ? "" : json.Serialize(response))) + "\r\n";
                            output = UiText.TailCodeUnits(output, 2400);
                        }
                        else if (eventName == "PermissionRequest" || eventName == "Notification")
                        { status = "请在 Claude CLI 窗口确认或输入"; reaction = "PermissionRequest"; output = "\r\n[等待交互] " + Text(payload, "message") + "\r\n"; }
                        else if (eventName == "Stop" || eventName == "StopFailure")
                        {
                            bool failed = eventName == "StopFailure";
                            status = failed ? "Claude 本轮失败；CLI 保持打开" : "本轮回复完成；CLI 等待输入";
                            reaction = failed ? "StopFailure" : "Stop";
                            string reply = Text(payload, "last_assistant_message");
                            string streamed = File.Exists(log + ".reply") ? File.ReadAllText(log + ".reply", Encoding.UTF8) : "";
                            if (reply.Length > 0 && !streamed.EndsWith(reply, StringComparison.Ordinal))
                                output = reply.StartsWith(streamed, StringComparison.Ordinal) ? reply.Substring(streamed.Length) : reply;
                            output += "\r\n[本轮结束，仍可在原生 CLI 中继续对话]\r\n";
                        }
                        else if (eventName == "SessionEnd") { status = "Claude CLI 会话已退出"; reaction = "Stop"; }
                        else return;
                        if (output.Length > 0) File.AppendAllText(log, output, new UTF8Encoding(false));
                        File.WriteAllText(log + ".state", status + "\r\n" + session + "\r\n" + reaction, new UTF8Encoding(false));
                    }
                    finally { gate.ReleaseMutex(); }
                }
            }
            catch { /* Hooks must never break the user's native CLI session. */ }
        }

        private static string Text(Dictionary<string, object> data, string key)
        { object value; return data != null && data.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : ""; }
    }
}
