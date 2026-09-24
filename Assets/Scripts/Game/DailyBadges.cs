using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The daily puzzle's badges. Like <see cref="Achievements"/>, nothing is stored: a badge is
    /// earned when the number behind it is reached, so it can never disagree with the record and
    /// an old save arrives with everything it has already earned.
    ///
    /// Most of them are about coming back — the streak, the count of solves, a whole week — and a
    /// few are about how a solve went: first try, three stars, under a minute, the Sunday puzzle.
    /// Each is worth one star, the currency themes are bought with.
    /// </summary>
    public static class DailyBadges
    {
        public sealed class Badge
        {
            public readonly string Id;
            public readonly Func<Sprite> Icon;
            public readonly Func<Color> Tint;
            public readonly Func<long> Value;
            public readonly long Target;

            public Badge(string id, Func<Sprite> icon, Func<Color> tint, Func<long> value, long target)
            {
                Id = id;
                Icon = icon;
                Tint = tint;
                Value = value;
                Target = target;
            }

            public bool Earned => Value() >= Target;

            /// <summary>0..1 of the way there, for the badge detail line.</summary>
            public float Progress => Mathf.Clamp01(Value() / (float)Math.Max(1, Target));

            public string Name => Str.BadgeName(Id);
            public string Goal => Str.BadgeGoal(Id, Target);
        }

        // Colour says how hard a badge is to come by: mint for the first steps, gold for a habit,
        // rose for a long one, the prism for the rarest.
        static Color Tier1 => Design.Mint;
        static Color Tier2 => Design.Gold;
        static Color Tier3 => Design.PreviewTint(3);
        static Color Tier4 => Design.Prism;

        static long Fast => Progress.DailyFastest > 0f && Progress.DailyFastest <= 60f ? 1 : 0;

        public static readonly Badge[] All =
        {
            new Badge("first", () => Icons.Check, () => Tier1, () => Progress.DailySolves, 1),
            new Badge("streak3", () => Icons.Flame, () => Tier1, () => Progress.DailyBestStreak, 3),
            new Badge("streak7", () => Icons.Flame, () => Tier2, () => Progress.DailyBestStreak, 7),
            new Badge("streak14", () => Icons.Flame, () => Tier2, () => Progress.DailyBestStreak, 14),
            new Badge("streak30", () => Icons.Flame, () => Tier3, () => Progress.DailyBestStreak, 30),
            new Badge("streak100", () => Icons.Flame, () => Tier4, () => Progress.DailyBestStreak, 100),
            new Badge("solves10", () => Icons.Calendar, () => Tier1, () => Progress.DailySolves, 10),
            new Badge("solves50", () => Icons.Calendar, () => Tier3, () => Progress.DailySolves, 50),
            new Badge("firsttry", () => Icons.Target, () => Tier1, () => Progress.DailyFirstTry, 1),
            new Badge("firsttry10", () => Icons.Target, () => Tier3, () => Progress.DailyFirstTry, 10),
            new Badge("stars3", () => Icons.Star, () => Tier2, () => Progress.DailyThreeStars, 1),
            new Badge("speed", () => Icons.Bolt, () => Tier2, () => Fast, 1),
            new Badge("sunday", () => Icons.Crown, () => Tier3, () => Progress.DailySundays, 1),
            new Badge("week", () => Icons.Crown, () => Tier4, () => Progress.DailyPerfectWeeks, 1),
            new Badge("early", () => Icons.Sun, () => Tier2, () => Progress.DailyEarly, 1),
            new Badge("night", () => Icons.Moon, () => Tier2, () => Progress.DailyLate, 1),
        };

        public static int EarnedCount
        {
            get
            {
                int n = 0;
                foreach (var badge in All) if (badge.Earned) n++;
                return n;
            }
        }

        public static int EarnedStars => EarnedCount;

        /// <summary>Which badges are earned right now, to compare before and after a solve.</summary>
        public static bool[] Snapshot()
        {
            var earned = new bool[All.Length];
            for (int i = 0; i < All.Length; i++) earned[i] = All[i].Earned;
            return earned;
        }

        /// <summary>Badges earned since <paramref name="before"/> was taken.</summary>
        public static List<Badge> NewSince(bool[] before)
        {
            var list = new List<Badge>();
            for (int i = 0; i < All.Length; i++)
                if (All[i].Earned && (before == null || i >= before.Length || !before[i]))
                    list.Add(All[i]);
            return list;
        }

        /// <summary>
        /// The badge to show first on the badge page: the one closest to being earned. What the
        /// player can reach next is more interesting than what they already have.
        /// </summary>
        public static int Closest()
        {
            int best = -1;
            float bestProgress = -1f;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Earned) continue;
                float p = All[i].Progress;
                if (p > bestProgress) { bestProgress = p; best = i; }
            }
            return best < 0 ? 0 : best;
        }
    }
}
