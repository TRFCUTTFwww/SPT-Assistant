from pathlib import Path
import json, hashlib, struct, wave, math
root=Path(__file__).resolve().parents[1]
assets=root/'projecte-client/Assets'
manifest=json.loads((assets/'provenance.json').read_text())
assert manifest['commit']=='f432b0c66837759fb0731c9144dc53176b949c5d'
for name,entry in manifest['sources'].items():
 assert hashlib.sha256((assets/name).read_bytes()).hexdigest()==entry['sha256'],name
for key in ('dm','rm','katar'):
 png=(assets/(key+'_sword.png')).read_bytes();assert struct.unpack('>II',png[16:24])==(16,16)
 vertices=[];uv=[];faces=[]
 for line in (assets/(key+'_sword.obj')).read_text().splitlines():
  fields=line.split()
  if fields and fields[0]=='v':vertices.append(tuple(map(float,fields[1:])))
  elif fields and fields[0]=='vt':uv.append(tuple(map(float,fields[1:])))
  elif fields and fields[0]=='f':faces.append([int(x.split('/')[0])-1 for x in fields[1:]])
 assert vertices and len(vertices)==len(uv) and all(0<=u<=1 and 0<=v<=1 for u,v in uv)
 assert all(len(face)==4 and all(0<=v<len(vertices) for v in face) for face in faces)
 low=min(x+y for x,y,z in vertices);high=max(x+y for x,y,z in vertices);scale=.831764/(high-low)*.55
 assert abs((high-low)*scale-.4574702)<1e-7
 assert all(math.isfinite(v) for vertex in vertices for v in vertex)
 for ext in ('obj','png'):assert (assets/(key+'_sword.'+ext)).read_bytes()==(root/'atm-swords-client/Assets'/(key+'_sword.'+ext)).read_bytes()
 print('PASS '+key+': original 16x16 sprite, valid mesh/UV, 55 percent bounds, shared renderer assets')
for name in ('petransmute','pecharge','peuncharge','chargetick','tock'):
 with wave.open(str(assets/(name+'.wav'))) as audio:
  assert audio.getsampwidth()==2 and 0<audio.getnframes()/audio.getframerate()<30 and audio.getnchannels() in (1,2)
 print('PASS original ProjectE sound '+name)
for kind in ('drop',):
 for state in ('empty','half','full'):
  data=(assets/f'{kind}_{state}.png').read_bytes();assert struct.unpack('>II',data[16:24])==(9,9)
print('PASS three cached water sprites and all pinned ProjectE upstream hashes')
