#!/usr/bin/env python3
"""
glb_reduce.py  -  SAFE DEPOSIT

Turn a 9-million-triangle AI mesh into a clean game-ready one.

THE THREE THINGS THAT WENT WRONG BEFORE
---------------------------------------
1. TRIANGLE SOUP. The GLB stores 26.9M vertices for 9M triangles - three
   per triangle, none shared. Nothing is actually connected. Decimating that
   deletes faces and leaves holes, which is what Blender was doing.

2. MATERIAL SEAMS. 13 material slots means 13 borders the algorithm has to
   preserve. Decimating per-material makes every border a wall and the mesh
   pulls apart along all of them.

3. MEMORY. Quadric decimation on 9M faces needs more RAM than we have, so
   the process just got killed.

THE FIX, IN ORDER
-----------------
weld  -> collapse triangle soup into a real connected surface, ACROSS
         material boundaries so there are no walls left
cluster -> cheap grid-snapping reduction to get under a size quadric
         decimation can actually handle. O(n) memory, no adjacency graph.
quadric -> the good decimation, on a mesh small enough to fit
restore -> look up each surviving triangle's material from the original
         surface, so the slots survive even though the seams did not
"""

import argparse, gc, json, struct
import numpy as np

COMP = {5120:'i1',5121:'u1',5122:'i2',5123:'u2',5125:'u4',5126:'f4'}
NCOMP = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}
BITS = 21                      # 2M distinct values per axis, 63 bits total


def read_glb(path):
    with open(path,'rb') as f:
        struct.unpack('<III', f.read(12))
        clen, _ = struct.unpack('<II', f.read(8))
        js = json.loads(f.read(clen))
        struct.unpack('<II', f.read(8))
        base = f.tell()
    return js, np.memmap(path, dtype='u1', mode='r'), base


def acc(js, mm, base, i):
    a = js['accessors'][i]; bv = js['bufferViews'][a['bufferView']]
    dt = np.dtype(COMP[a['componentType']]); n = NCOMP[a['type']]
    off = base + bv.get('byteOffset',0) + a.get('byteOffset',0)
    return np.frombuffer(mm[off:off+a['count']*n*dt.itemsize], dtype=dt).reshape(a['count'],n)


def cluster(V, F, cell):
    """
    Weld every vertex that lands in the same grid cell.

    Uses a single packed int64 key per vertex so the unique() is 1-D. The
    obvious np.unique(V, axis=0) sorts a 4.5-million-row 2-D array and needs
    gigabytes; this needs a few hundred megabytes and is far quicker.
    """
    q = np.floor(V / cell).astype(np.int64)
    q -= q.min(axis=0)
    key = q[:,0] | (q[:,1] << BITS) | (q[:,2] << (2*BITS))
    del q
    uniq, first, inv = np.unique(key, return_index=True, return_inverse=True)
    del key, uniq
    Vn = V[first]
    Fn = inv[F]
    ok = (Fn[:,0]!=Fn[:,1]) & (Fn[:,1]!=Fn[:,2]) & (Fn[:,0]!=Fn[:,2])
    return Vn, Fn[ok], ok, first


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input"); ap.add_argument("output")
    ap.add_argument("--target", type=int, default=30000)
    ap.add_argument("--height", type=float, default=0.0)
    ap.add_argument("--floor", action="store_true")
    ap.add_argument("--smooth", type=int, default=0,
                    help="Taubin smoothing passes AFTER decimation. This is what "
                         "removes the lumpy noise inherent to AI-generated meshes. "
                         "8-25 is the useful range.")
    ap.add_argument("--quadric-max", type=int, default=700_000,
                    help="cluster down to this before the quality pass")
    a = ap.parse_args()

    import fast_simplification as fs, trimesh
    from scipy.spatial import cKDTree

    js, mm, base = read_glb(a.input)
    Vs, Fs, Ms, off = [], [], [], 0
    for m in js['meshes']:
        for p in m['primitives']:
            v = acc(js, mm, base, p['attributes']['POSITION']).astype(np.float32)
            f = acc(js, mm, base, p['indices']).reshape(-1,3).astype(np.int64) + off
            Vs.append(v); Fs.append(f)
            Ms.append(np.full(len(f), p.get('material',0), np.int16)); off += len(v)
    V = np.concatenate(Vs); F = np.concatenate(Fs); M = np.concatenate(Ms)
    del Vs, Fs, Ms; mm._mmap.close(); del mm; gc.collect()
    print(f"loaded   {len(V):,} verts  {len(F):,} tris  {int(M.max())+1} materials")

    size = float((V.max(axis=0) - V.min(axis=0)).max())

    # ---- 1. weld the triangle soup -----------------------------------
    V, F, ok, _ = cluster(V, F, size * 1e-5)
    M = M[ok]
    print(f"welded   {len(V):,} verts  {len(F):,} tris")
    gc.collect()

    # reference for putting materials back, taken now while detail is high
    ref_pts = V.copy()
    ref_mat = np.zeros(len(V), np.int16)
    ref_mat[F.reshape(-1)] = np.repeat(M, 3)

    # ---- 2. cluster down to something quadric can chew ---------------
    cell = size / 400.0
    while len(F) > a.quadric_max:
        V, F, ok, _ = cluster(V, F, cell)
        M = M[ok]
        print(f"cluster  cell {cell*1000:6.2f}mm -> {len(V):,} verts  {len(F):,} tris")
        cell *= 1.6
        gc.collect()

    del M; gc.collect()

    # ---- 3. quality pass ---------------------------------------------
    if len(F) > a.target:
        ratio = float(np.clip(1.0 - a.target/len(F), 0.0, 0.9999))
        V, F = fs.simplify(V.astype(np.float32), F.astype(np.int32), target_reduction=ratio)
        print(f"quadric  -> {len(F):,} tris")
    gc.collect()

    # ---- 3b. relax the surface ---------------------------------------
    # AI image-to-3D output is covered in sub-millimetre noise. Invisible at
    # 9M triangles, but once reduced it IS the surface, and smooth shading
    # turns every bump into a visible dent.
    #
    # Taubin smoothing alternates a shrinking pass with an expanding one, so
    # it removes high-frequency noise WITHOUT deflating the model the way
    # plain Laplacian smoothing does. That distinction matters on a character:
    # naive smoothing would melt the helmet.
    if a.smooth > 0:
        tmp = trimesh.Trimesh(vertices=V, faces=F, process=False)
        trimesh.smoothing.filter_taubin(tmp, lamb=0.5, nu=0.53, iterations=a.smooth)
        V = np.asarray(tmp.vertices, dtype=np.float32)
        print(f"smoothed x{a.smooth}")
        del tmp; gc.collect()

    # ---- 4. materials back -------------------------------------------
    _, nn = cKDTree(ref_pts).query(V[F].mean(axis=1), k=1)
    Fmat = ref_mat[nn]
    print(f"materials {len(np.unique(Fmat))} slots in use")

    # ---- transform ----------------------------------------------------
    if a.height > 0:
        V = V * (a.height / float((V.max(axis=0)-V.min(axis=0)).max()))
    if a.floor:
        lo, hi = V.min(axis=0), V.max(axis=0)
        V = V - np.array([(lo[0]+hi[0])/2, lo[1], (lo[2]+hi[2])/2], np.float32)

    names = [m.get('name', f'mat{i}') for i,m in enumerate(js.get('materials',[]))]
    parts = {}
    for mid in np.unique(Fmat):
        sel = F[Fmat == mid]
        used, remap = np.unique(sel, return_inverse=True)
        nm = names[mid] if mid < len(names) else f"mat{mid}"
        parts[nm] = trimesh.Trimesh(vertices=V[used], faces=remap.reshape(-1,3), process=False)
        print(f"   {nm:20s} {len(sel):7,d} tris")

    trimesh.Scene(parts).export(a.output)
    ext = np.round(V.max(axis=0)-V.min(axis=0), 3)
    print(f"\nwrote {a.output}\n  {len(F):,} tris   size {ext.tolist()}")


if __name__ == "__main__":
    main()
