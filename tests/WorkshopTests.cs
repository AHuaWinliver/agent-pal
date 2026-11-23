// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AgentPal;
internal static class WorkshopTests
{
    private sealed class Fake : ISectAgent,ISectAgentProgress
    {
        public event Action<string> Output;
        public event Action<string> Process;
        internal void Tool(string value){if(Process!=null)Process(value);}
        public event Action<int> Completed;
        internal string Prompt;
        internal bool Stopped;
        public void Start(string prompt, string directory, string marker, string characterId) { Prompt=prompt; if (!Directory.Exists(directory) || !marker.StartsWith("sect-") || String.IsNullOrEmpty(characterId)) throw new Exception("invalid launch"); }
        internal void Emit(string s) { if(Output!=null)Output(s); }
        internal void Finish(int code) { if(Completed!=null)Completed(code); }
        public void Stop() { Stopped=true; }
        public void Dispose() { }
    }
    private static void Check(bool ok,string name) { if(!ok)throw new Exception(name); Console.WriteLine("PASS "+name); }
    private static bool AgentTerminology(Control control)
    {
        foreach(string old in new[]{"门派","掌门","符匠","执律","护山"})if(control.Text.Contains(old))return false;
        var grid=control as DataGridView;
        if(grid!=null)foreach(DataGridViewColumn column in grid.Columns)if(column.HeaderText.Contains("门派"))return false;
        foreach(Control child in control.Controls)if(!AgentTerminology(child))return false;
        return true;
    }
    private static CompanionSect Sect(string root,bool same)
    {
        var s=new CompanionSect {Name="测试门派",AutoHandoff=false};
        s.Members.Add(new SectMember {Id="a",Name="策士",Role="规划",Directory=Path.Combine(root,"a")});
        s.Members.Add(new SectMember {Id="b",Name="匠师",Role="实施",Directory=Path.Combine(root,same?"a":"b")});
        s.Steps.Add(new SectStep {Id="one",Member="a",Task="分析"}); s.Steps.Add(new SectStep {Id="two",Member="b",Task="实施"}); return s;
    }
    private static void Invalid(CompanionSect s,string name) { bool bad=false;try{SectExecution.Validate(s);}catch(ArgumentException){bad=true;}Check(bad,name); }
    [STAThread] private static int Main()
    {
        try
        {
            string root=Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");if(String.IsNullOrWhiteSpace(root))throw new Exception("isolated state required");
            Application.EnableVisualStyles();
            Directory.CreateDirectory(Path.Combine(root,"a"));Directory.CreateDirectory(Path.Combine(root,"b"));
            var agents=new List<Fake>();Func<ISectAgent> factory=delegate {var f=new Fake();agents.Add(f);return f;};
            var preset=CompanionRolePresets.Create("四道友研发",root,false);
            Invalid(preset,"preset requires explicit shared goal before execution");
            preset.Goal="只检查隔离测试目录中的示例，保留现有数据；验收需列出测试证据。";SectExecution.Validate(preset);
            Check(preset.Members.Count==4&&preset.Steps.Count==5&&preset.Members[0].Name=="青衡"&&preset.Members[1].CharacterId=="spark"&&preset.Members[2].Role.Contains("只读")&&preset.Members[3].Role.Contains("未运行"),"four complementary personas and role boundaries");
            Check(preset.Steps[4].DependsOn.Contains("review")&&preset.Steps[4].DependsOn.Contains("verify"),"leader waits for independent review and test evidence");
            string context=CompanionRolePresets.Context(preset,preset.Members[1]);
            Check(context.Contains(preset.Goal)&&context.Contains("符箓")&&context.Contains("不是用户授权")&&context.Contains("不共享完整聊天历史")&&context.Contains("qingheng"),"shared goal, original persona, roster and untrusted handoff context");
            var consultation=CompanionRolePresets.Create("四道友会诊",root,true);consultation.Goal="只读调查";SectExecution.Validate(consultation);
            Check(consultation.Steps[1].Task.Contains("只读")&&consultation.Steps[3].Task.Contains("不运行测试"),"research-first template overrides implementation and execution duties");
            using(var presetRun=new SectExecution(preset,factory))
            {
                presetRun.Pump(DateTime.UtcNow);Check(agents.Count==1&&agents[0].Prompt.Contains(preset.Goal)&&agents[0].Prompt.Contains("青年游修"),"preset is actually injected into launched CLI prompt");
                using(var hall=new SectHallControl(delegate{return preset;},delegate{return presetRun.Record;},delegate{}))
                {hall.Size=new Size(1000,880);using(var picture=new Bitmap(1000,880)){hall.DrawToBitmap(picture,new Rectangle(0,0,1000,880));picture.Save(Path.Combine(root,"four-companions-hall.png"));}}
            }
            agents.Clear();
            SectRun finished;
            var s=Sect(root,false);s.Steps[1].DependsOn="ONE";
            using(var run=new SectExecution(s,factory))
            {
                s.Steps[0].Task="outside mutation";run.Pump(DateTime.UtcNow);Check(agents.Count==1&&!agents[0].Prompt.Contains("outside mutation"),"snapshot and dependency gating");
                agents[0].Emit("计划：中文 😀");agents[0].Finish(0);run.Pump(DateTime.UtcNow);Check(agents.Count==2&&agents[1].Prompt.Contains("计划：中文 😀"),"upstream Unicode and case-insensitive dependency");
                Check(run.Record.Transfers.Count==1&&run.Record.Transfers[0].Text=="计划：中文 😀","real delivered context recorded for hall");
                agents[1].Finish(0);run.Pump(DateTime.UtcNow);Check(run.Record.Status=="success","successful DAG");
                finished=run.Record;
                run.Record.Steps[1].CharacterId="spark";
                using(var hall=new SectHallControl(delegate{return s;},delegate{return run.Record;},delegate{}))
                {hall.Size=new Size(880,680);using(var picture=new Bitmap(880,680)){hall.DrawToBitmap(picture,new Rectangle(0,0,880,680));picture.Save(Path.Combine(root,"sect-hall.png"));}Check(hall.Width==880,"pixel hall render with actual transfer");}
            }
            agents.Clear();using(var run=new SectExecution(Sect(root,false),factory)){run.Pump(DateTime.UtcNow);Check(agents.Count==2,"parallel independent directories");run.Cancel();Check(!run.IsRunning&&agents.TrueForAll(delegate(Fake f){return f.Stopped;}),"cancel owned agents");agents[0].Emit("late");run.Pump(DateTime.UtcNow);Check(!run.Record.Steps[0].Output.Contains("late"),"ignore late callbacks");}
            agents.Clear();using(var run=new SectExecution(Sect(root,true),factory)){run.Pump(DateTime.UtcNow);Check(agents.Count==1,"same directory serialized");agents[0].Finish(0);run.Pump(DateTime.UtcNow);Check(agents.Count==2,"serialized next task");}
            Check(SectExecution.DirectoriesOverlap(Path.Combine(root,"a"),Path.Combine(root,"a","child")),"ancestor directories overlap");
            agents.Clear();s=Sect(root,true);s.Steps[0].ExecutionMode="read-only";s.Steps[1].ExecutionMode="isolated";
            using(var parallel=new SectExecution(s,factory))
            {
                parallel.Pump(DateTime.UtcNow);
                Check(agents.Count==2&&agents[1].Prompt.Contains("隔离输出并行"),"read-only review and isolated verification can overlap in the same project");
                agents[0].Emit(new string('甲',17000)+"最新进度🌱");parallel.Pump(DateTime.UtcNow);
                Check(parallel.Record.Steps[0].Output.EndsWith("最新进度🌱")&&!UiText.HasUnpairedSurrogate(parallel.Record.Steps[0].Output),"bounded execution output retains newest Unicode progress rather than freezing on the first chunk");
            }
            agents.Clear();s=Sect(root,false);s.Steps[0].ExecutionMode="serial";
            using(var exclusive=new SectExecution(s,factory)){exclusive.Pump(DateTime.UtcNow);Check(agents.Count==1,"exclusive serial tasks block concurrency even across different project directories");}
            agents.Clear();s=Sect(root,true);s.Steps[0].ExecutionMode="read-only";
            using(var writer=new SectExecution(s,factory)){writer.Pump(DateTime.UtcNow);Check(agents.Count==1,"overlapping workspace writes stay exclusive while a reader is active");}
            agents.Clear();s=Sect(root,false);s.Steps[1].DependsOn="one";
            using(var paused=new SectExecution(s,factory))
            {
                paused.Pump(DateTime.UtcNow);paused.SetPaused(true);agents[0].Finish(0);paused.Pump(DateTime.UtcNow);
                Check(agents.Count==1&&paused.Record.Steps[0].Status=="success"&&paused.Record.Steps[1].WaitReason=="已暂停派发",
                    "pause drains the live worker and leaves downstream work queued without another CLI call");
                paused.SetPaused(false);paused.Pump(DateTime.UtcNow);agents[1].Emit("failure context");agents[1].Finish(1);paused.Pump(DateTime.UtcNow);
                Check(paused.RetryFailed(DateTime.UtcNow),"a failed run can be retried explicitly");
                paused.Pump(DateTime.UtcNow);
                Check(agents.Count==3&&paused.Record.Steps[0].Attempt==1&&paused.Record.Steps[1].Attempt==2&&paused.Record.Steps[1].Output.Contains("failure context"),
                    "retry preserves successful steps and earlier failure context");
                agents[2].Finish(1);paused.Pump(DateTime.UtcNow);paused.RetryFailed(DateTime.UtcNow);paused.Pump(DateTime.UtcNow);agents[3].Finish(1);paused.Pump(DateTime.UtcNow);
                Check(!paused.RetryFailed(DateTime.UtcNow),"three-attempt cap prevents unbounded repeated failures");
            }
            agents.Clear();s=Sect(root,false);s.Steps[1].DependsOn="one";using(var run=new SectExecution(s,factory)){run.Pump(DateTime.UtcNow);agents[0].Finish(1);run.Pump(DateTime.UtcNow);Check(run.Record.Status=="failed"&&agents.Count==1&&run.Record.Steps[1].Status=="blocked","failed upstream blocks downstream");}
            TestTimeLimits(s, factory, agents);
            TestRecovery(root,factory,agents);
            s=Sect(root,false);s.Steps[0].DependsOn="two";s.Steps[1].DependsOn="one";Invalid(s,"reject cycle");s=Sect(root,false);s.Steps[1].Id="ONE";Invalid(s,"reject duplicate ID");s=Sect(root,false);s.Steps[1].DependsOn="missing";Invalid(s,"reject missing dependency");
            var data=new WorkshopData();data.Sects.Add(Sect(root,false));data.Events.Add(new PlannedEvent {Title="休息 😀",When=new DateTime(2026,11,5,9,0,0),Repeat="daily"});
            data.History.Add(finished);
            string path=Path.Combine(root,"workshop.json");WorkshopFiles.Save(path,data);WorkshopFiles.Save(path,data);
            var loaded=WorkshopFiles.Load<WorkshopData>(path);
            Check(loaded.Events[0].Title=="休息 😀","atomic Unicode save/load");
            TestRunConversation(loaded.History[0]);
            Check(PlannedEvent.Next(new DateTime(2026,11,1,9,0,0),new DateTime(2026,11,4,12,0,0),"daily")==new DateTime(2026,11,5,9,0,0),"daily missed event catchup");
            Check(PlannedEvent.Next(new DateTime(2026,11,1,9,0,0),new DateTime(2026,11,15,9,0,0),"weekly")==new DateTime(2026,11,22,9,0,0),"weekly next event");
            using(var form=new WorkshopForm(data,delegate{},delegate{return null;},delegate{throw new Exception("must not call AI");},delegate{}))
            {
                form.Show(); Application.DoEvents();
                var fields=BindingFlags.NonPublic|BindingFlags.Instance;
                ((NumericUpDown)typeof(WorkshopForm).GetField("runLimit",fields).GetValue(form)).Value=0;
                ((NumericUpDown)typeof(WorkshopForm).GetField("stepLimit",fields).GetValue(form)).Value=0;
                typeof(WorkshopForm).GetMethod("SaveSect",fields).Invoke(form,null);
                WorkshopFiles.Save(path,data);
                var saved=WorkshopFiles.Load<WorkshopData>(path).Sects[0];
                Check(saved.RunLimitMinutes==0&&saved.StepLimitMinutes==0,"UI saves and reloads unlimited overall and step limits");
                ((TabControl)form.Controls[0]).SelectedIndex=3;
                Application.DoEvents();
                var view=(ChatConversationView)typeof(WorkshopForm).GetField("runConversation",fields).GetValue(form);
                Check(view.Visible&&view.MessageCount==5&&view.HasBothSides&&view.UnicodeSupportedForTest(),"saved workshop run displays two-sided Unicode bubbles");
                form.Hide();
            }
            var presetData=new WorkshopData();presetData.Sects.Add(preset);
            using(var form=new WorkshopForm(presetData,delegate{},delegate{return null;},delegate{},delegate{}))
            {
                form.Show();System.Windows.Forms.Application.DoEvents();
                foreach(string language in new[]{"zh-CN","en"})
                {
                    UiLanguage.Set(language);UiLanguageBindings.Apply(form);
                    Check(AgentTerminology(form),"Agent terminology replaces sect concepts in workspace controls and headers: "+language);
                    Check(form.Text.Contains("Agent"),"localized collaboration window identifies Agent workflows: "+language);
                }
                typeof(WorkshopForm).GetMethod("SaveSect",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);Check(presetData.Sects[0].Goal==preset.Goal&&presetData.Sects[0].TemplateId=="four-companions-v1","UI preserves goal, template and editable roles");using(var picture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(picture,new Rectangle(0,0,form.Width,form.Height));picture.Save(Path.Combine(root,"role-presets-ui.png"));}form.Hide();
                UiLanguage.Set("zh-CN");
            }
            UiLanguage.Set("en");
            using(var preview=new WorkshopForm(presetData,delegate{},delegate{return null;},delegate{},delegate{}))
            {preview.Show();UiLanguageBindings.Apply(preview);Application.DoEvents();using(var picture=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(picture,new Rectangle(0,0,preview.Width,preview.Height));picture.Save(Path.Combine(root,"workshop-controls-en.png"));}preview.Hide();}
            UiLanguage.Set("zh-CN");
            TestEditorRows(root);
            TestLiveScroll(root);
            Console.WriteLine("All workshop checks passed. No Claude/model calls.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }

    private static void TestEditorRows(string root)
    {
        var draft = new WorkshopData();
        draft.Sects.Add(Sect(root, false));
        using (var form = new WorkshopForm(draft, delegate { }, delegate { return null; }, delegate { throw new Exception("must not execute"); }, delegate { }))
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            form.Show();
            Application.DoEvents();
            var members = (DataGridView)typeof(WorkshopForm).GetField("members", flags).GetValue(form);
            var tasks = (DataGridView)typeof(WorkshopForm).GetField("steps", flags).GetValue(form);
            int memberHeight = members.Rows[0].Height, taskHeight = tasks.Rows[0].Height;
            string role = "第一行职责\r\n" + new string('测', 1250);
            string instructions = "第一行任务\r\n" + new string('项', 1250);
            UseRowDialog(form, "编辑成员", delegate(Form editor) { RowInput(editor, "memberRole").Text = role; });
            UseRowDialog(form, "编辑任务", delegate(Form editor) { RowInput(editor, "taskInstructions").Text = instructions; });
            Application.DoEvents();
            Check(members.ReadOnly && tasks.ReadOnly && members.SelectionMode == DataGridViewSelectionMode.FullRowSelect &&
                members.Rows[0].Height > memberHeight && tasks.Rows[0].Height > taskHeight && members.Rows[0].Height < 115,
                "read-only lists wrap compact summaries and select a full row");
            form.Size = form.MinimumSize;
            Application.DoEvents();
            Check(members.Rows[0].Height > memberHeight && tasks.Rows[0].Height > taskHeight, "text stays wrapped when the workshop is resized");
            UseRowDialog(form, "编辑成员", delegate(Form editor)
            {
                TextBox input = RowInput(editor, "memberRole");
                Check(input.Multiline && input.ScrollBars == ScrollBars.Vertical && input.Text == role, "Edit member opens the full scrollable role, not the abbreviated summary");
                using (var picture = new Bitmap(editor.Width, editor.Height))
                {
                    editor.DrawToBitmap(picture, new Rectangle(0, 0, editor.Width, editor.Height));
                    picture.Save(Path.Combine(root, "member-editor.png"));
                }
                input.Text = "discard this edit";
            }, false);
            Check(Convert.ToString(members.Rows[0].Cells[2].Value) == role, "Cancel does not change the draft");
            UseRowDialog(form, "新增成员", delegate(Form editor)
            {
                RowInput(editor, "memberName").Text = "新成员";
                RowInput(editor, "memberRole").Text = "执行检查";
            });
            string memberId = Convert.ToString(members.CurrentRow.Cells[0].Value);
            Check(members.Rows.Count == 3 && memberId == "member-1" && !members.AllowUserToDeleteRows && !tasks.AllowUserToDeleteRows, "explicit member add creates a selected draft with unique ID and protected deletion");
            UseRowDialog(form, "新增任务", delegate(Form editor)
            {
                RowInput(editor, "taskInstructions").Text = "新增任务说明";
                RowInput(editor, "taskDependencies").Text = "TWO,\r\none";
            });
            Check(tasks.Rows.Count == 3 && Convert.ToString(tasks.CurrentRow.Cells[0].Value) == "task-1" && Convert.ToString(tasks.CurrentRow.Cells[1].Value) == memberId &&
                Convert.ToString(members.CurrentRow.Cells[1].Value) == "新成员", "new task defaults to the selected member");
            UseRowDialog(form, "编辑成员", delegate(Form editor) { RowInput(editor, "memberId").Text = "new-owner"; });
            Check(Convert.ToString(tasks.Rows[2].Cells[1].Value) == "new-owner", "editing a member ID updates its task assignments");
            AssertEditBlocked(form, "RemoveMember", "task-1");
            tasks.CurrentCell = tasks.Rows[1].Cells[0];
            AssertEditBlocked(form, "RemoveStep", "task-1");
            UseRowDialog(form, "编辑任务", delegate(Form editor) { RowInput(editor, "taskId").Text = "second"; });
            Check(Convert.ToString(tasks.Rows[2].Cells[2].Value) == "second, one", "editing a task ID updates case-insensitive downstream dependencies");
            AssertEditBlocked(form, "RemoveStep", "task-1");
            tasks.CurrentCell = tasks.Rows[2].Cells[0];
            typeof(WorkshopForm).GetMethod("RemoveStep", flags).Invoke(form, null);
            typeof(WorkshopForm).GetMethod("RemoveMember", flags).Invoke(form, null);
            typeof(WorkshopForm).GetMethod("SaveSect", flags).Invoke(form, null);
            Check(draft.Sects[0].Members.Count == 2 && draft.Sects[0].Steps.Count == 2 && draft.Sects[0].Members[0].Role == role && draft.Sects[0].Steps[0].Task == instructions,
                "unreferenced rows can be removed and multiline draft text survives saving");
            form.Hide();
        }
    }

    private static TextBox RowInput(Form editor, string name) { return (TextBox)editor.Controls.Find(name, true)[0]; }

    private static Button FindButton(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            var button = child as Button;
            if (button != null && button.Text == text) return button;
            button = FindButton(child, text);
            if (button != null) return button;
        }
        return null;
    }

    private static void UseRowDialog(WorkshopForm form, string buttonText, Action<Form> fill, bool confirm = true)
    {
        var button = FindButton(form, UiLanguage.Text(buttonText));
        if (button == null) throw new Exception("Missing editor button: " + buttonText);
        bool opened = false;
        Exception failure = null;
        form.BeginInvoke(new Action(delegate
        {
            Form editor = null;
            foreach (Form window in Application.OpenForms) if (window.Modal && window != form) { editor = window; break; }
            if (editor == null) return;
            opened = true;
            try
            {
                fill(editor);
                if (confirm) ((Button)editor.Controls.Find("confirmRowEdit", true)[0]).PerformClick();
                if (confirm && editor.DialogResult != DialogResult.OK) throw new Exception("Valid edit was not accepted: " + buttonText);
            }
            catch (Exception ex) { failure = ex; }
            if (editor.DialogResult != DialogResult.OK) editor.DialogResult = DialogResult.Cancel;
        }));
        button.PerformClick();
        if (failure != null) throw failure;
        if (!opened) throw new Exception("The editor did not open: " + buttonText);
    }

    private static void AssertEditBlocked(WorkshopForm form, string method, string taskId)
    {
        bool blocked = false;
        try { typeof(WorkshopForm).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null); }
        catch (TargetInvocationException error) { blocked = error.InnerException is ArgumentException && error.InnerException.Message.Contains(taskId); }
        Check(blocked, method + " rejects a referenced row and names the referring task");
    }

    private static void TestTimeLimits(CompanionSect plan, Func<ISectAgent> factory, List<Fake> agents)
    {
        DateTime now=DateTime.UtcNow;
        agents.Clear();
        plan.StepLimitMinutes=1;
        using(var run=new SectExecution(plan,factory))
        {
            run.Pump(now);
            run.Pump(now.AddSeconds(59));
            Check(!agents[0].Stopped&&run.IsRunning,"step still runs before custom deadline");
            run.Pump(now.AddMinutes(1));
            Check(agents[0].Stopped&&agents.Count==1&&run.Record.Steps[0].Status=="timeout"&&
                run.Record.Steps[1].Status=="blocked"&&!run.IsRunning,"step timeout stops the agent and blocks dependents");
        }
        agents.Clear();
        plan.StepLimitMinutes=0;
        plan.RunLimitMinutes=20;
        using(var run=new SectExecution(plan,factory))
        {
            run.Pump(now);
            run.Pump(now.AddMinutes(15));
            Check(!agents[0].Stopped&&run.IsRunning,"unlimited step continues past the old ten-minute limit");
            run.Pump(now.AddMinutes(20));
            Check(agents[0].Stopped&&agents.Count==1&&run.Record.Status=="timeout"&&
                run.Record.Steps[1].Status=="blocked"&&!run.IsRunning,"overall deadline stops unlimited step and skips queued work");
            run.Cancel();
            Check(run.Record.Status=="timeout","cancelling a finished run preserves its timeout status");
        }
    }

    private static void TestRecovery(string root,Func<ISectAgent> factory,List<Fake> agents)
    {
        var plan=Sect(root,false);plan.AutoHandoff=true;plan.RunLimitMinutes=0;plan.StepLimitMinutes=0;plan.Steps[1].DependsOn="one";
        agents.Clear();SectRun history;
        using(var run=new SectExecution(plan,factory))
        {
            DateTime now=DateTime.UtcNow;run.Pump(now);agents[0].Tool("[工具失败] test exited 1");agents[0].Emit("已创建文件；验证失败 🌱");agents[0].Finish(1);run.Pump(now);
            var step=run.Record.Steps[0];
            Check(agents.Count==2&&step.MemberId=="b"&&step.Attempt==2&&step.PreviousAttempts.Count==1&&
                step.Directory==Path.Combine(root,"a")&&agents[1].Prompt.Contains("已创建文件；验证失败 🌱")&&
                agents[1].Prompt.Contains("工作目录："+Path.Combine(root,"a"))&&agents[1].Prompt.Contains("test exited 1"),"failure hands over to a distinct member with original workspace and prior evidence");
            agents[0].Emit("STALE");agents[0].Finish(1);run.Pump(now);
            Check(step.Status=="running"&&!step.Output.Contains("STALE")&&agents.Count==2,"retired callbacks cannot corrupt or complete the replacement worker");
            run.Pump(now.AddDays(60));Check(run.IsRunning&&!agents[1].Stopped,"zero overall and step limits remain unlimited across many days");
            agents[1].Emit("修复完成\nAGENT_PAL_RESULT: success");agents[1].Finish(0);run.Pump(now.AddDays(60));
            Check(agents.Count==3&&step.Status=="success"&&agents[2].Prompt.Contains("修复完成"),"downstream starts only after successful takeover");
            agents[2].Finish(0);run.Pump(now.AddDays(60));history=run.Record;
            var projection=new SectRunConversation();projection.Update(history);
            Check(projection.Conversation.Messages.Exists(message=>message.Speaker=="策士"&&message.Text.Contains("验证失败"))&&
                projection.Conversation.Messages.Exists(message=>message.Speaker=="匠师"&&message.Text.Contains("修复完成")),"failure and recovery replies retain their actual speakers");
        }
        string path=Path.Combine(root,"handoff.json");WorkshopFiles.Save(path,history);
        Check(WorkshopFiles.Load<SectRun>(path).Steps[0].PreviousAttempts[0].Output.Contains("🌱"),"takeover history persists with Unicode evidence");
        agents.Clear();plan=Sect(root,false);plan.AutoHandoff=true;plan.Steps.RemoveAt(1);
        plan.Members.Add(new SectMember{Id="c",Name="执律",Role="审查",Directory=root});
        plan.Members.Add(new SectMember{Id="d",Name="护山",Role="测试",Directory=root});
        using(var run=new SectExecution(plan,factory))
        {
            run.Pump(DateTime.UtcNow);
            for(int i=0;i<3;i++){agents[i].Finish(1);run.Pump(DateTime.UtcNow);}
            Check(agents.Count==3&&run.Record.Status=="failed"&&run.Record.Steps[0].PreviousAttempts.Count==2&&!run.RetryFailed(DateTime.UtcNow),"automatic takeover stops after three total attempts without cycling through members");
        }
        agents.Clear();plan=Sect(root,false);plan.AutoHandoff=true;
        using(var run=new SectExecution(plan,factory))
        {
            run.Pump(DateTime.UtcNow);agents[0].Finish(1);run.Pump(DateTime.UtcNow);
            Check(agents.Count==2&&run.Record.Steps[0].Status=="pending","takeover waits while the replacement member is already busy");
            agents[1].Finish(0);run.Pump(DateTime.UtcNow);Check(agents.Count==3,"takeover starts after replacement member becomes free");
            run.Cancel();Check(agents.Count==3&&!run.IsRunning,"user cancellation never triggers another takeover");
        }
        agents.Clear();plan=Sect(root,false);plan.AutoHandoff=true;plan.Steps[1].DependsOn="one";
        using(var run=new SectExecution(plan,factory))
        {
            run.Pump(DateTime.UtcNow);agents[0].Emit("需要用户授权\nAGENT_PAL_RESULT: needs-user");agents[0].Finish(0);run.Pump(DateTime.UtcNow);
            Check(agents.Count==1&&run.Record.Steps[0].Status=="needs-user"&&run.Record.Steps[1].Status=="blocked"&&!run.IsRunning,"requests for permission or user decisions stop without handing off or hanging dependents");
        }
        agents.Clear();using(var run=new SectExecution(plan,factory))
        {
            run.Pump(DateTime.UtcNow);agents[0].Emit("检查有问题\nAGENT_PAL_RESULT: failed");agents[0].Finish(0);run.Pump(DateTime.UtcNow);
            Check(agents.Count==2&&run.Record.Steps[0].Attempt==2,"explicit unsuccessful task result triggers takeover even with exit code zero");
        }
        agents.Clear();plan.StepLimitMinutes=1;plan.RunLimitMinutes=0;
        using(var run=new SectExecution(plan,factory))
        {
            DateTime now=DateTime.UtcNow;run.Pump(now);run.Pump(now.AddMinutes(1));
            Check(agents[0].Stopped&&agents.Count==2&&run.Record.Steps[0].PreviousAttempts[0].Status=="timeout","step timeout can hand over while overall duration is unlimited");
        }
        agents.Clear();plan.RunLimitMinutes=1;plan.StepLimitMinutes=0;
        using(var run=new SectExecution(plan,factory))
        {
            DateTime now=DateTime.UtcNow;run.Pump(now);run.Pump(now.AddMinutes(1));
            Check(agents.Count==1&&agents[0].Stopped&&run.Record.Status=="timeout","finite overall timeout stops recovery rather than launching a replacement");
        }
        agents.Clear();plan.RunLimitMinutes=0;plan.Steps.RemoveAt(1);plan.Members.RemoveAt(1);
        using(var run=new SectExecution(plan,factory))
        {
            run.Pump(DateTime.UtcNow);agents[0].Finish(1);run.Pump(DateTime.UtcNow);
            Check(agents.Count==1&&!run.IsRunning&&run.Record.Status=="failed","single-member failure stays visible without repeatedly retrying the same member");
        }
    }

    private static void TestRunConversation(SectRun run)
    {
        var projection=new SectRunConversation();
        Check(projection.Update(run),"loaded run projects conversation");
        var messages=projection.Conversation.Messages;
        Check(messages.Count==5&&messages[1].Speaker=="策士"&&messages[1].Text.Contains("计划：中文 😀")&&
            messages[2].Speaker=="策士"&&messages[2].CharacterId==messages[1].CharacterId&&
            messages[2].Role=="user"&&messages[2].Text.Contains("匠师")&&messages[4].CharacterId=="spark",
            "record attributes replies and actual handoffs to their companion");
        ChatMessage reply=messages[4];
        run.Steps[1].Output="追加输出";
        Check(projection.Update(run)&&Object.ReferenceEquals(reply,messages[4])&&reply.Text.Contains("追加输出"),
            "streaming updates retain message identity");
        run.Steps[0].Output="较早启动的道友现在继续回复";
        run.Steps[0].Updated=DateTime.UtcNow.AddSeconds(1).ToString("o");
        Check(projection.Update(run)&&Object.ReferenceEquals(projection.LatestReply,messages[1]),"parallel earlier-started member becomes the latest reply when new output arrives");
        run.Steps[0].Updated=DateTime.UtcNow.ToString("o");run.Steps[1].Updated=DateTime.UtcNow.AddSeconds(-2).ToString("o");
        var reopened=new SectRunConversation();reopened.Update(run);
        Check(reopened.LatestReply.Speaker=="策士","reopening hidden records uses actual update timestamps instead of task-start order");
    }

    private static void TestLiveScroll(string root)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var data=new WorkshopData();data.Sects.Add(Sect(root,false));
        var run=new SectRun {Sect="测试门派",Started=DateTime.Now.ToString("o")};
        run.Steps.Add(new SectStepRun {Id="first",Member="策士",Status="running",Started=run.Started,Output=String.Join("\n",System.Linq.Enumerable.Range(0,150))+"\n最新一句",Task="任务"});
        run.Steps.Add(new SectStepRun {Id="second",Member="匠师",Status="running",Started=run.Started,Output="第二个道友",Task="任务"});
        data.History.Add(run);
        using(var form=new WorkshopForm(data,delegate{},delegate{return null;},delegate{},delegate{}))
        {
            form.Show();((TabControl)form.Controls[0]).SelectedIndex=3;Application.DoEvents();
            run.Steps[0].Output+="\n又一条实时进度";
            typeof(WorkshopForm).GetMethod("RefreshRuns",flags).Invoke(form,null);Application.DoEvents();
            var view=(ChatConversationView)typeof(WorkshopForm).GetField("runConversation",flags).GetValue(form);
            var content=(Panel)typeof(ChatConversationView).GetField("content",flags).GetValue(view);
            RichTextBox body=null;
            foreach(Control row in content.Controls)
                foreach(Control card in row.Controls)
                    foreach(Control control in card.Controls)
                    {
                        var text=control as RichTextBox;
                        if(text!=null&&text.Text.Contains("又一条实时进度"))body=text;
                    }
            Check(body!=null&&body.GetCharIndexFromPosition(new Point(2,2))>0,"live run follows the newest text inside a long member bubble");
            body.Select(0,0);body.ScrollToCaret();
            typeof(Form).GetMethod("OnActivated",flags).Invoke(form,new object[]{EventArgs.Empty});Application.DoEvents();
            Check(body.GetCharIndexFromPosition(new Point(2,2))>0,"returning to the workshop restores newest member text even without a new stream event");
            form.Hide();
        }
    }
}
