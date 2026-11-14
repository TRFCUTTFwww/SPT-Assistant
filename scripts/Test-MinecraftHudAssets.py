"""Verify original HUD bytes and native-size atlas composition; optional offline preview."""
from pathlib import Path
import argparse, hashlib, json
from PIL import Image

root=Path(__file__).resolve().parents[1]
assets=root/'projecte-client/Assets'
manifest=json.loads((assets/'mc-hud-provenance.json').read_text())
assert manifest['version']=='1.8.9' and manifest['clientSha1']=='3870888a6c3d349d3771a3e9d16c9bf5e076b908'
for name,entry in manifest['sources'].items():
    assert hashlib.sha256((assets/name).read_bytes()).hexdigest()==entry['sha256'], name
icons=Image.open(assets/'mc_icons.png').convert('RGBA')
widgets=Image.open(assets/'mc_widgets.png').convert('RGBA')
font=Image.open(assets/'mc_ascii.png').convert('RGBA')
assert icons.size==widgets.size==(256,256) and font.size==(128,128)
advances=manifest['glyphAdvances']
assert len(advances)==128 and all(advances[ord(c)]==6 for c in '0123456789')

def crop(image,x,y,w,h):return image.crop((x,y,x+w,y+h))
def hotbar(count):
    result=Image.new('RGBA',(count*20+2,22))
    original=min(count,9)*20+1
    result.paste(crop(widgets,0,0,original,22),(0,0))
    for i in range(9,count):result.paste(crop(widgets,81,0,20,22),(1+i*20,0))
    result.paste(crop(widgets,181,0,1,22),(count*20+1,0))
    return result

assert hotbar(9).tobytes()==crop(widgets,0,0,182,22).tobytes(), 'Nine-slot composition must exactly match vanilla'
for count in (10,12):
    bar=hotbar(count)
    assert bar.size==(count*20+2,22)
    assert crop(bar,0,0,1,22).tobytes()==crop(widgets,0,0,1,22).tobytes()
    assert crop(bar,count*20+1,0,1,22).tobytes()==crop(widgets,181,0,1,22).tobytes()
assert crop(icons,0,64,182,5).getbbox() and crop(icons,0,69,182,5).getbbox()
assert crop(widgets,0,22,24,22).getbbox()
print('PASS official atlas hashes, original digit metrics, exact nine-slot pixels, 10/12-slot borders, XP and selection crops')

parser=argparse.ArgumentParser();parser.add_argument('--preview',type=Path);args=parser.parse_args()
if args.preview:
    def text(image,x,y,value,color):
        for c in str(value):
            code=ord(c);glyph=crop(font,code%16*8,code//16*8,8,8)
            ink=Image.new('RGBA',(8,8),color);ink.putalpha(glyph.getchannel('A'));image.alpha_composite(ink,(int(x),int(y)));x+=advances[code]
    def hud(hp,food,xp,level,count,selected):
        width=count*20+2;result=Image.new('RGBA',(width+8,60));left=4;top=34;stats=(width-182)//2
        result.alpha_composite(hotbar(count),(left,top))
        result.alpha_composite(crop(widgets,0,22,24,22),(left+selected*20-1,top-1))
        for i in range(10):
            for value,row,x in ((hp,0,left+stats+i*8),(food,27,left+stats+173-i*8)):
                result.alpha_composite(crop(icons,16,row,9,9),(x,top-17))
                units=value-i*2
                if units>0:result.alpha_composite(crop(icons,52 if units>1 else 61,row,9,9),(x,top-17))
            result.alpha_composite(Image.open(assets/'drop_full.png').convert('RGBA'),(left+stats+173-i*8,top-27))
        result.alpha_composite(crop(icons,0,64,182,5),(left+stats,top-7))
        fill=min(182,int(xp*183))
        if fill:result.alpha_composite(crop(icons,0,69,fill,5),(left+stats,top-7))
        x=left+(width-sum(advances[ord(c)] for c in str(level)))//2;y=top-13
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):text(result,x+dx,y+dy,level,(0,0,0,255))
        text(result,x,y,level,(128,255,32,255))
        labels=list('1234567890') if count==10 else ['V']+list('1234567890')+['G']
        for i,key in enumerate(labels[:count]):
            text(result,left+i*20+3,top+2,key,(255,255,255,255))
        for i,key in ((0,'dm'),(1,'rm'),(2,'katar')):
            result.alpha_composite(Image.open(assets/(key+'_sword.png')).convert('RGBA'),(left+i*20+3,top+3))
        return result
    canvas=Image.new('RGBA',(1000,560),(42,49,57,255))
    for index,values in enumerate(((20,20,.7,42,12,2),(13,7,.35,17,10,1),(1,1,.02,1,12,0))):
        row=hud(*values);row=row.resize((row.width*3,row.height*3),Image.Resampling.NEAREST)
        canvas.alpha_composite(row,((1000-row.width)//2,index*180))
    args.preview.parent.mkdir(parents=True,exist_ok=True);canvas.save(args.preview)
    print('Saved offline composition preview:',args.preview)
