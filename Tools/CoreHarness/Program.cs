using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BlockPuzzle.Core;

/// <summary>
/// Rule tests and balance measurements for Assets/Scripts/Core, run outside Unity.
/// Every run has a move cap: an uncapped simulation against a strong dealer never ends.
/// </summary>
static class Program
{
    static int _failures;

    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "tests";
        switch (mode)
        {
            case "tests": Tests(); break;
            case "balance": Balance(args.Length > 1 ? int.Parse(args[1]) : 40); break;
            case "levels": Levels(args.Length > 1 ? int.Parse(args[1]) : 1, args.Length > 2 ? int.Parse(args[2]) : 60); break;
            case "perf": Perf(); break;
            case "daily": Daily(args.Length > 1 ? int.Parse(args[1]) : 14); break;
            default: Console.WriteLine("usage: tests | balance [runs] | levels [from] [to] | perf"); return 2;
        }

        if (_failures > 0) Console.WriteLine($"FAILURES: {_failures}");
        return _failures > 0 ? 1 : 0;
    }

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _failures++;
    }

    // ------------------------------------------------------------------ tests

    static void Tests()
    {
        Console.WriteLine("== rotation");
        foreach (var w in PieceLibrary.All)
        {
            var r = PieceLibrary.Canonical(w.Shape.RotatedClockwise());
            bool inLibrary = PieceLibrary.All.Any(o => ReferenceEquals(o.Shape, r));
            if (!inLibrary) Check(false, $"{w.Shape.Id} rotates to a shape outside the library");
        }
        Check(PieceLibrary.Canonical(PieceLibrary.ById("corner_tl").RotatedClockwise()).Id == "corner_tr", "corner_tl -> corner_tr");
        Check(PieceLibrary.Canonical(PieceLibrary.ById("bar3_h").RotatedClockwise()).Id == "bar3_v", "bar3_h -> bar3_v");
        Check(PieceLibrary.ById("s_h").ToCompact() == ".XX/XX.", "compact form");

        Console.WriteLine("== determinism");
        Check(PlayOut(123, 77, 0.7f, 400).Score == PlayOut(123, 77, 0.7f, 400).Score, "same seeds, same run");

        Console.WriteLine("== stone");
        {
            var b = new BoardModel(8);
            for (int x = 0; x < 5; x++) b.SetPrefill(x, 0, 1, false, x == 2 ? BoardModel.Stone : 0);
            var r = b.Place(PieceLibrary.ById("bar3_h"), 5, 0, 1);
            Check(r.ClearedRows.Count == 1, "a row with a stone still clears");
            Check(b.IsStone(2, 0) && b.RowCount(0) == 1, "the stone stays, alone in its row");
            Check(r.CrackedIce.Count == 0 && !r.PerfectClear, "stone is not cracked and blocks a perfect clear");
            b.Blast(2, 0, 1);
            Check(!b.IsOccupied(2, 0) && !b.IsStone(2, 0), "the bomb breaks a stone");
            var level = LevelGenerator.Generate(30, 7);
            Check(level.Prefill.Exists(c => c.Ice == BoardModel.Stone), "level 30 has stones");
            Check(!LevelGenerator.Generate(12, 7).Prefill.Exists(c => c.Ice == BoardModel.Stone), "level 12 has none");
        }

        Console.WriteLine("== snapshot");
        {
            var a = new GameSession(new SessionConfig { Seed = 99 });
            var bot = new Autoplayer(5, 0.7f);
            for (int i = 0; i < 25 && !a.IsFinished; i++) bot.Step(a);

            var b = GameSession.Restore(a.CreateSnapshot());
            Check(SameState(a, b), "restored state matches");

            var botA = new Autoplayer(42, 0.7f);
            var botB = new Autoplayer(42, 0.7f);
            for (int i = 0; i < 200; i++)
            {
                bool ga = !a.IsFinished && botA.Step(a);
                bool gb = !b.IsFinished && botB.Step(b);
                if (!ga && !gb) break;
            }
            Check(a.Score == b.Score && a.MovesUsed == b.MovesUsed && SameState(a, b), $"restored run continues identically ({a.Score} vs {b.Score})");
        }

        Console.WriteLine("== board rules");
        {
            var board = new BoardModel(8);
            for (int x = 1; x < 8; x++) board.SetPrefill(x, 0, 2, gem: x == 3, ice: x == 5 ? 1 : 0);
            var dot = PieceLibrary.ById("dot");
            var clear = board.Place(dot, 0, 0, 2);
            Check(clear.ClearedRows.Count == 1, "row completes");
            Check(clear.MonoLines == 1 && clear.MonoRows.Count == 1, "single-colour row detected");
            Check(clear.CollectedGems.Count == 1 && board.GemCount == 0, "gem collected");
            Check(clear.CrackedIce.Count == 1 && board.IsOccupied(5, 0) && board.IceAt(5, 0) == 0, "ice cracks, block stays");
            Check(!clear.PerfectClear && board.OccupiedCount == 1, "one block left");

            // Left behind: the ice-cracked block at x=5. Fill around it: 0-3, 4, then 6-7.
            var clear2 = board.Place(PieceLibrary.ById("bar4_h"), 0, 0, 1);
            board.Place(dot, 4, 0, 1);
            var clear3 = board.Place(PieceLibrary.ById("bar2_h"), 6, 0, 1);
            Check(!clear2.Any && clear3.Any && clear3.PerfectClear, "second clear removes it: perfect clear");
            Check(clear3.MonoLines == 0, "mixed row is not single-colour");

            var b2 = new BoardModel(8);
            b2.SetPrefill(4, 4, 0, gem: true, ice: 2);
            var blast = b2.Blast(4, 4, 1);
            Check(blast.CollectedGems.Count == 1 && b2.OccupiedCount == 0, "bomb clears through ice");
        }

        Console.WriteLine("== powers");
        {
            var s = new GameSession(new SessionConfig { Seed = 5, StartCharges = 3 });
            int slot = Enumerable.Range(0, 3).FirstOrDefault(i => s.RotatedShape(i) != null);
            if (s.RotatedShape(slot) != null)
            {
                var expected = s.RotatedShape(slot);
                s.TryRotate(slot);
                Check(ReferenceEquals(s.Tray[slot].Shape, expected) && s.Charges == 2, "rotate spends one charge");
            }
            s.TryReroll();
            Check(s.Charges == 1, "reroll spends one charge");
            Check(!s.CanUsePower(PowerKind.Bomb), "bomb needs two charges");

            var none = new GameSession(new SessionConfig { Seed = 11, StartCharges = 0 });
            Check(!none.CanUsePower(PowerKind.Reroll), "no charges, no powers");
            Check(PowerRules.LinesForCharge(1) > PowerRules.LinesForCharge(0), "each charge costs more than the last");
        }

        Console.WriteLine("== extra moves");
        {
            var def = new LevelDefinition { Number = 99, Seed = 3, Goal = GoalKind.Lines, Target = 999, MoveLimit = 6, StartCharges = 3 };
            var s = GameSession.NewLevelRun(def, 8, 7);
            var bot = new Autoplayer(1, 1f);
            int guard = 0;
            while (s.State == SessionState.Playing && guard++ < 50) bot.Step(s);
            Check(s.State == SessionState.OutOfMoves && s.MovesLeft == 0, "running out offers more moves");

            int charges = s.Charges;
            Check(s.TryBuyMoves() && s.MovesLeft == PowerRules.ExtraMoves && s.Charges == charges - PowerRules.ExtraMovesCost,
                "extra moves cost two charges");

            var restored = GameSession.Restore(s.CreateSnapshot());
            Check(restored.MovesLeft == s.MovesLeft && restored.BonusMoves == s.BonusMoves, "bought moves survive a save");

            guard = 0;
            while (s.State == SessionState.Playing && guard++ < 50) bot.Step(s);
            Check(s.State == SessionState.Lost, "only once per attempt");

            var poor = GameSession.NewLevelRun(new LevelDefinition { Number = 98, Seed = 4, Goal = GoalKind.Lines, Target = 999, MoveLimit = 4, StartCharges = 0 }, 8, 7);
            guard = 0;
            while (poor.State == SessionState.Playing && guard++ < 50) bot.Step(poor);
            Check(poor.State == SessionState.Lost, "without charges it is simply over");
        }

        Console.WriteLine("== glow tiles");
        {
            var b = new BoardModel(8);
            for (int x = 0; x < 6; x++) b.SetPrefill(x, 0, 1);
            b.SetTile(6, 0, true);
            b.SetTile(2, 0, true);
            b.SetTile(3, 5, true);
            Check(b.TileCount == 3, "tiles laid on free and filled cells");
            var r = b.Place(PieceLibrary.ById("bar2_h"), 6, 0, 1);
            Check(r.CollectedTiles.Count == 2 && b.TileCount == 1, "a clear puts out the tiles in its line, placed-on ones too");
            Check(r.ClearedColors.Count == r.ClearedCells.Count, "every cleared cell reports its colour");
        }

        Console.WriteLine("== shade");
        {
            var b = new BoardModel(8);
            b.SetPrefill(0, 0, BoardModel.Shade);
            Check(b.ShadeCount == 1 && b.IsOccupied(0, 0), "shade is a block");
            var spread = b.SpreadShade(new Rng(3));
            Check(b.ShadeCount == 2 && (spread.X == 1 && spread.Y == 0 || spread.X == 0 && spread.Y == 1), "shade creeps into a neighbour");
            for (int x = 0; x < 8; x++) if (!b.IsOccupied(x, 0)) b.SetPrefill(x, 0, BoardModel.Shade);
            var full = new BoardModel(8);
            for (int x = 0; x < 7; x++) full.SetPrefill(x, 0, BoardModel.Shade);
            var r = full.Place(PieceLibrary.ById("dot"), 7, 0, 3);
            Check(r.ClearedShade == 7 && r.MonoLines == 0 && full.ShadeCount == 0, "a line of shade clears, and is never single-colour");

            var def = new LevelDefinition { Number = 1, Seed = 5, Goal = GoalKind.Shade, Target = 1, MoveLimit = 40, ShadeSpread = 1 };
            def.Prefill.Add(new PrefillCell(7, 7, BoardModel.Shade, false, 0));
            var s = GameSession.NewLevelRun(def, 8, 7);
            var bot = new Autoplayer(2, 0f);
            int before = s.Board.ShadeCount;
            bot.Step(s);
            Check(s.Board.ShadeCount >= before || s.State == SessionState.Won, "left alone, shade grows every quiet move");
            var restored = GameSession.Restore(s.CreateSnapshot());
            Check(restored.Board.ShadeCount == s.Board.ShadeCount && restored.Level.ShadeSpread == 1 && restored.ShadeQuiet == s.ShadeQuiet, "shade survives a save");
        }

        Console.WriteLine("== timers");
        {
            var def = new LevelDefinition { Number = 1, Seed = 9, Goal = GoalKind.Lines, Target = 99, MoveLimit = 40, StartCharges = 3 };
            def.Prefill.Add(new PrefillCell(0, 7, 1, false, 0, timer: 2));
            var s = GameSession.NewLevelRun(def, 8, 7);
            Check(s.Board.TimerCount == 1 && s.Board.TimerAt(0, 7) == 2, "timer set from the prefill");
            var saved = GameSession.Restore(s.CreateSnapshot());
            Check(saved.Board.TimerAt(0, 7) == 2, "a timer survives a save");
            // Two moves nowhere near its row.
            for (int i = 0; i < 2 && s.State == SessionState.Playing; i++)
            {
                bool done = false;
                for (int slot = 0; slot < 3 && !done; slot++)
                for (int row = 0; row < 4 && !done; row++)
                for (int col = 0; col < 8 && !done; col++)
                    if (s.CanPlace(slot, col, row)) done = s.TryPlace(slot, col, row) != null;
            }
            Check(s.State == SessionState.Lost && s.LossReason == LossReason.Timer, $"a timer at zero ends the run, charges or not ({s.State}, {s.LossReason})");

            var b = new BoardModel(8);
            for (int x = 0; x < 7; x++) b.SetPrefill(x, 0, 2, false, 0, x == 3 ? 5 : 0);
            var r = b.Place(PieceLibrary.ById("dot"), 7, 0, 2);
            Check(r.DefusedTimers.Count == 1 && b.TimerCount == 0, "a clear takes the timer with it");
        }

        Console.WriteLine("== hammer and colours");
        {
            var def = new LevelDefinition { Number = 1, Seed = 9, Goal = GoalKind.Colors, OrderColor = 3, Target = 7, MoveLimit = 40 };
            def.Prefill.Add(new PrefillCell(2, 2, 1, false, BoardModel.Stone));
            for (int x = 0; x < 7; x++) def.Prefill.Add(new PrefillCell(x, 7, 3, false, 0));
            var s = GameSession.NewLevelRun(def, 8, 7, extraCharges: 1, extraMoves: 3);
            Check(s.Charges == 2 && s.MovesLeft == 43, "boosters add a charge and moves");
            var hit = s.TryHammer(2, 2);
            Check(hit != null && !s.Board.IsOccupied(2, 2) && s.Charges == 2, "the hammer breaks stone and costs no charge");
            int slot = -1;
            for (int i = 0; i < 3; i++) if (s.Tray[i].Shape.Id == "dot") slot = i;
            if (slot < 0)
            {
                var hit2 = s.TryHammer(0, 7);
                Check(hit2 != null && s.ColorCollected == 1, "a hammered block of the order colour counts");
            }
            var restored = GameSession.Restore(s.CreateSnapshot());
            Check(restored.MovesLeft == s.MovesLeft && restored.Level.OrderColor == 3 && restored.ColorCollected == s.ColorCollected, "order and extra moves survive a save");
        }

        Console.WriteLine("== adventure");
        {
            Check(LevelGenerator.WorldOf(1) == 0 && LevelGenerator.WorldOf(10) == 0 && LevelGenerator.WorldOf(11) == 1 && LevelGenerator.WorldOf(100) == 9, "ten levels a world");
            Check(LevelGenerator.HardnessOf(10) == 2 && LevelGenerator.HardnessOf(16) == 1 && LevelGenerator.HardnessOf(6) == 0, "hard levels where the map says");
            Check(LevelGenerator.Generate(LevelGenerator.TilesFrom, 7).Goal == GoalKind.Tiles, "the tiles world opens on a tiles board");
            Check(LevelGenerator.Generate(LevelGenerator.ShadeFrom, 7).Goal == GoalKind.Shade, "the shade world opens on shade");
            Check(LevelGenerator.Generate(LevelGenerator.TimersFrom, 7).Prefill.Exists(c => c.Timer > 0), "the timer world opens on a timer");
            Check(!LevelGenerator.Generate(20, 7).Prefill.Exists(c => c.Color == BoardModel.Shade || c.Timer > 0), "no shade or timers before their worlds");
        }

        Console.WriteLine("== level");
        {
            var sw = Stopwatch.StartNew();
            var level = LevelGenerator.Generate(12);
            sw.Stop();
            Console.WriteLine($"  level 12: goal {level.Goal} target {level.Target} limit {level.MoveLimit} prefill {level.Prefill.Count}  ({sw.ElapsedMilliseconds} ms)");
            Check(ReferenceEquals(level, LevelGenerator.Generate(12)), "cached");
            var s = GameSession.NewLevelRun(level);
            Check(s.Board.OccupiedCount == level.Prefill.Count, "prefill applied");
            var restored = GameSession.Restore(s.CreateSnapshot());
            Check(restored.Level.MoveLimit == level.MoveLimit && restored.Level.Target == level.Target, "level survives a save");

            // A prefetch on a worker and a tap on the main thread asking for the same level: the
            // second waits for the first instead of building it again.
            Check(!LevelGenerator.IsReady(57), "level 57 not built yet");
            LevelDefinition a = null, b = null;
            var worker = new System.Threading.Thread(() => a = LevelGenerator.Generate(57));
            worker.Start();
            System.Threading.Thread.Sleep(5);
            b = LevelGenerator.Generate(57);
            worker.Join();
            Check(ReferenceEquals(a, b) && LevelGenerator.IsReady(57), "concurrent requests share one build");
        }
    }

    static bool SameState(GameSession a, GameSession b)
    {
        if (a.Score != b.Score || a.Charges != b.Charges || a.ChargeProgress != b.ChargeProgress ||
            a.ChargesEarned != b.ChargesEarned || a.ComboStreak != b.ComboStreak || a.State != b.State ||
            a.MovesUsed != b.MovesUsed) return false;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            if (a.Board.GetCell(x, y) != b.Board.GetCell(x, y) || a.Board.IceAt(x, y) != b.Board.IceAt(x, y) ||
                a.Board.HasGem(x, y) != b.Board.HasGem(x, y)) return false;
        for (int i = 0; i < 3; i++)
            if (a.Tray[i].Used != b.Tray[i].Used || a.Tray[i].ColorIndex != b.Tray[i].ColorIndex ||
                !a.Tray[i].Shape.SameCells(b.Tray[i].Shape)) return false;
        return true;
    }

    static GameSession PlayOut(int seed, int botSeed, float skill, int cap, bool powers = true)
    {
        var s = new GameSession(new SessionConfig { Seed = seed, PowersEnabled = powers, PaletteSize = 7 });
        var bot = new Autoplayer(botSeed, skill);
        int guard = 0;
        while (!s.IsFinished && s.MovesUsed < cap && guard++ < cap * 4)
            if (!bot.Step(s)) break;
        return s;
    }

    /// <summary>
    /// What a run felt like, as opposed to how long it was. Totals alone hid the complaint that
    /// prompted this: a run can last plenty of moves while the board sits nearly full the whole
    /// time, never clears more than one line, and never once comes back to empty.
    /// </summary>
    sealed class Feel
    {
        public int Moves;
        public int Clears;
        public int MultiLine;      // clears that took two or more lines in one move
        public int PerfectClears;
        public int BestStreak;
        public long StreakSum;      // streak value on every clearing move
        public double OccupancySum; // board fill, sampled after every move
        public int Samples;
        public int Tight;           // moves played with the board more than 60% full
        public double PeakOccupancy;
        public double DeathOccupancy; // how full the board was when the run ended
        public int MinCells = int.MaxValue; // emptiest the board ever got
        public int MinSpanRows;             // distinct rows those survivors sat in
        public int MinSpanCols;

        public double Occupancy => Samples == 0 ? 0 : OccupancySum / Samples;
        public double MultiLineRate => Clears == 0 ? 0 : MultiLine / (double)Clears;
        public double AvgStreak => Clears == 0 ? 0 : StreakSum / (double)Clears;

        /// <summary>
        /// Share of the run spent with the board crowded. This is what "boş yerim oldukça az"
        /// actually describes — the average fill can look calm while the player spends a third of
        /// the run with nowhere to put anything.
        /// </summary>
        public double TightShare => Samples == 0 ? 0 : Tight / (double)Samples;
    }

    static GameSession PlayOut(int seed, int botSeed, float skill, int cap, bool powers, Feel feel)
    {
        var s = new GameSession(new SessionConfig { Seed = seed, PowersEnabled = powers, PaletteSize = 7 });

        s.Moved += r =>
        {
            feel.Moves++;
            double fill = s.Board.OccupiedCount / (double)(s.Board.Size * s.Board.Size);
            feel.OccupancySum += fill;
            feel.Samples++;
            if (fill > 0.60) feel.Tight++;
            if (fill > feel.PeakOccupancy) feel.PeakOccupancy = fill;
            if (s.Board.OccupiedCount < feel.MinCells)
            {
                feel.MinCells = s.Board.OccupiedCount;

                // A wipe needs the survivors to share a line. Scattered across four rows, no tray
                // on earth can clear them without first filling four whole rows.
                int rows = 0, cols = 0;
                for (int i = 0; i < s.Board.Size; i++)
                {
                    if (s.Board.RowCount(i) > 0) rows++;
                    if (s.Board.ColumnCount(i) > 0) cols++;
                }
                feel.MinSpanRows = rows;
                feel.MinSpanCols = cols;
            }

            if (r.Clear == null || !r.Clear.Any) return;
            feel.Clears++;
            feel.StreakSum += r.ComboStreak;
            if (r.Clear.LinesCleared > 1) feel.MultiLine++;
            if (r.Clear.PerfectClear) feel.PerfectClears++;
            if (r.ComboStreak > feel.BestStreak) feel.BestStreak = r.ComboStreak;
        };

        var bot = new Autoplayer(botSeed, skill);
        int guard = 0;
        while (!s.IsFinished && s.MovesUsed < cap && guard++ < cap * 4)
            if (!bot.Step(s)) break;
        return s;
    }

    // ------------------------------------------------------------------ balance

    static void Balance(int runs)
    {
        const int cap = 3000;
        foreach (float skill in new[] { 0.40f, 0.55f, 0.80f })
        foreach (bool powers in new[] { false, true })
        {
            var moves = new List<int>();
            var lines = new List<int>();
            var scores = new List<int>();
            var feels = new List<Feel>();
            int capped = 0, powersUsed = 0;
            var sw = Stopwatch.StartNew();

            for (int r = 0; r < runs; r++)
            {
                var feel = new Feel();
                var s = PlayOut(1000 + r, 7 + r, skill, cap, powers, feel);
                feel.DeathOccupancy = s.Board.OccupiedCount / (double)(s.Board.Size * s.Board.Size);
                moves.Add(s.MovesUsed);
                lines.Add(s.LinesCleared);
                scores.Add(s.Score);
                feels.Add(feel);
                powersUsed += s.PowersUsed;
                if (!s.IsFinished) capped++;
            }

            sw.Stop();
            moves.Sort();
            Console.WriteLine($"skill {skill:0.00} powers {(powers ? "on " : "off")}: moves avg {moves.Average():0.0} med {moves[runs / 2]} max {moves.Max()}  " +
                              $"lines {lines.Average():0.0}  score {scores.Average():0}  powers/run {powersUsed / (float)runs:0.0}  capped {capped}  ({sw.ElapsedMilliseconds / runs} ms/run)");

            // The feel of it: how full the board sat, how often a move took more than one line,
            // how long a streak ever got, and whether the board ever came back to empty.
            Console.WriteLine($"                     board {feels.Average(f => f.Occupancy):P0} full (peak {feels.Average(f => f.PeakOccupancy):P0}, " +
                              $"crowded {feels.Average(f => f.TightShare):P0} of moves)  " +
                              $"multi-line {feels.Average(f => f.MultiLineRate):P1}");
            Console.WriteLine($"                     streak avg {feels.Average(f => f.AvgStreak):0.00} best {feels.Average(f => f.BestStreak):0.0} " +
                              $"(max {feels.Max(f => f.BestStreak)})  " +
                              $"board emptied {feels.Sum(f => f.PerfectClears)}x in {runs} runs " +
                              $"({feels.Count(f => f.PerfectClears > 0) / (float)runs:P0} of runs)  " +
                              $"board {feels.Average(f => f.DeathOccupancy):P0} full at the end  " +
                              $"emptiest {feels.Average(f => f.MinCells == int.MaxValue ? 0 : f.MinCells):0.0} cells " +
                              $"spread over {feels.Average(f => f.MinSpanRows):0.0} rows x {feels.Average(f => f.MinSpanCols):0.0} cols");
        }
    }

    // ------------------------------------------------------------------ perf

    /// <summary>
    /// What one deal costs, at the board states that matter.
    ///
    /// The dealer runs on the main thread in the middle of play, so this is frame time, not
    /// background time — and it is the one part of the game whose cost is allowed to grow with
    /// how much trouble the player is in. An open board is cheap; a crowded one rolls far more
    /// candidates, and that is exactly when a hitch would be felt.
    /// </summary>
    static void Perf()
    {
        Console.WriteLine("  fill   assist  candidates   ms/deal   alloc KB/deal");

        foreach (float fill in new[] { 0.0f, 0.25f, 0.50f, 0.70f, 0.85f })
        {
            var rng = new Rng(12345);
            var board = new BoardModel(8);
            FillTo(board, fill, rng);

            var dealer = new PieceDealer(new Rng(999));
            var tray = new[] { new TrayPiece(), new TrayPiece(), new TrayPiece() };

            // Warm up the scratch board and the JIT.
            for (int i = 0; i < 200; i++) dealer.Deal(board, tray, 7, 0);

            const int Deals = 2000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Deals; i++) dealer.Deal(board, tray, 7, 0);
            sw.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Console.WriteLine($"  {fill,4:P0}   {dealer.LastAssist,6:0.00}   {16 + (int)(dealer.LastAssist * 28),10}   " +
                              $"{sw.Elapsed.TotalMilliseconds / Deals,7:0.000}   {bytes / (double)Deals / 1024,13:0.00}");
        }

        Console.WriteLine();
        Console.WriteLine("  bir dagitim ~3 hamlede bir olur; 60 fps butcesi 16.7 ms/kare.");
    }

    static void FillTo(BoardModel board, float fill, Rng rng)
    {
        int want = (int)(board.Size * board.Size * fill);
        int guard = 0;
        while (board.OccupiedCount < want && guard++ < 5000)
        {
            int x = rng.Next(board.Size);
            int y = rng.Next(board.Size);
            if (!board.IsOccupied(x, y)) board.SetPrefill(x, y, rng.Next(7), false, 0);
        }
    }

    // ------------------------------------------------------------------ levels

    static void Levels(int from, int to)
    {
        Console.WriteLine(" lvl h goal    tgt  lim  pre ice sto shd tim til |  weak casual  good | gen ms");
        var sw = new Stopwatch();
        var byWorld = new Dictionary<int, (float weak, float casual, float good, int count)>();
        for (int n = from; n <= to; n++)
        {
            sw.Restart();
            var level = LevelGenerator.Generate(n, 7);
            sw.Stop();

            int ice = level.Prefill.Count(c => c.Ice > 0 && c.Ice < BoardModel.Stone);
            int stone = level.Prefill.Count(c => c.Ice == BoardModel.Stone);
            int shade = level.Prefill.Count(c => c.Color == BoardModel.Shade);
            int timers = level.Prefill.Count(c => c.Timer > 0);
            int tiles = level.Prefill.Count(c => c.Tile);

            float weak = WinRate(level, 0.40f, 12);
            float casual = WinRate(level, 0.50f, 12);
            float good = WinRate(level, 0.85f, 12);

            int world = LevelGenerator.WorldOf(n);
            byWorld.TryGetValue(world, out var w);
            byWorld[world] = (w.weak + weak, w.casual + casual, w.good + good, w.count + 1);

            string mark = level.Hardness == 2 ? "!!" : level.Hardness == 1 ? "! " : "  ";
            Console.WriteLine($"{n,4} {mark}{level.Goal,-6} {level.Target,4} {level.MoveLimit,4} {level.Prefill.Count,4} {ice,3} {stone,3} {shade,3} {timers,3} {tiles,3} |  {weak,4:P0} {casual,5:P0} {good,5:P0} | {sw.ElapsedMilliseconds}");
        }

        Console.WriteLine();
        Console.WriteLine("world   weak casual  good");
        foreach (var pair in byWorld.OrderBy(p => p.Key))
        {
            var w = pair.Value;
            Console.WriteLine($"{pair.Key + 1,5} {w.weak / w.count,6:P0} {w.casual / w.count,6:P0} {w.good / w.count,5:P0}");
        }
    }

    /// <summary>
    /// The daily puzzle across <paramref name="days"/> days from today: how hard each weekday
    /// really plays, for a struggling, a casual and a good player. A puzzle is retried until it is
    /// solved, so what matters is less the win rate than how many tries it takes on average.
    /// </summary>
    static void Daily(int days)
    {
        Console.WriteLine(" #    date       day tier goal   tgt  lim  pre ice |  weak casual  good | gen ms");
        var sw = new Stopwatch();
        var start = DateTime.Now.Date;
        var byGrade = new float[4, 3];
        var gradeCount = new int[4];

        for (int d = 0; d < days; d++)
        {
            var date = start.AddDays(d);
            sw.Restart();
            var puzzle = LevelGenerator.GenerateDaily(date, 7);
            sw.Stop();

            int ice = puzzle.Prefill.Count(c => c.Ice > 0);
            float weak = WinRate(puzzle, 0.40f, 12, daily: true);
            float casual = WinRate(puzzle, 0.55f, 12, daily: true);
            float good = WinRate(puzzle, 0.85f, 12, daily: true);

            int grade = LevelGenerator.DailyGrade(date.DayOfWeek);
            byGrade[grade, 0] += weak; byGrade[grade, 1] += casual; byGrade[grade, 2] += good;
            gradeCount[grade]++;

            Console.WriteLine($"{puzzle.Number,4} {date:yyyy-MM-dd} {date.DayOfWeek.ToString().Substring(0, 3)} {LevelGenerator.DailyTier(date.DayOfWeek),4} " +
                              $"{puzzle.Goal,-6} {puzzle.Target,4} {puzzle.MoveLimit,4} {puzzle.Prefill.Count,4} {ice,3} |  {weak,4:P0} {casual,5:P0} {good,5:P0} | {sw.ElapsedMilliseconds}");
        }

        Console.WriteLine();
        Console.WriteLine("grade      weak casual  good   (win rate per attempt)");
        string[] names = { "easy", "medium", "hard", "hardest" };
        for (int g = 0; g < 4; g++)
        {
            if (gradeCount[g] == 0) continue;
            float n = gradeCount[g];
            Console.WriteLine($"{names[g],-8} {byGrade[g, 0] / n,6:P0} {byGrade[g, 1] / n,6:P0} {byGrade[g, 2] / n,5:P0}");
        }
    }

    static float WinRate(LevelDefinition level, float skill, int runs, bool daily = false)
    {
        int wins = 0;
        for (int r = 0; r < runs; r++)
        {
            var s = daily ? GameSession.NewDailyRun(level, 8, 7) : GameSession.NewLevelRun(level, 8, 7);
            var bot = new Autoplayer(900 + r * 13, skill);
            int guard = 0;
            while (!s.IsFinished && guard++ < 600)
                if (!bot.Step(s)) break;
            if (s.State == SessionState.Won) wins++;
        }
        return wins / (float)runs;
    }
}
