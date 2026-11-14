"""Offline source-hash, mesh topology and native-coordinate checks; Python stdlib only."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import math
import struct

assets = Path(__file__).resolve().parent.parent / 'atm-swords-client' / 'Assets'
source = json.loads((assets / 'provenance.json').read_text(encoding='utf-8-sig'))
for key in ('allthemodium', 'vibranium', 'unobtainium', 'alloy'):
    png = assets / f'{key}_sword.png'
    expected = next(f['sha256'] for f in source['files'] if f['path'].endswith(f'/textures/item/{key}_sword.png'))
    assert hashlib.sha256(png.read_bytes()).hexdigest().upper() == expected
    assert struct.unpack('>II', png.read_bytes()[16:24]) == (16, 16)
    vertices, uvs, faces = [], [], []
    for line in (assets / f'{key}_sword.obj').read_text().splitlines():
        p = line.split()
        if not p:
            continue
        if p[0] == 'v':
            vertices.append(tuple(map(float, p[1:])))
        elif p[0] == 'vt':
            uvs.append(tuple(map(float, p[1:])))
        elif p[0] == 'f':
            assert all(s.split('/')[0] == s.split('/')[1] for s in p[1:])
            faces.append(tuple(int(s.split('/')[0]) - 1 for s in p[1:]))
    assert vertices and len(vertices) == len(uvs) and faces
    assert all(math.isfinite(x) for v in vertices for x in v)
    assert all(0 <= x <= 1 for uv in uvs for x in uv)
    edges = Counter()
    for face in faces:
        assert len(face) == 3 and all(0 <= i < len(vertices) for i in face)
        a, b, c = [vertices[i] for i in face]
        ab, ac = [b[i]-a[i] for i in range(3)], [c[i]-a[i] for i in range(3)]
        cross = (ab[1]*ac[2]-ab[2]*ac[1], ab[2]*ac[0]-ab[0]*ac[2], ab[0]*ac[1]-ab[1]*ac[0])
        assert sum(x*x for x in cross) > 1e-12
        for i, j in zip(face, face[1:]+face[:1]):
            edges[tuple(sorted((vertices[i], vertices[j])))] += 1
    # All visible surface edges are closed, including pixel boundaries.
    assert all(n % 2 == 0 for n in edges.values())
    lo, hi = min(v[0]+v[1] for v in vertices), max(v[0]+v[1] for v in vertices)
    scale = .831764/(hi-lo)
    mapped = [(z*scale*math.sqrt(2), (x-y)*scale-.003738, (x+y-lo)*scale-.152696) for x,y,z in vertices]
    assert abs(min(v[2] for v in mapped)+.152696) < 1e-6
    assert abs(max(v[2] for v in mapped)-.679068) < 1e-6
    assert max(abs(v[0]) for v in mapped) < .05
    print(f'PASS {key}: original PNG hash, 16x16, {len(vertices)} vertices, {len(faces)} nondegenerate closed triangles, gladius coordinate bounds')
