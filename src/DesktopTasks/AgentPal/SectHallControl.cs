// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    // Hall animation visualizes recorded execution states and handoffs.
    internal sealed partial class SectHallControl : Control
    {
        private readonly Func<CompanionSect> selected;
        private readonly Func<SectRun> record;
        private readonly Action<string> details;
        private readonly Timer clock = new Timer { Interval = 16 };
        private readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        private readonly Dictionary<string, Rectangle> seats = new Dictionary<string, Rectangle>();
        private string inspectedMember, lastInspection;
        private int ticks;
        private Image background;
        private readonly Dictionary<string,Image> spriteCache=new Dictionary<string,Image>();
        private readonly Queue<string> spriteOrder=new Queue<string>();
        private readonly string contentRoot;
        internal bool HasBackgroundImage { get { return background!=null; } }
        internal string BackgroundSource { get; private set; }
        internal const string BackgroundResourceName="AgentPal.SectHall.Background.png";
        internal int MemberCount {get{return Labels(selected(),record()).Count;}}
        internal SectHallControl(Func<CompanionSect> selected, Func<SectRun> record, Action<string> details)
        {
            this.selected = selected; this.record = record; this.details = details;
            string root=AppDomain.CurrentDomain.BaseDirectory;
            contentRoot=Directory.Exists(Path.Combine(root,"assets"))?root:Path.Combine(root,"payload");
            string scene=Path.Combine(contentRoot,"scenes","sect-hall-v2.png");
            LoadBackground(scene);
            DoubleBuffered = true; Dock = DockStyle.Fill; BackColor = Color.FromArgb(25, 33, 40);
            SetStyle(ControlStyles.ResizeRedraw,true);
            clock.Tick += delegate { if (Visible) { Invalidate(); if (++ticks % 6 == 0 && inspectedMember != null) InspectMember(inspectedMember); } };
            VisibleChanged += delegate { if (Visible) clock.Start(); else {clock.Stop();motion.Suspend();} };
            MouseClick += Inspect;
        }
        private void LoadBackground(string path)
        {
            try
            {
                if(File.Exists(path))using(Image source=Image.FromFile(path))
                {if(source.Width<320||source.Height<200||source.Width>8192||source.Height>8192)throw new InvalidDataException("背景图尺寸无效。");background=new Bitmap(source);BackgroundSource="file";return;}
            }
            catch(Exception ex)
            {
                // GDI+ also reports an invalid image file as OutOfMemoryException.
                if(!(ex is IOException)&&!(ex is ArgumentException)&&!(ex is OutOfMemoryException)&&!(ex is System.Runtime.InteropServices.ExternalException))throw;
            }
            using(Stream resource=typeof(SectHallControl).Assembly.GetManifestResourceStream(BackgroundResourceName))
                if(resource!=null)using(Image source=Image.FromStream(resource)){background=new Bitmap(source);BackgroundSource="embedded";return;}
            BackgroundSource="missing";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None; g.InterpolationMode = InterpolationMode.NearestNeighbor;
            if(Width<=0||Height<=0)return;
            if(background!=null)
            {
                // Cover the viewport without stretching the architecture.
                // Bottom anchoring keeps the floor behind the workstations.
                float fit=Math.Max(Width/(float)background.Width,Height/(float)background.Height);
                float sourceWidth=Width/fit,sourceHeight=Height/fit;
                g.DrawImage(background,ClientRectangle,new RectangleF((background.Width-sourceWidth)/2,background.Height-sourceHeight,sourceWidth,sourceHeight),GraphicsUnit.Pixel);
            }
            else g.Clear(Color.FromArgb(24,49,51));
            if(background==null)TextRenderer.DrawText(g,UiLanguage.Text("工作区背景资源缺失，请安装完整新版以修复。"),Font,new Point(18,145),Color.LightGoldenrodYellow);
            CompanionSect sect = selected(); SectRun run = record();
            string title = run != null ? run.Sect : sect != null ? sect.Name : "AgentPal · Agent 工作区";
            using(var font=new Font("Microsoft YaHei UI",Math.Max(10,Math.Min(18,Math.Min(Width/35f,Height/22f))),FontStyle.Bold))
            {
                var banner=new Rectangle(Width/5,Math.Max(6,(int)(Height*.04)),Width*3/5,font.Height+10);
                using(var shade=new SolidBrush(Color.FromArgb(215,28,30,28)))g.FillRectangle(shade,banner);
                using(var edge=new Pen(Color.FromArgb(164,129,66)))g.DrawRectangle(edge,banner);
                TextRenderer.DrawText(g,UiLanguage.Text(title),font,banner,Color.FromArgb(246,215,140),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }
            var labels=Labels(sect,run);UpdateSeats(labels);
            if(labels.Count==0) {TextRenderer.DrawText(g,UiLanguage.Text("先在“Agent 团队”中新建 Agent 团队并分配任务"),Font,new Point(25,120),Color.Gainsboro);return;}
            // Show real upstream submissions only. A repeated packet animation depicts this logged connection,
            // not repeated API calls or token-level telemetry.
            if(run!=null && run.Transfers!=null)
            {
                int edge=0;
                foreach(SectTransfer transfer in run.Transfers.GetRange(Math.Max(0,run.Transfers.Count-24),Math.Min(24,run.Transfers.Count)))
                {
                    SectStepRun from=run.Steps.Find(delegate(SectStepRun s){return String.Equals(s.Id,transfer.From,StringComparison.OrdinalIgnoreCase);});
                    SectStepRun to=run.Steps.Find(delegate(SectStepRun s){return String.Equals(s.Id,transfer.To,StringComparison.OrdinalIgnoreCase);});
                    string fromName=String.IsNullOrEmpty(transfer.FromMember)?(from==null?"":from.Member):transfer.FromMember;
                    string toName=String.IsNullOrEmpty(transfer.ToMember)?(to==null?"":to.Member):transfer.ToMember;
                    if(to==null||fromName==toName||!seats.ContainsKey(fromName)||!seats.ContainsKey(toName))continue;
                    Rectangle a=seats[fromName],b=seats[toName]; Point p=new Point(a.Left+a.Width/2,a.Top+a.Height/2),q=new Point(b.Left+b.Width/2,b.Top+b.Height/2);
                    using(var pen=new Pen(Color.FromArgb(100,186,197),2)){g.DrawLine(pen,p,q);}
                    int ax=(int)(p.X+(q.X-p.X)*.75),ay=(int)(p.Y+(q.Y-p.Y)*.75);Fill(g,Color.FromArgb(100,186,197),ax-3,ay-3,6,6);
                    if(run.Status=="running" && to.Status=="running")
                    {double t=(elapsed.Elapsed.TotalSeconds*.45+edge*.23)%1;int x=(int)(p.X+(q.X-p.X)*t),y=(int)(p.Y+(q.Y-p.Y)*t);Fill(g,Color.FromArgb(250,213,115),x-4,y-4,8,8);}
                    edge++;
                }
            }
            DrawActiveMembers(g,sect,run);
            TextRenderer.DrawText(g,UiLanguage.Text("点击成员查看任务、回复和交接数据 · 光点表示已记录的传递关系，不代表实时传输速度"),Font,new Rectangle(8,Height-24,Width-16,22),Color.Gainsboro,TextFormatFlags.EndEllipsis);
        }
        private static List<string> Labels(CompanionSect sect,SectRun run)
        {
            var labels=new List<string>();foreach(SectMemberStatus member in SectMemberStatus.Build(sect,run))labels.Add(member.Name);
            return labels;
        }
        private void UpdateSeats(List<string> labels)
        {
            seats.Clear();Rectangle[] boxes=SectHallLayout.Seats(ClientSize,labels.Count,Math.Max(1,Font.Height/16f));
            for(int i=0;i<labels.Count;i++)seats[labels[i]]=boxes[i];
        }
        private void DrawDesk(Graphics g,int cx,int feet,int width,bool working)
        {
            int left=cx-width/2,top=feet-57;
            using(var shadow=new SolidBrush(Color.FromArgb(85,7,17,18)))g.FillEllipse(shadow,left-8,feet-12,width+16,18);
            // Layered rosewood desk: beveled top, apron, brass drawer pulls and carved legs.
            Fill(g,Color.FromArgb(47,26,24),left+9,top+15,10,44);Fill(g,Color.FromArgb(47,26,24),left+width-19,top+15,10,44);
            Fill(g,Color.FromArgb(139,89,48),left+10,top+18,3,36);Fill(g,Color.FromArgb(139,89,48),left+width-17,top+18,3,36);
            Fill(g,Color.FromArgb(72,38,31),left+5,top+12,width-10,26);
            Fill(g,Color.FromArgb(115,64,40),left+9,top+17,width-18,16);
            Fill(g,Color.FromArgb(49,29,26),cx-2,top+17,4,16);
            Fill(g,Color.FromArgb(214,166,83),cx-width/4-5,top+22,10,3);Fill(g,Color.FromArgb(214,166,83),cx+width/4-5,top+22,10,3);
            Fill(g,Color.FromArgb(60,33,28),left-4,top+6,width+8,10);
            Fill(g,Color.FromArgb(163,101,53),left-4,top,width+8,9);
            Fill(g,Color.FromArgb(221,166,89),left-4,top,width+8,2);
            Fill(g,Color.FromArgb(99,52,35),left,top+5,width,2);
            // Scroll with jade paperweight, brush and porcelain cup. No simulated task text.
            Fill(g,Color.FromArgb(127,91,52),cx-38,top-4,72,6);Fill(g,Color.FromArgb(242,219,166),cx-33,top-6,62,8);
            for(int x=cx-23;x<cx+18;x+=9)Fill(g,Color.FromArgb(151,127,91),x,top-3,5,1);
            Fill(g,Color.FromArgb(50,109,100),cx+18,top-7,14,3);
            Fill(g,Color.FromArgb(222,229,206),left+12,top-11,11,9);Fill(g,Color.FromArgb(84,115,115),left+14,top-10,7,2);
            Fill(g,Color.FromArgb(205,216,196),left+23,top-8,3,4);
            Fill(g,Color.FromArgb(30,43,45),left+width-26,top-9,12,8);
            Fill(g,Color.FromArgb(185,132,63),left+width-21,top-20,3,16);
            if(working)
            {
                int frame=(int)(elapsed.Elapsed.TotalSeconds*4)%3;
                Fill(g,Color.FromArgb(222,192,114),cx-8+frame*5,top-10,2,5);
            }
        }
        private Image Sprite(string id,string state,int tick)
        {
            if(String.IsNullOrEmpty(id))id="orbit";
            string root=Path.Combine(contentRoot,"assets");
            string testRoot=Environment.GetEnvironmentVariable("AGENT_PAL_TEST_ASSETS");
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")=="1"&&!String.IsNullOrWhiteSpace(testRoot))root=testRoot;
            PetCharacter character=null;foreach(PetCharacter available in PetCharacter.Available)if(available.Id==id){character=available;break;}
            if(character!=null&&character.UsesPackFrames)root=character.ResourceRoot;else if(id!="orbit")root=Path.Combine(root,"characters",id);
            int count = CharacterPackCatalog.FrameCount(state);
            string path = Path.Combine(root, state, (tick % count).ToString("00") + ".png");
            Image cached;
            if (spriteCache.TryGetValue(path, out cached)) return cached;
            if (!File.Exists(path)) return null;

            Image frame = PetForm.LoadDetachedImage(path);
            if (spriteCache.Count >= 256)
            {
                string oldest = spriteOrder.Dequeue();
                spriteCache[oldest].Dispose();
                spriteCache.Remove(oldest);
            }
            spriteCache.Add(path, frame);
            spriteOrder.Enqueue(path);
            return frame;
        }
        private void Inspect(object sender,MouseEventArgs e)
        {
            UpdateSeats(Labels(selected(),record()));
            SectRun run=record();if(run==null){details("尚未执行：请先在Agent 团队配置中保存任务，再确认执行。\n工作区中的 Agent当前处于待命状态。");return;}
            foreach(var actor in actorHitBoxes)if(actor.Value.Contains(e.Location)){inspectedMember=actor.Key;lastInspection=null;InspectMember(actor.Key);return;}
            foreach(var pair in seats) if(pair.Value.Contains(e.Location))
            {
                inspectedMember=pair.Key;lastInspection=null;InspectMember(pair.Key);break;
            }
        }
        private void InspectMember(string member)
        {
                SectRun run=record();if(run==null)return;
                SectStepRun latest=null;
                foreach(SectStepRun step in run.Steps)
                    if(step.Member==member&&step.Status!="pending"&&(latest==null||step.Status=="running"||
                        (latest.Status!="running"&&String.CompareOrdinal(step.Started,latest.Started)>=0)))latest=step;
                var text=new System.Text.StringBuilder(member+"\n");
                foreach(SectStepRun step in run.Steps)foreach(SectAttempt attempt in step.PreviousAttempts)
                    if(attempt.Member==member)text.AppendLine("\n任务 "+step.Id+" ["+StatusText(attempt.Status)+"]\n"+attempt.Prompt+"\n"+attempt.Output);
                foreach(SectStepRun s in run.Steps) if(s.Member==member&&s!=latest) text.AppendLine("\n任务 "+s.Id+" ["+StatusText(s.Status)+"]\n目录："+s.Directory+"\n指令：\n"+s.Prompt+"\n回复：\n"+s.Output);
                if(run.Transfers!=null) foreach(SectTransfer t in run.Transfers)
                {var a=run.Steps.Find(delegate(SectStepRun s){return s.Id==t.From;});var b=run.Steps.Find(delegate(SectStepRun s){return s.Id==t.To;});if((a!=null&&a.Member==member)||(b!=null&&b.Member==member))text.AppendLine("\n交接 "+t.From+" → "+t.To+"\n提交时间："+t.SentAt+"\n实际传递摘要：\n"+t.Text);}
                if(latest!=null)text.AppendLine("\n任务 "+latest.Id+" ["+StatusText(latest.Status)+"]\n目录："+latest.Directory+"\n指令：\n"+latest.Prompt+"\n回复：\n"+latest.Output);
                string value=text.ToString();if(value!=lastInspection){lastInspection=value;details(value);}
        }
        internal void RefreshInspection()
        {if(inspectedMember!=null){lastInspection=null;InspectMember(inspectedMember);}}
        private static void Fill(Graphics g,Color c,int x,int y,int w,int h){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
        internal static string StatusText(string status)
        {switch(status){case "running":return "执行中";case "pending":return "等待依赖 / 工位";case "success":return "已完成";case "failed":return "失败";case "timeout":return "超时";case "cancelled":return "已取消";case "blocked":return "上游失败，已阻断";case "interrupted":return "上次运行中断";default:return status;}}
        protected override void Dispose(bool disposing){if(disposing){clock.Dispose();if(background!=null)background.Dispose();foreach(Image sprite in spriteCache.Values)sprite.Dispose();spriteCache.Clear();}base.Dispose(disposing);}
    }
}
