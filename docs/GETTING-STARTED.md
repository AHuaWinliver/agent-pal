# Getting started

[简体中文](GETTING-STARTED.zh-CN.md)

## Install agent-pal

Download the Windows installer from [Releases](https://github.com/AHuaWinliver/agent-pal/releases). Use the package's SHA256 checksum to verify your download. The current installer is unsigned. For a source build, use a short path such as `C:\src\agent-pal` to avoid legacy .NET Framework path limits.

The installer runs for the current user, offers an installation folder and language choice, and previews all six companions together, followed by conversation, folder-task, scheduling and Agent-workspace features. Interactive setup allows at least six seconds for its stages and remains on its completion screen; choose **Open AgentPal** to launch. Installation, including silent installation and upgrades, does not write Claude settings. Native interactive tasks load their hooks from a private per-run overlay via `--settings`; selected configuration files stay unchanged. Preview builds on a development branch may contain features absent from the latest published installer; check its release notes.

## Connect Claude Code

First verify that your own Claude Code CLI can start in an ordinary terminal, reach your configured model service and complete authentication. agent-pal does not bundle a model, credentials or a CLI installation.

Right-click the companion and open **Default settings**. Set a working directory, then configure a global executable and optional JSON settings file. Each companion can override either field independently; an empty field inherits its global default. See [companion profiles](COMPANION-PROFILES.md).

Folder trust and tool permissions remain under Claude's control. Review prompts in the CLI. AI features use your configured service and may consume paid usage or contact a cloud or intranet endpoint.

## Everyday interactions

- Right-click to change companions or open chat, tasks, reminders and settings.
- Double-click a reply bubble to open the conversation; text becomes selectable there.
- Drag the companion to reposition it. Drag to an edge to dock it; restore a hidden companion from the tray. A single left click on the tray icon opens the same settings menu as a companion right click.
- The shared menu includes Reduce motion and Monitor other Claude sessions. Size presets are in Default settings. External-session monitoring remains off by default.
- Choose On this day in the new-conversation footer for a character-voiced history introduction.
- Double-click the companion for a character-aware AI interaction. Optional AI routes, proactive messages and break reminders make model requests.
- Drop files or folders onto the companion, review the paths and working directory, then provide a request before sending. The eating animation does not delete the originals.

## Tasks and reminders

| Feature | Purpose | Behavior |
| --- | --- | --- |
| Daily reminders and countdowns | Remind you to do something | A character performance or notification; no automatic file processing |
| Claude scheduled tasks | Run a request in a specified directory | Daily, weekly or interval schedules, blocking or parallel overlap, and independent history |

Choose an execution companion, directory, mode and recurrence before saving a task. The task window aggregates all companions and shows each owner. Task output stays in its run history rather than ordinary chat. Native Background requires a compatible CLI.

The app does not wake a sleeping or powered-off computer. It checks overdue items when running again; this is a desktop scheduler, not a system service with guaranteed unattended execution.

## Add a companion

Open the character resource directory from the character menu and copy in a complete, validated pack. New characters appear after scanning; valid updates to the selected character apply without reinstalling. A single image is insufficient. See [character packs](CHARACTER-PACKS.md).

## Platform notes

Windows chat uses native RichEdit with Chinese input protection and emoji fallback. Color emoji and virtual desktop behavior depend on the Windows environment. The [Linux port](LINUX.en.md) has separate validation and release requirements.

Report issues with a sanitized OS, display scaling and CLI version. Keep credentials, private endpoints, prompts and transcripts out of public reports. See [privacy](PRIVACY.md) and [security reporting](../SECURITY.md).
