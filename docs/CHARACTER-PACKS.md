# Character packs and hot updates

[简体中文](CHARACTER-PACKS.zh-CN.md)

A compatible agent-pal installation can load new companions from local resource files without recompiling or reinstalling. Packs are data-only: character metadata, complete PNG animation frames and SHA256 checksums. They cannot load assemblies, scripts or commands.

## Install or update a pack

On Windows, choose **Companions → Import character pack…**, select the ZIP, then choose **Install and switch** after previewing the character. The ZIP may contain a single `<id>` folder or the contents of that folder. If the ID is already installed, the preview asks you to choose **Replace and switch**. Closing the preview leaves the installed character as it is.

For a collection, put multiple complete character folders directly at the ZIP root. Use the same menu, review the list and any replacements, then choose **Install all**. A collection can contain up to 16 characters and 512 MB, and the installed external character limit is still 16. Importing a collection keeps the current selection.

For an unpacked folder, use the character resource directory:

1. Right-click the companion and open the character resource directory.
2. Copy the complete `<id>` folder into it. `character.json` must be directly inside that folder.
3. Wait for the copy to finish, choose **Companions → Refresh character packs**, and select the newly listed custom companion.
4. When updating, replace frame files first and the manifest last. Prefer preparing a complete pack in a dot-prefixed staging folder and moving it into place.

Windows defaults to `%LOCALAPPDATA%\agent-pal\Characters`; `AGENT_PAL_CHARACTER_DIR` can select another directory before launch. Linux uses the XDG data directory's `agent-pal/Characters`. Paths must not contain symlinks or directory junctions. Packs live outside the installation and do not require administrator access.

Windows debounces changes before validation. The Linux port currently rescans periodically. An invalid update retains the already decoded character in memory; this is not a persistent backup. Invalid files will not load after restart. Keep a separate known-good pack. Removing the selected external pack falls back to Qingheng.

## Create from three images

In the Windows menu, choose **Companions → Create from images…**. Enter a name and choose front, left-facing and right-facing full-body views of the same character. Use PNG or JPEG, up to 16 MB per file and 4096 pixels on the longest edge. Transparent backgrounds and solid backgrounds are supported.

Add an optional **Character description** of up to 4,000 characters. It travels with the pack and sets the character's voice in AI chats. You can also choose one image for **Mystic wings** or a **Spirit vessel**. Wings render behind the character, and the vessel sits beneath its feet. Use **Flight preview** to check the result.

For an installed character, **Companions → Flight artwork** lets you choose either image directly. The selection is saved for that character. **Restore character flight artwork** returns to the appearance supplied by the pack or built into the app.

Select **Next: preview** and check the front and both moving views. The app creates 240 frames locally and fills in the profile, quotes and checksum manifest. Each direction uses its own source image. The biped gait bends elbows and knees, keeps a foot planted through each phase, and moves the arms opposite the legs. It estimates the occluded limbs from the visible sleeve and shoe. Clear wrist and shoe outlines give the most predictable results. The other generated states use basic image motion.

Choose **Install and switch** to save and use the character. Close the preview to return to the image selections.

## Folder contract

An optional `artifact-wings.png` or `artifact-platform.png` can sit alongside `character.json`. Choose one RGBA PNG up to 512×512 and include its SHA256 in the `files` manifest. The wizard and export tool add it automatically.

```text
Characters/my-companion/
  character.json
  idle/00.png ... 07.png
  waiting/00.png ... 07.png
  failed/00.png ... 07.png
  running/00.png ... 23.png
  running-left/00.png ... 23.png
  running-right/00.png ... 23.png
  waving/00.png ... 23.png
  review/00.png ... 23.png
  jumping/00.png ... 23.png
  punching/00.png ... 23.png
  eating/00.png ... 23.png
  wiping-mouth/00.png ... 23.png
```

Each pack contains 240 RGBA PNGs at exactly 384 × 416. Each image is limited to 2 MB, the manifest to 64 KB and the complete pack to 64 MB. Up to 16 non-staging pack directories are scanned. The three running states must each contain 24 distinct image hashes, not copies of eight poses. Optional root files are `README.md` and `LICENSE.txt`; unknown files or subdirectories are rejected.

## Manifest

Start with the [profile template](templates/character-profile.json). It is exporter input, not an installable manifest until the checksum list has been generated.

| Field | Contract |
| --- | --- |
| `schemaVersion` | `1` |
| `id` | 3–48 characters, starts with a lowercase letter, lowercase letters/digits/hyphens only; matches its folder |
| `version` | Three-part resource version, independent of the app version |
| `name`, `persona` | Display name up to 20 characters; personality up to 4,000 |
| `quotes` | 5–50 quotes, each 1–200 characters |
| `canvasWidth`, `canvasHeight` | `384`, `416` |
| `mouthX`, `mouthY` | Normalized file-card target, strictly between 0 and 1 |
| `reminderStyle` | `command` sword flight, `beam` talismans, `warning` plants, `stone` ink ripples |
| `files` | Exported map of all 240 relative frame paths to SHA256 hashes |

Built-in IDs `orbit`, `spark`, `moss`, `pippin` and historical compatibility IDs are reserved. New external companions do not need to share the built-ins' body types or names.

## Export and validate

Render all animation frames with your preferred tools. Layered art or rig JSON cannot replace the finished frames. The exporter currently runs on Windows with the .NET Framework compiler; recipients of the completed pack need neither.

```powershell
.\tools\ExportCharacterPack.ps1 `
  -FramesDirectory C:\my-art\rendered `
  -ProfilePath C:\my-art\character-profile.json `
  -OutputRoot C:\my-packs
.\tools\ValidateCharacterPack.ps1 -PackDirectory C:\my-packs\my-companion
```

The exporter copies frames, computes checksums and uses the production validator. It refuses to overwrite an existing output. Review the result, include artwork provenance and licensing, then share the whole `<id>` folder.

## Animation requirements

Keep identity, proportions, lighting and outlines consistent across states. Use real alpha, safe margins and smooth contralateral walking. Check both directions and the final-to-first frame transition.

External packs own their complete expression frames. Built-in head/neck parameters and eye/mouth patches are not applied to arbitrary external artwork. Encode blinks and mouth changes into your own frames. Reminder styles reuse the supported timelines with the selected character's frames; packs cannot provide executable effect scripts.

See the [customization guide](CHARACTER-CUSTOMIZATION.md) for joints, contact points, layering and review criteria. Structural validation does not establish visual quality or artwork rights.

After building, run `tests/character-pack-test.ps1` for isolated add/switch/update/corruption/recovery/removal and export checks. No real model is called.

## Manage companions and trails

Open **Manage companions** from the desktop companion or tray menu. Built-ins appear first and cannot be deleted. Imported packs follow in stable ID order. Deleting a pack removes only its resource folder; conversations, scheduled tasks and profile settings are kept. If the active pack is deleted, AgentPal switches to Qingheng.

Select an imported companion, choose **Trail colors…**, select the swatches and save. The palette is stored outside the resource pack in `companion-appearance.json`; **Restore defaults** clears its override. It applies to dragging and interaction trails and survives resource refreshes and restarts.
