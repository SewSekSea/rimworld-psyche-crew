using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Crew
{
    /// <summary>
    /// Showing the player what the mod already knows.
    ///
    /// Crews, rivalries and leaders were all worked out from the first version onwards and none of
    /// it was ever displayed — the only way to learn any of it was to catch a message as it went
    /// past. A subscriber pointed this out: "unless you're really paying attention to the messages,
    /// you'll kinda have no idea."
    ///
    /// Nothing here changes how a crew forms or what it does. It is display only.
    /// </summary>
    public static class CrewUI
    {
        /// <summary>
        /// The colour a crew is drawn in.
        ///
        /// Taken from the longest-standing member rather than from the leader or from the crew's
        /// position in the list. Position changes whenever any other crew forms or dissolves, and
        /// the leader changes whenever opinion shifts — a crew that flips colour because somebody
        /// else became the one the others look to is the exact confusion the colour exists to
        /// prevent. It is the same crew; it should look like the same crew.
        /// </summary>
        public static Color ColourFor(CrewGroup crew)
        {
            if (crew == null || crew.members.Count == 0)
            {
                return Color.grey;
            }
            int anchor = int.MaxValue;
            for (int i = 0; i < crew.members.Count; i++)
            {
                Pawn m = crew.members[i];
                if (m != null && m.thingIDNumber < anchor)
                {
                    anchor = m.thingIDNumber;
                }
            }
            if (anchor == int.MaxValue)
            {
                return Color.grey;
            }
            float h = (anchor * 0.6180339887f) % 1f;
            return Color.HSVToRGB(h, 0.45f, 0.95f);
        }

        /// <summary>Who this colonist runs with, as one line for the inspect pane.</summary>
        public static string InspectLineFor(Pawn p)
        {
            if (p == null || !p.IsColonist || p.Dead)
            {
                return null;
            }
            CrewGroup crew = GameComponent_Crews.CrewOf(p);
            if (crew == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("Crew: ").Append(crew.Label);
            if (crew.leader == p)
            {
                sb.Append(" (they lead it)");
            }

            List<Pawn> others = new List<Pawn>();
            for (int i = 0; i < crew.members.Count; i++)
            {
                if (crew.members[i] != p)
                {
                    others.Add(crew.members[i]);
                }
            }
            if (others.Count > 0)
            {
                sb.Append(" — with ");
                for (int i = 0; i < others.Count && i < 4; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(others[i].LabelShort);
                }
                if (others.Count > 4)
                {
                    sb.Append(" and ").Append(others.Count - 4).Append(" more");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// The overview: every crew, who is in it, and who they are at odds with.
    /// </summary>
    public class MainTabWindow_Crews : MainTabWindow
    {
        private Vector2 scroll = Vector2.zero;

        /// <summary>
        /// Sized to what is actually in it.
        ///
        /// A fixed 620x480 panel holding two lines of "nobody has formed a crew yet" reads as
        /// something that failed to load rather than as an empty list, so the window shrinks when
        /// there is nothing to show and grows with the crews, up to a sensible ceiling.
        /// </summary>
        public override Vector2 RequestedTabSize
        {
            get
            {
                List<CrewGroup> crews = GameComponent_Crews.AllCrews;
                if (crews == null || crews.Count == 0)
                {
                    return new Vector2(560f, 150f);
                }
                float needed = 24f;
                for (int i = 0; i < crews.Count; i++)
                {
                    needed += 30f + (crews[i].members.Count * 22f) + 30f;
                }
                return new Vector2(620f, Mathf.Clamp(needed, 180f, 620f));
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            List<CrewGroup> crews = GameComponent_Crews.AllCrews;

            Text.Font = GameFont.Small;
            if (crews == null || crews.Count == 0)
            {
                Widgets.Label(new Rect(rect.x, rect.y + 8f, rect.width, 120f),
                    "Nobody here has formed a crew yet.\n\n" +
                    "Crews are worked out from who actually gets on with whom. They appear on their " +
                    "own once enough colonists rate each other, and you cannot assign them.");
                return;
            }

            float height = 0f;
            for (int i = 0; i < crews.Count; i++)
            {
                height += 34f + (crews[i].members.Count * 24f) + 26f;
            }

            Rect view = new Rect(0f, 0f, rect.width - 20f, height);
            Widgets.BeginScrollView(rect, ref scroll, view);
            float y = 0f;

            for (int i = 0; i < crews.Count; i++)
            {
                CrewGroup crew = crews[i];
                Color colour = CrewUI.ColourFor(crew);

                // Heading: the crew's name, in its own colour, with a swatch.
                Widgets.DrawBoxSolid(new Rect(0f, y + 6f, 6f, 20f), colour);
                Rect head = new Rect(14f, y, view.width - 14f, 30f);
                Text.Font = GameFont.Small;
                GUI.color = colour;
                Widgets.Label(head, crew.Label + "  (" + crew.members.Count + ")");
                GUI.color = Color.white;
                y += 30f;

                // Members, the leader first and marked.
                for (int m = 0; m < crew.members.Count; m++)
                {
                    Pawn member = crew.members[m];
                    Rect row = new Rect(22f, y, view.width - 22f, 22f);
                    if (Mouse.IsOver(row))
                    {
                        Widgets.DrawHighlight(row);
                    }
                    string name = member.LabelShortCap;
                    if (member == crew.leader)
                    {
                        name += "  — the one the others look to";
                    }
                    Widgets.Label(row, name);
                    if (Widgets.ButtonInvisible(row))
                    {
                        // Jump to them, the same as clicking a name anywhere else in the game.
                        CameraJumper.TryJumpAndSelect(member);
                    }
                    y += 22f;
                }

                // Who they are at odds with.
                string rivals = RivalsOf(crew, crews);
                Rect rivalRow = new Rect(22f, y, view.width - 22f, 22f);
                GUI.color = rivals == null ? new Color(1f, 1f, 1f, 0.45f) : new Color(0.95f, 0.6f, 0.55f);
                Widgets.Label(rivalRow, rivals ?? "No rivals.");
                GUI.color = Color.white;
                y += 30f;
            }

            Widgets.EndScrollView();
        }

        /// <summary>Which other crews this one is at odds with, named.</summary>
        private static string RivalsOf(CrewGroup crew, List<CrewGroup> all)
        {
            if (crew.members.Count == 0)
            {
                return null;
            }
            List<string> names = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == crew || all[i].members.Count == 0)
                {
                    continue;
                }
                if (GameComponent_Crews.AreRivals(crew.members[0], all[i].members[0]))
                {
                    names.Add(all[i].Label);
                }
            }
            return names.Count == 0 ? null : "At odds with " + string.Join(", ", names) + ".";
        }
    }
}
