# -*- coding: utf-8 -*-
"""Сканер «слетевших» объектов в готовых GLB: ищет mesh-ноды и SPT-маркеры,
которые висят в центре карты (около origin) или далеко за пределами основной
геометрии. Читает только JSON-чанк GLB + min/max аксессоров POSITION."""
import json, struct, sys, os, glob, re
OBJ_RE = re.compile(r'_obj([1-9]\d*)')  # obj0 = «без объекта», не считаем

def read_glb_json(path):
    with open(path, 'rb') as f:
        magic, ver, length = struct.unpack('<III', f.read(12))
        if magic != 0x46546C67:
            raise ValueError('not glb')
        clen, ctype = struct.unpack('<II', f.read(8))
        return json.loads(f.read(clen).decode('utf-8'))

def node_index(g):
    nodes = g.get('nodes', [])
    parents = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []):
            parents[c] = i
    # world translation = sum up the chain (rotation ignored: нам нужны только позиции)
    def world_trans(i, depth=0):
        t = [0.0, 0.0, 0.0]
        j = i
        while j is not None and depth < 64:
            n = nodes[j]
            lt = n.get('translation', [0, 0, 0])
            t = [t[0]+lt[0], t[1]+lt[1], t[2]+lt[2]]
            j = parents.get(j)
            depth += 1
        return t
    return nodes, world_trans

def scan(path):
    g = read_glb_json(path)
    nodes, world_trans = node_index(g)
    acc = g.get('accessors', [])
    meshes = g.get('meshes', [])
    # bbox всех mesh-нод по min/max аксессоров (вершины запечены в world)
    entries = []  # (name, cx, cy, cz, min, max, is_obj, is_spt)
    for i, n in enumerate(nodes):
        if 'mesh' not in n:
            continue
        name = n.get('name', f'node{i}')
        is_spt = not name.startswith('BSP')
        mins = [1e30]*3; maxs = [-1e30]*3
        ok = False
        for prim in meshes[n['mesh']].get('primitives', []):
            ai = prim.get('attributes', {}).get('POSITION')
            if ai is None:
                continue
            a = acc[ai]
            if 'min' not in a or 'max' not in a:
                continue
            ok = True
            for k in range(3):
                mins[k] = min(mins[k], a['min'][k]); maxs[k] = max(maxs[k], a['max'][k])
        if not ok:
            continue
        if is_spt:
            # куб-маркер: вершины локальные, позиция — в translation ноды
            wt = world_trans(i)
            sc = n.get('scale', [1, 1, 1])
            for k in range(3):
                half = (maxs[k]-mins[k])/2 * abs(sc[k])
                mins[k] = wt[k]-half; maxs[k] = wt[k]+half
        c = [(mins[k]+maxs[k])/2 for k in range(3)]
        entries.append(dict(name=name, c=c, mn=mins, mx=maxs,
                            is_obj=bool(OBJ_RE.search(name)),
                            is_spt=is_spt))
    static = [e for e in entries if not e['is_obj'] and not e['is_spt']]
    if not static:
        return None
    mn = [min(e['mn'][k] for e in static) for k in range(3)]
    mx = [max(e['mx'][k] for e in static) for k in range(3)]
    size = [mx[k]-mn[k] for k in range(3)]
    diag = (size[0]**2+size[1]**2+size[2]**2) ** 0.5 or 1.0
    origin_far = (sum((mn[k]+mx[k])**2 for k in range(3))**0.5) / 2 > diag  # центр карты далеко от origin
    flagged = []
    for e in entries:
        if not (e['is_obj'] or e['is_spt']):
            continue
        c = e['c']
        # дистанция центра mesh до bbox статики
        d = 0.0
        for k in range(3):
            if c[k] < mn[k]: d += (mn[k]-c[k])**2
            elif c[k] > mx[k]: d += (c[k]-mx[k])**2
        d = d**0.5
        near_origin = (c[0]**2+c[1]**2+c[2]**2)**0.5 < 0.05*diag and origin_far
        if d > 0.15*diag or near_origin:
            flagged.append((e['name'], [round(v,1) for v in c], round(d/diag,2), near_origin))
    # SPT-ноды без mesh тоже проверим по translation
    spt_nodes = []
    for i, n in enumerate(nodes):
        name = n.get('name','')
        if name.startswith('SPT'):
            t = world_trans(i)
            d = 0.0
            for k in range(3):
                if t[k] < mn[k]: d += (mn[k]-t[k])**2
                elif t[k] > mx[k]: d += (t[k]-mx[k])**2
            d = d**0.5
            if d > 0.15*diag:
                spt_nodes.append((name, [round(v,1) for v in t], round(d/diag,2)))
    return dict(static_meshes=len(static), obj_meshes=sum(1 for e in entries if e['is_obj']),
                spt_meshes=sum(1 for e in entries if e['is_spt']), diag=round(diag,1),
                flagged=flagged, spt_far=spt_nodes)

for glb in sorted(glob.glob('ReadyMaps/*/*.glb')):
    name = os.path.basename(os.path.dirname(glb))
    try:
        r = scan(glb)
    except Exception as ex:
        print(f'{name:22s} ERROR {ex}'); continue
    if r is None:
        continue
    nf = len(r['flagged']); ns = len(r['spt_far'])
    if nf or ns:
        print(f"{name:22s} static={r['static_meshes']:4d} objMesh={r['obj_meshes']:3d} diag={r['diag']:9.1f} FLAGGED={nf} sptFar={ns}")
        for f in r['flagged'][:6]:
            print(f"    obj {f[0][:60]:60s} c={f[1]} d={f[2]}{' NEAR-ORIGIN' if f[3] else ''}")
        for f in r['spt_far'][:4]:
            print(f"    spt {f[0][:60]:60s} t={f[1]} d={f[2]}")
