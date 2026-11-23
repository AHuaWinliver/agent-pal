# AgentPal brand

The product name is **AgentPal**. The executable, repository slug, configuration
keys and CLI name remain `agent-pal` for compatibility.

The fixed AP mark has a softly rounded terminal recess and a terracotta-coral
cursor. It is the product identity and does not change when companions change.
The mark was developed with the built-in imagegen editor, refined through user
review, then selected for continued product integration. No existing provider's
logo is included in the mark. Character portraits identify companions only.

The wordmark uses **Oxanium SemiBold, weight 600**, from the pinned font in
`../fonts/oxanium`. Retain that font's copyright and OFL.txt when redistributing
its files. `tools/BuildBrand.py` uses fonttools to regenerate the SVG outlines;
the shipped PNGs and SVGs do not require the font to be installed at runtime.

Use the horizontal lockup when width is available, the stacked lockup for
covers, and the standalone AP mark for application and context-menu icons.
Do not stretch, add another mascot or recolor the coral terminal cursor.
Body text and Chinese UI continue to use the existing readable UI font stack.

The Windows ICO is built from the approved mark at nine sizes (16–256 px),
with an opaque black backdrop for visibility. See `tools/BuildAppIcon.cs`.
Its cache-distinct installed filename is `agent-pal-brand-v2.ico`.

The central silhouette is called **The little pal**. Keep it beside that heading
in the overview; do not add a second stacked logo to the same one-page layout.
`tools/BuildBrandOverview.py` composes the approved assets into the overview PNG
and its portable SVG image wrapper using Pillow and the pinned Oxanium font.
It retains the default order: Qingheng, Feijian, Cangzhi, Wukong, Bailong, Xiaomo.

Brand identity is independent of the agent harness. Current runtime features
are documented separately; the brand does not imply unimplemented providers.

The README cover retains the project's v0.6.13 preview illustration in `preview-cover-base.png`. The media builder updates only the product branding and companion-count text; the original illustration and layout remain unchanged.
