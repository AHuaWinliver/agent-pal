// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using AgentPal;

internal static class QuickTeamTaskTests
{
    sealed class Fake:ISectAgent
    {
        public event Action<string> Output;public event Action<int> Completed;
        internal string Prompt,Directory,Character,Marker;
        public void Start(string p,string d,string m,string c){Prompt=p;Directory=d;Character=c;Marker=m;}
        internal void Finish(){Output("Done.\nAGENT_PAL_RESULT: success");Completed(0);}
        public void Stop(){}public void Dispose(){}
    }
    static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Rejected(Action action,string label){try{action();}catch(Exception){Console.WriteLine("PASS "+label);return;}throw new Exception(label);}
    static string Enqueue(QuickTaskCommand command,QuickTaskBinding binding,string directory,DateTime expiry)
    {
        string token=Guid.NewGuid().ToString("N");WorkshopFiles.Save(Path.Combine(QuickTeamTasks.QueueDirectory,token+".pending.json"),
            new QuickTeamTasks.Request{Command=command.Id,Binding=binding.Id,Team=binding.TeamId,Directory=directory,Task="Review the project",ExpiresUtc=expiry});return token;
    }
    static QuickTeamTasks.Result Result(string token){return WorkshopFiles.Load<QuickTeamTasks.Result>(Path.Combine(QuickTeamTasks.QueueDirectory,token+".result.json"));}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();UiLanguage.Set("en");UiLanguageBindings.Initialize();
            string state=Program.StateDirectory,original=Path.Combine(state,"original"),cwd=Path.Combine(state,"current folder 中文 🌱");
            System.IO.Directory.CreateDirectory(original);System.IO.Directory.CreateDirectory(cwd);
            var team=new CompanionSect{Name="Project review",Goal="Saved template goal",MaxParallel=2,RunLimitMinutes=0};
            team.Members.Add(new SectMember{Id="reader",Name="Qingheng",CharacterId="orbit",Directory=original,Role="Read and plan"});
            team.Members.Add(new SectMember{Id="reviewer",Name="Cangzhi",CharacterId="moss",Directory=original,Role="Review independently"});
            team.Steps.Add(new SectStep{Id="inspect",Member="reader",Task="Inspect the project",ExecutionMode="read-only"});
            team.Steps.Add(new SectStep{Id="review",Member="reviewer",Task="Review risks",ExecutionMode="read-only"});
            team.Steps.Add(new SectStep{Id="summary",Member="reader",Task="Summarize findings",DependsOn="inspect,review",ExecutionMode="read-only"});
            var data=new WorkshopData();data.Sects.Add(team);
            string legacy=QuickTeamTasks.TeamKey(team);Check(legacy==QuickTeamTasks.TeamKey(team),"legacy team identity is deterministic before migration");
            team.Id=legacy;var binding=QuickTeamTasks.Binding(team);team.Name="Code review team";
            Check(binding.TeamId==QuickTeamTasks.TeamKey(team),"renaming a saved team preserves shortcut identity");
            var command=new QuickTaskCommand{Name="Review together"};command.Companions.Add(binding);
            var options=new QuickTaskOptions{Enabled=true,MainMenu=false};options.Commands.Add(command);
            QuickTaskCommands.Save(options);WorkshopFiles.Save(QuickTeamTasks.WorkshopPath,data);
            string task="Check this folder: quotes \" & $() ` | 中文 🌱\nReport evidence, then summarize.";
            var draft=QuickTeamTasks.Prepare(data,binding,cwd,task);
            Check(draft.Goal==task&&draft.Members.TrueForAll(m=>m.Directory==cwd),"current Unicode directory and exact request become the run scope");
            Check(team.Goal=="Saved template goal"&&team.Members[0].Directory==original&&draft.Steps[2].DependsOn=="inspect,review"&&draft.RunLimitMinutes==0,
                "saved goal, directories, roles, dependencies and unlimited duration remain intact in the template");
            var workers=new List<Fake>();SectExecution execution=null;int starts=0;
            var pool=new SectExecutionPool(data,delegate{WorkshopFiles.Save(QuickTeamTasks.WorkshopPath,data);},delegate{var f=new Fake();workers.Add(f);return f;});
            Action<CompanionSect> start=delegate(CompanionSect plan){starts++;execution=pool.Start(plan);};
            QuickTeamTasks.Submit(command,binding,cwd,task,delegate{QuickTeamTasks.ProcessPending(data,start);});
            Check(starts==1&&workers.Count==2&&workers[0].Character=="orbit"&&workers[1].Character=="moss"&&workers[0].Prompt.Contains(task),
                "request reaches the owner once and launches independent members with their actual profiles and shared objective");
            QuickTeamTasks.ProcessPending(data,start);Check(starts==1,"consumed request cannot dispatch twice");
            workers[0].Finish();execution.Pump(DateTime.UtcNow);Check(workers.Count==2,"summary waits for every upstream member");
            workers[1].Finish();execution.Pump(DateTime.UtcNow);Check(workers.Count==3,"dependent summary starts after both parallel results");
            workers[2].Finish();execution.Pump(DateTime.UtcNow);Check(execution.Record.Status=="success","end-to-end team run completes with isolated fake agents");
            QuickTeamTasks.Submit(command,binding,cwd,"Batch two",delegate{QuickTeamTasks.ProcessPending(data,start);});var second=execution;
            QuickTeamTasks.Submit(command,binding,cwd,"Batch three",delegate{QuickTeamTasks.ProcessPending(data,start);});var third=execution;
            Check(starts==3&&second.IsRunning&&third.IsRunning&&workers.Count==7,"submitting again starts an independent batch while the previous batch runs");
            Check(second.Record.Id!=third.Record.Id&&second.Record.Goal=="Batch two"&&third.Record.Goal=="Batch three"&&pool.Find(second.Record)==second,
                "each batch has a distinct identity, objective, scheduler and history entry");
            Check(workers[3].Character==workers[5].Character&&workers[3].Marker!=workers[5].Marker&&!Object.ReferenceEquals(workers[3],workers[5]),
                "the same companion profile launches distinct agent instances and session markers across batches");
            second.SetPaused(true);Check(second.Record.Paused&&!third.Record.Paused,"pausing a batch does not pause another batch");
            second.Cancel();Check(second.Record.Status=="cancelled"&&third.IsRunning,"stopping one batch leaves other batches running");
            Check(data.History.Count==3&&data.History.Contains(third.Record),"new batches never discard another active run from history");
            WorkshopForm workspace=null;
            using(workspace=new WorkshopForm(data,()=>WorkshopFiles.Save(QuickTeamTasks.WorkshopPath,data),()=>workspace==null?pool.Latest:pool.Find(workspace.SelectedRun),plan=>pool.Start(plan),()=>pool.Find(workspace.SelectedRun).Cancel()))
            {
                workspace.Show();workspace.ShowExecution(third.Record);DateTime tick=DateTime.UtcNow.AddMilliseconds(360);
                while(DateTime.UtcNow<tick){Application.DoEvents();Thread.Sleep(5);}
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                Check(workspace.SelectedRun==third.Record&&((Button)typeof(WorkshopForm).GetField("run",flags).GetValue(workspace)).Enabled,
                    "workspace can launch another batch while the selected batch is running");
                ((TextBox)typeof(WorkshopForm).GetField("name",flags).GetValue(workspace)).Text="Updated review team";
                typeof(WorkshopForm).GetMethod("SaveSect",flags).Invoke(workspace,null);
                Check(data.Sects[0].Id==binding.TeamId&&data.Sects[0].Name=="Updated review team"&&third.Plan.Name=="Code review team"&&third.Plan.Goal=="Batch three",
                    "editing a live team's template preserves its binding and leaves existing run snapshots unchanged");
                workspace.ShowExecution(second.Record);Check(workspace.SelectedRun==second.Record,"run selection follows the chosen batch rather than the newest run");
                workspace.ShowExecution(third.Record);((Button)typeof(WorkshopForm).GetField("stop",flags).GetValue(workspace)).PerformClick();
                Check(third.Record.Status=="cancelled"&&second.Record.Status=="cancelled","workspace Stop targets the selected batch");
                workspace.Dispose();
            }
            string expired=Enqueue(command,binding,cwd,DateTime.UtcNow.AddMinutes(-1));QuickTeamTasks.ProcessPending(data,start);
            Check(!Result(expired).Success&&starts==3,"expired request never executes after delayed startup");
            string stale=Enqueue(command,binding,cwd,DateTime.UtcNow.AddSeconds(20));options.Enabled=false;QuickTaskCommands.Save(options);QuickTeamTasks.ProcessPending(data,start);
            Check(!Result(stale).Success&&starts==3,"owner revalidates disabled bindings instead of trusting queued data");
            options.Enabled=true;QuickTaskCommands.Save(options);
            Rejected(()=>QuickTeamTasks.Prepare(new WorkshopData(),binding,cwd,task),"removed team is rejected");
            Rejected(()=>QuickTeamTasks.Prepare(data,binding,Path.Combine(cwd,"missing"),task),"missing current folder never falls back to template directory");
            Rejected(()=>QuickTeamTasks.Prepare(data,binding,cwd,new string('x',4001)),"team task enforces shared-objective size limit");
            data.Sects.Add(team);Rejected(()=>QuickTeamTasks.Prepare(data,binding,cwd,task),"duplicate team identity is rejected");data.Sects.RemoveAt(1);
            Check(QuickTaskShell.Caption(command,binding).Contains("team")&&QuickTaskShell.Arguments("app.exe",command,binding,QuickTaskShell.Locations[0]).Contains(binding.Id),
                "shell menu identifies the team and keeps the bound target in invocation");
            binding.IconPath=QuickCommandsForm.CreateIcon(binding.Id);Check(File.Exists(binding.IconPath),"team uses the fixed AgentPal icon");
            using(var editor=new QuickCommandsForm())
            {
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                var choices=(CheckedListBox)typeof(QuickCommandsForm).GetField("pets",flags).GetValue(editor);bool found=false;
                foreach(QuickTaskBinding choice in choices.Items)if(choice.IsTeam&&choice.TeamId==binding.TeamId)found=true;
                Check(found,"saved teams appear alongside companions in the binding picker");
                typeof(QuickCommandsForm).GetMethod("Store",flags).Invoke(editor,null);
                var saved=(QuickTaskOptions)typeof(QuickCommandsForm).GetField("options",flags).GetValue(editor);
                Check(saved.Commands[0].Companions[0].TeamId==binding.TeamId,"editing a shortcut preserves team binding metadata");
                editor.Show();Application.DoEvents();using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(Point.Empty,editor.Size));image.Save(Path.Combine(state,"team-shortcut-settings.png"));}editor.Close();
            }
            var release=new ManualResetEvent(false);int ticks=0;string submitted=null;
            using(var dialog=new QuickTaskRequestForm(command,binding,cwd,value=>{release.WaitOne(5000);submitted=value;}))
            using(var timer=new System.Windows.Forms.Timer{Interval=40})
            {
                dialog.Show();dialog.TaskInput.Text=task;Application.DoEvents();
                Check(dialog.TaskInput.MaxLength==4000&&dialog.Controls["QuickTaskHint"].Text.Contains("saved team"),"team dialog clearly describes scope and input limit");
                using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(state,"team-task-request.png"));}
                timer.Tick+=delegate{ticks++;if(ticks==3)release.Set();};timer.Start();dialog.Confirm.PerformClick();
                Check(!dialog.Confirm.Enabled&&dialog.TaskInput.ReadOnly,"submitting disables duplicate sends and preserves input");
                DateTime until=DateTime.UtcNow.AddSeconds(6);while(!dialog.IsDisposed&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(5);}
                Check(ticks>=3&&submitted==task&&dialog.IsDisposed,"team startup waits asynchronously while UI continues processing events");
            }
            pool.Dispose();
            var failureData=new WorkshopData();bool failSave=false;
            using(var isolated=new SectExecutionPool(failureData,delegate{if(failSave)throw new IOException("Isolated write failure");},()=>new Fake()))
            {
                var running=isolated.Start(draft);failSave=true;
                Rejected(()=>isolated.Start(draft),"failed history persistence rejects the new batch before launching members");
                Check(running.IsRunning&&failureData.History.Count==1&&isolated.Latest==running,"a failed new submission never cancels or replaces an existing batch");
            }
            Console.WriteLine("All team shortcut checks passed; no Claude or model calls. Evidence: "+state);return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
