// CarryableGripEditor.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/CarryableGripEditor.cs
//
// ========================================================================
// PLACE A GRIP BY DRAGGING IT, NOT BY TYPING NUMBERS.
//
// "each box has her own dimension and way to grab and each item too"
//
// The data lives on Carryable. This is the part that makes it possible to
// actually author: two draggable hands in the Scene view, on the object
// itself, at the scale you will see them in game.
//
// Typing local-space coordinates for a hand is miserable and produces bad
// grips, because you cannot tell whether -0.14 is on the box or inside it
// until you press play. Dragging is immediate, and the seed button means you
// are adjusting a sensible measured guess rather than pulling two hands out
// of the origin.
//
// HOW TO USE IT
//
//   1. Open the loot prefab (double-click it, or select it in the scene)
//   2. Set Grip Mode to Custom
//   3. Press "Seed From Bounds" - two hands appear on the object's sides
//   4. Drag them where they belong. W moves, E rotates, like anything else
//   5. Set the five finger curls per hand
//
// The blue hand is LEFT, the yellow is RIGHT. If they come out swapped for a
// particular model, swap the two X values - which way round they land depends
// on how that model was authored and there is nothing to detect it from.
// ========================================================================

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Carryable))]
public class CarryableGripEditor : Editor
{
    // Which handle the Scene view is currently editing. Both at once is
    // unusable - two overlapping gizmos and you grab the wrong one.
    enum Editing { None, Left, Right }

    static Editing editing = Editing.None;
    static bool rotating = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var item = (Carryable)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grip authoring", EditorStyles.boldLabel);

        if (item.gripMode == Carryable.GripMode.Auto)
        {
            EditorGUILayout.HelpBox(
                "Auto: the hands are measured from this object's bounds every " +
                "frame. Right for most crates, and a new prop needs no setup " +
                "at all.\n\n" +
                "Switch to Custom only when Auto is wrong for this particular " +
                "item - a handle, a recessed lip, a one-handed carry.",
                MessageType.Info);

            if (GUILayout.Button("Switch to Custom and seed from bounds"))
            {
                Undo.RecordObject(item, "Seed grips");
                item.gripMode = Carryable.GripMode.Custom;
                Seed(item);
                EditorUtility.SetDirty(item);
            }

            return;
        }

        EditorGUILayout.HelpBox(
            "Custom: the two points below are used exactly as placed.\n\n" +
            "Blue handle = LEFT hand, yellow = RIGHT. Drag them in the Scene " +
            "view. If they land on the wrong sides for this model, swap the " +
            "two X values.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Seed From Bounds"))
            {
                Undo.RecordObject(item, "Seed grips");
                Seed(item);
                EditorUtility.SetDirty(item);
            }

            if (GUILayout.Button("Back to Auto"))
            {
                Undo.RecordObject(item, "Grip mode");
                item.gripMode = Carryable.GripMode.Auto;
                EditorUtility.SetDirty(item);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Edit in Scene");

            Editing was = editing;
            editing = (Editing)GUILayout.Toolbar((int)editing,
                          new[] { "Off", "Left", "Right" });

            if (was != editing) SceneView.RepaintAll();
        }

        if (editing != Editing.None)
        {
            rotating = GUILayout.Toggle(rotating,
                           rotating ? "Rotating palm (click for move)"
                                    : "Moving hand (click for rotate)",
                           "Button");
        }

        if (GUILayout.Button("Mirror left grip onto right"))
        {
            Undo.RecordObject(item, "Mirror grip");
            Mirror(item.leftGrip, item.rightGrip);
            EditorUtility.SetDirty(item);
        }
    }

    void OnSceneGUI()
    {
        var item = (Carryable)target;
        if (item.gripMode != Carryable.GripMode.Custom) return;

        DrawHand(item, item.leftGrip, new Color(0.3f, 0.7f, 1f), "L");
        DrawHand(item, item.rightGrip, new Color(1f, 0.85f, 0.2f), "R");

        if (editing == Editing.None) return;

        var grip = editing == Editing.Left ? item.leftGrip : item.rightGrip;

        Vector3 world = item.transform.TransformPoint(grip.localPosition);
        Quaternion worldRot = item.transform.rotation * Quaternion.Euler(grip.localEuler);

        EditorGUI.BeginChangeCheck();

        if (rotating)
        {
            Quaternion moved = Handles.RotationHandle(worldRot, world);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(item, "Rotate grip");
                grip.localEuler =
                    (Quaternion.Inverse(item.transform.rotation) * moved).eulerAngles;
                EditorUtility.SetDirty(item);
            }
        }
        else
        {
            Vector3 moved = Handles.PositionHandle(world, worldRot);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(item, "Move grip");
                grip.localPosition = item.transform.InverseTransformPoint(moved);
                EditorUtility.SetDirty(item);
            }
        }
    }

    static void DrawHand(Carryable item, Carryable.HandGrip grip, Color c, string label)
    {
        if (!grip.used) return;

        Vector3 p = item.transform.TransformPoint(grip.localPosition);
        Quaternion r = item.transform.rotation * Quaternion.Euler(grip.localEuler);

        Handles.color = c;
        Handles.SphereHandleCap(0, p, Quaternion.identity, 0.04f, EventType.Repaint);

        // Which way the palm faces. A hand in the right place at the wrong
        // angle looks identical to a hand in the wrong place until you can
        // see this.
        Handles.DrawAAPolyLine(3f, p, p + r * Vector3.forward * 0.1f);

        Handles.Label(p + Vector3.up * 0.06f, label);
    }

    /// <summary>Seed with the item's own measurements if it has them, the
    /// known-good defaults otherwise. The four constants used to be typed out
    /// here AND in the Grip Library AND as field defaults on PlayerCarryArms -
    /// three places to change, so in practice one of them was always stale.
    /// Carryable.GripMeasure.Default is the only copy now.</summary>
    static void Seed(Carryable item) => item.SeedGripsFromBounds();

    /// <summary>Copy one hand to the other, reflected across the item's X
    /// axis. Most objects are symmetric and authoring the same grip twice by
    /// hand is how the two end up subtly different.</summary>
    static void Mirror(Carryable.HandGrip from, Carryable.HandGrip to)
    {
        to.localPosition = new Vector3(-from.localPosition.x,
                                        from.localPosition.y,
                                        from.localPosition.z);

        to.localEuler = new Vector3(from.localEuler.x,
                                    -from.localEuler.y,
                                    -from.localEuler.z);

        to.thumb = from.thumb;
        to.index = from.index;
        to.middle = from.middle;
        to.ring = from.ring;
        to.little = from.little;
        to.used = from.used;
    }
}
