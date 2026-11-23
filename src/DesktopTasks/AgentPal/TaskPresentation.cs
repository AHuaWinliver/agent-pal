// SPDX-License-Identifier: Apache-2.0
using System;
using System.Globalization;

namespace AgentPal
{
    internal static class TaskPresentation
    {
        internal static string Owner(string id)
        {
            foreach (PetCharacter profile in PetCharacter.All)
                if (String.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)) return UiLanguage.Text(profile.Name);
            return PetCharacter.OwnerName(id);
        }
        internal static string Desktop(int number) { return number == 0 ? UiLanguage.Text("当前桌面") : UiLanguage.Format("桌面 {0}", number); }
        internal static string Mode(string mode)
        {
            if (!UiLanguage.IsEnglish) return ClaudeTaskService.RunModeLabel(mode);
            switch (ClaudeTaskService.NormalizeRunMode(mode))
            {
                case "interactive-powershell": return "CLI · PowerShell";
                case "interactive-terminal": return "CLI · Terminal";
                case "powershell": return "PowerShell";
                case "terminal": return "Terminal";
                case "claude-background": return "Claude BG";
                default: return "Background";
            }
        }
        internal static string Schedule(ScheduledClaudeTask task)
        {
            if (!UiLanguage.IsEnglish) return task.ScheduleText;
            string time = (task.MinutesAfterMidnight / 60).ToString("00") + ":" + (task.MinutesAfterMidnight % 60).ToString("00");
            if(task.Recurrence=="weekly-interval")return WeekdaySchedule.Describe(task.WeekdayMask,true)+" from "+time+
                ", every "+task.IntervalMinutes+" min (each selected day)";
            if (task.Recurrence != "interval")
                return WeekdaySchedule.Describe(WeekdaySchedule.ForTask(task.Recurrence, task.Weekday, task.WeekdayMask), true) + " " + time;
            int minutes = Math.Max(1, task.IntervalMinutes);
            if (minutes % 1440 == 0) return "Every " + minutes / 1440 + (minutes == 1440 ? " day" : " days");
            if (minutes % 60 == 0) return "Every " + minutes / 60 + (minutes == 60 ? " hour" : " hours");
            return "Every " + minutes + (minutes == 1 ? " minute" : " minutes");
        }
    }
}
