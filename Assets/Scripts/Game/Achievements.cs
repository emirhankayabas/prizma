using System;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Tiered goals over the lifetime statistics <see cref="Progress"/> already keeps. Nothing is
    /// stored: a tier is reached when its number is, so an achievement can never disagree with the
    /// statistics page, and older saves arrive with everything they have already earned.
    ///
    /// Each tier pays stars (1, 2, 3), the same stars levels give. That ties achievements into the
    /// one reward the game already has — themes — instead of inventing a second currency, and lets
    /// a player who only plays classic or the daily still unlock them.
    /// </summary>
    public static class Achievements
    {
        public sealed class Entry
        {
            public readonly string Id;
            public readonly Func<Sprite> Icon;
            public readonly Func<long> Value;
            public readonly long[] Tiers;

            public Entry(string id, Func<Sprite> icon, Func<long> value, params long[] tiers)
            {
                Id = id;
                Icon = icon;
                Value = value;
                Tiers = tiers;
            }

            public string Name => Str.AchievementName(Id);

            /// <summary>Tiers reached, 0 to <see cref="Tiers"/>.Length.</summary>
            public int Reached
            {
                get
                {
                    long v = Value();
                    int n = 0;
                    while (n < Tiers.Length && v >= Tiers[n]) n++;
                    return n;
                }
            }

            /// <summary>The next target, or the last one when everything is done.</summary>
            public long NextTarget => Tiers[Math.Min(Reached, Tiers.Length - 1)];

            public int Stars
            {
                get
                {
                    int reached = Reached, stars = 0;
                    for (int i = 0; i < reached; i++) stars += StarsForTier(i);
                    return stars;
                }
            }
        }

        public static int StarsForTier(int tier) => tier + 1;

        public static readonly Entry[] All =
        {
            new Entry("games", () => Icons.Play, () => Progress.Games, 10, 50, 250),
            new Entry("lines", () => Icons.Rows, () => Progress.TotalLines, 100, 1000, 5000),
            new Entry("score", () => Icons.Gem, () => HighScores.Best, 5000, 20000, 50000),
            new Entry("combo", () => Icons.Flame, () => Progress.BestCombo, 3, 5, 8),
            new Entry("mono", () => Icons.Palette, () => Progress.MonoLinesTotal, 5, 50, 250),
            new Entry("perfect", () => Icons.Star, () => Progress.PerfectClears, 1, 10, 50),
            new Entry("gems", () => Art.Crystal, () => Progress.GemsCollected, 25, 250, 1000),
            new Entry("powers", () => Icons.Bomb, () => Progress.PowersUsed, 10, 50, 200),
            new Entry("levels", () => Icons.Flag, () => Progress.LevelsCompleted, 10, 50, 100),
            new Entry("streak", () => Icons.Calendar, () => Progress.DailyBestStreak, 3, 7, 30),
            new Entry("months", () => Icons.Check, () => Progress.CompletedMonths, 1, 3, 12),
        };

        public static int EarnedStars
        {
            get
            {
                int total = 0;
                foreach (var entry in All) total += entry.Stars;
                return total;
            }
        }
    }
}
