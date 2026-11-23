// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class WorkshopForm : AppWindow
    {
        private readonly WorkshopData data;
        private readonly Action save;
        private readonly ComboBox sects = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly TextBox name = new TextBox { Width = 180 };
        private readonly TextBox goal = new TextBox { Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical };
        private readonly DataGridView members = Grid(), steps = Grid(), events = Grid();
        private readonly ListBox history = new ListBox { Dock = DockStyle.Left, Width = 245 };
        private readonly ComboBox batches=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=280,DropDownWidth=620};
        private bool updatingBatches;
        private readonly RunMemberStatusPanel runMemberStates = new RunMemberStatusPanel();
        private readonly ChatConversationView runConversation = new ChatConversationView { Dock = DockStyle.Fill };
        private readonly Label runSummary = new Label { Dock = DockStyle.Top, Height = 78, Padding = new Padding(10), AutoEllipsis = true };
        private readonly SectRunConversation runMessages = new SectRunConversation();
        private string inspectedRunMember;
        private readonly List<SectRun> listedRuns = new List<SectRun>();
        private readonly NumericUpDown runLimit = new NumericUpDown { Minimum = 0, Maximum = CompanionSect.MaximumLimitMinutes, Width = 64 };
        private readonly NumericUpDown stepLimit = new NumericUpDown { Minimum = 0, Maximum = CompanionSect.MaximumLimitMinutes, Width = 64 };
        private readonly NumericUpDown parallelLimit=new NumericUpDown {Minimum=1,Maximum=8,Value=2,Width=50};
        private readonly CheckBox autoHandoff=new CheckBox {Text="失败时由其他成员接手",AutoSize=true,Checked=true};
        private readonly Timer refresh = new Timer { Interval = 300 };
        private readonly Button run = new Button { Text = "确认并执行协作", AutoSize = true }, stop = new Button { Text = "停止本轮", AutoSize = true };
        private readonly List<Control> editors = new List<Control>();
        private readonly Func<SectExecution> active;
        private readonly Action<CompanionSect> execute;
        private readonly Action cancel;
        private readonly TabControl tabs=new TabControl {Dock=DockStyle.Fill};
        private readonly TabPage hallPage=new TabPage("Agent 工作区");
        private List<PlannedEvent> eventBaseline;
        internal WorkshopForm(WorkshopData data, Action save, Func<SectExecution> active, Action<CompanionSect> execute, Action cancel)
        {
            this.data = data; this.save = save; this.active = active; this.execute = execute; this.cancel = cancel;
            Text = "AgentPal · 事件规划与 Agent 协作"; Size = new Size(1000, 740); MinimumSize = new Size(820, 600); StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 9F);
            TabPage sectPage = new TabPage("Agent 团队"), eventPage = new TabPage("事件规划"), logPage = new TabPage(UiLanguage.Text("运行记录"));
            var hallDetails = new UnicodeRichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Text = UiLanguage.Text("点击工作区中的 Agent查看实时回复与交接摘要。执行记录会保存在本机。") };
            var hall = new SectHallControl(delegate {var execution=active();return execution==null?sects.SelectedItem as CompanionSect:execution.Plan; }, delegate { return SelectedRun; }, delegate(string text)
            {
                hallDetails.Text = text;
                hallDetails.Select(hallDetails.TextLength,0);
                hallDetails.ScrollToCaret();
            });
            Activated += delegate { RefreshOutput(true); hall.RefreshInspection(); };
            var hallScroll = new SectHallViewport(hall);
            var hallSplit = new SplitContainer { Size=new Size(800,600), Orientation=Orientation.Horizontal, Dock=DockStyle.Fill, SplitterWidth=6, Panel1MinSize=120, Panel2MinSize=80, SplitterDistance=440 };
            hallSplit.Panel1.Controls.Add(hallScroll);hallSplit.Panel2.Controls.Add(hallDetails);hallPage.Controls.Add(hallSplit);
            // Keep the scene/details proportion during resize, including when
            // the tab is hidden. Only a user splitter move changes the ratio.
            double hallRatio=.75;bool arrangingHall=false;
            hallSplit.SizeChanged+=delegate
            {
                int room=hallSplit.ClientSize.Height-hallSplit.SplitterWidth;
                if(room<hallSplit.Panel1MinSize+hallSplit.Panel2MinSize)return;
                arrangingHall=true;
                try{hallSplit.SplitterDistance=Math.Max(hallSplit.Panel1MinSize,Math.Min(room-hallSplit.Panel2MinSize,(int)(room*hallRatio)));}
                finally{arrangingHall=false;}
            };
            hallSplit.SplitterMoved+=delegate
            {if(!arrangingHall&&hallSplit.ClientSize.Height>hallSplit.SplitterWidth)hallRatio=hallSplit.SplitterDistance/(double)(hallSplit.ClientSize.Height-hallSplit.SplitterWidth);};
            tabs.TabPages.AddRange(new[] { sectPage, hallPage, eventPage, logPage }); Controls.Add(tabs);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(5) };
            var add = Button("研发 Agent 团队", delegate { NewSect(false); }); var consult = Button("审查 Agent 团队", delegate { NewSect(true); }); var commit = Button("保存 Agent 团队", SaveSect);
            var pause=Button(UiLanguage.Text("暂停派发"),delegate{var execution=active();if(execution!=null&&execution.IsRunning){execution.SetPaused(!execution.Record.Paused);save();RefreshRuns();}});
            var retry=Button(UiLanguage.Text("重试失败步骤"),delegate{var execution=active();if(execution!=null&&execution.RetryFailed(DateTime.UtcNow)){save();RefreshRuns();}});
            toolbar.Controls.AddRange(new Control[] { sects, name, add, consult, commit, run });
            var batchBar=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(5),WrapContents=true};
            batchBar.Controls.AddRange(new Control[]{LimitLabel("运行批次"),batches,pause,stop,retry});Controls.Add(batchBar);
            var limits = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize=true, Padding = new Padding(7, 1, 4, 0), WrapContents = true };
            limits.Controls.AddRange(new Control[] {
                LimitLabel("整次时限（分钟）"), runLimit,
                LimitLabel("单轮时限（分钟）"), stepLimit,
                LimitLabel("0 表示不限时"),LimitLabel("并发工位"),parallelLimit,autoHandoff
            });
            var goalPanel=new Panel { Dock=DockStyle.Top,Height=72,Padding=new Padding(8) };
            goalPanel.Controls.Add(goal);goalPanel.Controls.Add(new Label { Dock=DockStyle.Top,Height=21,Text="共同目标 / 授权范围 / 验收要求（每位 Agent都能收到，不共享完整聊天历史）" });
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(8) };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            table.Controls.Add(Label(UiLanguage.Text("选中一行后点击编辑，可查看或修改完整内容。")));
            var line = new FlowLayoutPanel { Dock = DockStyle.Fill };
            line.Controls.Add(LimitLabel("成员定义"));
            line.Controls.Add(Button(UiLanguage.Text("新增成员"), AddMember));
            line.Controls.Add(Button(UiLanguage.Text("编辑成员"), EditMember));
            line.Controls.Add(Button(UiLanguage.Text("删除成员"), RemoveMember));
            var stepLine = new FlowLayoutPanel { Dock = DockStyle.Fill };
            stepLine.Controls.Add(LimitLabel("任务与依赖"));
            stepLine.Controls.Add(Button(UiLanguage.Text("新增任务"), AddStep));
            stepLine.Controls.Add(Button(UiLanguage.Text("编辑任务"), EditStep));
            stepLine.Controls.Add(Button(UiLanguage.Text("删除协作任务"), RemoveStep));
            table.Controls.Add(line); table.Controls.Add(members); table.Controls.Add(stepLine); table.Controls.Add(steps);
            sectPage.Controls.Add(table); sectPage.Controls.Add(goalPanel); sectPage.Controls.Add(limits); sectPage.Controls.Add(toolbar);
            Columns(members, new[] { "Id", "Name", "Role", "Directory", "CharacterId" }, new[] { "成员编号", "姓名", "职责 / 系统提示", "工作目录", "人物形象" });
            Columns(steps, new[] { "Id", "Member", "DependsOn", "Task" }, new[] { "任务编号", "成员编号", "依赖任务编号", "任务内容" });
            ConfigureEditorGrid(members, new[] { 12, 14, 40, 22, 12 }, "Role", "Directory");
            ConfigureEditorGrid(steps, new[] { 13, 14, 20, 53 }, "DependsOn", "Task");
            Columns(events, new[] { "Title", "When", "Repeat", "Sect", "Done" }, new[] { "事件", "本地时间，如2026-11-05 09:00", "重复：once/daily/weekly", "关联 Agent 团队名称（可空）", "已完成" });
            var eventToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(6) };
            eventToolbar.Controls.Add(Button("保存事件", SaveEvents)); eventToolbar.Controls.Add(new Label { Text = "到点提醒，不自动执行。关联 Agent 团队仅作为执行入口；补发过期提醒一次。", AutoSize = true, Padding = new Padding(5) });
            eventPage.Controls.Add(events); eventPage.Controls.Add(eventToolbar);
            logPage.Controls.Add(runConversation); logPage.Controls.Add(runSummary);logPage.Controls.Add(runMemberStates); logPage.Controls.Add(history);
            logPage.SizeChanged += delegate { runMemberStates.Reflow(); };
            runMemberStates.MemberSelected += delegate(SectMemberStatus member)
            {
                inspectedRunMember=member.Name;
                ChatMessage latest = runMessages.Conversation == null ? null : runMessages.Conversation.Messages.FindLast(delegate(ChatMessage message) { return message.Role == "assistant" && message.Speaker == UiLanguage.Text(member.Name); });
                if (latest != null) runConversation.FollowMessage(latest);
            };
            runConversation.VisibleChanged += delegate { RefreshOutput(true); };
            ReloadEvents();
            sects.SelectedIndexChanged += delegate { LoadSect(); };
            run.Click += delegate { Guard(delegate { SaveSect(); CompanionSect s = sects.SelectedItem as CompanionSect; if (s == null) return;
                string dirs = ""; foreach (SectMember m in s.Members) dirs += "\n" + m.Name + "：" + m.Directory;
                string scope = UiLanguage.Format("将调用本机 Claude 执行 {0} 项任务，可能读写以下目录并消耗模型额度。", s.Steps.Count);
                if (MessageBox.Show(this, scope + "\n" + LimitSummary(s.RunLimitMinutes, s.StepLimitMinutes) + dirs + "\n\n" + UiLanguage.Text("确认开始？"), "确认协作范围", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                execute(s); tabs.SelectedTab = hallPage; RefreshRuns(); if(history.Items.Count > 0) history.SelectedIndex = 0; }); };
            stop.Click += delegate { cancel(); RefreshRuns(); };
            history.SelectedIndexChanged += delegate {if(updatingBatches)return;inspectedRunMember=null;if(batches.SelectedIndex!=history.SelectedIndex)batches.SelectedIndex=history.SelectedIndex;RefreshOutput();};
            batches.SelectedIndexChanged+=delegate{if(!updatingBatches&&history.SelectedIndex!=batches.SelectedIndex)history.SelectedIndex=batches.SelectedIndex;};
            editors.AddRange(new Control[] { sects, name, goal, add, consult, commit, members, steps, line, stepLine, limits });
            refresh.Tick += delegate { bool busy = active() != null && active().IsRunning; run.Enabled = sects.SelectedItem!=null; stop.Enabled = busy; RefreshRuns();
                var execution=active();pause.Enabled=busy;pause.Text=UiLanguage.Text(busy&&execution.Record.Paused?"继续派发":"暂停派发");
                retry.Enabled=execution!=null&&!busy&&execution.Record.Status=="failed";
                hallScroll.RefreshMembers();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            VisibleChanged += delegate { if (Visible) { RefreshRuns(); refresh.Start(); } else refresh.Stop(); };
            foreach (CompanionSect s in data.Sects) sects.Items.Add(s); if (sects.Items.Count > 0) sects.SelectedIndex = 0;
        }
        private static List<T> Clone<T>(List<T> list) { var j = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 8388608 }; return j.Deserialize<List<T>>(j.Serialize(list)); }
        private static DataGridView Grid() { return new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, AllowUserToAddRows = true }; }
        private static void ConfigureEditorGrid(DataGridView grid, int[] columnWeights, params string[] longFields)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(220, 229, 225);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 241, 238);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(32, 59, 49);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.TopLeft;
            grid.DefaultCellStyle.Padding = new Padding(4, 3, 4, 3);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(215, 237, 226);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(23, 53, 41);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 247);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            for (int column = 0; column < columnWeights.Length; column++) grid.Columns[column].FillWeight = columnWeights[column];
            grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs args)
            {
                if (args.ColumnIndex < 0) return;
                string field = grid.Columns[args.ColumnIndex].DataPropertyName;
                if (field == "CharacterId") args.Value = PetCharacter.Find(Convert.ToString(args.Value)).Name;
                else if (Array.IndexOf(longFields, field) >= 0) args.Value = RowSummary(Convert.ToString(args.Value), field == "Directory" ? 38 : (field == "Role" ? 68 : 100));
                else return;
                args.FormattingApplied = true;
            };
        }

        private static string RowSummary(string text, int limit)
        {
            string summary = (text ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (summary.Length <= limit) return summary;
            if (Char.IsHighSurrogate(summary[limit - 1])) limit--;
            return summary.Substring(0, limit).TrimEnd() + "…";
        }

        private void RequireTeam()
        {
            if (sects.SelectedItem == null) throw new ArgumentException(UiLanguage.Text("请先新建 Agent 团队。"));
        }

        private static T SelectedItem<T>(DataGridView grid) where T : class
        {
            return grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as T;
        }

        private static string NextRowId(string prefix, IEnumerable<string> ids)
        {
            var occupied = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            int number = 1;
            while (occupied.Contains(prefix + number)) number++;
            return prefix + number;
        }

        private void AddMember()
        {
            RequireTeam();
            var list = (BindingList<SectMember>)members.DataSource;
            if (list.Count >= CompanionSect.MaximumMembers) throw new ArgumentException(UiLanguage.Format("最多允许 {0} 位成员。", CompanionSect.MaximumMembers));
            var selected = SelectedItem<SectMember>(members);
            var ids = new List<string>();
            foreach (SectMember item in list) ids.Add(item.Id);
            PetCharacter[] profiles=PetCharacter.Available;
            var member = new SectMember { Id = NextRowId("member-", ids), CharacterId = profiles[list.Count % profiles.Length].Id, Directory = selected == null ? "" : selected.Directory };
            if (!ShowMemberEditor(member, true)) return;
            list.Add(member);
            SelectRow(members, list.Count - 1);
        }

        private void RemoveMember()
        {
            RequireTeam();
            var selected = SelectedItem<SectMember>(members);
            if (selected == null) throw new ArgumentException(UiLanguage.Text("请先选中成员行。"));
            foreach (SectStep step in (BindingList<SectStep>)steps.DataSource)
                if (!String.IsNullOrEmpty(selected.Id) && String.Equals(step.Member, selected.Id, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(UiLanguage.Format("任务 {0} 仍分配给此成员。请先重新分配或删除该任务。", step.Id));
            ((BindingList<SectMember>)members.DataSource).Remove(selected);
        }

        private void AddStep()
        {
            RequireTeam();
            var list = (BindingList<SectStep>)steps.DataSource;
            if (list.Count >= CompanionSect.MaximumSteps) throw new ArgumentException(UiLanguage.Format("最多允许 {0} 项任务。", CompanionSect.MaximumSteps));
            var selected = SelectedItem<SectMember>(members);
            if (selected == null) throw new ArgumentException(UiLanguage.Text("请先新增成员。"));
            var ids = new List<string>();
            foreach (SectStep item in list) ids.Add(item.Id);
            var step = new SectStep { Id = NextRowId("task-", ids), Member = selected.Id, DependsOn = "" };
            if (!ShowStepEditor(step, true)) return;
            list.Add(step);
            SelectRow(steps, list.Count - 1);
        }

        private void RemoveStep()
        {
            RequireTeam();
            var selected = SelectedItem<SectStep>(steps);
            if (selected == null) throw new ArgumentException(UiLanguage.Text("请先选中任务行。"));
            var list = (BindingList<SectStep>)steps.DataSource;
            foreach (SectStep step in list)
                if (step != selected && Array.Exists(SectExecution.Dependencies(step.DependsOn), delegate(string dependency) { return String.Equals(dependency, selected.Id, StringComparison.OrdinalIgnoreCase); }))
                    throw new ArgumentException(UiLanguage.Format("任务 {0} 仍依赖此任务。请先移除该依赖。", step.Id));
            list.Remove(selected);
        }

        private static void SelectRow(DataGridView grid, int index)
        {
            grid.CurrentCell = grid.Rows[index].Cells[0];
            grid.Focus();
        }
        private static void Columns(DataGridView g, string[] fields, string[] titles) { for (int i = 0; i < fields.Length; i++) g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = fields[i], HeaderText = titles[i], SortMode = DataGridViewColumnSortMode.NotSortable }); }
        private static Label Label(string text) { return new Label { Text = text, Dock = DockStyle.Fill, AutoSize = false }; }
        private static Label LimitLabel(string text) { return new Label { Text = UiLanguage.Text(text), AutoSize = true, Padding = new Padding(5, 5, 0, 0) }; }
        private static string LimitSummary(int total, int step)
        {
            string round = step == 0 ? UiLanguage.Text("单轮不限时") : UiLanguage.Format("单轮最多 {0} 分钟", step);
            return (total==0?UiLanguage.Text("整次不限时"):UiLanguage.Format("整次最多 {0} 分钟", total)) + " · " + round;
        }
        private Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true }; b.Click += delegate { Guard(action); }; return b; }
        private void Guard(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "请检查设置", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
        private void NewSect(bool reviewOnly)
        {
            using(var picker=new FolderBrowserDialog { Description="为四位 Agent选择项目目录；不会立即执行任务。" })
            {
                if(picker.ShowDialog(this)!=DialogResult.OK)return;
                string title="Agent 团队"+(reviewOnly?"会诊":"研发")+(data.Sects.Count+1);
                while(data.Sects.Exists(delegate(CompanionSect item){return item.Name==title;}))title+="新";
                var s=CompanionRolePresets.Create(title,picker.SelectedPath,reviewOnly);
                data.Sects.Add(s); sects.Items.Add(s); sects.SelectedItem=s;goal.Focus();
            }
        }
        private void LoadSect()
        {
            var s = sects.SelectedItem as CompanionSect; if (s == null) return;
            name.Text = s.Name;
            goal.Text=s.Goal??"";
            runLimit.Value = Math.Max(runLimit.Minimum, Math.Min(runLimit.Maximum, s.RunLimitMinutes));
            stepLimit.Value = Math.Max(stepLimit.Minimum, Math.Min(stepLimit.Maximum, s.StepLimitMinutes));
            parallelLimit.Value=Math.Max(1,Math.Min(8,s.MaxParallel));
            autoHandoff.Checked=s.AutoHandoff;
            members.DataSource = new System.ComponentModel.BindingList<SectMember>(Clone(s.Members));
            steps.DataSource = new System.ComponentModel.BindingList<SectStep>(Clone(s.Steps));
        }
        private void SaveSect()
        {
            RequireTeam();
            var old = (CompanionSect)sects.SelectedItem;
            var draft = new CompanionSect { Id=QuickTeamTasks.TeamKey(old),Name = name.Text.Trim(), Goal=goal.Text.Trim(),TemplateId=old.TemplateId,AutoHandoff=autoHandoff.Checked,MaxParallel=(int)parallelLimit.Value, RunLimitMinutes=(int)runLimit.Value, StepLimitMinutes=(int)stepLimit.Value, Members = new List<SectMember>((IList<SectMember>)members.DataSource), Steps = new List<SectStep>((IList<SectStep>)steps.DataSource) };
            SectExecution.Validate(draft);
            if (data.Sects.Exists(delegate(CompanionSect s) { return s != old && s.Name == draft.Name; })) throw new ArgumentException("Agent 团队名称已存在。");
            int index = data.Sects.IndexOf(old); data.Sects[index] = draft;
            foreach (PlannedEvent e in data.Events) if (e.Sect == old.Name) e.Sect = draft.Name;
            foreach (PlannedEvent e in (IList<PlannedEvent>)events.DataSource) if (e.Sect == old.Name) e.Sect = draft.Name;
            save(); sects.Items[index] = draft; sects.SelectedIndex = index;
        }
        private void SaveEvents()
        {
            events.EndEdit(); BindingContext[events.DataSource].EndCurrentEdit();
            var copy = new List<PlannedEvent>((IList<PlannedEvent>)events.DataSource);
            // A reminder may have fired while this editor was open. Preserve its acknowledged
            // schedule unless the user actually edited the time/repeat/completed fields.
            foreach (PlannedEvent e in copy)
            {
                PlannedEvent before=eventBaseline.Find(delegate(PlannedEvent item){return item.Id==e.Id;});
                PlannedEvent current=data.Events.Find(delegate(PlannedEvent item){return item.Id==e.Id;});
                if(before!=null&&current!=null&&e.When==before.When&&e.Repeat==before.Repeat&&e.Done==before.Done){e.When=current.When;e.Done=current.Done;}
            }
            if (copy.Count > 100) throw new ArgumentException("最多保存100项事件。");
            foreach (PlannedEvent e in copy)
            {
                if (String.IsNullOrWhiteSpace(e.Title) || e.Title.Length > 200 || e.When.Year < 2000 || e.When.Year > 2090 || (e.Repeat != "once" && e.Repeat != "daily" && e.Repeat != "weekly")) throw new ArgumentException("请填写事件、有效时间和重复类型（once/daily/weekly）。");
                if (!String.IsNullOrWhiteSpace(e.Sect) && !data.Sects.Exists(delegate(CompanionSect s) { return s.Name == e.Sect; })) throw new ArgumentException("关联 Agent 团队不存在：" + e.Sect);
            }
            data.Events = copy; save(); ReloadEvents();
        }
        private void ReloadEvents() { eventBaseline=Clone(data.Events); events.DataSource=new System.ComponentModel.BindingList<PlannedEvent>(Clone(data.Events)); }
        private void RefreshRuns()
        {
            bool changed = listedRuns.Count != data.History.Count;
            for (int i = 0; !changed && i < listedRuns.Count; i++)
                changed = !Object.ReferenceEquals(listedRuns[i], data.History[i]);
            if (changed)
            {
                int selected = history.SelectedIndex;
                SectRun previous = selected >= 0 && selected < listedRuns.Count ? listedRuns[selected] : null;
                history.BeginUpdate();
                updatingBatches=true;batches.BeginUpdate();
                try
                {
                    history.Items.Clear();
                    batches.Items.Clear();
                    listedRuns.Clear();
                    listedRuns.AddRange(data.History);
                    foreach (SectRun entry in listedRuns){string label=HistoryLabel(entry);history.Items.Add(label);batches.Items.Add(label);}
                    if (listedRuns.Count > 0) history.SelectedIndex = Math.Max(0, listedRuns.IndexOf(previous));
                    batches.SelectedIndex=history.SelectedIndex;
                }
                finally {updatingBatches=false;batches.EndUpdate();history.EndUpdate();}
            }
            else
            {
                for (int i = 0; i < listedRuns.Count; i++)
                {
                    string label = HistoryLabel(listedRuns[i]);
                    if (!String.Equals((string)history.Items[i], label, StringComparison.Ordinal)) history.Items[i] = label;
                    if(!String.Equals((string)batches.Items[i],label,StringComparison.Ordinal))batches.Items[i]=label;
                }
            }
            RefreshOutput();
        }
        private static string HistoryLabel(SectRun run)
        {
            string id=run.Id??"";
            return SectRunConversation.DisplayTime(run.Started) + "  " + run.Sect + " · " + id.Substring(0,Math.Min(6,id.Length)) + " · " + SectRunConversation.Status(run.Status);
        }
        private void RefreshOutput() { RefreshOutput(false); }
        private void RefreshOutput(bool followLatest)
        {
            if (!runConversation.Visible) return;
            int index = history.SelectedIndex;
            SectRun selected = index >= 0 && index < listedRuns.Count ? listedRuns[index] : null;
            string summary = selected == null ? UiLanguage.Text("暂无运行记录") :
                selected.Sect + " · " + SectRunConversation.Status(selected.Status) + "    " +
                LimitSummary(selected.RunLimitMinutes, selected.StepLimitMinutes) + "\n" +
                SectRunConversation.DisplayTime(selected.Started) +
                (String.IsNullOrEmpty(selected.Finished) ? "" : " → " + SectRunConversation.DisplayTime(selected.Finished)) +
                (String.IsNullOrEmpty(selected.Goal)?"":"\n"+selected.Goal.Replace("\r"," ").Replace("\n"," "));
            if (runSummary.Text != summary) runSummary.Text = summary;
            var execution=active();runMemberStates.Present(SectMemberStatus.Build(execution==null?sects.SelectedItem as CompanionSect:execution.Plan, selected));
            bool changed=runMessages.Update(selected);
            if(changed||followLatest)runConversation.Present(selected == null ? null : runMessages.Conversation);
            if(changed||followLatest)
            {
                ChatMessage latest=String.IsNullOrEmpty(inspectedRunMember)?runMessages.LatestReply:runMessages.Conversation==null?null:runMessages.Conversation.Messages.FindLast(delegate(ChatMessage message){return message.Role=="assistant"&&message.Speaker==UiLanguage.Text(inspectedRunMember);});
                if(latest!=null)runConversation.FollowMessage(latest);
            }
        }
        internal void SelectSect(string sect) { foreach (CompanionSect s in sects.Items) if (s.Name == sect) { sects.SelectedItem = s; break; } }
        internal SectRun SelectedRun {get{return history.SelectedIndex>=0&&history.SelectedIndex<listedRuns.Count?listedRuns[history.SelectedIndex]:null;}}
        internal void ShowExecution(SectRun record)
        {SelectSect(record.Sect);RefreshRuns();history.SelectedIndex=listedRuns.IndexOf(record);tabs.SelectedTab=hallPage;}
        protected override void Dispose(bool disposing) { if (disposing) refresh.Dispose(); base.Dispose(disposing); }
    }
}
