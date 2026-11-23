# Privacy and local configuration

[简体中文](PRIVACY.zh-CN.md)

agent-pal stores companion selection, position, scheduled-task prompts and directories, and run history locally. These records can contain private information. Historical Windows installations may retain compatibility state names; do not upload the state directory with source code. Linux uses the user's XDG directories.

- External character packs contain local metadata and PNGs. Loading a pack does not download updates or execute plugins. Its personality text is used in AI prompts; import packs from sources you trust.
- Claude credentials, models, service endpoints and permissions remain in your existing CLI configuration. agent-pal does not bundle API keys.
- Installation, upgrades and uninstall never edit Claude configuration files. Native interactive tasks read the selected settings into a private per-run overlay, add only their session hooks and pass it with `--settings`. The selected file stays byte-for-byte unchanged. The overlay is removed when the CLI exits; an interrupted process may leave it in AgentPal’s private run directory. Legacy hooks from older versions remain untouched; they are not automatically cleaned up.
- Monitoring unrelated local Claude sessions is off by default and requires explicit opt-in. Owned scheduled runs are identified so their output is not duplicated into ordinary chat.
- Proactive messages, AI routes and history introductions call the configured CLI. They may contact a network service and consume usage.
- Dropping a file stages paths and a request. Sending allows the CLI to process those paths according to its existing permissions; the animation itself deletes nothing.
- agent-pal has no separate analytics server. Your CLI, MCP tools and configured services can have their own network and retention behavior.
- Windows uninstall removes startup entries and shortcuts; it leaves Claude settings and legacy hooks untouched. It does not recursively delete the installation directory or stop unrelated Claude jobs. Review retained files before cleaning them up.

Redact usernames, intranet endpoints, paths, prompts and model output before publishing screenshots or logs. Report sensitive vulnerabilities through the [security policy](../SECURITY.md).

Claude Code supports session settings via [`--settings`](https://code.claude.com/docs/en/cli-reference). AgentPal uses that mechanism for its private native-task overlay.

Enabling external-session monitoring only accepts incoming bridge events. Newly opened external CLI sessions need a bridge configured separately; toggling this preference never installs hooks.
