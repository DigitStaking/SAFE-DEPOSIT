// ViewmodelLayerSetup.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/ViewmodelLayerSetup.cs
//
// ========================================================================
// ONE-CLICK SETUP FOR THE VIEWMODEL LAYER.
//
// FirstPersonViewmodel needs a Unity LAYER to put the cloned arms on, and a
// layer is a PROJECT SETTING (ProjectSettings/TagManager.asset) - it cannot
// be created from play-mode code, only from the editor, and only once.
//
// This is that once. Run it from SAFE DEPOSIT > Player > Setup First-Person
// Viewmodel Layer before FirstPersonViewmodel is used for the first time.
// Safe to run again later - it does nothing if the layer already exists.
//
// WHY A LAYER AT ALL
//
// The viewmodel camera needs to render ONLY the cloned arms and nothing else
// - not the level, not your real body, not a teammate. A culling mask is how
// a camera says "only these layers", and a culling mask needs a layer to
// name.
// ========================================================================

using UnityEditor;
using UnityEngine;

public static class ViewmodelLayerSetup
{
    // MUST MATCH FirstPersonViewmodel's own constants exactly - see the
    // comment there for why these cannot be shared constants.
    public const string LayerName = "Viewmodel";

    // The local player's OWN body goes here so the main camera can simply not
    // draw it. Its own layer rather than reusing Viewmodel, because the two
    // are opposites: the viewmodel camera draws ONLY Viewmodel, and the main
    // camera draws everything EXCEPT LocalBody.
    public const string BodyLayerName = "LocalBody";

    [MenuItem("SAFE DEPOSIT/Player/Setup First-Person Viewmodel Layer")]
    static void Setup()
    {
        Claim(LayerName);
        Claim(BodyLayerName);
    }

    static void Claim(string LayerName)
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0)
        {
            Debug.LogError("[Viewmodel] Could not open ProjectSettings/TagManager.asset.");
            return;
        }

        var tagManager = new SerializedObject(asset[0]);
        var layers = tagManager.FindProperty("layers");

        if (layers == null)
        {
            Debug.LogError("[Viewmodel] TagManager.asset has no 'layers' property - " +
                           "Unity may have changed its format.");
            return;
        }

        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == LayerName)
            {
                Debug.Log($"[Viewmodel] Layer '{LayerName}' already exists at slot {i}. " +
                          "Nothing to do.");
                return;
            }
        }

        // Slots 0-7 are Unity's own (Default, TransparentFX, Ignore Raycast,
        // Water, UI and three reserved) and cannot be touched. User layers
        // start at 8.
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[Viewmodel] Created layer '{LayerName}' at slot {i}.");
                return;
            }
        }

        Debug.LogError("[Viewmodel] No free layer slots (8-31 are all in use). " +
                       "Free one manually in Project Settings > Tags and Layers, " +
                       $"then run this again to claim it for '{LayerName}'.");
    }
}
