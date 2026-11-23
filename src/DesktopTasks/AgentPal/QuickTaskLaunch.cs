// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AgentPal
{
    internal sealed class QuickTaskReopen
    {
        public string Command="",Companion="",Directory="";
    }
    internal static class QuickTaskLaunch
    {
        internal static string Relaunch(QuickTaskCommand command,QuickTaskBinding binding,string directory)
        {
            // Shell's relaunch property has a short length limit. Keep long,
            // Unicode folder names in local data, never in that command line.
            string token;
            using(var hash=SHA256.Create())token=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(command.Id+"|"+binding.Id)),0,16).Replace("-","").ToLowerInvariant();
            try{WorkshopFiles.Save(Path.Combine(Program.StateDirectory,"quick-task-contexts",token+".json"),new QuickTaskReopen{Command=command.Id,Companion=binding.Id,Directory=directory});}
            catch(IOException){return "\""+typeof(Program).Assembly.Location+"\"";}
            catch(UnauthorizedAccessException){return "\""+typeof(Program).Assembly.Location+"\"";}
            return "\""+typeof(Program).Assembly.Location+"\" --reopen-quick-task "+token;
        }
        internal static string[] ExpandReopen(string[] args)
        {
            if(args.Length==0||args[0]!="--reopen-quick-task")return args;
            if(args.Length!=2||!System.Text.RegularExpressions.Regex.IsMatch(args[1],"\\A[0-9a-f]{32}\\z"))throw new ArgumentException(UiLanguage.Text("快捷任务参数无效。"));
            var context=WorkshopFiles.Load<QuickTaskReopen>(Path.Combine(Program.StateDirectory,"quick-task-contexts",args[1]+".json"));
            return new[]{"--quick-task",context.Command,"--companion",context.Companion,"--directory",context.Directory};
        }
        internal static ProcessStartInfo BuildStartInfo(string directory,string characterId,string task)
        {
            string cwd=QuickTaskCommands.ResolveDirectory(directory,false);
            if(String.IsNullOrWhiteSpace(task)||task.Length>8000)throw new ArgumentException(UiLanguage.Text("请输入任务，最多 8000 字。"));
            ClaudeCliRunner.RequireClaudeCommand(characterId);
            ClaudeSettingsFile.CurrentPath(characterId);
            string file=Path.Combine(Program.StateDirectory,"quick-task-requests",Guid.NewGuid().ToString("N")+".json");
            WorkshopFiles.Save(file,new { Task=task,WorkingDirectory=cwd,Companion=characterId,Submitted=DateTime.UtcNow.ToString("o") });
            return PetForm.BuildTerminalStartInfo(cwd,characterId,file);
        }
        internal static bool Handle(string[] args)
        {
            if(args.Length==1&&args[0]=="--refresh-quick-commands")
            {
                try
                {
                    QuickTaskOptions options=QuickTaskCommands.Load();if(!options.Enabled)return true;
                    string executable=typeof(Program).Assembly.Location;
                    if(options.MainMenu&&QuickTaskModern.Supported)
                    {
                        if(!QuickTaskModern.PackageAvailable||QuickTaskModern.NeedsPreviewTrust){Environment.ExitCode=3;return true;}
                        QuickTaskModern.Register(false);QuickTaskModern.ApplyMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath,options,executable);QuickTaskNativeProbe.Verify(options);
                        QuickTaskShell.Remove(Registry.CurrentUser,@"Software\Classes");
                    }
                    else QuickTaskShell.Apply(Registry.CurrentUser,@"Software\Classes",options,executable);
                }
                catch(Exception error){Console.Error.WriteLine(error.Message);Environment.ExitCode=2;}
                return true;
            }
            if(args.Length==1&&args[0]=="--remove-quick-commands")
            {
                string executable=typeof(Program).Assembly.Location;
                QuickTaskShell.Remove(Registry.CurrentUser,@"Software\Classes",executable);
                QuickTaskModern.RemoveMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath,executable);
                if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")QuickTaskModern.RunRegistration("Remove",false);
                return true;
            }
            if(args.Length==0||(args[0]!="--quick-task"&&args[0]!="--reopen-quick-task"))return false;
            try
            {
                args=ExpandReopen(args);
                bool desktop=args.Length==5&&args[4]=="--desktop";
                if(!(desktop||args.Length==6&&args[4]=="--directory")||args[2]!="--companion"||!QuickTaskCommands.ValidId(args[1])||!QuickTaskCommands.ValidId(args[3]))
                    throw new ArgumentException(UiLanguage.Text("快捷任务参数无效。"));
                QuickTaskOptions settings=QuickTaskCommands.Load();QuickTaskCommands.Validate(settings);
                QuickTaskCommand command=settings.Commands.Find(item=>item.Id==args[1]);
                QuickTaskBinding binding=command==null?null:command.Companions.Find(item=>item.Id==args[3]);
                if(!settings.Enabled||binding==null)throw new InvalidOperationException(UiLanguage.Text("此快捷命令已停用或伙伴绑定已移除，请刷新右键菜单。"));
                string directory=QuickTaskCommands.ResolveDirectory(desktop?null:args[5],desktop);
                QuickTaskWindow.SetCompanionIdentity(binding.Id);
                Application.EnableVisualStyles();
                using(var window=new QuickTaskRequestForm(command,binding,directory,delegate(string task)
                    {if(binding.IsTeam)QuickTeamTasks.Submit(command,binding,directory,task);else Process.Start(BuildStartInfo(directory,binding.Id,task));}))
                    Application.Run(window);
            }
            catch(Exception error){MessageBox.Show(UiLanguage.Text(error.Message),Program.ProductName,MessageBoxButtons.OK,MessageBoxIcon.Warning);Environment.ExitCode=2;}
            return true;
        }
    }
    internal sealed class QuickTaskRequestForm:AppWindow
    {
        private Icon companionIcon;
        private bool submitting;
        private readonly string taskbarCompanion,taskbarIcon,taskbarRelaunch;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if(disposing&&companionIcon!=null){companionIcon.Dispose();companionIcon=null;}
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);QuickTaskWindow.Mark(Handle);
            QuickTaskTaskbar.Apply(Handle,taskbarCompanion,taskbarIcon,Text,taskbarRelaunch);
        }
        protected override void OnHandleDestroyed(EventArgs e){QuickTaskTaskbar.Clear(Handle);base.OnHandleDestroyed(e);}
        internal readonly ChineseInputTextBox TaskInput=new ChineseInputTextBox();
        internal readonly Button Confirm=new Button();
        internal QuickTaskRequestForm(QuickTaskCommand command,QuickTaskBinding pet,string directory,Action<string> submit)
        {
            taskbarCompanion=pet.Id;taskbarIcon=pet.IconPath;
            taskbarRelaunch=QuickTaskLaunch.Relaunch(command,pet,directory);
            Name="QuickTaskRequest";Text=UiLanguage.Format("给{0}指派任务",UiLanguage.Text(pet.Name));
            ClientSize=new Size(560,430);MinimumSize=new Size(460,370);Font=UiText.Font(9.5f);BackColor=Color.FromArgb(252,251,248);
            StartPosition=FormStartPosition.Manual;ShowInTaskbar=true;ShowIcon=true;
            Rectangle screen=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(screen.Left+(screen.Width-Width)/2,screen.Top+(screen.Height-Height)/2);
            var avatar=new PictureBox {Location=new Point(18,14),Size=new Size(48,48),SizeMode=PictureBoxSizeMode.Zoom};
            try{avatar.Image=QuickTaskIcons.ReadThumbnail(pet.IconPath);}catch(IOException){}catch(ArgumentException){}
            if(avatar.Image==null)avatar.Image=AppIcon.WindowIcon.ToBitmap();
            companionIcon=QuickTaskIcons.CreateWindowIcon(avatar.Image);Icon=companionIcon;
            var heading=new Label {Text=UiLanguage.Text(pet.Name)+" · "+UiLanguage.Text(command.Name),AutoEllipsis=true,Font=UiText.Font(13f,FontStyle.Bold),Location=new Point(78,16),Size=new Size(460,26),Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right};
            var hint=new Label {Name="QuickTaskHint",Text=UiLanguage.Text(pet.IsTeam?"本次目标交给已有团队，所有成员使用当前目录。保留职责、任务和依赖，不修改团队模板。":"使用该伙伴绑定的 Claude 程序和 settings，在下方目录打开原生 CLI。"),ForeColor=Color.FromArgb(105,112,104),Location=new Point(78,46),Size=new Size(460,40),Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right};
            var pathCaption=new Label {Text=UiLanguage.Text("当前文件夹"),Location=new Point(18,94),Size=new Size(260,23)};
            var path=new TextBox {Name="QuickTaskDirectory",Text=directory,ReadOnly=true,Location=new Point(18,120),Size=new Size(524,26),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
            var taskCaption=new Label {Text=UiLanguage.Text("任务命令"),Location=new Point(18,158),Size=new Size(260,23)};
            var inputCard=new RoundedPanel {Radius=12,BorderColor=UiDesign.Line,BackColor=Color.White,Padding=new Padding(12),Location=new Point(18,184),Size=new Size(524,176),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom};
            TaskInput.Name="QuickTaskInput";TaskInput.Text=command.Task??"";TaskInput.Multiline=true;TaskInput.AcceptsReturn=true;
            TaskInput.MaxLength=pet.IsTeam?4000:8000;TaskInput.ScrollBars=ScrollBars.Vertical;TaskInput.BorderStyle=BorderStyle.None;TaskInput.BackColor=Color.White;TaskInput.Dock=DockStyle.Fill;inputCard.Controls.Add(TaskInput);
            Confirm.Name="ConfirmQuickTask";Confirm.Text=UiLanguage.Text("确定");Confirm.Size=new Size(104,36);Confirm.Location=new Point(438,378);Confirm.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;
            var cancel=new Button {Text=UiLanguage.Text("取消"),Size=new Size(104,36),Location=new Point(322,378),Anchor=AnchorStyles.Right|AnchorStyles.Bottom,DialogResult=DialogResult.Cancel};
            cancel.Click+=delegate{Close();};
            Confirm.Click+=delegate
            {
                if(submitting||String.IsNullOrWhiteSpace(TaskInput.Text)||TaskInput.Text.Length>TaskInput.MaxLength)return;
                if(!pet.IsTeam)
                {
                    try{submit(TaskInput.Text);DialogResult=DialogResult.OK;Close();}
                    catch(Exception error){MessageBox.Show(this,UiLanguage.Text(error.Message),Program.ProductName,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                    return;
                }
                string request=TaskInput.Text;submitting=true;TaskInput.ReadOnly=true;Confirm.Enabled=cancel.Enabled=false;UseWaitCursor=true;
                Confirm.Text=UiLanguage.Text("正在提交…");
                var worker=new System.Threading.Thread(delegate()
                {
                    Exception failure=null;try{submit(request);}catch(Exception error){failure=error;}
                    BeginInvoke((Action)delegate
                    {
                        submitting=false;UseWaitCursor=false;TaskInput.ReadOnly=false;cancel.Enabled=true;Confirm.Enabled=true;Confirm.Text=UiLanguage.Text("确定");
                        if(failure==null){DialogResult=DialogResult.OK;Close();}
                        else{MessageBox.Show(this,UiLanguage.Text(failure.Message),Program.ProductName,MessageBoxButtons.OK,MessageBoxIcon.Warning);TaskInput.Focus();}
                    });
                });worker.IsBackground=true;worker.Start();
            };
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(submitting)e.Cancel=true;};
            TaskInput.TextChanged+=delegate{Confirm.Enabled=!String.IsNullOrWhiteSpace(TaskInput.Text);};Confirm.Enabled=!String.IsNullOrWhiteSpace(TaskInput.Text);
            TaskInput.KeyDown+=delegate(object sender,KeyEventArgs key){if(key.Control&&key.KeyCode==Keys.Enter&&!TaskInput.IsComposing){key.SuppressKeyPress=true;Confirm.PerformClick();}};
            var shortcut=new Label {Text=UiLanguage.Text("Ctrl+Enter 确定 · Enter 换行"),Font=UiText.Font(8f),ForeColor=Color.FromArgb(117,123,115),Location=new Point(18,386),Size=new Size(290,22),Anchor=AnchorStyles.Left|AnchorStyles.Bottom};
            var tips=new ToolTip();tips.SetToolTip(path,directory);tips.SetToolTip(heading,heading.Text);
            Controls.AddRange(new Control[]{avatar,heading,hint,pathCaption,path,taskCaption,inputCard,shortcut,Confirm,cancel});CancelButton=cancel;
            Shown+=delegate{TaskInput.Focus();TaskInput.Select(TaskInput.TextLength,0);};
            FormClosed+=delegate{tips.Dispose();if(avatar.Image!=null)avatar.Image.Dispose();};
            UiLanguageBindings.Apply(this);
        }
    }
}
