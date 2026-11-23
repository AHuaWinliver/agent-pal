using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class WorkshopForm
    {
        private void EditMember()
        {
            RequireTeam();
            var member = SelectedItem<SectMember>(members);
            if (member == null) throw new ArgumentException(UiLanguage.Text("请先选中成员行。"));
            int index = members.CurrentRow.Index;
            string previousId = member.Id;
            if (!ShowMemberEditor(member, false)) return;
            ((BindingList<SectMember>)members.DataSource).ResetItem(index);
            if (previousId != member.Id)
            {
                var list = (BindingList<SectStep>)steps.DataSource;
                foreach (SectStep step in list)
                    if (String.Equals(step.Member, previousId, StringComparison.OrdinalIgnoreCase)) step.Member = member.Id;
                list.ResetBindings();
            }
            SelectRow(members, index);
        }

        private void EditStep()
        {
            RequireTeam();
            var step = SelectedItem<SectStep>(steps);
            if (step == null) throw new ArgumentException(UiLanguage.Text("请先选中任务行。"));
            int index = steps.CurrentRow.Index;
            string previousId = step.Id;
            if (!ShowStepEditor(step, false)) return;
            var list = (BindingList<SectStep>)steps.DataSource;
            if (previousId != step.Id)
                foreach (SectStep item in list)
                {
                    string[] dependencies = SectExecution.Dependencies(item.DependsOn);
                    bool changed = false;
                    for (int dependency = 0; dependency < dependencies.Length; dependency++)
                        if (String.Equals(dependencies[dependency], previousId, StringComparison.OrdinalIgnoreCase))
                        {
                            dependencies[dependency] = step.Id;
                            changed = true;
                        }
                    if (changed) item.DependsOn = String.Join(", ", dependencies);
                }
            list.ResetBindings();
            SelectRow(steps, index);
        }

        private bool ShowMemberEditor(SectMember member, bool isNew)
        {
            using (var editor = new RowEditor(isNew ? "新增成员" : "编辑成员", Font))
            {
                var id = RowText("memberId", member.Id, 32);
                var displayName = RowText("memberName", member.Name, 60);
                var folder = RowText("memberDirectory", member.Directory, 32767);
                var role = RowText("memberRole", member.Role, 4000, true);
                var profiles = new List<KeyValuePair<string, string>>();
                foreach (PetCharacter profile in PetCharacter.Available) profiles.Add(new KeyValuePair<string, string>(profile.Id, profile.Name));
                var character = RowChoices("memberCharacter", profiles, member.CharacterId);
                var presets = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                presets.Items.AddRange(CompanionRolePresets.Titles);
                presets.SelectedIndex = 0;
                editor.Field("成员编号", id);
                editor.Field("姓名", displayName);
                editor.Field("人物形象", character);
                editor.Field("工作目录", RowWithButton(folder, "浏览…", delegate
                {
                    using (var picker = new FolderBrowserDialog { SelectedPath = Directory.Exists(folder.Text) ? folder.Text : "" })
                        if (picker.ShowDialog(editor) == DialogResult.OK) folder.Text = picker.SelectedPath;
                }));
                editor.Field("Agent 预设", RowWithButton(presets, "套用预设", delegate
                {
                    PetCharacter preset = PetCharacter.All[presets.SelectedIndex];
                    displayName.Text = preset.Name;
                    character.SelectedValue = preset.Id;
                    role.Text = CompanionRolePresets.Role(presets.SelectedIndex);
                }));
                editor.Field("职责 / 系统提示", role, true);
                editor.ActiveControl = displayName;
                return editor.Edit(this, delegate
                {
                    string nextId = id.Text.Trim(), nextName = displayName.Text.Trim(), nextFolder = folder.Text.Trim();
                    var list = (BindingList<SectMember>)members.DataSource;
                    if (!SectExecution.ValidId(nextId) || AnyOther(list, member, delegate(SectMember item) { return SameKey(item.Id, nextId); }))
                        return "编号需唯一，由 1–32 位英文字母、数字或短横线组成。";
                    if (nextName.Length == 0 || AnyOther(list, member, delegate(SectMember item) { return SameKey(item.Name, nextName); })) return "请填写不重复的成员姓名。";
                    if (!Directory.Exists(nextFolder)) return "请选择已存在的工作目录。";
                    if (String.IsNullOrWhiteSpace(role.Text)) return "请填写成员职责。";
                    member.Id = nextId; member.Name = nextName; member.Directory = nextFolder;
                    member.CharacterId = Convert.ToString(character.SelectedValue); member.Role = role.Text;
                    return null;
                });
            }
        }

        private bool ShowStepEditor(SectStep step, bool isNew)
        {
            using (var editor = new RowEditor(isNew ? "新增任务" : "编辑任务", Font))
            {
                var id = RowText("taskId", step.Id, 32);
                var roster = new List<KeyValuePair<string, string>>();
                foreach (SectMember member in (BindingList<SectMember>)members.DataSource)
                    roster.Add(new KeyValuePair<string, string>(member.Id, member.Name + "  ·  " + member.Id));
                var owner = RowChoices("taskMember", roster, step.Member);
                var execution = RowChoices("taskExecutionMode", new List<KeyValuePair<string,string>> {
                    new KeyValuePair<string,string>("auto",UiLanguage.Text("自动（可修改工作区）")),
                    new KeyValuePair<string,string>("read-only",UiLanguage.Text("只读，可并行")),
                    new KeyValuePair<string,string>("isolated",UiLanguage.Text("仅写隔离输出，可并行")),
                    new KeyValuePair<string,string>("serial",UiLanguage.Text("独占串行"))
                },step.ExecutionMode??"auto");
                var dependencies = RowText("taskDependencies", step.DependsOn, 32767, true);
                var instructions = RowText("taskInstructions", step.Task, 8000, true);
                editor.Field("任务编号", id);
                editor.Field("执行成员", owner);
                editor.Field("执行方式", execution);
                editor.Field("依赖任务编号（逗号分隔）", dependencies, false, 78);
                editor.Field("任务内容", instructions, true);
                editor.ActiveControl = instructions;
                return editor.Edit(this, delegate
                {
                    string nextId = id.Text.Trim();
                    var list = (BindingList<SectStep>)steps.DataSource;
                    if (!SectExecution.ValidId(nextId) || AnyOther(list, step, delegate(SectStep item) { return SameKey(item.Id, nextId); }))
                        return "编号需唯一，由 1–32 位英文字母、数字或短横线组成。";
                    if (String.IsNullOrWhiteSpace(instructions.Text)) return "请填写任务内容。";
                    if (owner.SelectedIndex < 0) return "请选择执行成员。";
                    step.Id = nextId; step.Member = Convert.ToString(owner.SelectedValue);
                    step.DependsOn = dependencies.Text; step.Task = instructions.Text;
                    step.ExecutionMode=Convert.ToString(execution.SelectedValue);
                    return null;
                });
            }
        }

        private static bool SameKey(string first, string second) { return String.Equals(first, second, StringComparison.OrdinalIgnoreCase); }

        private static bool AnyOther<T>(IEnumerable<T> items, T current, Predicate<T> matches) where T : class
        {
            foreach (T item in items) if (item != current && matches(item)) return true;
            return false;
        }

        private static TextBox RowText(string controlName, string value, int maxLength, bool multiline = false)
        {
            return new TextBox { Name = controlName, Dock = DockStyle.Fill, Text = value ?? "", MaxLength = maxLength,
                Multiline = multiline, AcceptsReturn = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
        }

        private static ComboBox RowChoices(string controlName, List<KeyValuePair<string, string>> options, string selected)
        {
            var choices = new ComboBox { Name = controlName, Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                BindingContext = new BindingContext(), DisplayMember = "Value", ValueMember = "Key", DataSource = options };
            int index = options.FindIndex(delegate(KeyValuePair<string, string> option) { return SameKey(option.Key, selected); });
            choices.SelectedIndex = Math.Max(0, index);
            return choices;
        }

        private static Control RowWithButton(Control input, string caption, Action action)
        {
            var row = new Panel { Dock = DockStyle.Fill };
            var button = new Button { Dock = DockStyle.Right, AutoSize = true, Text = UiLanguage.Text(caption) };
            button.Click += delegate { action(); };
            row.Controls.Add(input);
            row.Controls.Add(button);
            return row;
        }

        private sealed class RowEditor : AppWindow
        {
            private readonly TableLayoutPanel fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
            private readonly Label validation = new Label { Dock = DockStyle.Bottom, Height = 33, Padding = new Padding(15, 4, 10, 0), ForeColor = Color.Firebrick };
            private readonly Button confirm = new Button { Name = "confirmRowEdit", AutoSize = true, Text = UiLanguage.Text("确定") };

            internal RowEditor(string title, Font font)
            {
                Text = UiLanguage.Text(title); Font = font; ClientSize = new Size(640, 465); MinimumSize = new Size(480, 380);
                StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 45, Padding = new Padding(12, 2, 12, 0) };
                var dismiss = new Button { AutoSize = true, Text = UiLanguage.Text("取消"), DialogResult = DialogResult.Cancel };
                actions.Controls.Add(confirm); actions.Controls.Add(dismiss);
                Controls.Add(fields); Controls.Add(validation); Controls.Add(actions);
                AcceptButton = confirm; CancelButton = dismiss;
            }

            internal void Field(string title, Control input, bool fill = false, int height = 33)
            {
                int row = fields.RowCount++;
                fields.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, height));
                fields.Controls.Add(new Label { Text = UiLanguage.Text(title), Dock = DockStyle.Fill, Padding = new Padding(0, 6, 4, 0) }, 0, row);
                fields.Controls.Add(input, 1, row);
            }

            internal bool Edit(Form owner, Func<string> apply)
            {
                confirm.Click += delegate
                {
                    string error = apply();
                    if (error == null) DialogResult = DialogResult.OK;
                    else validation.Text = UiLanguage.Text(error);
                };
                return ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
