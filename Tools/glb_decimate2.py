#!/usr/bin/env python3
"""
glb_decimate2.py  -  SAFE DEPOSIT

Decimate a huge AI mesh WITHOUT tearing at material seams.

WHAT WENT WRONG BEFORE, IN BOTH TOOLS
-------------------------------------
The model has 13 material slots. Every boundary between two materials is a
SEAM, and at a seam the vertices are physically duplicated in the mesh data
so each side can carry its own material. Neither side can merge across it.

Decimating per-material - which is what Blender does, and what my first
script did - means every one of those 13 borders is a hard wall the
algorithm must preserve. Push the ratio down and the geometry pulls apart
along all of them. Those are the holes.

THE FIX
-------
1. Merge every primitive into ONE mesh and weld across the seams. Now there
   are no walls at all and decimation can collapse anything.
2. Decimate the whole thing as a single surface.
3. Put the materials BACK afterwards: for each surviving triangle, look up
   which material the nearest original surface had, and use that.

Step 3 is what makes this safe to do. You get seam-free decimation and you
still keep your material slots - they are just re-derived rather than
preserved.
"""

import argparse, gc, json, struct
import numpy as np


COMP = {5120:'i1', 5121:'u1', 5122:'i2', 5123:'u2', 5125:'u4', 5126:'f4'}
NCOMP = {'SCALAR':1, 'VEC2':2, 'VEC3':3, 'VEC4':4}


def read_glb(path):
    with open(path, 'rb') as f:
        magic, ver, total = struct.unpack('<III', f.read(12))
        clen, ctype = struct.unpack('<II', f.read(8))
        js = json.loads(f.read(clen))
        struct.unpack('<II', f.read(8))
        base = f.tell()
    return js, np.memmap(path, dtype='u1', mode='r'), base


def acc(js, mm, base, i):
    a = js['accessors'][i]
    bv = js['bufferViews'][a['bufferView']]
    dt = np.dtype(COMP[a['componentType']])
    n = NCOMP[a['type']]
    off = base + bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    return np.frombuffer(mm[off:off + a['count'] * n * dt.itemsize],
                         dtype=dt).reshape(a['count'], n)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input"); ap.add_argument("output")
    ap.add_argument("--target", type=int, default=30000)
    ap.add_argument("--height", type=float, default=0.0)
    ap.add_argument("--floor", action="store_true")
    ap.add_argument("--weld", type=float, default=1e-4,
                    help="weld radius in model units. MUST be big enough to "
                         "close the material seams - too small and this does "
                         "nothing, which is exactly how the first attempt failed.")
    args = ap.parse_args()

    import fast_simplification as fs
    import trimesh
    from scipy.spatial import cKDTree

    js, mm, base = read_glb(args.input)

    all_v, all_f, all_m = [], [], []
    voff = 0
    for mesh in js['meshes']:
        for p in mesh['primitives']:
            v = acc(js, mm, base, p['attributes']['POSITION']).astype(np.float32)
            f = acc(js, mm, base, p['indices']).reshape(-1, 3).astype(np.int64) + voff
            all_v.append(v); all_f.append(f)
            all_m.append(np.full(len(f), p.get('material', 0), dtype=np.int16))
            voff += len(v)

    V = np.concatenate(all_v); F = np.concatenate(all_f); M = np.concatenate(all_m)
    del all_v, all_f, all_m
    print(f"loaded  {len(V):,} verts  {len(F):,} tris  {M.max()+1} materials")

    # ---- 1. weld ACROSS the seams -------------------------------------
    q = np.round(V / args.weld).astype(np.int64)
    uniq, first_idx, inverse = np.unique(q, axis=0, return_index=True, return_inverse=True)
    Vw = V[first_idx]
    Fw = inverse[F]
    keep = (Fw[:,0]!=Fw[:,1]) & (Fw[:,1]!=Fw[:,2]) & (Fw[:,0]!=Fw[:,2])
    Fw, Mw = Fw[keep], M[keep]
    print(f"welded  {len(V):,} -> {len(Vw):,} verts   ({len(V)-len(Vw):,} merged)")
    print(f"        {len(F):,} -> {len(Fw):,} tris")

    # material id per ORIGINAL vertex, for putting materials back later
    vert_mat = np.zeros(len(Vw), dtype=np.int16)
    vert_mat[Fw.reshape(-1)] = np.repeat(Mw, 3)

    # FREE EVERYTHING before decimating. fast_simplification builds large
    # adjacency structures; on a 9M face mesh it needs every byte it can get,
    # and holding the originals alongside it is what got the process killed.
    ref_pts = Vw
    ref_mat = vert_mat
    Fw = Fw.astype(np.int32)
    del V, F, M, q, uniq, inverse, first_idx, keep, Mw
    mm._mmap.close()
    del mm
    gc.collect()

    # ---- 2. decimate in STAGES ---------------------------------------
    # One 9M -> 30k jump needs more memory than we have. Two passes with a
    # free in between costs a couple of seconds and always fits.
    Vd, Fd = ref_pts.astype(np.float32), Fw
    for stage_target in (1_200_000, args.target):
        if len(Fd) <= stage_target:
            continue
        ratio = float(np.clip(1.0 - stage_target/len(Fd), 0.0, 0.9999))
        Vd, Fd = fs.simplify(Vd.astype(np.float32), Fd.astype(np.int32),
                             target_reduction=ratio)
        print(f"  stage -> {len(Fd):,} tris")
        gc.collect()
    print(f"decimated to {len(Fd):,} tris")

    # ---- 3. put the materials back -----------------------------------
    tree = cKDTree(ref_pts)
    centroids = Vd[Fd].mean(axis=1)
    _, nn = tree.query(centroids, k=1)
    Fd_mat = ref_mat[nn]
    print(f"reassigned materials: {len(np.unique(Fd_mat))} slots in use")

    # ---- transform ----------------------------------------------------
    mesh_all = trimesh.Trimesh(vertices=Vd, faces=Fd, process=False)
    if args.height > 0:
        k = args.height / mesh_all.extents.max()
        Vd = Vd * k
        print(f"scaled x{k:.4f}")
    if args.floor:
        lo = Vd.min(axis=0); hi = Vd.max(axis=0)
        Vd = Vd - np.array([(lo[0]+hi[0])/2, lo[1], (lo[2]+hi[2])/2], dtype=np.float32)
        print("pivot -> base centre")

    # ---- export, one geometry per material ---------------------------
    names = [m.get('name', f'mat{i}') for i, m in enumerate(js.get('materials', []))]
    parts = {}
    for mid in np.unique(Fd_mat):
        sel = Fd[Fd_mat == mid]
        used, remap = np.unique(sel, return_inverse=True)
        nm = names[mid] if mid < len(names) else f"mat{mid}"
        parts[f"{nm}"] = trimesh.Trimesh(vertices=Vd[used],
                                         faces=remap.reshape(-1,3),
                                         process=False)
        print(f"  {nm:20s} {len(sel):7,d} tris")

    trimesh.Scene(parts).export(args.output)
    ext = np.round(Vd.max(axis=0) - Vd.min(axis=0), 3)
    print(f"\nwrote {args.output}\n  {len(Fd):,} tris   size {ext.tolist()}")


if __name__ == "__main__":
    main()
