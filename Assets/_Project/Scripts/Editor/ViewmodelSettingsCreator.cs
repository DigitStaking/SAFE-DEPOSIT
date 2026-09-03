// ViewmodelSettingsCreator.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/ViewmodelSettingsCreator.cs
//
// ========================================================================
// ONE CLICK TO MAKE THE VIEWMODEL SETTINGS ASSET, IN THE RIGHT FOLDER.
//
// The runtime loads it by name out of a Resources folder, so it has to be in
// one - and "put it in exactly this folder or it silently does nothing" is a
// bad thing to ask of anybody. This puts it there and then selects it, so the
// next thing on screen is the inspector you are meant to be editing.
// ========================================================================

using UnityEditor;
using UnityEngine;

public static class ViewmodelSettingsCreator
{
    const string Folder = "Assets/_Project/Resources";
    const string Path = Folder + "/" + FirstPersonViewmodelSettings.ResourceName + ".asset";

    [MenuItem("SAFE DEPOSIT/Player/Create Viewmodel Settings Asset")]
    public static void Create()
    {
        var existing = AssetDatabase.LoadAssetAtPath<FirstPersonViewmodelSettings>(Path);

        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("[Viewmodel] Settings already exist at " + Path + " - selected it. " +
                      "Edit the values here; they persist and apply live while playing.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
        {
            var parts = Folder.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        var asset = ScriptableObject.CreateInstance<FirstPersonViewmodelSettings>();
        AssetDatabase.CreateAsset(asset, Path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        Debug.Log("[Viewmodel] Created " + Path + " and selected it. THIS is the object to " +
                  "tune - it exists outside Play mode and Unity saves it, unlike the runtime " +
                  "~FirstPersonViewmodel object whose values were thrown away on Stop.");
    }
}
