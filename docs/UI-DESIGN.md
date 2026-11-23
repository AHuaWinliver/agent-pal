# Interface conventions

Use a restrained neutral canvas, readable text, one green primary-action color and consistent spacing. Keep tasks, process details and final results visually distinct. Interface text follows the selected language; user commands, paths and transcripts retain their original text.

Windows management windows inherit `AppWindow`. This applies the shared button treatment and a 160 ms cubic ease-out entrance. Hover feedback and CLI process expansion use the same duration and curve. The app's Reduce motion setting and Windows animation preference disable these transitions. Opacity transitions finish at the requested opacity, including docked conversations, and do not move the window or companion. Closing or hiding a window stops its animation timer.

The shared rules cover chat, settings, scheduled tasks and history, reminders, team configuration and row editors, weekday selection, character import and quick-task windows. Authored sprite canvases, hall scenes and formatted message content keep their own rendering rules.

The quick-task entry flow has one decision per step: click a companion's direct menu entry, verify the current folder, type or edit the command, then confirm. The confirmation action stays disabled for empty input. Enter adds a line; Ctrl+Enter confirms after IME composition. Cancel does not launch a process. A window property lets the existing pet recognize a separately launched quick-task editor and pause while it has focus, without persistent lock files.

`QuickCommandsForm` previews the configured direct menu items and thumbnails before registration. Registration is scoped to the current user, with ownership checks for removal. Thumbnail creation is reused across commands bound to the same companion.

The Linux port shares the transcript renderer and uses the same entrance duration and easing through `NativeUiMotion`, respecting the app's Reduce motion preference. Native CentOS/TurboVNC visual acceptance remains a separate target-machine check.

Relevant checks: `tests/quick-task-test.ps1`, `tests/chat-bubble-test.ps1`, `tests/chat-drag-test.ps1`, `tests/profile-settings-test.ps1` and `tests/workshop-test.ps1`.

The new-conversation close action sits at the top right and stays anchored there when resized. A single left click on the tray icon opens the same settings menu as a pet right click, including while the pet is hidden. The collaboration UI uses Agent teams and Agent workspace; stored schema keys retain compatibility with existing profiles and history.

Explorer task editors use an independent application message loop and explicit window-level Shell icon, title and relaunch metadata. `tests/quick-task-shell-test.ps1` launches the compiled entry in a separate process, checks that it has no hidden modal owner, and asks the Windows Shell to extract the companion icon. Pixel captures are optional and skipped for an auto-hidden taskbar; metadata checks do not prove the appearance on every target desktop.


## README overview

The cover keeps the original preview illustration and composition. Only the AgentPal brand and current companion-count text are updated. The baseline is retained in `art/branding/preview-cover-base.png`; `Hero` in `tools/BuildReadmeMedia.cs` rebuilds the production cover while preserving the illustration pixels. Supporting screenshots explain quick tasks, scheduling, reminders and customization in their respective sections. Team collaboration stays in its own experimental section.
