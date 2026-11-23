// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using AgentPal;
internal static class QuickTaskTests
{
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")]private static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("shell32.dll")]private static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr appId);
    private static void CheckTaskbar(Form dialog,QuickTaskBinding binding)
    {
        string group=QuickTaskTaskbar.Read(dialog.Handle,5);
        Check(group==QuickTaskTaskbar.Identity(binding.Id),"Shell window property store has the companion group identity: "+binding.Id+" (actual="+(group??"empty")+")");
        Check(QuickTaskTaskbar.Read(dialog.Handle,3)==binding.IconPath+",0","Shell taskbar group explicitly resolves the companion ICO file: "+binding.Id);
        Check(QuickTaskTaskbar.Read(dialog.Handle,4)==dialog.Text,"Shell taskbar group has the localized task editor title: "+binding.Id);
        string relaunch=QuickTaskTaskbar.Read(dialog.Handle,2);
        Check(relaunch!=null&&relaunch.Length<260&&relaunch.Contains(" --reopen-quick-task "),"Shell relaunch fits its length limit even with a long working folder");
        var reopened=QuickTaskLaunch.ExpandReopen(new[]{"--reopen-quick-task",relaunch.Substring(relaunch.LastIndexOf(' ')+1)});
        Check(reopened[3]==binding.Id&&reopened[5]==dialog.Controls.Find("QuickTaskDirectory",true)[0].Text,"taskbar relaunch preserves the exact companion and working folder");
        Check(dialog.ShowInTaskbar&&dialog.ShowIcon&&(GetWindowLong(dialog.Handle,-20)&0x40000)!=0&&(GetWindowLong(dialog.Handle,-20)&0x80)==0,"assignment editor has a native taskbar button: "+binding.Id);
        IntPtr handle=SendMessage(dialog.Handle,0x7f,new IntPtr(1),IntPtr.Zero);
        Check(handle!=IntPtr.Zero,"Windows receives a nonempty taskbar icon: "+binding.Id);
        using(var borrowed=Icon.FromHandle(handle))
        using(var actual=borrowed.ToBitmap())
        using(var portrait=QuickTaskIcons.ReadThumbnail(binding.IconPath))
        using(var expectedIcon=QuickTaskIcons.CreateWindowIcon(portrait))
        using(var expected=expectedIcon.ToBitmap())
        {
            bool same=actual.Size==expected.Size;
            for(int y=0;same&&y<actual.Height;y++)for(int x=0;x<actual.Width;x++)if(actual.GetPixel(x,y).ToArgb()!=expected.GetPixel(x,y).ToArgb()){same=false;break;}
            Check(same,"native taskbar icon matches the bound companion's portrait: "+binding.Id);
        }
    }
    private static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    [STAThread]private static int Main(string[] args)
    {
        string registry="Software\\agent-pal-tests\\quick-"+Guid.NewGuid().ToString("N");
        try
        {
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")!="1")throw new Exception("Isolated state required.");
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();UiLanguage.Set("en");
            string root=Program.StateDirectory;Directory.CreateDirectory(root);
            string directory=Path.Combine(root,"中文 & project's folder");Directory.CreateDirectory(directory);
            var options=new QuickTaskOptions {Enabled=true};var command=new QuickTaskCommand{Name="Review code",Task=""};
            command.Companions.Add(new QuickTaskBinding{Id="orbit",Name="Qingheng",IconPath=QuickCommandsForm.CreateIcon("orbit",args[1])});
            command.Companions.Add(new QuickTaskBinding{Id="spark",Name="Feijian",IconPath=QuickCommandsForm.CreateIcon("spark",args[1])});
            options.Commands.Add(command);QuickTaskCommands.Save(options);options=QuickTaskCommands.Load();command=options.Commands[0];
            Check(command.Companions.Count==2&&File.Exists(command.Companions[0].IconPath),"custom command and multiple companion thumbnails persist");
            using(var reader=new BinaryReader(File.OpenRead(command.Companions[0].IconPath)))
            {reader.ReadUInt16();Check(reader.ReadUInt16()==1&&reader.ReadUInt16()==6,"shell icon contains six full-color resolutions");reader.ReadBytes(6);Check(reader.ReadUInt16()==32,"shell icon retains 32-bit color rather than GDI palette reduction");}
            using(Image original=Image.FromFile(Path.Combine(args[1],"idle","00.png")))
            using(Bitmap taskbar=AppIcon.CreateBitmap(original,128))
            using(Bitmap thumbnail=(Bitmap)QuickTaskIcons.ReadThumbnail(command.Companions[0].IconPath))
            {
                bool same=thumbnail.Width==128;
                for(int y=0;y<128;y+=13)for(int x=0;x<128;x+=13)same&=thumbnail.GetPixel(x,y).ToArgb()==taskbar.GetPixel(x,y).ToArgb();
                Check(same&&thumbnail.GetPixel(0,0).A==255,"Q-style companion thumbnail retains its opaque portrait badge independently of the product icon");
            }
            QuickTaskShell.Apply(Registry.CurrentUser,registry,options,args[0]);
            foreach(string location in QuickTaskShell.Locations)
            using(RegistryKey shell=Registry.CurrentUser.OpenSubKey(registry+"\\"+location))
            {
                Check(shell.GetSubKeyNames().Length==2,"one direct item per bound pet in "+location);
                using(RegistryKey item=shell.OpenSubKey(QuickTaskShell.Prefix+command.Id+".orbit"))
                using(RegistryKey invoke=item.OpenSubKey("command"))
                {
                    string line=(string)invoke.GetValue("");
                    Check(((string)item.GetValue("MUIVerb")).Contains("Assign a task to Qingheng here")&&((string)item.GetValue("Icon")).Contains("orbit.ico")&&
                        line.Contains("--companion orbit")&&!line.Contains(command.Task+"unsafe"),"menu carries companion thumbnail and ID without embedding task text");
                    Check(location==QuickTaskShell.Locations[2]?line.EndsWith("--desktop"):line.Contains("\\.\""),"desktop or folder path passes safely even at a drive root");
                }
            }
            using(RegistryKey foreign=Registry.CurrentUser.CreateSubKey(registry+"\\"+QuickTaskShell.Locations[0]+"\\OtherApplication"))foreign.SetValue("keep",1);
            QuickTaskShell.Remove(Registry.CurrentUser,registry);
            using(RegistryKey shell=Registry.CurrentUser.OpenSubKey(registry+"\\"+QuickTaskShell.Locations[0]))Check(shell.GetSubKeyNames().SequenceEqual(new[]{"OtherApplication"}),"removal preserves unrelated application verbs");
            string request="检查当前目录，保留中文 🌱、引号 \"及反引号 `，不要执行 $() & | %PATH% ! ^ > <。\n第二行";
            foreach(string id in new[]{"orbit","spark"})
            {
                string executable=id=="orbit"?args[0]:Path.Combine(root,"claude shim.cmd");
                if(id=="spark")File.WriteAllText(executable,"@echo off\r\n\""+args[0]+"\" %*\r\n",Encoding.ASCII);
                string config=Path.Combine(root,id+" settings.json");File.WriteAllText(config,"{\"model\":\"isolated-test\"}");
                var profile=IntegrationPreferences.Load();profile.SetExecutablePath(id,executable);profile.SetSettingsPath(id,config);profile.Save();
                string capture=Path.Combine(root,id+"-capture.json");Environment.SetEnvironmentVariable("AGENT_PAL_TEST_QUICK_CAPTURE",capture);
                ProcessStartInfo info=QuickTaskLaunch.BuildStartInfo(directory,id,request);
                string encoded=info.Arguments.Substring(info.Arguments.IndexOf("-EncodedCommand ")+16);
                string script=Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
                Check(script.Contains(executable.Replace("'","''"))&&script.Contains("Set-Location -LiteralPath")&&script.Contains(config.Replace("'","''"))&&!script.Contains(request),"launch resolves the selected pet's executable, exact folder and settings");
                info.Arguments=info.Arguments.Replace("-NoExit ","");info.UseShellExecute=false;info.CreateNoWindow=true;info.WindowStyle=ProcessWindowStyle.Hidden;
                info.RedirectStandardInput=true;info.RedirectStandardOutput=true;info.RedirectStandardError=true;
                using(Process child=Process.Start(info)){child.StandardInput.Close();if(!child.WaitForExit(20000)){child.Kill();throw new Exception("Test CLI host timeout.");}string error=child.StandardError.ReadToEnd();if(child.ExitCode!=0)throw new Exception("Native test host exit "+child.ExitCode+": "+error+"\n"+child.StandardOutput.ReadToEnd());Check(child.ExitCode==0,"native CLI startup completes for "+id);}
                var json=new JavaScriptSerializer();var actual=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(capture));
                Check((string)actual["Directory"]==directory,"native CLI receives the actual Explorer folder");
                var payload=json.Deserialize<Dictionary<string,object>>(File.ReadAllText((string)actual["Request"]));
                Check((string)payload["Task"]==request&&(string)payload["Companion"]==id,"multiline Unicode and shell metacharacters arrive intact without command injection");
                var arguments=(System.Collections.IEnumerable)actual["Arguments"];var received=new List<string>();foreach(object argument in arguments)received.Add(Convert.ToString(argument));
                Check(received.Contains("--settings")&&received.Contains(config),"native CLI receives the matching settings argument");
            }
            bool invalid=false;try{QuickTaskCommands.ResolveDirectory(Path.Combine(root,"missing"),false);}catch(DirectoryNotFoundException){invalid=true;}Check(invalid,"missing Explorer folder never silently falls back to another directory");
            string submitted=null;
            var taskbarBindings=new List<QuickTaskBinding>(command.Companions);
            foreach(var character in PetCharacter.Available)
                if(!taskbarBindings.Exists(item=>item.Id==character.Id))
                    taskbarBindings.Add(new QuickTaskBinding{Id=character.Id,Name=character.Name,IconPath=QuickCommandsForm.CreateIcon(character.Id,args[1])});
            foreach(var binding in taskbarBindings)
            {
                QuickTaskWindow.SetCompanionIdentity(binding.Id);
                IntPtr appId;
                Check(GetCurrentProcessExplicitAppUserModelID(out appId)==0,"independent task editor has an explicit taskbar identity");
                try{Check(Marshal.PtrToStringUni(appId)==QuickTaskTaskbar.Identity(binding.Id),"taskbar grouping is companion-specific: "+binding.Id);}
                finally{Marshal.FreeCoTaskMem(appId);}
                using(var modal=new QuickTaskRequestForm(command,binding,directory,value=>{}))
                {
                    modal.Shown+=delegate{CheckTaskbar(modal,binding);modal.BeginInvoke(new Action(()=>modal.Close()));};
                    modal.ShowDialog();
                }
            }
            using(var dialog=new QuickTaskRequestForm(command,command.Companions[0],directory,value=>submitted=value))
            {
                dialog.Show();Application.DoEvents();Check(!dialog.Confirm.Enabled&&submitted==null,"opening a shortcut does not run Claude and requires a task");
                Check(QuickTaskWindow.IsEditor(dialog.Handle),"independent assignment window is recognizable for pausing pet movement during input");
                dialog.TaskInput.Text=request;Application.DoEvents();
                using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,dialog.Width,dialog.Height));image.Save(Path.Combine(root,"quick-task-request.png"));}
                dialog.Confirm.PerformClick();Check(submitted==request,"Confirm submits the edited task exactly once");
            }
            using(var editor=new QuickCommandsForm())
            {
                editor.Show();Application.DoEvents();
                using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(0,0,editor.Width,editor.Height));image.Save(Path.Combine(root,"quick-task-settings.png"));}
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                typeof(QuickCommandsForm).GetMethod("PreviewMenu",flags).Invoke(editor,null);Application.DoEvents();
                var menu=(ContextMenuStrip)typeof(QuickCommandsForm).GetField("preview",flags).GetValue(editor);
                bool native=((CheckBox)typeof(QuickCommandsForm).GetField("mainMenu",flags).GetValue(editor)).Checked;
                if(native){var entry=(ToolStripMenuItem)menu.Items[0];Check(menu.Items.Count==1&&entry.Image!=null&&entry.DropDownItems.Count==2&&entry.DropDownItems[0].Image!=null&&entry.DropDownItems[1].Image!=null,"native preview has the fixed product icon and groups multiple companion entries");}
                else Check(menu.Items.Count==2&&menu.Items[0].Image!=null&&menu.Items[1].Image!=null,"compatibility preview retains direct companion entries");
                using(var image=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(image,new Rectangle(0,0,menu.Width,menu.Height));image.Save(Path.Combine(root,"quick-task-menu-preview.png"));}
                if(native)
                {
                    menu.Close();var picker=(CheckedListBox)typeof(QuickCommandsForm).GetField("pets",flags).GetValue(editor);
                    for(int index=0;index<picker.Items.Count;index++)picker.SetItemChecked(index,index==0);
                    typeof(QuickCommandsForm).GetMethod("PreviewMenu",flags).Invoke(editor,null);Application.DoEvents();
                    var single=(ContextMenuStrip)typeof(QuickCommandsForm).GetField("preview",flags).GetValue(editor);var entry=(ToolStripMenuItem)single.Items[0];
                    Check(single.Items.Count==1&&entry.DropDownItems.Count==0&&entry.Image!=null,"one native binding previews a direct product entry without a flyout");
                }
                menu.Close();editor.Close();
            }
            Check(UiMotion.OpacityAt(.35,0)<.35&&UiMotion.OpacityAt(.35,1)==.35&&UiMotion.Ease(.4)<UiMotion.Ease(.6),"entry easing preserves the intended docked opacity and progresses monotonically");
            Console.WriteLine("All quick-task checks passed using isolated registry keys and fake CLI executables.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{Registry.CurrentUser.DeleteSubKeyTree(registry,false);}
    }
}
