// RetireOldHandScripts.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Editor/RetireOldHandScripts.cs
// Run from: SAFE DEPOSIT / Player / Retire Old Hand Scripts
//
// ========================================================================
// THREE THINGS WERE WRITING THE SAME TWO HAND IK GOALS.
//
// "check if there is other scripts about carry remove them because they make
//  this new script hard to use"
//
// Correct, and here is the exact collision. On the local player's body:
//
//   30  FirstPersonHands   pins BOTH hands toward the camera at weight 0.4,
//                          every single frame
//   35  PlayerCarryArms    places them on the item, or zeroes both goals
//   40  PlayerPushArms     shoves, or releases - but only if it believes
//                          nobody else is driving the hands
//
// Execution order means 35 runs after 30, so PlayerCarryArms overwrites
// FirstPersonHands on every frame it has an opinion, and zeroes it on every
// frame it does not. FirstPersonHands' output never survives to the screen.
// It is doing a full solve per frame to be discarded.
//
// Worse than wasted work: PlayerPushArms decides whether to release its own
// goals by asking whether FirstPersonHands is enabled. That question used to
// mean "is somebody else driving the hands". It does not mean that any more,
// because the answer can be yes while its output is being thrown away. A
// stale question with a confident answer is harder to debug than no answer.
//
// ---- WHY THE COMPONENT AND NOT THE FILE ----
//
// The class stays. It is still referenced by AnimatorBuilder, FirstPersonFixer
// and ArmPoseAudit, and deleting a 472-line file to fix a prefab is how you
// spend an afternoon chasing compile errors instead of testing a grip.
//
// What gets removed is the COMPONENT, from the prefab, which is the thing
// actually running. Reversible in one click, and nothing else has to change.
//
// ---- WHAT IS GIVEN UP, HONESTLY ----
//
// FirstPersonHands was the documented fallback for a viewmodel that fails to
// build: no arms rig, no hands at all. After this, a failed viewmodel means
// the local player sees their real arms doing normal animation instead of
// camera-locked fake hands. That is a worse failure mode than it sounds like
// it should be, and it is the reason this is a deliberate menu item rather
// than something that happened quietly during a repair.
// ========================================================================

using System.Text;
using UnityEditor;
using UnityEngine;

public static class RetireOldHandScripts
{
    const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

    [MenuItem("SAFE DEPOSIT/Player/Retire Old Hand Scripts")]
    public static void Retire()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        if (root == null)
        {
            Debug.LogError("[Retire] Could not open " + PlayerPrefabPath);
            return;
        }

        try
        {
            var log = new StringBuilder("[Retire] ");
            int removed = 0;

            // Anywhere under the player, not just the model - an earlier tool
            // put a stray copy on the root once and it took a while to find.
            foreach (var hands in root.GetComponentsInChildren<FirstPersonHands>(true))
            {
                log.Append("FirstPersonHands removed from '")
                   .Append(hands.gameObject.name).Append("'. ");

                Object.DestroyImmediate(hands, true);
                removed++;
            }

            if (removed == 0) log.Append("No FirstPersonHands found - already retired. ");

            // ---- report what is left holding the hands ----
            //
            // Printed rather than assumed, because the whole point of this
            // exercise was that nobody could tell who was driving them.
            var visual = root.GetComponentInChildren<Animator>(true);

            if (visual != null)
            {
                log.Append("\n  Hand IK now written by: ");

                var carry = visual.GetComponent<PlayerCarryArms>();
                var push = visual.GetComponent<PlayerPushArms>();
                var curl = visual.GetComponent<HandFingerCurl>();

                log.Append(carry != null ? "PlayerCarryArms(35) " : "");
                log.Append(push != null ? "PlayerPushArms(40) " : "");
                log.Append(curl != null ? "+ HandFingerCurl(60, fingers only)" : "");

                if (carry == null)
                    log.Append("\n  WARNING: no PlayerCarryArms - run Repair Player " +
                               "Prefab Components.");
            }

            if (removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                log.Append("\n  Prefab saved.");
            }

            Debug.Log(log.ToString());
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
