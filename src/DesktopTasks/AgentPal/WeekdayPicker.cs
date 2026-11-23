// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class WeekdayPicker : Button
    {
        private int selection;

        internal int Selection
        {
            get { return selection; }
            set
            {
                selection = WeekdaySchedule.ValidSelection(value) ? value : 0;
                RefreshCaption();
            }
        }

        internal WeekdayPicker()
        {
            AutoEllipsis = true;
            UiLanguageBindings.PreserveContent(this);
            RefreshCaption();
        }

        internal void RefreshCaption()
        {
            AccessibleName = UiLanguage.Text("选择星期…");
            Text = selection == 0 ? AccessibleName : WeekdaySchedule.Describe(selection, UiLanguage.IsEnglish);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            using (var dialog = new AppWindow())
            using (var days = new CheckedListBox())
            {
                dialog.Text = UiLanguage.Text("选择星期…");
                dialog.Font = SystemFonts.MessageBoxFont;
                dialog.AutoScaleMode = AutoScaleMode.Font;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ShowInTaskbar = dialog.MaximizeBox = dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(254, 242);
                days.SetBounds(12, 12, 230, 178);
                days.CheckOnClick = true;
                for (int index = 1; index <= 7; index++)
                {
                    int day = index % 7;
                    days.Items.Add(UiLanguage.Text(ScheduledClaudeTask.WeekdayText(day)), (selection & WeekdaySchedule.Day(day)) != 0);
                }
                var accept = new Button { Text = UiLanguage.Text("确定"), DialogResult = DialogResult.OK, Enabled = selection != 0 };
                accept.SetBounds(164, 200, 78, 30);
                days.ItemCheck += delegate(object sender, ItemCheckEventArgs change)
                {
                    int others = days.CheckedItems.Count - (change.CurrentValue == CheckState.Checked ? 1 : 0);
                    accept.Enabled = others > 0 || change.NewValue == CheckState.Checked;
                };
                var cancel = new Button { Text = UiLanguage.Text("取消"), DialogResult = DialogResult.Cancel };
                cancel.SetBounds(80, 200, 78, 30);
                dialog.Controls.AddRange(new Control[] { days, accept, cancel });
                dialog.AcceptButton = accept;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                int mask = 0;
                foreach (int index in days.CheckedIndices) mask |= WeekdaySchedule.Day((index + 1) % 7);
                Selection = mask;
            }
        }
    }
}
