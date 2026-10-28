using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Crew
{
    /// <summary>
    /// Insults between rivals are not ordinary insults. They land as disrespect, which sits
    /// there until it is answered rather than quietly expiring like everything else.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Patch_TryInteractWith
    {
        public static void Postfix(bool __result, Pawn ___pawn, Pawn recipient, InteractionDef intDef)
        {
            if (!__result || !Tuning.Disrespect || intDef == null || recipient?.needs?.mood == null)
            {
                return;
            }
            if (intDef != InteractionDefOf.Insult && intDef.defName != "Slight")
            {
                return;
            }
            if (!GameComponent_Crews.AreRivals(___pawn, recipient))
            {
                return;
            }
            recipient.needs.mood.thoughts.memories.TryGainMemory(CR_DefOf.CR_Disrespected, ___pawn);
            if (PawnUtility.ShouldSendNotificationAbout(recipient))
            {
                Messages.Message(
                    $"{___pawn.LabelShort} disrespected {recipient.LabelShort} in front of people.",
                    recipient, MessageTypeDefOf.NegativeEvent);
            }
        }
    }

    /// <summary>
    /// Rivals are far likelier to end up in a social fight. This only changes how likely a
    /// quarrel is, never who anyone attacks or what they do afterwards.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.SocialFightChance))]
    public static class Patch_SocialFightChance
    {
        public static void Postfix(Pawn ___pawn, Pawn initiator, ref float __result)
        {
            if (__result <= 0f || !Tuning.Rivalries)
            {
                return;
            }
            if (GameComponent_Crews.AreRivals(initiator, ___pawn))
            {
                __result *= Tuning.RivalFightFactor;
            }
        }
    }

    /// <summary>
    /// Settling it. Coming out of a social fight with someone who disrespected you clears the
    /// account, and everyone involved knows it was dealt with.
    /// </summary>
    [HarmonyPatch(typeof(MentalState), nameof(MentalState.RecoverFromState))]
    public static class Patch_RecoverFromState
    {
        public static void Postfix(MentalState __instance)
        {
            if (!Tuning.Disrespect || !(__instance is MentalState_SocialFighting fight))
            {
                return;
            }
            Pawn pawn = fight.pawn;
            Pawn against = fight.otherPawn;
            if (pawn?.needs?.mood == null || against == null)
            {
                return;
            }
            MemoryThoughtHandler memories = pawn.needs.mood.thoughts.memories;
            if (!CR.HasMemoryAbout(pawn, CR_DefOf.CR_Disrespected, against))
            {
                return;
            }
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(CR_DefOf.CR_Disrespected, against);
            memories.TryGainMemory(CR_DefOf.CR_Settled);
        }
    }

    /// <summary>
    /// Backing your own. Hurt someone and their whole crew takes it personally, not just them.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_PostApplyDamage
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (!Tuning.BackYourOwn || totalDamageDealt <= 0f)
            {
                return;
            }
            if (!(dinfo.Instigator is Pawn attacker) || attacker == __instance)
            {
                return;
            }
            if (!CR.Counts(__instance) || !CR.Counts(attacker))
            {
                return;
            }
            CrewGroup crew = GameComponent_Crews.CrewOf(__instance);
            if (crew == null || crew.Contains(attacker))
            {
                return;
            }
            for (int i = 0; i < crew.members.Count; i++)
            {
                Pawn mate = crew.members[i];
                if (mate == __instance || mate == attacker || mate.needs?.mood == null)
                {
                    continue;
                }
                mate.needs.mood.thoughts.memories.TryGainMemory(CR_DefOf.CR_TouchedOneOfOurs, attacker);
            }
        }
    }

    /// <summary>
    /// Put the colonist's crew into the inspect pane, under their name.
    ///
    /// This is the cheapest possible answer to "who is in a crew with who" -- it appears in the
    /// place a player is already looking when they click somebody, and costs a single line.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_GetInspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            string line = CrewUI.InspectLineFor(__instance);
            if (line.NullOrEmpty())
            {
                return;
            }
            __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
        }
    }
}
