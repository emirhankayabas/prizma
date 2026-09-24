using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The light a run is played in. As a classic run climbs, the ground's light walks the prism's
    /// spectrum — rose, amber, lime, aqua, azure, violet, and finally the whole prism turning —
    /// the way Lumines moves through its skins: the player reads how far they have come from the
    /// colour of the room, with nothing written on the screen.
    ///
    /// Only light changes: the glow behind the board, the grid, the board's resting rim. The
    /// blocks, the surfaces and every colour that means something in play (gold, mint, the prism
    /// cyan, crystals, ice, the preview tints) stay exactly as the theme has them — the contrast
    /// checks in Tools/theme-check.py still hold, and a single-colour line is still a single colour.
    ///
    /// A level or the daily has a goal instead of an open score, so its light follows the goal:
    /// cool at the start, warming as the goal comes within reach.
    /// </summary>
    public static class Spectrum
    {
        /// <summary>Stage colours, from the first step up. Stage 0 is the theme's own light, untinted.</summary>
        static readonly Color[] Stages =
        {
            Hex("#FF5C8A"), // rose
            Hex("#FFB23E"), // amber
            Hex("#9BE15D"), // lime
            Hex("#3FE0D0"), // aqua
            Hex("#4DA8FF"), // azure
            Hex("#A77BFF"), // violet
        };

        /// <summary>
        /// Scores that open each stage in a classic run. Spaced against the measured runs
        /// (CoreHarness balance): a struggling player's run ends around 4-5k and sees one or two,
        /// a casual one around 7.5k sees three, a good one around 24k sees six. The last — the
        /// turning prism — is for the runs people remember.
        /// </summary>
        static readonly int[] Thresholds = { 1500, 4000, 8000, 13000, 20000, 30000, 45000 };

        public const int MaxStage = 7;

        /// <summary>The goal modes' path: cool to warm as the goal nears.</summary>
        static readonly Color[] GoalPath = { Hex("#4DA8FF"), Hex("#3FE0D0"), Hex("#9BE15D"), Hex("#FFB23E") };

        public static int StageForScore(int score)
        {
            int stage = 0;
            while (stage < Thresholds.Length && score >= Thresholds[stage]) stage++;
            return stage;
        }

        /// <summary>Score needed for a stage; stage 0 is free.</summary>
        public static int ThresholdFor(int stage) => stage <= 0 ? 0 : Thresholds[Mathf.Min(stage, Thresholds.Length) - 1];

        /// <summary>
        /// The light of a classic stage. 0 is none (null); the top stage turns through the whole
        /// spectrum, so its colour depends on the time.
        /// </summary>
        public static Color? ColorFor(int stage, float time)
        {
            if (stage <= 0) return null;
            if (stage <= Stages.Length) return Stages[stage - 1];
            return Prism(time * 0.12f);
        }

        /// <summary>A goal run's light: 0..1 of the goal done. Nothing until a quarter is.</summary>
        public static Color? ColorForGoal(float fraction)
        {
            int step = Mathf.FloorToInt(Mathf.Clamp01(fraction) * GoalPath.Length);
            if (step <= 0) return null;
            return GoalPath[Mathf.Min(step, GoalPath.Length) - 1];
        }

        /// <summary>The spectrum as one continuous loop, 0..1 around.</summary>
        public static Color Prism(float t)
        {
            t = Mathf.Repeat(t, 1f) * Stages.Length;
            int a = Mathf.FloorToInt(t) % Stages.Length;
            int b = (a + 1) % Stages.Length;
            return Color.Lerp(Stages[a], Stages[b], t - Mathf.Floor(t));
        }

        /// <summary>
        /// The six letters of the title light one by one as the player's best stage rises —
        /// the home screen remembering how far they have ever got. Stage seven lights them all,
        /// each its own colour of the spectrum.
        /// </summary>
        public static string TitleMarkup(string title, int bestStage)
        {
            if (bestStage <= 0) return title;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < title.Length; i++)
            {
                bool lit = i < bestStage || bestStage >= MaxStage;
                if (!lit)
                {
                    sb.Append(title[i]);
                    continue;
                }

                var colour = Stages[i % Stages.Length];
                // Lit, but mostly white: a letter that took the full stage colour would lose its
                // contrast on the brighter grounds. A third of the colour reads as a tint of light.
                var tinted = Color.Lerp(Color.white, colour, 0.55f);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(tinted)).Append('>').Append(title[i]).Append("</color>");
            }

            return sb.ToString();
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
