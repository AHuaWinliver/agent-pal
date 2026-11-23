// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class PetForm
    {
        private void AddQuickSettings(ContextMenuStrip menu)
        {
            var reduced=new ToolStripMenuItem("减少动画"){Name="QuickReduceMotion"};
            reduced.Click+=delegate
            {
                reduceMotion=!reduceMotion;UiMotion.ReducedMotion=reduceMotion;
                if(reduceMotion)CancelWander();SavePreferences();RefreshPetVisual();
                reduced.Checked=reduceMotion;SyncQuickSettingControl("ReduceMotion",reduceMotion?1:0);
            };
            var monitor=new ToolStripMenuItem("收集其他 Claude 会话"){Name="QuickMonitorExternal",ToolTipText=UiLanguage.Text("外部会话需已有联动桥接；此开关只接收事件，不安装 Hooks，也不修改 Claude 配置。")};
            monitor.Click+=delegate
            {
                try
                {
                    var latest=IntegrationPreferences.Load();latest.MonitorExternalSessions=!integrationPreferences.MonitorExternalSessions;latest.Save();
                    integrationPreferences.MonitorExternalSessions=latest.MonitorExternalSessions;
                    monitor.Checked=latest.MonitorExternalSessions;SyncQuickSettingControl("MonitorExternal",latest.MonitorExternalSessions?1:0);
                }
                catch(Exception error){MessageBox.Show(this,UiLanguage.Text(error.Message),UiLanguage.Text("Settings were not saved"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            };
            menu.Items.Add(reduced);menu.Items.Add(monitor);
            menu.Opening+=delegate
            {
                reduced.Checked=reduceMotion;monitor.Checked=integrationPreferences.MonitorExternalSessions;
            };
        }
        private void SyncQuickSettingControl(string name,int value)
        {
            if(defaultSettingsWindow==null||defaultSettingsWindow.IsDisposed)return;
            foreach(Control control in defaultSettingsWindow.Controls.Find(name,true))
            {
                var number=control as NumericUpDown;if(number!=null)number.Value=value;
                var check=control as CheckBox;if(check!=null)check.Checked=value!=0;
            }
        }
    }
}
