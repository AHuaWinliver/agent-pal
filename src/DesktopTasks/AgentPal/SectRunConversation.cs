// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AgentPal
{
    internal sealed class SectRunConversation
    {
        private readonly Dictionary<string, ChatMessage> messages = new Dictionary<string, ChatMessage>();
        internal ActivityRecord Conversation { get; private set; }
        internal ChatMessage LatestReply { get; private set; }
        private SectRun selected;

        internal bool Update(SectRun run)
        {
            bool changed = !Object.ReferenceEquals(run, selected);
            if (changed)
            {
                selected = run;
                messages.Clear();
                LatestReply = null;
                Conversation = run == null ? null : new ActivityRecord { Output = UiLanguage.Text("等待分派任务…"), Updated = LocalTime(run.Started) };
            }
            if (run == null) return changed;

            DateTime started = LocalTime(run.Started);
            var ordered = new List<ChatMessage>();
            DateTime latestUpdate=DateTime.MinValue;
            ChatMessage newestTimestamped=null;
            foreach (SectStepRun step in run.Steps.OrderBy(step => TimeOr(step.Started, started)))
            {
                if (step.PreviousAttempts!=null)for(int attemptIndex=0;attemptIndex<step.PreviousAttempts.Count;attemptIndex++)
                {
                    SectAttempt attempt=step.PreviousAttempts[attemptIndex];
                    Add(ordered,"attempt/"+step.Id+"/"+attemptIndex,"assistant",UiLanguage.Text(attempt.Member),attempt.CharacterId,
                        TimeOr(attempt.Started,started),Status(attempt.Status)+"\n\n"+attempt.Output,ref changed,attempt.Process);
                }
                if (step.Status == "pending") continue;
                DateTime stepTime = TimeOr(step.Started, started);
                for (int i = 0; i < run.Transfers.Count; i++)
                {
                    SectTransfer transfer = run.Transfers[i];
                    if (!String.Equals(transfer.To, step.Id, StringComparison.OrdinalIgnoreCase)) continue;
                    SectStepRun source = run.Steps.Find(item => String.Equals(item.Id, transfer.From, StringComparison.OrdinalIgnoreCase));
                    string speaker = UiLanguage.Text(!String.IsNullOrEmpty(transfer.FromMember)?transfer.FromMember:source == null ? transfer.From : source.Member);
                    SectAttempt sender=source==null?null:source.PreviousAttempts.FindLast(item=>item.Member==transfer.FromMember);
                    string senderCharacter=sender!=null?sender.CharacterId:source==null?null:source.CharacterId;
                    Add(ordered, "transfer/" + i, "user", speaker, senderCharacter,
                        TimeOr(transfer.SentAt, stepTime),
                        UiLanguage.Format("交接给 {0} · {1}", UiLanguage.Text(step.Member), step.Id) + "\n\n" + transfer.Text, ref changed);
                }
                string task = String.IsNullOrEmpty(step.Task) ? step.Prompt : step.Task;
                if (!String.IsNullOrWhiteSpace(task))
                    Add(ordered, "task/" + step.Id, "user", UiLanguage.Text("Agent 团队调度"), null, stepTime,
                        UiLanguage.Format("交给 {0} · {1}", UiLanguage.Text(step.Member), step.Id) + "\n" + step.Directory + "\n\n" + task, ref changed);
                string reply = String.IsNullOrWhiteSpace(step.Output) && step.Status == "running" ?
                    UiLanguage.Text("正在回复…") : Status(step.Status);
                if (!String.IsNullOrWhiteSpace(step.Output)) reply += "\n\n" + step.Output;
                string replyKey="reply/"+step.Id+"/"+(step.PreviousAttempts==null?0:step.PreviousAttempts.Count);
                Add(ordered, replyKey, "assistant", UiLanguage.Text(step.Member), step.CharacterId, stepTime, reply, ref changed,step.Process);
                DateTime updated;
                if(!String.IsNullOrEmpty(step.Updated)&&DateTime.TryParse(step.Updated,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out updated)&&updated.ToUniversalTime()>=latestUpdate)
                {latestUpdate=updated.ToUniversalTime();newestTimestamped=messages[replyKey];}
            }
            if(newestTimestamped!=null)LatestReply=newestTimestamped;
            if (!changed && ordered.Count == Conversation.Messages.Count)
                for (int i = 0; !changed && i < ordered.Count; i++)
                    changed = !Object.ReferenceEquals(ordered[i], Conversation.Messages[i]);
            if (!changed && ordered.Count == Conversation.Messages.Count) return false;
            Conversation.Messages.Clear();
            Conversation.Messages.AddRange(ordered);
            return true;
        }

        private void Add(List<ChatMessage> ordered, string key, string role, string speaker, string characterId,
            DateTime at, string text, ref bool changed,string process="")
        {
            ChatMessage message;
            if (!messages.TryGetValue(key, out message))
            {
                message = new ChatMessage { Role = role, Speaker = speaker, CharacterId = characterId, At = at };
                messages.Add(key, message);
            }
            if (message.Text != text)
            {
                message.Text = text;
                if(role=="assistant")LatestReply=message;
                changed = true;
            }
            ordered.Add(message);
            if(message.Process!=process){message.Process=process;changed=true;}
            if(message.Speaker!=speaker||message.CharacterId!=characterId){message.Speaker=speaker;message.CharacterId=characterId;changed=true;}
        }

        private static DateTime TimeOr(string value, DateTime fallback)
        {
            DateTime result;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result)
                ? result.ToLocalTime() : fallback;
        }

        private static DateTime LocalTime(string value) { return TimeOr(value, DateTime.Now); }

        internal static string DisplayTime(string value) { return TimeOr(value, DateTime.MinValue).ToString("g"); }

        internal static string Status(string state)
        {
            switch (state)
            {
                case "running": return UiLanguage.Text("正在运行");
                case "success": return UiLanguage.Text("成功");
                case "failed": return UiLanguage.Text("失败");
                case "timeout": return UiLanguage.Text("超时");
                case "blocked": return UiLanguage.Text("未执行");
                case "cancelled": return UiLanguage.Text("已取消");
                case "interrupted": return UiLanguage.Text("已中断");
                case "needs-user": return UiLanguage.Text("需要用户处理");
                default: return state;
            }
        }
    }
}
