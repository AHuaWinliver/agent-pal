// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AgentPal
{
    internal sealed class QuickCommandsForm:AppWindow
    {
        private readonly QuickTaskOptions options;
        private readonly ListBox list=new ListBox {Dock=DockStyle.Fill,IntegralHeight=false,BorderStyle=BorderStyle.None};
        private readonly TextBox name=new TextBox {Dock=DockStyle.Fill};
        private readonly ChineseInputTextBox task=new ChineseInputTextBox {Dock=DockStyle.Fill,Multiline=true,AcceptsReturn=true,ScrollBars=ScrollBars.Vertical,MaxLength=8000};
        private readonly CheckedListBox pets=new CheckedListBox {Dock=DockStyle.Fill,CheckOnClick=true,BorderStyle=BorderStyle.None};
        private readonly CheckBox desktop=new CheckBox {AutoSize=true,Text=UiLanguage.Text("桌面右键")};
        private readonly CheckBox folders=new CheckBox {AutoSize=true,Text=UiLanguage.Text("文件夹与空白处右键")};
        private readonly CheckBox mainMenu=new CheckBox {AutoSize=true,Text=UiLanguage.Text("直接显示在 Windows 11 主菜单")};
        private readonly Label registrationStatus=new Label {AutoSize=true,MaximumSize=new Size(720,0)};
        private bool applying;
        private string registrationError="";
        private QuickTaskCommand selected;
        private bool loading;
        private ContextMenuStrip preview;
        [DllImport("shell32.dll")]private static extern void SHChangeNotify(uint id,uint flags,IntPtr first,IntPtr second);
        internal QuickCommandsForm()
        {
            Name="QuickCommandsWindow";Text=UiLanguage.Text("右键快捷任务");Icon=AppIcon.WindowIcon;
            ClientSize=new Size(790,650);MinimumSize=new Size(710,600);StartPosition=FormStartPosition.CenterParent;Font=UiText.Font(9.5f);
            options=QuickTaskCommands.Load();desktop.Checked=options.Desktop;folders.Checked=options.Folders;
            mainMenu.Checked=QuickTaskModern.Supported&&options.MainMenu;mainMenu.Enabled=QuickTaskModern.Supported;
            foreach(PetCharacter character in PetCharacter.Available)pets.Items.Add(new QuickTaskBinding {Id=character.Id,Name=character.Name});
            foreach(CompanionSect team in WorkshopFiles.Load<WorkshopData>(QuickTeamTasks.WorkshopPath).Sects)pets.Items.Add(QuickTeamTasks.Binding(team));
            // Preserve bindings to temporarily unavailable character packs.
            foreach(QuickTaskCommand command in options.Commands)foreach(QuickTaskBinding binding in command.Companions)
                if(!HasPet(binding.Id))pets.Items.Add(binding);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new Padding(18)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,140));
            var introduction=new Label {Text=UiLanguage.Text("右键当前文件夹，将任务交给伙伴或已保存的协作团队。伙伴打开原生 CLI；团队按已有职责和依赖执行，并在 Agent 工作空间中显示。"),Dock=DockStyle.Fill,Padding=new Padding(0,3,0,0)};
            layout.Controls.Add(introduction,0,0);layout.SetColumnSpan(introduction,2);
            var left=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,Margin=new Padding(0,0,16,0)};
            left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,42));left.Controls.Add(list,0,0);
            var tools=new FlowLayoutPanel {Dock=DockStyle.Fill};tools.Controls.Add(ActionButton("添加",Add));tools.Controls.Add(ActionButton("删除",Remove));left.Controls.Add(tools,0,1);
            layout.Controls.Add(left,0,1);
            var editor=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6};
            foreach(int height in new[]{26,34,28,150,28})editor.RowStyles.Add(new RowStyle(SizeType.Absolute,height));editor.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            editor.Controls.Add(Caption("快捷命令名称"),0,0);editor.Controls.Add(name,0,1);
            editor.Controls.Add(Caption("预填任务（可留空，点击菜单后输入）"),0,2);editor.Controls.Add(task,0,3);
            editor.Controls.Add(Caption("绑定执行者：伙伴 / 协作团队（可多选）"),0,4);editor.Controls.Add(pets,0,5);
            layout.Controls.Add(editor,1,1);
            var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(0,10,0,0),WrapContents=true};
            var applyButton=ActionButton("保存并更新右键菜单",Apply);applyButton.Name="ApplyQuickCommands";
            footer.Controls.Add(desktop);footer.Controls.Add(folders);footer.Controls.Add(mainMenu);footer.SetFlowBreak(mainMenu,true);
            footer.Controls.Add(applyButton);footer.Controls.Add(ActionButton("预览菜单",PreviewMenu));footer.Controls.Add(ActionButton("移除右键菜单",Unregister));
            footer.SetFlowBreak(footer.Controls[footer.Controls.Count-1],true);footer.Controls.Add(registrationStatus);
            layout.Controls.Add(footer,0,2);layout.SetColumnSpan(footer,2);Controls.Add(layout);
            list.SelectedIndexChanged+=delegate{if(loading)return;Store();selected=list.SelectedItem as QuickTaskCommand;LoadSelected();};
            Reload(options.Commands.Count>0?options.Commands[0]:null);if(options.Commands.Count==0)Add();
            UiLanguageBindings.Apply(this);
            registrationStatus.Text=UiLanguage.Text("保存命令后会注册并验证所选菜单模式。");
            registrationStatus.Click+=delegate{if(registrationError.Length>0)MessageBox.Show(this,registrationError,UiLanguage.Text("菜单注册失败："),MessageBoxButtons.OK,MessageBoxIcon.Warning);};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(applying)e.Cancel=true;};
        }
        private bool HasPet(string id){foreach(QuickTaskBinding pet in pets.Items)if(pet.Id==id)return true;return false;}
        private static Label Caption(string text){return new Label {Text=UiLanguage.Text(text),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};}
        private Button ActionButton(string text,Action action)
        {
            var button=new Button {Text=UiLanguage.Text(text),AutoSize=true,Height=32};
            button.Click+=delegate{try{action();}catch(Exception error){MessageBox.Show(this,UiLanguage.Text(error.Message),Program.ProductName,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};return button;
        }
        private void Store()
        {
            if(selected==null)return;selected.Name=name.Text.Trim();selected.Task=task.Text;
            selected.Companions.Clear();foreach(QuickTaskBinding pet in pets.CheckedItems)selected.Companions.Add(new QuickTaskBinding {Id=pet.Id,Name=pet.Name,IconPath=pet.IconPath,TeamId=pet.TeamId});
        }
        private void LoadSelected()
        {
            name.Text=selected==null?"":selected.Name;task.Text=selected==null?"":selected.Task;
            for(int i=0;i<pets.Items.Count;i++)
            {
                var pet=(QuickTaskBinding)pets.Items[i];var binding=selected==null?null:selected.Companions.Find(item=>item.Id==pet.Id);
                if(binding!=null&&!String.IsNullOrEmpty(binding.IconPath))pet.IconPath=binding.IconPath;
                pets.SetItemChecked(i,binding!=null);
            }
            name.Enabled=task.Enabled=pets.Enabled=selected!=null;
        }
        private void Reload(QuickTaskCommand choose)
        {
            loading=true;list.DataSource=null;list.DataSource=new List<QuickTaskCommand>(options.Commands);
            list.SelectedItem=choose;selected=choose;loading=false;LoadSelected();
        }
        private void Add()
        {
            Store();if(options.Commands.Count>=20)throw new ArgumentException(UiLanguage.Text("最多设置 20 条快捷命令。"));
            var command=new QuickTaskCommand{Name=UiLanguage.Text("指派任务")};
            if(pets.Items.Count>0){var pet=(QuickTaskBinding)pets.Items[0];command.Companions.Add(new QuickTaskBinding{Id=pet.Id,Name=pet.Name});}
            options.Commands.Add(command);Reload(command);name.Focus();
        }
        private void Remove(){if(selected==null)return;options.Commands.Remove(selected);Reload(options.Commands.Count>0?options.Commands[0]:null);}
        private void Apply()
        {
            Store();options.Enabled=true;options.Desktop=desktop.Checked;options.Folders=folders.Checked;options.MainMenu=mainMenu.Checked;QuickTaskCommands.Validate(options);
            if(options.MainMenu)QuickTaskModern.ValidateBindings(options);
            bool trust=options.MainMenu&&QuickTaskModern.NeedsPreviewTrust;
            if(options.MainMenu&&!QuickTaskModern.PackageAvailable)throw new InvalidOperationException(UiLanguage.Text("缺少原生菜单扩展，请安装包含 shell 组件的新版安装包。"));
            if(trust&&MessageBox.Show(this,UiLanguage.Text("Windows 11 主菜单需要安装 agent-pal 签名扩展。此预览使用专用测试证书，将仅加入本机“受信任的人”（不加入根证书），需要管理员授权。是否继续？"),Program.ProductName,MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            var icons=new Dictionary<string,string>();
            foreach(QuickTaskCommand command in options.Commands)foreach(QuickTaskBinding pet in command.Companions)
            {string icon;if(!icons.TryGetValue(pet.Id,out icon)){icon=CreateIcon(pet.Id);icons[pet.Id]=icon;}if(icon.Length>0)pet.IconPath=icon;}
            applying=true;UseWaitCursor=true;foreach(Control control in Controls)control.Enabled=false;
            registrationStatus.Text=UiLanguage.Text("正在注册并校验右键菜单…");
            var worker=new System.Threading.Thread(delegate()
            {
                Exception failure=null;
                QuickTaskOptions previous=null;string previousNative=null,previousClassic=null;bool saved=false;
                try
                {
                    string executable=typeof(Program).Assembly.Location;
                    previous=QuickTaskCommands.Load();
                    previousNative=QuickTaskModern.RegisteredExecutable(Registry.CurrentUser,QuickTaskModern.RegistryPath);
                    previousClassic=QuickTaskShell.RegisteredExecutable(Registry.CurrentUser,@"Software\Classes");
                    if(options.MainMenu)QuickTaskModern.Register(trust);
                    QuickTaskCommands.Save(options);saved=true;
                    if(options.MainMenu)
                    {
                        QuickTaskModern.ApplyMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath,options,executable);
                        QuickTaskNativeProbe.Verify(options);
                        QuickTaskShell.Remove(Registry.CurrentUser,@"Software\Classes");
                    }
                    else
                    {
                        QuickTaskShell.Apply(Registry.CurrentUser,@"Software\Classes",options,executable);
                        QuickTaskModern.RemoveMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath);
                    }
                    SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
                }
                catch(Exception error)
                {
                    failure=error;
                    if(saved&&previous!=null)try
                    {
                        QuickTaskCommands.Save(previous);
                        if(!String.IsNullOrEmpty(previousNative))QuickTaskModern.ApplyMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath,previous,previousNative);
                        else QuickTaskModern.RemoveMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath);
                        if(!String.IsNullOrEmpty(previousClassic)&&File.Exists(previousClassic))QuickTaskShell.Apply(Registry.CurrentUser,@"Software\Classes",previous,previousClassic);
                        else QuickTaskShell.Remove(Registry.CurrentUser,@"Software\Classes");
                    }
                    catch(Exception restore){failure=new AggregateException(error.Message+Environment.NewLine+restore.Message,error,restore);}
                }
                BeginInvoke((Action)delegate
                {
                    applying=false;UseWaitCursor=false;foreach(Control control in Controls)control.Enabled=true;mainMenu.Enabled=QuickTaskModern.Supported;
                    registrationStatus.ForeColor=failure==null?Color.FromArgb(29,113,85):Color.FromArgb(177,67,48);
                    registrationError=failure==null?"":failure.GetBaseException().Message;
                    registrationStatus.Cursor=failure==null?Cursors.Default:Cursors.Hand;
                    registrationStatus.Text=failure==null?UiLanguage.Text(options.MainMenu?"主菜单扩展已注册，入口会直接显示在 Windows 11 菜单中。":"兼容菜单已注册。Windows 11 中位于“显示更多选项”。"):UiLanguage.Text("菜单注册失败。点击查看详细原因。");
                    Reload(selected);
                });
            });
            worker.SetApartmentState(System.Threading.ApartmentState.STA);worker.Start();
        }
        private void Unregister()
        {
            QuickTaskShell.Remove(Registry.CurrentUser,@"Software\Classes");options.Enabled=false;QuickTaskCommands.Save(options);
            QuickTaskModern.RemoveMappings(Registry.CurrentUser,QuickTaskModern.RegistryPath);
            registrationStatus.Text=UiLanguage.Text("已移除右键菜单，命令定义已保留。");
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }
        private void PreviewMenu()
        {
            Store();QuickTaskCommands.Validate(options);
            DisposePreview();
            var menu=new ContextMenuStrip {ImageScalingSize=new Size(24,24)};
            preview=menu;
            var entries=new List<ToolStripMenuItem>();
            foreach(QuickTaskCommand command in options.Commands)foreach(QuickTaskBinding pet in command.Companions)
            {
                var item=new ToolStripMenuItem(QuickTaskShell.Caption(command,pet).Replace("&","&&"));
                string path=CreateIcon(pet.Id);if(path.Length==0)path=pet.IconPath;
                item.Image=QuickTaskIcons.ReadThumbnail(path);
                item.Click+=delegate{MessageBox.Show(this,UiLanguage.Text("这是菜单预览。请在目标文件夹右键来指派任务。"),Program.ProductName);};
                entries.Add(item);
            }
            if(mainMenu.Checked&&entries.Count>0)
            {
                var root=new ToolStripMenuItem(UiLanguage.Text("发送Agent Pal任务")){Image=AppIcon.WindowIcon.ToBitmap()};
                if(entries.Count==1){if(entries[0].Image!=null)entries[0].Image.Dispose();entries[0].Dispose();root.Click+=delegate{MessageBox.Show(this,UiLanguage.Text("这是菜单预览。请在目标文件夹右键来指派任务。"),Program.ProductName);};}
                else foreach(var item in entries)root.DropDownItems.Add(item);
                menu.Items.Add(root);
            }
            else foreach(var item in entries)menu.Items.Add(item);
            menu.Show(this,new Point(22,120));
        }
        private void DisposePreview()
        {
            if(preview==null)return;
            if(!preview.IsDisposed){preview.Close();DisposeImages(preview.Items);preview.Dispose();}
            preview=null;
        }
        private static void DisposeImages(ToolStripItemCollection items){foreach(ToolStripItem item in items){if(item.Image!=null)item.Image.Dispose();var menu=item as ToolStripMenuItem;if(menu!=null)DisposeImages(menu.DropDownItems);}}
        protected override void Dispose(bool disposing){if(disposing)DisposePreview();base.Dispose(disposing);}
        internal static string CreateIcon(string characterId,string assetsRoot=null)
        {
            if(characterId.StartsWith("team-",StringComparison.Ordinal))
            {
                string product=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"agent-pal-brand-v2.ico");
                if(File.Exists(product))return product;
                string icons=Path.Combine(Program.StateDirectory,"shell-icons");Directory.CreateDirectory(icons);
                string brand=Path.Combine(icons,"agent-pal-team.ico");using(var file=File.Create(brand))AppIcon.WindowIcon.Save(file);return brand;
            }
            PetCharacter character=null;foreach(PetCharacter candidate in PetCharacter.Available)if(candidate.Id==characterId){character=candidate;break;}
            if(character==null)return "";
            string root=character.ResourceRoot;
            if(String.IsNullOrEmpty(root))root=Path.Combine(assetsRoot??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),characterId=="orbit"?"":Path.Combine("characters",characterId));
            string source=Path.Combine(root,"idle","00.png");
            if(!File.Exists(source))return "";
            string folder=Path.Combine(Program.StateDirectory,"shell-icons");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,characterId+".ico");
            using(Image image=Image.FromFile(source))QuickTaskIcons.Write(image,path);
            return path;
        }
    }
}
