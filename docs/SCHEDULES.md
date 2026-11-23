# Weekday and hourly schedules

[简体中文](SCHEDULES.zh-CN.md)

In scheduled tasks, choose **Selected weekdays** to run at a fixed local time on one or more days, such as Tuesday and Friday at 14:30.

Choose **Weekly interval** to set a daily start time and a repeat interval. Monday and Wednesday, starting at 09:00 every two hours, runs at 09:00, 11:00, 13:00 and so on through 23:00. The next cycle begins at 09:00 on the next selected day.

Intervals stay anchored to the chosen daily start time when a launch is late. The app does not wake a sleeping machine. When it resumes, it catches up at most once within the allowed days and times, then schedules the next future occurrence. Companion ownership, working directory, run mode and overlap policy still apply.

## Edit tasks and reminders

Select a saved task and choose **Edit task**, double-click it or press F2. Change its companion, prompt, directory, recurrence or execution options, then choose **Save changes** (Ctrl+S). The task ID and previous run records remain intact; changed settings apply to future runs. Cancel leaves the saved task unchanged.

In reminders, use **Edit reminder**, double-click or F2. Change the text, time, selected days or presentation, then select the checkmark (or Ctrl+S). Cancel discards the draft. Disabled reminders stay disabled.

Hover over a task’s companion in the list, or a companion in the creation picker, to inspect the effective executable and settings file. The hint distinguishes companion overrides from global/default configuration and never displays settings contents. Monday–Friday and Saturday–Sunday are fixed weekday groups, not holiday calendars.
