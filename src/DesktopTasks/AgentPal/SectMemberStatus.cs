// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

namespace AgentPal
{
    internal sealed class SectRunMember
    {
        public string Id = "", Name = "", CharacterId = "";
    }
    internal sealed class SectMemberStatus
    {
        internal string Id, Name, CharacterId, Status = "idle", Task = "", Detail = "";
        internal int Completed, Total;
        internal static List<SectMemberStatus> Build(CompanionSect sect, SectRun run)
        {
            var result = new List<SectMemberStatus>();
            if (run != null && run.Members != null && run.Members.Count > 0)
                foreach (SectRunMember member in run.Members) Add(result, member.Id, member.Name, member.CharacterId);
            else if (sect != null && (run == null || sect.Name == run.Sect))
                for (int i = 0; i < sect.Members.Count; i++)
                { SectMember member = sect.Members[i]; Add(result, member.Id, member.Name, String.IsNullOrEmpty(member.CharacterId) ? PetCharacter.All[i % 4].Id : member.CharacterId); }
            if (run == null) return result;
            foreach (SectStepRun step in run.Steps)
            {
                foreach (SectAttempt attempt in step.PreviousAttempts) Add(result, attempt.MemberId, attempt.Member, attempt.CharacterId);
                Add(result, step.MemberId, step.Member, step.CharacterId);
            }
            foreach (SectMemberStatus member in result)
            {
                var own = run.Steps.FindAll(delegate(SectStepRun step) { return Matches(member, step.MemberId, step.Member); });
                member.Total = own.Count;
                member.Completed = own.FindAll(delegate(SectStepRun step) { return step.Status == "success"; }).Count;
                SectStepRun latest = null;
                foreach (SectStepRun step in own)
                {
                    if (latest == null || step.Status == "running" || (latest.Status != "running" && String.CompareOrdinal(Stamp(step), Stamp(latest)) > 0)) latest = step;
                }
                if (own.Exists(delegate(SectStepRun step) { return step.Status == "running"; })) member.Status = "running";
                else if (own.Exists(delegate(SectStepRun step) { return step.Status == "needs-user"; })) member.Status = "needs-user";
                else if (own.Exists(delegate(SectStepRun step) { return step.Status == "failed" || step.Status == "timeout"; })) member.Status = "failed";
                else if (own.Exists(delegate(SectStepRun step) { return step.Status == "pending"; })) member.Status = run.Paused ? "paused" : "pending";
                else if (own.Exists(delegate(SectStepRun step) { return step.Status == "blocked"; })) member.Status = "blocked";
                else if (own.Count > 0) member.Status = own.TrueForAll(delegate(SectStepRun step) { return step.Status == "success"; }) ? "success" : own[own.Count - 1].Status;
                if (latest != null)
                {
                    member.Task = latest.Id;
                    member.Detail = !String.IsNullOrEmpty(latest.FailureKind) && member.Status == "failed" ? SectPromptPolicy.FailureText(latest.FailureKind) :
                        !String.IsNullOrWhiteSpace(latest.WaitReason) && latest.Status != "running" ? UiLanguage.Text(latest.WaitReason) : LatestLine(latest.Output);
                    if (member.Detail.Length == 0) member.Detail = LatestLine(latest.Process);
                }
                else
                    foreach (SectStepRun step in run.Steps) foreach (SectAttempt attempt in step.PreviousAttempts)
                        if (Matches(member, attempt.MemberId, attempt.Member)) { member.Status = "handoff"; member.Task = step.Id; member.Detail = UiLanguage.Format("已交给 {0}", UiLanguage.Text(step.Member)); }
            }
            return result;
        }
        private static string Stamp(SectStepRun step) { return String.IsNullOrEmpty(step.Updated) ? step.Started : step.Updated; }
        private static bool Matches(SectMemberStatus member, string id, string name)
        { return !String.IsNullOrEmpty(id) && !String.IsNullOrEmpty(member.Id) ? String.Equals(id, member.Id, StringComparison.OrdinalIgnoreCase) : member.Name == name; }
        private static void Add(List<SectMemberStatus> result, string id, string name, string character)
        {
            if (String.IsNullOrWhiteSpace(name)) return;
            var existing = result.Find(delegate(SectMemberStatus member) { return Matches(member, id, name); });
            if (existing == null) result.Add(new SectMemberStatus { Id = id ?? "", Name = name, CharacterId = character ?? "" });
            else if (String.IsNullOrEmpty(existing.CharacterId)) existing.CharacterId = character ?? "";
        }
        internal static string LatestLine(string value)
        {
            string[] lines = UiText.TailCodeUnits(value ?? "", 1200).Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--) { string line = lines[i].Trim(); if (line.Length > 0 && !line.StartsWith("AGENT_PAL_RESULT:", StringComparison.Ordinal)) return line; }
            return "";
        }
        internal static string Label(string state)
        {
            switch (state) { case "idle": return UiLanguage.Text("待命"); case "paused": return UiLanguage.Text("派发已暂停"); case "handoff": return UiLanguage.Text("已交接"); case "pending": return UiLanguage.Text("排队中"); default: return SectRunConversationLabel(state); }
        }
        private static string SectRunConversationLabel(string state)
        {
            switch (state) { case "running": return UiLanguage.Text("执行中"); case "success": return UiLanguage.Text("已完成"); case "failed": return UiLanguage.Text("失败"); case "timeout": return UiLanguage.Text("超时"); case "needs-user": return UiLanguage.Text("需要用户处理"); case "blocked": return UiLanguage.Text("上游失败，已阻断"); case "cancelled": return UiLanguage.Text("已取消"); case "interrupted": return UiLanguage.Text("已中断"); default: return state; }
        }
    }
}
