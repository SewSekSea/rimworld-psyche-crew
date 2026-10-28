using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Crew
{
    /// <summary>A group of colonists who all rate each other. Worked out from opinion, never assigned.</summary>
    public class CrewGroup
    {
        public readonly List<Pawn> members = new List<Pawn>();
        public Pawn leader;

        public string Label => leader == null ? "a crew" : leader.LabelShortCap + "'s crew";

        public bool Contains(Pawn p) => members.Contains(p);
    }

    /// <summary>What was there last time, so the mod can tell the player when it changes.</summary>
    public class CrewSnapshot : IExposable
    {
        public List<int> memberIds = new List<int>();
        public int leaderId = -1;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref memberIds, "memberIds", LookMode.Value);
            Scribe_Values.Look(ref leaderId, "leaderId", -1);
            if (memberIds == null)
            {
                memberIds = new List<int>();
            }
        }

        /// <summary>How much this crew and that list of people are the same group.</summary>
        public float Overlap(List<Pawn> others)
        {
            if (memberIds.Count == 0 || others.Count == 0)
            {
                return 0f;
            }
            int shared = 0;
            for (int i = 0; i < others.Count; i++)
            {
                if (memberIds.Contains(others[i].thingIDNumber))
                {
                    shared++;
                }
            }
            int larger = memberIds.Count > others.Count ? memberIds.Count : others.Count;
            return shared / (float)larger;
        }
    }

    /// <summary>
    /// Works out who runs with whom, which crews are at odds, and tells the player when that changes.
    /// The crews themselves are derived from opinion every so often rather than stored.
    /// </summary>
    public class GameComponent_Crews : GameComponent
    {
        private static readonly List<CrewGroup> crews = new List<CrewGroup>();
        private static readonly Dictionary<Pawn, int> standings = new Dictionary<Pawn, int>();
        private static readonly HashSet<long> rivalPairs = new HashSet<long>();
        private static GameComponent_Crews instance;

        private List<CrewSnapshot> known = new List<CrewSnapshot>();

        public GameComponent_Crews(Game game)
        {
            instance = this;
        }

        public static GameComponent_Crews Instance => instance ?? Current.Game?.GetComponent<GameComponent_Crews>();

        public override void FinalizeInit()
        {
            instance = this;
            Recalculate();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref known, "known", LookMode.Deep);
            if (known == null)
            {
                known = new List<CrewSnapshot>();
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % Tuning.RecalcTicks != 0)
            {
                return;
            }
            Recalculate();
        }

        // ---- working out the crews ---------------------------------------

        /// <summary>
        /// Work the crews out again.
        ///
        /// The new answer is built alongside the old one and swapped in only when it is finished.
        /// That is not tidiness, it is the whole correctness of the thing.
        ///
        /// Every opinion this method reads makes the game evaluate situational social thoughts,
        /// and one of those is this mod's own CR_RunsWithThem -- which asks AreRivals, which reads
        /// `crews` and `rivalPairs`. Clearing those first meant every opinion consulted during the
        /// rebuild was missing the running-with-the-wrong-crowd penalty entirely: crews, leaders
        /// and rivalries were all decided on a table that said nobody ran with anybody, so a
        /// rivalry could never reinforce itself. It never threw, which is why it was never noticed.
        /// </summary>
        public static void Recalculate()
        {
            List<CrewGroup> fresh = new List<CrewGroup>();
            HashSet<long> freshRivals = new HashSet<long>();

            List<Pawn> pool = new List<Pawn>();
            List<Pawn> all = PawnsFinder.AllMaps_FreeColonists;
            for (int i = 0; i < all.Count; i++)
            {
                if (CR.Counts(all[i]))
                {
                    pool.Add(all[i]);
                }
            }
            if (pool.Count < Tuning.MinCrewSize)
            {
                // Too few people left for any crew at all. The empty answer still has to be
                // swapped in, or the colony keeps the crews it had when it was bigger.
                Commit(fresh, freshRivals);
                Instance?.Announce();
                return;
            }

            // Best regarded first, so crews form around the people others actually rate.
            //
            // Worked out once per person and then sorted on the stored value. Calling Standing from
            // inside the comparison re-scanned the whole colony twice for every comparison the sort
            // made, which is a great deal of opinion lookups for a result that cannot change during
            // the sort.
            standings.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                standings[pool[i]] = Standing(pool[i], pool);
            }
            pool.Sort((a, b) => standings[b].CompareTo(standings[a]));

            HashSet<Pawn> taken = new HashSet<Pawn>();
            for (int i = 0; i < pool.Count; i++)
            {
                Pawn seed = pool[i];
                if (taken.Contains(seed))
                {
                    continue;
                }
                CrewGroup crew = new CrewGroup();
                crew.members.Add(seed);

                for (int j = 0; j < pool.Count; j++)
                {
                    Pawn candidate = pool[j];
                    if (candidate == seed || taken.Contains(candidate) || crew.Contains(candidate))
                    {
                        continue;
                    }
                    if (AcceptedBy(crew, candidate))
                    {
                        crew.members.Add(candidate);
                    }
                }

                if (crew.members.Count >= Tuning.MinCrewSize)
                {
                    crew.leader = PickLeader(crew);
                    fresh.Add(crew);
                    for (int m = 0; m < crew.members.Count; m++)
                    {
                        taken.Add(crew.members[m]);
                    }
                }
            }

            FindRivalries(fresh, freshRivals);
            Commit(fresh, freshRivals);
            Instance?.Announce();
        }

        /// <summary>
        /// Swap the finished answer in. Nothing reads a half-built one because nothing is written
        /// until here, and the two collections change together in the same breath.
        /// </summary>
        private static void Commit(List<CrewGroup> fresh, HashSet<long> freshRivals)
        {
            crews.Clear();
            crews.AddRange(fresh);
            rivalPairs.Clear();
            foreach (long key in freshRivals)
            {
                rivalPairs.Add(key);
            }
        }

        /// <summary>How many of these people rate this one highly.</summary>
        private static int Standing(Pawn p, List<Pawn> pool)
        {
            int n = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != p && CR.Opinion(pool[i], p) >= Tuning.CrewOpinion)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Enough of the crew has to be willing, not just the person who started it.</summary>
        private static bool AcceptedBy(CrewGroup crew, Pawn candidate)
        {
            int willing = 0;
            for (int i = 0; i < crew.members.Count; i++)
            {
                if (CR.RunsWith(crew.members[i], candidate))
                {
                    willing++;
                }
            }
            return willing >= crew.members.Count * Tuning.AcceptFraction;
        }

        private static Pawn PickLeader(CrewGroup crew)
        {
            Pawn best = crew.members[0];
            int bestScore = int.MinValue;
            for (int i = 0; i < crew.members.Count; i++)
            {
                Pawn p = crew.members[i];
                int score = 0;
                for (int j = 0; j < crew.members.Count; j++)
                {
                    if (i != j)
                    {
                        score += CR.Opinion(crew.members[j], p);
                    }
                }
                if (score > bestScore)
                {
                    best = p;
                    bestScore = score;
                }
            }
            return best;
        }

        private static long PairKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        private static void FindRivalries(List<CrewGroup> crews, HashSet<long> rivalPairs)
        {
            if (!Tuning.Rivalries)
            {
                return;
            }
            for (int i = 0; i < crews.Count; i++)
            {
                for (int j = i + 1; j < crews.Count; j++)
                {
                    int bad = 0;
                    int pairs = 0;
                    for (int a = 0; a < crews[i].members.Count; a++)
                    {
                        for (int b = 0; b < crews[j].members.Count; b++)
                        {
                            pairs++;
                            int one = CR.Opinion(crews[i].members[a], crews[j].members[b]);
                            int two = CR.Opinion(crews[j].members[b], crews[i].members[a]);
                            if (one <= Tuning.RivalOpinion || two <= Tuning.RivalOpinion)
                            {
                                bad++;
                            }
                        }
                    }
                    // A third of the cross pairs going badly is enough for the whole thing to sour.
                    if (pairs > 0 && bad / (float)pairs >= 0.34f)
                    {
                        rivalPairs.Add(PairKey(i, j));
                    }
                }
            }
        }

        // ---- questions the rest of the mod asks ---------------------------

        public static CrewGroup CrewOf(Pawn p)
        {
            for (int i = 0; i < crews.Count; i++)
            {
                if (crews[i].Contains(p))
                {
                    return crews[i];
                }
            }
            return null;
        }

        private static int IndexOfCrew(Pawn p)
        {
            for (int i = 0; i < crews.Count; i++)
            {
                if (crews[i].Contains(p))
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool SameCrew(Pawn a, Pawn b)
        {
            CrewGroup c = CrewOf(a);
            return c != null && c.Contains(b);
        }

        public static bool AreRivals(Pawn a, Pawn b)
        {
            if (!Tuning.Rivalries || a == null || b == null || a == b)
            {
                return false;
            }
            int ia = IndexOfCrew(a);
            int ib = IndexOfCrew(b);
            if (ia < 0 || ib < 0 || ia == ib)
            {
                return false;
            }
            return rivalPairs.Contains(PairKey(ia, ib));
        }

        public static int CrewCount => crews.Count;

        public static bool AnyCrews => crews.Count > 0;

        /// <summary>
        /// Every crew as it currently stands, for the overview tab. Read only: crews are worked out
        /// from opinion on a schedule and editing this list would simply be overwritten.
        /// </summary>
        public static List<CrewGroup> AllCrews => crews;

        /// <summary>
        /// The colonist a remembered crew was led by, if they are still findable. Used to name a
        /// crew that has just dissolved, when there is no longer a live crew to ask.
        /// </summary>
        private static Pawn LeaderOfSnapshot(CrewSnapshot snap)
        {
            if (snap == null || snap.leaderId < 0)
            {
                return null;
            }
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                foreach (Pawn p in maps[m].mapPawns.FreeColonistsSpawned)
                {
                    if (p.thingIDNumber == snap.leaderId)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        // ---- telling the player -------------------------------------------

        private void Announce()
        {
            List<CrewSnapshot> fresh = new List<CrewSnapshot>();
            List<bool> matched = new List<bool>();
            for (int i = 0; i < known.Count; i++)
            {
                matched.Add(false);
            }

            for (int i = 0; i < crews.Count; i++)
            {
                CrewGroup crew = crews[i];
                int bestIndex = -1;
                float bestOverlap = 0f;
                for (int k = 0; k < known.Count; k++)
                {
                    if (matched[k])
                    {
                        continue;
                    }
                    float o = known[k].Overlap(crew.members);
                    if (o > bestOverlap)
                    {
                        bestOverlap = o;
                        bestIndex = k;
                    }
                }

                // Mostly the same people means the same crew carrying on, not a new one.
                bool isNew = bestOverlap < 0.5f;
                if (!isNew)
                {
                    matched[bestIndex] = true;
                    if (known[bestIndex].leaderId != crew.leader.thingIDNumber && Find.TickManager.TicksGame > 2000)
                    {
                        Messages.Message(
                            $"{crew.leader.LabelShortCap} is the one the others look to now.",
                            crew.leader, MessageTypeDefOf.NeutralEvent);
                    }
                }
                else if (Find.TickManager.TicksGame > 2000)
                {
                    Find.LetterStack.ReceiveLetter(
                        crew.Label + " has formed",
                        $"{crew.leader.LabelShortCap} and {crew.members.Count - 1} other" +
                        (crew.members.Count - 1 == 1 ? string.Empty : "s") +
                        " have quietly become a group of their own.\n\n" +
                        "Nobody decided this and you cannot assign it. They simply rate each other, " +
                        "and from here they will take each other's side.",
                        LetterDefOf.NeutralEvent, new LookTargets(crew.members));
                }

                CrewSnapshot snap = new CrewSnapshot { leaderId = crew.leader.thingIDNumber };
                for (int m = 0; m < crew.members.Count; m++)
                {
                    snap.memberIds.Add(crew.members[m].thingIDNumber);
                }
                fresh.Add(snap);
            }

            for (int k = 0; k < known.Count; k++)
            {
                if (!matched[k] && known[k].memberIds.Count >= Tuning.MinCrewSize && Find.TickManager.TicksGame > 2000)
                {
                    // Name it if the leader is still around to name it after. "A crew here has
                    // broken up" told the player nothing they could act on or even identify.
                    Pawn wasLed = LeaderOfSnapshot(known[k]);
                    Messages.Message(
                        wasLed != null
                            ? $"{wasLed.LabelShortCap}'s crew has broken up."
                            : "A crew here has broken up.",
                        wasLed != null ? (LookTargets)wasLed : LookTargets.Invalid,
                        MessageTypeDefOf.NeutralEvent);
                }
            }
            known = fresh;
        }
    }
}
