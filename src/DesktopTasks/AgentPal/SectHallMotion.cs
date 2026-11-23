// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;

namespace AgentPal
{
    internal sealed class HallActorPose
    {
        internal PointF Feet;
        internal double Distance;
        internal bool Moving, Reading, Handoff, AtDesk = true, FaceLeft;
        internal string Target = "";
    }
    // Presentation only: consumes real handoff records, never starts or delays agents.
    internal sealed class SectHallMotion
    {
        private sealed class Visit { internal string Target; internal double Started = -1, Duration; internal PointF[] Outbound, Returning, ReceiverPath; internal double ReturnDuration,ReceiverDuration,MeetingStart; }
        private readonly Dictionary<string, Queue<Visit>> visits = new Dictionary<string, Queue<Visit>>();
        private readonly Dictionary<string, PointF> previousOffsets = new Dictionary<string, PointF>();
        private readonly Dictionary<string, double> workOrigins = new Dictionary<string, double>(), strides = new Dictionary<string, double>();
        private readonly Dictionary<string, string> lastStates = new Dictionary<string, string>();
        private SectRun observed;
        private SectHallNavigation navigation;
        private string geometry="";
        private int seen;
        private bool skipBacklog;
        internal void Suspend(){skipBacklog=true;}
        internal Dictionary<string, HallActorPose> Sample(SectRun run, IList<SectMemberStatus> members, IDictionary<string, PointF> homes, IDictionary<string, float> scales, double now, bool reduced)
        {
            if (!Object.ReferenceEquals(observed, run)) { observed = run; seen = run == null || run.Transfers == null ? 0 : run.Transfers.Count; visits.Clear(); previousOffsets.Clear(); workOrigins.Clear();strides.Clear();lastStates.Clear(); }
            if(skipBacklog){seen=run==null||run.Transfers==null?0:run.Transfers.Count;visits.Clear();skipBacklog=false;}
            if (run != null && run.Transfers != null)
            {
                if (seen > run.Transfers.Count) { seen = run.Transfers.Count; visits.Clear(); }
                while (seen < run.Transfers.Count)
                {
                    SectTransfer transfer = run.Transfers[seen++];
                    SectStepRun from = run.Steps.Find(delegate(SectStepRun step) { return step.Id == transfer.From; });
                    SectStepRun to = run.Steps.Find(delegate(SectStepRun step) { return step.Id == transfer.To; });
                    string sender = String.IsNullOrEmpty(transfer.FromMember) ? from == null ? "" : from.Member : transfer.FromMember;
                    string receiver = String.IsNullOrEmpty(transfer.ToMember) ? to == null ? "" : to.Member : transfer.ToMember;
                    if (sender == receiver || !homes.ContainsKey(sender) || !homes.ContainsKey(receiver)) continue;
                    Queue<Visit> queue; if (!visits.TryGetValue(sender, out queue)) visits[sender] = queue = new Queue<Visit>();
                    bool queued=false;foreach(Visit visit in queue)if(visit.Target==receiver){queued=true;break;}
                    if (!queued&&queue.Count < 24) queue.Enqueue(new Visit { Target = receiver });
                }
            }
            var stamp=new System.Text.StringBuilder();
            foreach(var entry in homes)stamp.Append(entry.Key).Append('|').Append(entry.Value.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(entry.Value.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(scales[entry.Key].ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            if(geometry!=stamp.ToString())
            {geometry=stamp.ToString();navigation=new SectHallNavigation(homes,scales);foreach(var queue in visits.Values)foreach(var visit in queue)visit.Started=-1;}
            if(reduced)visits.Clear();
            var result = new Dictionary<string, HallActorPose>();
            for (int index = 0; index < members.Count; index++)
            {
                SectMemberStatus member = members[index]; PointF home; if (!homes.TryGetValue(member.Name, out home)) continue;
                string last;
                if(!lastStates.TryGetValue(member.Name,out last)||last!=member.Status)
                {workOrigins[member.Name]=now-index*1.1;lastStates[member.Name]=member.Status;}
                float scale = scales[member.Name]; HallActorPose pose = Working(member.Name,member.Status == "running", index, now-workOrigins[member.Name], home, scale, reduced);
                Queue<Visit> queue;
                if (!reduced && visits.TryGetValue(member.Name, out queue) && queue.Count > 0)
                {
                    Visit visit = queue.Peek(); PointF target;
                    if (!homes.TryGetValue(visit.Target, out target)) { queue.Dequeue(); result[member.Name] = pose; continue; }
                    if (visit.Started < 0)
                    {
                        bool occupied=false;
                        foreach(var other in visits)if(other.Value.Count>0)
                        {Visit current=other.Value.Peek();if(current.Started>=0&&(other.Key==visit.Target||current.Target==member.Name||current.Target==visit.Target)){occupied=true;break;}}
                        if(occupied){result[member.Name]=pose;continue;}
                        PointF previous;PointF start=previousOffsets.TryGetValue(member.Name,out previous)?new PointF(home.X+previous.X*scale,home.Y+previous.Y*scale):pose.Feet;
                        float targetScale=scales[visit.Target],side=home.X<target.X?-1:1;
                        PointF end=new PointF(target.X+side*44*targetScale,target.Y+64*Math.Max(scale,targetScale));
                        visit.Outbound=navigation.Plan(member.Name,start,end,false);
                        if(visit.Outbound==null)visit.Outbound=navigation.Plan(member.Name,home,end,false);
                        visit.Returning=navigation.Plan(member.Name,end,home,true);
                        if(visit.Outbound==null||visit.Returning==null){queue.Dequeue();result[member.Name]=pose;continue;}
                        visit.Started=now;visit.Duration=Math.Max(.15,Length(visit.Outbound)/Math.Max(1,105*scale));
                        visit.ReturnDuration=Math.Max(.15,Length(visit.Returning)/Math.Max(1,105*scale));
                        PointF receiverHome=homes[visit.Target],receiverStart=receiverHome;
                        if(previousOffsets.TryGetValue(visit.Target,out previous))receiverStart=new PointF(receiverHome.X+previous.X*targetScale,receiverHome.Y+previous.Y*targetScale);
                        visit.ReceiverPath=navigation.Plan(visit.Target,receiverStart,receiverHome,true);
                        visit.ReceiverDuration=visit.ReceiverPath==null?0:Length(visit.ReceiverPath)/Math.Max(1,105*targetScale);
                        visit.MeetingStart=Math.Max(visit.Duration,visit.ReceiverDuration);
                    }
                    double t=Math.Max(0,now-visit.Started);PointF meeting=visit.Outbound[visit.Outbound.Length-1];
                    if(t<visit.Duration)pose=WalkingRoute(visit.Outbound,t/visit.Duration,Length(visit.Outbound),false);
                    else if(t<visit.MeetingStart+1.25)pose=new HallActorPose{Feet=meeting,Handoff=true,Reading=true,AtDesk=false,Target=visit.Target,FaceLeft=meeting.X>target.X};
                    else if(t<visit.MeetingStart+1.25+visit.ReturnDuration)
                        pose=WalkingRoute(visit.Returning,(t-visit.MeetingStart-1.25)/visit.ReturnDuration,Length(visit.Returning),false);
                    else{queue.Dequeue();workOrigins[member.Name]=now;pose=new HallActorPose{Feet=home,Reading=member.Status=="running"};}
                    if(pose.Moving)pose.AtDesk=Distance(pose.Feet,home)<2*scale;
                    if (pose.Moving) { pose.Handoff = true; pose.Target = visit.Target; }
                }
                result[member.Name] = pose;
            }
            // The receiving member waits at its desk while the sender approaches.
            // This changes only the illustration, never the running agent.
            foreach(var sender in visits)if(sender.Value.Count>0)
            {
                Visit visit=sender.Value.Peek();HallActorPose receiver;PointF home;
                if(visit.Started<0||now-visit.Started>visit.MeetingStart+1.25||!result.TryGetValue(visit.Target,out receiver)||!homes.TryGetValue(visit.Target,out home))continue;
                double time=Math.Max(0,now-visit.Started);
                if(visit.ReceiverPath!=null&&time<visit.ReceiverDuration)
                {receiver=WalkingRoute(visit.ReceiverPath,time/visit.ReceiverDuration,Length(visit.ReceiverPath),false);receiver.Handoff=true;receiver.Target=sender.Key;result[visit.Target]=receiver;}
                else{receiver.Feet=home;receiver.AtDesk=true;receiver.Moving=false;receiver.Reading=true;}
                workOrigins[visit.Target]=now;
            }
            foreach(var actor in result)
            {
                PointF home=homes[actor.Key];float scale=scales[actor.Key];var offset=new PointF((actor.Value.Feet.X-home.X)/scale,(actor.Value.Feet.Y-home.Y)/scale);
                PointF old;double stride;strides.TryGetValue(actor.Key,out stride);
                if(actor.Value.Moving&&previousOffsets.TryGetValue(actor.Key,out old))stride+=Math.Min(18,Distance(old,offset));
                strides[actor.Key]=stride;actor.Value.Distance=stride*scale;previousOffsets[actor.Key]=offset;
            }
            return result;
        }
        private HallActorPose Working(string actor,bool working,int index,double now,PointF home,float scale,bool reduced)
        {
            var pose=new HallActorPose{Feet=home,Reading=working};if(!working||reduced)return pose;
            PointF[] path=navigation.Patrol(actor,index);if(path==null||path.Length<2)return pose;
            double length=Length(path),travel=Math.Max(.15,length/Math.Max(1,105*scale)),cycle=6+travel*2;
            double phase=(now%cycle+cycle)%cycle;
            if(phase<4)return pose;
            if(phase<4+travel)pose=WalkingRoute(path,(phase-4)/travel,length,false);
            else if(phase<6+travel)pose=new HallActorPose{Feet=path[path.Length-1],Reading=true,AtDesk=false};
            else{var back=(PointF[])path.Clone();Array.Reverse(back);pose=WalkingRoute(back,(phase-6-travel)/travel,length,false);}
            pose.AtDesk=Distance(pose.Feet,home)<2*scale;return pose;
        }
        private static double Smooth(double value) { return value * value * (3 - 2 * value); }
        private static double Length(PointF[] path) { double length = 0; for (int i = 1; i < path.Length; i++) length += Distance(path[i - 1], path[i]); return length; }
        private static double Distance(PointF a, PointF b) { return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)); }
        private static HallActorPose WalkingRoute(PointF[] path, double fraction, double total, bool reverse)
        {
            // Ease only at the endpoints; cumulative distance drives the foot cycle.
            double distance = total * Smooth(Math.Max(0, Math.Min(1, fraction))), remaining = distance;
            PointF position = path[0]; bool left = false;
            for (int i = 1; i < path.Length; i++)
            {
                double length = Distance(path[i - 1], path[i]);
                if (remaining <= length || i == path.Length - 1)
                {
                    double t = length < .001 ? 0 : Math.Min(1, remaining / length);
                    position = new PointF((float)(path[i - 1].X + (path[i].X - path[i - 1].X) * t), (float)(path[i - 1].Y + (path[i].Y - path[i - 1].Y) * t));
                    left = reverse ? path[i].X >= path[i - 1].X : path[i].X < path[i - 1].X; break;
                }
                remaining -= length;
            }
            return new HallActorPose { Feet = position, Distance = reverse ? total - distance : distance, Moving = true, AtDesk = false, FaceLeft = left };
        }
    }
}
