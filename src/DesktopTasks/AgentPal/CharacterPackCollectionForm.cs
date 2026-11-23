using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class CharacterPackCollectionForm : AppWindow
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ListView characters = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly ImageList portraits = new ImageList { ImageSize = new Size(36, 38), ColorDepth = ColorDepth.Depth32Bit };
        private readonly Label status = new Label { Name = "characterCollectionStatus", Dock = DockStyle.Top, Height = 56, Padding = new Padding(2, 6, 4, 0) };
        private readonly Button install = new Button { Name = "installCharacterCollection", AutoSize = true, Enabled = false };
        private readonly Button dismiss = new Button { AutoSize = true };
        private CharacterPackDraft.Collection collection;
        private bool loading;
        private bool installing;

        internal CharacterPackCollectionForm(string root, string archive)
        {
            Text = UiLanguage.Text("导入角色合集");
            ClientSize = new Size(540, 510);
            MinimumSize = new Size(470, 370);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            Padding = new Padding(14);
            characters.Columns.Add(UiLanguage.Text("角色"), 205);
            characters.Columns.Add("ID", 131);
            characters.Columns.Add(UiLanguage.Text("操作"), 120);
            characters.SmallImageList = portraits;
            status.Text = UiLanguage.Text("正在逐个校验合集中的角色…");
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 43, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 7, 0, 0) };
            install.Text = UiLanguage.Text("安装全部");
            dismiss.Text = UiLanguage.Text("取消");
            dismiss.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(install);
            buttons.Controls.Add(dismiss);
            Controls.Add(characters);
            Controls.Add(status);
            Controls.Add(buttons);
            CancelButton = dismiss;
            Shown += async delegate
            {
                loading = true;
                try
                {
                    collection = await Task.Run(delegate { return CharacterPackDraft.Collection.Prepare(root, archive, cancellation.Token); });
                    if (cancellation.IsCancellationRequested) return;
                    int count = 0;
                    foreach (var draft in collection.Drafts)
                    {
                        portraits.Images.Add(draft.Preview);
                        characters.Items.Add(new ListViewItem(new[] { draft.Character.Name, draft.Character.Id,
                            UiLanguage.Text(collection.WillReplace(draft.Character.Id) ? "替换已有角色" : "新增角色") }, count++));
                    }
                    status.Text = UiLanguage.Format("已校验 {0} 个角色。安装后当前形象不变。", count);
                    if (collection.HasReplacements)
                    {
                        status.Text += Environment.NewLine + UiLanguage.Text("标记为替换的角色将覆盖原资源。");
                        install.Text = UiLanguage.Text("安装全部并替换");
                    }
                    install.Enabled = true;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    status.Text = UiLanguage.Text("无法准备角色包。") + Environment.NewLine + ex.Message;
                    dismiss.Text = UiLanguage.Text("关闭");
                }
                finally
                {
                    loading = false;
                    if (cancellation.IsCancellationRequested) Close();
                }
            };
            install.Click += async delegate
            {
                if (collection == null || installing) return;
                installing = true;
                install.Enabled = dismiss.Enabled = false;
                status.Text = UiLanguage.Text("正在安装角色…");
                try
                {
                    await Task.Run(delegate { collection.Install(collection.HasReplacements); });
                    installing = false;
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { status.Text = ex.Message; }
                finally
                {
                    installing = false;
                    if (!IsDisposed) dismiss.Enabled = true;
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (loading) cancellation.Cancel();
            if (loading || installing) { e.Cancel = true; DialogResult = DialogResult.None; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                characters.SmallImageList = null;
                portraits.Dispose();
                cancellation.Dispose();
                if (collection != null) collection.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
