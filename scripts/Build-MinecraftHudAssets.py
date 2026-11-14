"""Extract Java 1.8.9 HUD atlases from Mojang's pinned client (requires Pillow).

Only the three texture PNGs are kept; no client jar is stored in the repository.
Minecraft artwork is not licensed under ProjectE's MIT.
"""
from pathlib import Path
import hashlib, io, json, urllib.request, zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'projecte-client/Assets'
CLIENT_SHA1 = '3870888a6c3d349d3771a3e9d16c9bf5e076b908'
CLIENT_URL = f'https://launcher.mojang.com/v1/objects/{CLIENT_SHA1}/client.jar'
EXPECTED = {
    'gui/icons.png': '3f5c3d10c6ac1931d097354c8c5c0ce0d38f9b4944b2a5772ef5d0ff915e16dd',
    'gui/widgets.png': '1b9e5899e1726b503046c873c11e2215e78566030d4c889d15b6cd5c5b76207f',
    'font/ascii.png': '63189e06bad788841fff43b90ebd19858eb774437b96d2a23578e252d0a5d4d2',
}

def build():
    client = urllib.request.urlopen(CLIENT_URL, timeout=60).read()
    assert hashlib.sha1(client).hexdigest() == CLIENT_SHA1, 'Mojang client SHA-1 mismatch'
    sources = {}
    with zipfile.ZipFile(io.BytesIO(client)) as archive:
        for name, expected in EXPECTED.items():
            path = 'assets/minecraft/textures/' + name
            content = archive.read(path)
            assert hashlib.sha256(content).hexdigest() == expected, path
            target = 'mc_' + Path(name).name
            (OUT / target).write_bytes(content)
            sources[target] = dict(path=path, sha256=expected)
    font = Image.open(OUT / 'mc_ascii.png').convert('RGBA')
    advances = []
    for char in range(128):
        glyph = font.crop(((char % 16) * 8, (char // 16) * 8,
                           (char % 16 + 1) * 8, (char // 16 + 1) * 8))
        bounds = glyph.getchannel('A').getbbox()
        advances.append(4 if char == 32 else (bounds[2] + 1 if bounds else 4))
    manifest = dict(version='1.8.9', clientUrl=CLIENT_URL, clientSha1=CLIENT_SHA1,
                    sources=sources, glyphAdvances=advances,
                    copyright='Minecraft textures copyright Mojang AB / Microsoft; not ProjectE MIT artwork.')
    (OUT / 'mc-hud-provenance.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    (OUT / 'Minecraft-HUD-NOTICE.txt').write_text(
        'Original Minecraft Java Edition 1.8.9 HUD artwork and bitmap font.\n'
        'Copyright Mojang AB / Microsoft. All rights reserved.\n'
        'These textures are not covered by the ProjectE MIT license.\n'
        'Source: ' + CLIENT_URL + '\n'
        'Client SHA-1: ' + CLIENT_SHA1 + '\n'
        'https://www.minecraft.net/eula\nhttps://www.minecraft.net/usage-guidelines\n', encoding='utf-8', newline='\n')
    print('Extracted and verified original Minecraft icons, widgets and bitmap font.')

if __name__ == '__main__':
    build()
