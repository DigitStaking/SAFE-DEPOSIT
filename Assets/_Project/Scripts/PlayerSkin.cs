// PlayerSkin.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/PlayerSkin.cs
// Goes on: the Player root (or the character model root).
//
// ========================================================================
// FOUR PLAYERS, ONE MODEL, ONE TEXTURE.
//
// The idea this rests on: in URP, BASE COLOUR IS MULTIPLIED OVER BASE MAP.
// White base colour shows the texture unchanged; orange base colour shows
// the same texture tinted orange. So four suit colours is four numbers, not
// four models and not four textures.
//
// For that to look right the texture wants to be fairly grey. A texture
// that is already strongly coloured fights the tint - tinting a blue suit
// yellow gives you a muddy green, not yellow.
//
// WHY MATERIALPROPERTYBLOCK AND NOT r.material.color
//
// Writing to renderer.material silently CLONES the material - a fresh copy
// per player, per part, every time. Four players would leave you with a
// dozen near-identical materials, none of which batch together, and none of
// which show up in your project so you can never find them again.
//
// A MaterialPropertyBlock overrides one value for one renderer without
// touching the shared material at all. It is the correct tool for exactly
// this job and almost nobody uses it.
// ========================================================================

using UnityEngine;

public class PlayerSkin : MonoBehaviour
{
    [Header("Who am I")]
    [Tooltip("0-based. Player 1 is 0. Networking will set this later; for now " +
             "change it in the Inspector to preview each crew colour.")]
    public int playerIndex = 0;

    [Header("Crew colours")]
    [Tooltip("One per player, straight from the concept sheet. Order matters - " +
             "player 1 is always orange, so a crew always looks the same and " +
             "people can say 'orange, behind you' and be understood.")]
    public Color[] palette =
    {
        new Color(0.85f, 0.35f, 0.08f),   // orange
        new Color(0.10f, 0.62f, 0.62f),   // teal
        new Color(0.88f, 0.72f, 0.10f),   // yellow
        new Color(0.86f, 0.87f, 0.88f),   // white
        new Color(0.55f, 0.25f, 0.65f),   // purple  (5th and 6th players)
        new Color(0.30f, 0.45f, 0.80f),   // blue
    };

    [Header("What gets tinted")]
    [Tooltip("Only materials whose name contains one of these words are " +
             "tinted. Leave the list EMPTY to tint everything - useful before " +
             "you have split the model into material slots, but it will also " +
             "colour the visor and the hands.\n\n" +
             "Name your Blender material slots to match: Suit, Body, Torso.")]
    public string[] tintMaterialNames = { "Suit", "Body", "Torso" };

    [Tooltip("How strong the tint is. 1 is the full palette colour. Lower " +
             "values let more of the texture's own colour through, which can " +
             "look better on a texture that is not fully grey.")]
    [Range(0f, 1f)] public float tintStrength = 1f;

    /// <summary>
    /// The colour this player actually is. Read by the HUD, name tags, and
    /// anything else that needs to match.
    /// </summary>
    public Color CurrentColor =>
        palette.Length == 0 ? Color.white : palette[Mathf.Abs(playerIndex) % palette.Length];

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    MaterialPropertyBlock block;

    void Start() => Apply();

    // Runs when you change a value in the Inspector, so you can flick through
    // playerIndex and see each crew colour without entering play mode.
    void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    [ContextMenu("Apply Skin")]
    public void Apply()
    {
        if (block == null) block = new MaterialPropertyBlock();

        Color target = Color.Lerp(Color.white, CurrentColor, tintStrength);

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;

            for (int slot = 0; slot < mats.Length; slot++)
            {
                if (!ShouldTint(mats[slot])) continue;

                // Read the existing overrides for THIS submesh, change one
                // value, write it back. Everything else on the material is
                // untouched, and no new material is created.
                r.GetPropertyBlock(block, slot);
                block.SetColor(BaseColorId, target);   // URP
                block.SetColor(ColorId, target);       // built-in, harmless if unused
                r.SetPropertyBlock(block, slot);
            }
        }
    }

    bool ShouldTint(Material m)
    {
        if (m == null) return false;

        // No filter set: tint everything. Gets you a coloured crew in one
        // click before the model is split into slots - just expect the visor
        // and the gloves to go with it.
        if (tintMaterialNames == null || tintMaterialNames.Length == 0) return true;

        string name = m.name;
        foreach (var key in tintMaterialNames)
        {
            if (string.IsNullOrEmpty(key)) continue;
            if (name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
