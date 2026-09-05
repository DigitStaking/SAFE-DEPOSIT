using UnityEngine;

/// <summary>
/// The seed a floor is generated from. Nothing else lives here any more.
///
/// ====================================================================
/// WHY THE SEED IS RunNumber AND NOT A NEW NETWORK VARIABLE
///
/// PHASE5_SPEC asks for a seed published by the host, the way LootSpawner's
/// roster is. There already is one: Campaign.RunNumber is a
/// NetworkVariable&lt;int&gt; the host owns and every machine already agrees
/// on. Deriving the floor from it means four machines build the same building
/// without one byte of geometry crossing the wire, and without a second thing
/// that can disagree.
///
/// A new seed variable would have been a second source of truth for "which run
/// is this", which is the mistake Phase 4 spent seven weeks undoing across 59
/// statics.
///
/// WHY System.Random AND NOT UnityEngine.Random
///
/// UnityEngine.Random is global mutable state. Its next value depends on how
/// many times anything else in the process has rolled, so two machines agree
/// only while every other system makes the same number of draws in the same
/// order - which nobody can maintain, and which fails silently and rarely.
/// Determinism that depends on call order is not determinism.
///
/// THE ATTEMPT NUMBER
///
/// The generator rejects a floor it cannot place correctly and tries again.
/// Each attempt needs its own stream, or every retry reproduces the failure
/// it just rejected - so the attempt is mixed into the seed rather than the
/// generator drawing more numbers from the same one.
/// ====================================================================
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public static class FloorLayout
{
    /// <summary>
    /// Mixed so neighbouring floors of the same run do not produce
    /// neighbouring sequences. A plain run + floor gives floor 3 of run 1 and
    /// floor 1 of run 3 the same building, which a player WILL notice by the
    /// third run even though no single floor looks wrong.
    /// </summary>
    public static int SeedFor(int runNumber, int floor, int attempt = 0) =>
        unchecked(runNumber * 73856093)
        ^ unchecked((floor + 1) * 19349663)
        ^ unchecked((attempt + 1) * 83492791);
}
