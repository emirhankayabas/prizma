using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Looks the player unlocks with stars from the adventure mode. A theme is the whole mood of
    /// the screen — the ground, the glow, every surface, the accent on the main buttons and the
    /// blocks — not just the block colours.
    ///
    /// It used to be the blocks alone, and the five palettes shared one hue order with only
    /// lightness moved: the nearest pair was ΔE 15 apart per colour, with one colour at 6. Picked
    /// on a menu that has no blocks and judged a run later from memory, a new theme read as
    /// nothing having changed at all. Now the ground alone is ΔE 30-85 between themes.
    ///
    /// What does not change is what means something in play: gold, mint, the prism cyan, crystals,
    /// ice and the line preview tints. Those stay the product's own, so a theme never changes how
    /// the game reads.
    ///
    /// Every palette has the same number of colours and keeps them apart (ΔE ≥ 22 between any two):
    /// single-colour lines score a bonus, so two colours that look alike would be a trap, not a
    /// style. Values were checked for contrast before landing here — white text ≥ 7:1 on surfaces,
    /// ≥ 3:1 on the accent, gold and mint ≥ 4.5:1 on inset chips, blocks ≥ 3:1 on the board.
    /// </summary>
    public static class Themes
    {
        /// <summary>The colours of one theme, written as hex so they read like a design spec.</summary>
        public struct Mood
        {
            public string BgTop, BgBottom, BgGlow, BgVignette, TextOnGround;
            public string Surface, SurfaceHigh, BoardSurface, SurfaceButton, SurfaceTrack, SurfaceInset, SurfaceLeader;
            public string AccentA, AccentB, Scrim;
            public float GridAlpha, GlowAlpha;
        }

        public sealed class Theme
        {
            public readonly string Name;
            public readonly int StarsToUnlock;
            public readonly Color[] Blocks;

            public readonly Color BgTop, BgBottom, BgGlow, BgVignette, TextOnGround;
            public readonly Color Surface, SurfaceHigh, BoardSurface, SurfaceButton, SurfaceTrack, SurfaceInset, SurfaceLeader;
            public readonly Color AccentA, AccentB, Scrim;
            public readonly float GridAlpha, GlowAlpha;

            public Theme(string name, int stars, Mood mood, params string[] blocks)
            {
                Name = name;
                StarsToUnlock = stars;

                Blocks = new Color[blocks.Length];
                for (int i = 0; i < blocks.Length; i++) Blocks[i] = Hex(blocks[i]);

                BgTop = Hex(mood.BgTop);
                BgBottom = Hex(mood.BgBottom);
                BgGlow = Hex(mood.BgGlow);
                BgVignette = Hex(mood.BgVignette);
                TextOnGround = Hex(mood.TextOnGround);
                Surface = Hex(mood.Surface);
                SurfaceHigh = Hex(mood.SurfaceHigh);
                BoardSurface = Hex(mood.BoardSurface);
                SurfaceButton = Hex(mood.SurfaceButton);
                SurfaceTrack = Hex(mood.SurfaceTrack);
                SurfaceInset = Hex(mood.SurfaceInset);
                SurfaceLeader = Hex(mood.SurfaceLeader);
                AccentA = Hex(mood.AccentA);
                AccentB = Hex(mood.AccentB);
                Scrim = Hex(mood.Scrim);
                GridAlpha = mood.GridAlpha;
                GlowAlpha = mood.GlowAlpha;
            }

            static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static readonly Theme[] All =
        {
            // Daytime sky. The product's own look: the default, and the app icon.
            new Theme("Prizma", 0, new Mood
            {
                BgTop = "#57A6F5",
                BgBottom = "#3A6BDB",
                BgGlow = "#8CCBFF",
                BgVignette = "#1B3F8F",
                TextOnGround = "#0C1F52",
                Surface = "#26305E",
                SurfaceHigh = "#2E3A6E",
                BoardSurface = "#1E2650",
                SurfaceButton = "#38447F",
                SurfaceTrack = "#2C3768",
                SurfaceInset = "#28315C",
                SurfaceLeader = "#4A3C1E",
                AccentA = "#8B5CFF",
                AccentB = "#5F32E8",
                Scrim = "#0C1636",
                GridAlpha = 0.16f,
                GlowAlpha = 0.42f,
            },
                "#FF5A7E", "#FFB13C", "#38D39F", "#3AA9FF", "#9B6BFF", "#FF7C4D", "#26C9C3"),

            // Lavender morning. Light, soft blocks that stand out on a deep plum board.
            new Theme("Pastel", 6, new Mood
            {
                BgTop = "#D3A3EA",
                BgBottom = "#9A7BD6",
                BgGlow = "#FFD9EC",
                BgVignette = "#5B45A8",
                TextOnGround = "#3E2C85",
                Surface = "#3A2E66",
                SurfaceHigh = "#44376F",
                BoardSurface = "#302658",
                SurfaceButton = "#54468A",
                SurfaceTrack = "#403370",
                SurfaceInset = "#3A2E63",
                SurfaceLeader = "#5C4526",
                AccentA = "#E0557C",
                AccentB = "#B8385E",
                Scrim = "#231845",
                GridAlpha = 0.18f,
                GlowAlpha = 0.45f,
            },
                "#FF9FBF", "#FFD98A", "#B4EC8E", "#7FB6FF", "#DCC8FF", "#FFB896", "#7FE0E6"),

            // Night. The one dark ground: magenta light, electric blocks.
            new Theme("Neon", 20, new Mood
            {
                BgTop = "#2A1760",
                BgBottom = "#0C0828",
                BgGlow = "#FF2E9A",
                BgVignette = "#04020E",
                TextOnGround = "#A796F0",
                Surface = "#1C1545",
                SurfaceHigh = "#241B54",
                BoardSurface = "#120D33",
                SurfaceButton = "#2F2466",
                SurfaceTrack = "#261D58",
                SurfaceInset = "#1B1446",
                SurfaceLeader = "#4E2E12",
                AccentA = "#FF2E8C",
                AccentB = "#C0106A",
                Scrim = "#05030F",
                GridAlpha = 0.1f,
                GlowAlpha = 0.3f,
            },
                "#FF2E88", "#FFE81A", "#1CFF9A", "#1AB8FF", "#B24BFF", "#FF7A1A", "#1AFFE4"),

            // Emerald. Deep green ground, ruby accent, gemstone blocks. No white stone: it would read as ice.
            new Theme("Mücevher", 45, new Mood
            {
                BgTop = "#1C8C77",
                BgBottom = "#0A4750",
                BgGlow = "#6BE8C4",
                BgVignette = "#042629",
                TextOnGround = "#C4F2E3",
                Surface = "#10383B",
                SurfaceHigh = "#154449",
                BoardSurface = "#0B2A2E",
                SurfaceButton = "#22606A",
                SurfaceTrack = "#17474C",
                SurfaceInset = "#103438",
                SurfaceLeader = "#5A4418",
                AccentA = "#E0336F",
                AccentB = "#A81A4E",
                Scrim = "#031A1D",
                GridAlpha = 0.12f,
                GlowAlpha = 0.34f,
            },
                "#F0245F", "#F7B500", "#98D83E", "#3380FF", "#A461FF", "#EE6A2B", "#48DCC8"),

            // Candy. Warm pink ground, grape accent, berry surfaces.
            new Theme("Şeker", 80, new Mood
            {
                BgTop = "#FF93C2",
                BgBottom = "#F27BAE",
                BgGlow = "#FFE0EE",
                BgVignette = "#8C2459",
                TextOnGround = "#6A1545",
                Surface = "#4A1C45",
                SurfaceHigh = "#562150",
                BoardSurface = "#3D1739",
                SurfaceButton = "#682C5F",
                SurfaceTrack = "#52204C",
                SurfaceInset = "#461A42",
                SurfaceLeader = "#5E3A1C",
                AccentA = "#7C44F2",
                AccentB = "#5A25C9",
                Scrim = "#3A0F2E",
                GridAlpha = 0.18f,
                GlowAlpha = 0.45f,
            },
                "#E8176E", "#FFD84A", "#6BEFA8", "#56B8FF", "#B383FF", "#FF904A", "#3FE3D6")
        };

        /// <summary>
        /// The product's own look. Anything that is not the player's screen — the app icon, the
        /// splash — uses this, never <see cref="Current"/>: the icon is baked in the Editor, where
        /// the current theme is whatever the developer last picked while testing.
        /// </summary>
        public static Theme Default => All[0];

        public static Theme Current => All[Mathf.Clamp(Progress.Theme, 0, All.Length - 1)];

        public static bool IsUnlocked(int index) =>
            index >= 0 && index < All.Length && Progress.TotalStars >= All[index].StarsToUnlock;
    }
}
