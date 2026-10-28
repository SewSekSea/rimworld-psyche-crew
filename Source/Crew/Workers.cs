using RimWorld;
using Verse;

namespace Crew
{
    /// <summary>Having people. A Sociopath is surrounded by them and belongs to none of them.</summary>
    public class ThoughtWorker_InACrew : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!CR.Counts(p) || CR.FeelsNoLoyalty(p))
            {
                return ThoughtState.Inactive;
            }
            CrewGroup crew = GameComponent_Crews.CrewOf(p);
            if (crew == null)
            {
                return ThoughtState.Inactive;
            }
            return crew.members.Count >= 4 ? ThoughtState.ActiveAtStage(1) : ThoughtState.ActiveAtStage(0);
        }
    }

    /// <summary>
    /// Everyone else sorted themselves out and you did not. Only bites once there are crews
    /// to be left out of, and never bothers a Sociopath.
    /// </summary>
    public class ThoughtWorker_OnMyOwn : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!CR.Counts(p) || CR.FeelsNoLoyalty(p) || !GameComponent_Crews.AnyCrews)
            {
                return ThoughtState.Inactive;
            }
            return GameComponent_Crews.CrewOf(p) == null ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }

    /// <summary>
    /// Running with the wrong crowd. A Machiavellian keeps every door open and never holds this
    /// against anyone, which is exactly how they end up useful to both sides.
    /// </summary>
    public class ThoughtWorker_RunsWithThem : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (!Tuning.Rivalries || !CR.Counts(p) || other == null || CR.AboveTheFray(p))
            {
                return ThoughtState.Inactive;
            }
            return GameComponent_Crews.AreRivals(p, other)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }
}
