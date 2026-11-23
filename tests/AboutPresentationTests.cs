// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AgentPal;

internal static class AboutPresentationTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    [STAThread] private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();string output=Environment.GetEnvironmentVariable("AGENT_PAL_STATE_DIR");Directory.CreateDirectory(output);
            foreach(string id in new[]{"orbit","spark","moss","pippin","sun-wukong","bailong"})
                using(var portrait=CompanionAvatar.Load(id,192))
                    Check(portrait!=null&&portrait.Width==192&&portrait.Height==192,"embedded portrait available without loose assets: "+id);
            foreach(string language in new[]{"en","zh-CN"})
            {
                UiLanguage.Set(language);
                using(var form=new AboutForm())
                {
                    form.Show();Application.DoEvents();
                    Check(form.Text==(language=="en"?"About AgentPal":"关于 AgentPal"),"localized About title "+language);
                    Check(form.ShowInTaskbar&&form.CancelButton!=null,"taskbar presence and keyboard dismissal");
                    Capture(form,Path.Combine(output,"about-"+language+".png"));
                    Check(form.Controls[0].Controls[0].AccessibleDescription.Contains(language=="en"?"Small steps, steady heart.":"罗盘指向远山"),"persona-based companion poetry is visible and accessible "+language);
                    form.ClientSize=new Size(640,480);Application.DoEvents();
                    Check(form.Controls[0] is Panel,"About content remains scrollable at small sizes");
                    var panel=(Panel)form.Controls[0];Console.WriteLine("LAYOUT viewport="+panel.ClientSize+" canvas="+panel.Controls[0].Bounds+" scroll="+panel.DisplayRectangle);
                    Check(!panel.HorizontalScroll.Visible,"no horizontal scrolling at minimum width");
                    Capture(form,Path.Combine(output,"about-small-"+language+".png"));
                    form.Scale(new SizeF(1.5f,1.5f));Application.DoEvents();Capture(form,Path.Combine(output,"about-scaled-"+language+".png"));
                    UiLanguage.Set(language=="en"?"zh-CN":"en");Application.DoEvents();
                    Check(form.Text==(language=="en"?"关于 AgentPal":"About AgentPal"),"open About follows language switch");
                    form.Close();
                }
            }
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void Capture(Form form,string file){using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(file);}}
}
