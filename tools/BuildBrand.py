"""Rebuild approved AgentPal wordmarks using pinned OFL Oxanium 600 outlines.

Requires fonttools. Runtime needs neither Python nor an installed brand font.
"""
import base64
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.boundsPen import BoundsPen

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/branding'

def main():
    with TTFont(ROOT/'art/fonts/oxanium/Oxanium[wght].ttf') as font:
        glyphs=font.getGlyphSet(location={'wght':600});cmap=font.getBestCmap()
        path=SVGPathPen(glyphs);bounds=BoundsPen(glyphs);x=0
        for char in 'AgentPal':
            glyph=glyphs[cmap[ord(char)]];transform=(1,0,0,1,x,0)
            glyph.draw(TransformPen(path,transform));glyph.draw(TransformPen(bounds,transform));x+=glyph.width
        left,bottom,right,top=bounds.bounds;commands=path.getCommands()
    def text(x,y,width,color):
        scale=width/(right-left)
        return f'<path fill="{color}" transform="translate({x-left*scale},{y+top*scale}) scale({scale},-{scale})" d="{commands}"/>'
    width=1600;height=round((top-bottom)*1576/(right-left)+24)
    for name,color in [('agentpal-wordmark','#181818'),('agentpal-wordmark-inverse','#faf7f0')]:
        (OUT/(name+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="{height}" viewBox="0 0 1600 {height}"><title>AgentPal / Oxanium SemiBold 600 / SIL OFL 1.1</title>{text(12,12,1576,color)}</svg>',encoding='utf-8')
    icon=base64.b64encode((OUT/'agentpal-mark.png').read_bytes()).decode('ascii')
    for name,vertical in [('agentpal-lockup-horizontal',False),('agentpal-lockup-stacked',True)]:
        w,h=(1600,620) if not vertical else (1000,1100)
        ix,iy,size=(70,120,380) if not vertical else (175,60,650)
        tx,ty,tw=(520,(620-1000*(top-bottom)/(right-left))/2,1000) if not vertical else (75,800,850)
        svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><title>AgentPal brand lockup / Oxanium 600</title><rect width="100%" height="100%" fill="white"/><image x="{ix}" y="{iy}" width="{size}" height="{size}" href="data:image/png;base64,{icon}"/>{text(tx,ty,tw,"#181818")}</svg>'
        (OUT/(name+'.svg')).write_text(svg,encoding='utf-8')
    print('Built approved Oxanium 600 wordmarks and horizontal/stacked lockups.')

if __name__=='__main__':main()
