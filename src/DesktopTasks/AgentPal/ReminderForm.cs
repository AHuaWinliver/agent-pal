using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class ReminderForm : AppWindow
    {
        private readonly ReminderService service;
        private readonly ComboBox recurrence = new ComboBox();
        private readonly ComboBox weekday = new ComboBox();
        private readonly WeekdayPicker weekdayPicker = new WeekdayPicker();
        private readonly DateTimePicker time = new DateTimePicker();
        private readonly TextBox task = new TextBox();
        private readonly ComboBox effectMode = new ComboBox();
        private readonly Label placeholder = new Label();
        private readonly ListBox list = new ListBox();
        private readonly Button toggle = new Button();
        private readonly Button delete = new Button();
        private readonly Button edit = new Button(),cancelEdit=new Button();
        private readonly RoundIconButton add=new RoundIconButton();
        private DailyReminder editingReminder;
        private Label reminderTitle;
        private readonly ReminderActionButton preview = new ReminderActionButton();
        private readonly NumericUpDown customMinutes = new NumericUpDown();
        private readonly Label countdownStatus = new Label();
        private readonly Button cancelCountdown = new Button();
        private readonly Timer countdownClock = new Timer();

        internal ReminderForm(ReminderService reminderService)
        {
            service = reminderService;
            Text = "提醒与倒计时";
            Icon = AppIcon.WindowIcon;
            ClientSize = new Size(520, 588);
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(253, 253, 252);
            DoubleBuffered = true;
            KeyPreview=true;
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.S&&editingReminder!=null){e.SuppressKeyPress=true;AddReminder();}};
            Font = new Font("Microsoft YaHei UI", 9f);

            Label title = new Label { Text = "提醒与倒计时", Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold), ForeColor = Color.FromArgb(34, 34, 32), AutoSize = true, Location = new Point(18, 16) };
            Label subtitle = new Label { Text = "到点后，当前 AgentPal会来到屏幕中间", Font = new Font("Microsoft YaHei UI", 8f), ForeColor = Color.FromArgb(112, 112, 106), AutoSize = true, Location = new Point(18, 34) };
            Button close = TextButton("×", new Point(476, 8), new Size(30, 30));
            close.Font = new Font("Segoe UI", 13f);
            close.Click += delegate { Hide(); };

            RoundedPanel countdownCard = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 52), Size = new Size(492, 122) };
            int presetX = 12;
            foreach (KeyValuePair<string, int> preset in new[]
            {
                new KeyValuePair<string, int>("5 分钟", 5), new KeyValuePair<string, int>("10 分钟", 10),
                new KeyValuePair<string, int>("15 分钟", 15), new KeyValuePair<string, int>("30 分钟", 30),
                new KeyValuePair<string, int>("1 小时", 60), new KeyValuePair<string, int>("2 小时", 120)
            })
            {
                int selectedMinutes = preset.Value;
                Button quick = new Button { Text = UiLanguage.Text(preset.Key), Location = new Point(presetX, 12), Size = new Size(70, 31) };
                StyleButton(quick, false);
                quick.Click += delegate { StartCountdown(selectedMinutes); };
                countdownCard.Controls.Add(quick);
                presetX += 78;
            }
            Label customLabel = new Label { Text = "自定义", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 76), Location = new Point(14, 78), BackColor = countdownCard.BackColor };
            customMinutes.Minimum = 1;
            customMinutes.Maximum = 10080;
            customMinutes.Value = 25;
            customMinutes.Location = new Point(70, 72);
            customMinutes.Size = new Size(72, 28);
            Label minuteUnit = new Label { Text = "分钟", AutoSize = true, ForeColor = Color.FromArgb(80, 80, 76), Location = new Point(146, 78), BackColor = countdownCard.BackColor };
            Button startCustom = new Button { Text = "开始", Location = new Point(208, 68), Size = new Size(64, 33) };
            StyleButton(startCustom, true);
            startCustom.Click += delegate { StartCountdown((int)customMinutes.Value); };
            cancelCountdown.Text = "取消";
            cancelCountdown.Location = new Point(278, 68);
            cancelCountdown.Size = new Size(58, 33);
            StyleButton(cancelCountdown, false);
            cancelCountdown.Click += delegate { service.CancelCountdown(); UpdateCountdownStatus(); };
            countdownStatus.Location = new Point(345, 61);
            countdownStatus.Size = new Size(132, 52);
            countdownStatus.TextAlign = ContentAlignment.MiddleLeft;
            countdownStatus.ForeColor = Color.FromArgb(40, 132, 84);
            countdownStatus.BackColor = countdownCard.BackColor;
            countdownCard.Controls.Add(customLabel);
            countdownCard.Controls.Add(customMinutes);
            countdownCard.Controls.Add(minuteUnit);
            countdownCard.Controls.Add(startCustom);
            countdownCard.Controls.Add(cancelCountdown);
            countdownCard.Controls.Add(countdownStatus);

            reminderTitle = new Label { Text = "定时提醒", Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(52, 52, 49), AutoSize = true, Location = new Point(18, 184) };
            RoundedPanel editor = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 210), Size = new Size(492, 112) };
            Label repeat = new Label { Text = "重复", AutoSize = true, Location = new Point(14, 77), ForeColor = Color.FromArgb(80, 80, 76), BackColor = editor.BackColor };
            recurrence.DropDownStyle = ComboBoxStyle.DropDownList;
            recurrence.Location = new Point(76, 72);
            recurrence.Size = new Size(154, 28);
            recurrence.Items.AddRange(new object[] { "每天", "每周", "周一至周五", "周六至周日", "自选星期" });
            recurrence.SelectedIndex = 0;
            weekday.DropDownStyle = ComboBoxStyle.DropDownList;
            weekday.Location = new Point(240, 72);
            weekday.Size = new Size(118, 28);
            for (int day = 0; day < 7; day++) weekday.Items.Add(ScheduledClaudeTask.WeekdayText(day));
            weekday.SelectedIndex = (int)DateTime.Now.DayOfWeek;
            weekday.Visible = false;
            weekdayPicker.Bounds = new Rectangle(240, 72, 160, 28);
            weekdayPicker.Visible = false;
            recurrence.SelectedIndexChanged += delegate
            {
                weekday.Visible = recurrence.SelectedIndex == 1;
                weekdayPicker.Visible = recurrence.SelectedIndex == 4;
                weekdayPicker.RefreshCaption();
            };
            time.Format = DateTimePickerFormat.Custom;
            time.CustomFormat = "HH:mm";
            time.ShowUpDown = true;
            time.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            time.Location = new Point(14, 22);
            time.Size = new Size(78, 29);

            var taskField=new RoundedPanel {Name="ReminderTextField",Radius=8,BorderColor=Color.FromArgb(139,153,144),BackColor=Color.White,Location=new Point(102,19),Size=new Size(210,36)};
            task.Name="ReminderText";
            task.AccessibleName=UiLanguage.Text("提醒内容");
            task.BorderStyle = BorderStyle.None;
            task.BackColor = Color.White;
            task.Font = new Font("Microsoft YaHei UI", 9.5f);
            task.Location = new Point(9, 8);
            task.Size = new Size(192, 24);
            task.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddReminder(); } };
            task.TextChanged += delegate { UpdatePlaceholder(); };
            task.GotFocus += delegate { UpdatePlaceholder(); };
            task.LostFocus += delegate { UpdatePlaceholder(); };
            Action updateField=delegate{taskField.BorderColor=!task.Enabled?Color.FromArgb(166,175,168):task.Focused?Color.FromArgb(47,109,88):Color.FromArgb(139,153,144);taskField.BackColor=task.Enabled?Color.White:Color.FromArgb(237,240,235);task.BackColor=placeholder.BackColor=taskField.BackColor;taskField.Invalidate();};
            task.GotFocus+=delegate{updateField();};task.LostFocus+=delegate{updateField();};task.EnabledChanged+=delegate{updateField();};
            taskField.Click+=delegate{task.Focus();};

            placeholder.Text = "提醒我做什么…";
            placeholder.ForeColor = Color.FromArgb(145, 145, 140);
            placeholder.BackColor = Color.White;
            placeholder.Font = task.Font;
            placeholder.AutoSize = true;
            placeholder.Location = new Point(9, 8);
            placeholder.Click += delegate { task.Focus(); };

            effectMode.DropDownStyle = ComboBoxStyle.DropDownList;
            effectMode.FlatStyle = FlatStyle.Flat;
            effectMode.Font = new Font("Microsoft YaHei UI", 8.5f);
            effectMode.BackColor = Color.White;
            effectMode.Location = new Point(318, 21);
            effectMode.Size = new Size(108, 28);
            effectMode.Items.AddRange(new object[] { "角色全屏演出", "仅提示框" });
            effectMode.SelectedIndex = 0;

            add.Name="SaveReminder";add.Text="+";add.AccessibleName=UiLanguage.Text("添加提醒");add.Font=new Font("Segoe UI",18f);add.ForeColor=Color.White;add.BackColor=Color.FromArgb(32,33,31);add.FlatStyle=FlatStyle.Flat;add.Location=new Point(434,17);add.Size=new Size(42,42);add.Cursor=Cursors.Hand;
            add.FlatAppearance.BorderSize = 0;
            add.Click += delegate { AddReminder(); };
            cancelEdit.Name="CancelReminderEdit";cancelEdit.Text=UiLanguage.Text("取消");cancelEdit.Bounds=new Rectangle(408,72,68,28);cancelEdit.Visible=false;StyleButton(cancelEdit,false);cancelEdit.Click+=delegate{CancelEditing();};
            editor.Controls.Add(time);
            taskField.Controls.Add(task);
            taskField.Controls.Add(placeholder);
            editor.Controls.Add(taskField);
            editor.Controls.Add(effectMode);
            editor.Controls.Add(repeat);
            editor.Controls.Add(recurrence);
            editor.Controls.Add(weekday);
            editor.Controls.Add(weekdayPicker);
            editor.Controls.Add(add);
            editor.Controls.Add(cancelEdit);

            RoundedPanel listCard = new RoundedPanel { Radius = 16, BorderColor = Color.FromArgb(226, 226, 223), BackColor = Color.FromArgb(247, 247, 246), Location = new Point(14, 334), Size = new Size(492, 205) };
            list.BorderStyle = BorderStyle.None;
            list.BackColor = listCard.BackColor;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 48;
            list.IntegralHeight = false;
            list.Location = new Point(8, 8);
            list.Size = new Size(476, 189);
            list.DrawItem += DrawReminder;
            list.SelectedIndexChanged += delegate { UpdateButtons(); };
            list.Name="ReminderList";list.DoubleClick+=delegate{EditSelectedReminder();};
            list.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.F2){e.SuppressKeyPress=true;EditSelectedReminder();}};
            listCard.Controls.Add(list);

            toggle.Text = "停用";
            toggle.Location = new Point(14, 548);
            toggle.Size = new Size(74, 31);
            StyleButton(toggle, false);
            toggle.Click += delegate { DailyReminder item = list.SelectedItem as DailyReminder; service.Toggle(item); RefreshList(item); };

            delete.Text = "删除";
            delete.Location = new Point(94, 548);
            delete.Size = new Size(70, 31);
            StyleButton(delete, false);
            delete.ForeColor = Color.FromArgb(174, 65, 58);
            delete.Click += delegate { DailyReminder item = list.SelectedItem as DailyReminder;if(Object.ReferenceEquals(item,editingReminder))CancelEditing(); service.Delete(item); RefreshList(null); };
            edit.Name="EditReminder";edit.Text="修改提醒";edit.Bounds=new Rectangle(174,548,126,31);StyleButton(edit,false);edit.Click+=delegate{EditSelectedReminder();};

            preview.Text = "立即测试效果";
            preview.Name = "reminderPreviewButton";
            preview.AccessibleName = "立即测试提醒效果";
            preview.Location = new Point(370, 548);
            preview.Size = new Size(136, 31);
            preview.Click += delegate { PreviewCurrentReminder(); };

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(close);
            Controls.Add(countdownCard);
            Controls.Add(reminderTitle);
            Controls.Add(editor);
            Controls.Add(listCard);
            Controls.Add(toggle);
            Controls.Add(delete);
            Controls.Add(edit);
            Controls.Add(preview);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Resize += delegate { ApplyRoundedRegion(); };
            service.Changed += delegate { if (!IsDisposed) { RefreshList(list.SelectedItem as DailyReminder); UpdateCountdownStatus(); } };
            countdownClock.Interval = 1000;
            countdownClock.Tick += delegate { if (Visible) UpdateCountdownStatus(); };
            countdownClock.Start();
            ApplyRoundedRegion();
            UpdatePlaceholder();
            RefreshList(null);
            UpdateCountdownStatus();
        }

        internal void ShowNear(Rectangle petBounds, bool activate = true)
        {
            Rectangle area = Screen.FromRectangle(petBounds).WorkingArea;
            int x = petBounds.Left + ((petBounds.Width - Width) / 2);
            x = Math.Max(area.Left + 8, Math.Min(x, area.Right - Width - 8));
            int y = petBounds.Bottom + 8;
            if (y + Height > area.Bottom - 8) y = petBounds.Top - Height - 8;
            Location = new Point(x, Math.Max(area.Top + 8, y));
            if (!activate) return;
            RefreshList(list.SelectedItem as DailyReminder);
            UpdateCountdownStatus();
            if (!Visible) Show();
            Activate();
            task.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 18))
            using (Pen pen = new Pen(Color.FromArgb(221, 221, 217))) e.Graphics.DrawPath(pen, path);
        }

        private void AddReminder()
        {
            string text = task.Text.Trim();
            if (text.Length == 0) { task.Focus(); return; }
            string effect = effectMode.SelectedIndex == 1 ? "popup" : "impact";
            DayOfWeek? day = recurrence.SelectedIndex == 1 ? (DayOfWeek?)weekday.SelectedIndex : null;
            int mask = recurrence.SelectedIndex == 2 ? WeekdaySchedule.Workdays :
                recurrence.SelectedIndex == 3 ? WeekdaySchedule.Weekend : recurrence.SelectedIndex == 4 ? weekdayPicker.Selection : 0;
            if (recurrence.SelectedIndex == 4 && !WeekdaySchedule.ValidSelection(mask))
            {
                MessageBox.Show(this, UiLanguage.Text("请选择至少一个星期。"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                DailyReminder added=editingReminder;
                if(added==null)added=service.Add(time.Value.TimeOfDay,text,effect,day,mask);
                else service.Update(added,time.Value.TimeOfDay,text,effect,day,mask);
                CancelEditing();RefreshList(added);task.Focus();
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }

        private void EditSelectedReminder()
        {
            var reminder=list.SelectedItem as DailyReminder;if(reminder==null)return;editingReminder=reminder;
            task.Text=reminder.Text;time.Value=DateTime.Today.AddMinutes(Math.Max(0,Math.Min(1439,reminder.MinutesAfterMidnight)));
            int mask=reminder.WeekdayMask;
            recurrence.SelectedIndex=mask==WeekdaySchedule.Workdays?2:mask==WeekdaySchedule.Weekend?3:WeekdaySchedule.ValidSelection(mask)?4:reminder.Weekday>=0?1:0;
            weekday.SelectedIndex=Math.Max(0,Math.Min(6,reminder.Weekday));weekdayPicker.Selection=WeekdaySchedule.ValidSelection(mask)?mask:WeekdaySchedule.Day((int)DateTime.Today.DayOfWeek);
            effectMode.SelectedIndex=ReminderService.NormalizeEffect(reminder.Effect)=="popup"?1:0;
            reminderTitle.Text=UiLanguage.Text("修改提醒");add.Text="✓";add.AccessibleName=UiLanguage.Text("保存修改");cancelEdit.Visible=true;task.Focus();task.SelectAll();
        }
        private void CancelEditing(){editingReminder=null;task.Clear();reminderTitle.Text=UiLanguage.Text("定时提醒");add.Text="+";add.AccessibleName=UiLanguage.Text("添加提醒");cancelEdit.Visible=false;}

        private void StartCountdown(int minutes)
        {
            DailyReminder countdown = service.StartCountdown(minutes);
            UpdateCountdownStatus();
            countdownStatus.Text = UiLanguage.Format("{0} 分钟倒计时已开始\n{1} 到点",countdown.CountdownMinutes,countdown.TimeText);
        }

        private void UpdateCountdownStatus()
        {
            DailyReminder countdown = service.Countdown;
            cancelCountdown.Enabled = countdown != null;
            if (countdown == null)
            {
                countdownStatus.Text = UiLanguage.Text("当前没有倒计时");
                countdownStatus.ForeColor = Color.FromArgb(120, 120, 115);
                return;
            }
            DateTime due;
            if (!DateTime.TryParse(countdown.DueAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out due))
            {
                countdownStatus.Text = UiLanguage.Text("倒计时状态异常");
                return;
            }
            TimeSpan remaining = due.ToLocalTime() - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            countdownStatus.ForeColor = Color.FromArgb(40, 132, 84);
            countdownStatus.Text = FormatRemaining(remaining) + "\n" + UiLanguage.Format("{0} 到点",due.ToLocalTime().ToString("HH:mm"));
        }

        internal static string FormatRemaining(TimeSpan remaining)
        {
            int totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            return hours > 0
                ? UiLanguage.Format("剩余 {0}:{1:00}:{2:00}", hours, minutes, seconds)
                : UiLanguage.Format("剩余 {0:00}:{1:00}", minutes, seconds);
        }

        private void RefreshList(DailyReminder selected)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (DailyReminder reminder in service.Reminders) list.Items.Add(reminder);
            list.EndUpdate();
            if (selected != null && list.Items.Contains(selected)) list.SelectedItem = selected;
            else if (list.Items.Count > 0) list.SelectedIndex = 0;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            DailyReminder item = list.SelectedItem as DailyReminder;
            toggle.Enabled = delete.Enabled = edit.Enabled = item != null;
            preview.Enabled = true;
            toggle.Text = item != null && item.Enabled ? "停用" : "启用";
        }

        private void PreviewCurrentReminder()
        {
            DailyReminder item = list.SelectedItem as DailyReminder;
            if (item == null || editingReminder!=null)
            {
                string previewText = task.Text.Trim();
                item = new DailyReminder
                {
                    MinutesAfterMidnight = Math.Max(0, Math.Min(1439, (int)time.Value.TimeOfDay.TotalMinutes)),
                    Text = previewText.Length == 0 ? "这是提醒效果测试" : previewText,
                    Enabled = true,
                    Effect = effectMode.SelectedIndex == 1 ? "popup" : "impact"
                };
            }
            service.TriggerForPreview(item);
        }

        internal bool PreviewButtonAvailable
        {
            get { return preview.Enabled; }
        }

        internal void TriggerPreviewForTest()
        {
            PreviewCurrentReminder();
        }

        private void DrawReminder(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            DailyReminder item = list.Items[e.Index] as DailyReminder;
            if (item == null) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Color.FromArgb(231, 241, 234) : list.BackColor;
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            using (SolidBrush dot = new SolidBrush(item.Enabled ? Color.FromArgb(40, 132, 84) : Color.FromArgb(170, 170, 165))) e.Graphics.FillEllipse(dot, e.Bounds.Left + 11, e.Bounds.Top + 20, 8, 8);
            Rectangle timeBounds = new Rectangle(e.Bounds.Left + 30, e.Bounds.Top + 3, 58, 22);
            Rectangle textBounds = new Rectangle(e.Bounds.Left + 92, e.Bounds.Top + 3, e.Bounds.Width - 104, 22);
            Rectangle scheduleBounds = new Rectangle(e.Bounds.Left + 30, e.Bounds.Top + 24, e.Bounds.Width - 146, 21);
            Rectangle stateBounds = new Rectangle(e.Bounds.Right - 114, e.Bounds.Top + 24, 102, 21);
            using (Font bold = new Font(list.Font, FontStyle.Bold)) TextRenderer.DrawText(e.Graphics, item.TimeText, bold, timeBounds, Color.FromArgb(38, 38, 36), TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, item.Text, list.Font, textBounds, item.Enabled ? Color.FromArgb(38, 38, 36) : Color.FromArgb(135, 135, 130), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            string schedule = WeekdaySchedule.Describe(WeekdaySchedule.ForReminder(item.Weekday, item.WeekdayMask), UiLanguage.IsEnglish);
            TextRenderer.DrawText(e.Graphics, schedule, list.Font, scheduleBounds, Color.FromArgb(105, 105, 100), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            string stateText = UiLanguage.Text(item.Enabled ? (ReminderService.NormalizeEffect(item.Effect) == "popup" ? "提示框" : "演出") : "已停用");
            TextRenderer.DrawText(e.Graphics, stateText, list.Font, stateBounds, item.Enabled ? Color.FromArgb(40, 132, 84) : Color.FromArgb(145, 145, 140), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private void UpdatePlaceholder()
        {
            placeholder.Visible = task.TextLength == 0 && !task.Focused;
        }

        private static Button TextButton(string text, Point location, Size size)
        {
            Button button = new Button { Text = text, Location = location, Size = size, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 237);
            return button;
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.BackColor = primary ? Color.FromArgb(32, 33, 31) : Color.FromArgb(247, 247, 245);
            button.ForeColor = primary ? Color.White : Color.FromArgb(60, 60, 57);
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(32, 33, 31) : Color.FromArgb(220, 220, 216);
        }

        private void ApplyRoundedRegion()
        {
            Region old = Region;
            Region = new Region(PromptForm.RoundedRectangle(ClientRectangle, 18));
            if (old != null) old.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) countdownClock.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ReminderActionButton : Button
    {
        private bool hovering;

        internal ReminderActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = !Enabled ? Color.FromArgb(166, 178, 169) : (hovering ? Color.FromArgb(31, 116, 73) : Color.FromArgb(40, 132, 84));
            e.Graphics.Clear(Parent == null ? Color.FromArgb(253, 253, 252) : Parent.BackColor);
            using (GraphicsPath path = PromptForm.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (SolidBrush brush = new SolidBrush(background))
            using (Pen border = new Pen(Color.FromArgb(27, 99, 62)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -4), Color.White, background);
        }
    }
}
