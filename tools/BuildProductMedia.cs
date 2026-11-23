// SPDX-License-Identifier: Apache-2.0
// Real application controls with illustrative English content. Never submits work.
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AgentPal;

internal static class BuildProductMedia
{
    internal static void Render(string output)
    {
        if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")throw new InvalidOperationException("Documentation requires isolated demonstration state.");
        RenderCompanions(output);
        string icon=Path.Combine(Program.StateDirectory,"documentation-qingheng.ico");
        using(var sprite=Image.FromFile(PetCharacter.Find("orbit").AvatarPath))QuickTaskIcons.Write(sprite,icon);
        var command=new QuickTaskCommand {Id="review-project",Name="Review code"};
        var companion=new QuickTaskBinding {Id="orbit",Name="青衡",IconPath=icon};
        using(var form=new QuickTaskRequestForm(command,companion,@"C:\Projects\sample-app",delegate(string task)
        {throw new InvalidOperationException("Documentation rendering must never submit a task.");}))
        {
            form.TaskInput.Text="Review the project structure and identify the three most important improvements.\r\n\r\nSummarize your findings with file references and a short action plan.";
            using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"folder-task.png"));
        }
        var activity=new ActivityRecord {Title="Project review",CharacterId="orbit",CharacterConversation=true,
            SessionId="illustrative-review",State="就绪",Updated=new DateTime(2026,11,22,9,42,0)};
        activity.StartConversation("Review this project and suggest the next steps.");
        activity.AppendOutput("## Project review\n\nThe core workflow is easy to follow. I would focus on three improvements:\n\n"+
            "1. **Clarify configuration** — document the required settings.\n2. **Cover failure paths** — test invalid input.\n"+
            "3. **Keep changes small** — review one behavior at a time.\n\n### Next step\nStart with configuration validation, then run the existing checks.");
        activity.Messages[activity.Messages.Count-1].Process="[Tool] Read — README.md\n[Tool] Read — src/configuration.cs\n"+
            "[Tool] Glob — tests/**/*.cs\n[Tool result] Project files inspected.\nIllustrative transcript; no CLI or model was called.";
        foreach(var message in activity.Messages)message.At=new DateTime(2026,11,22,9,42,0);
        using(var form=new ActivityTrayForm())
        {
            form.SetActivities(new[]{activity});form.SelectActivity(activity);form.FlushConversationForTest();
            using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"desktop-chat.png"));
        }
        using(var form=new AboutForm())
        using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"about-en.png"));
        using(var manager=new CompanionManagerForm(()=>"orbit",p=>{},p=>{},()=>{}))
        using(var image=BuildReadmeMedia.Capture(manager))image.Save(Path.Combine(output,"companion-manager.png"));
        UiLanguage.Set("zh-CN");
        using(var manager=new CompanionManagerForm(()=>"orbit",p=>{},p=>{},()=>{}))
        using(var image=BuildReadmeMedia.Capture(manager))image.Save(Path.Combine(output,"companion-manager.zh-CN.png"));
        UiLanguage.Set("en");
        var sample=new PetCharacter {Id="documentation-sample",Name="Sample companion",ResourceRoot=Path.Combine(Program.StateDirectory,"sample-pack")};
        using(var editor=new TrailColorForm(sample))
        using(var image=BuildReadmeMedia.Capture(editor))image.Save(Path.Combine(output,"trail-colors.png"));
        string workspace=Path.Combine(Program.StateDirectory,"sample-app");Directory.CreateDirectory(workspace);
        var team=CompanionRolePresets.Create("Code review team",workspace,true);team.Id="documentation-team";team.Goal="Review this project and summarize the findings.";
        var data=new WorkshopData();data.Sects.Add(team);WorkshopFiles.Save(QuickTeamTasks.WorkshopPath,data);
        var binding=QuickTeamTasks.Binding(team);binding.IconPath=QuickCommandsForm.CreateIcon(binding.Id);
        var together=new QuickTaskCommand {Name="Review together",Task="Review this project, compare the findings, and recommend the next steps."};together.Companions.Add(binding);
        var options=new QuickTaskOptions {Enabled=true,MainMenu=true};options.Commands.Add(together);QuickTaskCommands.Save(options);
        using(var form=new QuickTaskRequestForm(together,binding,@"C:\Projects\sample-app",delegate(string request){throw new InvalidOperationException("Documentation never submits work.");}))
        using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"folder-team-task.png"));
        using(var form=new QuickCommandsForm())
        using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"folder-task-bindings.png"));
        together.Companions.Insert(0,companion);QuickTaskCommands.Save(options);
        RenderMenu(output);
        RenderPlanning(output,workspace);
        using(var first=new SectExecution(team,delegate{throw new InvalidOperationException("Documentation never launches an agent.");}))
        using(var second=new SectExecution(team,delegate{throw new InvalidOperationException("Documentation never launches an agent.");}))
        {
            first.Record.Id="c742a900000000000000000000000001";second.Record.Id="a81f7c00000000000000000000000002";
            first.Record.Started="2026-11-22T09:40:00";second.Record.Started="2026-11-22T09:42:00";
            first.Record.Goal="Review configuration handling.";second.Record.Goal="Review the desktop task workflow.";
            foreach(var run in new[]{first.Record,second.Record})
            {run.Steps[0].Status="success";run.Steps[0].Output="Scope and review checklist prepared.";run.Steps[1].Status="running";run.Steps[1].Output="Inspecting the implementation and collecting evidence.";}
            data.History.Add(second.Record);data.History.Add(first.Record);WorkshopForm form=null;
            using(form=new WorkshopForm(data,delegate{},delegate{return form!=null&&Object.ReferenceEquals(form.SelectedRun,first.Record)?first:second;},delegate{throw new InvalidOperationException("Documentation never starts work.");},delegate{}))
            {form.ShowExecution(second.Record);using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"agent-batches.png"));}
        }
    }

    private static void RenderCompanions(string output)
    {
        var profiles=PetCharacter.Defaults;
        using(var image=new Bitmap(1440,430))using(var g=Graphics.FromImage(image))
        {
            g.Clear(Color.FromArgb(238,239,234));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            using(var title=UiText.Font(20))using(var label=UiText.Font(11))using(var ink=new SolidBrush(Color.FromArgb(32,66,67)))
            {
                g.DrawString("Six pals. One companion spirit.",title,ink,24,18);
                for(int i=0;i<profiles.Length;i++)
                using(var frame=Image.FromFile(profiles[i].AvatarPath))
                {
                    g.DrawImage(frame,new Rectangle(i*240+8,66,224,243));
                    string name=UiLanguage.Text(profiles[i].Name);SizeF size=g.MeasureString(name,label);
                    g.DrawString(name,label,ink,i*240+120-size.Width/2,352);
                }
            }
            image.Save(Path.Combine(output,"companions.png"));
        }
    }
    private static T Field<T>(object instance,string name)
    {return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
    private static void RenderMenu(string output)
    {
        using(var form=new QuickCommandsForm())
        {
            form.Show();Application.DoEvents();
            typeof(QuickCommandsForm).GetMethod("PreviewMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
            Application.DoEvents();var menu=Field<ContextMenuStrip>(form,"preview");
            var root=(ToolStripMenuItem)menu.Items[0];if(root.HasDropDownItems)root.ShowDropDown();Application.DoEvents();
            using(var top=new Bitmap(menu.Width,menu.Height))
            using(var children=root.HasDropDownItems?new Bitmap(root.DropDown.Width,root.DropDown.Height):null)
            using(var canvas=new Bitmap(Math.Max(top.Width,children==null?0:children.Width)+48,top.Height+(children==null?0:children.Height)+94))
            using(var g=Graphics.FromImage(canvas))
            {
                menu.DrawToBitmap(top,new Rectangle(Point.Empty,top.Size));if(children!=null)root.DropDown.DrawToBitmap(children,new Rectangle(Point.Empty,children.Size));
                g.Clear(Color.FromArgb(238,239,234));g.DrawImageUnscaled(top,24,18);if(children!=null)g.DrawImageUnscaled(children,24,top.Height+32);
                using(var font=UiText.Font(9f))using(var brush=new SolidBrush(Color.FromArgb(88,94,86)))
                    g.DrawString("Configured menu · AgentPal in-app preview",font,brush,24,canvas.Height-30);
                canvas.Save(Path.Combine(output,"folder-menu.png"));
            }
        }
    }
    private static void RenderPlanning(string output,string workspace)
    {
        using(var service=new ClaudeTaskService())
        {
            var scheduled=service.Add("Review project notes and summarize the next steps.",workspace,"weekly-interval",TimeSpan.FromHours(9),DayOfWeek.Monday,120,
                new DateTime(2026,11,22,8,0,0),"background","block","orbit",WeekdaySchedule.Day(1)|WeekdaySchedule.Day(3));
            var other=service.Add("Check pending documentation updates.",workspace,"workdays",TimeSpan.FromHours(17),DayOfWeek.Monday,60,
                new DateTime(2026,11,22,8,0,0),"terminal","parallel","spark");
            scheduled.WorkingDirectory=other.WorkingDirectory=@"C:\Projects\sample-app";
            using(var form=new ClaudeTaskForm(service))
            {
                Field<ListBox>(form,"list").SelectedItem=scheduled;
                typeof(ClaudeTaskForm).GetMethod("EditSelectedTask",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"scheduled-tasks.png"));
            }
        }
        using(var service=new ReminderService())
        {
            service.Add(TimeSpan.FromHours(15),"Stand up, stretch, and rest your eyes.","impact",null,WeekdaySchedule.Workdays);
            service.Add(TimeSpan.FromHours(18),"Plan tomorrow's next steps.","popup",null,WeekdaySchedule.Workdays);
            service.StartCountdown(25);
            using(var form=new ReminderForm(service))
            {
                Field<TextBox>(form,"task").Text="Stand up, stretch, and rest your eyes.";
                Field<ComboBox>(form,"recurrence").SelectedIndex=2;
                Field<DateTimePicker>(form,"time").Value=DateTime.Today.AddHours(15);
                using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"reminders.png"));
                Field<Button>(form,"edit").PerformClick();
                using(var image=BuildReadmeMedia.Capture(form))image.Save(Path.Combine(output,"reminder-edit.png"));
            }
        }
    }
}
