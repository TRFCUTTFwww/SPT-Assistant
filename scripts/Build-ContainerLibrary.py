"""Build local, map-independent native container prefabs from this EFT installation.

Requires UnityPy. Output contains game assets and must never be redistributed or
committed. No Unity scenes are loaded or executed. Each bundle contains one small
furniture hierarchy, its native interaction components and its asset dependencies.
"""
import argparse
import copy
import hashlib
import json
import pathlib
import re
import struct
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--game', required=True, type=pathlib.Path)
parser.add_argument('--output', required=True, type=pathlib.Path)
parser.add_argument('--modules', type=str)
parser.add_argument('--template', default='')
parser.add_argument('--claymore', action='store_true', help='Extract a local native directional mine instead of containers (EFT 48558).')
args = parser.parse_args()
if args.modules:
    sys.path.insert(0, args.modules)
import UnityPy
from UnityPy.streams import EndianBinaryReader
from UnityPy.files.SerializedFile import FileIdentifier
from UnityPy.helpers.Tpk import get_typetree_node
from UnityPy.helpers.TypeTreeNode import TypeTreeNode

data = args.game / 'EscapeFromTarkov_Data'
items = json.loads((args.game / 'SPT_Runtime/SPT_Data/database/templates/items.json').read_text(encoding='utf-8'))
templates = {k: v['_name'] for k, v in items.items() if v.get('_parent') == '566965d44bdc2d814c8b4571'}
if args.claymore:
    templates = {'claymore': 'Native MON-50'}
    args.template = 'claymore'
args.output.mkdir(parents=True, exist_ok=True)
index_path = args.output / 'catalog.json'
index = json.loads(index_path.read_text(encoding='utf-8')) if index_path.exists() else {}
# Never advertise old archives built before the flattened Unity bundle format.
index = {k: v for k, v in index.items() if v.get('version') == 2}
index_path.write_text(json.dumps(index, indent=2), encoding='utf-8')
if args.template and args.template not in templates:
    raise ValueError('Unknown container template')
script_nodes = {}
native_prefab = data / 'StreamingAssets/Windows/assets/content/prefabs/levelobjects/indoor/suitcase_plastic_lootable.bundle'
for obj in UnityPy.load(str(native_prefab)).objects:
    if obj.type.name == 'MonoBehaviour' and obj.serialized_type.node:
        script_nodes[obj.serialized_type.script_id] = obj.serialized_type.node


def mine_schema(name):
    # Actual 48558 serialized fields. Validate byte-for-byte before exporting;
    # fail on other layouts rather than inventing damage/trigger parameters.
    sample = next(iter(script_nodes.values()))
    keys = ('m_Level', 'm_Type', 'm_Name', 'm_ByteSize', 'm_MetaFlag', 'm_Version')
    nodes = [{k: getattr(n, k) for k in keys} for n in list(sample.traverse())[:12]]
    def add(level, kind, field, size=4, flags=0):
        nodes.append(dict(m_Level=level, m_Type=kind, m_Name=field, m_ByteSize=size, m_MetaFlag=flags, m_Version=1))
    def string(field):
        add(2, 'string', field, -1, 32768); add(3, 'Array', 'Array', -1, 16384)
        add(4, 'int', 'size'); add(4, 'char', 'data', 1)
    if name == 'MineDirectional':
        add(1, 'MineSettings', '_mineData', -1, 32768)
        for field in ('_blindness', '_contusion', '_armorDistanceDistanceDamage'):
            add(2, 'Vector3f', field, 12)
            for axis in ('x', 'y', 'z'): add(3, 'float', axis)
        for field in ('_minExplosionDistance', '_maxExplosionDistance'): add(2, 'float', field)
        add(2, 'int', '_fragmentsCount'); add(2, 'float', '_strength'); string('_tag')
        for field in ('_armorDamage', '_staminaBurnRate', '_penetrationPower'): add(2, 'float', field)
        string('_fragmentType'); string('_fxName'); add(2, 'int', '_ignoreRole')
        add(2, 'float', '_directionalDamageAngle'); add(2, 'float', '_directionalDamageMultiplier')
    for i, n in enumerate(nodes): n.update(m_Index=i, m_TypeFlags=0, m_RefTypeHash=0)
    return TypeTreeNode.from_list(nodes)


def ptrs(tree):
    if isinstance(tree, dict):
        if set(tree) == {'m_FileID', 'm_PathID'}:
            yield tree
        else:
            for value in tree.values():
                yield from ptrs(value)
    elif isinstance(tree, (list, tuple)):
        for value in tree:
            yield from ptrs(value)


def build(level, template):
    # Per-template state bounds memory and allows rebuilding individual entries.
    files = {}
    def load(name):
        if name not in files:
            env = UnityPy.load(str(data / name))
            files[name] = next(iter(env.files.values()))
        return files[name]
    scene = load(level)
    if args.claymore:
        scripts = load('globalgamemanagers.assets')
        def script_name(obj):
            fid, pid = struct.unpack_from('<iq', obj.get_raw_data(), 16)
            if not fid or pathlib.Path(scene.externals[fid-1].path).name != 'globalgamemanagers.assets': return ''
            return scripts.objects[pid].read_typetree()['m_ClassName']
        source = next(o for o in scene.objects.values() if o.type.name == 'MonoBehaviour' and script_name(o) == 'MineDirectional')
    else:
        source = next(o for o in scene.objects.values() if o.type.name == 'MonoBehaviour' and template.encode() in o.get_raw_data())
    goid = struct.unpack_from('<q', source.get_raw_data(), 4)[0]
    def transform(go):
        return next(scene.objects[c['component']['m_PathID']] for c in go.read_typetree()['m_Component'] if scene.objects[c['component']['m_PathID']].type.name == 'Transform')
    root = scene.objects[goid]
    # Doors/drawers are children of the actual cabinet/chest. Keep furniture,
    # never its LOOTABLE / building / map parent.
    tr = transform(root).read_typetree()
    father = tr['m_Father']['m_PathID']
    if father and not args.claymore:
        parent = scene.objects[scene.objects[father].read_typetree()['m_GameObject']['m_PathID']]
        name = parent.read_typetree()['m_Name'].lower()
        if name not in ('lootable', 'loot', 'containers', 'oo') and any(s in name for s in ('card_file', 'safe', 'crate', 'box', 'case', 'register', 'computer', 'pc_', 'bag', 'jacket', 'cache', 'corpse', 'container', 'medkit')):
            root = parent
    hierarchy = set()
    def subtree(go):
        if go.path_id in hierarchy:
            return
        hierarchy.add(go.path_id)
        go_tree = go.read_typetree()
        if args.claymore:
            # Navigation authoring is map-specific; native mine triggers remain.
            go_tree['m_Component'] = [c for c in go_tree['m_Component'] if scene.objects[c['component']['m_PathID']].type.name != 'MonoBehaviour' or script_name(scene.objects[c['component']['m_PathID']]) in ('MineDirectional', 'MineDirectionalColliders')]
            go.save_typetree(go_tree)
        for component in go_tree['m_Component']:
            cid = component['component']['m_PathID']
            hierarchy.add(cid)
            comp = scene.objects[cid]
            if args.claymore and comp.type.name == 'MonoBehaviour':
                node = mine_schema(script_name(comp)); before = comp.get_raw_data()
                tree = comp.read_typetree(nodes=node); comp.save_typetree(tree, nodes=node)
                if before != comp.get_raw_data(): raise ValueError('Native mine schema round-trip mismatch')
                script_nodes[comp.serialized_type.script_id] = node
            if comp.type.name == 'Transform':
                for child in comp.read_typetree()['m_Children']:
                    subtree(scene.objects[scene.objects[child['m_PathID']].read_typetree()['m_GameObject']['m_PathID']])
    subtree(root)
    root_tr = transform(root)
    tree = root_tr.read_typetree()
    tree['m_Father'] = {'m_FileID': 0, 'm_PathID': 0}
    tree['m_LocalPosition'] = dict(x=0., y=0., z=0.)
    # Preserve the furniture's authored orientation (many cabinets use Z-up).
    root_tr.save_typetree(tree)
    tree = root.read_typetree()
    tree['m_IsActive'] = False
    root.save_typetree(tree)
    keep, queue, trees = {}, [(level, p) for p in hierarchy], {}
    builtin = {'globalgamemanagers.assets', 'resources.assets', 'unity default resources', 'unity_builtin_extra'}
    def reference(owner, p):
        if not p['m_PathID']:
            return
        name = owner.name if not p['m_FileID'] else pathlib.Path(owner.externals[p['m_FileID'] - 1].path).name
        if name in builtin:
            return
        if not (data / name).is_file():
            raise ValueError('Missing native dependency: ' + name)
        target = load(name).objects.get(p['m_PathID'])
        if target is None:
            return
        if target.type.name in ('GameObject', 'Transform', 'MonoBehaviour', 'LODGroup', 'BoxCollider', 'MeshCollider', 'SphereCollider', 'CapsuleCollider', 'MeshRenderer', 'MeshFilter', 'Rigidbody') and (name != level or target.path_id not in hierarchy):
            return
        queue.append((name, target.path_id))
    while queue:
        name, pid = queue.pop()
        if pid in keep.setdefault(name, set()):
            continue
        keep[name].add(pid)
        owner = load(name)
        obj = owner.objects[pid]
        if obj.type.name == 'MonoBehaviour':
            # Player builds strip script type trees. Preserve native script data
            # byte-for-byte; collect only valid PPtrs to assets or this furniture.
            raw = obj.get_raw_data()
            for offset in range(0, len(raw) - 11, 4):
                fid, rid = struct.unpack_from('<iq', raw, offset)
                if 0 <= fid <= len(owner.externals) and rid:
                    reference(owner, dict(m_FileID=fid, m_PathID=rid))
        else:
            tree = obj.read_typetree()
            trees[name, pid] = tree
            for p in ptrs(tree):
                reference(owner, p)
        if sum(map(len, keep.values())) > 15000:
            raise ValueError('Container dependency graph exceeded safety limit')
    # Merge dependencies into one serialized file: Unity resolves in-bundle
    # references reliably when the prefab, meshes and materials share one file.
    unique = hashlib.sha256((level + template).encode()).hexdigest()[:20]
    streams = {}
    def stream_tree(tree):
        if isinstance(tree, dict):
            if 'path' in tree and 'offset' in tree and 'size' in tree and tree['path'] and tree['size']:
                original = data / pathlib.Path(tree['path']).name
                key = (str(original), tree['offset'], tree['size'])
                if key not in streams:
                    path = 'editor-' + unique + '-' + str(len(streams)) + '.resource'
                    with original.open('rb') as f:
                        f.seek(tree['offset'])
                        payload = f.read(tree['size'])
                    if len(payload) != tree['size']:
                        raise ValueError('Truncated native stream')
                    streams[key] = path, payload
                tree['path'], tree['offset'] = streams[key][0], 0
            else:
                for v in tree.values():
                    stream_tree(v)
        elif isinstance(tree, (list, tuple)):
            for v in tree:
                stream_tree(v)
    root_id = root.path_id
    objects = {(name, pid): load(name).objects[pid] for name, ids in keep.items() for pid in ids}
    identities = {key: 1000000 + i for i, key in enumerate(objects)}
    externals = []
    def remap(owner, ptr):
        if not ptr['m_PathID']:
            return
        name = level if owner is scene else owner.name
        if ptr['m_FileID']:
            name = pathlib.Path(owner.externals[ptr['m_FileID'] - 1].path).name
        key = name, ptr['m_PathID']
        if key in identities:
            ptr['m_FileID'], ptr['m_PathID'] = 0, identities[key]
        elif name == level or name in keep:
            ptr['m_FileID'], ptr['m_PathID'] = 0, 0
        else:
            if name not in externals:
                externals.append(name)
            ptr['m_FileID'] = externals.index(name) + 1
    sample = data / 'StreamingAssets/Windows/assets/content/weapons/gladius/weapon_apoc_wasteland_gladius_container.bundle'
    env = UnityPy.load(str(sample))
    bundle = next(iter(env.files.values()))
    manifest = next(f for f in bundle.files.values() if hasattr(f, 'objects'))
    asset = next(o for o in manifest.objects.values() if o.type.name == 'AssetBundle')
    manifest.objects = {1: asset}
    manifest.types = [asset.serialized_type]
    asset.type_id = 0
    manifest.script_types = []
    manifest.ref_types = []
    typeids = {}
    for key, obj in objects.items():
        owner = obj.assets_file
        st = obj.serialized_type
        node = st.node or (script_nodes.get(st.script_id) if st.class_id == 114 else get_typetree_node(st.class_id, owner.version))
        if node is None:
            raise ValueError('Missing native script schema: ' + str(st.script_id))
        tree = obj.read_typetree(nodes=node)
        for pointer in ptrs(tree):
            remap(owner, pointer)
        stream = tree.get('m_StreamData', {})
        if args.claymore and stream.get('size') and obj.type.name in ('Mesh', 'Texture2D'):
            with (data / pathlib.Path(stream['path']).name).open('rb') as source:
                source.seek(stream['offset'])
                payload = source.read(stream['size'])
            if len(payload) != stream['size']:
                raise ValueError('Truncated claymore render data')
            if obj.type.name == 'Mesh':
                tree['m_VertexData']['m_DataSize'] = payload
            else:
                tree['image data'] = payload
            stream.update(path='', offset=0, size=0)
        else:
            stream_tree(tree)
        typekey = st.class_id, st.script_id, st.old_type_hash
        if typekey not in typeids:
            newtype = copy.copy(st)
            newtype.node = node
            for position, child in enumerate(node.traverse()):
                for field, fallback in (('m_TypeFlags', 0), ('m_Index', position), ('m_MetaFlag', 0), ('m_RefTypeHash', 0)):
                    if getattr(child, field) is None:
                        setattr(child, field, fallback)
            newtype.type_dependencies = ()
            if st.class_id == 114:
                script = copy.copy(owner.script_types[st.script_type_index])
                pointer = dict(m_FileID=script.local_serialized_file_index, m_PathID=script.local_identifier_in_file)
                remap(owner, pointer)
                script.local_serialized_file_index, script.local_identifier_in_file = pointer['m_FileID'], pointer['m_PathID']
                newtype.script_type_index = len(manifest.script_types)
                manifest.script_types.append(script)
            typeids[typekey] = len(manifest.types)
            manifest.types.append(newtype)
        obj.path_id = identities[key]
        obj.type_id = typeids[typekey]
        obj.serialized_type = manifest.types[obj.type_id]
        obj.assets_file = manifest
        obj.save_typetree(tree, nodes=node)
        manifest.objects[obj.path_id] = obj
    manifest.externals = []
    for name in externals:
        ext = copy.copy(scene.externals[0])
        ext.path = name
        manifest.externals.append(ext)
    table = [dict(m_FileID=0, m_PathID=pid) for pid in identities.values()]
    tree = asset.read_typetree()
    tree.update(m_Name='assistant-editor-' + template, m_AssetBundleName='assistant-editor-' + template, m_Dependencies=[], m_PreloadTable=table, m_Container=[(template + '.prefab', dict(preloadIndex=0, preloadSize=len(table), asset=dict(m_FileID=0, m_PathID=identities[level, root_id])))], m_MainAsset=dict(preloadIndex=0, preloadSize=0, asset=dict(m_FileID=0, m_PathID=0)), m_SceneHashes=[])
    asset.save_typetree(tree)
    revision = 'inline-v3' if args.claymore else 'container-library-v2'
    manifest.name = 'CAB-' + hashlib.sha256((template+revision).encode()).hexdigest()[:32]
    bundle.files = {manifest.name: manifest}
    for stream, payload in streams.values():
        reader = EndianBinaryReader(payload)
        reader.flags = 0
        bundle.files[stream] = reader
    destination = args.output / (template + '.bundle')
    temporary = destination.with_suffix('.tmp')
    temporary.write_bytes(bundle.save(packer='lz4'))
    # Verify the archive can be parsed and exposes exactly the selected prefab.
    check = UnityPy.load(temporary.read_bytes())
    if list(check.container) != [template + '.prefab']:
        raise ValueError('Generated prefab index verification failed')
    temporary.replace(destination)
    return dict(file=destination.name, source=level, name=templates[template], objects=len(table), version=2, bytes=destination.stat().st_size, sha256=hashlib.sha256(destination.read_bytes()).hexdigest())


levels = sorted((p for p in data.glob('level*') if p.name[5:].isdigit()), key=lambda p: p.stat().st_size)
remaining = {args.template} if args.template else set(templates) - {k for k,v in index.items() if v.get("version")==2}
for level in levels:
    raw = level.read_bytes()
    hits = ({'claymore'} if b'MineDirectional' in raw else set()) & remaining if args.claymore else set(t.decode() for t in re.findall(rb'[a-f0-9]{24}', raw)) & remaining
    for template in sorted(hits):
        try:
            index[template] = build(level.name, template)
            index_path.write_text(json.dumps(index, indent=2), encoding='utf-8')
            remaining.remove(template)
            print('BUILT', template, level.name, index[template]['bytes'], flush=True)
        except Exception as error:
            print('FAILED', template, level.name, str(error), flush=True)
    if not remaining:
        break
print('Available:', len(index), 'missing:', sorted(remaining))
