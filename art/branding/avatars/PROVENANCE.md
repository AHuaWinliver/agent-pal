# Companion portrait sources

Default presentation order: Qingheng, Feijian, Cangzhi, Wukong, Bailong, Xiaomo. The application shares this order through PetCharacter.Defaults/Available and PetCharacter.DefaultDisplayIds. The four-profile rig generation array retains its stable legacy indices.

- Qingheng (`orbit`), Feijian (`spark`) and Cangzhi (`moss`): original idle frames, crop (65,16)-(325,250). Original faces, outfits, collars and shoulders are preserved.
- Wukong (`sun-wukong`): built-in imagegen portrait generated on 2026-11-21 from the bundled idle sprite and companion style references. Prompt: preserve monkey identity, curled golden headband, brown fur, black/gold collar and red scarf; use delicate painterly chibi shading, fine warm outlines, amber irises with eye whites and soft highlights; transparent head-and-shoulders composition, no text or frame. Cropped for avatar framing.
- Xiaomo (`pippin`): original program rig, texture, and limb mesh. The near forelimb is rotated 72 degrees toward the left; the far forelimb sits behind the body for this portrait. The original face pixels are preserved. sources/pippin-forward-original.png is the portrait source frame; sources/pippin-forward-pose.json records the pose and crop. pippin.svg applies transparent feathering to the clipped shell and lower edge. No generated Xiaomo limb redesign is used.

Desktop animation code, motion timing and sprite layers are unchanged. The small spirit is a complete black silhouette derived from the approved AP mark’s central shape, with its lower body, white terminal chevron and coral cursor.
