"""Build local PNG glyph atlases from the verified CP2077 font supplement."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import re
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--ui-root', type=Path, required=True)
parser.add_argument('--source-root', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--module-path')
args = parser.parse_args()
if args.module_path:
    sys.path.insert(0, args.module_path)
from PIL import Image, ImageDraw, ImageFont
from fontTools.ttLib import TTFont

manifest = {r['Path']: r for r in csv.DictReader(
    (args.ui_root / 'RESOURCE_MANIFEST.csv').read_text(encoding='utf-8-sig').splitlines())}
source = ''.join(p.read_text(encoding='utf-8-sig') for p in args.source_root.glob('*.cs'))
characters = set(chr(c) for c in range(32, 127)) | {c for c in source if ord(c) >= 128}
han = set(re.findall(r'[\u4e00-\u9fff]', source))
fonts = {
    'font_body': 'foreign/chinese/jing_xi_heig/arudjingxiheig30_db.ttf',
    'font_code': 'orbitron/orbitron-regular.ttf',
    'font_latin': 'raj/raj-semibold.ttf',
}
args.output.mkdir(parents=True, exist_ok=True)
for key, relative in fonts.items():
    depot = '04_fonts/base/gameplay/gui/fonts/' + relative
    path = args.ui_root / depot
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest.lower() != manifest[depot]['SHA256'].lower():
        raise ValueError('Font source hash mismatch: ' + depot)
    with TTFont(path, checkChecksums=2) as ttf:
        cmap = ttf.getBestCmap()
        if key == 'font_body' and any(ord(c) not in cmap for c in han):
            raise ValueError('Original Chinese font does not cover the current UI')
    chars = sorted((c for c in characters if ord(c) in cmap), key=ord)
    font = ImageFont.truetype(str(path), 64)
    boxes = {c: font.getbbox(c, anchor='lt') for c in chars}
    # One transparent pixel gutter on every edge prevents bilinear bleed.
    cell_w = max(72, max(b[2] - b[0] for b in boxes.values()) + 4)
    cell_h = max(88, max(b[3] - b[1] for b in boxes.values()) + 4)
    width = 2048
    columns = width // cell_w
    rows = math.ceil(len(chars) / columns)
    height = 1 << math.ceil(math.log2(max(1, rows * cell_h)))
    image = Image.new('RGBA', (width, height), (255, 255, 255, 0))
    draw = ImageDraw.Draw(image)
    glyphs = {}
    for i, c in enumerate(chars):
        x, y = (i % columns) * cell_w + 2, (i // columns) * cell_h + 2
        left, top, right, bottom = boxes[c]
        w, h = right - left, bottom - top
        if w and h:
            draw.text((x - left, y - top), c, font=font, fill='white', anchor='lt')
        # Baseline metrics keep mixed Chinese/Latin text on one baseline.
        baseline_box = font.getbbox(c, anchor='ls')
        ascent = font.getmetrics()[0]
        glyphs[str(ord(c))] = dict(x=x, y=y, width=w, height=h,
            left=baseline_box[0], top=ascent + baseline_box[1], advance=font.getlength(c))
    image.save(args.output / (key + '.png'))
    metadata = dict(version=1, pixels=64, width=width, height=height,
        ascent=font.getmetrics()[0], descent=font.getmetrics()[1],
        source=depot, sourceSha256=digest, glyphs=glyphs)
    (args.output / (key + '.json')).write_text(json.dumps(metadata, ensure_ascii=False), encoding='utf-8')
    print(f'{key}: {len(chars)} glyphs, {width}x{height}, verified source {digest[:12]}')
