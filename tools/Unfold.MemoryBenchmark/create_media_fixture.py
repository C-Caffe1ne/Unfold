import argparse, json, pathlib
try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("Pillow is required: python3 -m venv .venv; .venv/bin/python -m pip install Pillow; run with that Python.")
parser = argparse.ArgumentParser(description="Generate the original 512px/120-frame/80ms memory fixture.")
parser.add_argument("output", type=pathlib.Path)
root = parser.parse_args().output
root.mkdir(parents=True, exist_ok=False)
frames=[]
palette=[value for i in range(256) for value in (i, (i*3)%256, (i*7)%256)]
for frame in range(120):
    image=Image.new('P',(512,512),0)
    image.putpalette(palette)
    draw=ImageDraw.Draw(image)
    draw.ellipse((32,32,480,480),fill=frame+1)
    draw.rectangle((180+(frame%20),180,320,320),fill=(frame+53)%255+1)
    frames.append(image)
frames[0].save(root/'idle.gif',save_all=True,append_images=frames[1:],duration=80,loop=0,optimize=False,transparency=0,disposal=2)
frames[0].convert('RGBA').save(root/'spritesheet.png')
manifest={'id':'media-stress','name':'512px 120frames memory fixture','version':1,
 'spriteSheet':{'file':'spritesheet.png','columns':1,'rows':1,'frameWidth':512,'frameHeight':512},
 'animations':{'idle':{'gif':'idle.gif','loop':True}},'renderStyle':'smooth'}
(root/'character.json').write_text(json.dumps(manifest))
print(json.dumps({'gifBytes':(root/'idle.gif').stat().st_size,'width':512,'height':512,'frames':120,'decodedBytes':512*512*120*4}))
