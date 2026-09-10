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

            return (int)(clear.ClearedCells.Count * PointsPerClearedCell * multiLine * combo);
        }

        public static float ComboMultiplier(int comboStreak)
        {
            if (comboStreak <= 1) return 1f;

            float multiplier = 1f + (comboStreak - 1) * ComboBonusPerStreak;
            return multiplier > MaxComboMultiplier ? MaxComboMultiplier : multiplier;
        }
    }
}
