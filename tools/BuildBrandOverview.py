"""Compose the approved brand assets; never repaint the mark or portraits.

Requires Pillow. Fonts and artwork are pinned in art/. No network access.
"""
from pathlib import Path
import base64
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'art/branding'
PAPER, INK, MUTED, LINE = '#eeefea', '#181818', '#666c61', '#d2d6cd'
ROSTER = [('orbit', 'Qingheng'), ('spark', 'Feijian'), ('moss', 'Cangzhi'),
          ('sun-wukong', 'Wukong'), ('bailong', 'Bailong'), ('pippin', 'Xiaomo')]


def main():
    font_path = ROOT / 'art/fonts/oxanium/Oxanium[wght].ttf'

    def font(size):
        f = ImageFont.truetype(str(font_path), size)
        f.set_variation_by_name('SemiBold')
        return f

    im = Image.new('RGB', (1600, 1320), PAPER)
    d = ImageDraw.Draw(im)

    def label(text, xy, size=22, fill=MUTED, anchor=None):
        d.text(xy, text, font=font(size), fill=fill, anchor=anchor)

    def asset(path, box):
        with Image.open(path) as src:
            src = src.convert('RGBA')
            src.thumbnail(box[2:], Image.Resampling.LANCZOS)
            im.paste(src, (int(box[0]), int(box[1])), src)

    label('AGENTPAL / BRAND AT A GLANCE', (70, 50), 20)
    label('An AI companion, with a terminal at heart.', (70, 90), 26)
    asset(ART / 'agentpal-mark.png', (70, 185, 240, 240))
    asset(ART / 'agentpal-wordmark.png', (365, 222, 1130, 235))
    d.line((70, 492, 1530, 492), fill=LINE, width=2)
    columns = [(70, '01 / NAME', 'Agent + Pal',
                ['Agent capability.', 'Everyday companionship.']),
               (575, '02 / MARK', 'AP meets >_',
                ['A recognizable monogram,', 'with an integrated terminal prompt.']),
               (1080, '03 / COMPANION', 'The little pal',
                ['A little pal at the heart of the A,', 'bringing companionship to the terminal.'])]
    for x, number, title, lines in columns:
        label(number, (x, 536), 18)
        label(title, (x, 578), 34, INK)
        for row, text in enumerate(lines):
            label(text, (x, 636 + row * 34), 20)
    title_right = 1080 + d.textlength('The little pal', font=font(34))
    # Align to the letter bodies, excluding p's descender for optical balance.
    title_bounds = d.textbbox((1080, 578), 'The little', font=font(34))
    title_center = (title_bounds[1] + title_bounds[3]) / 2
    with Image.open(ART / 'agentpal-spirit.png') as sprite:
        sprite = sprite.convert('RGBA')
        sprite.thumbnail((50, 50), Image.Resampling.LANCZOS)
        bounds = sprite.getchannel('A').getbbox()
        icon_y = round(title_center - (bounds[1] + bounds[3]) / 2)
    asset(ART / 'agentpal-spirit.png', (title_right + 12, icon_y, 50, 50))
    d.line((70, 727, 1530, 727), fill=LINE, width=2)
    label('THE COMPANIONS', (70, 765), 18)
    label('Six Pals', (70, 803), 32, INK)
    label('One companion spirit.', (70, 851), 20)
    for index, (cid, name) in enumerate(ROSTER):
        x = 450 + index * 178
        size = 136 if cid != 'pippin' else 123
        asset(ART / 'avatars' / (cid + '.png'),
              (x + (136 - size) // 2, 790 + 136 - size, size, size))
        label(name, (x + 68, 966), 21, anchor='mm')
    d.line((70, 1010, 1530, 1010), fill=LINE, width=2)
    label('COLOR', (70, 1050), 18)
    for x, color, name in [(70, '#000000', 'Black'), (300, '#faf7f0', 'Warm white'),
                           (530, '#f4866b', 'Coral cursor')]:
        d.rounded_rectangle((x, 1090, x + 192, 1160), radius=8, fill=color,
                            outline=LINE, width=2)
        label(name, (x, 1178), 19)
        label(color.upper(), (x, 1208), 17)
    label('TYPOGRAPHY', (900, 1050), 18)
    asset(ART / 'agentpal-wordmark.png', (900, 1090, 550, 112))
    label('Oxanium SemiBold / 600', (900, 1220), 21)
    label('Selected AP mark + outlined wordmark', (70, 1280), 16)
    label('SIL Open Font License 1.1', (1530, 1280), 16, anchor='ra')
    output = ROOT / 'docs/images/brand-introduction.png'
    im.save(output, optimize=True)
    # A portable wrapper of the exact composed page, not a second editable logo.
    encoded = base64.b64encode(output.read_bytes()).decode('ascii')
    (ART / 'brand-introduction.svg').write_text(
        '<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="1320" viewBox="0 0 1600 1320">'
        '<title>AgentPal brand overview / The little pal / Six Pals</title>'
        f'<image width="1600" height="1320" href="data:image/png;base64,{encoded}"/></svg>', encoding='utf-8')
    print('Composed six-companion brand overview from approved assets:', output)


if __name__ == '__main__':
    main()
