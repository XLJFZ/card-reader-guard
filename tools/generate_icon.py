"""Regenerate the application icon with Pillow (development only)."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
svg = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
<rect x="12" y="12" width="232" height="232" rx="52" fill="#20232b"/>
<path d="M74 44h66l38 38v120H74z" fill="#d0d8f5"/>
<path d="M140 44v38h38" fill="#a1afef"/>
<path d="M90 52v24m16-24v24m16-24v24" stroke="#20232b" stroke-width="8"/>
<rect x="90" y="100" width="72" height="50" rx="7" fill="#41495f"/>
<path d="M142 151l38-14 38 14v34c0 22-16 37-38 48-22-11-38-26-38-48z" fill="#a1afef" stroke="#20232b" stroke-width="8" stroke-linejoin="round"/>
<path d="M161 181l13 13 25-29" fill="none" stroke="#20232b" stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>
</svg>'''
(root / 'Assets' / 'App.svg').write_text(svg, encoding='utf-8')
(root / 'docs' / 'favicon.svg').write_text(svg, encoding='utf-8')
s=4
im=Image.new('RGBA',(256*s,256*s))
d=ImageDraw.Draw(im)
def box(coords,fill,r=0):
    coords=tuple(int(v*s) for v in coords)
    if r:d.rounded_rectangle(coords,r*s,fill=fill)
    else:d.rectangle(coords,fill=fill)
def poly(coords,fill):d.polygon([(x*s,y*s) for x,y in coords],fill=fill)
def line(coords,fill,width):d.line([(x*s,y*s) for x,y in coords],fill=fill,width=width*s,joint='curve')
box((12,12,244,244),'#20232b',52)
poly([(74,44),(140,44),(178,82),(178,202),(74,202)],'#d0d8f5')
poly([(140,44),(140,82),(178,82)],'#a1afef')
for x in (90,106,122):line([(x,52),(x,76)],'#20232b',8)
box((90,100,162,150),'#41495f',7)
# Smooth shield outline; points sample the vector's lower curve.
shield=[(142,151),(180,137),(218,151),(218,185),(215,199),(208,210),(196,222),(180,233),(164,222),(152,210),(145,199),(142,185)]
poly(shield,'#a1afef');line(shield+[shield[0]],'#20232b',8)
line([(161,181),(174,194),(199,165)],'#20232b',10)
for x,y in [(161,181),(199,165)]:box((x-5,y-5,x+5,y+5),'#20232b',5)
im=im.resize((256,256),Image.Resampling.LANCZOS)
im.save(root/'Assets'/'App.png')
im.save(root/'Assets'/'App.ico',sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])
print('Generated SVG, PNG and multi-resolution ICO assets.')
