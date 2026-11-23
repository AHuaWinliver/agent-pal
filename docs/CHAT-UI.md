# Conversation interface

Each companion has one chat history for manual messages, proactive speech, break reminders and history-on-this-day responses. Each response adds a separate transcript entry. Scheduled tasks and external CLI activity keep their own records. Changing the working directory keeps the chat history but starts a fresh CLI context.

[简体中文](CHAT-UI.zh-CN.md)

![CLI-style conversation with expandable tool events](images/cli-conversation.png)

Double-click the desktop reply bubble to expand a conversation below its companion. The initial position is computed before showing the window, within that companion's monitor. Windows can shorten the window or reposition the pair when space is limited. A very small display can still require overlap.

The desktop bubble is not selectable. Expanded conversations support selecting and copying text; they remain open until manually closed. Opening the pet's context menu hides the small reply bubble.

## Messages and updates

Replies retain paragraphs, list items and fenced code blocks. The Windows desktop preview shows recent structured lines, while the expanded window shows the full reply and a separate tool-progress area. Tool names and results come from CLI events; hidden reasoning is not displayed. Streaming still uses the existing coalesced updates.

Regular conversations use rounded message bubbles: your messages align right, and companion replies align left beside their portraits. Names, timestamps, structured text and expandable process panels keep the conversation readable. Headings, emphasis and code are rendered without changing the saved source. The copy-message action copies the original reply. Tool panels preserve bounded selectable event text and a recent summary when collapsed. Workshop records retain their separate message-bubble layout.

Explicit speaker metadata separates new turns; the app does not infer user messages from quoted text such as “You:”. Legacy or native CLI output without trustworthy turn metadata remains one intact output block. Native CLI conversations continue in their terminal.

Windows merges streaming updates about every 120 ms and preserves selections and scroll position. The Linux port uses a 100 ms update interval. This limits token-by-token flicker without discarding text. New output follows the bottom only when the reader was already there.

Windows uses `RICHEDIT50W` and font fallback; Chinese text is not assigned an emoji font. Emoji color depends on the OS. Enter sends and Shift+Enter adds a line, with an IME composition guard. The Linux conversation currently uses Ctrl+Enter to send. New sends are disabled while a response is running.

## Connection errors

Managed conversations surface CLI connection failures and retry attempts in the active reply, and preserve the last failure after the process exits. CLI startup and telemetry do not imply that a model is connected. Independent external terminals depend on the events their CLI publishes; AgentPal does not scrape their terminal display.

## Moving the pair

On Windows, drag the title area, companion name or status after a small movement threshold. The pet follows while text and buttons keep their normal interactions. Positioning uses the monitor under the pointer and ends cleanly on release, lost capture or close. On Linux, moving the native conversation window updates the companion position through window-position events.

Message activity is local to the current run; this interface does not add a permanent chat database. Scheduled-task transcripts remain in their separate persisted run history.

Validation: `tests/chat-bubble-test.ps1`, `tests/chat-drag-test.ps1` and the CLI integration tests. They use isolated fixtures rather than paid model calls.
