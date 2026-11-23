// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgentPal;
internal static class ChatBubbleTests
{
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    private static void Bodies(Control control,List<RichTextBox> result){foreach(Control child in control.Controls){var rich=child as RichTextBox;if(rich!=null&&rich.Name!="CliProcessDetails")result.Add(rich);Bodies(child,result);}}
    [STAThread] private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();string root=Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");Directory.CreateDirectory(root);
            var a=new ActivityRecord {Title="今天一起做些什么？",CharacterId="orbit",SessionId=Guid.NewGuid().ToString(),State="就绪",Updated=DateTime.Now,Output="你：今天一起做些什么？\r\n\r\n"};
            a.StartConversation("今天一起做些什么？");a.AppendOutput("道友好。先把今天最重要的一件事告诉我，我们一起慢慢理清。🍵\n\n我可以陪你规划日程，也能帮你检查项目、整理文件。");
            a.AppendUserPrompt("先帮我把项目计划列出来，要留一点休息时间 😊");a.AppendOutput("好，我们把事情分成三步：\n\n1. 明确目标与今天可用的时间。\n2. 拆成两三个能完成的小任务。\n3. 每完成一段，留几分钟休息。\n\n你希望先做哪一项？");
            Check(a.Messages.Count==4&&a.Messages[0].Role=="user"&&a.Messages[1].Role=="assistant","explicit alternating roles without parsing transcript");
            a.AppendOutput("\n引用示例：你：这仍然是道友引用的内容。");Check(a.Messages.Count==4,"quoted user marker never creates a fake turn");
            using(var form=new ActivityTrayForm())
            {
                form.SetActivities(new[]{a});form.ShowNear(new Rectangle(600,20,192,208));Application.DoEvents();form.FlushConversationForTest();Application.DoEvents();
                Check(form.ConversationForTest.MessageCount==4&&form.ConversationForTest.HasBothSides&&form.ConversationForTest.UsesCliLayout,"regular conversation uses a CLI transcript with explicit user and assistant roles");
                Check(form.ConversationForTest.UnicodeSupportedForTest(),"actual visible bubbles use modern Unicode RichEdit");
                var bodies=new List<RichTextBox>();Bodies(form.ConversationForTest,bodies);Check(bodies.Count==4,"one selectable native text control per message");
                bodies[3].Select(0,2);string selected=bodies[3].SelectedText;
                string previousText=bodies[3].Text;a.AppendOutput("\n新的流式内容已到达。✅");form.SetActivities(new[]{a});Check(bodies[3].Text==previousText,"stream refresh is throttled instead of immediate per-token redraw");form.FlushConversationForTest();Check(bodies[3].SelectedText==selected,"stream update preserves text selection");
                Check(form.ConversationForTest.MessageCount==4,"stream delta updates existing bubble instead of adding duplicates");
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(root,"chat-preview.png"));}
                Check(form.PinnedOpen,"conversation remains open until manual close");form.HideUnlessPinned();Check(form.Visible,"background updates do not hide pinned chat");
                var input=(TextBox)typeof(ActivityTrayForm).GetField("reply",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(form);input.Text="尚未发送的草稿";
                var legacy=new ActivityRecord {Title="外部会话",Output="你：引用文字\n旧的输出没有角色元数据",State="就绪",Updated=DateTime.Now};form.SetActivities(new[]{legacy,a});form.SelectActivity(legacy);form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==1,"external legacy output preserved without guessing roles");
                form.SelectActivity(a);form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==4,"switching conversation restores its message bubbles");Check(input.Text=="尚未发送的草稿","switching sessions preserves unsent drafts");
                a.AppendUserPrompt("长消息测试");a.AppendOutput(new String('测',18000)+"😀");form.SetActivities(new[]{a});form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==6,"long response can scroll without losing message roles");
                form.DismissForTest();Check(!form.Visible&&!form.PinnedOpen,"manual close dismisses chat");
            }
            var unicode=new ActivityRecord {Output=""};unicode.StartConversation("👨‍💻");unicode.AppendOutput(new String('字',130000)+"🚀");Check(!UiText.HasUnpairedSurrogate(unicode.Messages[1].Text),"bounded chat history keeps surrogate pairs intact");
            var waiting=new ActivityRecord {State="正在运行",CharacterId="orbit",Updated=DateTime.Now,Output="你：你好"};waiting.StartConversation("你好");
            using(var form=new ActivityTrayForm())
            {
                form.SetActivities(new[]{waiting});form.ShowNear(new Rectangle(600,20,192,208));form.FlushConversationForTest();
                var bodies=new List<RichTextBox>();Bodies(form.ConversationForTest,bodies);Check(bodies.Exists(delegate(RichTextBox box){return box.Text=="正在思考…";})&&waiting.Messages.Count==1,"thinking placeholder is visible but never logged as a model response");
                waiting.AppendOutput("你好，道友。");form.SetActivities(new[]{waiting});form.FlushConversationForTest();Check(form.ConversationForTest.MessageCount==2&&waiting.Messages[1].Text=="你好，道友。","first token replaces pending indicator without a duplicate turn");form.DismissForTest();
            }
            using(var preview=new ReplyBubbleForm())
            {
                preview.BeginRequest("结构化回复");
                preview.AppendReply("## 执行结果\n\n- 已检查配置\n- 正在运行验证\n\n```sh\necho done\n```");
                preview.AppendProcess("[工具] Bash · echo done");
                preview.FlushPendingReply();
                preview.ShowNear(new Rectangle(600,20,192,208));Application.DoEvents();
                Check(preview.PreviewTextForTest.Contains("\n- 已检查配置")&&preview.PreviewTextForTest.Contains("echo done"),"desktop reply preserves headings, list lines and code rather than flattening the last sentence");
                using(var image=new Bitmap(preview.Width,preview.Height))
                {preview.DrawToBitmap(image,new Rectangle(0,0,preview.Width,preview.Height));image.Save(Path.Combine(root,"structured-reply-preview.png"));}
            }
            using(var runner=new ClaudeCliRunner())
            {
                var progress=new List<string>();
                string output="";
                runner.ProcessReceived+=delegate(string text){progress.Add(text);};
                runner.OutputReceived+=delegate(string text){output+=text;};
                string tool="{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"tool_use\",\"id\":\"read-one\",\"name\":\"Read\",\"input\":{\"file_path\":\"README.md\"}}}}";
                runner.ParseLine(tool);runner.ParseLine(tool);
                runner.ParseLine("{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"thinking\",\"thinking\":\"hidden content\"}}}");
                runner.ParseLine("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"read-one\",\"content\":\"file read successfully\"}]}}");
                Check(progress.Count==2&&progress[0].Contains("Read")&&progress[0].Contains("README.md")&&progress[1].Contains("file read successfully")&&output.Length==0,
                    "actual tool progress is deduplicated and kept separate from reply text and hidden reasoning");
                runner.ParseLine("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"array-result\",\"content\":[{\"type\":\"text\",\"text\":\"structured result 🌱\"}]}]}}");
                Check(progress.Count==3&&progress[2].Contains("structured result 🌱"),"tool-result text blocks retain readable Unicode content");
            }
            UiLanguage.Set("en");
            var transcript=new ActivityRecord {Title="Review workspace",CharacterId="orbit",SessionId=Guid.NewGuid().ToString("D"),CharacterConversation=true,State="正在运行",Updated=DateTime.Now};
            transcript.StartConversation("Review this folder and explain the changes.");
            string raw="## Result\n\nThe **configuration** is ready.\n\n- Directory verified\n- Unicode preserved: 中文 🌱\n\n```sh\ndotnet test\n```";
            transcript.AppendOutput(raw);
            for(int step=1;step<=8;step++)transcript.AppendProcess("[Tool] Read · step "+step);
            using(var form=new ActivityTrayForm())
            {
                form.SetActivities(new[]{transcript});form.ShowNear(new Rectangle(600,20,192,208));form.FlushConversationForTest();Application.DoEvents();
                var bodies=new List<RichTextBox>();Bodies(form.ConversationForTest,bodies);
                Check(bodies[1].Text.Contains("Result")&&!bodies[1].Text.Contains("##")&&!bodies[1].Text.Contains("**")&&bodies[1].Text.Contains("dotnet test")&&transcript.Messages[1].Text==raw,
                    "CLI transcript renders heading, emphasis and code while preserving the original reply");
                var panels=new List<CliProcessView>();FindProcesses(form.ConversationForTest,panels);
                var process=panels.Find(item=>item.Visible);Check(process!=null&&!process.Expanded,"tool process is separate and collapsed by default");
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(root,"cli-conversation-collapsed.png"));}
                process.ToggleForTest();Application.DoEvents();
                Check(process.Expanded&&process.Height==232,"tool process expands without replacing or adding conversation turns");
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(root,"cli-conversation-preview.png"));}
                Check(form.ConversationForTest.MessageCount==2,"expanding tools preserves message identities");form.DismissForTest();
            }
            TestFailureReporting(root);
            Console.WriteLine("Chat UI tests passed. No Claude/model calls.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void FindProcesses(Control root,List<CliProcessView> values){foreach(Control control in root.Controls){var process=control as CliProcessView;if(process!=null)values.Add(process);FindProcesses(control,values);}}
    private static void TestFailureReporting(string root)
    {
        using(var runner=new ClaudeCliRunner())
        {
            var notices=new List<string>();var retryFlags=new List<bool>();var statuses=new List<string>();
            runner.NoticeReceived+=delegate(string text,bool retry){notices.Add(text);retryFlags.Add(retry);};runner.StatusChanged+=statuses.Add;
            runner.ParseLine("{\"type\":\"system\",\"subtype\":\"api_retry\",\"attempt\":2,\"max_retries\":10,\"retry_delay_ms\":2000,\"error\":{\"message\":\"Connection refused (ECONNREFUSED)\"}}");
            Check(notices.Count==1&&retryFlags[0]&&notices[0].Contains("2/10")&&notices[0].Contains("2s"),"structured connection retries expose the actual error, attempt and delay");
            runner.ParseLine("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"Recovered response\"}]}}");
            Check(runner.FailureKind=="","genuine assistant output clears a transient connection failure");
            runner.ParseLine("{\"type\":\"result\",\"is_error\":true,\"errors\":[\"API connection failed\"]}");
            Check(notices.Count==2&&!retryFlags[1]&&runner.FailureKind.Length>0&&notices[1].Contains("API connection failed"),"final errors stay visible after partial output even with a zero process exit code");
            runner.ParseLine("{\"type\":\"assistant\",\"error\":\"authentication_failed\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"API Error: invalid key\"}]}}");
            Check(notices[notices.Count-1].Contains("authentication_failed")&&!retryFlags[retryFlags.Count-1],"assistant API error envelopes are failures rather than successful model replies");
            Check(!ClaudeCliRunner.ShouldSuppressDiagnostic("Connection refused ECONNREFUSED query_source=cli"),"diagnostic metadata cannot suppress connection failures");
            Check(CliFailureNotice.FromEvent(new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>("{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"content\":\"Connection refused ECONNREFUSED\"}]}}"))==null,"tool output containing connection errors is not misclassified as an API failure");
            var diagnostic=typeof(ClaudeCliRunner).GetMethod("HandleDiagnostic",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            diagnostic.Invoke(runner,new object[]{"claude-code:unrecognized_model query_source=cli"});Check(!statuses.Exists(s=>s.Contains("已连接本地模型")),"ignored telemetry never claims a model connection");
            diagnostic.Invoke(runner,new object[]{"Connection refused (ECONNREFUSED) · Retrying in 2s · attempt 2/10"});Check(retryFlags[retryFlags.Count-1],"plain stderr connection retries are surfaced too");
        }
        var activity=new ActivityRecord {State="正在运行",CharacterId="orbit",Updated=DateTime.Now,StatusDetail="Connection issue · retrying\nECONNREFUSED · attempt 2/10"};activity.StartConversation("hello");activity.AppendProcess(activity.StatusDetail);
        using(var form=new ActivityTrayForm())
        {
            form.SetActivities(new[]{activity});form.ShowNear(new Rectangle(600,20,96,104));form.FlushConversationForTest();Application.DoEvents();
            var bodies=new List<RichTextBox>();Bodies(form.ConversationForTest,bodies);
            Check(bodies.Exists(box=>box.Text.Contains("ECONNREFUSED"))&&!bodies.Exists(box=>box.Text=="正在思考…"),"the actual conversation replaces endless thinking with the live retry warning");
            var row=bodies[0].Parent.Parent;var next=bodies[1].Parent.Parent;
            Check(bodies[0].Parent.Left>bodies[1].Parent.Left&&row.Controls.OfType<PictureBox>().Any(p=>p.Visible)&&next.Controls.OfType<PictureBox>().Any(p=>p.Visible),"user and companion have opposite bubble alignment with visible avatars");
            using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(root,"chat-connection-retry.png"));}
            form.DismissForTest();
        }
    }
}
