// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class CharacterFromImagesForm : AppWindow
    {
        private readonly TextBox characterName = new TextBox();
        private readonly TextBox description = new TextBox();
        private readonly TextBox[] imageNames = { new TextBox(), new TextBox(), new TextBox() };
        private readonly string[] paths = new string[3];
        private readonly string[] labels = { "正面", "左侧", "右侧" };
        private readonly Button next = new Button();
        private readonly Label artifactName = new Label();
        internal string ArtifactPath { get; private set; }
        internal bool ArtifactIsWings { get; private set; }
        internal string CharacterName { get { return characterName.Text.Trim(); } }
        internal string CharacterDescription { get { return description.Text.Trim(); } }
        internal string FrontPath { get { return paths[0]; } }
        internal string LeftPath { get { return paths[1]; } }
        internal string RightPath { get { return paths[2]; } }

        internal CharacterFromImagesForm()
        {
            Text = UiLanguage.Text("从图片创建…");
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 389);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12),
                ColumnCount = 3, RowCount = 10
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
            for (int i = 0; i < 3; i++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var prompt = new Label
            {
                Text = UiLanguage.Text("选择同一双足角色的三张全身图，尽量露出手腕和鞋。支持透明或纯色背景的 PNG、JPEG。"),
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(prompt, 0, 0);
            panel.SetColumnSpan(prompt, 3);
            panel.Controls.Add(RowLabel("角色名"), 0, 1);
            characterName.Name = "characterName";
            characterName.MaxLength = 20;
            characterName.Dock = DockStyle.Fill;
            characterName.TextChanged += delegate { UpdateNextButton(); };
            panel.Controls.Add(characterName, 1, 1);
            panel.SetColumnSpan(characterName, 2);
            panel.Controls.Add(RowLabel("人物描述"), 0, 2);
            description.Name = "characterDescription";
            description.AccessibleName = UiLanguage.Text("人物描述（可选）");
            description.Multiline = true;
            description.ScrollBars = ScrollBars.Vertical;
            description.MaxLength = 4000;
            description.Dock = DockStyle.Fill;
            panel.Controls.Add(description, 1, 2);
            panel.SetColumnSpan(description, 2);

            for (int i = 0; i < imageNames.Length; i++)
            {
                int view = i;
                panel.Controls.Add(RowLabel(labels[i]), 0, i + 3);
                TextBox file = imageNames[i];
                file.ReadOnly = true;
                file.TabStop = false;
                file.Dock = DockStyle.Fill;
                file.AccessibleName = UiLanguage.Text(labels[i]);
                panel.Controls.Add(file, 1, i + 3);
                var browse = new Button
                {
                    Text = UiLanguage.Text("选择图片…"), Dock = DockStyle.Fill,
                    AccessibleName = UiLanguage.Format("选择{0}图片", UiLanguage.Text(labels[i]))
                };
                browse.Click += delegate { BrowseImage(view); };
                panel.Controls.Add(browse, 2, i + 3);
            }

            panel.Controls.Add(RowLabel("飞行外观"), 0, 6);
            var artworkButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            var wings = new Button { Text = UiLanguage.Text("选择玄翼图片…"), AutoSize = true };
            var platform = new Button { Text = UiLanguage.Text("选择灵器图片…"), AutoSize = true };
            var reset = new Button { Text = UiLanguage.Text("清除"), AutoSize = true };
            wings.Click += delegate { BrowseArtwork(true); };
            platform.Click += delegate { BrowseArtwork(false); };
            reset.Click += delegate { SetArtifactImage(null, false); };
            artworkButtons.Controls.AddRange(new Control[] { wings, platform, reset });
            panel.Controls.Add(artworkButtons, 1, 6);
            panel.SetColumnSpan(artworkButtons, 2);
            artifactName.Dock = DockStyle.Fill;
            artifactName.AutoEllipsis = true;
            SetArtifactImage(null, false);
            panel.Controls.Add(artifactName, 1, 7);
            panel.SetColumnSpan(artifactName, 2);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Dock = DockStyle.Fill };
            next.Name = "previewCreatedCharacter";
            next.Text = UiLanguage.Text("下一步：预览");
            next.Width = 118;
            next.DialogResult = DialogResult.OK;
            next.Enabled = false;
            var cancel = new Button { Text = UiLanguage.Text("取消"), DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(next);
            buttons.Controls.Add(cancel);
            panel.Controls.Add(buttons, 0, 9);
            panel.SetColumnSpan(buttons, 3);
            Controls.Add(panel);
            AcceptButton = next;
            CancelButton = cancel;
        }

        private void BrowseArtwork(bool wings)
        {
            using (var picker = new OpenFileDialog
            {
                Title = UiLanguage.Text(wings ? "选择玄翼图片…" : "选择灵器图片…"),
                Filter = UiLanguage.Text("图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"),
                CheckFileExists = true, Multiselect = false, RestoreDirectory = true
            })
                if (picker.ShowDialog(this) == DialogResult.OK) SetArtifactImage(picker.FileName, wings);
        }

        internal void SetArtifactImage(string path, bool wings)
        {
            ArtifactPath = path;
            ArtifactIsWings = wings;
            artifactName.Text = path == null ? UiLanguage.Text("可选 · 使用默认飞行外观") :
                UiLanguage.Text(wings ? "玄翼" : "灵器") + " · " + Path.GetFileName(path);
        }

        private static Label RowLabel(string source)
        { return new Label { Text = UiLanguage.Text(source), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }; }

        private void BrowseImage(int view)
        {
            using (var picker = new OpenFileDialog
            {
                Title = UiLanguage.Format("选择{0}图片", UiLanguage.Text(labels[view])),
                Filter = UiLanguage.Text("图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"),
                CheckFileExists = true, Multiselect = false, RestoreDirectory = true,
                FileName = paths[view] ?? ""
            })
                if (picker.ShowDialog(this) == DialogResult.OK) SetViewImage(view, picker.FileName);
        }

        internal void SetViewImage(int view, string path)
        {
            paths[view] = path;
            imageNames[view].Text = Path.GetFileName(path);
            UpdateNextButton();
        }

        private void UpdateNextButton()
        {
            next.Enabled = CharacterName.Length > 0 && FrontPath != null && LeftPath != null && RightPath != null;
        }
    }
}
