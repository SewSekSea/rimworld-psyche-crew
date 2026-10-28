using UnityEngine;
using Verse;

namespace Crew
{
    public class CrewSettings : ModSettings
    {
        public int crewOpinion = 30;
        public int rivalOpinion = -15;
        public int minCrewSize = 3;
        public float rivalFightFactor = 3f;

        public bool rivalries = true;
        public bool backYourOwn = true;
        public bool disrespect = true;

        public override void ExposeData()
        {
            CrewSettings d = new CrewSettings();
            Scribe_Values.Look(ref crewOpinion, "crewOpinion", d.crewOpinion);
            Scribe_Values.Look(ref rivalOpinion, "rivalOpinion", d.rivalOpinion);
            Scribe_Values.Look(ref minCrewSize, "minCrewSize", d.minCrewSize);
            Scribe_Values.Look(ref rivalFightFactor, "rivalFightFactor", d.rivalFightFactor);
            Scribe_Values.Look(ref rivalries, "rivalries", true);
            Scribe_Values.Look(ref backYourOwn, "backYourOwn", true);
            Scribe_Values.Look(ref disrespect, "disrespect", true);
        }

        public void Reset()
        {
            CrewSettings d = new CrewSettings();
            crewOpinion = d.crewOpinion;
            rivalOpinion = d.rivalOpinion;
            minCrewSize = d.minCrewSize;
            rivalFightFactor = d.rivalFightFactor;
            rivalries = backYourOwn = disrespect = true;
        }
    }

    public class CrewMod : Mod
    {
        public static CrewSettings Settings = new CrewSettings();

        public CrewMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CrewSettings>();
        }

        public override string SettingsCategory() => "Crew";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            CrewSettings s = Settings;
            Listing_Standard list = new Listing_Standard { ColumnWidth = (inRect.width - 34f) / 2f };
            list.Begin(inRect);

            list.Label("How crews form");
            s.crewOpinion = Mathf.RoundToInt(list.SliderLabeled($"They run together at opinion {s.crewOpinion}+",
                s.crewOpinion, 10f, 80f, 0.6f,
                "Two colonists have to rate each other at least this highly, both ways, before they count as running together. Lower this and the colony splits into crews readily; raise it and only real friendships count."));
            s.minCrewSize = Mathf.RoundToInt(list.SliderLabeled($"A crew is {s.minCrewSize} people or more",
                s.minCrewSize, 2f, 6f, 0.6f));
            s.rivalOpinion = Mathf.RoundToInt(list.SliderLabeled($"Crews turn rival at opinion {s.rivalOpinion} or lower",
                s.rivalOpinion, -60f, 0f, 0.6f,
                "Once enough people across two crews dislike each other this much, the two crews are at odds as a whole."));
            s.rivalFightFactor = list.SliderLabeled($"Rivals brawl {s.rivalFightFactor:0.0}x as often",
                s.rivalFightFactor, 1f, 8f, 0.6f,
                "How much likelier a social fight is between rivals. This only changes how likely a quarrel is. It never changes who anyone attacks.");

            list.NewColumn();
            list.Label("Optional behaviours");
            list.CheckboxLabeled("Rivalries", ref s.rivalries,
                "Crews that dislike each other become rivals, and running with the wrong crowd counts against a person on its own.");
            list.CheckboxLabeled("Backing your own", ref s.backYourOwn,
                "Hurt someone and their whole crew takes it personally, not only them.");
            list.CheckboxLabeled("Disrespect", ref s.disrespect,
                "An insult from a rival is remembered as disrespect and does not fade on its own. Winning a social fight with them settles it.");

            list.Gap(12f);
            list.Label("Crews are worked out from who actually gets on with whom. You cannot assign them, and there is no switch to stop them forming.");

            list.Gap(24f);
            if (list.ButtonText("Reset to defaults"))
            {
                s.Reset();
            }
            list.End();
        }
    }
}
