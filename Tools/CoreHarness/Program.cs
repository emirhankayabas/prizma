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
            default: Console.WriteLine("usage: tests | balance [runs] | levels [from] [to]"); return 2;
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

    // ------------------------------------------------------------------ balance

    static void Balance(int runs)
    {
        const int cap = 3000;
        foreach (float skill in new[] { 0.55f, 0.80f })
        foreach (bool powers in new[] { false, true })
        {
            var moves = new List<int>();
            var lines = new List<int>();
            var scores = new List<int>();
            int capped = 0, powersUsed = 0;
            var sw = Stopwatch.StartNew();

            for (int r = 0; r < runs; r++)
            {
                var s = PlayOut(1000 + r, 7 + r, skill, cap, powers);
                moves.Add(s.MovesUsed);
                lines.Add(s.LinesCleared);
                scores.Add(s.Score);
                powersUsed += s.PowersUsed;
                if (!s.IsFinished) capped++;
            }

            sw.Stop();
            moves.Sort();
            Console.WriteLine($"skill {skill:0.00} powers {(powers ? "on " : "off")}: moves avg {moves.Average():0.0} med {moves[runs / 2]} max {moves.Max()}  " +
                              $"lines {lines.Average():0.0}  score {scores.Average():0}  powers/run {powersUsed / (float)runs:0.0}  capped {capped}  ({sw.ElapsedMilliseconds / runs} ms/run)");
        }
    }

    // ------------------------------------------------------------------ levels

    static void Levels(int from, int to)
    {
        Console.WriteLine(" lvl goal   tgt  lim  pre ice gem |  casual  good | gen ms");
        var sw = new Stopwatch();
        for (int n = from; n <= to; n++)
        {
            sw.Restart();
            var level = LevelGenerator.Generate(n, 7);
            sw.Stop();

            int ice = level.Prefill.Count(c => c.Ice > 0);
            int gem = level.Prefill.Count(c => c.Gem);

            float casual = WinRate(level, 0.50f, 12);
            float good = WinRate(level, 0.85f, 12);

            Console.WriteLine($"{n,4} {level.Goal,-6} {level.Target,4} {level.MoveLimit,4} {level.Prefill.Count,4} {ice,3} {gem,3} |  {casual,5:P0} {good,5:P0} | {sw.ElapsedMilliseconds}");
        }
    }

    static float WinRate(LevelDefinition level, float skill, int runs)
    {
        int wins = 0;
        for (int r = 0; r < runs; r++)
        {
            var s = GameSession.NewLevelRun(level, 8, 7);
            var bot = new Autoplayer(900 + r * 13, skill);
            int guard = 0;
            while (!s.IsFinished && guard++ < 600)
                if (!bot.Step(s)) break;
            if (s.State == SessionState.Won) wins++;
        }
        return wins / (float)runs;
    }
}
