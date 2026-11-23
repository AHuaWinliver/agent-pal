// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class AboutForm:AppWindow
    {
        private readonly AboutSurface page=new AboutSurface();
        private readonly Panel scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};
        private readonly FlowLayoutPanel footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=64,Padding=new Padding(20,12,20,12),FlowDirection=FlowDirection.RightToLeft};
        private readonly Button close=new Button {Width=90,Height=34,DialogResult=DialogResult.Cancel};
        private readonly LinkLabel repository=new LinkLabel {AutoSize=true,Margin=new Padding(14,9,16,0)};
        private readonly LinkLabel licenses=new LinkLabel {AutoSize=true,Margin=new Padding(14,9,16,0)};
        private bool fitting;
        internal AboutForm()
        {
            Name="AboutAgentPal";StartPosition=FormStartPosition.CenterScreen;
            ClientSize=new Size(880,740);MinimumSize=new Size(640,480);AutoScaleMode=AutoScaleMode.Dpi;
            Font=UiText.Font(9);BackColor=Color.FromArgb(238,239,234);
            MaximizeBox=true;MinimizeBox=true;ShowInTaskbar=true;
            page.Location=Point.Empty;scroll.Controls.Add(page);Controls.Add(scroll);Controls.Add(footer);
            footer.Controls.Add(close);footer.Controls.Add(repository);footer.Controls.Add(licenses);
            foreach(var link in new[]{repository,licenses}){link.LinkColor=Color.FromArgb(93,98,89);link.ActiveLinkColor=Color.FromArgb(190,97,69);link.VisitedLinkColor=link.LinkColor;link.LinkBehavior=LinkBehavior.HoverUnderline;}
            CancelButton=close;repository.LinkClicked+=delegate{Open("https://github.com/AHuaWinliver/agent-pal");};
            licenses.LinkClicked+=delegate{string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ASSET-LICENSE.md");Open(File.Exists(file)?file:"https://github.com/AHuaWinliver/agent-pal/blob/main/ASSET-LICENSE.md");};
            scroll.Resize+=delegate{FitPage();};UiLanguage.Changed+=Localize;Localize();FitPage();
        }
        private void FitPage()
        {
            if(fitting)return;fitting=true;
            try
            {
                scroll.AutoScroll=false;scroll.AutoScrollMinSize=Size.Empty;page.Location=Point.Empty;
                page.Width=Math.Max(1,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8);
                page.Height=(int)Math.Ceiling(page.Width*680.0/840);
                scroll.AutoScroll=true;scroll.AutoScrollMinSize=new Size(0,page.Height);
                scroll.PerformLayout();
            }
            finally{fitting=false;}
        }
        private void Localize(){Text=UiLanguage.IsEnglish?"About AgentPal":"关于 AgentPal";close.Text=UiLanguage.IsEnglish?"Close":"关闭";repository.Text=UiLanguage.IsEnglish?"GitHub repository":"GitHub 仓库";licenses.Text=UiLanguage.IsEnglish?"Licenses & credits":"许可与致谢";page.Invalidate();}
        private void Open(string target)
        {
            try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        protected override void Dispose(bool disposing){if(disposing)UiLanguage.Changed-=Localize;base.Dispose(disposing);}

        private sealed class AboutSurface:Control
        {
            private readonly Image mark;
            private readonly Image spirit;
            private readonly List<Image> faces=new List<Image>();
            private static readonly string[] ids=PetCharacter.DefaultDisplayIds;
            private static readonly string[] english={"Qingheng","Feijian","Cangzhi","Wukong","Bailong","Xiaomo"};
            private static readonly string[] chinese={"青衡","绯笺","苍芷","行者悟空","小白龙","小墨"};
            private static readonly string[] poemsEn={"A compass for the road.\nA calm hand for today.","Gather a little spark.\nBrighten the everyday.","Let the leaves unfold.\nLet your thoughts grow.","A staff to part the mist.\nA friend for every climb.","Follow the flowing water.\nFind a steady way.","Small steps, steady heart.\nA little calm beside you."};
            private static readonly string[] poemsZh={"罗盘指向远山，\n也替你照看眼前。","一笺收好灵感，\n一笔点亮寻常。","听草木舒展，\n陪思绪慢慢生长。","一棒拨开迷雾，\n与你踏过千重山。","云水自有归处，\n与你从容同行。","慢慢走，稳稳当当，\n把日子陪成安心。"};
            internal AboutSurface()
            {
                DoubleBuffered=true;BackColor=Color.FromArgb(238,239,234);
                AccessibleName="AgentPal brand introduction";AccessibleRole=AccessibleRole.StaticText;
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentPal.Brand.IntroMark.png"))
                    if(stream!=null)using(var image=Image.FromStream(stream))mark=new Bitmap(image);
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentPal.Brand.Spirit.png"))
                    if(stream!=null)using(var image=Image.FromStream(stream))spirit=new Bitmap(image);
                foreach(string id in ids)faces.Add(CompanionAvatar.Load(id,192));
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);Graphics g=e.Graphics;g.ScaleTransform(Width/840f,Width/840f);
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                bool en=UiLanguage.IsEnglish;
                Draw(g,"AGENTPAL / "+(en?"ABOUT":"品牌与伙伴"),38,28,764,24,13,Color.FromArgb(106,110,102),false);
                Draw(g,"v"+typeof(Program).Assembly.GetName().Version.ToString(3),717,28,85,24,13,Color.FromArgb(106,110,102),false,true);
                if(mark!=null)g.DrawImage(mark,new Rectangle(38,82,138,138));
                if(BrandAssets.Wordmark!=null)g.DrawImage(BrandAssets.Wordmark,new Rectangle(206,87,510,104));
                else Draw(g,"AgentPal",206,91,510,65,42,Color.Black,true);
                Draw(g,en?"An AI companion, with a terminal at heart.":"以终端为心，让 AI 成为身边的伙伴。",208,193,580,40,18,Color.FromArgb(93,98,89),false);
                Line(g,38,254,802,254);
                string[] titles=en?new[]{"Agent + Pal","AP meets >_","The little pal"}:new[]{"Agent + Pal","AP 与终端","中间的小精灵"};
                string[] descriptions=en?new[]{"Agent capability.\nEveryday companionship.","A recognizable monogram\nwith an integrated terminal.","A little pal inside the A,\nbringing the terminal to life."}:new[]{"Agent 的能力，\nPal 的日常陪伴。","AP 字母与终端提示符相融，\n连接行动与交互。","A 中央藏着一只小精灵，\n让终端也拥有伙伴感。"};
                for(int i=0;i<3;i++)
                {
                    float x=38+i*260;Draw(g,"0"+(i+1),x,279,240,20,12,Color.FromArgb(121,124,116),false);
                    Draw(g,titles[i],x,309,i==2?183:245,34,22,Color.FromArgb(23,23,23),true);
                    Draw(g,descriptions[i],x,351,245,60,15,Color.FromArgb(93,98,89),false);
                }
                if(spirit!=null)
                    using(var familyFont=UiText.Font(9))
                    using(var titleFont=new Font(familyFont.FontFamily,22,FontStyle.Bold,GraphicsUnit.Pixel))
                    {
                        float titleWidth=g.MeasureString(titles[2],titleFont,Int32.MaxValue,StringFormat.GenericTypographic).Width;
                        g.DrawImage(spirit,new RectangleF(558+titleWidth+8,306,30,32));
                    }
                Line(g,38,431,802,431);
                Draw(g,en?"SIX PALS":"六位伙伴",38,452,764,24,12,Color.FromArgb(106,110,102),false);
                for(int i=0;i<faces.Count;i++)
                {
                    float cell=764f/faces.Count,x=38+i*cell;
                    if(faces[i]!=null){bool turtle=ids[i]=="pippin";float size=turtle?82.8f:92f;float y=500+92-size;g.DrawImage(faces[i],new RectangleF(x+(cell-size)/2,y,size,size));}
                    Draw(g,en?english[i]:chinese[i],x,601,cell,24,13,Color.FromArgb(60,65,58),false,true);
                    Draw(g,en?poemsEn[i]:poemsZh[i],x,629,cell,44,11f,Color.FromArgb(105,111,101),false,true);
                }
                string version=typeof(Program).Assembly.GetName().Version.ToString(3);
                AccessibleDescription="AgentPal "+version+". "+String.Join(" ",descriptions)+" "+String.Join(" ",en?poemsEn:poemsZh);
            }
            private static void Line(Graphics g,float x,float y,float right,float bottom){using(var pen=new Pen(Color.FromArgb(210,214,205)))g.DrawLine(pen,x,y,right,bottom);}
            private static void Draw(Graphics g,string text,float x,float y,float width,float height,float size,Color color,bool bold,bool centered=false)
            {
                using(var familyFont=UiText.Font(9))
                using(var font=new Font(familyFont.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(color))using(var format=new StringFormat {Alignment=centered?StringAlignment.Center:StringAlignment.Near})
                    g.DrawString(text,font,brush,new RectangleF(x,y,width,height),format);
            }
            protected override void Dispose(bool disposing){if(disposing){if(mark!=null)mark.Dispose();if(spirit!=null)spirit.Dispose();foreach(var face in faces)if(face!=null)face.Dispose();}base.Dispose(disposing);}
        }
    }
}
