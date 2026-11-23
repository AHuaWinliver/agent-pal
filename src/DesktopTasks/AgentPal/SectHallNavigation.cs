// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;

namespace AgentPal
{
    // The seated position is behind its desk. The sole permitted docking
    // segment runs sideways behind that desk, never through its front edge.
    // Every other segment stays outside furniture plus foot clearance.
    internal sealed class SectHallNavigation
    {
        private readonly IDictionary<string,PointF> homes;
        private readonly IDictionary<string,float> scales;
        private readonly Dictionary<string,PointF[]> patrols=new Dictionary<string,PointF[]>();
        internal SectHallNavigation(IDictionary<string,PointF> homes,IDictionary<string,float> scales)
        {this.homes=homes;this.scales=scales;}
        internal static RectangleF Furniture(PointF home,float scale)
        {return new RectangleF(home.X-95*scale,home.Y-78*scale,190*scale,82*scale);}
        internal RectangleF Obstacle(string desk,float moverScale)
        {RectangleF r=Furniture(homes[desk],scales[desk]);r.Inflate(24*moverScale,12*moverScale);return r;}
        internal PointF[] Patrol(string actor,int index)
        {
            PointF[] path;if(patrols.TryGetValue(actor,out path))return path;
            PointF home=homes[actor];float scale=scales[actor],side=index%2==0?1:-1;
            path=Plan(actor,home,new PointF(home.X+side*66*scale,home.Y+64*scale),false);
            if(path==null)path=Plan(actor,home,new PointF(home.X-side*66*scale,home.Y+64*scale),false);
            patrols[actor]=path;return path;
        }
        internal PointF[] Plan(string actor,PointF start,PointF end,bool docking)
        {
            if(Distance(start,end)<.01)return new[]{end};
            float scale=scales[actor];RectangleF own=Obstacle(actor,scale);
            var obstacles=new List<RectangleF>();foreach(string name in homes.Keys)obstacles.Add(Obstacle(name,scale));
            var starts=new List<PointF>();var ends=new List<PointF>();
            if(Inside(own,start))
            {
                // Only positions on the rear seated line may leave the desk.
                if(Math.Abs(start.Y-homes[actor].Y)>2*scale)return null;
                AddGates(starts,actor,start,scale);
            }
            else starts.Add(start);
            if(docking)AddGates(ends,actor,end,scale);else ends.Add(end);
            PointF[] best=null;double bestLength=Double.MaxValue;
            foreach(PointF first in starts)foreach(PointF last in ends)
            {
                PointF[] middle=Find(first,last,obstacles);if(middle==null)continue;
                var path=new List<PointF>();path.Add(start);
                foreach(PointF p in middle)if(Distance(path[path.Count-1],p)>.01)path.Add(p);
                if(Distance(path[path.Count-1],end)>.01)path.Add(end);
                double length=Length(path.ToArray());if(length<bestLength){bestLength=length;best=path.ToArray();}
            }
            return best;
        }
        private void AddGates(List<PointF> result,string actor,PointF dock,float scale)
        {
            RectangleF own=Obstacle(actor,scale);
            foreach(float x in new[]{own.Left-2*scale,own.Right+2*scale})
            {
                PointF gate=new PointF(x,homes[actor].Y);bool clear=true;
                foreach(string other in homes.Keys)if(other!=actor&&Intersects(dock,gate,Obstacle(other,scale)))clear=false;
                if(clear)result.Add(gate);
            }
        }
        internal bool SafeSegment(string actor,PointF a,PointF b)
        {
            float scale=scales[actor];PointF home=homes[actor];
            foreach(string desk in homes.Keys)
            {
                if(desk==actor&&Math.Abs(a.Y-home.Y)<.01&&Math.Abs(b.Y-home.Y)<.01)continue;
                if(Intersects(a,b,Obstacle(desk,scale)))return false;
            }
            return true;
        }
        private static PointF[] Find(PointF start,PointF end,List<RectangleF> obstacles)
        {
            foreach(RectangleF r in obstacles)if(Inside(r,start)||Inside(r,end))return null;
            var xs=new List<float>{start.X,end.X};var ys=new List<float>{start.Y,end.Y};
            foreach(RectangleF r in obstacles){xs.Add(r.Left-1);xs.Add(r.Right+1);ys.Add(r.Top-1);ys.Add(r.Bottom+1);}
            xs.Sort();ys.Sort();Unique(xs);Unique(ys);int w=xs.Count,n=w*ys.Count;
            var valid=new bool[n];var cost=new double[n];var previous=new int[n];var visited=new bool[n];
            int source=-1,target=-1;
            for(int y=0;y<ys.Count;y++)for(int x=0;x<w;x++)
            {
                int id=y*w+x;var point=new PointF(xs[x],ys[y]);valid[id]=true;cost[id]=Double.MaxValue;previous[id]=-1;
                foreach(RectangleF r in obstacles)if(Inside(r,point)){valid[id]=false;break;}
                if(point==start)source=id;if(point==end)target=id;
            }
            if(source<0||target<0)return null;cost[source]=0;
            for(int count=0;count<n;count++)
            {
                int current=-1;double smallest=Double.MaxValue;
                for(int i=0;i<n;i++)if(!visited[i]&&valid[i]&&cost[i]<smallest){current=i;smallest=cost[i];}
                if(current<0)break;if(current==target)break;visited[current]=true;
                int x=current%w,y=current/w;PointF point=new PointF(xs[x],ys[y]);
                foreach(int next in new[]{x>0?current-1:-1,x+1<w?current+1:-1,y>0?current-w:-1,y+1<ys.Count?current+w:-1})
                {
                    if(next<0||!valid[next]||visited[next])continue;
                    PointF to=new PointF(xs[next%w],ys[next/w]);bool clear=true;
                    foreach(RectangleF r in obstacles)if(Intersects(point,to,r)){clear=false;break;}
                    double candidate=smallest+Distance(point,to);
                    if(clear&&candidate<cost[next]){cost[next]=candidate;previous[next]=current;}
                }
            }
            if(cost[target]==Double.MaxValue)return null;
            var result=new List<PointF>();for(int id=target;id>=0;id=previous[id]){result.Add(new PointF(xs[id%w],ys[id/w]));if(id==source)break;}
            result.Reverse();
            for(int i=result.Count-2;i>0;i--)if((result[i-1].X==result[i].X&&result[i].X==result[i+1].X)||(result[i-1].Y==result[i].Y&&result[i].Y==result[i+1].Y))result.RemoveAt(i);
            return result.ToArray();
        }
        private static void Unique(List<float> values){for(int i=values.Count-1;i>0;i--)if(values[i]==values[i-1])values.RemoveAt(i);}
        private static bool Inside(RectangleF r,PointF p){return p.X>r.Left&&p.X<r.Right&&p.Y>r.Top&&p.Y<r.Bottom;}
        private static bool Intersects(PointF a,PointF b,RectangleF r)
        {
            if(Math.Abs(a.Y-b.Y)<.01)return a.Y>r.Top&&a.Y<r.Bottom&&Math.Max(a.X,b.X)>r.Left&&Math.Min(a.X,b.X)<r.Right;
            if(Math.Abs(a.X-b.X)<.01)return a.X>r.Left&&a.X<r.Right&&Math.Max(a.Y,b.Y)>r.Top&&Math.Min(a.Y,b.Y)<r.Bottom;
            return true;
        }
        internal static double Length(PointF[] path){double value=0;for(int i=1;i<path.Length;i++)value+=Distance(path[i-1],path[i]);return value;}
        internal static double Distance(PointF a,PointF b){return Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
    }
}
