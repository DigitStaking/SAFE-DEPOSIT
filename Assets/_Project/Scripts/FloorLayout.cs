using UnityEngine;

/// <summary>
/// Which rooms a floor is made of. A pure function, and deliberately nothing
/// else.
///
/// ====================================================================
/// WHY THE SEED IS RunNumber AND NOT A NEW NETWORK VARIABLE
///
/// PHASE5_SPEC asks for a seed published by the host, the way LootSpawner's
/// roster is. There already is one: Campaign.RunNumber is a
/// NetworkVariable&lt;int&gt; the host owns and every machine already agrees
/// on. Deriving the layout from it means four machines build the same
/// building without one byte of geometry crossing the wire, and without a
/// second thing that can disagree.
///
/// A new seed variable would have been a second source of truth for "which
/// run is this", and this project has already paid for that mistake once, in
/// the 59 statics Phase 4 spent seven weeks undoing.
///
/// WHY System.Random AND NOT UnityEngine.Random
///
/// UnityEngine.Random is global mutable state. Its next value depends on how
/// many times anything else in the process has called it, so two machines
/// running the same seed produce the same floors only while every other
/// system makes the same number of rolls in the same order - which is not a
/// property anybody can maintain, and which fails silently and rarely.
///
/// A local System.Random seeded per floor cannot be perturbed by anything
/// else. Determinism that depends on call order is not determinism.
/// ====================================================================
///
/// Phase 5, Step 6. See PHASE5_SPEC.md.
/// </summary>
public struct FloorLayout
{
    public int landing;      // index into the landing modules
    public int main;         // index into the main modules
    public bool hasSide;
    public bool hasBack;

    /// <summary>
    /// Mixed so that neighbouring floors of the same run do not produce
    /// neighbouring sequences. A plain seed of run + floor gives floor 3 of
    /// run 1 and floor 1 of run 3 the same building, which a player WILL
    /// notice by the third run even though no single floor looks wrong.
    /// </summary>
    static int SeedFor(int runNumber, int floor) =>
        unchecked(runNumber * 73856093) ^ unchecked((floor + 1) * 19349663);

    /// <summary>Separate stream for the main-room walk, so the step a floor
    /// takes is independent of which landing it drew.</summary>
    static int StepSeedFor(int runNumber, int floor) =>
        unchecked(SeedFor(runNumber, floor) * 83492791) ^ 0x5bf03635;

    /// <summary>
    /// The floor's shape, from numbers every machine already shares.
    ///
    /// <paramref name="floor"/> is 1-based, matching Level_01.
    /// </summary>
    public static FloorLayout For(int runNumber, int floor,
                                  int landingCount, int mainCount)
    {
        var rng = new System.Random(SeedFor(runNumber, floor));

        return new FloorLayout
        {
            landing = landingCount > 0 ? rng.Next(landingCount) : 0,
            main = MainFor(runNumber, floor, mainCount),

            // Not every floor gets everything. A side room on every floor is
            // not a side room, it is a corridor with a bulge - the point of
            // "optional" is that finding one is worth something.
            hasSide = rng.NextDouble() < 0.6,
            hasBack = rng.NextDouble() < 0.75
        };
    }

    /// <summary>
    /// Which main room, guaranteed different from the floor above.
    ///
    /// ---- WHY THIS WALKS AND DOES NOT JUST COMPARE ----
    ///
    /// The first version drew a main for this floor, drew the floor above's
    /// main, and nudged on a collision. It did not work, and the failure is
    /// worth keeping: the floor above's main may ITSELF have been nudged, so
    /// comparing against its raw draw compares against a room that was never
    /// built. Repeats survived at roughly the rate you would expect from no
    /// rule at all, which is exactly the kind of bug that ships - the code
    /// reads correct and the output is only wrong sometimes.
    ///
    /// So the choice is made by STEPPING instead. Each floor moves 1..n-1
    /// places around the set of mains, and a step of at least one cannot land
    /// where it started. No comparison, no nudge, and nothing to get wrong.
    ///
    /// It costs a walk from floor 1, which is at most twenty iterations of
    /// integer arithmetic. That is still a pure function of (run, floor):
    /// nothing is remembered between calls, so a client joining on floor 12
    /// asks for floor 12 and gets the same answer as the host who walked
    /// through it. Recomputing is not state.
    ///
    /// WITH ONLY TWO MAINS THIS FORCES A,B,A,B - the step can only be 1. That
    /// is a visible pattern and it is not a generator problem: two of anything
    /// alternates. The answer is more main modules in Step 5, and this comment
    /// is here so that when someone notices the alternation they change the
    /// module set rather than this file.
    /// </summary>
    static int MainFor(int runNumber, int floor, int mainCount)
    {
        if (mainCount <= 1) return 0;

        int current = new System.Random(SeedFor(runNumber, 1)).Next(mainCount);

        for (int f = 2; f <= floor; f++)
        {
            int step = 1 + new System.Random(StepSeedFor(runNumber, f)).Next(mainCount - 1);
            current = (current + step) % mainCount;
        }

        return current;
    }
}
