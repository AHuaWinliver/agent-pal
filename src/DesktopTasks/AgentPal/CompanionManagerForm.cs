// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class CompanionManagerForm:AppWindow
    {
        private readonly ListBox list=new ListBox {Dock=DockStyle.Fill,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=42,IntegralHeight=false,BorderStyle=BorderStyle.None};
        private readonly Label detail=new Label {AutoSize=true};
        private readonly Panel detailScroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};
        private readonly Label heading=new Label {Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},kind=new Label {Dock=DockStyle.Fill},hint=new Label {Dock=DockStyle.Fill};
        private readonly Button use=new Button {Width=124,Height=32},remove=new Button {Width=128,Height=32},colors=new Button {Width=116,Height=32};
        private readonly Action<PetCharacter> activate,delete;
        private readonly Action appearanceChanged;
        private readonly Func<string> currentId;
        internal CompanionManagerForm(Func<string> current,Action<PetCharacter> select,Action<PetCharacter> erase,Action updateAppearance)
        {
            currentId=current;activate=select;delete=erase;appearanceChanged=updateAppearance;
            Name="CompanionManager";Text=UiLanguage.Text("伙伴管理");Font=UiText.Font(10);StartPosition=FormStartPosition.CenterParent;
            AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(600,300);MinimumSize=new Size(560,320);BackColor=Color.FromArgb(248,249,246);
            var bottom=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=54,Padding=new Padding(8),FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            var close=new Button {Text=UiLanguage.Text("关闭"),Width=76,Height=32,DialogResult=DialogResult.Cancel};CancelButton=close;
            use.Text=UiLanguage.Text("使用此伙伴");remove.Text=UiLanguage.Text("删除已导入伙伴");colors.Text=UiLanguage.Text("轨迹配色…");
            bottom.Controls.Add(close);bottom.Controls.Add(use);bottom.Controls.Add(colors);bottom.Controls.Add(remove);
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(8,10,12,0),BackColor=BackColor};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,184));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            list.BackColor=BackColor;list.Font=UiText.Font(9);list.Margin=new Padding(0,0,12,0);
            var info=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=new Padding(4,0,0,0)};
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));info.RowStyles.Add(new RowStyle(SizeType.Absolute,30));info.RowStyles.Add(new RowStyle(SizeType.Absolute,26));info.RowStyles.Add(new RowStyle(SizeType.Percent,100));info.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
            heading.Font=UiText.Font(12,FontStyle.Bold);heading.ForeColor=Color.FromArgb(35,49,42);kind.Font=hint.Font=UiText.Font(8.5f);kind.ForeColor=hint.ForeColor=Color.FromArgb(107,119,110);
            detail.BackColor=BackColor;detail.ForeColor=Color.FromArgb(57,68,60);detailScroll.Margin=new Padding(3,4,3,10);detailScroll.Controls.Add(detail);
            detailScroll.Resize+=delegate{detail.MaximumSize=new Size(Math.Max(1,detailScroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-2),0);};
            info.Controls.Add(heading,0,0);info.Controls.Add(kind,0,1);info.Controls.Add(detailScroll,0,2);info.Controls.Add(hint,0,3);
            body.Controls.Add(list,0,0);body.Controls.Add(info,1,0);Controls.Add(body);Controls.Add(bottom);
            list.DrawItem+=DrawItem;list.SelectedIndexChanged+=delegate{ShowSelected();};
            use.Click+=delegate{var p=list.SelectedItem as PetCharacter;if(p!=null){activate(p);ShowSelected();list.Invalidate();}};
            colors.Click+=delegate
            {
                var p=list.SelectedItem as PetCharacter;if(p==null||!p.IsExternal)return;
                using(var dialog=new TrailColorForm(p))if(dialog.ShowDialog(this)==DialogResult.OK){appearanceChanged();ShowSelected();}
            };
            remove.Click+=delegate
            {
                var p=list.SelectedItem as PetCharacter;if(p==null||!p.IsExternal)return;
                if(MessageBox.Show(this,UiLanguage.Format("删除“{0}”的已导入资源？对话记录、任务与配置会保留。",p.Name),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
                try{delete(p);Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
            };
            Reload();
        }
        private void Reload(){list.Items.Clear();foreach(var p in PetCharacter.Available)list.Items.Add(p);for(int i=0;i<list.Items.Count;i++)if(((PetCharacter)list.Items[i]).Id==currentId())list.SelectedIndex=i;if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;ShowSelected();}
        private void ShowSelected()
        {
            var p=list.SelectedItem as PetCharacter;use.Enabled=p!=null&&p.Id!=currentId();remove.Enabled=colors.Enabled=p!=null&&p.IsExternal;
            heading.Text=p==null?"":UiLanguage.Text(p.Name);kind.Text=p==null?"":UiLanguage.Text(p.IsExternal?"已导入伙伴":"内置伙伴");
            detail.Text=p==null?"":PersonaDescription(p);
            detailScroll.AutoScrollPosition=Point.Empty;
            hint.Text=p==null?"":UiLanguage.Text(p.IsExternal?"轨迹配色按伙伴保存，刷新资源或重启后仍然保留。":"内置伙伴受到保护，不能删除。可删除的已导入伙伴排列在列表末尾。");
        }
        private static string PersonaDescription(PetCharacter p)
        {
            if(!UiLanguage.IsEnglish||p.IsExternal)return p.Persona??"";
            switch(p.Id)
            {
                case "orbit":return "A calm, dependable traveller with a compass at his side. Qingheng brings clear thinking and steady encouragement to everyday conversations and complex tasks.";
                case "spark":return "A bright, attentive craftswoman who collects ideas on little slips of paper. Feijian turns scattered thoughts into clear plans, with a warm touch and an eye for detail.";
                case "moss":return "A quiet herbalist who follows the rhythm of the seasons. Cangzhi makes room for focused work, thoughtful answers and gentle reminders to take a break.";
                case "sun-wukong":return "An adventurous companion with a quick mind, a staff and a flying cloud. Wayfarer Wukong meets obstacles with curiosity, courage and a little mischief.";
                case "bailong":return "A gentle, quick-witted white dragon in human form. Bailong keeps track of details, brings clarity to handoffs and helps each task move forward at a steady pace.";
                case "pippin":return "A small ink-black turtle with amber eyes and a cloud-patterned shell. Xiaomo brings patient company, practical help and a reassuring sense of steady progress.";
            }
            return p.Persona??"";
        }
        private void DrawItem(object sender,DrawItemEventArgs e)
        {
            if(e.Index<0)return;var p=(PetCharacter)list.Items[e.Index];e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var background=new SolidBrush(list.BackColor))e.Graphics.FillRectangle(background,e.Bounds);
            if((e.State&DrawItemState.Selected)!=0)using(var path=PromptForm.RoundedRectangle(new Rectangle(e.Bounds.X+2,e.Bounds.Y+2,e.Bounds.Width-4,e.Bounds.Height-4),8))using(var fill=new SolidBrush(Color.FromArgb(224,236,228)))e.Graphics.FillPath(fill,path);
            using(var image=CompanionAvatar.Load(p.Id,32))if(image!=null)e.Graphics.DrawImage(image,e.Bounds.X+8,e.Bounds.Y+5,32,32);
            TextRenderer.DrawText(e.Graphics,UiLanguage.Text(p.Name),list.Font,new Rectangle(e.Bounds.X+47,e.Bounds.Y,e.Bounds.Width-62,e.Bounds.Height),Color.FromArgb(48,62,53),TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
            if(p.Id==currentId())using(var dot=new SolidBrush(Color.FromArgb(53,122,93)))e.Graphics.FillEllipse(dot,e.Bounds.Right-11,e.Bounds.Y+18,5,5);
            if((e.State&DrawItemState.Focus)!=0&&list.Focused)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(e.Bounds,-4,-4),Color.FromArgb(90,120,99),list.BackColor);
        }
    }
    internal sealed class TrailColorForm:AppWindow
    {
        private readonly Button[] swatches=new Button[4];private readonly Panel preview=new Panel {Height=66,Dock=DockStyle.Top};private Color[] selected;
        private bool resetToDefault;
        internal TrailColorForm(PetCharacter companion)
        {
            Name="TrailColors";Text=UiLanguage.Text("轨迹配色")+" · "+companion.Name;Font=UiText.Font(10);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(440,270);MinimumSize=Size;MaximumSize=Size;StartPosition=FormStartPosition.CenterParent;
            selected=CompanionAppearance.Colors(companion.Id);if(selected.Length!=4)selected=new[]{selected[0],selected[1],selected[2%selected.Length],selected[3%selected.Length]};
            var hint=new Label {Text=UiLanguage.Text("点击色块选择颜色；配色会独立保存到当前伙伴。"),Dock=DockStyle.Top,Height=46,Padding=new Padding(14,12,8,0)};
            var row=new FlowLayoutPanel {Dock=DockStyle.Top,Height=60,Padding=new Padding(12,6,0,0)};
            for(int i=0;i<4;i++){int index=i;var button=new TrailColorSwatch {Name="TrailColor"+i,Width=96,Height=40,UseVisualStyleBackColor=false};swatches[i]=button;row.Controls.Add(button);button.Click+=delegate{using(var chooser=new ColorDialog {Color=selected[index],FullOpen=true})if(chooser.ShowDialog(this)==DialogResult.OK){resetToDefault=false;selected[index]=chooser.Color;RefreshColors();}};}
            var actions=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=64,Padding=new Padding(10),FlowDirection=FlowDirection.RightToLeft};
            Button save=new Button {Text=UiLanguage.Text("保存"),Width=90,Height=34},cancel=new Button {Text=UiLanguage.Text("取消"),Width=90,Height=34,DialogResult=DialogResult.Cancel},reset=new Button {Text=UiLanguage.Text("恢复默认"),Width=160,Height=34};
            save.Click+=delegate{try{CompanionAppearance.Save(companion.Id,resetToDefault?null:selected);DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};
            reset.Click+=delegate{resetToDefault=true;selected=PetBrain.DefaultColors(companion.Id);if(selected.Length!=4)selected=new[]{selected[0],selected[1],selected[2],selected[3]};RefreshColors();};
            actions.Controls.Add(save);actions.Controls.Add(cancel);actions.Controls.Add(reset);AcceptButton=save;CancelButton=cancel;
            preview.Paint+=delegate(object sender,PaintEventArgs e){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;for(int i=0;i<4;i++)using(var pen=new Pen(selected[i],7))e.Graphics.DrawBezier(pen,24,39-i*5,140,5+i*7,260,58-i*6,410,20+i*5);};
            Controls.Add(preview);Controls.Add(row);Controls.Add(hint);Controls.Add(actions);RefreshColors();
        }
        private void RefreshColors(){for(int i=0;i<4;i++){swatches[i].BackColor=selected[i];swatches[i].ForeColor=(.299*selected[i].R+.587*selected[i].G+.114*selected[i].B)<155?Color.White:Color.Black;swatches[i].Text="#"+(selected[i].ToArgb()&0xffffff).ToString("X6");}preview.Invalidate();}
    }
    internal sealed class TrailColorSwatch:Button
    {
        internal TrailColorSwatch(){FlatStyle=FlatStyle.Flat;Cursor=Cursors.Hand;}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var path=PromptForm.RoundedRectangle(new Rectangle(1,1,Width-3,Height-3),8))using(var fill=new SolidBrush(BackColor))using(var pen=new Pen(Color.FromArgb(180,187,180))){e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8),ForeColor,BackColor);
        }
    }
    internal static class CharacterPackRemoval
    {
        internal static void Remove(string root,PetCharacter companion)
        {
            if(companion==null||!companion.IsExternal||!CharacterPackCatalog.ValidId(companion.Id))throw new InvalidOperationException(UiLanguage.Text("内置伙伴不能删除。"));
            root=Path.GetFullPath(root);string target=Path.GetFullPath(companion.ResourceRoot),expected=Path.Combine(root,companion.Id);
            if(!String.Equals(target.TrimEnd(Path.DirectorySeparatorChar),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Resource folder is outside the companion directory.");
            Validate(target,true);string staged=Path.Combine(root,".deleting-"+Guid.NewGuid().ToString("N"));
            // Both resolved absolute paths are direct children of the verified root.
            // Validate every descendant before moving; never traverse a junction.
            Directory.Move(target,staged);
            try{Validate(staged,true);Directory.Delete(staged,true);}
            catch{if(!Directory.Exists(target)&&Directory.Exists(staged))Directory.Move(staged,target);throw;}
        }
        private static void Validate(string path,bool top)
        {
            CharacterPackCatalog.CheckPath(path);
            foreach(string file in Directory.GetFiles(path))
            {
                CharacterPackCatalog.CheckPath(file);string name=Path.GetFileName(file);
                bool allowed=top?(name=="character.json"||name=="README.md"||name=="LICENSE.txt"||name==CharacterPackCatalog.WingsFile||name==CharacterPackCatalog.PlatformFile):System.Text.RegularExpressions.Regex.IsMatch(name,@"\A[0-9]{2}\.png\z");
                if(!allowed)throw new InvalidDataException("Unexpected file in resource folder: "+name);
            }
            foreach(string folder in Directory.GetDirectories(path))
            {if(!top||Array.IndexOf(CharacterPackCatalog.States,Path.GetFileName(folder))<0)throw new InvalidDataException("Unexpected resource subfolder.");Validate(folder,false);}
        }
    }
}
