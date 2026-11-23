// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

internal static class InstallerConfigurationTests
{
    private static void Check(bool value, string text)
    { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            if (Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE") != "1") throw new Exception("Test isolation required.");
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var program = assembly.GetType("AgentPal.Installer.Program", true);
            var select = program.GetMethod("SelectInstallDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            var location = program.GetField("InstallDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            string root = Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");
            string custom = Path.Combine(root, "agent-pal custom folder");
            select.Invoke(null, new object[] { custom });
            Check((string)location.GetValue(null) == custom, "custom Unicode installation directory is selected");
            string unrelated = Path.Combine(root, "unrelated"); Directory.CreateDirectory(unrelated);
            string userFile = Path.Combine(unrelated, "user.txt"); File.WriteAllText(userFile, "keep");
            foreach (string invalid in new[] { unrelated, Path.GetPathRoot(custom), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
            {
                bool rejected = false;
                try { select.Invoke(null, new object[] { invalid }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                Check(rejected && (string)location.GetValue(null) == custom && File.ReadAllText(userFile) == "keep", "invalid installation folder is rejected without changing the previous selection or user files");
            }
            var locale = assembly.GetType("AgentPal.UiLanguage", true).GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic);
            locale.Invoke(null, new object[] { "en" });
            using (var window = (Form)Activator.CreateInstance(assembly.GetType("AgentPal.Installer.InstallerForm", true), true))
            {
                var destination = window.Controls.Find("InstallDirectory", true)[0] as TextBox;
                Check(destination != null && destination.Text == custom, "installer shows an editable custom destination");
                Check(window.Controls.Find("Install", true)[0].Text == "Install", "installer opens in the selected English language");
                object preview = FindPreview(window);
                var clips = (IList)preview.GetType().GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
                Check(clips.Count == 6, "installer contains all six default companion clips");
                var features=(IList)preview.GetType().GetField("features",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(preview);
                Check(features.Count==4&&features[0]!=null&&features[1]!=null&&features[2]!=null&&features[3]!=null,"installer embeds four real feature screenshots alongside the six-companion overview");
                foreach (Array clip in clips) Check(clip.Length == 24, "preview uses the complete 24-frame original artwork sequence");
                var mark=(PictureBox)window.Controls.Find("AgentPalMark",true)[0];
                Check(mark.Image!=null&&mark.Image.Width==1254,"installer displays the approved full-resolution product logo beside its wordmark");
                using(var resource=assembly.GetManifestResourceStream("AgentPal.Brand.Mark.png"))
                using(var hash=System.Security.Cryptography.SHA256.Create())
                    Check(BitConverter.ToString(hash.ComputeHash(resource)).Replace("-","").ToLowerInvariant()=="98ed7e6043f5b59012329c038e88318194df5937e73f3718f88df75dcce7e3af","installer embeds the exact approved logo without regenerating its shape");
                Check(window.Controls.Find("BrandIntroduction",true)[0].Text.Contains("little pal"),"English setup includes the Agent + Pal, terminal and little pal introduction");
                using(var payload=assembly.GetManifestResourceStream("AgentPalPayload.zip"))
                using(var zip=new ZipArchive(payload,ZipArchiveMode.Read))
                using(var stream=zip.GetEntry("assets/bundled/sun-wukong/punching/00.png").Open())
                using(var source=new Bitmap(stream))
                {
                    var wukong=(Bitmap)((Array)clips[3]).GetValue(0);bool equal=wukong.Size==source.Size;
                    for(int y=0;y<source.Height;y+=11)for(int x=0;x<source.Width;x+=11)
                    {
                        Color a=source.GetPixel(x,y),b=wukong.GetPixel(x,y);
                        equal&=a.A==b.A&&Math.Abs(a.R*a.A-b.R*b.A)<=255&&Math.Abs(a.G*a.A-b.G*b.A)<=255&&Math.Abs(a.B*a.A-b.B*b.A)<=255;
                    }
                    Check(equal,"Wukong preview uses the actual bundled staff-spinning frames");
                }
                window.Location = new Point(SystemInformation.VirtualScreen.Right + 100, 0);
                // Use a public illustrative path in screenshot evidence.
                destination.Text = @"C:\Apps\agent-pal";
                window.Show(); Application.DoEvents();
                var choose=preview.GetType().GetMethod("SelectSlide",BindingFlags.Instance|BindingFlags.NonPublic);choose.Invoke(preview,new object[]{2});
                typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,new object[]{new KeyEventArgs(Keys.Right)});
                Check((int)preview.GetType().GetProperty("ActiveSlide",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(preview,null)==3,"keyboard navigation can select schedules without waiting for autoplay");
                Rectangle lastDot=(Rectangle)preview.GetType().GetMethod("Dot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,new object[]{4});
                typeof(Control).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,new object[]{new MouseEventArgs(MouseButtons.Left,1,lastDot.Left+12,lastDot.Top+12,0)});
                Check((int)preview.GetType().GetProperty("ActiveSlide",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(preview,null)==4,"clicking the fifth dot selects Agent workspace");
                choose.Invoke(preview,new object[]{0});
                using (var capture = new Bitmap(window.Width, window.Height))
                { window.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size)); capture.Save(Path.Combine(root, "installer-en.png")); }
                for(int feature=1;feature<=4;feature++){choose.Invoke(preview,new object[]{feature});using(var capture=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(capture,new Rectangle(Point.Empty,capture.Size));capture.Save(Path.Combine(root,"installer-feature-"+feature+".png"));}}
                choose.Invoke(preview,new object[]{0});
                var selector=FindCombo(window);selector.SelectedIndex=1;Application.DoEvents();
                Check(window.Controls.Find("BrandIntroduction",true)[0].Text.Contains("小精灵")&&window.Controls.Find("Install",true)[0].Text=="安装","Chinese setup localizes brand introduction and keeps the selected preview");
                window.ClientSize=new Size(864,562);Application.DoEvents();
                var button=window.Controls.Find("Install",true)[0];var point=window.PointToClient(button.PointToScreen(Point.Empty));
                Check(point.Y>=0&&point.Y+button.Height<=window.ClientSize.Height,"Install action remains inside the minimum-size window below the scrollable introduction");
                using(var capture=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(capture,new Rectangle(Point.Empty,capture.Size));capture.Save(Path.Combine(root,"installer-zh-CN.png"));}
                selector.SelectedIndex=0;window.ClientSize=new Size(960,620);Application.DoEvents();
                var progress = (ProgressBar)window.Controls.Find("InstallationProgress", true)[0];
                var percentage = (Label)window.Controls.Find("InstallationPercentage", true)[0];
                Check(progress.Maximum == 100 && progress.Value == 0, "installer has a determinate percentage bar");
                progress.Visible = percentage.Visible = true;
                var showProgress = window.GetType().GetMethod("ShowProgress", BindingFlags.Instance | BindingFlags.NonPublic);
                showProgress.Invoke(window, new object[] { 52, "Extracting files…" });
                showProgress.Invoke(window, new object[] { 41, "Extracting files…" });
                Check(progress.Value == 52 && percentage.Text == "52%", "visible percentage does not go backwards");
                window.Controls.Find("Install", true)[0].Enabled = false;
                Application.DoEvents();
                using (var bar = new Bitmap(progress.Width, progress.Height))
                {
                    progress.DrawToBitmap(bar, new Rectangle(Point.Empty, bar.Size));
                    Check(bar.GetPixel(10, 5).G != bar.GetPixel(bar.Width - 10, 5).G, "progress fill visibly occupies the completed portion");
                }
                using (var capture = new Bitmap(window.Width, window.Height))
                { window.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size)); capture.Save(Path.Combine(root, "installer-progress.png")); }
                destination.Text = custom;
                window.Close();
            }
            int previous = -1, updates = 0; var milestones = new HashSet<int>();
            Action<int, string> report = delegate(int value, string stage)
            {
                if (value < previous || value < 0 || value > 100 || String.IsNullOrWhiteSpace(stage)) throw new Exception("Invalid progress report.");
                previous = value; updates++; milestones.Add(value);
            };
            var install = program.GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic);
            install.Invoke(null, new object[] { report });
            Check(previous == 100 && updates > 40 && milestones.Contains(70) && milestones.Contains(78) && milestones.Contains(84) && milestones.Contains(88) && milestones.Contains(94), "actual extraction and validation report monotonic milestones through completion");
            Check(File.Exists(Path.Combine(custom, "agent-pal.exe")), "progress reaches completion after files are installed");
            previous = -1; location.SetValue(null, unrelated); bool failed = false;
            try { install.Invoke(null, new object[] { report }); }
            catch (TargetInvocationException error) { failed = error.InnerException is InvalidOperationException; }
            finally { location.SetValue(null, custom); }
            Check(failed && previous < 100 && File.ReadAllText(userFile) == "keep", "failed installation never reports completion or modifies unrelated files");
            using(var window=(Form)Activator.CreateInstance(assembly.GetType("AgentPal.Installer.InstallerForm",true),true))
            {
                string interactive=Path.Combine(root,"interactive-install");
                ((TextBox)window.Controls.Find("InstallDirectory",true)[0]).Text=interactive;
                window.Location=new Point(SystemInformation.VirtualScreen.Right+100,0);window.Show();Application.DoEvents();
                var start=(Button)window.Controls.Find("Install",true)[0];var elapsed=System.Diagnostics.Stopwatch.StartNew();start.PerformClick();
                var installed=window.GetType().GetField("installed",BindingFlags.Instance|BindingFlags.NonPublic);
                while(!(bool)installed.GetValue(window)&&elapsed.ElapsedMilliseconds<60000){Application.DoEvents();System.Threading.Thread.Sleep(15);}
                Check((bool)installed.GetValue(window)&&elapsed.ElapsedMilliseconds>=5900,"interactive setup keeps the UI responsive and allows at least six seconds for its stages");
                Check(window.Visible&&start.Enabled&&start.Text=="Open AgentPal"&&((ProgressBar)window.Controls.Find("InstallationProgress",true)[0]).Value==100,"setup remains open at completion with an explicit launch action instead of closing or auto-launching");
                Check(File.Exists(Path.Combine(interactive,"agent-pal.exe")),"interactive completion follows actual extraction and validation");
                using(var capture=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(capture,new Rectangle(Point.Empty,capture.Size));capture.Save(Path.Combine(root,"installer-complete.png"));}
                window.Close();
            }
            Console.WriteLine("PASS installer configuration, embedded previews and isolated installation progress.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static ComboBox FindCombo(Control parent)
    {foreach(Control child in parent.Controls){var choice=child as ComboBox;if(choice!=null)return choice;choice=FindCombo(child);if(choice!=null)return choice;}return null;}
    private static Control FindPreview(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.GetType().Name == "InstallerPreview") return child;
            var found = FindPreview(child); if (found != null) return found;
        }
        return null;
    }
}
