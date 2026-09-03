// FirstPersonArmsMeshBuilder.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/FirstPersonArmsMeshBuilder.cs
//
// ========================================================================
// STAGE 1 ONLY - EXTRACT AN ARMS-ONLY MESH FROM geometry_001.
//
// Does not touch PlayerModel_FBX_VISUAL, the Player prefab, movement,
// animation, IK, multiplayer or the camera. It reads the character's mesh,
// writes a NEW mesh asset next to it, and leaves a preview object in the
// scene to look at. Nothing about the game is wired to this yet - that is
// Stage 2, after this is confirmed correct.
//
// ------------------------------------------------------------------------
// THE MESH HAS NO "ARMS SUBMESH" TO PULL OUT
//
// geometry_001 is one SkinnedMeshRenderer with seven material slots - suit,
// skin, boots, belt, helmet, visor - and those slots split the surface by
// MATERIAL, not by body part. There is no clean "arms" piece sitting there
// ready to lift out.
//
// What every vertex DOES carry is BONE WEIGHTS - which bones move it, and
// how much. A knuckle is weighted almost entirely to Hand/ForeArm/UpperArm; a
// sternum vertex is weighted to Spine/Chest. So the cut is made there: keep a
// vertex if its DOMINANT bone is somewhere in the arm chain, keep a triangle
// only if all three of its vertices survived, and everything else - hips,
// spine, chest, head, legs - simply is not copied into the new mesh at all.
// Not hidden. Not scaled to nothing. Not present.
//
// ------------------------------------------------------------------------
// WHY THIS PRESERVES SKINNING, BINDPOSES AND MATERIAL SLOTS FOR FREE
//
// Bone weights reference bones by INDEX INTO AN ARRAY (mesh.bindposes /
// renderer.bones), never by name or direct reference. Carrying that array
// over UNCHANGED, and only ever touching the per-vertex weight entries (which
// keep the same bone indices, just fewer vertices), means the new mesh stays
// skinnable to any renderer whose bones array is laid out the same way -
// which is exactly true of a clone made by Instantiate(), because Unity
// remaps every internal cross-reference (SkinnedMeshRenderer.bones included)
// to the clone's own Transforms automatically. That is what makes Stage 2
// possible without this file knowing anything about Stage 2.
//
// Submesh order is preserved too, on purpose, rather than dropped or
// renumbered - submesh 0 is still "whatever material 0 was" even if it ends
// up with zero triangles. A renderer with more material slots than the mesh
// has submeshes is not an error in Unity; the extra slots are simply unused.
// That means the ORIGINAL materials array can be handed to a renderer using
// this trimmed mesh with no remapping at all.
// ========================================================================

using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public static class FirstPersonArmsMeshBuilder
{
    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
    const string OutputFolder = "Assets/_Project/Models/Generated";
    const string OutputPath = OutputFolder + "/PlayerArmsViewmodel.asset";

    // Every humanoid bone that is arm, hand, or finger. Built explicitly
    // rather than by name-matching, so there is no ambiguity about what
    // counts - and a bone this rig does not have (many rigs skip fingers)
    // is simply skipped, logged, and does not stop the rest from working.
    static readonly HumanBodyBones[] ArmChain =
    {
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,

        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
    };

    [MenuItem("SAFE DEPOSIT/Player/Build First-Person Arms Mesh")]
    public static void Build()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("[Arms] ---- extraction report ----");

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError("[Arms] " + PlayerPrefabPath + " not found.");
            return;
        }

        // ---- READ/WRITE, THE ONE BLOCKER FOUND DURING INSPECTION ----
        //
        // Mesh.GetBonesPerVertex / GetAllBoneWeights / .vertices all refuse to
        // return anything for a mesh imported with Read/Write off, in the
        // Editor or in a build - it is not a permissions nicety, the CPU-side
        // copy of the data simply is not kept in memory otherwise. Fixed here
        // rather than asked of the user: it is one ModelImporter field and a
        // reimport, and there is no reason to make that a manual step.
        if (!EnsureReadWrite(out string fbxPath))
        {
            Debug.LogError("[Arms] Could not confirm Read/Write on the source FBX. Aborting.");
            return;
        }

        var temp = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        temp.name = "~ArmsExtract_temp";
        bool reachedPreview = false;

        try
        {
            var visual = temp.transform.Find("PlayerModel_FBX_VISUAL");
            if (visual == null)
            {
                Debug.LogError("[Arms] No PlayerModel_FBX_VISUAL under the Player prefab.");
                return;
            }

            var anim = visual.GetComponent<Animator>();
            var smr = visual.GetComponentInChildren<SkinnedMeshRenderer>();

            if (anim == null || !anim.isHuman)
            {
                Debug.LogError("[Arms] PlayerModel_FBX_VISUAL has no Humanoid Animator - " +
                               "cannot resolve bone names without one.");
                return;
            }

            if (smr == null)
            {
                Debug.LogError("[Arms] No SkinnedMeshRenderer found under PlayerModel_FBX_VISUAL.");
                return;
            }

            var mesh = smr.sharedMesh;
            if (mesh == null) { Debug.LogError("[Arms] SkinnedMeshRenderer has no mesh."); return; }

            if (!mesh.isReadable)
            {
                Debug.LogError("[Arms] Mesh is still not readable after the import fix - open " +
                               fbxPath + " in the Inspector, Model tab, and confirm Read/Write " +
                               "Enabled is ticked, then run this again.");
                return;
            }

            log.AppendLine($"source mesh: {mesh.name}  ({mesh.vertexCount} verts, " +
                           $"{mesh.subMeshCount} submeshes, {smr.bones.Length} bones)");

            // ---- WHICH BONE-ARRAY INDICES COUNT AS ARM ----
            var bones = smr.bones;
            var keepBoneIndex = new HashSet<int>();
            var foundNames = new List<string>();
            var missingNames = new List<string>();

            foreach (var hb in ArmChain)
            {
                var t = anim.GetBoneTransform(hb);
                if (t == null) { missingNames.Add(hb.ToString()); continue; }

                int idx = System.Array.IndexOf(bones, t);
                if (idx < 0) { missingNames.Add(hb.ToString() + " (not in renderer.bones)"); continue; }

                keepBoneIndex.Add(idx);
                foundNames.Add(hb + " -> " + t.name);
            }

            if (keepBoneIndex.Count == 0)
            {
                Debug.LogError("[Arms] Matched zero arm bones - this rig's bone names do not line " +
                               "up with the Humanoid map the way expected. Aborting rather than " +
                               "writing an empty mesh.");
                return;
            }

            log.AppendLine($"arm bones matched: {keepBoneIndex.Count} of {ArmChain.Length}");
            foreach (var n in foundNames) log.AppendLine("  found   " + n);
            foreach (var n in missingNames) log.AppendLine("  MISSING " + n +
                                                           "  (fine if this rig has no fingers)");

            // ---- CLASSIFY EVERY VERTEX BY ITS DOMINANT BONE ----
            int vertCount = mesh.vertexCount;
            var bonesPerVertex = mesh.GetBonesPerVertex();
            var allWeights = mesh.GetAllBoneWeights();

            var keepVertex = new bool[vertCount];
            int offset = 0;

            for (int v = 0; v < vertCount; v++)
            {
                int count = bonesPerVertex[v];
                int dominant = -1;
                float best = -1f;

                for (int k = 0; k < count; k++)
                {
                    var bw = allWeights[offset + k];
                    if (bw.weight > best) { best = bw.weight; dominant = bw.boneIndex; }
                }

                offset += count;
                keepVertex[v] = dominant >= 0 && keepBoneIndex.Contains(dominant);
            }

            int keptCount = 0;
            for (int i = 0; i < vertCount; i++) if (keepVertex[i]) keptCount++;

            log.AppendLine($"vertices kept: {keptCount} of {vertCount} " +
                           $"({(100f * keptCount / vertCount):0.0}%)");

            if (keptCount == 0)
            {
                Debug.LogError("[Arms] Zero vertices survived the filter. Aborting.\n" + log);
                return;
            }

            // ---- REMAP KEPT VERTICES, COPY THEIR ATTRIBUTES ----
            var remap = new int[vertCount];
            for (int i = 0; i < vertCount; i++) remap[i] = -1;

            var srcVerts = mesh.vertices;
            var srcNormals = mesh.normals;
            var srcUv = mesh.uv;
            var srcTangents = mesh.tangents;

            bool hasNormals = srcNormals != null && srcNormals.Length == vertCount;
            bool hasUv = srcUv != null && srcUv.Length == vertCount;
            bool hasTangents = srcTangents != null && srcTangents.Length == vertCount;

            var newVerts = new List<Vector3>(keptCount);
            var newNormals = hasNormals ? new List<Vector3>(keptCount) : null;
            var newUv = hasUv ? new List<Vector2>(keptCount) : null;
            var newTangents = hasTangents ? new List<Vector4>(keptCount) : null;
            var newBonesPerVertex = new List<byte>(keptCount);
            var newWeights = new List<BoneWeight1>();

            offset = 0;
            for (int v = 0; v < vertCount; v++)
            {
                int count = bonesPerVertex[v];

                if (keepVertex[v])
                {
                    remap[v] = newVerts.Count;
                    newVerts.Add(srcVerts[v]);
                    if (hasNormals) newNormals.Add(srcNormals[v]);
                    if (hasUv) newUv.Add(srcUv[v]);
                    if (hasTangents) newTangents.Add(srcTangents[v]);

                    newBonesPerVertex.Add((byte)count);
                    for (int k = 0; k < count; k++)
                        newWeights.Add(allWeights[offset + k]);
                }

                offset += count;
            }

            bonesPerVertex.Dispose();
            allWeights.Dispose();

            // ---- TRIANGLES, PER SUBMESH, SUBMESH ORDER PRESERVED ----
            var newMesh = new Mesh { name = "PlayerArmsViewmodel" };
            newMesh.indexFormat = mesh.indexFormat;
            newMesh.SetVertices(newVerts);
            if (hasNormals) newMesh.SetNormals(newNormals);
            if (hasUv) newMesh.SetUVs(0, newUv);
            if (hasTangents) newMesh.SetTangents(newTangents);

            newMesh.subMeshCount = mesh.subMeshCount;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                var newTris = new List<int>(tris.Length);

                int srcTriCount = tris.Length / 3;
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    if (keepVertex[a] && keepVertex[b] && keepVertex[c])
                    {
                        newTris.Add(remap[a]);
                        newTris.Add(remap[b]);
                        newTris.Add(remap[c]);
                    }
                }

                newMesh.SetTriangles(newTris, s);

                string matName = s < smr.sharedMaterials.Length && smr.sharedMaterials[s] != null
                    ? smr.sharedMaterials[s].name : "(slot " + s + ")";
                log.AppendLine($"  submesh {s} [{matName}]: " +
                               $"{newTris.Count / 3} of {srcTriCount} triangles kept");
            }

            using (var npv = new NativeArray<byte>(newBonesPerVertex.ToArray(), Allocator.Temp))
            using (var nw = new NativeArray<BoneWeight1>(newWeights.ToArray(), Allocator.Temp))
            {
                newMesh.SetBoneWeights(npv, nw);
            }

            // Bindposes copied WHOLE and UNCHANGED - see the header comment for
            // why this is what keeps the mesh skinnable to any bones array laid
            // out the same way, including a Stage 2 clone this file never sees.
            newMesh.bindposes = mesh.bindposes;
            newMesh.RecalculateBounds();

            // ---- SAVE THE ASSET ----
            EnsureFolder(OutputFolder);
            AssetDatabase.DeleteAsset(OutputPath);
            AssetDatabase.CreateAsset(newMesh, OutputPath);
            AssetDatabase.SaveAssets();

            log.AppendLine($"saved to {OutputPath}");
            log.AppendLine($"final mesh: {newMesh.vertexCount} verts, " +
                           $"{TotalTriangles(newMesh)} triangles");

            Debug.Log(log.ToString());

            LeavePreview(temp, visual, smr, newMesh);
            reachedPreview = true;
        }
        finally
        {
            // Every early return above (bad rig, zero vertices, missing
            // mesh) leaves this false, and without this the temp clone would
            // sit there under its "~ArmsExtract_temp" name forever - a full
            // player clone silently cluttering the scene rather than an
            // obvious failure. Cleaned up here so a failed run leaves
            // nothing behind; a successful one leaves exactly the preview,
            // which LeavePreview already renamed and is what should remain.
            if (!reachedPreview && temp != null) Object.DestroyImmediate(temp);
        }
    }

    static int TotalTriangles(Mesh m)
    {
        int total = 0;
        for (int s = 0; s < m.subMeshCount; s++) total += (int)m.GetIndexCount(s) / 3;
        return total;
    }

    /// <summary>
    /// Turns off Read/Write on the CHARACTER'S mesh, not the whole model
    /// necessarily - it is the model importer's own single flag, so this
    /// touches exactly the one setting that was blocking everything, on
    /// whichever FBX PlayerModel_FBX_VISUAL's mesh actually came from.
    /// </summary>
    static bool EnsureReadWrite(out string fbxPath)
    {
        fbxPath = null;

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var visual = playerPrefab != null ? playerPrefab.transform.Find("PlayerModel_FBX_VISUAL") : null;
        var smr = visual != null ? visual.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        var mesh = smr != null ? smr.sharedMesh : null;

        if (mesh == null)
        {
            Debug.LogError("[Arms] Could not find the character mesh to check Read/Write on.");
            return false;
        }

        if (mesh.isReadable) return true;   // already fine, nothing to do

        fbxPath = AssetDatabase.GetAssetPath(mesh);
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;

        if (importer == null)
        {
            Debug.LogError("[Arms] " + fbxPath + " has no ModelImporter - cannot flip Read/Write " +
                           "automatically. Open it in the Inspector, Model tab, tick Read/Write " +
                           "Enabled, Apply, then run this again.");
            return false;
        }

        Debug.Log("[Arms] Read/Write was off on " + fbxPath + " - enabling it and reimporting.");
        importer.isReadable = true;
        importer.SaveAndReimport();

        return true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts = path.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    /// <summary>
    /// Leaves a real, selectable object in the scene wearing the trimmed
    /// mesh with the ORIGINAL materials still attached, so it can be looked
    /// at directly rather than trusted from a vertex count in the console.
    ///
    /// Built from the SAME temp instance the extraction just read, reusing
    /// its skeleton and its one SkinnedMeshRenderer rather than spawning
    /// anything new - everything else on that instance (every other script
    /// the Player prefab carries) is stripped, because this is a mesh to
    /// look at, not a working character.
    /// </summary>
    static void LeavePreview(GameObject temp, Transform visual, SkinnedMeshRenderer smr, Mesh trimmed)
    {
        temp.name = "PREVIEW - Arms Mesh (delete after inspecting)";
        temp.transform.position = new Vector3(0f, 3f, 0f);   // clear of the elevator geometry

        smr.sharedMesh = trimmed;

        // Every other component the Player prefab carries assumes it is a
        // real, playing character - PlayerMotor wants a Rigidbody driving it
        // every physics step, PlayerHealth polls Crew state, and so on. None
        // of that belongs on a static mesh sitting in the air for inspection.
        foreach (var c in temp.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null) continue;               // a component whose script failed to load
            Object.DestroyImmediate(c);
        }

        var rb = temp.GetComponentInChildren<Rigidbody>(true);
        if (rb != null) Object.DestroyImmediate(rb);

        foreach (var col in temp.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        Selection.activeGameObject = temp;

        var view = SceneView.lastActiveSceneView;
        if (view != null) view.FrameSelected();

        Debug.Log("[Arms] Preview left in the scene at (0, 3, 0), selected. " +
                  "Look at it in the Scene view - it should read as arms and hands only, " +
                  "with real materials, and nothing that reads as torso, head or legs. " +
                  "Delete the GameObject once you have confirmed it.");
    }
}
