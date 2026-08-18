#!/usr/bin/env python3
"""
glb_decimate.py  -  SAFE DEPOSIT

Repair + decimate a huge AI-generated GLB, one primitive at a time.

    python3 glb_decimate.py in.glb out.glb --target 12000 --height 1.65

WHY NOT JUST LOAD IT
--------------------
A 9-million-triangle GLB is ~1GB of buffer. Loading the whole scene and
then concatenating it duplicates that several times over and the process
gets killed. So this memory-maps the file and touches one primitive at a
time, decimating each before moving on. Peak memory stays small no matter
how big the input is.

WHY IT DOES NOT TEAR HOLES
--------------------------
Blender's Decimate merges edges. AI meshes have duplicate vertices sitting
on top of each other, so faces that look joined are not connected - merging
an edge there deletes a face and leaves a hole. Voxel Remesh fails for a
related reason: it needs a watertight volume to know inside from outside,
and open shells come out as fragments.

This welds first (numpy unique on quantised positions), which actually
connects the surface, and only then runs quadric decimation. Same reason
the order matters in a kitchen: prep, then cook.
"""

import argparse, json, struct, sys
import numpy as np

COMP = {5120:'i1', 5121:'u1', 5122:'i2', 5123:'u2', 5125:'u4', 5126:'f4'}
NCOMP = {'SCALAR':1, 'VEC2':2, 'VEC3':3, 'VEC4':4}


def read_glb(path):
    with open(path, 'rb') as f:
        magic, ver, total = struct.unpack('<III', f.read(12))
        if magic != 0x46546C67:
            raise SystemExit("not a GLB")
        clen, ctype = struct.unpack('<II', f.read(8))
        js = json.loads(f.read(clen))
        blen, btype = struct.unpack('<II', f.read(8))
        bin_offset = f.tell()
    # memory-map instead of reading - we only ever touch slices we need
    mm = np.memmap(path, dtype='u1', mode='r')
    return js, mm, bin_offset


def accessor(js, mm, base, index):
    acc = js['accessors'][index]
    bv = js['bufferViews'][acc['bufferView']]
    dtype = np.dtype(COMP[acc['componentType']])
    n = NCOMP[acc['type']]
    start = base + bv.get('byteOffset', 0) + acc.get('byteOffset', 0)
    count = acc['count']
    raw = mm[start:start + count * n * dtype.itemsize]
    return np.frombuffer(raw, dtype=dtype).reshape(count, n)


def weld(verts, faces, precision=6):
    """Merge coincident vertices. This is the step that fixes the holes."""
    key = np.round(verts, precision)

    uniq, inverse = np.unique(key, axis=0, return_inverse=True)
    new_faces = inverse[faces]
    # drop faces that collapsed to a line or point
    ok = (new_faces[:, 0] != new_faces[:, 1]) & \
         (new_faces[:, 1] != new_faces[:, 2]) & \
         (new_faces[:, 0] != new_faces[:, 2])
    return uniq.astype(np.float32), new_faces[ok].astype(np.int32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input"); ap.add_argument("output")
    ap.add_argument("--target", type=int, default=12000)
    ap.add_argument("--height", type=float, default=0.0)
    ap.add_argument("--floor", action="store_true")
    args = ap.parse_args()

    import fast_simplification as fs
    import trimesh

    js, mm, base = read_glb(args.input)
    prims = [(mi, pi, p) for mi, m in enumerate(js['meshes'])
                          for pi, p in enumerate(m['primitives'])]

    sizes = []
    for _, _, p in prims:
        sizes.append(js['accessors'][p['indices']]['count'] // 3)
    total = sum(sizes)
    print(f"{len(prims)} primitives, {total:,} triangles total\n")

    mats = js.get('materials', [])
    pieces = {}

    for (mi, pi, p), tris in zip(prims, sizes):
        v = accessor(js, mm, base, p['attributes']['POSITION']).astype(np.float32)
        f = accessor(js, mm, base, p['indices']).reshape(-1, 3).astype(np.int64)

        before = len(f)
        v, f = weld(v, f)

        # Share the budget in proportion to size, but never crush a small part.
        budget = max(60, int(round(args.target * tris / total)))

        if len(f) > budget:
            ratio = float(np.clip(1.0 - budget / len(f), 0.0, 0.999))
            v, f = fs.simplify(v, f.astype(np.int32), target_reduction=ratio)

        name = mats[p['material']].get('name', f'mat{pi}') if 'material' in p else f'prim{pi}'
        name = f"{name}_{pi}"
        pieces[name] = trimesh.Trimesh(vertices=v, faces=f, process=False)
        print(f"  {name:22s} {before:9,d} -> {len(f):7,d}")

        del v, f

    scene = trimesh.Scene(pieces)
    combined = trimesh.util.concatenate(list(pieces.values()))
    print(f"\ncombined: {len(combined.faces):,} faces")

    # Uniform scale + pivot, applied to the whole scene so parts stay aligned.
    ext = combined.extents
    if args.height > 0:
        k = args.height / ext.max()
        for g in pieces.values(): g.apply_scale(k)
        combined.apply_scale(k)
        print(f"scaled x{k:.4f} -> {combined.extents.round(3).tolist()}")

    if args.floor:
        lo, hi = combined.bounds
        off = [-(lo[0]+hi[0])/2, -lo[1], -(lo[2]+hi[2])/2]
        for g in pieces.values(): g.apply_translation(off)
        combined.apply_translation(off)
        print("pivot -> base centre")

    trimesh.Scene(pieces).export(args.output)
    print(f"\nwrote {args.output}")


if __name__ == "__main__":
    main()
