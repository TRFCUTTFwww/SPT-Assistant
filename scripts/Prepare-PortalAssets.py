"""Convert the local Portal 2 prop mesh (LOD0) and copy the selected local assets.
No proprietary assets are checked into the repository. Source MDL49/VVD4/VTX7 only.
Usage: python scripts/Prepare-PortalAssets.py SOURCE OUTPUT
"""
from pathlib import Path
import struct, json, shutil, sys, hashlib

def convert(root, out):
    stem = root / '01_Source_original/models/props/prop_portalgun'
    mdl, vvd, vtx = [stem.with_suffix(ext).read_bytes() for ext in ('.mdl','.vvd','.dx90.vtx')]
    def ints(b, p, n=1): return struct.unpack_from('<'+'i'*n, b, p)
    assert mdl[:4] == b'IDST' and ints(mdl,4)[0] == 49
    assert vvd[:4] == b'IDSV' and ints(vvd,4)[0] == 4 and ints(vtx,0)[0] == 7
    assert ints(mdl,8)[0] == ints(vvd,8)[0] == ints(vtx,16)[0], 'Model checksum mismatch'
    count, fixcount, fixoff, voff = ints(vvd,16)[0], *ints(vvd,48,3)
    remap = []
    for k in range(fixcount):
        lod, start, size = ints(vvd,fixoff+12*k,3)
        if lod >= 0: remap.extend(range(start,start+size))
    if not fixcount: remap = list(range(count))
    assert len(remap) == count
    positions, normals, uvs = [], [], []
    for raw in remap:
        x,y,z,nx,ny,nz,u,v = struct.unpack_from('<8f',vvd,voff+48*raw+16)
        # Source forward X / left Y / up Z -> Unity right X / up Y / forward Z.
        positions.extend((-y*.0254,z*.0254,x*.0254))
        normals.extend((-ny,nz,nx)); uvs.extend((u,1-v))
    triangles = []
    nb, bo = ints(mdl,232,2); tbcount, tbo = ints(vtx,28,2)
    assert nb == tbcount
    for bi in range(nb):
        bp=bo+bi*16; nm=ints(mdl,bp+4)[0]; mo=bp+ints(mdl,bp+12)[0]
        tb=tbo+bi*8; tnm,tmo=ints(vtx,tb,2); assert nm==tnm
        for mi in range(nm):
            model=mo+148*mi; mc,meshbase,nv,vertexbytes=ints(mdl,model+72,4)
            tm=tb+tmo+mi*8; lods,lo=ints(vtx,tm,2); assert lods>0
            lod=tm+lo; tmc,tmesh=ints(vtx,lod,2); assert mc==tmc
            for meshindex in range(mc):
                mesh=model+meshbase+meshindex*116
                material,_,meshverts,meshoff=ints(mdl,mesh,4)
                assert material==0, 'Unexpected material: update converter before using another model'
                tmsh=lod+tmesh+meshindex*9; ng,go=ints(vtx,tmsh,2)
                for gi in range(ng):
                    group=tmsh+go+gi*25
                    vn,vp,indn,indp,sn,sp=ints(vtx,group,6)
                    verts=[struct.unpack_from('<H',vtx,group+vp+j*9+4)[0]+meshoff+vertexbytes//48 for j in range(vn)]
                    indices=struct.unpack_from('<'+'H'*indn,vtx,group+indp)
                    for si in range(sn):
                        strip=group+sp+si*27; ni,io=ints(vtx,strip,2); flags=vtx[strip+18]
                        assert flags&1, 'Expected triangle-list strip'
                        assert ni%3==0
                        for j in range(io,io+ni,3):
                            a,b,c=[verts[indices[j+k]] for k in range(3)]
                            assert min(a,b,c)>=0 and max(a,b,c)<count
                            if len({a,b,c})==3: triangles.extend((a,c,b))
    assert len(triangles)>300 and all(abs(v)<10 for v in positions)
    # Centre the prop so its first-person transform is independent of Source origin.
    bounds=[(min(positions[i::3]),max(positions[i::3])) for i in range(3)]
    for i in range(len(positions)): positions[i]-=sum(bounds[i%3])/2
    with (out/'gun.mesh').open('wb') as f:
        f.write(b'PGM1');f.write(struct.pack('<ii',count,len(triangles)))
        for arr,code in ((positions,'f'),(normals,'f'),(uvs,'f'),(triangles,'i')): f.write(struct.pack('<'+code*len(arr),*arr))
    shutil.copy2(root/'02_PNG_textures/models/weapons/w_models/portalgun/w_portalgun.png',out/'gun.png')
    sounds={
        'blue':'weapons/portalgun/portalgun_shoot_blue1.wav','orange':'weapons/portalgun/portalgun_shoot_red1.wav',
        'open_blue':'weapons/portalgun/portal_open_blue_01.wav','open_orange':'weapons/portalgun/portal_open_red_01.wav',
        'close':'weapons/portalgun/portal_close1.wav','enter':'weapons/portalgun/portal_enter_01.wav',
        'exit':'weapons/portalgun/portal_exit_01.wav','invalid':'weapons/portalgun/portal_invalid_surface_01.wav',
        'equip':'weapons/portalgun_powerup1.wav','unequip':'weapons/portalgun_powerdown.wav'}
    for name,rel in sounds.items(): shutil.copy2(root/'01_Source_original/sound'/rel,out/(name+'.wav'))
    report={'source':str(root),'vertices':count,'triangles':len(triangles)//3,'sourceBoundsMetres':bounds,
            'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.is_file()}}
    (out/'provenance.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':
    output=Path(sys.argv[2]);output.mkdir(parents=True,exist_ok=True)
    convert(Path(sys.argv[1]),output)
