// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace AgentPal.Installer
{
    internal sealed class InstallerForm : Form
    {
        private readonly Button install = new Button { AutoSize = true, Name = "Install" };
        private readonly Button cancel = new Button { AutoSize = true };
        private readonly TextBox destination = new TextBox { Dock = DockStyle.Fill, Name = "InstallDirectory" };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(430, 0) };
        private readonly ProgressBar progress = new InstallerProgressBar { Name = "InstallationProgress", Width = 420, Height = 14, Minimum = 0, Maximum = 100, Visible = false };
        private readonly Label percentage = new Label { Name = "InstallationPercentage", AutoSize = true, Visible = false };
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private bool installing,installed;

        internal InstallerForm()
        {
            Name = "agent-palInstaller"; Text = "AgentPal Setup";
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            ClientSize = new Size(900, 580); MinimumSize = new Size(880, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(250, 248, 244);
            var preview = new InstallerPreview { Dock = DockStyle.Fill,Margin=new Padding(20,0,0,0) };
            var content=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=2,RowCount=2};
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,44));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,112));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var header=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};
            var brand=new Panel {Name="InstallerBrand",Bounds=new Rectangle(0,0,370,66)};
            brand.Controls.Add(new PictureBox {Name="AgentPalMark",Image=BrandAssets.Mark,SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(0,0,58,58),AccessibleName="AgentPal product icon"});
            if(BrandAssets.Wordmark!=null)brand.Controls.Add(new PictureBox {Name="AgentPalWordmark",Image=BrandAssets.Wordmark,SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(74,0,250,58),AccessibleName="AgentPal"});
            else brand.Controls.Add(new Label {Text="AgentPal",AutoSize=true,Font=new Font("Segoe UI",25,FontStyle.Bold),Location=new Point(74,0)});
            language.Items.AddRange(new object[]{"English","简体中文"});language.SelectedIndex=UiLanguage.IsEnglish?0:1;
            header.Controls.Add(brand);header.Controls.Add(language);
            header.Resize+=delegate{language.Location=new Point(Math.Max(0,header.Width-language.Width),4);};
            var description=new Label {AutoSize=false,Bounds=new Rectangle(0,76,760,28),ForeColor=Color.FromArgb(74,87,78)};header.Controls.Add(description);
            var left=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            left.RowStyles.Add(new RowStyle(SizeType.Percent,100));left.RowStyles.Add(new RowStyle(SizeType.Absolute,122));
            var setup=new TableLayoutPanel {Name="InstallationOptions",Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(16),BackColor=Color.White,Margin=new Padding(0,0,0,18)};
            setup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));setup.RowStyles.Add(new RowStyle(SizeType.Absolute,28));setup.RowStyles.Add(new RowStyle(SizeType.Absolute,38));setup.RowStyles.Add(new RowStyle(SizeType.Absolute,66));setup.RowStyles.Add(new RowStyle(SizeType.Absolute,1));setup.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var location=new Label {Dock=DockStyle.Fill,Font=new Font(Font,FontStyle.Bold),Margin=Padding.Empty};
            var folder=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};folder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));folder.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));
            var browse=new Button {Dock=DockStyle.Fill,Name="BrowseInstallDirectory",Margin=new Padding(8,0,0,6)};
            destination.Text=Program.InstallDirectory;destination.Margin=new Padding(0,3,0,6);destination.BorderStyle=BorderStyle.FixedSingle;
            browse.Click+=delegate{using(var picker=new FolderBrowserDialog()){if(Directory.Exists(destination.Text))picker.SelectedPath=destination.Text;if(picker.ShowDialog(this)==DialogResult.OK)destination.Text=picker.SelectedPath;}};
            folder.Controls.Add(destination,0,0);folder.Controls.Add(browse,1,0);
            var information=new Label {Dock=DockStyle.Fill,ForeColor=Color.FromArgb(99,110,102),Margin=new Padding(0,12,0,8)};
            var brandStory=new Label {Name="BrandIntroduction",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(99,110,102),Margin=new Padding(0,14,0,0)};
            setup.Controls.Add(location,0,0);setup.Controls.Add(folder,0,1);setup.Controls.Add(information,0,2);setup.Controls.Add(new Panel {Dock=DockStyle.Fill,BackColor=Color.FromArgb(231,235,228)},0,3);setup.Controls.Add(brandStory,0,4);
            var footer=new TableLayoutPanel {Name="InstallationStatus",Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=Padding.Empty};
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,46));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,40));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,12));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            status.AutoSize=false;status.Dock=DockStyle.Fill;status.MaximumSize=Size.Empty;status.Margin=Padding.Empty;percentage.Dock=DockStyle.Fill;percentage.TextAlign=ContentAlignment.MiddleRight;
            progress.Dock=DockStyle.Fill;progress.Margin=Padding.Empty;
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,18,0,0),Margin=Padding.Empty};
            install.Padding=new Padding(22,8,22,8);install.FlatStyle=FlatStyle.Flat;install.BackColor=Color.FromArgb(32,75,66);install.ForeColor=Color.White;
            cancel.Padding=new Padding(12,8,12,8);actions.Controls.Add(install);actions.Controls.Add(cancel);
            footer.Controls.Add(status,0,0);footer.Controls.Add(percentage,1,0);footer.Controls.Add(progress,0,1);footer.SetColumnSpan(progress,2);footer.Controls.Add(actions,0,2);footer.SetColumnSpan(actions,2);
            left.Controls.Add(setup,0,0);left.Controls.Add(footer,0,1);content.Controls.Add(header,0,0);content.SetColumnSpan(header,2);content.Controls.Add(left,0,1);content.Controls.Add(preview,1,1);Controls.Add(content);
            Action captions = delegate
            {
                Text = UiLanguage.IsEnglish ? "AgentPal Setup" : "AgentPal 安装程序";
                description.Text = UiLanguage.Text("An animated desktop companion for your local Claude Code workflow.");
                brandStory.Text=UiLanguage.Text("Agent + Pal: capability with companionship.\nAP and >_ form a little pal with a terminal at heart.");
                location.Text = UiLanguage.Text("Installation folder");
                information.Text = UiLanguage.Text("Installs for the current user. Uses your own Claude Code configuration; no model or credentials are bundled.");
                browse.Text = UiLanguage.Text("Browse…"); install.Text = UiLanguage.Text("Install"); cancel.Text = UiLanguage.Text("Cancel");
                status.Text = UiLanguage.Text(Program.HasClaudeCli() ? "Claude Code CLI detected." : "Claude Code CLI was not detected. You can configure it after installation.");
                preview.Invalidate();
            };
            captions();
            language.SelectedIndexChanged += delegate
            {
                try { UiLanguage.Set(language.SelectedIndex == 1 ? "zh-CN" : "en"); captions(); }
                catch (Exception error) { status.Text = error.Message; }
            };
            cancel.Click += delegate { Close(); };
            install.Click += delegate
            {
                if(installed){Process.Start(new ProcessStartInfo(Program.PetExe){UseShellExecute=true});Close();return;}
                try { Program.SelectInstallDirectory(destination.Text); }
                catch (Exception error) { status.Text = UiLanguage.Text(error.Message); return; }
                installing = true; install.Enabled = cancel.Enabled = destination.Enabled = browse.Enabled = language.Enabled = false;
                progress.Value = 0; progress.Visible = percentage.Visible = true;
                ShowProgress(0, UiLanguage.Text("Preparing installation…"));
                var worker = new Thread(delegate()
                {
                    Exception failure = null;var elapsed=Stopwatch.StartNew();Program.DeferLaunch=true;
                    try
                    {
                        Program.Install(delegate(int percent, string stage)
                        {
                            if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { ShowProgress(Math.Min(99,percent),percent==100?UiLanguage.Text("Finalizing installation…"):stage); });
                        });
                    }
                    catch (Exception error) { failure = error; }
                    if(failure==null&&elapsed.ElapsedMilliseconds<6000)Thread.Sleep((int)(6000-elapsed.ElapsedMilliseconds));
                    BeginInvoke((Action)delegate
                    {
                        installing = false;
                        if (failure == null)
                        {
                            installed=true;ShowProgress(100,UiLanguage.Text("Installation complete.")+(Program.IntegrationNotice.Length==0?"":"\n"+Program.IntegrationNotice));
                            install.Text=UiLanguage.Text("Open AgentPal");cancel.Text=UiLanguage.Text("Close");install.Enabled=cancel.Enabled=true;
                        }
                        else
                        {
                            status.Text = UiLanguage.Text("Installation failed.") + " " + failure.GetBaseException().Message;
                            install.Enabled = cancel.Enabled = destination.Enabled = browse.Enabled = language.Enabled = true;
                        }
                    });
                });
                worker.SetApartmentState(ApartmentState.STA); worker.Start();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (installing) e.Cancel = true; };
        }

        internal void ShowProgress(int percent, string stage)
        {
            progress.Value = Math.Max(progress.Value, Math.Max(0, Math.Min(100, percent)));
            progress.Invalidate();
            status.Text = stage;
            percentage.Text = progress.Value.ToString() + "%";
        }
    }

    internal sealed class InstallerProgressBar : ProgressBar
    {
        internal InstallerProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(227, 232, 225));
            int width = (int)Math.Round(ClientSize.Width * (double)Value / Math.Max(1, Maximum));
            using (var fill = new SolidBrush(Color.FromArgb(32, 75, 66))) e.Graphics.FillRectangle(fill, 0, 0, width, ClientSize.Height);
        }
    }

    internal sealed class InstallerPreview : Panel
    {
        private readonly List<Image[]> clips = new List<Image[]>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 16 };
        private readonly string[] headings = { "Meet your six pals", "Conversation, with company", "Assign work from any folder", "Tasks, reminders and countdowns", "An Agent workspace" };
        private readonly List<Image> features=new List<Image>();
        private readonly string[] names={"青衡","绯笺","苍芷","行者悟空","小白龙","小墨"};
        private readonly string[] captions = { "Six built-in companions. One shared workspace.", "Chat bubbles, structured replies and expandable tool activity.", "Choose a companion or team, enter a task, and use the current folder.", "Pick a companion, choose days and times, and edit tasks or reminders later.", "Follow member status, task dependencies and recorded handoffs." };
        private int? selectedSlide;
        private readonly ToolTip hints=new ToolTip();
        internal int ActiveSlide {get{return selectedSlide??(int)(clock.ElapsedMilliseconds/7000%headings.Length);}}
        internal void SelectSlide(int index){if(index<0||index>=headings.Length)throw new ArgumentOutOfRangeException("index");selectedSlide=index;Invalidate();}

        internal InstallerPreview()
        {
            DoubleBuffered = true; BackColor = Color.FromArgb(238, 242, 232);TabStop=true;SetStyle(ControlStyles.Selectable,true);AccessibleName="Companion animation previews";
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentPalPayload.zip"))
                if (resource != null)
                    using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
                    {
                        string[] roots = { "assets", "assets/characters/spark", "assets/characters/moss", "assets/bundled/sun-wukong", "assets/bundled/bailong", "assets/characters/pippin" };
                        string[] states = { "waving", "running-right", "eating", "punching", "jumping", "waving" };
                        for (int role = 0; role < roots.Length; role++)
                        {
                            var frames = new List<Image>();
                            for (int index = 0; index < 24; index++)
                            {
                                var entry = archive.GetEntry(roots[role] + "/" + states[role] + "/" + index.ToString("00") + ".png");
                                if (entry == null) break;
                                using (var input = entry.Open()) using (var image = Image.FromStream(input)) frames.Add(new Bitmap(image));
                            }
                            clips.Add(frames.ToArray());
                        }
                    }
            foreach(string name in new[]{"Chat","Folder","Schedules","Workspace"})
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentPal.Setup."+name+".png"))
                    features.Add(stream==null?null:new Bitmap(stream));
            timer.Tick += delegate { Invalidate(); }; timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            int slide=ActiveSlide;
            using(var font=new Font("Segoe UI",13,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,UiLanguage.Text(headings[slide]),font,new Rectangle(18,18,Width-36,60),Color.FromArgb(32,75,66),TextFormatFlags.WordBreak);
            var area=new Rectangle(16,82,Math.Max(1,Width-32),Math.Max(1,Height-184));
            if(slide==0)
            {
                int cellWidth=area.Width/3,cellHeight=area.Height/2;
                for(int i=0;i<clips.Count;i++)
                {
                    int x=area.Left+(i<3?i*cellWidth:(clips.Count==5?cellWidth/2:0)+(i-3)*cellWidth),y=area.Top+(i<3?0:cellHeight);
                    var frames=clips[i];if(frames.Length==0)continue;
                    double phase=clock.ElapsedMilliseconds/55.0;int index=(int)phase%frames.Length;
                    double scale=Math.Min((cellWidth-10)/(double)frames[index].Width,(cellHeight-24)/(double)frames[index].Height);
                    int w=(int)(frames[index].Width*scale),h=(int)(frames[index].Height*scale);var target=new Rectangle(x+(cellWidth-w)/2,y+(cellHeight-24-h)/2,w,h);
                    Draw(e.Graphics,frames[index],target,1);Draw(e.Graphics,frames[(index+1)%frames.Length],target,(float)(phase%1));
                    using(var font=new Font("Segoe UI",8))TextRenderer.DrawText(e.Graphics,UiLanguage.Text(names[i]),font,new Rectangle(x,y+cellHeight-22,cellWidth,22),Color.FromArgb(65,85,72),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                }
            }
            else if(features.Count>=slide&&features[slide-1]!=null)
            {
                Image picture=features[slide-1];double scale=Math.Min(area.Width/(double)picture.Width,area.Height/(double)picture.Height);
                int w=(int)(picture.Width*scale),h=(int)(picture.Height*scale);e.Graphics.DrawImage(picture,new Rectangle(area.X+(area.Width-w)/2,area.Y+(area.Height-h)/2,w,h));
            }
            using(var font=new Font("Segoe UI",9))TextRenderer.DrawText(e.Graphics,UiLanguage.Text(captions[slide]),font,new Rectangle(18,Height-88,Width-36,54),Color.FromArgb(73,91,78),TextFormatFlags.WordBreak);
            for(int i=0;i<headings.Length;i++)using(var brush=new SolidBrush(i==slide?Color.FromArgb(32,75,66):Color.FromArgb(171,185,169)))
            {Rectangle dot=Dot(i);e.Graphics.FillEllipse(brush,dot.X+8,dot.Y+8,8,8);}
        }
        private Rectangle Dot(int index){return new Rectangle((Width-headings.Length*28)/2+index*28,Height-30,24,24);}
        protected override void OnMouseDown(MouseEventArgs e)
        {base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;for(int i=0;i<headings.Length;i++)if(Dot(i).Contains(e.Location)){SelectSlide(i);Focus();break;}}
        protected override void OnMouseMove(MouseEventArgs e)
        {base.OnMouseMove(e);string caption="";for(int i=0;i<headings.Length;i++)if(Dot(i).Contains(e.Location))caption=UiLanguage.Text(headings[i]);if(hints.GetToolTip(this)!=caption)hints.SetToolTip(this,caption);Cursor=caption.Length==0?Cursors.Default:Cursors.Hand;}
        protected override bool IsInputKey(Keys keyData){return keyData==Keys.Left||keyData==Keys.Right||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e)
        {base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){SelectSlide((ActiveSlide+(e.KeyCode==Keys.Right?1:headings.Length-1))%headings.Length);e.Handled=true;}}

        private static void Draw(Graphics graphics, Image image, Rectangle target, float opacity)
        {
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                graphics.DrawImage(image, target, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose();hints.Dispose();foreach(var feature in features)if(feature!=null)feature.Dispose(); foreach (var clip in clips) foreach (Image frame in clip) frame.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
