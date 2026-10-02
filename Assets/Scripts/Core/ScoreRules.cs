namespace BlockPuzzle.Core
{
    /// <summary>
    /// Every number that shapes how the game feels to score, in one place so it can be tuned
    /// without touching the rules.
    /// </summary>
    public static class ScoreRules
    {
        /// <summary>Points for each cell of a piece just for placing it. Keeps the score always moving.</summary>
        public const int PointsPerPlacedCell = 1;

        /// <summary>Base points for every cell wiped by a line clear.</summary>
        public const int PointsPerClearedCell = 10;

        /// <summary>Extra multiplier added per line beyond the first in a single placement.</summary>
        public const float MultiLineBonusPerExtraLine = 1.0f;

        /// <summary>Extra multiplier added per consecutive clearing move.</summary>
        public const float ComboBonusPerStreak = 0.5f;

        /// <summary>Ceiling on the combo multiplier so late-game scores stay readable.</summary>
        public const float MaxComboMultiplier = 5.0f;

        /// <summary>
        /// A cleared line that was one colour end to end — the "prism" line. Worth chasing, since
        /// the tray's colours are random: the player has to notice two same-coloured pieces and
        /// build for them.
        /// </summary>
        public const int MonoLineBonus = 120;

        /// <summary>Emptying the whole board. Rare, and it should feel like it.</summary>
        public const int PerfectClearBonus = 600;

        /// <summary>Per cell a bomb removes. Half a line clear: the bomb buys room, not points.</summary>
        public const int PointsPerBlastedCell = 5;

        public static int PlacementScore(PieceShape shape) => shape.CellCount * PointsPerPlacedCell;

        /// <summary>
        /// Scores a clear. <paramref name="comboStreak"/> is 1 on the first clearing move of a run,
        /// 2 on the next consecutive one, and so on.
        /// </summary>
        public static int ClearScore(ClearResult clear, int comboStreak)
        {
            if (clear == null || !clear.Any) return 0;

            float multiLine = 1f + (clear.LinesCleared - 1) * MultiLineBonusPerExtraLine;
            float combo = ComboMultiplier(comboStreak);

            float score = clear.AffectedCells * PointsPerClearedCell * multiLine * combo;
            score += clear.MonoLines * MonoLineBonus * combo;
            if (clear.PerfectClear) score += PerfectClearBonus;

            return (int)score;
        }

        public static int BlastScore(ClearResult blast)
        {
            if (blast == null) return 0;
            return blast.ClearedCells.Count * PointsPerBlastedCell + (blast.PerfectClear ? PerfectClearBonus : 0);
        }

        public static float ComboMultiplier(int comboStreak)
        {
            if (comboStreak <= 1) return 1f;

            float multiplier = 1f + (comboStreak - 1) * ComboBonusPerStreak;
            return multiplier > MaxComboMultiplier ? MaxComboMultiplier : multiplier;
        }
    }

    /// <summary>
    /// The prism charge economy. Powers are earned by play, never bought: clearing lines fills the
    /// prism, a full prism is one charge, and charges are spent on powers.
    /// </summary>
    public static class PowerRules
    {
        public const int MaxCharges = 3;

        /// <summary>
        /// Lines needed for the first charge of a run. A single-colour line counts double.
        /// Every later charge asks for <see cref="ChargeStep"/> more lines than the one before.
        ///
        /// A flat price did not work: a strong player clears lines fast enough to refill the prism
        /// before the next jam, and a good player's run went three to four times longer than
        /// without powers — the powers were doing the dealer's job and taking the tension out.
        /// The rising price keeps them a rescue, not a way of life. See "Güçler" in CLAUDE.md.
        ///
        /// The first price used to be thirty, and measured, that put it out of reach of exactly
        /// the player it was meant to rescue: a struggling run clears about twenty lines in total,
        /// so it never earned a single charge and spent the whole run on the one it started with.
        /// So the first one is cheap now and the step is steeper — the shape the balance notes
        /// already pointed at. A weak run gets a second rescue; a strong one still pays more for
        /// every charge after that.
        /// </summary>
        public const int LinesPerCharge = 12;
        public const int ChargeStep = 26;

        /// <summary>Lines the next charge costs, given how many this run has already earned.</summary>
        public static int LinesForCharge(int earnedSoFar) => LinesPerCharge + ChargeStep * earnedSoFar;

        /// <summary>
        /// Charges an endless run starts with. With none, most casual players never cleared enough
        /// lines to earn a first charge and the powers only ever helped the players who needed
        /// them least. One was still not enough: a struggling run spent the whole game on that
        /// single charge, so the rescue existed once and then never again. Two, with the cheap
        /// first charge above, gives a weak run about three ways out of a jam and leaves a strong
        /// one almost unchanged — over four hundred moves, one extra charge is noise.
        /// </summary>
        public const int StartCharges = 2;

        /// <summary>Square radius of the bomb: 1 means a 3x3 blast.</summary>
        public const int BombRadius = 1;

        /// <summary>
        /// Moves a level sells when its budget runs out, and what they cost. Once per attempt, and
        /// a level finished on bought moves earns one star — a rescue, not a way to the top score.
        /// </summary>
        public const int ExtraMoves = 5;
        public const int ExtraMovesCost = 2;

        public static int Cost(PowerKind kind) => kind == PowerKind.Bomb ? 2 : kind == PowerKind.Hammer ? 0 : 1;
    }
}
