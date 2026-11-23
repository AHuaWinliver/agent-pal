// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace AgentPal
{
    internal sealed class CliProcessView:Panel
    {
        private readonly Button toggle=new Button {FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,Cursor=Cursors.Hand};
        private readonly Label summary=new Label {AutoEllipsis=true};
        private readonly UnicodeRichTextBox details=new UnicodeRichTextBox {Name="CliProcessDetails",ReadOnly=true,BorderStyle=BorderStyle.None,WordWrap=true,ScrollBars=RichTextBoxScrollBars.Vertical,DetectUrls=false};
        private string shown="";
        private bool expanded;
        private readonly Timer motion=new Timer {Interval=15};
        private readonly Stopwatch clock=new Stopwatch();
        private int fromHeight,toHeight,tweenHeight=-1;
        internal event Action ExpandedChanged;
        internal bool Expanded {get{return expanded;}}
        internal CliProcessView()
        {
            BackColor=Color.FromArgb(241,240,235);
            toggle.Font=UiText.Font(8.5f,FontStyle.Bold);toggle.ForeColor=Color.FromArgb(118,78,53);toggle.FlatAppearance.BorderSize=0;
            summary.Font=UiText.ContentFont(8.5f);summary.ForeColor=Color.FromArgb(100,100,95);
            details.Font=UiText.ContentFont(9f);details.BackColor=BackColor;details.ForeColor=Color.FromArgb(78,80,74);
            Controls.Add(toggle);Controls.Add(summary);Controls.Add(details);
            toggle.Click+=delegate
            {
                motion.Stop();fromHeight=Height;expanded=!expanded;toHeight=expanded?232:58;
                if(UiMotion.Disabled){tweenHeight=-1;Arrange(Width);if(ExpandedChanged!=null)ExpandedChanged();return;}
                tweenHeight=fromHeight;clock.Restart();motion.Start();Arrange(Width);
            };
            motion.Tick+=delegate
            {
                double t=Math.Min(1,clock.Elapsed.TotalMilliseconds/UiMotion.Duration);
                tweenHeight=(int)Math.Round(fromHeight+(toHeight-fromHeight)*UiMotion.Ease(t));
                if(t>=1){motion.Stop();tweenHeight=-1;}Arrange(Width);if(ExpandedChanged!=null)ExpandedChanged();
            };
        }
        internal void SetText(string value)
        {
            string text=UiText.Normalize(value??"").Trim();if(text==shown)return;
            bool follow=details.SelectionLength==0;int start=details.SelectionStart,length=details.SelectionLength;
            shown=text;details.Text=text;UiText.ApplyMixedGlyphFonts(details);
            if(follow){details.Select(details.TextLength,0);details.ScrollToCaret();}
            else details.Select(Math.Min(start,details.TextLength),Math.Min(length,Math.Max(0,details.TextLength-start)));
            string[] lines=text.Split('\n');summary.Text=lines[lines.Length-1];Arrange(Width);
        }
        internal void Arrange(int width)
        {
            Width=Math.Max(80,width);Visible=shown.Length>0;
            toggle.Text=(expanded?"▾ ":"▸ ")+UiLanguage.Format("执行过程 · {0} 条",shown.Split('\n').Length);
            toggle.Bounds=new Rectangle(4,2,Math.Max(60,Width-8),26);
            summary.Bounds=new Rectangle(12,29,Math.Max(50,Width-24),24);summary.Visible=!expanded;
            details.Bounds=new Rectangle(12,32,Math.Max(50,Width-24),190);details.Visible=expanded;
            Height=shown.Length==0?0:tweenHeight>=0?tweenHeight:expanded?232:58;
        }
        internal void ToggleForTest(){toggle.PerformClick();}
        protected override void Dispose(bool disposing){if(disposing)motion.Dispose();base.Dispose(disposing);}
    }
}
