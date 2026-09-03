// FirstPersonViewmodelSettings.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/FirstPersonViewmodelSettings.cs
//
// ========================================================================
// THE VIEWMODEL NUMBERS, AS A FILE YOU CAN ACTUALLY FIND.
//
// "I CHANGED FIRST PERSON VIEW BEFORE IN GAME AND NOTHING CHANGED WHEN I GO
//  BACK FROM GAME AND I CAN'T FIND IT OUTSIDE OF GAME"
//
// Both halves of that are true and both are my fault. FirstPersonViewmodel
// builds itself at runtime with RuntimeInitializeOnLoadMethod, so:
//
//   * outside Play mode the object does not exist at all, so there is nothing
//     in the Hierarchy to select and nothing to set up in advance
//   * inside Play mode it is a runtime object like any other, so Unity throws
//     away every value you dragged the moment you press Stop
//
// Which makes it perfectly tunable and completely unable to remember
// anything - the worst possible combination, and exactly the trap that let
// two wrong guesses at the placement survive so long.
//
// A ScriptableObject fixes both at once. It is a real asset sitting in the
// project: selectable outside Play mode, saved by Unity like any other file,
// committed to git with everything else. The runtime component READS it every
// frame, so dragging a value during play still updates the game instantly -
// and now the value is still there when you stop.
//
// Tune the ASSET, not the runtime object. The runtime object's own fields are
// only the fallback for when this asset is missing.
// ========================================================================

using UnityEngine;

[CreateAssetMenu(fileName = "FirstPersonViewmodelSettings",
                 menuName = "SAFE DEPOSIT/First Person Viewmodel Settings")]
public class FirstPersonViewmodelSettings : ScriptableObject
{
    /// <summary>
    /// Where the runtime component looks for this. Must be inside a folder
    /// literally named "Resources" - that is a Unity special case, not a
    /// naming preference.
    /// </summary>
    public const string ResourceName = "FirstPersonViewmodelSettings";

    [Header("Viewmodel - drag these while playing, they now PERSIST")]
    [Tooltip("Show the arms at all. Quickest A/B against no viewmodel.")]
    public bool visible = true;

    [Tooltip("Position of the arms relative to the camera. X right, Y up, " +
             "Z forward, in metres. " +
             "Y IS VERY NEGATIVE ON PURPOSE: the arms rig's origin is the " +
             "CHARACTER'S ROOT, between the feet, not its shoulders. The arms " +
             "sit about 1.4m above that origin inside the model, so a root at " +
             "the camera puts the hands well over your head.")]
    public Vector3 localPosition = new Vector3(0f, -1.05f, 0.35f);

    [Header("Height - measured, not guessed")]
    [Tooltip("Work out the arms' HEIGHT automatically instead of using " +
             "localPosition.y. " +
             "Strongly recommended. The rig's origin is between the feet, so " +
             "the correct Y depends on the eye height, the scale AND how far " +
             "up the arms sit inside the model - three numbers that were each " +
             "being guessed at separately. This measures the rig and solves " +
             "for it, so changing scale or eyeOffset corrects itself instead " +
             "of needing the offset re-tuned by hand.")]
    public bool deriveHeightFromEye = true;

    [Tooltip("How far BELOW eye level the hands sit, in metres. The one " +
             "number worth tuning by feel once deriveHeightFromEye is on - " +
             "bigger drops them further down the screen.")]
    public float handsBelowEye = 0.42f;

    [Tooltip("Rotation of the arms relative to the camera, in degrees.")]
    public Vector3 localEulerAngles = Vector3.zero;

    [Tooltip("Uniform scale of the whole arm rig.")]
    [Range(0.1f, 2f)] public float localScale = 0.7f;

    [Header("Per hand - fine placement")]
    [Tooltip("Extra offset for the LEFT hand only, in metres, in CAMERA space.")]
    public Vector3 leftHandOffset = Vector3.zero;

    [Tooltip("Extra offset for the RIGHT hand only, in metres, in CAMERA space.")]
    public Vector3 rightHandOffset = Vector3.zero;

    [Tooltip("Pushes BOTH hands apart along the camera's right axis.")]
    public float handSpread = 0f;

    [Tooltip("Pushes BOTH hands forward along the camera's forward axis.")]
    public float handReach = 0f;

    [Header("Only show the hands when they are doing something")]
    [Tooltip("Keep the arms out of sight until you actually use them. " +
             "OFF WHILE YOU ARE PLACING THEM - you cannot position something " +
             "that is only on screen for half a second at a time. Turn it back " +
             "on once the position, rotation and scale are where you want " +
             "them, and the hide behaviour is waiting exactly as it was.")]
    public bool showOnlyWhenBusy = false;

    [Tooltip("Where the arms rest while idle, as an offset from their normal " +
             "position. Straight down by default so they lower out of frame " +
             "and rise back into it rather than blinking on and off.")]
    public Vector3 hiddenOffset = new Vector3(0f, -0.45f, 0f);

    [Tooltip("Seconds for the arms to rise into view or lower back out.")]
    public float raiseTime = 0.22f;

    [Tooltip("Seconds the hands stay up after an action finishes, so grabbing " +
             "two things in a row does not lower and raise them twice.")]
    public float holdAfter = 0.6f;

    [Header("Push - the shove, seen from your own eyes")]
    [Tooltip("How far the hands MOVE during a shove, in metres, in CAMERA " +
             "space: X right, Y up, Z forward. " +
             "Was two separate numbers - forward reach and downward drop - " +
             "which meant the shove could only ever go straight out and down. " +
             "One vector gives the whole direction, so the hands can push up, " +
             "across, or wherever the gesture actually wants to go. " +
             "SMALL ON PURPOSE: a push is mostly the palms TURNING, see " +
             "pushTurn. 0.38 forward on its own read as arms flying.")]
    public Vector3 pushMove = new Vector3(0f, -0.02f, 0.12f);

    [Tooltip("How far the palms rotate to face forward at full push, in " +
             "degrees. THIS is the gesture - the hands turning into the shove. " +
             "Mirrored between the two hands, so Z is the one that usually " +
             "matters and the sign flips itself for the left hand.")]
    public Vector3 pushTurn = new Vector3(0f, 0f, 55f);

    [Tooltip("How far the hands draw BACK before the thrust, in metres.")]
    public float pushWindBack = 0.05f;

    [Tooltip("How much the hands separate during the shove, in metres. Two " +
             "palms going out, not one fist.")]
    public float pushSpread = 0.04f;


    [Header("Camera")]
    [Tooltip("Field of view of the dedicated viewmodel camera, in degrees.")]
    public float fieldOfView = 60f;

    [Tooltip("Near clip of the viewmodel camera. Can be tiny - its culling " +
             "mask contains nothing but the arms.")]
    public float nearClip = 0.01f;

    [Header("Animation")]
    [Tooltip("Drive the viewmodel arms from the same animator parameters and " +
             "layer weights as your real body, so they swing when you walk " +
             "and breathe when you stand still.")]
    public bool followBodyAnimation = true;
}
