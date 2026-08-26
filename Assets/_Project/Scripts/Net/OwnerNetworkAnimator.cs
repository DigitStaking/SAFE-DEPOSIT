// OwnerNetworkAnimator.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/OwnerNetworkAnimator.cs
// Goes on: the Player prefab, next to NetworkTransform.
//
// ====================================================================
// PHASE 4 - THE BODY MOVES, BUT IT DOES NOT ACT.
//
// Step 2 put the BODY on the wire. NetworkTransform sends where you are and
// which way you face, and that is all it sends. Everything else your body
// does - walking, dancing, kneeling when downed, reaching out to grab a crate
// - is an ANIMATOR PARAMETER, and no animator parameter has ever left this
// machine.
//
// So you dance and nobody sees it. From across the lift you are a red suit
// gliding around in a permanent idle pose. Reported exactly that way: "i'm
// right now dancing but the other can't see it".
//
// WHY THE MOVEMENT ANIMATION WAS BROKEN TOO, WHICH IS SUBTLER
//
// PlayerAnimatorDriver reads rb.linearVelocity and feeds it to the blend
// tree. On a REMOTE body the Rigidbody is not moving under its own power -
// NetworkTransform is writing the transform directly - so the velocity it
// reads is roughly zero and the blend tree correctly plays "standing still"
// for somebody sprinting past. The animation was not missing. It was being
// computed from the wrong machine's idea of what that body was doing.
//
// So the driver is now gated to the OWNER, and the parameters it produces
// are what travel. One machine decides what its body is doing; everyone else
// is told.
//
// WHY OWNER AUTHORITY AND NOT SERVER
//
// NGO's NetworkAnimator is server-authoritative by default: your dance would
// go to the host, and the host would tell everyone including you. That is a
// round trip before your OWN emote plays on your OWN screen, which feels
// like input lag on the one thing that has no reason to have any.
//
// Owner authority matches NetworkTransform, which was set the same way in
// Step 2 for the same reason: this is co-op PvE. Nobody is being shot,
// there is nothing to cheat for, and the worst case is a teammate's dance
// arriving 50ms late.
//
// One override, and it is the whole file.
// ====================================================================

using Unity.Netcode.Components;

public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative() => false;
}
