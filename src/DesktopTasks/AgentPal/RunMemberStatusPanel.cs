// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class RunMemberStatusPanel : Control
    {
        private List<SectMemberStatus> members = new List<SectMemberStatus>();
        private readonly Dictionary<string, Image> portraits = new Dictionary<string, Image>();
        private readonly ToolTip tooltip = new ToolTip { AutoPopDelay = 15000 };
        private string signature = "", hover = "";
        private int columns = 2, cardHeight = 82;
        private bool reflowQueued;
        private int lastWidth;
        private int focusIndex;
        private string selectedName;
        internal event Action<SectMemberStatus> MemberSelected;
        internal int MemberCount { get { return members.Count; } }
        internal int AvatarCount { get { return portraits.Count; } }
        internal RunMemberStatusPanel()
        {
            Name = "RunMemberStatuses"; Dock = DockStyle.Bottom; Height = 210; BackColor = Color.FromArgb(246, 248, 245);
            DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true);
            TabStop=true;AccessibleRole=AccessibleRole.List;AccessibleName=UiLanguage.Text("成员状态");
            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                int index = Hit(e.Location); string next = index < 0 ? "" : members[index].Name + "\n" + SectMemberStatus.Label(members[index].Status) + " · " + members[index].Task + "\n" + members[index].Detail;
                if (hover != next) { hover = next; tooltip.SetToolTip(this, next); }
                Cursor = index < 0 ? Cursors.Default : Cursors.Hand;
            };
            MouseClick += delegate(object sender, MouseEventArgs e) { int index = Hit(e.Location); if (e.Button == MouseButtons.Left && index >= 0){Focus();SelectMember(index);} };
            KeyDown+=delegate(object sender,KeyEventArgs e)
            {
                if(members.Count==0)return;
                if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){SelectMember(focusIndex);e.Handled=true;return;}
                int delta=e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:e.KeyCode==Keys.Up?-columns:e.KeyCode==Keys.Down?columns:0;
                if(delta!=0){focusIndex=Math.Max(0,Math.Min(members.Count-1,focusIndex+delta));e.Handled=true;Invalidate();}
            };
        }
        internal void Present(List<SectMemberStatus> value)
        {
            var text = new System.Text.StringBuilder(); foreach (SectMemberStatus member in value) text.Append(member.Id).Append('|').Append(member.Name).Append('|').Append(member.CharacterId).Append('|').Append(member.Status).Append('|').Append(member.Task).Append('|').Append(member.Detail).Append('\n');
            if (text.ToString() == signature) return;
            signature = text.ToString(); members = value;
            focusIndex=Math.Max(0,Math.Min(focusIndex,members.Count-1));AccessibleDescription=signature;
            foreach (SectMemberStatus member in members) Portrait(member.CharacterId);
            Visible = members.Count > 0; Reflow(); Invalidate();
        }
        private void SelectMember(int index){focusIndex=index;selectedName=members[index].Name;Invalidate();if(MemberSelected!=null)MemberSelected(members[index]);}
        protected override bool IsInputKey(Keys keyData){Keys key=keyData&Keys.KeyCode;return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(keyData);}
        internal void Reflow()
        {
            columns = Math.Max(1, Math.Min(Math.Max(1, members.Count), Math.Min(4, Math.Max(2, Width / (members.Count > 4 ? 178 : 240)))));
            if(members.Count==4&&Width<960)columns=2;
            int rows = Math.Max(1, (members.Count + columns - 1) / columns);
            int available = Parent == null ? 400 : Math.Max(200, Parent.ClientSize.Height - 205);
            cardHeight = Math.Max(Font.Height * 2 + 10, Math.Min(92, (available - 35) / rows - 6));
            int desired = 35 + rows * (cardHeight + 6);
            if (Height != desired) Height = desired;
        }
        protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(members!=null)Reflow();Invalidate();}
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (lastWidth == Width) return; lastWidth = Width;
            if (!IsHandleCreated || reflowQueued) return;
            reflowQueued = true;
            BeginInvoke((Action)delegate { reflowQueued = false; if (IsDisposed) return; Reflow(); if (Parent != null) Parent.PerformLayout(); });
        }
        internal Rectangle CardBounds(int index)
        {
            int width = Math.Max(1, (Width - 16 - (columns - 1) * 8) / columns);
            return new Rectangle(8 + index % columns * (width + 8), 31 + index / columns * (cardHeight + 6), width, cardHeight);
        }
        private int Hit(Point point) { for (int i = 0; i < members.Count; i++) if (CardBounds(i).Contains(point)) return i; return -1; }
        private Image Portrait(string id)
        {
            if (String.IsNullOrEmpty(id)) return null;
            Image image; if (portraits.TryGetValue(id, out image)) return image;
            foreach (PetCharacter character in PetCharacter.Available) if (character.Id == id)
            {
                try { if (File.Exists(character.AvatarPath)) using (Image source = Image.FromFile(character.AvatarPath)) image = AppIcon.CreateBitmap(source, 64); }
                catch (IOException) { } catch (ArgumentException) { }
                if (image != null) portraits[id] = image;
                return image;
            }
            return null;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias; graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            TextRenderer.DrawText(graphics, UiLanguage.Format("成员状态 · {0} 位", members.Count), Font, new Rectangle(10, 5, Math.Max(1, Width - 20), 24), Color.FromArgb(42, 76, 65),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            using (var title = new Font(Font, FontStyle.Bold))
            for (int i = 0; i < members.Count; i++)
            {
                SectMemberStatus member = members[i]; Rectangle card = CardBounds(i);
                using (var fill = new SolidBrush(Color.White)) graphics.FillRectangle(fill, card);
                using (var border = new Pen(selectedName==member.Name?Color.FromArgb(43,108,88):Color.FromArgb(218, 226, 219),selectedName==member.Name?2:1)) graphics.DrawRectangle(border, card);
                if(Focused&&focusIndex==i)ControlPaint.DrawFocusRectangle(graphics,Rectangle.Inflate(card,-3,-3));
                bool compact=card.Height<Font.Height*3+18;
                int portraitSize = Math.Max(20,Math.Min(compact?30:42, card.Height - Font.Height - 24)), x = card.Left + 9, y = card.Top + (compact?7:10);
                Image image = Portrait(member.CharacterId);
                if (image != null) graphics.DrawImage(image, x, y, portraitSize, portraitSize);
                else { using (var fill = new SolidBrush(Color.FromArgb(232, 238, 229))) graphics.FillEllipse(fill, x, y, portraitSize, portraitSize); TextRenderer.DrawText(graphics, member.Name.Substring(0, 1), title, new Rectangle(x, y, portraitSize, portraitSize), Color.DarkSlateGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
                int left = x + portraitSize + 9, width = Math.Max(1, card.Right - left - 8), line = Font.Height + 1;
                const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, UiLanguage.Text(member.Name), title, new Rectangle(left, y, width, line), Color.FromArgb(35, 59, 49), flags);
                Color color = member.Status == "running" ? Color.FromArgb(29, 113, 85) : member.Status == "failed" || member.Status == "needs-user" ? Color.FromArgb(177, 67, 48) : Color.FromArgb(88, 109, 96);
                string state=member.Status=="needs-user"?UiLanguage.Text("需要处理"):SectMemberStatus.Label(member.Status);
                if(compact)TextRenderer.DrawText(graphics,state,Font,new Rectangle(card.Left+9,card.Bottom-line-5,Math.Max(1,card.Width-18),line),color,flags);
                else
                {
                    TextRenderer.DrawText(graphics,state,Font,new Rectangle(left,y+line,width,line),color,flags);
                    TextRenderer.DrawText(graphics,member.Detail.Length==0?UiLanguage.Text("暂无执行输出"):member.Detail,Font,new Rectangle(card.Left+9,card.Bottom-line-7,Math.Max(1,card.Width-18),line),Color.FromArgb(105,116,108),flags);
                }
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) { tooltip.Dispose(); foreach (Image image in portraits.Values) image.Dispose(); portraits.Clear(); } base.Dispose(disposing); }
    }
}
