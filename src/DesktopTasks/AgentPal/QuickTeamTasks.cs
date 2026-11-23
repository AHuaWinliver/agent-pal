// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace AgentPal
{
    // Explorer editors run separately. Only the singleton desktop process owns
    // team execution and workshop history; requests never start a second runner.
    internal static class QuickTeamTasks
    {
        internal sealed class Request
        {
            public string Command="",Binding="",Team="",Directory="",Task="";
            public DateTime ExpiresUtc;
        }
        internal sealed class Result { public bool Success; public string Error=""; }
        internal static string QueueDirectory {get{return Path.Combine(Program.StateDirectory,"team-quick-requests");}}
        internal static string WorkshopPath {get{return Path.Combine(Program.StateDirectory,"workshop.json");}}
        internal static string TeamKey(CompanionSect team)
        {
            if(!String.IsNullOrEmpty(team.Id))return team.Id;
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(team.Name??"")),0,16).Replace("-","").ToLowerInvariant();
        }
        internal static QuickTaskBinding Binding(CompanionSect team)
        {string key=TeamKey(team);return new QuickTaskBinding {Id="team-"+key,TeamId=key,Name=team.Name};}
        internal static CompanionSect Prepare(WorkshopData data,QuickTaskBinding binding,string directory,string task)
        {
            if(!binding.IsTeam||!QuickTaskCommands.ValidId(binding.TeamId))throw new ArgumentException(UiLanguage.Text("协作团队绑定无效。"));
            if(String.IsNullOrWhiteSpace(task)||task.Length>4000)throw new ArgumentException(UiLanguage.Text("团队任务请输入 1–4000 字。"));
            var matches=data.Sects.FindAll(delegate(CompanionSect team){return TeamKey(team)==binding.TeamId;});
            if(matches.Count!=1)throw new InvalidOperationException(UiLanguage.Text("此协作团队已删除或标识冲突，请重新绑定快捷命令。"));
            string cwd=QuickTaskCommands.ResolveDirectory(directory,false);
            var json=new JavaScriptSerializer {MaxJsonLength=8388608};
            CompanionSect copy=json.Deserialize<CompanionSect>(json.Serialize(matches[0]));
            copy.Id=TeamKey(matches[0]);copy.Goal=task;
            if(copy.Members!=null)foreach(var member in copy.Members)if(member!=null)member.Directory=cwd;
            SectExecution.Validate(copy);
            return copy;
        }
        internal static void Submit(QuickTaskCommand command,QuickTaskBinding binding,string directory,string task,Action wake=null)
        {
            // Validate before writing a request. The owner repeats validation
            // against its current saved definitions before accepting execution.
            Prepare(WorkshopFiles.Load<WorkshopData>(WorkshopPath),binding,directory,task);
            string token=Guid.NewGuid().ToString("N"),file=Path.Combine(QueueDirectory,token+".pending.json"),ack=Path.Combine(QueueDirectory,token+".result.json");
            WorkshopFiles.Save(file,new Request {Command=command.Id,Binding=binding.Id,Team=binding.TeamId,
                Directory=directory,Task=task,ExpiresUtc=DateTime.UtcNow.AddSeconds(20)});
            try
            {
                if(wake!=null)wake();
                else using(Process child=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location){UseShellExecute=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory})){}
                DateTime until=DateTime.UtcNow.AddSeconds(20);
                while(DateTime.UtcNow<until)
                {
                    if(File.Exists(ack))
                    {
                        Result result=WorkshopFiles.Load<Result>(ack);
                        if(!result.Success)throw new InvalidOperationException(result.Error);
                        return;
                    }
                    Thread.Sleep(50);
                }
                throw new TimeoutException(UiLanguage.Text("尚未收到团队启动确认，请先查看 Agent 执行记录，避免重复提交。"));
            }
            finally
            {
                // A claimed request belongs to the owner. Never remove it from
                // under execution or retry it after an ambiguous timeout.
                if(File.Exists(file))File.Delete(file);
                if(File.Exists(ack))File.Delete(ack);
            }
        }
        internal static void ProcessPending(WorkshopData data,Action<CompanionSect> start)
        {
            if(!System.IO.Directory.Exists(QueueDirectory))return;
            int count=0;
            foreach(string file in System.IO.Directory.GetFiles(QueueDirectory,"*.pending.json"))
            {
                if(++count>8)break;
                string token=Path.GetFileName(file).Replace(".pending.json","");
                if(!System.Text.RegularExpressions.Regex.IsMatch(token,"\\A[0-9a-f]{32}\\z"))continue;
                string claimed=Path.Combine(QueueDirectory,token+".processing.json"),ack=Path.Combine(QueueDirectory,token+".result.json");
                try{File.Move(file,claimed);}catch(IOException){continue;}
                var result=new Result();
                try
                {
                    if(new FileInfo(claimed).Length>32768)throw new InvalidDataException(UiLanguage.Text("团队快捷请求无效。"));
                    Request request=WorkshopFiles.Load<Request>(claimed);
                    if(request.ExpiresUtc<DateTime.UtcNow||request.ExpiresUtc>DateTime.UtcNow.AddMinutes(1))throw new InvalidOperationException(UiLanguage.Text("团队快捷请求已过期，请重新提交。"));
                    QuickTaskOptions options=QuickTaskCommands.Load();QuickTaskCommands.Validate(options);
                    var command=options.Commands.Find(item=>item.Id==request.Command);
                    var binding=command==null?null:command.Companions.Find(item=>item.Id==request.Binding);
                    if(!options.Enabled||binding==null||!binding.IsTeam||binding.TeamId!=request.Team)
                        throw new InvalidOperationException(UiLanguage.Text("此快捷命令或团队绑定已移除，请刷新右键菜单。"));
                    CompanionSect team=Prepare(data,binding,request.Directory,request.Task);
                    start(team);result.Success=true;
                }
                catch(Exception error){result.Error=UiLanguage.Text(error.Message);}
                try{WorkshopFiles.Save(ack,result);}finally{File.Delete(claimed);}
            }
        }
    }
}
