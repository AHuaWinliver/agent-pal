// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AgentPal
{
    internal static class WeekdaySchedule
    {
        internal const int EveryDay = 127, Workdays = 62, Weekend = 65;

        internal static int Day(int weekday) { return 1 << Math.Max(0, Math.Min(6, weekday)); }
        internal static bool Includes(int mask, DayOfWeek day) { return (mask & Day((int)day)) != 0; }
        internal static bool ValidSelection(int mask) { return mask > 0 && (mask & ~EveryDay) == 0; }

        internal static int ForTask(string recurrence, int weekday, int selection)
        {
            switch (recurrence)
            {
                case "weekly": return Day(weekday);
                case "workdays": return Workdays;
                case "weekend": return Weekend;
                case "selected-days":
                case "weekly-interval": return ValidSelection(selection) ? selection : Day(weekday);
                default: return EveryDay;
            }
        }

        internal static int ForReminder(int weekday, int selection)
        { return ValidSelection(selection) ? selection : weekday >= 0 ? Day(weekday) : EveryDay; }

        internal static string Describe(int mask, bool english)
        {
            if (mask == EveryDay) return english ? "Daily" : "每天";
            if (mask == Workdays) return english ? "Monday–Friday" : "周一至周五";
            if (mask == Weekend) return english ? "Saturday–Sunday" : "周六至周日";
            var names = new List<string>();
            DateTimeFormatInfo calendar = CultureInfo.GetCultureInfo("en").DateTimeFormat;
            bool single = (mask & (mask - 1)) == 0;
            for (int index = 1; index <= 7; index++)
            {
                int day = index % 7;
                if (!Includes(mask, (DayOfWeek)day)) continue;
                names.Add(english ? (single ? calendar.DayNames[day] : calendar.AbbreviatedDayNames[day])
                    : ScheduledClaudeTask.WeekdayText(day));
            }
            string label = String.Join(english ? ", " : "、", names);
            return single ? (english ? "Every " : "每") + label : label;
        }
    }
}
