"""Verify vertex-color rendering preserves all seven original pixel surfaces.
Requires Pillow. Checks colors, alpha, UV orientation and outward face winding
before enabling back-face culling; reads no game or profile files.
"""
from pathlib import Path
from collections import defaultdict
from PIL import Image

assets = Path(__file__).resolve().parents[1] / 'atm-swords-client/Assets'
for obj in sorted(assets.glob('*_sword.obj')):
    image = Image.open(obj.with_suffix('.png')).convert('RGBA')
    vertices, uvs, faces = [], [], []
    for line in obj.read_text().splitlines():
        p = line.split()
        if p and p[0] == 'v': vertices.append(tuple(map(float, p[1:])))
        elif p and p[0] == 'vt': uvs.append(tuple(map(float, p[1:])))
        elif p and p[0] == 'f': faces.append([int(v.split('/')[0])-1 for v in p[1:]])
    groups = defaultdict(list)
    colors = []
    for i, (u, v) in enumerate(uvs):
        x, y = int(u*image.width), image.height-1-int(v*image.height)
        groups[(x, y)].append(vertices[i])
        colors.append(image.getpixel((x, y)))
    expected = {(x, y) for y in range(image.height) for x in range(image.width)
                if image.getpixel((x, y))[3] >= 128}
    assert set(groups) == expected, (obj.name, 'pixel coverage or UV orientation')
    assert all(c[3] == 255 for c in colors), (obj.name, 'opaque depth path requires opaque source pixels')
    centers = {key: tuple(sum(p[k] for p in points)/len(points) for k in range(3))
               for key, points in groups.items()}
    for face in faces:
        assert len({colors[i] for i in face}) == 1, (obj.name, 'vertex color interpolation would alter face')
        a, b, c = [vertices[i] for i in face[:3]]
        ab, ac = [b[k]-a[k] for k in range(3)], [c[k]-a[k] for k in range(3)]
        normal = (ab[1]*ac[2]-ab[2]*ac[1], ab[2]*ac[0]-ab[0]*ac[2], ab[0]*ac[1]-ab[1]*ac[0])
        u, v = uvs[face[0]]
        center = centers[(int(u*image.width), image.height-1-int(v*image.height))]
        face_center = [sum(vertices[i][k] for i in face)/len(face) for k in range(3)]
        assert sum(normal[k]*(face_center[k]-center[k]) for k in range(3)) > 0, (obj.name, 'inward face would disappear with culling')
    print(f'PASS {obj.stem}: {len(groups)} original pixels, constant face colors, opaque source, outward winding')
assert len(list(assets.glob('*_sword.obj'))) == 7
