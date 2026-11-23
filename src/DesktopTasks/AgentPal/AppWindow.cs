// SPDX-License-Identifier: Apache-2.0
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AgentPal
{
    internal class AppWindow:Form
    {
        private readonly Timer entrance=new Timer {Interval=15};
        private readonly Stopwatch clock=new Stopwatch();
        private double targetOpacity,lastOpacity;
        protected override void OnLoad(EventArgs e)
        {
            UiDesign.Apply(this);base.OnLoad(e);
            try{int corner=2;DwmSetWindowAttribute(Handle,33,ref corner,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        internal AppWindow()
        {
            Icon=AppIcon.WindowIcon;
            entrance.Tick+=delegate
            {
                if(!Visible||IsDisposed){entrance.Stop();return;}
                if(Math.Abs(Opacity-lastOpacity)>.005)targetOpacity=Opacity; // docking opacity can change during entry
                double progress=Math.Min(1,clock.Elapsed.TotalMilliseconds/UiMotion.Duration);
                lastOpacity=UiMotion.OpacityAt(targetOpacity,progress);Opacity=lastOpacity;
                if(progress>=1||UiMotion.Disabled){Opacity=targetOpacity;entrance.Stop();}
            };
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            if(!Visible&&entrance.Enabled){entrance.Stop();Opacity=targetOpacity;}
            base.OnVisibleChanged(e);
            if(Visible&&!UiMotion.Disabled&&WindowState!=FormWindowState.Minimized)
            {
                targetOpacity=Opacity;lastOpacity=UiMotion.OpacityAt(targetOpacity,0);Opacity=lastOpacity;
                clock.Restart();entrance.Start();
            }
        }
        protected override void Dispose(bool disposing){if(disposing)entrance.Dispose();base.Dispose(disposing);}
    }
    internal static class UiMotion
    {
        internal const double Duration=160;
        internal static bool ReducedMotion;
        internal static void LoadPreference()
        {
            if(Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")=="1")return;
            try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\agent-pal")??Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\OrbitPet"))
                ReducedMotion=key!=null&&Convert.ToString(key.GetValue("ReduceMotion",0))=="1";}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
        }
        internal static bool Disabled {get{return ReducedMotion||!SystemInformation.IsMenuAnimationEnabled||Environment.GetEnvironmentVariable("AGENT_PAL_TEST_MODE")=="1";}}
        internal static double Ease(double value){double t=Math.Max(0,Math.Min(1,value));return 1-Math.Pow(1-t,3);}
        internal static double OpacityAt(double target,double time){return Math.Max(0,Math.Min(1,target))*(.86+.14*Ease(time));}
    }
    internal static class UiDesign
    {
        internal static readonly Color Canvas=Color.FromArgb(250,250,247),Ink=Color.FromArgb(40,48,44),Accent=Color.FromArgb(43,108,88),Line=Color.FromArgb(216,224,216);
        private static readonly ConditionalWeakTable<Button,ButtonMotion> buttons=new ConditionalWeakTable<Button,ButtonMotion>();
        internal static void Apply(Form window)
        {
            if(window.BackColor==SystemColors.Control)window.BackColor=Canvas;
            Style(window);
        }
        private static void Style(Control control)
        {
            // Preserve sprite canvases, authored chat formatting and scene colors.
            if(control is ChatMessageRow||control is CliProcessView||control is SectHallControl||control is PictureBox||control is TrailColorSwatch)return;
            var button=control as Button;
            if(button!=null&&!(button is RoundIconButton)&&button.Text!="×")buttons.GetValue(button,delegate(Button value){return new ButtonMotion(value);});
            var grid=control as DataGridView;
            if(grid!=null){grid.BackgroundColor=Color.White;grid.GridColor=Line;grid.BorderStyle=BorderStyle.None;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(220,238,229);grid.DefaultCellStyle.SelectionForeColor=Ink;}
            var input=control as TextBox;
            if(input!=null){if(input.BorderStyle==BorderStyle.Fixed3D)input.BorderStyle=BorderStyle.FixedSingle;if(input.BackColor==SystemColors.Window||input.BackColor==SystemColors.Control)input.BackColor=input.ReadOnly?Color.FromArgb(244,245,241):Color.White;}
            if(control.GetType()==typeof(Panel)||control is TabPage||control is FlowLayoutPanel||control is TableLayoutPanel)
                if(control.BackColor==SystemColors.Control)control.BackColor=Canvas;
            foreach(Control child in control.Controls)Style(child);
        }
        private sealed class ButtonMotion
        {
            private readonly Button button;
            private readonly Timer timer=new Timer {Interval=15};
            private readonly Stopwatch watch=new Stopwatch();
            private readonly Color normal,hover;
            private Color from,to;
            internal ButtonMotion(Button value)
            {
                button=value;
                Form owner=button.FindForm();
                bool primary=button.Name=="ConfirmQuickTask"||button.Name=="ApplySettings"||button.Name=="ApplyQuickCommands"||
                    (owner!=null&&Object.ReferenceEquals(owner.AcceptButton,button))||
                    (button.BackColor.R<80&&button.BackColor.G<100&&button.BackColor.B<80);
                normal=primary?Accent:Color.White;hover=primary?Color.FromArgb(60,125,105):Color.FromArgb(233,241,235);
                button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=1;
                button.FlatAppearance.BorderColor=primary?Accent:Line;button.BackColor=normal;
                if(primary)button.ForeColor=Color.White;
                Action enabledColors=delegate{timer.Stop();button.BackColor=button.Enabled?normal:Color.FromArgb(232,235,230);if(primary)button.ForeColor=button.Enabled?Color.White:Color.FromArgb(127,136,128);};
                button.EnabledChanged+=delegate{enabledColors();};enabledColors();
                button.FlatAppearance.MouseOverBackColor=normal;button.FlatAppearance.MouseDownBackColor=hover;
                button.Cursor=Cursors.Hand;
                button.MouseEnter+=delegate{Move(hover);};button.MouseLeave+=delegate{Move(normal);};button.Disposed+=delegate{timer.Dispose();};
                timer.Tick+=delegate
                {
                    double progress=UiMotion.Ease(watch.Elapsed.TotalMilliseconds/UiMotion.Duration);
                    Set(Color.FromArgb((int)(from.R+(to.R-from.R)*progress),(int)(from.G+(to.G-from.G)*progress),(int)(from.B+(to.B-from.B)*progress)));
                    if(progress>=1)timer.Stop();
                };
            }
            private void Set(Color value){button.BackColor=value;button.FlatAppearance.MouseOverBackColor=value;}
            private void Move(Color target){timer.Stop();if(UiMotion.Disabled){Set(target);return;}from=button.BackColor;to=target;watch.Restart();timer.Start();}
        }
    }
}
