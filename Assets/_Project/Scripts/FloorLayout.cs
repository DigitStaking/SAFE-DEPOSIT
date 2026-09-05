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
    public int landing;
    public int firstMain;

    /// <summary>How many rooms this floor is allowed, landing included.</summary>
    public int roomBudget;

    /// <summary>Drives the branching walk in FloorGenerator. Handed over
    /// rather than used here, because the shape of a TREE depends on which
    /// exits actually exist on the modules chosen - a fact about prefabs, and
    /// this file deliberately knows nothing about prefabs.</summary>
    public int seed;

    static int SeedFor(int runNumber, int floor) =>
        unchecked(runNumber * 73856093) ^ unchecked((floor + 1) * 19349663);

    static int StepSeedFor(int runNumber, int floor) =>
        unchecked(SeedFor(runNumber, floor) * 83492791) ^ 0x5bf03635;

    /// <summary>
    /// The floor's budget and its first room, from numbers every machine
    /// already shares.
    ///
    /// ---- WHAT MAKES FLOORS DIFFERENT ----
    ///
    /// Two things, and neither is which main was picked. LENGTH - a floor of
    /// three rooms and a floor of nine are not the same place - and SHAPE,
    /// because a main offers left, right and straight on, so the budget is
    /// spent as a tree rather than as a queue.
    ///
    /// The first version varied only the main module, which is a partition
    /// wall you have to walk inside to notice. Ten floors of that were ten
    /// identical silhouettes.
    /// </summary>
    public static FloorLayout For(int runNumber, int floor,
                                  int landingCount, int mainCount)
    {
        var rng = new System.Random(SeedFor(runNumber, floor));

        // 3..9 rooms, weighted toward the middle. A floor of three is a quick
        // stop; nine is somewhere you can get lost. Both should happen.
        int[] budgets = { 3, 4, 4, 5, 5, 6, 6, 7, 8, 9 };

        return new FloorLayout
        {
            landing = landingCount > 0 ? rng.Next(landingCount) : 0,
            firstMain = FirstMainFor(runNumber, floor, mainCount),
            roomBudget = budgets[rng.Next(budgets.Length)],
            seed = StepSeedFor(runNumber, floor)
        };
    }

    /// <summary>
    /// The first main, guaranteed different from the floor above's.
    ///
    /// ---- WHY THIS WALKS AND DOES NOT JUST COMPARE ----
    ///
    /// The first version drew this floor's main, drew the floor above's, and
    /// nudged on a collision. It did not work: the floor above's main may
    /// ITSELF have been nudged, so it compared against a room that was never
    /// built, and repeats survived at about the rate of having no rule at all.
    /// Code that reads correct with output that is only sometimes wrong.
    ///
    /// Stepping instead. Each floor moves 1..n-1 places around the set, and a
    /// step of at least one cannot land where it started. The walk from floor
    /// 1 is at most twenty integer operations and keeps this a pure function
    /// of (run, floor) - a client joining on floor 12 asks for 12 and gets the
    /// host's answer. Recomputing is not state.
    /// </summary>
    static int FirstMainFor(int runNumber, int floor, int mainCount)
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
