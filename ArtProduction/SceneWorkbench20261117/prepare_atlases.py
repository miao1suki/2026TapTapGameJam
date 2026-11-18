"""Reproduce candidate source atlases; requires Pillow, no image generation calls."""
import argparse
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('sources', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

def tile(image, x, y, grid, size):
    w, h = image.size
    image = image.crop((round(x*w/grid)+2, round(y*h/grid)+2,
                        round((x+1)*w/grid)-2, round((y+1)*h/grid)-2))
    image = image.convert('RGBA').resize((size, size), Image.Resampling.NEAREST)
    pixels = []
    for r, g, b, a in image.getdata():
        if r > 150 and b > 130 and g < 110:
            a = 0
        if r < 35 and g < 60 and b < 100:
            r, g, b = int(14+(r-14)*.35), int(31+(g-31)*.35), int(47+(b-47)*.35)
        pixels.append((r, g, b, a))
    image.putdata(pixels)
    return image

outer = Image.open(args.sources/'moss-terrain-half-safe-r4.png')
inner = Image.open(args.sources/'moss-inner-wide-r2.png')
compact = Image.open(args.sources/'moss-compact-r1.png')
wide_positions = [(0,0),(1,0),(2,0),(0,1),(1,1),(3,1),(0,2),(1,2),(3,2)]
small_positions = [(0,0),(1,0),(2,0),(0,1),(1,1),(2,1),(0,2),(1,2),(2,2),
                   (0,3),(1,3),(2,3),(3,3)]
wide = [tile(outer,x,y,4,32) for x,y in wide_positions]
wide += [tile(inner,x,y,2,32) for x,y in [(0,0),(1,0),(0,1),(1,1)]]
small = [tile(compact,x,y,4,16) for x,y in small_positions]
for name, pieces, size in [('MossTerrainWide',wide,32), ('MossTerrainBands',small,16)]:
    atlas = Image.new('RGBA',(size*13,size))
    for index, piece in enumerate(pieces):
        atlas.paste(piece,(index*size,0))
    atlas.save(args.output/(name+'.png'))
