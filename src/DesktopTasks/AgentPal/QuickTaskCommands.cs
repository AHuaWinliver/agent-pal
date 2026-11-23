// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AgentPal
{
    internal sealed class QuickTaskBinding
    {
        public string Id="",Name="",IconPath="",TeamId="";
        internal bool IsTeam {get{return !String.IsNullOrEmpty(TeamId);}}
        public override string ToString(){return IsTeam?UiLanguage.Format("协作团队 · {0}",Name):UiLanguage.Text(Name);}
    }
    internal sealed class QuickTaskCommand
    {
        public string Id=Guid.NewGuid().ToString("N"),Name="",Task="";
        public List<QuickTaskBinding> Companions=new List<QuickTaskBinding>();
        public override string ToString(){return Name;}
    }
    internal sealed class QuickTaskOptions
    {
        public bool Enabled,Desktop=true,Folders=true;
        public bool MainMenu=true;
        public List<QuickTaskCommand> Commands=new List<QuickTaskCommand>();
    }
    internal static class QuickTaskCommands
    {
        internal static string FilePath {get{return Path.Combine(Program.StateDirectory,"quick-commands.json");}}
        internal static QuickTaskOptions Load(){return WorkshopFiles.Load<QuickTaskOptions>(FilePath);}
        internal static void Save(QuickTaskOptions options){Validate(options);WorkshopFiles.Save(FilePath,options);}
        internal static bool ValidId(string value){return Regex.IsMatch(value??"",@"\A[a-zA-Z0-9-]{1,80}\z");}
        internal static void Validate(QuickTaskOptions options)
        {
            if(options==null||options.Commands==null||options.Commands.Count>20)throw new ArgumentException(UiLanguage.Text("最多设置 20 条快捷命令。"));
            var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(QuickTaskCommand command in options.Commands)
            {
                if(command==null||!ValidId(command.Id)||!ids.Add(command.Id)||String.IsNullOrWhiteSpace(command.Name)||command.Name.Length>60||
                    (command.Task??"").Length>8000||command.Companions==null||command.Companions.Count<1||command.Companions.Count>24)
                    throw new ArgumentException(UiLanguage.Text("每条快捷命令需要名称和至少一位伙伴，预填任务最多 8000 字。"));
                var pets=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if(command.Companions.Exists(item=>item!=null&&item.IsTeam)&&(command.Task??"").Length>4000)
                    throw new ArgumentException(UiLanguage.Text("绑定协作团队的快捷命令，预填任务最多 4000 字。"));
                foreach(QuickTaskBinding pet in command.Companions)
                    if(pet==null||!ValidId(pet.Id)||!pets.Add(pet.Id)||String.IsNullOrWhiteSpace(pet.Name)||pet.Name.Length>80||
                        (pet.IsTeam&&(!ValidId(pet.TeamId)||pet.Id!="team-"+pet.TeamId)))
                        throw new ArgumentException(UiLanguage.Text("快捷命令中的伙伴绑定无效。"));
            }
            if(options.Enabled&&(!options.Desktop&&!options.Folders||options.Commands.Count==0))
                throw new ArgumentException(UiLanguage.Text("请选择右键菜单位置并添加快捷命令。"));
        }
        internal static string ResolveDirectory(string path,bool desktop)
        {
            string value=desktop?Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory):path;
            if(String.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value))throw new ArgumentException(UiLanguage.Text("当前工作目录无效。"));
            string directory=Path.GetFullPath(value);
            if(!Directory.Exists(directory))throw new DirectoryNotFoundException(directory);
            return directory;
        }
    }
    // Only per-user verbs carrying this module's ownership marker are managed.
    internal static class QuickTaskShell
    {
        internal const string Prefix="AgentPal.QuickTask.";
        internal const string Owner="agent-pal-quick-task-v1";
        internal static readonly string[] Locations={@"Directory\shell",@"Directory\Background\shell",@"DesktopBackground\Shell"};
        internal static string Caption(QuickTaskCommand command,QuickTaskBinding pet)
        {
            string caption=pet.IsTeam?UiLanguage.Format("在当前路径交给团队 {0}",pet.Name):UiLanguage.Format("在当前路径给{0}指派任务",UiLanguage.Text(pet.Name));
            return command.Name=="指派任务"||command.Name=="Assign task"?caption:caption+" · "+command.Name;
        }
        internal static string Arguments(string executable,QuickTaskCommand command,QuickTaskBinding pet,string location)
        {
            string argument=location==Locations[2]?" --desktop":location==Locations[0]?" --directory \"%1\\.\"":" --directory \"%V\\.\"";
            return "\""+executable+"\" --quick-task "+command.Id+" --companion "+pet.Id+argument;
        }
        internal static void Apply(RegistryKey root,string classes,QuickTaskOptions options,string executable)
        {
            QuickTaskCommands.Validate(options);
            if(!Path.IsPathRooted(executable)||!File.Exists(executable))throw new FileNotFoundException(executable);
            // Inspect all owned-name collisions before changing any location.
            foreach(string location in Locations)
            using(RegistryKey shell=root.OpenSubKey(classes+"\\"+location))
                if(shell!=null)foreach(string name in shell.GetSubKeyNames())if(name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase))
                    using(RegistryKey key=shell.OpenSubKey(name))
                        if(Convert.ToString(key.GetValue("AgentPalOwner"))!=Owner)throw new IOException("A context-menu key is owned by another application: "+name);
            Remove(root,classes);
            if(!options.Enabled)return;
            foreach(string location in Locations)
            {
                if(location==Locations[2]?!options.Desktop:!options.Folders)continue;
                foreach(QuickTaskCommand command in options.Commands)foreach(QuickTaskBinding pet in command.Companions)
                using(RegistryKey key=root.CreateSubKey(classes+"\\"+location+"\\"+Prefix+command.Id+"."+pet.Id))
                {
                    key.SetValue("AgentPalOwner",Owner);
                    string caption=Caption(command,pet);
                    key.SetValue("MUIVerb",caption.Replace("&","&&"));
                    key.SetValue("Icon","\""+(File.Exists(pet.IconPath)?pet.IconPath:executable)+"\"");
                    key.SetValue("MultiSelectModel","Single");
                    using(RegistryKey invoke=key.CreateSubKey("command"))invoke.SetValue("",Arguments(executable,command,pet,location));
                }
            }
        }
        internal static void Remove(RegistryKey root,string classes,string expectedExecutable=null)
        {
            foreach(string location in Locations)
            using(RegistryKey shell=root.OpenSubKey(classes+"\\"+location,true))
                if(shell!=null)foreach(string name in shell.GetSubKeyNames())if(name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase))
                {
                    bool owned;using(RegistryKey key=shell.OpenSubKey(name))
                    {
                        owned=Convert.ToString(key.GetValue("AgentPalOwner"))==Owner;
                        if(owned&&expectedExecutable!=null)using(RegistryKey command=key.OpenSubKey("command"))
                            owned=command!=null&&Convert.ToString(command.GetValue("")).StartsWith("\""+expectedExecutable+"\" ",StringComparison.OrdinalIgnoreCase);
                    }
                    if(owned)shell.DeleteSubKeyTree(name,false);
                }
        }
        internal static string RegisteredExecutable(RegistryKey root,string classes)
        {
            foreach(string location in Locations)
            using(RegistryKey shell=root.OpenSubKey(classes+"\\"+location))
                if(shell!=null)foreach(string name in shell.GetSubKeyNames())if(name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase))
                using(RegistryKey key=shell.OpenSubKey(name))
                    if(Convert.ToString(key.GetValue("AgentPalOwner"))==Owner)
                    using(RegistryKey command=key.OpenSubKey("command"))
                    {
                        string value=command==null?"":Convert.ToString(command.GetValue(""));
                        int end=value.StartsWith("\"",StringComparison.Ordinal)?value.IndexOf('"',1):-1;
                        if(end>1)return value.Substring(1,end-1);
                    }
            return null;
        }
    }
}
