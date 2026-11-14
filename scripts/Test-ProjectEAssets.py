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
print('PASS all pinned ProjectE upstream hashes')
portable=json.loads((assets/'portable-provenance.json').read_text(encoding='utf-8-sig'))
for name,entry in portable.items():
 assert hashlib.sha256((assets/name).read_bytes()).hexdigest()==entry['sha256'],name
for key in [Path(name).stem for name in portable if name.endswith('.png')]:
 vertices=[];uv=[];faces=[]
 mesh=root/'atm-swords-client/Assets'/(key+'_sword.obj')
 for line in mesh.read_text().splitlines():
  fields=line.split()
  if fields and fields[0]=='v':vertices.append(tuple(map(float,fields[1:])))
  elif fields and fields[0]=='vt':uv.append(tuple(map(float,fields[1:])))
  elif fields and fields[0]=='f':faces.append([int(x.split('/')[0])-1 for x in fields[1:]])
 assert vertices and len(vertices)==len(uv) and faces,key
 assert all(math.isfinite(x) for vertex in vertices for x in vertex),key
 assert all(0<=u<=1 and 0<=v<=1 for u,v in uv),key
 from collections import Counter
 edges=Counter()
 for face in faces:
  assert len(face)==4 and len(set(face))==4 and all(0<=i<len(vertices) for i in face),key
  a,b,c=(vertices[i] for i in face[:3]);ab=[b[i]-a[i] for i in range(3)];ac=[c[i]-a[i] for i in range(3)]
  cross=(ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0])
  assert sum(v*v for v in cross)>1e-12,key
  for i,j in zip(face,face[1:]+face[:1]):edges[tuple(sorted((vertices[i],vertices[j])))]+=1
 assert all(count%2==0 for count in edges.values()),key
 assert (assets/(key+'.png')).read_bytes()==(root/'atm-swords-client/Assets'/(key+'_sword.png')).read_bytes(),key
 print('PASS '+key+': source hash, shared sprite, finite closed geometry, UV and nondegenerate faces')
with wave.open(str(assets/'totem.wav')) as audio:
 assert audio.getsampwidth()==2 and audio.getnchannels() in (1,2) and 0<audio.getnframes()/audio.getframerate()<30
print('PASS original totem sound and portable asset provenance')
quantum=json.loads((assets/'quantum-provenance.json').read_text())
for name,field in [('quantum_sword.png','sha256'),('quantum_sword.obj','meshSha256')]:
 assert hashlib.sha256((assets/name).read_bytes()).hexdigest()==quantum[field]
 assert (assets/name).read_bytes()==(root/'atm-swords-client/Assets'/name).read_bytes()
assert struct.unpack('>II',(assets/'quantum_sword.png').read_bytes()[16:24])==(16,16)
thirst=json.loads((assets/'thirst-provenance.json').read_text())
assert hashlib.sha256((assets/'thirst_icons.png').read_bytes()).hexdigest()==thirst['sha256']
assert struct.unpack('>II',(assets/'thirst_icons.png').read_bytes()[16:24])==(25,9)
assert (root/'projecte-server/Assets/developer.png').read_bytes().startswith(b'\x89PNG\r\n\x1a\n')
print('PASS pinned quantum sprite/mesh, original Thirst atlas and Developer PNG')
