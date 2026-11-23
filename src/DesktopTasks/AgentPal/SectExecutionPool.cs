// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

namespace AgentPal
{
    // Each submission owns a scheduler and agent instances. A companion profile
    // is configuration, never a shared live process or conversation.
    internal sealed class SectExecutionPool:IDisposable
    {
        private readonly List<SectExecution> runs=new List<SectExecution>();
        private readonly WorkshopData data;
        private readonly Action save;
        private readonly Func<ISectAgent> factory;
        internal SectExecutionPool(WorkshopData data,Action save,Func<ISectAgent> factory)
        {this.data=data;this.save=save;this.factory=factory;}
        internal SectExecution Latest {get{return runs.Count==0?null:runs[runs.Count-1];}}
        internal bool HasRunning {get{return runs.Exists(item=>item.IsRunning);}}
        internal SectExecution Find(SectRun record){return runs.Find(item=>Object.ReferenceEquals(item.Record,record));}
        internal SectExecution Start(CompanionSect plan)
        {
            var next=new SectExecution(plan,factory);
            data.History.Insert(0,next.Record);
            try{save();}catch{data.History.Remove(next.Record);next.Dispose();throw;}
            runs.Add(next);next.Pump(DateTime.UtcNow);return next;
        }
        internal void Pump(DateTime utc)
        {
            foreach(var run in runs)if(run.IsRunning)run.Pump(utc);
            for(int i=runs.Count-1;i>=0;i--)if(!runs[i].IsRunning&&!data.History.Contains(runs[i].Record)){runs[i].Dispose();runs.RemoveAt(i);}
        }
        internal void CancelAll(){foreach(var run in runs)if(run.IsRunning)run.Cancel();}
        public void Dispose(){foreach(var run in runs)run.Dispose();runs.Clear();}
    }
}
