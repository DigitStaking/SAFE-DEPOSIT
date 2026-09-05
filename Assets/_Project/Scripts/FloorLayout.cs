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

    /// <summary>The spine, in the order you walk it. One entry per main room,
    /// and a floor may use the same main more than once.</summary>
    public int[] mains;

    /// <summary>Whether a side room hangs off each main. Same length as
    /// mains.</summary>
    public bool[] sideAt;

    /// <summary>Whether the spine ends in a dead end.</summary>
    public bool hasBack;

    /// <summary>How many rooms a player walks, landing included.</summary>
    public int RoomCount
    {
        get
        {
            int n = 1 + (mains != null ? mains.Length : 0) + (hasBack ? 1 : 0);
            if (sideAt != null)
                foreach (bool b in sideAt) if (b) n++;
            return n;
        }
    }

    /// <summary>
    /// Mixed so that neighbouring floors of the same run do not produce
    /// neighbouring sequences. A plain seed of run + floor gives floor 3 of
    /// run 1 and floor 1 of run 3 the same building, which a player WILL
    /// notice by the third run even though no single floor looks wrong.
    /// </summary>
    static int SeedFor(int runNumber, int floor) =>
        unchecked(runNumber * 73856093) ^ unchecked((floor + 1) * 19349663);

    /// <summary>Separate stream for the first main, so which room you walk
    /// into is independent of how long the floor is.</summary>
    static int StepSeedFor(int runNumber, int floor) =>
        unchecked(SeedFor(runNumber, floor) * 83492791) ^ 0x5bf03635;

    /// <summary>
    /// The floor's shape, from numbers every machine already shares.
    ///
    /// ---- LENGTH IS THE VARIETY. WHICH ROOM IS NOT. ----
    ///
    /// The first version built landing -> main -> side -> back every single
    /// time, and varied only WHICH main. Ten of them side by side were ten
    /// identical silhouettes: the difference was a partition wall you had to
    /// walk inside to see, while the shape you actually navigate by never
    /// changed at all.
    ///
    /// A player does not remember which main room they were in. They remember
    /// that the fourth floor went on forever and the fifth was a cupboard. So
    /// the spine is 1 to 4 mains now, side rooms hang off any of them, and the
    /// dead end is optional - between two and eight rooms per floor instead of
    /// a fixed four.
    ///
    /// Repeats within a floor are allowed and wanted. A corridor of two halls
    /// end to end reads as a long floor, not as a bug - it is only the room
    /// you just LEFT that must not be the room you just entered.
    /// </summary>
    public static FloorLayout For(int runNumber, int floor,
                                  int landingCount, int mainCount)
    {
        var rng = new System.Random(SeedFor(runNumber, floor));

        var l = new FloorLayout
        {
            landing = landingCount > 0 ? rng.Next(landingCount) : 0,
            hasBack = rng.NextDouble() < 0.7
        };

        if (mainCount <= 0)
        {
            l.mains = new int[0];
            l.sideAt = new bool[0];
            return l;
        }

        // 1..4, weighted toward the middle. A floor of one main is a short
        // stop; four is a march. Both should happen, and neither every time.
        int[] lengths = { 1, 2, 2, 3, 3, 4 };
        int spine = lengths[rng.Next(lengths.Length)];

        l.mains = new int[spine];
        l.sideAt = new bool[spine];

        // The first main is the cross-floor rule: it must differ from the
        // first main of the floor above, because that is the room you just
        // left. See FirstMainFor.
        l.mains[0] = FirstMainFor(runNumber, floor, mainCount);

        for (int i = 1; i < spine; i++)
        {
            // Within a floor, only ADJACENT repeats are avoided - two halls
            // back to back read as one enormous room rather than as a
            // corridor, which loses the length the spine was for.
            int pick = rng.Next(mainCount);
            if (mainCount > 1 && pick == l.mains[i - 1])
                pick = (pick + 1 + rng.Next(mainCount - 1)) % mainCount;

            l.mains[i] = pick;
        }

        for (int i = 0; i < spine; i++)
            l.sideAt[i] = rng.NextDouble() < 0.45;

        return l;
    }

    /// <summary>
    /// The first main, guaranteed different from the floor above's first main.
    ///
    /// ---- WHY THIS WALKS AND DOES NOT JUST COMPARE ----
    ///
    /// The first version drew a main for this floor, drew the floor above's
    /// main, and nudged on a collision. It did not work, and the failure is
    /// worth keeping: the floor above's main may ITSELF have been nudged, so
    /// comparing against its raw draw compares against a room that was never
    /// built. Repeats survived at roughly the rate you would expect from no
    /// rule at all - code that reads correct with output that is only
    /// sometimes wrong, which is the kind that ships.
    ///
    /// So the choice is made by STEPPING. Each floor moves 1..n-1 places
    /// around the set of mains, and a step of at least one cannot land where
    /// it started. No comparison, nothing to get wrong.
    ///
    /// The walk from floor 1 is at most twenty iterations of integer
    /// arithmetic, and it keeps this a pure function of (run, floor): nothing
    /// is remembered between calls, so a client joining on floor 12 asks for
    /// floor 12 and gets the host's answer. Recomputing is not state.
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
