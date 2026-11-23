// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class ChatMessage
    {
        internal string Role, Text, Speaker, CharacterId, Process;
        internal DateTime At = DateTime.Now;
    }
    internal sealed class ChatConversationView : Panel
    {
        private readonly Panel content = new Panel();
        private readonly List<ChatMessageRow> rows = new List<ChatMessageRow>();
        private ActivityRecord record;
        private string fallbackText;
        private ChatMessageRow typing;
        private bool showTyping;
        private readonly bool cliLayout;
        internal bool UsesCliLayout {get{return cliLayout;}}
        internal ChatConversationView(bool useCliLayout=false)
        {
            cliLayout=useCliLayout;
            DoubleBuffered = true; AutoScroll = true; BackColor = Color.FromArgb(250,251,249);
            if(cliLayout)BackColor=Color.FromArgb(252,251,248);
            content.BackColor=BackColor;Controls.Add(content);
            Resize += delegate { LayoutRows(false); };
        }
        internal int MessageCount { get { return rows.Count; } }
        internal bool HasBothSides { get { return rows.Exists(delegate(ChatMessageRow r){return r.IsUser;})&&rows.Exists(delegate(ChatMessageRow r){return !r.IsUser;}); } }
        internal void FollowMessage(ChatMessage message)
        {
            ChatMessageRow row=rows.Find(delegate(ChatMessageRow item){return Object.ReferenceEquals(item.Message,message);});
            if(row==null)return;
            LayoutRows(false);
            AutoScrollPosition=new Point(0,Math.Max(0,row.Bottom-ClientSize.Height+8));
            row.FollowLatestText();
        }
        internal void Present(ActivityRecord activity)
        {
            bool changed=!Object.ReferenceEquals(record,activity),follow=changed||-AutoScrollPosition.Y+ClientSize.Height>=content.Height-40;
            if(changed) { ClearRows();record=activity;fallbackText=null; }
            if(activity==null){ClearRows();return;}
            IList<ChatMessage> messages=activity.Messages;
            if(messages.Count==0)
            {
                // Legacy/external output has no trustworthy role delimiters: never guess from “你：”.
                string text=ActivityTrayForm.NormalizeOutput(activity.Output);
                if(text.Length==0)text=!String.IsNullOrEmpty(activity.StatusDetail)?activity.StatusDetail:activity.State=="正在运行"?"正在准备回复…":"暂无回复";
                if(rows.Count==0)rows.Add(AddRow(new ChatMessage {Role="assistant",Text=text,At=activity.Updated},activity.CharacterId));
                else if(fallbackText!=text)rows[0].SetText(text);
                fallbackText=text;
            }
            else
            {
                bool rebuild=rows.Count>messages.Count;
                for(int i=0;!rebuild&&i<rows.Count;i++)rebuild=!Object.ReferenceEquals(rows[i].Message,messages[i]);
                if(rebuild)ClearRows();
                for(int i=0;i<messages.Count;i++)
                {
                    if(i>=rows.Count)rows.Add(AddRow(messages[i],activity.CharacterId));
                    string text=messages[i].Text;
                    if(i==messages.Count-1&&messages[i].Role=="assistant"&&String.IsNullOrEmpty(text))text=!String.IsNullOrEmpty(activity.StatusDetail)?activity.StatusDetail:activity.State=="正在运行"?"正在思考…":"本轮未收到回复。";
                    rows[i].SetText(text);
                    rows[i].SetNotice(i==messages.Count-1&&messages[i].Role=="assistant"&&!String.IsNullOrEmpty(messages[i].Text)?activity.StatusDetail:null);
                    rows[i].SetProcess(messages[i].Process);
                }
            }
            showTyping=messages.Count>0&&messages[messages.Count-1].Role=="user"&&activity.State=="正在运行";
            if(showTyping&&typing==null)typing=AddRow(new ChatMessage {Role="assistant",Text="正在思考…"},activity.CharacterId);
            if(showTyping&&typing!=null)typing.SetText(!String.IsNullOrEmpty(activity.StatusDetail)?activity.StatusDetail:"正在思考…");
            if(typing!=null)typing.Visible=showTyping;
            LayoutRows(follow);
        }
        private ChatMessageRow AddRow(ChatMessage message,string characterId)
        {var row=new ChatMessageRow(message,characterId,cliLayout);row.LayoutChanged+=delegate{LayoutRows(false);};content.Controls.Add(row);return row;}
        private void ClearRows(){foreach(ChatMessageRow row in rows)row.Dispose();rows.Clear();if(typing!=null){typing.Dispose();typing=null;}showTyping=false;content.Height=0;}
        private void LayoutRows(bool follow)
        {
            if(IsDisposed)return;int scroll=-AutoScrollPosition.Y;
            int width=Math.Max(240,ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4),y=12;
            content.SuspendLayout();
            foreach(ChatMessageRow row in rows){row.Width=width;row.Arrange();row.Location=new Point(0,y);y+=row.Height+16;}
            if(showTyping&&typing!=null){typing.Width=width;typing.Arrange();typing.Location=new Point(0,y);y+=typing.Height+16;}
            content.Size=new Size(width,y+8);content.ResumeLayout(false);
            AutoScrollMinSize=new Size(0,content.Height);
            AutoScrollPosition=new Point(0,follow?Math.Max(0,content.Height-ClientSize.Height):scroll);
        }
        internal bool UnicodeSupportedForTest()
        {
            foreach(ChatMessageRow row in rows)if(!row.UnicodeSupported)return false;return rows.Count>0;
        }
    }
    internal sealed class ChatMessageRow : Panel
    {
        internal readonly ChatMessage Message;
        internal bool IsUser { get { return Message.Role=="user"; } }
        private readonly ChatBubbleSurface card=new ChatBubbleSurface();
        private readonly UnicodeRichTextBox body=new UnicodeRichTextBox();
        private readonly Label process=new Label {ForeColor=Color.FromArgb(90,109,98),BackColor=Color.FromArgb(239,245,241),Font=UiText.Font(8.5f)};
        private readonly ToolTip processTips=new ToolTip();
        private readonly Label label=new Label();
        private readonly Label notice=new Label {Visible=false,AutoSize=false,ForeColor=Color.FromArgb(154,55,39),BackColor=Color.FromArgb(255,240,230),Font=UiText.Font(9)};
        private readonly PictureBox avatar=new PictureBox();
        private readonly string characterId;
        private readonly bool cliLayout;
        private readonly CliProcessView cliProcess;
        internal event Action LayoutChanged;
        private string shown;
        private int naturalHeight=28;
        private bool formatting;
        [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        internal ChatMessageRow(ChatMessage message,string id,bool useCliLayout=false)
        {
            cliLayout=useCliLayout;
            Message=message;characterId=message.CharacterId??id??"";
            DoubleBuffered=true;BackColor=Color.FromArgb(250,251,249);
            card.BackColor=BackColor;card.FillColor=IsUser?Color.FromArgb(224,239,231):Color.White;card.BorderColor=IsUser?Color.FromArgb(212,229,219):Color.FromArgb(232,235,230);
            body.ReadOnly=true;body.BorderStyle=BorderStyle.None;body.BackColor=card.FillColor;body.ForeColor=Color.FromArgb(37,48,42);body.Font=UiText.ContentFont(10.5f);body.DetectUrls=false;body.WordWrap=true;body.ScrollBars=RichTextBoxScrollBars.None;
            body.ContentsResized+=delegate(object sender,ContentsResizedEventArgs e){naturalHeight=Math.Max(28,e.NewRectangle.Height+5);};
            label.Font=UiText.Font(8.5f);label.ForeColor=Color.FromArgb(111,124,116);label.AutoSize=false;
            string speaker=message.Speaker??(IsUser?UiLanguage.Text("你"):characterId.Length==0?"Claude Code":TaskPresentation.Owner(characterId));
            label.Text=speaker+"   "+message.At.ToString("HH:mm");
            avatar.SizeMode=PictureBoxSizeMode.Zoom;avatar.Size=new Size(36,36);avatar.BackColor=Color.Transparent;
            if((!IsUser||message.CharacterId!=null)&&characterId.Length>0)avatar.Image=LoadAvatar(characterId);
            avatar.Paint+=delegate(object sender,PaintEventArgs e){if(avatar.Image==null){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(Color.FromArgb(57,88,74)))e.Graphics.FillEllipse(brush,0,0,34,34);TextRenderer.DrawText(e.Graphics,message.Speaker==null?(IsUser?(UiLanguage.IsEnglish?"U":"我"):"AI"):"•",label.Font,new Rectangle(0,0,34,34),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}};
            card.Controls.Add(body);card.Controls.Add(process);card.Controls.Add(notice);Controls.Add(card);Controls.Add(label);Controls.Add(avatar);
            if(cliLayout)
            {
                cliProcess=new CliProcessView();cliProcess.ExpandedChanged+=delegate{if(LayoutChanged!=null)LayoutChanged();};card.Controls.Add(cliProcess);
            }
            var menu=new ContextMenuStrip();menu.Items.Add(UiLanguage.Text("复制整条消息"),null,delegate {try{Clipboard.SetText(Message.Text??body.Text);}catch(System.Runtime.InteropServices.ExternalException){}});body.ContextMenuStrip=menu;
            SetText(message.Text);
        }
        internal bool UnicodeSupported {get{return body.UsesUnicodeRichEdit&&!UiText.HasUnpairedSurrogate(body.Text);}}
        internal void SetNotice(string text){notice.Text=UiText.Normalize(text??"");notice.Visible=notice.Text.Length>0;}
        internal void SetProcess(string text)
        {
            if(cliLayout){process.Visible=false;cliProcess.SetText(text);return;}
            process.Visible=!String.IsNullOrWhiteSpace(text);
            string[] lines=(text??"").Split('\n');
            string recent=String.Join("\n",lines,Math.Max(0,lines.Length-4),Math.Min(4,lines.Length));
            if(process.Text!=recent){process.Text=recent;processTips.SetToolTip(process,text??"");}
        }
        internal void FollowLatestText()
        {
            if(body.IsHandleCreated&&body.SelectionLength==0)SendMessage(body.Handle,0x0115,new IntPtr(7),IntPtr.Zero);
        }
        internal void SetText(string text)
        {
            string clean=UiText.Normalize(text??"");if(clean==shown)return;
            int start=body.SelectionStart,length=body.SelectionLength;bool selected=length>0;
            formatting=true;
            bool handle=body.IsHandleCreated;if(handle)SendMessage(body.Handle,0x000B,IntPtr.Zero,IntPtr.Zero);
            try
            {
                CliTextDocument document=cliLayout&&!IsUser?CliTranscriptText.Render(clean):null;
                body.Text=clean.Length==0?UiLanguage.Text("正在回复…"):document==null?clean:document.Text;
                UiText.ApplyMixedGlyphFonts(body);
                if(document==null)CliReplyFormatting.Apply(body);
                else foreach(CliTextSpan span in document.Spans)
                {
                    if(span.Length==0)continue;body.Select(span.Start,span.Length);
                    if(span.Code){body.SelectionBackColor=Color.FromArgb(239,237,230);using(var font=new Font("Consolas",body.Font.Size))body.SelectionFont=font;}
                    if(span.Strong){using(var font=UiText.Font(body.Font.Size,FontStyle.Bold))body.SelectionFont=font;}
                    if(span.Muted)body.SelectionColor=Color.FromArgb(126,122,112);
                }
                if(selected)body.Select(Math.Min(start,body.TextLength),Math.Min(length,Math.Max(0,body.TextLength-start)));
                shown=clean;
            }
            finally{formatting=false;if(handle){SendMessage(body.Handle,0x000B,new IntPtr(1),IntPtr.Zero);body.Invalidate();}}
        }
        internal void Arrange()
        {
            if(formatting)return;
            int maximum=Math.Max(130,(int)(Width*.77)),minimum=90;
            string text=shown??"";
            int measured=TextRenderer.MeasureText(text.Length>200?text.Substring(0,200):text,body.Font,new Size(maximum-28,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding).Width+30;
            int width=text.Length>200?maximum:Math.Max(minimum,Math.Min(maximum,measured));
            if(cliLayout&&!String.IsNullOrWhiteSpace(Message.Process))width=maximum;
            int processHeight=process.Visible?Math.Min(100,TextRenderer.MeasureText(process.Text,process.Font,new Size(width-28,1000),TextFormatFlags.WordBreak).Height+8):0;
            if(cliLayout){cliProcess.Arrange(width-28);processHeight=cliProcess.Visible?cliProcess.Height+10:0;cliProcess.Location=new Point(14,10);}
            int noticeHeight=notice.Visible?Math.Min(160,TextRenderer.MeasureText(notice.Text,notice.Font,new Size(width-28,1000),TextFormatFlags.WordBreak).Height+12):0;
            notice.Bounds=new Rectangle(14,12+processHeight,width-28,noticeHeight);processHeight+=noticeHeight;
            process.Bounds=new Rectangle(14,10,width-28,processHeight);
            body.Width=width-28;body.Location=new Point(14,12+processHeight);
            if(!body.IsHandleCreated)body.CreateControl();
            if(body.TextLength>0){Point last=body.GetPositionFromCharIndex(body.TextLength-1);naturalHeight=Math.Max(naturalHeight,last.Y+body.Font.Height+8);}
            int height=Math.Max(30,Math.Min(600,naturalHeight));
            body.Height=height;body.ScrollBars=naturalHeight>600?RichTextBoxScrollBars.Vertical:RichTextBoxScrollBars.None;
            int x=IsUser?Width-width-46:48;
            card.Bounds=new Rectangle(x,23,width,height+24+processHeight);
            int labelWidth=Math.Max(width,Math.Min(maximum,TextRenderer.MeasureText(label.Text,label.Font).Width+4));
            label.Bounds=new Rectangle(IsUser?x+width-labelWidth:x,0,labelWidth,20);label.TextAlign=IsUser?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft;
            avatar.Location=new Point(IsUser?Width-40:6,24);
            Height=card.Bottom+2;
        }
        private static Image LoadAvatar(string id)
        {
            return CompanionAvatar.Load(id,48);
        }
        protected override void Dispose(bool disposing){if(disposing){processTips.Dispose();if(avatar.Image!=null)avatar.Image.Dispose();if(body.ContextMenuStrip!=null)body.ContextMenuStrip.Dispose();}base.Dispose(disposing);}
    }
    internal sealed class ChatBubbleSurface : Panel
    {
        internal Color FillColor,BorderColor;
        internal ChatBubbleSurface(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath path=PromptForm.RoundedRectangle(new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),16))
            using(var fill=new SolidBrush(FillColor))using(var border=new Pen(BorderColor)){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);}
        }
    }
}
