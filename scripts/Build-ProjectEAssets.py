"""Fetch pinned MIT ProjectE assets; extrude original alpha pixels and decode audio.
Requires Pillow and soundfile. No game files or player data are read.
"""
from pathlib import Path
import hashlib, json, urllib.request, wave, shutil
from PIL import Image
import soundfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'projecte-client/Assets'
COMMIT = 'f432b0c66837759fb0731c9144dc53176b949c5d'
BASE = f'https://raw.githubusercontent.com/sinkillerj/ProjectE/{COMMIT}/'
OUT.mkdir(parents=True, exist_ok=True)
sources = {}
def fetch(path, name):
    data = urllib.request.urlopen(BASE + path, timeout=30).read()
    (OUT / name).write_bytes(data)
    sources[name] = dict(path=path, sha256=hashlib.sha256(data).hexdigest())
    return OUT / name

fetch('LICENSE', 'ProjectE-LICENSE.txt')
for key, path in [('dm','dm_tools/sword'),('rm','rm_tools/sword'),('katar','rm_tools/katar')]:
    image = Image.open(fetch(f'src/main/resources/assets/projecte/textures/item/{path}.png', key+'_sword.png')).convert('RGBA')
    w,h = image.size
    vertices=[]; uvs=[]; faces=[]
    # Each opaque pixel is a closed prism; exact original UV and nearest sampling.
    for y in range(h):
        for x in range(w):
            if image.getpixel((x,y))[3]<128: continue
            cube=[(x,h-y-1,-.5),(x+1,h-y-1,-.5),(x+1,h-y,-.5),(x,h-y,-.5),
                  (x,h-y-1,.5),(x+1,h-y-1,.5),(x+1,h-y,.5),(x,h-y,.5)]
            start=len(vertices)+1; vertices.extend(cube)
            uvs.extend([((x+.5)/w,1-(y+.5)/h)]*8)
            for indices in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]:
                faces.append(tuple(start+i for i in indices))
    obj=['# ProjectE original sprite pixel extrusion; source and license adjacent.']
    obj += [f'v {x} {y} {z}' for x,y,z in vertices]
    obj += [f'vt {u} {v}' for u,v in uvs]
    obj += ['f '+' '.join(f'{i}/{i}' for i in face) for face in faces]
    (OUT/(key+'_sword.obj')).write_text('\n'.join(obj)+'\n',encoding='utf-8',newline='\n')
fetch('src/main/resources/assets/projecte/textures/gui/transmute.png','transmute.png')
fetch('src/main/resources/assets/projecte/textures/item/transmutation_tablet.png','tablet.png')
for name in ['petransmute','pecharge','peuncharge','chargetick','tock']:
    source=fetch(f'src/main/resources/assets/projecte/sounds/item/{name}.ogg',name+'.ogg')
    data,rate=soundfile.read(source,dtype='int16',always_2d=True)
    with wave.open(str(OUT/(name+'.wav')),'wb') as output:
        output.setnchannels(data.shape[1]); output.setsampwidth(2); output.setframerate(rate); output.writeframes(data.tobytes())
(OUT/'provenance.json').write_text(json.dumps(dict(repository='https://github.com/sinkillerj/ProjectE',commit=COMMIT,license='MIT',sources=sources),indent=2)+'\n',encoding='utf-8',newline='\n')
for name in ['ProjectE-LICENSE.txt']+[key+'_sword.'+ext for key in ('dm','rm','katar') for ext in ('png','obj')]:
 shutil.copyfile(OUT/name,ROOT/'atm-swords-client/Assets'/name)
shutil.copyfile(OUT/'provenance.json',ROOT/'atm-swords-client/Assets/projecte-provenance.json')
print('Built ProjectE weapons, original GUI texture and five original sound effects.')
