# agent-pal 0.6.7 — AI desktop companion source

`AgentPal/` contains the Windows desktop application in the `agent-pal` namespace;
`AgentPal.Installer/` contains its per-user installer in `AgentPal.Installer`.

The application preserves the task scheduling, native CLI interaction,
background-session supervision, virtual desktop, reminder, dialogue and
file-drop features from the local 2.3.28 baseline. Public artwork and
character personas are defined in `PetCharacter.cs`; approved transparent
textures are shipped in `art/rigs`, and `tools/BuildOriginalCharacters.cs`
renders them into continuous animation. No legacy character images are built.

See the repository README and `docs/DEVELOPMENT.md` for build/test steps.
