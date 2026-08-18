#!/usr/bin/env python3
"""
clean_mesh.py  -  SAFE DEPOSIT

Repair and decimate an AI-generated mesh.

    python3 clean_mesh.py input.glb output.glb --target 12000

WHY DECIMATE TEARS HOLES IN AI MESHES
-------------------------------------
Image-to-3D generators do not produce clean geometry. Typical output has:

  - duplicate vertices sitting on top of each other, so faces that look
    joined are not actually connected
  - degenerate faces with zero area
  - inconsistent winding, so normals point in random directions
  - non-manifold edges shared by three or more faces
  - sometimes several overlapping shells rather than one closed surface

Blender's Decimate Collapse merges edges. Given geometry that is not
actually connected, merging an edge deletes a face and leaves nothing
behind - which is the holes you are seeing. It is not that the ratio is
too aggressive; the mesh was already broken and decimation is what makes
that visible.

THE FIX IS ORDER OF OPERATIONS
------------------------------
Repair FIRST, decimate SECOND:

  1. weld duplicate vertices        - actually connect the surface
  2. drop degenerate faces          - remove zero-area junk
  3. fix winding and normals        - make it a coherent surface
  4. fill remaining holes           - close what is left
  5. THEN decimate                  - now it has something valid to collapse

Step 5 is the only one most people do, which is why most people get holes.
"""

import argparse
import sys

import numpy as np
import trimesh


def load_single_mesh(path):
    """Load a file and flatten it to one mesh, whatever it arrives as."""
    loaded = trimesh.load(path, force="mesh", process=False)

    if isinstance(loaded, trimesh.Scene):
        parts = [g for g in loaded.geometry.values()
                 if isinstance(g, trimesh.Trimesh)]
        if not parts:
            raise SystemExit("No mesh geometry found in that file.")
        loaded = trimesh.util.concatenate(parts)

    if not isinstance(loaded, trimesh.Trimesh):
        raise SystemExit(f"Could not read a mesh from {path}")

    return loaded


def repair(mesh, weld_distance):
    """Make the surface actually watertight before touching the face count."""
    before = len(mesh.faces)

    # Weld vertices that are within weld_distance of each other. This is the
    # big one - it turns a pile of disconnected triangles into a surface.
    mesh.merge_vertices(merge_tex=True, merge_norm=True)

    # Remove faces with no area and vertices no face refers to.
    mesh.update_faces(mesh.nondegenerate_faces())
    mesh.update_faces(mesh.unique_faces())
    mesh.remove_unreferenced_vertices()

    # Make winding consistent, then point normals outward. Without this,
    # decimation makes decisions based on garbage normals and folds the
    # surface in on itself.
    mesh.fix_normals()

    # Close whatever gaps are left. Only works on small holes, which is all
    # that should remain after welding.
    try:
        mesh.fill_holes()
    except Exception:
        pass

    print(f"  repaired: {before:,} -> {len(mesh.faces):,} faces, "
          f"watertight={mesh.is_watertight}")
    return mesh


def decimate(mesh, target_faces):
    """
    Quadric error decimation. Chooses which edges to collapse by how much
    each collapse changes the shape, so silhouettes and hard edges survive
    far better than a naive collapse.
    """
    if len(mesh.faces) <= target_faces:
        print(f"  already at or below {target_faces:,} faces, skipping")
        return mesh

    try:
        import fast_simplification as fs
    except ImportError:
        raise SystemExit("pip install fast-simplification --break-system-packages")

    ratio = 1.0 - (target_faces / len(mesh.faces))
    ratio = float(np.clip(ratio, 0.0, 0.999))

    verts, faces = fs.simplify(
        mesh.vertices.astype(np.float32),
        mesh.faces.astype(np.int32),
        target_reduction=ratio,
    )

    out = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    out.merge_vertices()
    out.fix_normals()

    print(f"  decimated to {len(out.faces):,} faces")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("output")
    ap.add_argument("--target", type=int, default=12000,
                    help="target triangle count (characters 8-15k, props 300-2000)")
    ap.add_argument("--weld", type=float, default=1e-5,
                    help="vertex weld distance in model units")
    ap.add_argument("--smooth", type=int, default=0,
                    help="Taubin smoothing passes. 2-4 removes decimation "
                         "faceting without shrinking the model. 0 = off.")
    ap.add_argument("--height", type=float, default=0.0,
                    help="rescale so the model is exactly this tall in metres "
                         "(1.8 for a human). 0 = leave scale alone.")
    ap.add_argument("--floor", action="store_true",
                    help="move the origin to the base centre, so the model "
                         "stands on y=0 instead of floating around its middle")
    args = ap.parse_args()

    print(f"loading {args.input}")
    mesh = load_single_mesh(args.input)
    print(f"  {len(mesh.vertices):,} verts, {len(mesh.faces):,} faces")

    mesh = repair(mesh, args.weld)
    mesh = decimate(mesh, args.target)

    if args.smooth > 0:
        trimesh.smoothing.filter_taubin(mesh, iterations=args.smooth)
        print(f"  smoothed x{args.smooth}")

    if args.height > 0:
        current = mesh.bounds[1][1] - mesh.bounds[0][1]
        if current > 1e-6:
            mesh.apply_scale(args.height / current)
            print(f"  rescaled to {args.height}m tall")

    if args.floor:
        # Pivot at the base centre. Unity expects this - a model pivoted at
        # its middle floats half underground the moment you place it.
        lo, hi = mesh.bounds
        mesh.apply_translation([-(lo[0] + hi[0]) / 2, -lo[1], -(lo[2] + hi[2]) / 2])
        print("  pivot moved to base centre")

    mesh.export(args.output)
    print(f"\nwrote {args.output}")
    print(f"  {len(mesh.vertices):,} verts, {len(mesh.faces):,} faces, "
          f"watertight={mesh.is_watertight}")


if __name__ == "__main__":
    main()
