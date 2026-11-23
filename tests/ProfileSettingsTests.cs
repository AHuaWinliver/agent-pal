// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AgentPal;

internal static class ProfileSettingsTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static object Field(object value, string name)
    { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }

    [STAThread] private static int Main(string[] args)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") != "1") throw new Exception("Isolated test state required.");
            Directory.CreateDirectory(AgentPal.Program.StateDirectory);
            Application.EnableVisualStyles();
            UiLanguageBindings.Initialize();
            TestLanguage();
            TestProfiles();
            TestTasks(args[0]);
            TestReminderWeekday();
            TestRepeatingDays();
            TestWeeklyIntervals();
            TestEditingAndConfigurationTips();
            TestConversationPopup(args[0]);
            TestEditingPause(args[0]);
            Console.WriteLine("PASS product settings, locale persistence and companion task ownership; no model calls.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void TestRepeatingDays()
    {
        string previous = Program.StateDirectory;
        string root = Path.Combine(previous, "selected-days");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", root);
        try
        {
            var friday = new DateTime(2026, 11, 20, 9, 0, 0);
            int days = WeekdaySchedule.Day((int)DayOfWeek.Tuesday) | WeekdaySchedule.Day((int)DayOfWeek.Friday);
            using (var service = new ClaudeTaskService())
            {
                var work = service.Add("work", root, "workdays", TimeSpan.FromHours(10), DayOfWeek.Monday, 60, friday);
                var weekend = service.Add("weekend", root, "weekend", TimeSpan.FromHours(10), DayOfWeek.Saturday, 60, friday);
                var custom = service.Add("custom", root, "selected-days", TimeSpan.FromHours(10), DayOfWeek.Friday, 60, friday,
                    "background", "block", "orbit", days);
                Check(ClaudeTaskService.CalculateNextRun(work, friday.AddHours(2)) == friday.AddDays(3).AddHours(1)
                    && ClaudeTaskService.CalculateNextRun(weekend, friday) == friday.AddDays(1).AddHours(1)
                    && ClaudeTaskService.CalculateNextRun(custom, friday.AddHours(2)) == friday.AddDays(4).AddHours(1),
                    "task rules select the next workday, weekend day and one of several chosen days");
            }
            using (var service = new ClaudeTaskService())
            {
                var fired = new System.Collections.Generic.List<string>();
                service.TaskDue += task => fired.Add(task.Prompt);
                service.CheckNow(friday.AddDays(2));
                Check(fired.Count == 0, "overdue restricted tasks do not launch on an excluded day or before the time of an eligible day");
                service.CheckNow(friday.AddDays(2).AddHours(1));
                Check(String.Join(",", fired) == "weekend", "the weekend task launches on Sunday at its selected time");
                service.CheckNow(friday.AddDays(3).AddHours(1));
                Check(String.Join(",", fired) == "weekend,work", "only the weekday task launches on Monday");
                service.CheckNow(friday.AddDays(4).AddHours(1));
                fired.Sort(StringComparer.Ordinal);
                Check(String.Join(",", fired) == "custom,weekend,work,work", "reloaded task masks dispatch only on eligible days");
            }
            using (var service = new ReminderService())
            using (var window = new ReminderForm(service))
            {
                window.Show();Application.DoEvents();
                var input=(TextBox)Field(window,"task");var surface=(RoundedPanel)input.Parent;
                Check(surface.Name=="ReminderTextField"&&surface.BackColor.ToArgb()==Color.White.ToArgb()&&surface.ClientRectangle.Contains(input.Bounds),"reminder text has a padded, visible input boundary without clipped text");
                Color normal=surface.BorderColor;input.Focus();Application.DoEvents();
                Check(surface.BorderColor!=normal||input.Focused,"reminder input supports keyboard focus within its visible boundary");
                using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(Program.StateDirectory,"reminder-input.png"));}
                input.Enabled=false;Check(surface.BackColor.ToArgb()!=Color.White.ToArgb(),"disabled reminder field remains distinguishable");input.Enabled=true;
                var repeat = (ComboBox)Field(window, "recurrence");
                var picker = (WeekdayPicker)Field(window, "weekdayPicker");
                picker.Selection = days;
                ((DateTimePicker)Field(window, "time")).Value = DateTime.Today.AddHours(10);
                var labels = new[] { "work", "weekend", "custom" };
                for (int i = 0; i < labels.Length; i++)
                {
                    repeat.SelectedIndex = i + 2;
                    ((TextBox)Field(window, "task")).Text = labels[i];
                    typeof(ReminderForm).GetMethod("AddReminder", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                }
            }
            using (var service = new ReminderService())
            {
                var fired = new System.Collections.Generic.List<string>();
                service.ReminderDue += reminder => fired.Add(reminder.Text);
                for (int day = 0; day < 5; day++)
                {
                    service.CheckNow(friday.AddDays(day));
                    service.CheckNow(friday.AddDays(day).AddHours(1));
                    service.CheckNow(friday.AddDays(day).AddHours(1).AddSeconds(5));
                }
                fired.Sort(StringComparer.Ordinal);
                Check(String.Join(",", fired) == "custom,custom,weekend,weekend,work,work,work",
                    "all three day rules survive saving from the reminder editor and fire once per matching date");
            }
        }
        finally { Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", previous); }
    }

    private static void TestEditingAndConfigurationTips()
    {
        string previous=Program.StateDirectory,root=Path.Combine(previous,"editing-and-tips");Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",root);
        try
        {
            using(var service=new ReminderService())
            {
                var item=service.Add(TimeSpan.FromHours(9),"Original reminder","impact",null,WeekdaySchedule.Workdays);string id=item.Id;item.Enabled=false;item.LastTriggeredDate="2026-11-22";
                using(var form=new ReminderForm(service))
                {
                    form.Show();Application.DoEvents();((Button)Field(form,"edit")).PerformClick();
                    Check((string)((Button)Field(form,"add")).Text=="✓"&&((TextBox)Field(form,"task")).Text==item.Text,"reminder edit restores the original fields and exposes Save");
                    ((TextBox)Field(form,"task")).Text="Discarded";((Button)Field(form,"cancelEdit")).PerformClick();Check(item.Text=="Original reminder","cancel reminder edit leaves the saved reminder untouched");
                    ((Button)Field(form,"edit")).PerformClick();((TextBox)Field(form,"task")).Text="Updated reminder";((ComboBox)Field(form,"recurrence")).SelectedIndex=3;((DateTimePicker)Field(form,"time")).Value=DateTime.Today.AddHours(16);((ComboBox)Field(form,"effectMode")).SelectedIndex=1;((Button)Field(form,"add")).PerformClick();
                    Check(service.Reminders.Count==1&&item.Id==id&&item.Text=="Updated reminder"&&item.WeekdayMask==WeekdaySchedule.Weekend&&item.MinutesAfterMidnight==960&&item.Effect=="popup"&&!item.Enabled,"saving reminder edits updates in place and preserves ID and disabled state");
                    Check(item.LastTriggeredDate=="","changing reminder schedule clears the old delivery marker");form.Hide();
                }
            }
            using(var reloaded=new ReminderService())Check(reloaded.Reminders.Count==1&&reloaded.Reminders[0].Text=="Updated reminder"&&reloaded.Reminders[0].WeekdayMask==WeekdaySchedule.Weekend,"edited reminders persist without duplicates after restart");
            string exe=Path.Combine(root,"custom-claude.cmd"),settings=Path.Combine(root,"profile.json");File.WriteAllText(exe,"@exit /b 0");File.WriteAllText(settings,"{}");var preferences=new IntegrationPreferences();preferences.SetExecutablePath("spark",exe);preferences.SetSettingsPath("spark",settings);preferences.Save();
            string tip=CompanionConfigurationTip.Describe("spark");Check(tip.Contains(exe)&&tip.Contains(settings),"configuration hover resolves the assigned executable and settings file without exposing file contents");
            using(var service=new ClaudeTaskService())
            {
                var item=service.Add("Original task",root,"daily",TimeSpan.FromHours(9),DayOfWeek.Monday,60,DateTime.Now,"background","block","orbit");string id=item.Id;item.History.Add(new ClaudeTaskRunRecord {Status="success",CharacterId="orbit",Prompt="Original task"});
                using(var form=new ClaudeTaskForm(service))
                {
                    form.ShowNear(new Rectangle(500,20,96,104),root);Application.DoEvents();((Button)Field(form,"edit")).PerformClick();
                    Check(Field(form,"configurationTip")!=null&&Object.ReferenceEquals(Field(form,"editingTask"),item),"task creation picker and saved-task list share configuration hover support and a visible edit action");
                    string different=Path.Combine(root,"different");Directory.CreateDirectory(different);form.Hide();form.ShowNear(new Rectangle(500,20,96,104),different);
                    Check((string)Field(form,"workingDirectory")==root,"reopening a task draft never silently replaces its working directory");
                    ((TextBox)Field(form,"prompt")).Text="Updated task";((ComboBox)Field(form,"owner")).SelectedIndex=1;((ComboBox)Field(form,"recurrence")).SelectedIndex=4;((DateTimePicker)Field(form,"time")).Value=DateTime.Today.AddHours(17);((Button)Field(form,"save")).PerformClick();
                    Check(service.Tasks.Count==1&&item.Id==id&&item.Prompt=="Updated task"&&item.CharacterId=="spark"&&item.Recurrence=="weekend"&&item.MinutesAfterMidnight==1020&&item.History.Count==1&&item.History[0].CharacterId=="orbit","task edits update owner and schedule in place while preserving original run history");form.Hide();
                }
            }
            using(var reloaded=new ClaudeTaskService())Check(reloaded.Tasks.Count==1&&reloaded.Tasks[0].Prompt=="Updated task"&&reloaded.Tasks[0].CharacterId=="spark"&&reloaded.Tasks[0].History.Count==1,"task changes and historical ownership survive restart");
        }
        finally{Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",previous);}
    }

    private static void TestWeeklyIntervals()
    {
        string previous=Program.StateDirectory;
        string root=Path.Combine(previous,"weekly-interval");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",root);
        try
        {
            DateTime monday=new DateTime(2026,11,23,8,0,0);
            int mask=WeekdaySchedule.Day(1)|WeekdaySchedule.Day(3);
            using(var service=new ClaudeTaskService())
            {
                var rule=service.Add("weekly hours",root,"weekly-interval",TimeSpan.FromHours(9),DayOfWeek.Monday,120,monday,"background","block","orbit",mask);
                Check(ClaudeTaskService.CalculateNextRun(rule,monday)==monday.AddHours(1),"selected weekday interval starts at the chosen local time");
                Check(ClaudeTaskService.CalculateNextRun(rule,monday.AddHours(1).AddSeconds(6))==monday.AddHours(3),"late dispatch does not shift the anchored two-hour rhythm");
                Check(ClaudeTaskService.CalculateNextRun(rule,monday.Date.AddHours(23))==monday.Date.AddDays(2).AddHours(9),"weekday interval skips midnight and unselected Tuesday");
                int fired=0;service.TaskDue+=delegate{fired++;};
                rule.NextRunAt=monday.AddHours(1).ToString("o");
                service.CheckNow(monday.Date.AddDays(1).AddHours(10));
                Check(fired==0&&DateTime.Parse(rule.NextRunAt)==monday.Date.AddDays(2).AddHours(9),"missed weekly intervals do not launch on an excluded weekday");
                service.CheckNow(monday.Date.AddDays(2).AddHours(9));
                Check(fired==1&&DateTime.Parse(rule.NextRunAt)==monday.Date.AddDays(2).AddHours(11),"selected day starts one run and schedules the next anchored hour");
                using(var form=new ClaudeTaskForm(service))
                {
                    form.ShowNear(new Rectangle(500,30,192,208),root);Application.DoEvents();
                    var list=(ListBox)Field(form,"list");list.SelectedItem=rule;
                    typeof(ClaudeTaskForm).GetMethod("EditSelectedTask",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                    Check(((WeekdayPicker)Field(form,"weekdayPicker")).Selection==mask&&((NumericUpDown)Field(form,"intervalValue")).Value==2,
                        "editing restores multiple weekdays and the hourly interval");
                    form.Hide();
                }
            }
            using(var reloaded=new ClaudeTaskService())
                Check(reloaded.Tasks[0].Recurrence=="weekly-interval"&&reloaded.Tasks[0].WeekdayMask==mask&&reloaded.Tasks[0].IntervalMinutes==120,"weekday-hour schedule survives restart");
        }
        finally{Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",previous);}
    }

    [DllImport("user32.dll")]
    private static extern bool GetLayeredWindowAttributes(IntPtr window, out uint key, out byte alpha, out uint flags);

    private static void TestConversationPopup(string assets)
    {
        string previousAssets = Environment.GetEnvironmentVariable("AGENT_PAL_TEST_ASSETS");
        string previousState = Program.StateDirectory;
        Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS", assets);
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", Path.Combine(previousState, "conversation"));
        var instance = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            using (var pet = new PetForm())
            {
                pet.Show();
                ((Timer)Field(pet, "timer")).Stop();
                var bubble = (ReplyBubbleForm)Field(pet, "replyBubble");
                var runner = (ClaudeCliRunner)Field(pet, "claudeRunner");
                var show = typeof(PetForm).GetMethod("ShowReplyBubble", instance);
                typeof(PetForm).GetMethod("BeginReplyBubble", instance).Invoke(pet, new object[] { "准备测试" });
                bubble.SetStatus("正在准备");
                bubble.AppendReply(" \r\n");
                runner.ParseLine("CLI startup diagnostic");
                show.Invoke(pet, new object[] { true });
                Check(!bubble.Visible, "request preparation and whitespace do not open the conversation window");
                typeof(PetForm).GetField("dockedEdge", instance).SetValue(pet, "bottom");
                typeof(PetForm).GetField("dockOpacityPercent", instance).SetValue(pet, 40);
                Point parked = pet.Location;
                runner.ParseLine("{\"type\":\"stream_event\",\"event\":{\"delta\":{\"type\":\"text_delta\",\"text\":\"这是第一段回复。\"}}}");
                Check(bubble.Visible && bubble.PreviewTextForTest.Contains("这是第一段回复。") && pet.Location == parked,
                    "the first real reply opens immediately without moving the docked pet");
                byte alpha;
                uint key, flags;
                Check(GetLayeredWindowAttributes(bubble.Handle, out key, out alpha, out flags) && (flags & 2) != 0 && alpha == 102 &&
                    ((PromptForm)Field(pet, "promptForm")).Opacity == bubble.Opacity &&
                    ((ActivityTrayForm)Field(pet, "activityTray")).Opacity == bubble.Opacity,
                    "native reply alpha matches the docked pet and applies to the input and expanded conversation");
                typeof(PetForm).GetMethod("ReleaseEdgeDock", instance).Invoke(pet, null);
                Check(bubble.Opacity == 1, "undocking restores full conversation opacity on the same window");
                pet.Close();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR", previousState);
            Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS", previousAssets);
        }
    }

    private static void TestEditingPause(string assets)
    {
        string previousAssets=Environment.GetEnvironmentVariable("AGENT_PAL_TEST_ASSETS");
        string previousState=Program.StateDirectory;
        Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS",assets);
        Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",Path.Combine(previousState,"editing"));
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            using(var pet=new PetForm())
            {
                pet.Show();
                ((Timer)Field(pet,"timer")).Stop();
                var begin=typeof(PetForm).GetMethod("BeginCharacterConversation",flags);
                var role=PetCharacter.Find("orbit");
                var chat=(ActivityRecord)begin.Invoke(pet,new object[] {role,previousState});
                chat.AppendUserPrompt("第一次手动对话");
                chat.AppendOutput("第一次回复");
                chat.SessionId="retained-session";
                var proactive=(ActivityRecord)begin.Invoke(pet,new object[] {role,previousState});
                proactive.BeginAssistantTurn();
                proactive.AppendOutput("主动提醒休息");
                var same=(ActivityRecord)begin.Invoke(pet,new object[] {role,previousState});
                same.AppendUserPrompt("继续上一条");
                same.AppendOutput("后续回复");
                Check(Object.ReferenceEquals(chat,proactive)&&Object.ReferenceEquals(chat,same)&&chat.Messages.Count==5&&
                    chat.Messages[1].Text=="第一次回复"&&chat.Messages[2].Text=="主动提醒休息"&&chat.SessionId=="retained-session",
                    "manual prompts and proactive speech reuse one character history with distinct assistant turns and retained context");
                var other=(ActivityRecord)begin.Invoke(pet,new object[] {PetCharacter.Find("spark"),previousState});
                Check(!Object.ReferenceEquals(chat,other)&&other.Messages.Count==0,"different characters keep separate conversation histories");
                var changed=(ActivityRecord)begin.Invoke(pet,new object[] {role,Path.Combine(previousState,"another-directory")});
                Check(Object.ReferenceEquals(chat,changed)&&chat.Messages.Count==5&&chat.SessionId==null,
                    "changing the working directory retains character history without resuming the wrong CLI directory");
                typeof(PetForm).GetField("currentActivity",flags).SetValue(pet,null);
                var tasks=(ClaudeTaskForm)Field(pet,"claudeTaskForm");
                tasks.ShowNear(pet.Bounds,previousState);
                string directory=Path.Combine(previousState,"editing");
                Directory.CreateDirectory(directory);
                typeof(ClaudeTaskForm).GetField("workingDirectory",flags).SetValue(tasks,directory);
                var input=(TextBox)typeof(ClaudeTaskForm).GetField("prompt",flags).GetValue(tasks);
                input.Text="保留任务草稿";
                input.Select(2,2);
                var tick=typeof(PetForm).GetMethod("TickAnimation",flags);
                typeof(PetForm).GetField("nextIdleAction",flags).SetValue(pet,DateTime.UtcNow.AddSeconds(-1));
                typeof(PetForm).GetField("nextWander",flags).SetValue(pet,DateTime.UtcNow.AddSeconds(-1));
                Point position=pet.Location;
                typeof(PetForm).GetMethod("QueueReminder",flags).Invoke(pet,new object[] { new DailyReminder { Text="编辑后的提醒" } });
                var bubble=(ReplyBubbleForm)Field(pet,"replyBubble");
                typeof(PetForm).GetMethod("BeginReplyBubble",flags).Invoke(pet,new object[] { "Background reply" });
                bubble.AppendReply("回复");
                typeof(PetForm).GetMethod("ShowReplyBubble",flags).Invoke(pet,new object[] { false });
                tick.Invoke(pet,new object[] { null,EventArgs.Empty });
                typeof(PetForm).GetMethod("AnchorAuxiliaryWindows",flags).Invoke(pet,null);
                Check(tasks.Visible&&pet.Location==position&&(bool)Field(pet,"interactionPaused")&&
                    !bubble.Visible&&Field(pet,"reminderOverlay")==null,"pet pauses roaming, incoming replies and reminders while the task editor is open");
                Check(input.Text=="保留任务草稿"&&input.SelectionStart==2&&input.SelectionLength==2&&
                    (string)typeof(ClaudeTaskForm).GetField("workingDirectory",flags).GetValue(tasks)==directory,
                    "window anchoring preserves an active task draft, selection and custom directory");
                tasks.Hide();
                tick.Invoke(pet,new object[] { null,EventArgs.Empty });
                Check(!(bool)Field(pet,"interactionPaused")&&Field(pet,"reminderOverlay")!=null,"deferred reminder appears once editing ends");
                typeof(PetForm).GetMethod("FinishReminder",flags).Invoke(pet,null);
                tick.Invoke(pet,new object[] { null,EventArgs.Empty });
                Check(bubble.Visible,"deferred reply appears after the reminder is dismissed");
                pet.Close();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_PAL_STATE_DIR",previousState);
            Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS",previousAssets);
        }
    }

    private static void TestLanguage()
    {
        UiLanguage.Set("en");
        using (var window = new Form())
        using (var input = new TextBox { Text = "取消 🌱 用户原文" })
        using (var label = new Label { Text = "执行 Agent" })
        using (var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList })
        {
            choice.Items.AddRange(new object[] { "每天", "每周" }); choice.SelectedIndex = 1;
            window.Controls.AddRange(new Control[] { input, label, choice });
            UiLanguageBindings.Apply(window);
            Check(label.Text == "Run as Agent" && (string)choice.Items[1] == "Weekly" && choice.SelectedIndex == 1, "English Agent captions preserve selection");
            UiLanguage.Set("zh-CN"); UiLanguageBindings.Apply(window);
            Check(label.Text == "执行 Agent" && (string)choice.Items[1] == "每周" && input.Text == "取消 🌱 用户原文", "switching back preserves Agent captions and user input");
            UiLanguage.Set("en");
            typeof(UiLanguage).GetField("selected", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            Check(UiLanguage.Current == "en", "language selection survives a fresh settings read");
        }
    }

    private static void TestProfiles()
    {
        string root = AgentPal.Program.StateDirectory;
        var preferences = new IntegrationPreferences();
        string global = Path.Combine(root, "global claude.cmd"); File.WriteAllText(global, "@exit /b 0");
        preferences.SetExecutablePath(null, global);
        foreach (PetCharacter role in PetCharacter.All)
        {
            string executable = Path.Combine(root, role.Id + " 中文 ' cli.cmd"); File.WriteAllText(executable, "@exit /b 0");
            string settings = Path.Combine(root, role.Id + " settings.json"); File.WriteAllText(settings, "{\"env\":{\"PROFILE\":\"" + role.Id + "\"}}", new UTF8Encoding(false));
            preferences.SetExecutablePath(role.Id, executable);
            preferences.SetSettingsPath(role.Id, settings);
        }
        preferences.Save();
        foreach (PetCharacter role in PetCharacter.All)
        {
            string executable = preferences.ExecutablePathFor(role.Id);
            Check(ClaudeCliRunner.FindClaudeCommand(role.Id.ToUpperInvariant()) == executable && ClaudeSettingsFile.CurrentPath(role.Id) == preferences.SettingsPathFor(role.Id), role.Id + " resolves its own executable and JSON profile");
            string script = PetForm.BuildTerminalStartInfo(root, role.Id).Arguments;
            script = Encoding.Unicode.GetString(Convert.FromBase64String(script.Substring(script.LastIndexOf(' ') + 1)));
            Check(script.Contains(executable.Replace("'", "''")) && script.Contains(preferences.SettingsPathFor(role.Id)), role.Id + " manual terminal uses the same profile with quoted paths");
            var task = new ScheduledClaudeTask { CharacterId = "orbit", WorkingDirectory = root };
            var record = new ClaudeTaskRunRecord { CharacterId = role.Id, BackgroundJobId = "job-profile-test", WorkingDirectory = root };
            script = PetForm.BuildBackgroundAttachStartInfo(task, record).Arguments;
            script = Encoding.Unicode.GetString(Convert.FromBase64String(script.Substring(script.LastIndexOf(' ') + 1)));
            Check(script.Contains(executable.Replace("'", "''")) && script.Contains("attach 'job-profile-test'") && script.Contains(preferences.SettingsPathFor(role.Id)), role.Id + " background attach uses the recorded owner, not the edited task owner");
        }
        string removed = preferences.ExecutablePathFor("moss"); File.Delete(removed);
        bool rejected = false; try { ClaudeCliRunner.FindClaudeCommand("moss"); } catch (FileNotFoundException) { rejected = true; }
        Check(rejected, "missing configured executable fails instead of using a different companion's CLI");
        File.WriteAllText(removed, "@exit /b 0");
        preferences.SetExecutablePath("moss", ""); preferences.Save();
        Check(ClaudeCliRunner.FindClaudeCommand("moss") == global, "clearing an override restores the global executable");
    }

    private static void TestTasks(string assets)
    {
        Environment.SetEnvironmentVariable("AGENT_PAL_TEST_ASSETS", Path.GetFullPath(assets));
        using (var service = new ClaudeTaskService())
        {
            foreach (PetCharacter role in PetCharacter.All)
            {
                var task = service.Add("Keep original 中文🌱 " + role.Id, AgentPal.Program.StateDirectory, "daily", TimeSpan.FromHours(9), DayOfWeek.Monday, 60, DateTime.Now, "background", "block", role.Id);
                var run = service.BeginRun(task, DateTime.Now);
                service.CompleteRun(task, run.Id, true, 0, "原文 🌱", DateTime.Now);
                service.RecordSkipped(task, DateTime.Now, "Still active");
                string replacement = role.Id == "orbit" ? "spark" : "orbit";
                string editedDirectory = Path.Combine(AgentPal.Program.StateDirectory, "edited-folder"); Directory.CreateDirectory(editedDirectory);
                service.Update(task, "Updated task 中文🌱", editedDirectory, "weekly", TimeSpan.FromHours(10), DayOfWeek.Friday, 60, DateTime.Now, "background", "block", replacement);
                Check(run.CharacterId == role.Id && task.History[0].CharacterId == role.Id && task.CharacterId == replacement, "editing owner preserves historical and skipped run identities for " + role.Id);
                Check(run.WorkingDirectory == AgentPal.Program.StateDirectory && run.Prompt.Contains("Keep original") && ClaudeTaskRunTranscriptForm.BuildTranscript(task, run).Contains("Keep original"), "historical directory and original prompt survive task edits for " + role.Id);
            }
            using (var window = new ClaudeTaskForm(service))
            {
                var list = (ListBox)Field(window, "list");
                Check(list.Items.Count == 4, "one task window includes all companions");
                foreach (PetCharacter role in PetCharacter.All)
                    Check(window.GetType().GetMethod("Avatar", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { role.Id }) is Image, role.Id + " task avatar loads the original character art");
                UiLanguageBindings.Apply(window);
                Check(window.Text == "Scheduled tasks", "task window title is localized");
                UiLanguage.Set("zh-CN"); UiLanguageBindings.Apply(window);
                Check(((ScheduledClaudeTask)list.Items[0]).Prompt.Contains("中文🌱"), "language switching leaves task prompts untouched");

                var selected = (ScheduledClaudeTask)list.SelectedItem;
                window.GetType().GetMethod("EditSelectedTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                var repeat = (ComboBox)Field(window, "recurrence");
                var weekday = (ComboBox)Field(window, "weekday");
                Check(repeat.SelectedIndex == 1 && weekday.SelectedIndex == (int)DayOfWeek.Friday, "task editor restores the saved weekly day");
                weekday.SelectedIndex = (int)DayOfWeek.Sunday;
                window.GetType().GetMethod("SaveTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                Check(selected.Weekday == (int)DayOfWeek.Sunday && TaskPresentation.Schedule(selected) == "每周日 10:00" &&
                      ClaudeTaskService.CalculateNextRun(selected, new DateTime(2026, 11, 14, 12, 0, 0)) == new DateTime(2026, 11, 15, 10, 0, 0),
                      "editing a weekly task updates its schedule and next execution day");
            }
            var legacy = service.Tasks[0]; legacy.CharacterId = ""; legacy.History[0].CharacterId = "";
            service.AssignLegacyTasks("pippin");
            Check(legacy.CharacterId == "pippin" && legacy.History[0].CharacterId == "pippin", "legacy tasks and records receive an explicit migrated owner");
        }
        using (var reloaded = new ClaudeTaskService())
        {
            var weekly = new System.Collections.Generic.List<ScheduledClaudeTask>(reloaded.Tasks).Find(task => task.Weekday == (int)DayOfWeek.Sunday);
            Check(reloaded.Tasks.Count == 4 && reloaded.Tasks[0].History.Count > 0 && !String.IsNullOrEmpty(reloaded.Tasks[0].CharacterId) && weekly != null,
                  "weekly day, owner assignments and run history persist across reload");
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var dateStyle = System.Globalization.DateTimeStyles.RoundtripKind;
            DateTime due = DateTime.Parse(weekly.NextRunAt, culture, dateStyle);
            int runs = 0;
            reloaded.TaskDue += delegate(ScheduledClaudeTask task) { if (task == weekly) runs++; };
            reloaded.CheckNow(due.AddSeconds(-1));
            Check(runs == 0 && due.DayOfWeek == DayOfWeek.Sunday && due.TimeOfDay == TimeSpan.FromHours(10), "the saved weekly task waits for Sunday at 10:00");
            reloaded.CheckNow(due);
            reloaded.CheckNow(due.AddSeconds(1));
            Check(runs == 1 && DateTime.Parse(weekly.NextRunAt, culture, dateStyle) == due.AddDays(7),
                  "the weekly task dispatches once and schedules the same time next week");
        }
    }

    private static void TestReminderWeekday()
    {
        string path = Path.Combine(AgentPal.Program.StateDirectory, "reminders.json");
        File.WriteAllText(path, "{\"Reminders\":[{\"Text\":\"Legacy daily\",\"MinutesAfterMidnight\":600}]}");
        UiLanguage.Set("en");
        using (var service = new ReminderService())
        using (var window = new ReminderForm(service))
        {
            var repeat = (ComboBox)Field(window, "recurrence");
            var weekday = (ComboBox)Field(window, "weekday");
            UiLanguageBindings.Apply(window);
            Check(repeat.SelectedIndex == 0 && (string)repeat.Items[1] == "Weekly" && weekday.Items.Count == 7 &&
                  (string)weekday.Items[(int)DayOfWeek.Sunday] == "Sunday", "reminder editor offers daily and every weekday in English");
            Check((string)((ComboBox)Field(window,"effectMode")).Items[0]=="Performance"&&ReminderForm.FormatRemaining(TimeSpan.FromMinutes(25))=="Left 25:00",
                  "reminder effects and live countdown status are localized in English");
            UiLanguage.Set("zh-CN");
            Check(ReminderForm.FormatRemaining(TimeSpan.FromMinutes(25))=="剩余 25:00","countdown status respects Chinese language");
            UiLanguage.Set("en");
            repeat.SelectedIndex = 1;
            weekday.SelectedIndex = (int)DayOfWeek.Sunday;
            ((DateTimePicker)Field(window, "time")).Value = DateTime.Today.AddHours(10);
            ((TextBox)Field(window, "task")).Text = "Weekly reminder";
            window.GetType().GetMethod("AddReminder", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        }
        using (var service = new ReminderService())
        {
            var reminders = new System.Collections.Generic.List<DailyReminder>(service.Reminders);
            var legacy = reminders.Find(item => item.Text == "Legacy daily");
            var weekly = reminders.Find(item => item.Text == "Weekly reminder");
            Check(legacy != null && legacy.Weekday == -1 && weekly != null && weekly.Weekday == (int)DayOfWeek.Sunday,
                  "weekly reminder survives reload and a legacy reminder stays daily");
            int dailyCount = 0, weeklyCount = 0;
            service.ReminderDue += delegate(DailyReminder reminder)
            {
                if (reminder == legacy) dailyCount++;
                if (reminder == weekly) weeklyCount++;
            };
            var saturday = new DateTime(2026, 11, 14, 9, 59, 0);
            service.CheckNow(saturday);
            service.CheckNow(saturday.AddMinutes(1));
            Check(dailyCount == 1 && weeklyCount == 0, "Saturday fires the daily reminder only");
            service.CheckNow(saturday.AddDays(1));
            service.CheckNow(saturday.AddDays(1).AddMinutes(1));
            service.CheckNow(saturday.AddDays(1).AddMinutes(2));
            Check(dailyCount == 2 && weeklyCount == 1, "Sunday fires the weekly reminder once at 10:00");
            service.CheckNow(saturday.AddDays(8));
            service.CheckNow(saturday.AddDays(8).AddMinutes(1));
            Check(weeklyCount == 2, "the same weekly reminder fires again the following Sunday");
        }
    }
}
