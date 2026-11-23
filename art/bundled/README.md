# Default companion packs

`default-companions.json` lists data-only companions shipped with AgentPal.
`companions/sun-wukong` is the existing reviewed 1.0.1 pack, with 240 transparent
frames and its cloud platform. The animation PNGs are unchanged; the manifest
uses the built-in display name 行者悟空 (Wayfarer Wukong).

The normal build copies these files into `assets/bundled` and writes the index
into `assets`. They appear alongside the four fixed-rig companions without
importing a ZIP or writing anything into the user's Characters directory.

User-imported packs with the same ID take precedence. Removing such a user
pack reveals the shipped default again. The four-member workshop template is
unchanged; Wukong can be selected as a task executor or custom workshop member.
