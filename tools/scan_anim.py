# -*- coding: utf-8 -*-
"""Проверка GLB-анимаций: канал translation на кадре 0 должен быть ~нулевым
(дельта от запечённой позы). Если на кадре 0 узел улетает — объект 'прыгает'
при проигрывании. Также ищем большие скачки в середине трека."""
import json, struct, glob, os, math

def read_glb(path):
    with open(path, 'rb') as f:
        magic, ver, length = struct.unpack('<III', f.read(12))
        chunks = []
        while f.tell() < length:
            clen, ctype = struct.unpack('<II', f.read(8))
            chunks.append((ctype, f.read(clen)))
    g = json.loads(chunks[0][1].decode('utf-8'))
    binbuf = chunks[1][1] if len(chunks) > 1 else b''
    return g, binbuf

CTYPE = {5126: ('f', 4), 5123: ('H', 2), 5125: ('I', 4), 5121: ('B', 1)}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

def read_accessor(g, binbuf, idx):
    a = g['accessors'][idx]
    bv = g['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    n = NCOMP[a['type']]
    fmt, sz = CTYPE[a['componentType']]
    count = a['count']
    stride = bv.get('byteStride') or n*sz
    out = []
    for i in range(count):
        o = off + i*stride
        out.append(struct.unpack_from('<' + fmt*n, binbuf, o))
    return out

for glb in sorted(glob.glob('ReadyMaps/*/*.glb')):
    name = os.path.basename(os.path.dirname(glb))
    g, binbuf = read_glb(glb)
    anims = g.get('animations', [])
    if not anims:
        continue
    nodes = g.get('nodes', [])
    bad0 = []   # улет на кадре 0
    badd = []   # большой скачок в треке
    nch = 0
    for an in anims:
        for ch in an.get('channels', []):
            tgt = ch.get('target', {})
            if tgt.get('path') != 'translation':
                continue
            ni = tgt.get('node')
            nname = nodes[ni].get('name', f'node{ni}') if ni is not None else '?'
            samp = an['samplers'][ch['sampler']]
            vals = read_accessor(g, binbuf, samp['output'])
            nch += 1
            if not vals:
                continue
            t0 = vals[0]
            d0 = math.sqrt(sum(v*v for v in t0))
            if d0 > 1.0:
                bad0.append((nname, [round(v,1) for v in t0]))
            # макс скачок между соседними кадрами
            mx = 0.0
            for i in range(1, len(vals)):
                d = math.sqrt(sum((vals[i][k]-vals[i-1][k])**2 for k in range(3)))
                mx = max(mx, d)
            if mx > 500.0:
                badd.append((nname, round(mx,1)))
    if bad0 or badd:
        print(f'{name:22s} channels={nch} jump@frame0={len(bad0)} spikes={len(badd)}')
        for b in bad0[:5]: print(f'    f0 {b[0][:55]:55s} t0={b[1]}')
        for b in badd[:5]: print(f'    spike {b[0][:52]:52s} maxStep={b[1]}')
