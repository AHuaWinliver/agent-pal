// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed class CompanionConfigurationTip:IDisposable
    {
        private readonly ToolTip tip=new ToolTip {InitialDelay=350,ReshowDelay=100,AutoPopDelay=30000,ShowAlways=true};
        private readonly Timer poll=new Timer {Interval=120};
        private readonly ComboBox picker;private readonly Func<int,string> resolve;private int hovered=-1;
        [StructLayout(LayoutKind.Sequential)]private struct Rect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]private struct Info {public int Size;public Rect Item,Button;public int State;public IntPtr Combo,Edit,List;}
        [DllImport("user32.dll")]private static extern bool GetComboBoxInfo(IntPtr handle,ref Info info);
        [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr handle,out Rect rect);
        [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wparam,IntPtr lparam);
        internal CompanionConfigurationTip(ComboBox combo,Func<int,string> characterId)
        {
            picker=combo;resolve=characterId;
            picker.MouseEnter+=delegate{tip.SetToolTip(picker,Describe(resolve(picker.SelectedIndex)));};
            picker.SelectedIndexChanged+=delegate{tip.SetToolTip(picker,Describe(resolve(picker.SelectedIndex)));};
            picker.DropDown+=delegate{hovered=-1;tip.Hide(picker);poll.Start();};
            picker.DropDownClosed+=delegate{poll.Stop();tip.Hide(picker);};
            poll.Tick+=delegate
            {
                if(!picker.Visible||!picker.DroppedDown){poll.Stop();tip.Hide(picker);return;}
                var info=new Info {Size=Marshal.SizeOf(typeof(Info))};Rect rect;
                if(!picker.IsHandleCreated||!GetComboBoxInfo(picker.Handle,ref info)||!GetWindowRect(info.List,out rect))return;
                Point mouse=Cursor.Position;if(mouse.X<rect.Left||mouse.X>=rect.Right||mouse.Y<rect.Top||mouse.Y>=rect.Bottom){if(hovered>=0)tip.Hide(picker);hovered=-1;return;}
                int value=SendMessage(info.List,0x01A9,IntPtr.Zero,new IntPtr(((mouse.Y-rect.Top)<<16)|((mouse.X-rect.Left)&0xffff))).ToInt32();
                int index=value&0xffff;if((value>>16)!=0||index>=picker.Items.Count)return;
                if(index==hovered)return;hovered=index;Point anchor=picker.PointToClient(new Point(rect.Right+8,mouse.Y));
                tip.Show(Describe(resolve(index)),picker,anchor,20000);
            };
        }
        internal void BindList(ListBox list,Func<int,string> characterId)
        {
            int previous=-1;
            list.MouseEnter+=delegate{previous=-1;};
            list.MouseMove+=delegate(object sender,MouseEventArgs e){int index=list.IndexFromPoint(e.Location);if(index==previous)return;previous=index;tip.SetToolTip(list,index<0?null:Describe(characterId(index)));};
            list.MouseLeave+=delegate{tip.Hide(list);previous=-1;};
        }
        internal static string Describe(string id)
        {
            if(String.IsNullOrEmpty(id))return "";var p=IntegrationPreferences.Load();
            string exe=p.ExecutablePathFor(id),settings=p.EffectiveSettingsPathFor(id);
            if(String.IsNullOrWhiteSpace(exe)){try{exe=ClaudeCliRunner.FindClaudeCommand(id)??"";}catch(Exception){exe="";}}
            string exeOrigin=UiLanguage.Text(String.IsNullOrWhiteSpace(p.ExecutableOverrideFor(id))?"沿用全局配置":"伙伴专属配置");
            string settingsOrigin=UiLanguage.Text(String.IsNullOrWhiteSpace(p.SettingsPathFor(id))?"沿用全局配置":"伙伴专属配置");
            return UiLanguage.Text(PetCharacter.OwnerName(id))+"\n\n"+UiLanguage.Text("Claude 可执行文件")+" · "+exeOrigin+"\n"+(String.IsNullOrWhiteSpace(exe)?UiLanguage.Text("未找到 Claude 程序"):exe)+"\n\n"+UiLanguage.Text("Claude 配置文件")+" · "+settingsOrigin+"\n"+(String.IsNullOrWhiteSpace(settings)?UiLanguage.Text("Claude 默认配置（未指定文件）"):settings);
        }
        public void Dispose(){poll.Dispose();tip.Dispose();}
    }
}
