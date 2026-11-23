// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AgentPal;

internal static class WorkshopPresentationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class Fake : ISectAgent, ISectAgentFailure
    {
        public event Action<string> Output; public event Action<int> Completed;
        public string FailureKind { get; set; }
        internal string Prompt;
        public void Start(string prompt, string directory, string marker, string character) { Prompt = prompt; }
        public void Stop() { } public void Dispose() { }
        internal void Finish(string output, int code) { if (Output != null) Output(output); if (Completed != null) Completed(code); }
    }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static void Capture(Control control, string path) { using (var bitmap = new Bitmap(control.Width, control.Height)) { control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path); } }
    [STAThread] private static int Main()
    {
        try
        {
            if (Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") != "1") throw new Exception("Isolated state required.");
            string root = Program.StateDirectory; Directory.CreateDirectory(root); Application.EnableVisualStyles(); UiLanguage.Set("en");
            TestContext(root); TestMotion(); TestNavigation(); TestMembers(root);
            Console.WriteLine("PASS workshop presentation and bounded recovery, without model calls. Evidence: " + root); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void TestContext(string root)
    {
        var renderer = new ClaudeTaskStreamRenderer(); var json = new JavaScriptSerializer();
        renderer.Consume(json.Serialize(new { type = "assistant", message = new { id = "a", content = new[] { new { type = "text", text = "Earlier analysis" } } } }));
        string failure = renderer.Consume(json.Serialize(new { type = "result", result = "Prompt is too long", is_error = true }));
        Check(failure.Contains("Prompt is too long") && renderer.FailureKind == "context-limit", "authoritative context error remains visible after earlier assistant text");
        var quoted = new ClaudeTaskStreamRenderer(); quoted.Consume(json.Serialize(new { type = "result", result = "Explain Prompt is too long", is_error = false }));
        Check(quoted.FailureKind == "", "a successful explanation quoting an error is not misclassified");
        var structured=new ClaudeTaskStreamRenderer{IncludeToolOutput=false};var process=new List<string>();structured.Process+=process.Add;
        string body=structured.Consume(json.Serialize(new {type="assistant",message=new{id="intro",content=new[]{new{type="text",text="Inspect the project."}}}}));
        body+=structured.Consume(json.Serialize(new {type="assistant",message=new{id="tool",content=new[]{new{type="tool_use",id="glob",name="Glob",input=new{path="demo",pattern="*.cs"}}}}}));
        body+=structured.Consume(json.Serialize(new {type="user",message=new{content=new[]{new{type="tool_result",tool_use_id="glob",content="First.cs\nSecond.cs",is_error=false}}}}));
        body+=structured.Consume(json.Serialize(new {type="assistant",message=new{id="reply",content=new[]{new{type="text",text="Two source files found."}}}}));
        Check(process.Count>0&&String.Join("",process).Contains("Second.cs")&&!body.Contains("Second.cs")&&body.Contains(Environment.NewLine+Environment.NewLine+"Two source files"),"real task-stream tools stay in the process panel and preserve paragraph boundaries");
        var sect = CompanionRolePresets.Create("Context test", root, false); sect.Goal = "Inspect a small project";
        var agents = new List<Fake>();
        using (var execution = new SectExecution(sect, delegate { var fake = new Fake(); agents.Add(fake); return fake; }))
        {
            execution.Pump(DateTime.UtcNow);
            Check(execution.Record.Members.Count == 4 && execution.Record.Members[3].CharacterId == "pippin", "run snapshots all members, including those with no current task");
            Check(agents[0].Prompt.Contains("禁止以 **/*") && agents[0].Prompt.Contains("Windows 不能假设 ls/head"), "task prompt supplies bounded discovery and actual-shell guidance");
            agents[0].FailureKind = "context-limit"; agents[0].Finish(new string('x', 20000) + "Prompt is too long", 0); execution.Pump(DateTime.UtcNow);
            Check(agents.Count == 2 && agents[1].Prompt.Length < 30000 && agents[1].Prompt.Contains("禁止重放") && !agents[1].Prompt.Contains(new string('x', 1000)), "context recovery starts a fresh bounded handoff without replaying oversized listings");
            Check(execution.Record.Steps[0].PreviousAttempts[0].FailureKind == "context-limit" && execution.Record.Steps[0].PreviousAttempts[0].ExitCode != 0, "context failure is persisted even when the CLI reports exit zero");
        }
        string text = new string('a', 12000) + "\nFinal evidence: result.txt 😀";
        string compact = SectPromptPolicy.Excerpt(text, 2400);
        Check(compact.Length <= 2400 && compact.EndsWith("result.txt 😀") && !UiText.HasUnpairedSurrogate(compact), "bounded handoff retains recent evidence and intact Unicode");
    }
    private static void TestMotion()
    {
        var run = new SectRun();
        var members = new List<SectMemberStatus> { new SectMemberStatus { Name = "Sender", Status = "running" }, new SectMemberStatus { Name = "Receiver", Status = "pending" } };
        var homes = new Dictionary<string, PointF> { { "Sender", new PointF(100, 250) }, { "Receiver", new PointF(400, 250) } };
        var scales = new Dictionary<string, float> { { "Sender", 1 }, { "Receiver", 1 } }; var motion = new SectHallMotion();
        var start = motion.Sample(run, members, homes, scales, 0, false);
        var walking = motion.Sample(run, members, homes, scales, 6, false);
        var reading = motion.Sample(run, members, homes, scales, 8, false);
        Check(walking["Sender"].Moving && !reading["Sender"].Moving && reading["Sender"].Reading, "working members alternate real walking and report reading");
        Check(!walking["Receiver"].Moving, "waiting members do not fake active work");
        run.Transfers.Add(new SectTransfer { FromMember = "Sender", ToMember = "Receiver", From = "a", To = "b" });
        motion.Sample(run, members, homes, scales, 13, false);
        bool met=false,returned=false;
        for(int frame=1;frame<600;frame++)
        {
            var pose=motion.Sample(run,members,homes,scales,13+frame/30.0,false)["Sender"];
            if(pose.Handoff&&!pose.Moving&&Math.Abs(pose.Feet.X-400)<90)met=true;
            if(met&&!pose.Handoff&&SectHallNavigation.Distance(pose.Feet,homes["Sender"])<2){returned=true;break;}
        }
        Check(met,"a newly recorded handoff brings the sender beside the receiving member");
        Check(returned,"handoff finishes once and returns the sender to its workstation");
        var reduced = motion.Sample(run, members, homes, scales, 32, true);
        Check(!reduced["Sender"].Moving && reduced["Sender"].Feet == homes["Sender"], "reduced motion uses static working poses");
        motion.Suspend();run.Transfers.Add(new SectTransfer{FromMember="Sender",ToMember="Receiver"});
        Check(!motion.Sample(run,members,homes,scales,36,false)["Sender"].Handoff,"returning to a hidden hall does not replay stale handoffs");
    }
    private static void TestNavigation()
    {
        foreach(int count in new[]{1,2,4,6,12})foreach(Size viewport in new[]{new Size(640,480),new Size(1000,640),new Size(1400,900)})
        {
            Size canvas=SectHallLayout.CanvasSize(viewport,count,1);Rectangle[] seats=SectHallLayout.Seats(canvas,count,1);
            var homes=new Dictionary<string,PointF>();var scales=new Dictionary<string,float>();
            for(int i=0;i<count;i++)
            {
                var box=seats[i];float scale=Math.Min(1.5f,Math.Min(box.Width/SectHallLayout.WorkstationWidth,box.Height/SectHallLayout.WorkstationHeight));
                float x=box.Left+(box.Width-220*scale)/2,y=box.Top+(box.Height-SectHallLayout.WorkstationHeight*scale)/2;
                homes["m"+i]=new PointF(x+110*scale,y+229*scale);scales["m"+i]=scale;
            }
            var navigation=new SectHallNavigation(homes,scales);
            foreach(string actor in homes.Keys)
            {
                if(navigation.Plan(actor,homes[actor],homes[actor],true).Length!=1)throw new Exception("A receiver already at its desk must stay there.");
                int index=Int32.Parse(actor.Substring(1));PointF[] patrol=navigation.Patrol(actor,index);
                if(patrol==null)throw new Exception("No patrol route: "+count+" / "+viewport+" / "+actor);
                for(int i=1;i<patrol.Length;i++)if(!navigation.SafeSegment(actor,patrol[i-1],patrol[i]))throw new Exception("Patrol crosses furniture: "+actor);
                foreach(string target in homes.Keys)if(target!=actor)
                {
                    PointF h=homes[target];float side=homes[actor].X<h.X?-1:1;
                    PointF end=new PointF(h.X+side*44*scales[target],h.Y+64*Math.Max(scales[actor],scales[target]));
                    foreach(PointF[] path in new[]{navigation.Plan(actor,homes[actor],end,false),navigation.Plan(actor,patrol[patrol.Length-1],end,false),navigation.Plan(actor,end,homes[actor],true)})
                    {
                        if(path==null)throw new Exception("No handoff route: "+count+" / "+viewport+" / "+actor+" -> "+target);
                        for(int i=1;i<path.Length;i++)if(!navigation.SafeSegment(actor,path[i-1],path[i]))throw new Exception("Handoff crosses furniture: "+actor+" -> "+target);
                    }
                }
            }
            Check(true,"desk-aware patrol, handoff and return paths: "+count+" members / "+viewport);
        }
    }
    private static void TestMembers(string root)
    {
        var sect = CompanionRolePresets.Create("Cloud Hall", root, false); sect.Goal = "Demonstration only";
        var run = new SectRun { Sect = sect.Name, Started = DateTime.UtcNow.ToString("o") };
        for (int i = 0; i < 12; i++)
        {
            string name = "Member " + (i + 1); run.Members.Add(new SectRunMember { Id = "m" + i, Name = name, CharacterId = PetCharacter.All[i % 4].Id });
            if (i < 11) run.Steps.Add(new SectStepRun { Id = "task-" + i, MemberId = "m" + i, Member = name, CharacterId = PetCharacter.All[i % 4].Id, Status = i == 0 ? "running" : i == 1 ? "success" : i == 2 ? "failed" : "pending", Output = "Latest update " + i, WaitReason = i > 2 ? "等待并发工位" : "" });
        }
        var data = new WorkshopData(); data.Sects.Add(sect); data.History.Add(run);
        using (var form = new WorkshopForm(data, delegate { }, delegate { return null; }, delegate { throw new Exception("No CLI calls expected"); }, delegate { }))
        {
            form.Show(); var tabs = (TabControl)form.Controls[0]; tabs.SelectedIndex = 3; Application.DoEvents();
            var panel = (RunMemberStatusPanel)typeof(WorkshopForm).GetField("runMemberStates", Private).GetValue(form);
            foreach (Size size in new[] { new Size(820, 600), new Size(1000, 740), new Size(1400, 900) })
            {
                form.Size = size; form.PerformLayout(); Application.DoEvents();
                Check(panel.MemberCount == 12 && panel.AvatarCount == 4, "all twelve states and four original portraits are present: " + size);
                for (int i = 0; i < 12; i++) Check(panel.ClientRectangle.Contains(panel.CardBounds(i)), "member card remains fully inside status region " + i);
                var view = (ChatConversationView)typeof(WorkshopForm).GetField("runConversation", Private).GetValue(form);
                Console.WriteLine("INFO status=" + panel.Bounds + " parent=" + panel.Parent.ClientSize + " transcript=" + view.Bounds);
                Capture(tabs.SelectedTab, Path.Combine(root, "member-states-" + size.Width + ".png"));
                Check(view.Height >= 100 && panel.Bottom <= panel.Parent.ClientSize.Height, "status region fits without hiding the transcript");
                Capture(tabs.SelectedTab, Path.Combine(root, "member-states-" + size.Width + ".png"));
            }
            run.Steps[0].Output += "\nNewest live reply";
            typeof(WorkshopForm).GetMethod("RefreshOutput", Private, null, new[] { typeof(bool) }, null).Invoke(form, new object[] { true });
            var latest = SectMemberStatus.Build(sect, run);
            Check(latest.Count == 12 && latest[0].Detail == "Newest live reply" && latest[11].Status == "idle", "latest reply updates while unassigned roster member remains visible");
            int clicked = 0; panel.MemberSelected += delegate { clicked++; };
            Rectangle first = panel.CardBounds(0);
            typeof(Control).GetMethod("OnMouseClick", Private).Invoke(panel, new object[] { new MouseEventArgs(MouseButtons.Left, 1, first.Left + 10, first.Top + 10, 0) });
            Check(clicked == 1, "member cards support direct selection without launching tasks");
            typeof(Control).GetMethod("OnKeyDown",Private).Invoke(panel,new object[]{new KeyEventArgs(Keys.Right)});
            typeof(Control).GetMethod("OnKeyDown",Private).Invoke(panel,new object[]{new KeyEventArgs(Keys.Enter)});
            Check(clicked==2,"member cards support keyboard selection");
            Font original=form.Font;
            using(var enlarged=new Font(original.FontFamily,13.5f))
            {
                form.Font=enlarged;form.Size=new Size(820,600);Application.DoEvents();panel.Reflow();panel.Parent.PerformLayout();Application.DoEvents();
                for(int i=0;i<12;i++)Check(panel.ClientRectangle.Contains(panel.CardBounds(i)),"large-font status remains accessible "+i);
                Check(panel.Bottom<=panel.Parent.ClientSize.Height,"large-font status does not extend below the window");
                Capture(tabs.SelectedTab,Path.Combine(root,"member-states-large-font.png"));form.Font=original;
            }
            data.History.Clear();typeof(WorkshopForm).GetMethod("RefreshRuns",Private).Invoke(form,null);Application.DoEvents();
            Rectangle standby=panel.CardBounds(0);
            typeof(Control).GetMethod("OnMouseClick",Private).Invoke(panel,new object[]{new MouseEventArgs(MouseButtons.Left,1,standby.Left+10,standby.Top+10,0)});
            typeof(WorkshopForm).GetMethod("RefreshOutput",Private,null,new[]{typeof(bool)},null).Invoke(form,new object[]{true});
            Check(panel.MemberCount==4,"standby member selection is safe when no run history exists");
            form.Hide();
        }
        var persisted = new JavaScriptSerializer().Deserialize<SectRun>(new JavaScriptSerializer().Serialize(run));
        sect.Members[0].Name = "Changed after run";
        Check(SectMemberStatus.Build(sect, persisted)[0].Name == "Member 1", "historical roster remains stable after editing current members");
        using (var hall = new SectHallControl(delegate { return sect; }, delegate { return run; }, delegate { }))
        {
            hall.Dock = DockStyle.None; hall.Size = new Size(1200, 800); hall.SetPreviewTime(6); Capture(hall, Path.Combine(root, "hall-working.png"));
            hall.SetPreviewTime(13); Capture(hall, Path.Combine(root, "hall-before-handoff.png"));
            run.Transfers.Add(new SectTransfer { FromMember = "Member 1", ToMember = "Member 2", From = "task-0", To = "task-1" });
            hall.SetPreviewTime(13); Capture(hall, Path.Combine(root, "hall-handoff-start.png"));
            hall.SetPreviewTime(16.2); Capture(hall, Path.Combine(root, "hall-handoff.png"));
        }
        var previewSect=CompanionRolePresets.Create("Cloud Pavilion",root,false);
        var previewRun=new SectRun{Sect=previewSect.Name};
        for(int i=0;i<4;i++){
            var member=previewSect.Members[i];previewRun.Members.Add(new SectRunMember{Id=member.Id,Name=member.Name,CharacterId=member.CharacterId});
            previewRun.Steps.Add(new SectStepRun{Id="task-"+i,MemberId=member.Id,Member=member.Name,CharacterId=member.CharacterId,Status="running"});
        }
        string movie=Path.Combine(root,"hall-cycle-frames");Directory.CreateDirectory(movie);
        using(var hall=new SectHallControl(delegate{return previewSect;},delegate{return previewRun;},delegate{}))
        {
            hall.Dock=DockStyle.None;hall.Size=new Size(1000,640);hall.Font=UiText.Font(10);
            for(int i=0;i<240;i++){
                if(i==36)previewRun.Transfers.Add(new SectTransfer{From="task-0",To="task-1",FromMember=previewSect.Members[0].Name,ToMember=previewSect.Members[1].Name});
                hall.SetPreviewTime(4+i/30.0);Capture(hall,Path.Combine(movie,i.ToString("000")+".png"));
            }
        }
    }
}
