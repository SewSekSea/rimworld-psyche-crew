using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Crew
{
    [DefOf]
    public static class CR_DefOf
    {
        public static ThoughtDef CR_TouchedOneOfOurs;
        public static ThoughtDef CR_Disrespected;
        public static ThoughtDef CR_Settled;
        public static MainButtonDef CR_Crews;

        static CR_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CR_DefOf));
        }
    }

    /// <summary>All the tuning numbers in one place. The ones players can change come from the settings menu.</summary>
    public static class Tuning
    {
        private static CrewSettings S => CrewMod.Settings;

        /// <summary>Mutual opinion at or above which two colonists count as running together.</summary>
        public static int CrewOpinion => S.crewOpinion;

        /// <summary>Mutual opinion at or below which two crews count as rivals.</summary>
        public static int RivalOpinion => S.rivalOpinion;

        /// <summary>Smallest group that counts as a crew at all.</summary>
        public static int MinCrewSize => S.minCrewSize;

        /// <summary>How much likelier a social fight is between two rivals.</summary>
        public static float RivalFightFactor => S.rivalFightFactor;

        public static bool Rivalries => S.rivalries;
        public static bool BackYourOwn => S.backYourOwn;
        public static bool Disrespect => S.disrespect;

        /// <summary>How often the crews are worked out again.</summary>
        public const int RecalcTicks = 2500;

        /// <summary>A crew needs this share of its members to accept a newcomer.</summary>
        public const float AcceptFraction = 0.6f;
    }

    [StaticConstructorOnStartup]
    public static class CR
    {
        /// <summary>Squad's battle bond, if that mod is installed. People who fought together run together.</summary>
        public static readonly ThoughtDef BattleBonded;

        /// <summary>The Sociopath trait, if installed. They run with a crew and never belong to one.</summary>
        public static readonly TraitDef Sociopath;

        /// <summary>The Machiavellian trait, if installed. They stay above the rivalries.</summary>
        public static readonly TraitDef Machiavellian;

        static CR()
        {
            BattleBonded = DefDatabase<ThoughtDef>.GetNamedSilentFail("SQ_BattleBonded");
            Sociopath = DefDatabase<TraitDef>.GetNamedSilentFail("SP_Sociopath");
            Machiavellian = DefDatabase<TraitDef>.GetNamedSilentFail("MAC_Machiavellian");

            new Harmony("crew.factions").PatchAll();
            Log.Message("[Crew] Loaded.");
        }

        public static bool Counts(Pawn p)
        {
            return p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike &&
                   p.needs?.mood != null && p.IsColonist && !p.DevelopmentalStage.Juvenile();
        }

        public static int Opinion(Pawn of, Pawn about)
        {
            if (of?.relations == null || about == null || of == about)
            {
                return 0;
            }
            return of.relations.OpinionOf(about);
        }

        /// <summary>Do these two rate each other enough to run together?</summary>
        public static bool RunsWith(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b)
            {
                return false;
            }
            // Squad: people who came through a fight together are crew material whatever else they think.
            if (BattleBonded != null && HasMemoryAbout(a, BattleBonded, b) && HasMemoryAbout(b, BattleBonded, a))
            {
                return true;
            }
            return Opinion(a, b) >= Tuning.CrewOpinion && Opinion(b, a) >= Tuning.CrewOpinion;
        }

        /// <summary>A Sociopath is happy to be surrounded by people and belongs to none of them.</summary>
        public static bool FeelsNoLoyalty(Pawn p)
        {
            return Sociopath != null && p?.story?.traits != null && p.story.traits.HasTrait(Sociopath);
        }

        /// <summary>A Machiavellian keeps every door open and takes no side.</summary>
        public static bool AboveTheFray(Pawn p)
        {
            return Machiavellian != null && p?.story?.traits != null && p.story.traits.HasTrait(Machiavellian);
        }

        public static bool HasMemoryAbout(Pawn pawn, ThoughtDef def, Pawn about)
        {
            if (def == null || about == null)
            {
                return false;
            }
            List<Thought_Memory> memories = pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null)
            {
                return false;
            }
            for (int i = 0; i < memories.Count; i++)
            {
                if (memories[i].def == def && memories[i].otherPawn == about)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
