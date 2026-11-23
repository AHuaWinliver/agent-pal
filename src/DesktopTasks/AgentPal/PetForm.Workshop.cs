// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private WorkshopData workshopData;
        private WorkshopForm workshopForm;
        private SectExecutionPool workshopExecutions;
        private readonly Timer workshopTimer = new Timer { Interval = 250 };
        private DateTime workshopLastSaved;
        private bool workshopSaveFailed;
        private bool workshopPendingSave;
        private string dueSect;
        private string WorkshopPath { get { return Path.Combine(Program.StateDirectory, "workshop.json"); } }
        private void InitializeWorkshop()
        {
            if (Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") == "1") return;
            try
            {
                workshopData = WorkshopFiles.Load<WorkshopData>(WorkshopPath);
                if (workshopData.Sects == null || workshopData.Events == null || workshopData.History == null) throw new InvalidDataException("规划数据不完整。");
                foreach (SectRun r in workshopData.History) if (r.Status == "running")
                { r.Status = "interrupted"; r.Finished = DateTime.Now.ToString("o"); foreach (SectStepRun s in r.Steps) if (s.Status == "running" || s.Status == "pending") s.Status = "interrupted"; }
                bool migrated=false;
                foreach(CompanionSect team in workshopData.Sects)if(String.IsNullOrEmpty(team.Id)){team.Id=QuickTeamTasks.TeamKey(team);migrated=true;}
                if(migrated)SaveWorkshop();
                workshopExecutions=new SectExecutionPool(workshopData,SaveWorkshop,delegate{return new LocalSectAgent();});
                workshopTimer.Tick += delegate { TickWorkshop(); };
                tray.BalloonTipClicked += delegate { if (dueSect != null) { ShowWorkshop(); if (workshopForm != null) workshopForm.SelectSect(dueSect); dueSect = null; } };
                workshopTimer.Start();
            }
            catch (Exception ex) { workshopData = null; MessageBox.Show("事件与 Agent 团队数据读取失败，原文件未覆盖。\n" + ex.Message); }
        }
        private void SaveWorkshop()
        {
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength=33554432 };
            while(workshopData.History.FindAll(item=>item.Status!="running").Count>20)
                workshopData.History.RemoveAt(workshopData.History.FindLastIndex(item=>item.Status!="running"));
            while(System.Text.Encoding.UTF8.GetByteCount(serializer.Serialize(workshopData))>6000000 && workshopData.History.Count>1)
            {
                int remove=workshopData.History.FindLastIndex(item=>item.Status!="running");
                if(remove<0)break;
                workshopData.History.RemoveAt(remove);
            }
            WorkshopFiles.Save(WorkshopPath, workshopData); workshopLastSaved = DateTime.UtcNow;workshopPendingSave=false;
        }
        private void ShowWorkshop()
        {
            if (workshopData == null) { MessageBox.Show("事件与 Agent 团队模块不可用，请检查 workshop.json。"); return; }
            if (workshopForm == null || workshopForm.IsDisposed)
                workshopForm = new WorkshopForm(workshopData, SaveWorkshop, SelectedWorkshopExecution, StartSect, delegate {var execution=SelectedWorkshopExecution();if(execution!=null){execution.Cancel();SaveWorkshop();} });
            workshopForm.Show(); workshopForm.BringToFront();
        }
        private void StartSect(CompanionSect sect)
        {
            workshopExecutions.Start(sect);workshopPendingSave=true;
        }
        private SectExecution SelectedWorkshopExecution()
        {return workshopExecutions==null?null:workshopForm!=null&&workshopForm.SelectedRun!=null?workshopExecutions.Find(workshopForm.SelectedRun):workshopExecutions.Latest;}
        private void TickWorkshop()
        {
            try
            {
                if(workshopExecutions!=null)
                {
                    if(workshopExecutions.HasRunning){workshopExecutions.Pump(DateTime.UtcNow);workshopPendingSave=true;}
                    if(workshopPendingSave&&(!workshopExecutions.HasRunning||(DateTime.UtcNow-workshopLastSaved).TotalSeconds>=5))SaveWorkshop();
                }
                QuickTeamTasks.ProcessPending(workshopData,delegate(CompanionSect team)
                {StartSect(team);ShowWorkshop();workshopForm.ShowExecution(workshopExecutions.Latest.Record);});
                foreach (PlannedEvent e in workshopData.Events)
                {
                    if (e.Done || e.When > DateTime.Now) continue;
                    if (e.Repeat == "daily" || e.Repeat == "weekly") e.When = PlannedEvent.Next(e.When, DateTime.Now, e.Repeat); else e.Done = true;
                    SaveWorkshop(); // Persist the acknowledgement before notifying.
                    dueSect = e.Sect ?? "";
                    tray.ShowBalloonTip(12000, "AgentPal · 事件到点", e.Title + (dueSect.Length > 0 ? "\n关联 Agent 团队：" + dueSect + "；点击查看，确认后执行。" : "\n点击打开事件规划。"), ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                workshopTimer.Stop(); if (workshopExecutions != null) workshopExecutions.CancelAll();
                if (!workshopSaveFailed) { workshopSaveFailed = true; MessageBox.Show("规划保存或执行出错，已暂停新任务，请检查磁盘后重启。\n" + ex.Message); }
            }
        }
        private void DisposeWorkshop()
        {
            workshopTimer.Stop(); workshopTimer.Dispose();
            if (workshopExecutions != null) {workshopExecutions.Dispose();try {SaveWorkshop();}catch {} }
            if (workshopForm != null) workshopForm.Dispose();
        }
    }
}
