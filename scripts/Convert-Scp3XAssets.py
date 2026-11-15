import bisect,collections,hashlib,json,math,shutil,struct,wave,zlib
from pathlib import Path
import argparse
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
parser=argparse.ArgumentParser();parser.add_argument('--game-root',required=True);parser.add_argument('--extracted',required=True);parser.add_argument('--output',required=True);args=parser.parse_args()
SOURCE=Path(args.extracted);OUT=Path(args.output);W=OUT.parent;OUT.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(Path(args.game_root)/'SCPSL_Data/resources.assets'));asset=env.file
index=json.loads((SOURCE/'ASSET_INDEX.json').read_text(encoding='utf-8'));index={r['id']:r for r in index if r['file']=='resources.assets'}
trees={pid:json.loads((SOURCE/r['typetree']).read_text(encoding='utf-8')) for pid,r in index.items()}
nodes=[];paths={};path_ids={}
def vec(v):return [v[k] for k in ['x','y','z']]
def quat(v):return [v[k] for k in ['x','y','z','w']]
def walk(pid,parent,path):
 t=trees[pid];name=index[t['m_GameObject']['m_PathID']]['name'];i=len(nodes);paths[pid]=path;path_ids[pid]=i
 nodes.append(dict(id=pid,name=name,parent=parent,position=vec(t['m_LocalPosition']),rotation=quat(t['m_LocalRotation']),scale=vec(t['m_LocalScale'])))
 for child in t['m_Children']:
  cp=child['m_PathID'];cn=index[trees[cp]['m_GameObject']['m_PathID']]['name'];walk(cp,i,path+'/'+cn if path else cn)
walk(42989,-1,'')
crc={}
for pid,path in paths.items():
 parts=path.split('/')
 for n in range(len(parts)):crc[zlib.crc32('/'.join(parts[n:]).encode())]=path_ids[pid]

def mesh_binary(pid):
 obj=asset.objects[pid].read();h=MeshHandler(obj);h.process();submeshes=h.get_triangles();bindposes=obj.m_BindPose or []
 path=OUT/f'mesh-{pid}.bin'
 with path.open('wb') as f:
  f.write(struct.pack('<4siii',b'3XM1',h.m_VertexCount,len(submeshes),len(bindposes)))
  for i,v in enumerate(h.m_Vertices):
   normal=(h.m_Normals[i][:3] if h.m_Normals else (0,1,0));uv=h.m_UV0[i][:2] if h.m_UV0 else (0,0);bi=h.m_BoneIndices[i] if h.m_BoneIndices else (0,0,0,0);bw=h.m_BoneWeights[i] if h.m_BoneWeights else (1,0,0,0)
   bi=tuple(bi)+(0,)*(4-len(bi));bw=tuple(bw)+(0,)*(4-len(bw))
   f.write(struct.pack('<8f4i4f',*v,*normal,*uv,*map(int,bi),*bw))
  for sub in submeshes:
   tris=[i for tri in sub for i in tri];f.write(struct.pack('<i',len(tris)));f.write(struct.pack('<'+'i'*len(tris),*tris))
  for matrix in bindposes:f.write(struct.pack('<16f',*[getattr(matrix,f'e{r}{c}') for r in range(4) for c in range(4)]))
 return dict(file=path.name,vertices=h.m_VertexCount,submeshes=len(submeshes),bindposes=len(bindposes))
renderers=[]
for pid in [64262,64558,65222]:
 r=trees[pid];go=r['m_GameObject']['m_PathID'];transform=next(c['component']['m_PathID'] for c in trees[go]['m_Component'] if c['component']['m_PathID'] in path_ids)
 renderers.append(dict(node=path_ids[transform],mesh=r['m_Mesh']['m_PathID'],bones=[path_ids[b['m_PathID']] for b in r['m_Bones']],rootBone=path_ids[r['m_RootBone']['m_PathID']],materials=[m['m_PathID'] for m in r['m_Materials']]))
meshids={r['mesh'] for r in renderers}|{3782}
meshes={str(pid):mesh_binary(pid) for pid in meshids}
materials={}
for mid in [354,355,356,357,358,359]:
 d=trees[mid];maps={}
 for prop,v in d['m_SavedProperties']['m_TexEnvs']:
  tid=v['m_Texture']['m_PathID']
  if tid and tid in index and index[tid]['type']=='Texture2D':
   filename=f'tex-{tid}.png';shutil.copyfile(SOURCE/index[tid]['preview'],OUT/filename);maps[prop]=filename
 materials[str(mid)]=dict(name=d['m_Name'],textures=maps)

def decode(a):
 clip=a['m_MuscleClip']['m_Clip']['data'];s=clip['m_StreamedClip'];raw=struct.pack('<'+'I'*len(s['data']),*s['data']);pos=0;curves=collections.defaultdict(list)
 while pos<len(raw):
  t,count=struct.unpack_from('<fi',raw,pos);pos+=8
  for _ in range(count):
   idx,*coeff=struct.unpack_from('<i4f',raw,pos);pos+=20
   if t>-1e10 and math.isfinite(t):curves[idx].append((t,coeff))
 dense=clip['m_DenseClip'];constant=clip.get('m_ConstantClip',{}).get('data',[]);offset=s['curveCount']+dense['m_CurveCount'];bindings=[];channel=0
 for b in a['m_ClipBindingConstant']['genericBindings']:
  size={1:3,2:4,3:3,4:3}.get(b['attribute'],1)
  if b['typeID']==4 and b['path'] in crc and b['attribute'] in [1,2,3,4]:bindings.append(dict(node=crc[b['path']],attribute=b['attribute'],channel=channel,size=size))
  channel+=size
 duration=a['m_MuscleClip']['m_StopTime'];frames=math.ceil(duration*60)+1
 def sample(c,t):
  if c<s['curveCount']:
   keys=curves[c];i=bisect.bisect_right([k[0] for k in keys],t)-1;i=max(0,i);kt,coeff=keys[i];dt=max(0,t-kt)
   if i==len(keys)-1:return coeff[3]
   return ((coeff[0]*dt+coeff[1])*dt+coeff[2])*dt+coeff[3]
  if c<offset:
   c-=s['curveCount'];f=min(dense['m_FrameCount']-1,max(0,int((t-dense['m_BeginTime'])*dense['m_SampleRate'])));return dense['m_SampleArray'][f*dense['m_CurveCount']+c]
  return constant[c-offset]
 tracks=[];values=[]
 for b in bindings:
  tracks.append(dict(node=b['node'],attribute=b['attribute'],offset=len(values),size=b['size']))
  for frame in range(frames):
   v=[sample(b['channel']+i,min(duration,frame/60)) for i in range(b['size'])]
   if b['attribute']==2:
    norm=math.sqrt(sum(x*x for x in v));v=[x/norm for x in v] if norm>1e-6 else [0,0,0,1]
   assert all(math.isfinite(x) for x in v)
   values.extend(v)
 return dict(duration=duration,frames=frames,sampleRate=60,tracks=tracks,bindings=len(bindings),unmatched=sum(b['typeID']==4 and b['path'] not in crc for b in a['m_ClipBindingConstant']['genericBindings'])),values
animations={}
for name in ['idle_pose','draw','pickup','shoot_single','shoot_rapid','break_single','break_rapid','reload','inspect','ads_in','ads_out']:
 p=next((SOURCE/'Animations').glob(name+'__*.animation.json'));a=json.loads(p.read_text(encoding='utf-8'));desc,values=decode(a);desc['file']='anim-'+name+'.bin';animations[name]=desc
 (OUT/desc['file']).write_bytes(struct.pack('<'+'f'*len(values),*values));print('ANIMATION',name,desc['duration'],'tracks',len(desc['tracks']),'unmatched',desc['unmatched'],flush=True)
assert all(a['unmatched']==0 for a in animations.values())
for r in index.values():
 if r['type']=='AudioClip' and (r['name'].startswith('3x_') or r['name']=='SingleDisruptorTracer'):
  shutil.copyfile(SOURCE/r['decoded'][0],OUT/(r['name']+'.wav'))
shutil.copyfile(SOURCE/index[898]['preview'],OUT/'icon.png')
world_filter=next(pid for pid,r in index.items() if r['type']=='MeshFilter' and trees[pid].get('m_Mesh',{}).get('m_PathID')==3782)
world_go=trees[world_filter]['m_GameObject']['m_PathID']
world_renderer=next(c['component']['m_PathID'] for c in trees[world_go]['m_Component'] if index[c['component']['m_PathID']]['type']=='MeshRenderer')
world_materials=[m['m_PathID'] for m in trees[world_renderer]['m_Materials']]
assert all(str(m) in materials for m in world_materials)
print('WORLD_MATERIALS',world_materials,flush=True)
manifest=dict(nodes=nodes,renderers=renderers,meshes=meshes,materials=materials,worldMesh=3782,worldMaterials=world_materials,animations=animations)
(OUT/'weapon.json').write_text(json.dumps(manifest,separators=(',',':')),encoding='utf-8')
report=dict(source='SCP Secret Laboratory / Unity 6000.4.11f1',meshes=meshes,rigNodes=len(nodes),animations={k:dict(duration=v['duration'],tracks=len(v['tracks']),unmatched=v['unmatched']) for k,v in animations.items()},files={p.name:dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in OUT.iterdir() if p.is_file()})
(W/'asset-conversion-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('CONVERTED',len(report['files']),'files',flush=True)
