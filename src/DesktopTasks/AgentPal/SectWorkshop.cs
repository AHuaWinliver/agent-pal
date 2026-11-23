// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#if AGENT_PAL_LINUX
using JavaScriptSerializer = AgentPal.Linux.CompatibleJson;
#else
using System.Web.Script.Serialization;
#endif

namespace AgentPal
{
    internal sealed class SectMember
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Directory { get; set; }
        public string CharacterId { get; set; }
    }
    internal sealed class SectStep
    {
        public string Id { get; set; }
        public string Member { get; set; }
        public string DependsOn { get; set; }
        public string Task { get; set; }
        public string ExecutionMode { get; set; }
    }
    internal sealed class CompanionSect
    {
        public string Id="";
        internal const int DefaultRunLimitMinutes = 240;
        internal const int DefaultStepLimitMinutes = 10;
        internal const int MaximumLimitMinutes = 1440;
        internal const int MaximumMembers = 12;
        internal const int MaximumSteps = 24;
        public int MaxParallel=2;
        public bool AutoHandoff=true;
        public string Name { get; set; }
        public string Goal { get; set; }
        public string TemplateId { get; set; }
        public int RunLimitMinutes = DefaultRunLimitMinutes;
        public int StepLimitMinutes = DefaultStepLimitMinutes;
        public List<SectMember> Members = new List<SectMember>();
        public List<SectStep> Steps = new List<SectStep>();
        public override string ToString() { return Name; }
    }
    internal sealed class PlannedEvent
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Title { get; set; }
        public DateTime When { get; set; }
        public string Repeat { get; set; }
        public string Sect { get; set; }
        public bool Done { get; set; }
        public static DateTime Next(DateTime due, DateTime now, string repeat)
        {
            if (repeat != "daily" && repeat != "weekly") return DateTime.MaxValue;
            int days = repeat == "daily" ? 1 : 7;
            double jumps = Math.Floor((now - due).TotalDays / days) + 1;
            return due.AddDays(Math.Max(1, jumps) * days);
        }
    }
    internal sealed class SectRun
    {
        public string Id=Guid.NewGuid().ToString("N"),Goal="";
        public bool Paused;
        public string Sect = "", Started = "", Finished = "", Status = "running";
        public int RunLimitMinutes = CompanionSect.DefaultRunLimitMinutes;
        public int StepLimitMinutes = CompanionSect.DefaultStepLimitMinutes;
        public List<SectStepRun> Steps = new List<SectStepRun>();
        public List<SectTransfer> Transfers = new List<SectTransfer>();
        public List<SectRunMember> Members = new List<SectRunMember>();
    }
    internal sealed class SectTransfer
    {
        public string FromMember="",ToMember="";
        public string From = "", To = "", Text = "", SentAt = "";
    }
    internal sealed class SectStepRun
    {
        public string MemberId="";
        public string Process="";
        public List<SectAttempt> PreviousAttempts=new List<SectAttempt>();
        public string WaitReason="";
        public string FailureKind="";
        public string Updated="";
        public int Attempt=1;
        public string Id = "", Member = "", Directory = "", Prompt = "", Output = "", Status = "pending";
        public string Task = "", Started = "";
        public int ExitCode;
        public string CharacterId = "orbit";
    }
    internal sealed class WorkshopData
    {
        public List<CompanionSect> Sects = new List<CompanionSect>();
        public List<PlannedEvent> Events = new List<PlannedEvent>();
        public List<SectRun> History = new List<SectRun>();
    }
    internal static class WorkshopFiles
    {
        internal static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, new JavaScriptSerializer { MaxJsonLength = 8388608 }.Serialize(data), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
        internal static T Load<T>(string path) where T : new()
        {
            if (!File.Exists(path)) return new T();
            // Never silently replace an unreadable plan with an empty document.
            if (new FileInfo(path).Length > 8388608) throw new IOException("规划文件过大，请先备份整理。");
            T data = new JavaScriptSerializer { MaxJsonLength = 8388608 }.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
            if (data == null) throw new IOException("规划文件无效，原文件已保留。");
            return data;
        }
    }
    internal interface ISectAgent : IDisposable
    {
        event Action<string> Output;
        event Action<int> Completed;
        void Start(string prompt, string directory, string marker, string characterId);
        void Stop();
    }
    internal interface ISectAgentProgress { event Action<string> Process; }
    internal interface ISectAgentFailure { string FailureKind { get; } }
    internal sealed class LocalSectAgent : ISectAgent,ISectAgentProgress,ISectAgentFailure
    {
        private readonly ClaudeCliRunner runner = new ClaudeCliRunner();
        public event Action<string> Process;
        public event Action<string> Output;
        public event Action<int> Completed;
        public string FailureKind { get { return runner.FailureKind; } }
        internal LocalSectAgent()
        {
            runner.SeparateTaskProcess=true;
            runner.OutputReceived += delegate(string s) { if (Output != null) Output(s); };
            runner.ProcessReceived += delegate(string s) { if(Process!=null)Process(s); };
            runner.Completed += delegate(int code) { if (Completed != null) Completed(code); };
        }
        public void Start(string prompt, string directory, string marker, string characterId) { runner.Start(prompt, directory, null, marker, characterId); }
        public void Stop() { runner.Stop(); }
        public void Dispose() { runner.Dispose(); }
    }
    // UI-thread owned dependency scheduler. Worker callbacks only enqueue immutable messages.
    internal sealed partial class SectExecution : IDisposable
    {
        private sealed class Live
        {
            internal ISectAgent Agent;
            internal SectStepRun Record;
            internal DateTime Deadline;
        }
        private readonly Func<ISectAgent> factory;
        private readonly Queue<Action> messages = new Queue<Action>();
        private readonly Dictionary<string, Live> live = new Dictionary<string, Live>();
        private readonly CompanionSect snapshot;
        internal CompanionSect Plan {get{return snapshot;}}
        private DateTime? runDeadline;
        private bool disposed, cancelled;
        internal readonly SectRun Record = new SectRun();
        internal bool IsRunning { get { return Record.Status == "running"; } }
        internal void SetPaused(bool value)
        {
            if(!IsRunning)return;
            Record.Paused=value;
        }
        internal bool RetryFailed(DateTime utc)
        {
            if(IsRunning||disposed||Record.Status!="failed")return false;
            bool retry=false;
            foreach(SectStepRun step in Record.Steps)
                if(step.Status=="failed"||step.Status=="blocked")
                {
                    if(step.Attempt>=3)return false;
                    retry=true;
                }
            if(!retry)return false;
            foreach(SectStepRun step in Record.Steps)
                if(step.Status=="failed"||step.Status=="blocked")
                {
                    step.Output=Limit(step.Output+"\n[用户重新执行，保留此前输出]\n");
                    step.Status="pending";step.Started="";step.Attempt++;
                }
            Record.Status="running";Record.Paused=false;Record.Finished="";
            runDeadline=snapshot.RunLimitMinutes==0?DateTime.MaxValue:utc.AddMinutes(snapshot.RunLimitMinutes);
            return true;
        }
        internal SectExecution(CompanionSect sect, Func<ISectAgent> factory)
        {
            Validate(sect);
            snapshot = new JavaScriptSerializer().Deserialize<CompanionSect>(new JavaScriptSerializer().Serialize(sect));
            this.factory = factory;
            Record.Sect = sect.Name;Record.Goal=sect.Goal??""; Record.Started = DateTime.Now.ToString("o");
            Record.RunLimitMinutes = snapshot.RunLimitMinutes;
            Record.StepLimitMinutes = snapshot.StepLimitMinutes;
            foreach (SectMember member in snapshot.Members) Record.Members.Add(new SectRunMember { Id = member.Id, Name = member.Name, CharacterId = Character(member) });
            foreach (SectStep s in snapshot.Steps)
            {
                SectMember m = Member(s.Member);
                Record.Steps.Add(new SectStepRun { Id = s.Id, MemberId=m.Id, Member = m.Name, Task = s.Task, Directory = Path.GetFullPath(m.Directory), CharacterId = Character(m) });
            }
        }
        internal static string[] Dependencies(string text)
        { return (text ?? "").Split(new[] { ',', '，', ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries); }
        internal static void Validate(CompanionSect sect)
        {
            if (sect == null || String.IsNullOrWhiteSpace(sect.Name) || sect.Name.Length > 60) throw new ArgumentException("请填写 Agent 团队名称（最多60字）。");
            if(sect.MaxParallel<1||sect.MaxParallel>8)throw new ArgumentException(UiLanguage.Text("并发工位应为 1–8。"));
            if (sect.RunLimitMinutes < 0 || sect.RunLimitMinutes > CompanionSect.MaximumLimitMinutes ||
                sect.StepLimitMinutes < 0 || sect.StepLimitMinutes > CompanionSect.MaximumLimitMinutes)
                throw new ArgumentException(UiLanguage.Text("请填写有效的协作时限。"));
            if ((sect.Goal ?? "").Length > 4000 || (sect.TemplateId == "four-companions-v1" && String.IsNullOrWhiteSpace(sect.Goal))) throw new ArgumentException("请填写共同目标、允许修改的范围和验收要求（最多4000字）。");
            if (sect.Members == null || sect.Members.Count < 1 || sect.Members.Count > CompanionSect.MaximumMembers || sect.Steps == null || sect.Steps.Count < 1 || sect.Steps.Count > CompanionSect.MaximumSteps)
                throw new ArgumentException("每个 Agent 团队需要1–12位成员、1–24项任务。");
            var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var steps = new Dictionary<string, SectStep>(StringComparer.OrdinalIgnoreCase);
            foreach (SectMember m in sect.Members)
            {
                if (m == null || !ValidId(m.Id) || !members.Add(m.Id) || String.IsNullOrWhiteSpace(m.Name) || m.Name.Length > 60 || String.IsNullOrWhiteSpace(m.Role) || m.Role.Length > 4000)
                    throw new ArgumentException("成员编号须唯一，使用英文、数字或短横线；姓名和职责不能为空。");
                if (!System.IO.Directory.Exists(m.Directory)) throw new ArgumentException("成员工作目录不存在：" + m.Name);
                if (!memberNames.Add(m.Name)) throw new ArgumentException("成员姓名须唯一，方便大堂区分成员。");
            }
            foreach (SectStep s in sect.Steps)
            {
                if (s!=null&&!String.IsNullOrEmpty(s.ExecutionMode) && Array.IndexOf(new[] { "auto", "read-only", "isolated", "serial" }, s.ExecutionMode) < 0)
                    throw new ArgumentException(UiLanguage.Text("任务执行方式无效。"));
                if (s == null || !ValidId(s.Id) || steps.ContainsKey(s.Id) || !members.Contains(s.Member ?? "") || String.IsNullOrWhiteSpace(s.Task) || s.Task.Length > 8000)
                    throw new ArgumentException("任务编号须唯一，成员编号须存在，任务内容需为1–8000字。");
                steps.Add(s.Id, s);
            }
            var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int pass = 0; pass < steps.Count; pass++)
                foreach (SectStep s in sect.Steps)
                {
                    bool ready = true;
                    foreach (string d in Dependencies(s.DependsOn)) { if (!steps.ContainsKey(d)) throw new ArgumentException("依赖任务不存在：" + d); if (!resolved.Contains(d)) ready = false; }
                    if (ready) resolved.Add(s.Id);
                }
            if (resolved.Count != steps.Count) throw new ArgumentException("任务依赖形成循环，请修改。");
        }
        internal static bool ValidId(string s) { return s != null && System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-zA-Z0-9-]{1,32}$"); }
        private SectMember Member(string id) { return snapshot.Members.Find(delegate(SectMember m) { return String.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase); }); }
        private SectStepRun Step(string id) { return Record.Steps.Find(delegate(SectStepRun r) { return String.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase); }); }
        private void Enqueue(Action message) { lock (messages) { if (!disposed) messages.Enqueue(message); } }
        internal void Pump(DateTime utc)
        {
            if (disposed || !IsRunning) return;
            if (!runDeadline.HasValue) runDeadline = snapshot.RunLimitMinutes==0?DateTime.MaxValue:utc.AddMinutes(snapshot.RunLimitMinutes);
            while (true) { Action a; lock (messages) { if (messages.Count == 0) break; a = messages.Dequeue(); } a(); }
            if (!cancelled && utc >= runDeadline.Value &&
                (live.Count > 0 || Record.Steps.Exists(delegate(SectStepRun step) { return step.Status == "pending"; })))
            {
                foreach (string id in new List<string>(live.Keys))
                    StopStep(id, UiLanguage.Text("已达到整次协作时限。"));
                foreach (SectStepRun step in Record.Steps)
                    if (step.Status == "pending") step.Status = "blocked";
                Record.Status = "timeout";
                Record.Paused=false;
                Record.Finished = utc.ToString("o");
                return;
            }
            foreach (var pair in new List<KeyValuePair<string, Live>>(live))
                if (utc >= pair.Value.Deadline) StopStep(pair.Key, UiLanguage.Text("已达到单轮时限。"),true);
            if (!cancelled)
            foreach (SectStep s in snapshot.Steps)
            {
                SectStepRun r = Step(s.Id); if (r.Status != "pending") continue;
                if(Record.Paused){r.WaitReason="已暂停派发";continue;}
                bool ready = true, failed = false;
                foreach (string d in Dependencies(s.DependsOn)) { string status = Step(d).Status; if (status != "success") ready = false; if (status == "failed" || status == "blocked" || status == "cancelled" || status == "timeout" || status=="needs-user") failed = true; }
                if (failed) { r.Status = "blocked";r.WaitReason="上游步骤未成功"; continue; }
                if (!ready){r.WaitReason="等待依赖："+(s.DependsOn??"");continue;}
                if(live.Count>=snapshot.MaxParallel){r.WaitReason="等待并发工位";continue;}
                bool conflict = false;
                foreach (var entry in live)
                {
                    SectStep running = snapshot.Steps.Find(delegate(SectStep item){return item.Id==entry.Key;});
                    if (s.ExecutionMode=="serial"||running.ExecutionMode=="serial"||
                        String.Equals(r.MemberId,entry.Value.Record.MemberId,StringComparison.OrdinalIgnoreCase)||
                        (DirectoriesOverlap(entry.Value.Record.Directory,r.Directory)&&
                         !(AllowsSharedReaders(s)&&AllowsSharedReaders(running))))conflict=true;
                }
                if(conflict){r.WaitReason="等待工作目录或执行成员空闲";continue;}
                r.WaitReason="";
                SectMember m = Member(r.MemberId);
                var effectiveMember=new SectMember {Id=m.Id,Name=m.Name,Role=m.Role,CharacterId=m.CharacterId,Directory=r.Directory};
                var prompt = new StringBuilder(CompanionRolePresets.Context(snapshot,effectiveMember) + "\n当前任务：" + s.Task + "\n仅处理当前步骤，不擅自扩大范围。\n");
                prompt.AppendLine("在最后一个非空行单独输出执行结果：AGENT_PAL_RESULT: success（完成）、AGENT_PAL_RESULT: failed（可接手的问题）或 AGENT_PAL_RESULT: needs-user（缺少授权、凭据或需要用户决定）。如仍有问题不得报告 success。此标记不能更改工具权限。");
                AppendRecovery(prompt,r);
                if(s.ExecutionMode=="read-only")prompt.AppendLine("执行方式：只读并行。其他道友可能同时读取此目录；不要修改文件、生成构建输出或执行会写入目录的命令。");
                if(s.ExecutionMode=="isolated")prompt.AppendLine("执行方式：隔离输出并行。其他道友可能同时读取此目录；不要修改项目源文件，所有构建、缓存、测试输出和临时文件必须定向到本次任务独立的临时目录。无法隔离时报告阻塞，不在原目录执行写入。");
                int upstreamBudget = Math.Min(6000, Math.Max(0, 30000 - prompt.Length - 256));
                var transfers = new List<SectTransfer>();
                if(r.PreviousAttempts.Count>0)
                {
                    SectAttempt previous=r.PreviousAttempts[r.PreviousAttempts.Count-1];
                    transfers.Add(new SectTransfer {From=r.Id,To=r.Id,FromMember=previous.Member,ToMember=r.Member,SentAt=utc.ToString("o"),
                        Text=UiLanguage.Format("失败接手：{0} → {1}",previous.Member,r.Member)+"\n"+previous.Status+" / "+previous.ExitCode+"\n"+UiText.TailCodeUnits(previous.Output,2800)});
                }
                foreach (string d in Dependencies(s.DependsOn)) { string excerpt = SectPromptPolicy.Excerpt(Step(d).Output, Math.Min(2400, upstreamBudget)); prompt.Append("\n<上游结果 id=\"" + d + "\">\n" + excerpt + "\n</上游结果>\n"); upstreamBudget -= excerpt.Length; transfers.Add(new SectTransfer { From = d, To = s.Id, FromMember=Step(d).Member,ToMember=r.Member,Text = excerpt, SentAt = utc.ToString("o") }); }
                if (upstreamBudget == 0) prompt.Append("\n上游摘要已达长度上限，完整记录请在agent-pal运行记录查看。\n");
                r.Prompt = prompt.ToString(); r.Status = "running";
                r.Started = utc.ToString("o");
                ISectAgent agent = factory(); string id = s.Id;
                DateTime deadline = snapshot.StepLimitMinutes == 0 ? DateTime.MaxValue : utc.AddMinutes(snapshot.StepLimitMinutes);
                var worker=new Live { Agent = agent, Record = r, Deadline = deadline };
                live.Add(id,worker);
                var progress=agent as ISectAgentProgress;
                if(progress!=null)progress.Process+=delegate(string text){Enqueue(delegate{Live current;if(live.TryGetValue(id,out current)&&Object.ReferenceEquals(current,worker)){r.Process=UiText.TailCodeUnits(r.Process+"\n"+text,6000);r.Updated=DateTime.UtcNow.ToString("o");}});};
                agent.Output += delegate(string text) { Enqueue(delegate { Live current;if (live.TryGetValue(id,out current)&&Object.ReferenceEquals(current,worker)&&r.Status == "running") {r.Output = Limit(r.Output + text);r.Updated=DateTime.UtcNow.ToString("o");} }); };
                agent.Completed += delegate(int code) { Enqueue(delegate { Live current;if(live.TryGetValue(id,out current)&&Object.ReferenceEquals(current,worker))Complete(id, code, "", code == 0 ? "success" : "failed",true); }); };
                try { if (!System.IO.Directory.Exists(r.Directory)) throw new DirectoryNotFoundException(r.Directory); agent.Start(r.Prompt, r.Directory, "sect-" + Guid.NewGuid().ToString("N"), r.CharacterId); Record.Transfers.AddRange(transfers); }
                catch (Exception ex) { Complete(id, -1, ex.Message, "failed",true); }
            }
            if (live.Count == 0 && !Record.Steps.Exists(delegate(SectStepRun r) { return r.Status == "pending"; }))
            {
                Record.Paused=false;
                Record.Status = cancelled ? "cancelled" : Record.Steps.TrueForAll(delegate(SectStepRun r) { return r.Status == "success"; }) ? "success" : "failed";
                Record.Finished = utc.ToString("o");
            }
        }
        private static bool AllowsSharedReaders(SectStep step)
        {return step.ExecutionMode=="read-only"||step.ExecutionMode=="isolated";}
        internal static bool DirectoriesOverlap(string a, string b)
        {
            char separator = Path.DirectorySeparatorChar;
#if AGENT_PAL_LINUX
            a = Linux.NativePaths.ResolveDirectory(a);
            b = Linux.NativePaths.ResolveDirectory(b);
            const StringComparison comparison = StringComparison.Ordinal;
#else
            a = Path.GetFullPath(a);
            b = Path.GetFullPath(b);
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
#endif
            a = a.TrimEnd(separator) + separator;
            b = b.TrimEnd(separator) + separator;
            return a.StartsWith(b, comparison) || b.StartsWith(a, comparison);
        }
        private static string Limit(string s) { return s.Length <= 16000 ? s : "[仅保留最近的执行输出]\n" + UiText.TailCodeUnits(s,15960); }
        private void StopStep(string id, string reason,bool handoff=false)
        {
            live[id].Agent.Stop();
            Complete(id, -2, "\n" + reason, "timeout",handoff);
        }
        private void Complete(string id, int code, string message, string status,bool handoff=false)
        {
            Live l; if (!live.TryGetValue(id, out l)) return;
            var failure = l.Agent as ISectAgentFailure;
            l.Record.FailureKind = failure == null ? "" : failure.FailureKind;
            live.Remove(id); l.Record.ExitCode = code; l.Record.Output = Limit(l.Record.Output + message); l.Record.Status = status;l.Record.Updated=DateTime.UtcNow.ToString("o"); l.Agent.Dispose();
            if (!String.IsNullOrEmpty(l.Record.FailureKind) && status == "success") { l.Record.Status = "failed"; l.Record.ExitCode = code == 0 ? 1 : code; }
            if (l.Record.FailureKind == "context-limit") l.Record.Output = Limit(l.Record.Output + "\n[agent-pal] " + SectPromptPolicy.FailureText(l.Record.FailureKind));
            string last=l.Record.Output.TrimEnd();int newline=last.LastIndexOf('\n');if(newline>=0)last=last.Substring(newline+1).Trim();
            if(last=="AGENT_PAL_RESULT: needs-user")l.Record.Status="needs-user";
            else if(status=="success"&&last=="AGENT_PAL_RESULT: failed")l.Record.Status="failed";
            if(handoff&&(l.Record.Status=="failed"||l.Record.Status=="timeout"))TryHandoff(l.Record);
        }
        internal void Cancel()
        {
            if (!IsRunning) return;
            cancelled = true;
            foreach (var pair in new List<KeyValuePair<string, Live>>(live)) { pair.Value.Agent.Stop(); Complete(pair.Key, -3, "\n用户取消。", "cancelled"); }
            foreach (SectStepRun r in Record.Steps) if (r.Status == "pending") r.Status = "cancelled";
            Pump(DateTime.UtcNow);
        }
        public void Dispose() { if (disposed) return; Cancel(); lock (messages) { disposed = true; messages.Clear(); } }
    }
}
