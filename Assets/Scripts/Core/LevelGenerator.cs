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

        /// <summary>The opening levels are where a player decides whether the mode is for them.</summary>
        const int WarmupLevels = 10;
        const int WarmupBonusMoves = 6;

        /// <summary>0 for the first level, rising towards 1. Most of the climb happens in the first hundred.</summary>
        public static float Difficulty(int number) => 1f - (float)Math.Exp(-(Math.Max(1, number) - 1) / 45.0);

        static readonly Dictionary<int, LevelDefinition> Cache = new Dictionary<int, LevelDefinition>();

        public static LevelDefinition Generate(int number, int paletteSize = 6)
        {
            number = Math.Max(1, number);
            lock (Cache)
            {
                if (Cache.TryGetValue(number, out var cached)) return cached;
            }

            var rng = new Rng(unchecked(number * 7919 + 104729));
            LevelDefinition best = null;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var draft = Draft(number, rng, attempt, paletteSize);
                int limit = EstimateMoveLimit(draft, paletteSize);
                if (limit > 0)
                {
                    draft.MoveLimit = limit;
                    best = draft;
                    break;
                }
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

            lock (Cache) Cache[number] = best;
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

        static LevelDefinition Draft(int number, Rng rng, int attempt, int paletteSize)
        {
            if (number <= 3) return Authored(number, paletteSize);

            // Each failed attempt eases off, so generation always converges on something playable.
            float t = Difficulty(number) * (1f - 0.18f * attempt);

            var def = new LevelDefinition
            {
                Number = number,
                Seed = unchecked(number * 7919 + attempt * 104729 + 17),
                StartCharges = 1,
                Goal = PickGoal(number)
            };

            var filled = new bool[BoardSize, BoardSize];
            int target = (int)Math.Round(BoardSize * BoardSize * (0.10f + 0.30f * t));
            if (number <= 2) target = number == 1 ? 6 : 9;

            switch (rng.Next(4))
            {
                case 0: Clusters(filled, target, rng); break;
                case 1: Mirrored(filled, target, rng); break;
                case 2: Stacked(filled, target, rng); break;
                default: Scattered(filled, target, rng); break;
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

            var gems = new bool[BoardSize, BoardSize];
            if (def.Goal == GoalKind.Gems)
            {
                int gemCount = number <= 2 ? 1 : 2 + (int)Math.Round(5 * t) + (number > 6 ? 1 : 0);
                gemCount = Math.Min(cells.Count, gemCount);
                PlaceGems(gems, cells, gemCount, rng);
                def.Target = gemCount;
            }
            else if (def.Goal == GoalKind.Lines)
            {
                def.Target = 5 + (int)Math.Round(11 * t);
            }
            else
            {
                def.Target = RoundTo(450 + 2600 * t, 50);
            }

            float iceChance = number >= 6 ? 0.10f + 0.32f * t : 0f;
            float doubleIce = number >= 30 ? 0.25f * t : 0f;

            // Stone arrives at level 21 as a new obstacle wave: a few cells that fill their lines but
            // never leave them. At most one per row and column, so no line is ever made of stone.
            int stones = number >= 21 ? 1 + (int)Math.Round(3 * t) : 0;
            var stoneRows = new bool[BoardSize];
            var stoneCols = new bool[BoardSize];
            var stoneAt = new bool[BoardSize, BoardSize];
            for (int tries = 0; stones > 0 && cells.Count > 0 && tries < 40; tries++)
            {
                var (sx, sy) = cells[rng.Next(cells.Count)];
                if (gems[sx, sy] || stoneRows[sy] || stoneCols[sx]) continue;
                stoneAt[sx, sy] = stoneRows[sy] = stoneCols[sx] = true;
                stones--;
            }

            foreach (var (x, y) in cells)
            {
                int ice = 0;
                if (stoneAt[x, y])
                {
                    ice = BoardModel.Stone;
                }
                else if (rng.Chance(iceChance))
                {
                    ice = rng.Chance(doubleIce) ? 2 : 1;
                }

                def.Prefill.Add(new PrefillCell(x, y, colours[x, y], gems[x, y], ice));
            }

            return def;
        }

        static GoalKind PickGoal(int number)
        {
            if (number <= 3) return GoalKind.Gems;
            switch (number % 6)
            {
                case 0: return GoalKind.Lines;
                case 3: return GoalKind.Score;
                default: return GoalKind.Gems;
            }
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
        static int EstimateMoveLimit(LevelDefinition draft, int paletteSize)
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
            int typical = needed[(int)(SimulationRuns * BudgetPercentile)];
            if (typical > MoveCap) return 0;

            float t = Difficulty(draft.Number);
            float slack = 1.40f - 0.25f * t;
            int bonus = draft.Number <= WarmupLevels ? WarmupBonusMoves : 0;

            return Math.Max(10, (int)Math.Ceiling(typical * slack) + bonus);
        }

        static int RoundTo(float value, int step) => (int)(Math.Round(value / step) * step);
    }
}
