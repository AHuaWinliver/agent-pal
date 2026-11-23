# Reviewed companion rigs

These files rebuild Wukong and Bailong without image generation or network access.
The base and profile atlases were drawn with the built-in `image_gen` tool; their
explicit crop rectangles are committed beside the original PNGs. `profile.json`
contains the character identity and import metadata. Artwork remains subject to
visual review before a distribution is published.

```powershell
.\tools\RebuildCompanionPack.ps1 -Id sun-wukong -OutputDirectory .\build\wukong-review
.\tools\RebuildCompanionPack.ps1 -Id bailong -OutputDirectory .\build\bailong-review
```

Use a new output directory. Each export creates 240 transparent PNG frames, a
hashed character manifest, and an adjacent `.arm-audit.json` containing the actual
rendered shoulder, elbow, palm and requested hand targets. Validate the pack with
`tools/ValidateCharacterPack.ps1` before importing it.

## Arm attachment rules

- Slots 2 and 3 have stable far/near ownership. They never swap during an action.
- Frontal shoulder sockets are measured on each companion's actual garment,
  rather than inherited from its geometry alias. Profile sockets are calibrated
  with the directional body. Leftward walking mirrors the entire figure;
  the hand or sleeve is not mirrored independently during an animation.
- `arm-layout.json` specifies source crop heights and authored hand orientation.
  The normal near arm is mirrored once at import so its thumb faces inward.
  Profile hands keep the forward-facing thumb direction from the source drawing.
- Remove neighboring atlas fragments before locating a hand. A gold ornament or
  stray hair fragment must never become the palm landmark.
- Upper arm and forearm use a constant-length two-bone solve. The forearm, wrist
  and hand are rendered with one rigid similarity transform. Do not mesh, stretch,
  shear or remap individual fingers to make a target reachable.
- Original sleeve pixels overlap at the elbow. Gesture arms remain in front.
  Side walking draws the far arm before the torso, and the near arm afterward.
  The torso hides overlapping far-sleeve pixels in both travel directions.
- Waving and wiping bend the raised right elbow outward, to the right of the
  palm. The hand rises on an outside arc rather than through its shoulder root.
- Frontal idle/waiting poses let both complete arms hang vertically from their
  shoulder sockets. Side walking keeps upper arm and forearm nearly straight,
  with only a six-degree pendulum swing. Do not hold either forearm at waist or
  chest height while the companion is resting or walking.
- Walking hands oppose their corresponding legs. Review both travel directions,
  not just one passing pose. Check the entire action at normal and reduced speed.
- A staff has a controlling hand. The free hand must release while a support
  point is outside its reach. Never keep two impossible, permanently opposed
  grips through a complete twirl.
- A wiping hand reaches lip height, moves sideways, and returns to rest. A wave
  uses an open palm outside the face, rather than a closed fist over the eyes.

## Source records

The Wukong identity prompts retained during generation are in
`sun-wukong/identity-prompts.json`. The exact open-palm edit prompt is in
`sun-wukong/wave-arm-prompt.txt`; `wave-arm.png` is the imported, alpha-bounded arm
from that edit. Its measured palm landmark is separate from the fingertips.
Complete earlier prompts for the other individual atlases were not retained.
Those PNG sources and their crop metadata are included, so reproduction of the
renderer does not depend on recreating the images with a model.

Geometry checks establish attachment, reach and length constraints. They do not
replace visual review of painted anatomy, cloth overlap or expression changes.

## Approved anatomy calibration

`../anatomy-calibration.json` is consumed by both native and bundled rig exports.
Qingheng's approved baseline uses 0.88 arm scale and symmetric frontal shoulders.
Other humanoids have independent source-neck, collar, aperture and shoulder data.
The foreground torso contains clothing only; the lower neck is rendered behind it,
with opaque textured overlap and a narrow, local feather. Profile skin samples
come from the profile art. Xiaomo uses its existing tapered-neck/shell socket and
quadruped limb rules rather than human arm/collar parameters. Keep body-only
proofs and light/dark composites with the full frame and walking-occlusion checks.

All six current companion forms were approved on 2026-11-23. See `../companion-baseline.json` for the ordered identities, calibration/renderer hashes and approved frame digests. Use this baseline when reviewing subsequent anatomy or motion changes.
