// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class CharacterPackPreviewForm : AppWindow
    {
        private readonly Func<CancellationToken, CharacterPackDraft> prepare;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly PictureBox picture = new PictureBox();
        private readonly Label heading = new Label();
        private readonly TextBox details = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None, BackColor = SystemColors.Control, TabStop = false
        };
        private readonly ComboBox direction = new ComboBox();
        private readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer { Interval = 42 };
        private readonly Button install = new Button();
        private readonly Button cancel = new Button();
        private CharacterPackDraft draft;
        private bool loading;
        private bool installing;
        private bool replaceExisting;
        private int previewPhase;
        private Image previewFrame;
        private Image flightArtwork;
        private double flightSeconds;
        private float flightSole;

        internal PetCharacter InstalledCharacter { get; private set; }

        internal CharacterPackPreviewForm(string title, string workMessage, Func<CancellationToken, CharacterPackDraft> prepare)
        {
            this.prepare = prepare;
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(475, 258);

            picture.Bounds = new Rectangle(16, 16, 164, 178);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.BackColor = Color.FromArgb(242, 246, 243);
            heading.Bounds = new Rectangle(195, 20, 264, 30);
            heading.AutoEllipsis = true;
            heading.Text = title;
            details.Bounds = new Rectangle(195, 54, 264, 142);
            details.Text = workMessage;
            direction.Bounds = new Rectangle(16, 202, 164, 24);
            direction.DropDownStyle = ComboBoxStyle.DropDownList;
            direction.Items.AddRange(new object[] { UiLanguage.Text("正面"), UiLanguage.Text("向左移动"), UiLanguage.Text("向右移动") });
            direction.SelectedIndex = 0;
            direction.Visible = false;
            direction.SelectedIndexChanged += delegate { previewPhase = 0; UpdatePreviewFrame(); };
            animation.Tick += delegate { UpdatePreviewFrame(); };
            cancel.Bounds = new Rectangle(217, 210, 104, 32);
            cancel.Text = UiLanguage.Text("取消");
            cancel.DialogResult = DialogResult.Cancel;
            install.Bounds = new Rectangle(329, 210, 130, 32);
            install.Name = "installCharacterPack";
            install.Enabled = false;
            install.Text = UiLanguage.Text("安装并切换");
            install.Click += InstallClicked;
            Controls.AddRange(new Control[] { picture, direction, heading, details, cancel, install });
            CancelButton = cancel;
            AcceptButton = install;
            Shown += PreparePreview;
        }

        private async void PreparePreview(object sender, EventArgs e)
        {
            loading = true;
            try
            {
                draft = await Task.Run(delegate { return prepare(cancellation.Token); });
                if (cancellation.IsCancellationRequested) return;
                picture.Image = draft.Preview;
                flightArtwork = draft.ReadArtifact();
                if (flightArtwork != null)
                {
                    flightSole = ArtifactTravelArt.FootRow(draft.Preview);
                    direction.Items.Add(UiLanguage.Text("飞行预览"));
                }
                direction.Visible = true;
                heading.Text = draft.Character.Name;
                details.Text = draft.Character.Id + "  ·  " + draft.Version;
                replaceExisting = draft.ReplacesExisting;
                if (replaceExisting)
                {
                    details.Text += Environment.NewLine + Environment.NewLine +
                        UiLanguage.Text("已经安装了同一角色。继续将替换它的资源。");
                    install.Text = UiLanguage.Text("替换并切换");
                }
                details.Text += Environment.NewLine + Environment.NewLine + UiLanguage.Text("人物描述") +
                    Environment.NewLine + draft.Character.Persona;
                details.Select(0, 0);
                install.Enabled = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!cancellation.IsCancellationRequested) ShowError(ex); }
            finally
            {
                loading = false;
                if (cancellation.IsCancellationRequested) Close();
            }
        }

        private async void InstallClicked(object sender, EventArgs e)
        {
            if (installing || draft == null) return;
            install.Enabled = cancel.Enabled = false;
            installing = true;
            animation.Stop();
            direction.Enabled = false;
            details.Text = UiLanguage.Text("正在安装角色…");
            try
            {
                InstalledCharacter = await Task.Run(delegate { return draft.Install(replaceExisting); });
                installing = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                installing = false;
                ShowError(ex);
                install.Enabled = true;
                direction.Enabled = true;
                UpdatePreviewFrame();
            }
            finally { if (!IsDisposed) cancel.Enabled = true; }
        }

        private void ShowError(Exception ex)
        {
            details.Text = UiLanguage.Text("无法准备角色包。") + Environment.NewLine + ex.Message;
            cancel.Text = UiLanguage.Text("关闭");
        }

        private void UpdatePreviewFrame()
        {
            if (draft == null || installing) return;
            Image next = null;
            if (direction.SelectedIndex == 3 && flightArtwork != null)
            {
                var frame = new Bitmap(draft.Preview.Width, draft.Preview.Height);
                using (Graphics g = Graphics.FromImage(frame))
                    ArtifactTravelArt.Render(g, frame.Size, draft.Preview, flightSole,
                        draft.Character.Id, draft.Character.ReminderStyle, 1f, flightSeconds += .042, false, flightArtwork,
                        draft.Character.ArtifactResourceFile == CharacterPackCatalog.WingsFile);
                next = frame;
            }
            else if (direction.SelectedIndex > 0)
            {
                try { next = draft.ReadPreviewFrame(direction.SelectedIndex == 1, previewPhase); }
                catch (System.IO.IOException) { animation.Stop(); return; }
                catch (ArgumentException) { animation.Stop(); return; }
                previewPhase = (previewPhase + 1) % 24;
            }
            picture.Image = next ?? draft.Preview;
            if (previewFrame != null) previewFrame.Dispose();
            previewFrame = next;
            animation.Enabled = next != null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (loading)
            {
                cancellation.Cancel();
                e.Cancel = true;
                DialogResult = DialogResult.None;
            }
            if (installing)
            {
                e.Cancel = true;
                DialogResult = DialogResult.None;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                picture.Image = null;
                animation.Dispose();
                if (previewFrame != null) previewFrame.Dispose();
                if (flightArtwork != null) flightArtwork.Dispose();
                if (draft != null) draft.Dispose();
                cancellation.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
