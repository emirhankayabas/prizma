using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Builds the levels of the adventure mode from nothing but their number. No level is stored
    /// anywhere: the same number always produces the same board, goal and move budget, so the
    /// mode is endless and costs nothing to ship.
    ///
    /// The move budget is not guessed. Each draft is played through by a computer player several
    /// times, and the budget is set from how many moves it actually needed — with slack that
    /// shrinks as the levels get harder. A draft the computer cannot finish is thrown away.
    /// </summary>
    public static class LevelGenerator
    {
        public const int BoardSize = 8;

        // A level's playthroughs vary a lot — the dealer reacts to every placement — so the budget
        // is read off many runs and a high percentile, not a median of a few. With seven runs and
        // the median, neighbouring levels swung between 8% and 100% for the same player.
        const int SimulationRuns = 15;
        const float BudgetPercentile = 0.75f;
        const float MinWinShare = 0.6f;
        const int MoveCap = 140;
        const float BotSkill = 0.62f;
        const int MaxAttempts = 6;

        /// <summary>The longest move budget an adventure level may have before a lighter draft is tried.</summary>
        const int MaxAdventureBudget = 70;

        /// <summary>The opening levels are where a player decides whether the mode is for them.</summary>
        const int WarmupLevels = 10;
        const int WarmupBonusMoves = 6;

        /// <summary>0 for the first level, rising towards 1. Most of the climb happens in the first hundred.</summary>
        public static float Difficulty(int number) => 1f - (float)Math.Exp(-(Math.Max(1, number) - 1) / 45.0);

        // ------------------------------------------------------------------ the adventure's shape

        /// <summary>
        /// The adventure is a road of a hundred levels in ten worlds. Each world brings one new
        /// thing onto the board — its first level shows it on an easy board with no words — and
        /// ends on a very hard level, like the episodes of the saga games it borrows the road from.
        /// </summary>
        public const int LevelCount = 100;
        public const int WorldSize = 10;
        public const int WorldCount = LevelCount / WorldSize;

        /// <summary>0-based world of a level.</summary>
        public static int WorldOf(int number) => Math.Min(WorldCount - 1, (Math.Max(1, number) - 1) / WorldSize);

        /// <summary>1..10, the level's place inside its world.</summary>
        public static int PlaceInWorld(int number) => (Math.Max(1, number) - 1) % WorldSize + 1;

        // Where each obstacle and goal first appears. The first level of its world introduces it.
        public const int IceFrom = 6;
        public const int TilesFrom = 11;
        public const int StoneFrom = 21;
        public const int ShadeFrom = 31;
        public const int ColorsFrom = 41;
        public const int TimersFrom = 51;
        public const int DoubleIceFrom = 61;

        /// <summary>
        /// 0 an ordinary level, 1 hard, 2 very hard. Every world ends on a very hard level and,
        /// from the second world on, has a hard one in the middle. The map marks both, so a wall
        /// the player hits is one they were warned about — the saga games learned that a hard
        /// level nobody flagged reads as the game being unfair.
        /// </summary>
        public static int HardnessOf(int number)
        {
            int place = PlaceInWorld(number);
            if (place == WorldSize) return 2;
            if (place == 6 && number > WorldSize) return 1;
            return 0;
        }

        /// <summary>The level that brings something new onto the board: the first of worlds 2 to 7.</summary>
        public static bool IsIntro(int number) =>
            number == TilesFrom || number == StoneFrom || number == ShadeFrom ||
            number == ColorsFrom || number == TimersFrom || number == DoubleIceFrom;

        /// <summary>The difficulty a level is built at: its number, pushed up for a hard level and down for an intro.</summary>
        static int DifficultyTier(int number)
        {
            if (IsIntro(number)) return Math.Max(4, number - 14);
            switch (HardnessOf(number))
            {
                case 1: return number + 12;
                case 2: return number + 24;
                default: return number;
            }
        }

        static readonly Dictionary<int, LevelDefinition> Cache = new Dictionary<int, LevelDefinition>();
        static readonly Dictionary<int, LevelDefinition> DailyCache = new Dictionary<int, LevelDefinition>();

        public static LevelDefinition Generate(int number, int paletteSize = 6)
        {
            number = Math.Max(1, number);
            lock (Cache)
            {
                if (Cache.TryGetValue(number, out var cached)) return cached;
            }

            var rng = new Rng(unchecked(number * 7919 + 104729));
            var built = Build(number, DifficultyTier(number), rng, unchecked(number * 7919 + 17), PickGoal(number),
                paletteSize, adventure: true);
            built.Hardness = HardnessOf(number);

            lock (Cache) Cache[number] = built;
            return built;
        }

        // ------------------------------------------------------------------ daily

        /// <summary>The first daily puzzle. Its number is 1; every day after counts up from it.</summary>
        public static readonly DateTime DailyEpoch = new DateTime(2026, 1, 1);

        /// <summary>"Puzzle #N": the same number for everyone on the same day.</summary>
        public static int DailyNumber(DateTime date) => Math.Max(1, (int)(date.Date - DailyEpoch).TotalDays + 1);

        /// <summary>
        /// How hard a day's puzzle is, as the adventure level it plays like. The week climbs the
        /// way a newspaper puzzle does — an open Monday, a hard Sunday — so a player learns the
        /// rhythm and a Sunday solve means something. Monday sits inside the warm-up levels and
        /// gets their extra moves; stone arrives from Thursday, double ice from Friday.
        /// </summary>
        public static int DailyTier(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return 8;
                case DayOfWeek.Tuesday: return 12;
                case DayOfWeek.Wednesday: return 17;
                case DayOfWeek.Thursday: return 23;
                case DayOfWeek.Friday: return 30;
                case DayOfWeek.Saturday: return 37;
                default: return 45;
            }
        }

        /// <summary>0 easy, 1 medium, 2 hard, 3 hardest — the label the daily card shows.</summary>
        public static int DailyGrade(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday: return 0;
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday: return 1;
                case DayOfWeek.Friday:
                case DayOfWeek.Saturday: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// The puzzle of a calendar day: one board, goal and move budget shared by everyone that
        /// day. Built from the date alone, like the levels are built from their number, so it is
        /// never stored and costs nothing to ship. <see cref="LevelDefinition.Number"/> is the
        /// puzzle number (<see cref="DailyNumber"/>), which is also how a saved attempt is matched
        /// to its day.
        /// </summary>
        public static LevelDefinition GenerateDaily(DateTime date, int paletteSize = 6)
        {
            int number = DailyNumber(date);
            lock (DailyCache)
            {
                if (DailyCache.TryGetValue(number, out var cached)) return cached;
            }

            int seedBase = unchecked(number * 48271 + 911);
            var rng = new Rng(unchecked(seedBase * 16807 + 12345));

            // Mostly crystals — the goal with a picture — and a lines day now and then for variety.
            // Lines only early in the week: measured over four weeks, a lines goal played a grade
            // easier than crystals at the same tier, and it was flattening the weekend back down
            // to a Wednesday. The score goal is left to the adventure: it has no single "solved"
            // moment to aim at.
            bool early = DailyGrade(date.DayOfWeek) <= 1;
            var goal = rng.Chance(early ? 0.4 : 0.0) ? GoalKind.Lines : GoalKind.Gems;

            var built = Build(number, DailyTier(date.DayOfWeek), rng, seedBase, goal, paletteSize, adventure: false);
            lock (DailyCache) DailyCache[number] = built;
            return built;
        }

        /// <summary>
        /// Drafts until the computer player can finish one, then sets its budget from those runs.
        /// <paramref name="tier"/> is the level number whose rules and difficulty apply; for a
        /// level it is the level itself, for a daily puzzle it comes from the day of the week.
        /// </summary>
        /// <param name="adventure">
        /// True for an adventure level: <paramref name="number"/> decides which obstacles exist yet
        /// and <paramref name="tier"/> only how hard the board is. The daily puzzle keeps to what it
        /// always had — crystals, ice and stone — so its measured week does not move.
        /// </param>
        static LevelDefinition Build(int number, int tier, Rng rng, int seedBase, GoalKind goal, int paletteSize, bool adventure)
        {
            LevelDefinition best = null;
            int features = adventure ? number : tier;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var draft = adventure && number <= 3 ? Authored(number, paletteSize)
                    : adventure && IsIntro(number) ? Intro(number, paletteSize)
                    : Draft(features, tier, rng, attempt, paletteSize, seedBase, goal, adventure);
                draft.Number = number;
                int limit = EstimateMoveLimit(draft, tier, paletteSize, adventure ? HardnessOf(number) : 0);
                if (limit <= 0) continue;

                draft.MoveLimit = limit;
                best = draft;

                // A level the computer needed a hundred moves for is a slog, not a challenge: try a
                // lighter board, and keep this one only if nothing better comes.
                if (adventure && limit > MaxAdventureBudget && attempt < MaxAttempts - 1) continue;
                break;
            }

            // Every attempt was too hard for the computer: fall back to an open board.
            if (best == null)
            {
                best = new LevelDefinition
                {
                    Number = number,
                    Seed = unchecked(number * 31337 + 7),
                    Goal = GoalKind.Lines,
                    Target = 6,
                    MoveLimit = 30,
                    StartCharges = 1
                };
            }

            return best;
        }

        // ------------------------------------------------------------------ drafting

        /// <summary>
        /// The first three levels are authored rather than generated: they are the tutorial.
        /// One crystal a single move away; then two, on a row and a column; then the first ice.
        /// Nothing is written on screen — each board is built so the answer is the obvious move.
        /// </summary>
        static LevelDefinition Authored(int number, int paletteSize)
        {
            var def = new LevelDefinition { Number = number, Seed = unchecked(number * 7919 + 17), Goal = GoalKind.Gems, StartCharges = 1 };
            int a = 0 % paletteSize, b = 3 % paletteSize, c = 1 % paletteSize;

            switch (number)
            {
                case 1:
                    // Bottom row, six of eight filled, the crystal in the middle.
                    for (int x = 0; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 7, a, x == 2, 0));
                    def.Target = 1;
                    break;

                case 2:
                    for (int x = 0; x < 5; x++) def.Prefill.Add(new PrefillCell(x, 6, b, x == 1, 0));
                    for (int y = 0; y < 5; y++) def.Prefill.Add(new PrefillCell(7, y, c, y == 3, 0));
                    def.Target = 2;
                    break;

                default:
                    // The same bottom row as level 1, but the crystal sits under ice: it takes two clears.
                    for (int x = 0; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 7, a, x == 3, x == 3 ? 1 : 0));
                    for (int x = 2; x < 8; x++) def.Prefill.Add(new PrefillCell(x, 5, c, x == 5, 0));
                    def.Target = 2;
                    break;
            }

            return def;
        }

        /// <summary>
        /// The first board of each new obstacle. Like the tutorial levels, built by hand so the
        /// right move is the obvious one; nothing on screen says what the new thing is.
        /// </summary>
        static LevelDefinition Intro(int number, int paletteSize)
        {
            var def = new LevelDefinition { Number = number, Seed = unchecked(number * 7919 + 17), StartCharges = 1 };
            int a = 2 % paletteSize, b = 4 % paletteSize, c = 5 % paletteSize;

            switch (number)
            {
                case TilesFrom:
                    // Glow under the bottom row and the two gaps of the row above: two short
                    // placements light most of it.
                    def.Goal = GoalKind.Tiles;
                    for (int x = 0; x < 8; x++) def.Prefill.Add(new PrefillCell(x, 7, x < 6 ? a : BoardModel.Empty, false, 0, tile: true));
                    for (int x = 0; x < 8; x++) def.Prefill.Add(new PrefillCell(x, 6, x >= 2 ? b : BoardModel.Empty, false, 0, tile: x < 2));
                    for (int x = 3; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 3, BoardModel.Empty, false, 0, tile: true));
                    def.Target = 13;
                    break;

                case StoneFrom:
                    // A stone in a nearly full row: the row clears around it, and it stays.
                    def.Goal = GoalKind.Gems;
                    for (int x = 0; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 7, a, x == 4, x == 1 ? BoardModel.Stone : 0));
                    for (int y = 2; y < 7; y++) def.Prefill.Add(new PrefillCell(0, y, c, y == 3, 0));
                    def.Target = 2;
                    break;

                case ShadeFrom:
                    // A patch of shade at the end of a row one piece from full. Leave it three moves
                    // and it takes a cell; clear it and it is gone.
                    def.Goal = GoalKind.Shade;
                    def.ShadeSpread = 3;
                    for (int x = 0; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 7, x < 2 ? BoardModel.Shade : b, false, 0));
                    def.Prefill.Add(new PrefillCell(0, 6, BoardModel.Shade, false, 0));
                    def.Target = 3;
                    break;

                case ColorsFrom:
                    // The order colour already fills most of two rows.
                    def.Goal = GoalKind.Colors;
                    def.OrderColor = c;
                    for (int x = 0; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 7, c, false, 0));
                    for (int x = 2; x < 8; x++) def.Prefill.Add(new PrefillCell(x, 6, x % 3 == 0 ? a : c, false, 0));
                    def.Target = 16;
                    break;

                case TimersFrom:
                    // A timer with a generous clock in a row one bar from full; a crystal beside it.
                    def.Goal = GoalKind.Gems;
                    for (int x = 0; x < 5; x++) def.Prefill.Add(new PrefillCell(x, 7, a, x == 1, 0, timer: x == 3 ? 9 : 0));
                    for (int x = 3; x < 8; x++) def.Prefill.Add(new PrefillCell(x, 4, b, x == 6, 0));
                    def.Target = 2;
                    break;

                default:
                    // Double ice: two rows of it, a crystal under the thickest.
                    def.Goal = GoalKind.Gems;
                    for (int x = 0; x < 6; x++) def.Prefill.Add(new PrefillCell(x, 7, a, x == 2, x == 2 ? 2 : 0));
                    for (int x = 2; x < 8; x++) def.Prefill.Add(new PrefillCell(x, 5, c, x == 6, x >= 5 ? 2 : 0));
                    def.Target = 2;
                    break;
            }

            return def;
        }

        /// <param name="features">Which obstacles exist yet: the level number (<see cref="IceFrom"/> and on).</param>
        /// <param name="tier">How hard the board is built, as a level number.</param>
        static LevelDefinition Draft(int features, int tier, Rng rng, int attempt, int paletteSize, int seedBase, GoalKind goal, bool adventure)
        {
            int number = features;
            if (!adventure && tier <= 3) return Authored(tier, paletteSize);

            // Each failed attempt eases off, so generation always converges on something playable.
            float t = Difficulty(tier) * (1f - 0.18f * attempt);

            var def = new LevelDefinition
            {
                Number = number,
                Seed = unchecked(seedBase + attempt * 104729),
                StartCharges = 1,
                Goal = goal
            };

            var filled = new bool[BoardSize, BoardSize];
            int target = (int)Math.Round(BoardSize * BoardSize * (0.10f + 0.30f * t));
            if (number <= 2) target = number == 1 ? 6 : 9;
            // A tiles board needs room to work: the glow is lit by building on it.
            if (goal == GoalKind.Tiles) target = (int)(target * 0.7f);

            switch (rng.Next(4))
            {
                case 0: Clusters(filled, target, rng); break;
                case 1: Mirrored(filled, target, rng); break;
                case 2: Stacked(filled, target, rng); break;
                default: Scattered(filled, target, rng); break;
            }

            // Shade starts as a patch grown out of one corner: it reads as something that spreads.
            var shade = new bool[BoardSize, BoardSize];
            bool withShade = adventure && number >= ShadeFrom && (goal == GoalKind.Shade || rng.Chance(0.28 + 0.2 * t));
            if (withShade)
            {
                int patch = goal == GoalKind.Shade ? 3 + (int)Math.Round(6 * t) : 2 + (int)Math.Round(3 * t);
                GrowPatch(shade, patch, rng);
                for (int y = 0; y < BoardSize; y++)
                for (int x = 0; x < BoardSize; x++)
                    if (shade[x, y]) filled[x, y] = true;
                def.ShadeSpread = t > 0.55f ? 2 : 3;
            }

            LeaveRoom(filled, rng);

            // Colours come in runs rather than per cell, so single-colour lines are within reach.
            var cells = new List<(int x, int y)>();
            for (int y = 0; y < BoardSize; y++)
            for (int x = 0; x < BoardSize; x++)
                if (filled[x, y]) cells.Add((x, y));

            var colours = new int[BoardSize, BoardSize];
            int runColour = rng.Next(paletteSize);
            foreach (var (x, y) in cells)
            {
                if (rng.Chance(0.35)) runColour = rng.Next(paletteSize);
                colours[x, y] = runColour;
            }

            // Shade is not a block the other layers can sit on. LeaveRoom may have taken some of it.
            var blocks = new List<(int x, int y)>();
            int shadeCount = 0;
            foreach (var (x, y) in cells)
            {
                if (shade[x, y]) shadeCount++;
                else blocks.Add((x, y));
            }

            if (def.Goal == GoalKind.Colors)
            {
                // The order colour is laid on thick: about a third of the board's blocks.
                def.OrderColor = rng.Next(paletteSize);
                foreach (var (x, y) in blocks)
                    if (rng.Chance(0.36)) colours[x, y] = def.OrderColor;
            }

            var gems = new bool[BoardSize, BoardSize];
            var tiles = new bool[BoardSize, BoardSize];
            if (def.Goal == GoalKind.Gems)
            {
                int gemCount = number <= 2 ? 1 : 2 + (int)Math.Round(5 * t) + (number > 6 ? 1 : 0);
                gemCount = Math.Min(blocks.Count, gemCount);
                if (gemCount > 0) PlaceGems(gems, blocks, gemCount, rng);
                def.Target = Math.Max(1, gemCount);
            }
            else if (def.Goal == GoalKind.Lines)
            {
                def.Target = 5 + (int)Math.Round(11 * t);
            }
            else if (def.Goal == GoalKind.Tiles)
            {
                def.Target = LayTiles(tiles, 10 + (int)Math.Round(16 * t), rng);
            }
            else if (def.Goal == GoalKind.Colors)
            {
                def.Target = 12 + (int)Math.Round(20 * t);
            }
            else if (def.Goal == GoalKind.Shade)
            {
                def.Target = Math.Max(1, shadeCount);
            }
            else
            {
                def.Target = RoundTo(450 + 2600 * t, 50);
            }

            float iceChance = number >= IceFrom ? 0.10f + 0.32f * t : 0f;
            // The daily's double ice keeps its old threshold; the adventure gives it a world of its own.
            float doubleIce = adventure
                ? (number >= DoubleIceFrom ? 0.45f * t : number >= StoneFrom ? 0.12f * t : 0f)
                : (number >= 30 ? 0.25f * t : 0f);
            // Tiles under ice would be a wall, not a puzzle.
            if (def.Goal == GoalKind.Tiles) iceChance *= 0.5f;

            // Stone arrives at level 21 as a new obstacle wave: a few cells that fill their lines but
            // never leave them. At most one per row and column, so no line is ever made of stone.
            int stones = number >= StoneFrom ? 1 + (int)Math.Round(3 * t) : 0;
            var stoneRows = new bool[BoardSize];
            var stoneCols = new bool[BoardSize];
            var stoneAt = new bool[BoardSize, BoardSize];
            for (int tries = 0; stones > 0 && blocks.Count > 0 && tries < 40; tries++)
            {
                var (sx, sy) = blocks[rng.Next(blocks.Count)];
                if (gems[sx, sy] || tiles[sx, sy] || stoneRows[sy] || stoneCols[sx]) continue;
                stoneAt[sx, sy] = stoneRows[sy] = stoneCols[sx] = true;
                stones--;
            }

            // Timer blocks: a clock on an ordinary block. Clear its line before it reaches zero.
            var timerAt = new int[BoardSize, BoardSize];
            if (adventure && number >= TimersFrom && rng.Chance(0.45 + 0.25 * t))
            {
                int timers = 1 + (int)Math.Round(2 * t);
                for (int tries = 0; timers > 0 && blocks.Count > 0 && tries < 40; tries++)
                {
                    var (tx, ty) = blocks[rng.Next(blocks.Count)];
                    if (gems[tx, ty] || stoneAt[tx, ty] || timerAt[tx, ty] > 0) continue;
                    timerAt[tx, ty] = 16 - (int)Math.Round(6 * t) + rng.Next(4);
                    timers--;
                }
            }

            foreach (var (x, y) in cells)
            {
                if (shade[x, y])
                {
                    def.Prefill.Add(new PrefillCell(x, y, BoardModel.Shade, false, 0, tiles[x, y]));
                    continue;
                }

                int ice = 0;
                if (stoneAt[x, y])
                {
                    ice = BoardModel.Stone;
                }
                else if (timerAt[x, y] == 0 && rng.Chance(iceChance))
                {
                    ice = rng.Chance(doubleIce) ? 2 : 1;
                }

                def.Prefill.Add(new PrefillCell(x, y, colours[x, y], gems[x, y], ice, tiles[x, y], timerAt[x, y]));
            }

            // Glow on free cells is laid on the floor alone.
            for (int y = 0; y < BoardSize; y++)
            for (int x = 0; x < BoardSize; x++)
                if (tiles[x, y] && !filled[x, y])
                    def.Prefill.Add(new PrefillCell(x, y, BoardModel.Empty, false, 0, tile: true));

            return def;
        }

        /// <summary>
        /// A level's goal. The first three are the tutorial's crystals; from then on each world
        /// leans on what it introduced, and the later worlds deal from everything.
        /// </summary>
        static GoalKind PickGoal(int number)
        {
            if (number <= 3) return GoalKind.Gems;
            if (number < TilesFrom)
            {
                switch (number % 6)
                {
                    case 0: return GoalKind.Lines;
                    case 3: return GoalKind.Score;
                    default: return GoalKind.Gems;
                }
            }

            // A stream of its own, so the choice does not shift the board drawn after it.
            var pick = new Rng(unchecked(number * 40503 + 97));
            double roll = pick.NextDouble();

            // The world that brings a goal plays it half the time.
            int world = WorldOf(number);
            GoalKind? featured = world == 1 ? GoalKind.Tiles : world == 3 ? GoalKind.Shade : world == 4 ? GoalKind.Colors : (GoalKind?)null;
            if (featured != null)
            {
                if (roll < 0.5) return featured.Value;
                roll = (roll - 0.5) * 2.0;
            }

            // Otherwise from everything unlocked so far, crystals the most common.
            var pool = new List<(GoalKind goal, double weight)>
            {
                (GoalKind.Gems, 3.0), (GoalKind.Lines, 1.0), (GoalKind.Score, 0.6), (GoalKind.Tiles, 2.0)
            };
            if (number >= ShadeFrom) pool.Add((GoalKind.Shade, 1.4));
            if (number >= ColorsFrom) pool.Add((GoalKind.Colors, 1.6));

            double total = 0;
            foreach (var entry in pool) total += entry.weight;
            double at = roll * total;
            foreach (var entry in pool)
            {
                at -= entry.weight;
                if (at < 0) return entry.goal;
            }

            return GoalKind.Gems;
        }

        /// <summary>
        /// Grows a patch of shade out of a random corner, one neighbouring cell at a time, so it
        /// starts as one blot rather than a scatter.
        /// </summary>
        static void GrowPatch(bool[,] patch, int count, Rng rng)
        {
            int cx = rng.Chance(0.5) ? 0 : BoardSize - 1;
            int cy = rng.Chance(0.5) ? 0 : BoardSize - 1;
            patch[cx, cy] = true;
            int placed = 1, guard = 0;

            while (placed < count && guard++ < 400)
            {
                int x = rng.Next(BoardSize), y = rng.Next(BoardSize);
                if (patch[x, y]) continue;
                bool touches = (x > 0 && patch[x - 1, y]) || (x < BoardSize - 1 && patch[x + 1, y]) ||
                               (y > 0 && patch[x, y - 1]) || (y < BoardSize - 1 && patch[x, y + 1]);
                if (!touches) continue;
                patch[x, y] = true;
                placed++;
            }
        }

        /// <summary>
        /// Lays glow tiles in a shape — two bands, a frame, a plus, a centre block, or a mirrored
        /// scatter — trimmed to about <paramref name="want"/>. Returns how many were laid.
        /// </summary>
        static int LayTiles(bool[,] tiles, int want, Rng rng)
        {
            int n = BoardSize;
            switch (rng.Next(5))
            {
                case 0:
                {
                    bool across = rng.Chance(0.5);
                    int a = 2 + rng.Next(2), b = Math.Min(n - 1, a + 2 + rng.Next(2));
                    for (int i = 0; i < n; i++)
                    {
                        if (across)
                        {
                            tiles[i, a] = true;
                            tiles[i, b] = true;
                        }
                        else
                        {
                            tiles[a, i] = true;
                            tiles[b, i] = true;
                        }
                    }
                    break;
                }
                case 1:
                    for (int i = 1; i < n - 1; i++)
                    {
                        tiles[i, 1] = true;
                        tiles[i, n - 2] = true;
                        tiles[1, i] = true;
                        tiles[n - 2, i] = true;
                    }
                    break;
                case 2:
                    for (int i = 0; i < n; i++)
                    {
                        tiles[i, 3] = true;
                        tiles[i, 4] = true;
                        tiles[3, i] = true;
                        tiles[4, i] = true;
                    }
                    break;
                case 3:
                    for (int y = 2; y < 6; y++)
                    for (int x = 1; x < 7; x++)
                        tiles[x, y] = true;
                    break;
                default:
                {
                    int guard = 0, laid = 0;
                    while (laid < want && guard++ < 300)
                    {
                        int x = rng.Next(n / 2), y = rng.Next(n);
                        if (tiles[x, y]) continue;
                        tiles[x, y] = true;
                        tiles[n - 1 - x, y] = true;
                        laid += 2;
                    }
                    break;
                }
            }

            // Trim a shape down to size in mirrored pairs, so it keeps its look.
            int count = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (tiles[x, y]) count++;

            int trims = 0;
            while (count > want + 1 && trims++ < 400)
            {
                int x = rng.Next(n / 2), y = rng.Next(n);
                if (!tiles[x, y]) continue;
                tiles[x, y] = false;
                count--;
                if (tiles[n - 1 - x, y])
                {
                    tiles[n - 1 - x, y] = false;
                    count--;
                }
            }

            return count;
        }

        static void Clusters(bool[,] filled, int target, Rng rng)
        {
            int placed = 0, guard = 0;
            while (placed < target && guard++ < 400)
            {
                int cx = rng.Next(BoardSize), cy = rng.Next(BoardSize);
                int size = 2 + rng.Next(5);
                for (int i = 0; i < size && placed < target; i++)
                {
                    if (!filled[cx, cy]) { filled[cx, cy] = true; placed++; }
                    switch (rng.Next(4))
                    {
                        case 0: cx = Math.Min(BoardSize - 1, cx + 1); break;
                        case 1: cx = Math.Max(0, cx - 1); break;
                        case 2: cy = Math.Min(BoardSize - 1, cy + 1); break;
                        default: cy = Math.Max(0, cy - 1); break;
                    }
                }
            }
        }

        static void Mirrored(bool[,] filled, int target, Rng rng)
        {
            int placed = 0, guard = 0;
            while (placed < target && guard++ < 400)
            {
                int x = rng.Next(BoardSize / 2), y = rng.Next(BoardSize);
                int mx = BoardSize - 1 - x;
                if (filled[x, y]) continue;
                filled[x, y] = true;
                filled[mx, y] = true;
                placed += 2;
            }
        }

        static void Stacked(bool[,] filled, int target, Rng rng)
        {
            // Rubble settled at the bottom, like a game already in progress.
            int placed = 0;
            for (int y = BoardSize - 1; y >= 0 && placed < target; y--)
            for (int x = 0; x < BoardSize && placed < target; x++)
            {
                if (rng.Chance(0.62)) { filled[x, y] = true; placed++; }
            }
        }

        static void Scattered(bool[,] filled, int target, Rng rng)
        {
            int placed = 0, guard = 0;
            while (placed < target && guard++ < 600)
            {
                int x = rng.Next(BoardSize), y = rng.Next(BoardSize);
                if (filled[x, y]) continue;
                filled[x, y] = true;
                placed++;
            }
        }

        /// <summary>No line may start full or nearly full — at least two gaps in every row and column.</summary>
        static void LeaveRoom(bool[,] filled, Rng rng)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < BoardSize; y++)
                {
                    int count = 0;
                    for (int x = 0; x < BoardSize; x++) if (filled[x, y]) count++;
                    while (count > BoardSize - 2)
                    {
                        int x = rng.Next(BoardSize);
                        if (!filled[x, y]) continue;
                        filled[x, y] = false;
                        count--;
                    }
                }

                for (int x = 0; x < BoardSize; x++)
                {
                    int count = 0;
                    for (int y = 0; y < BoardSize; y++) if (filled[x, y]) count++;
                    while (count > BoardSize - 2)
                    {
                        int y = rng.Next(BoardSize);
                        if (!filled[x, y]) continue;
                        filled[x, y] = false;
                        count--;
                    }
                }
            }
        }

        static void PlaceGems(bool[,] gems, List<(int x, int y)> cells, int count, Rng rng)
        {
            // Spread across rows where possible, so one lucky line does not free them all.
            var usedRows = new HashSet<int>();
            int placed = 0, guard = 0;

            while (placed < count && guard++ < 500)
            {
                var (x, y) = cells[rng.Next(cells.Count)];
                if (gems[x, y]) continue;
                if (usedRows.Contains(y) && guard < 250) continue;

                gems[x, y] = true;
                usedRows.Add(y);
                placed++;
            }
        }

        // ------------------------------------------------------------------ budget

        /// <summary>
        /// Plays the draft several times and returns a move budget, or 0 when the computer could
        /// not finish it often enough for the level to be fair.
        /// </summary>
        static int EstimateMoveLimit(LevelDefinition draft, int tier, int paletteSize, int hardness)
        {
            draft.MoveLimit = MoveCap;
            var needed = new List<int>(SimulationRuns);
            int wins = 0;

            for (int run = 0; run < SimulationRuns; run++)
            {
                var config = new SessionConfig
                {
                    Mode = GameMode.Level,
                    Seed = unchecked(draft.Seed + run * 977),
                    BoardSize = BoardSize,
                    PaletteSize = paletteSize,
                    StartCharges = draft.StartCharges,
                    Level = draft
                };

                var session = new GameSession(config);
                var bot = new Autoplayer(unchecked(draft.Seed ^ (run * 7331 + 11)), BotSkill);

                int guard = 0;
                while (!session.IsFinished && guard++ < MoveCap * 3)
                    if (!bot.Step(session)) break;

                if (session.State == SessionState.Won)
                {
                    wins++;
                    needed.Add(session.MovesUsed);
                }
                else
                {
                    needed.Add(MoveCap + 1);
                }
            }

            if (wins < SimulationRuns * MinWinShare) return 0;

            needed.Sort();
            // A hard level is sized from a lower slice of the runs: the computer's good days, not its typical one.
            float percentile = hardness == 2 ? 0.55f : hardness == 1 ? 0.65f : BudgetPercentile;
            int typical = needed[(int)(SimulationRuns * percentile)];
            if (typical > MoveCap) return 0;

            float t = Difficulty(tier);
            float slack = 1.40f - 0.25f * t;
            // A marked level is tight on purpose; the map told the player so.
            if (hardness == 1) slack *= 0.92f;
            else if (hardness == 2) slack *= 0.84f;
            int bonus = tier <= WarmupLevels ? WarmupBonusMoves : 0;

            return Math.Max(10, (int)Math.Ceiling(typical * slack) + bonus);
        }

        static int RoundTo(float value, int step) => (int)(Math.Round(value / step) * step);
    }
}
