using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Block palettes the player unlocks with stars from the adventure mode. Only the blocks
    /// change — the ground, the board and the type stay the product's own — so every theme keeps
    /// the contrast the board was designed around.
    ///
    /// Every palette has the same number of colours and keeps them far apart in hue: single-colour
    /// lines score a bonus, so two colours that look alike would be a trap, not a style.
    /// </summary>
    public static class Themes
    {
        public sealed class Theme
        {
            public readonly string Name;
            public readonly int StarsToUnlock;
            public readonly Color[] Blocks;

            public Theme(string name, int stars, params string[] hex)
            {
                Name = name;
                StarsToUnlock = stars;
                Blocks = new Color[hex.Length];
                for (int i = 0; i < hex.Length; i++)
                    Blocks[i] = ColorUtility.TryParseHtmlString(hex[i], out var c) ? c : Color.magenta;
            }
        }

        public static readonly Theme[] All =
        {
            new Theme("Prizma", 0, "#FF5A7E", "#FFB13C", "#38D39F", "#3AA9FF", "#9B6BFF", "#FF7C4D", "#26C9C3"),
            new Theme("Pastel", 6, "#F59AB2", "#FFD58C", "#8FE3BE", "#8CC8FF", "#C1A6FF", "#FFAF94", "#7FDCD6"),
            new Theme("Neon", 20, "#FF2E88", "#FFE01A", "#1CFF9A", "#1AB8FF", "#B24BFF", "#FF7A1A", "#1AFFE4"),
            new Theme("Mücevher", 45, "#E0115F", "#F2A900", "#16B36B", "#1F6FEB", "#8A3FFC", "#D2491E", "#0FA3A3"),
            new Theme("Şeker", 80, "#FF6FB5", "#FFC93C", "#6EE7B7", "#60A5FA", "#C084FC", "#FB923C", "#5EEAD4")
        };

        public static Theme Current => All[Mathf.Clamp(Progress.Theme, 0, All.Length - 1)];

        public static bool IsUnlocked(int index) =>
            index >= 0 && index < All.Length && Progress.TotalStars >= All[index].StarsToUnlock;
    }
}
