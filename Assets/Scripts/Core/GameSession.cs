using System;

namespace BlockPuzzle.Core
{
    /// <summary>One piece sitting in the tray, waiting to be dragged out.</summary>
    public sealed class TrayPiece
    {
        public PieceShape Shape;
        public int ColorIndex;
        public bool Used;
    }

    /// <summary>Everything that happened during a single successful placement.</summary>
    public sealed class MoveResult
    {
        public ClearResult Clear;
        public int PlacementScore;
        public int ClearScore;
        public int ComboStreak;
        public bool TrayRefilled;
        public bool GameOver;

        /// <summary>What was placed and where, so the view can animate exactly those cells.</summary>
        public PieceShape PlacedShape;
        public int PlacedColumn;
        public int PlacedRow;
        public int PlacedColorIndex;

        public int TotalScore => PlacementScore + ClearScore;
    }

    /// <summary>
    /// Drives a run: owns the board, the three-piece tray, the score and the combo streak, and
    /// decides when the run is over. Fully deterministic for a given seed, which is what makes a
    /// shared daily challenge possible later.
    /// </summary>
    public sealed class GameSession
    {
        public const int TraySlots = 3;

        readonly Random _random;
        readonly PieceDealer _dealer;
        readonly int _paletteSize;

        public BoardModel Board { get; }
        public TrayPiece[] Tray { get; }
        public int Seed { get; }

        public int Score { get; private set; }
        public int ComboStreak { get; private set; }
        public bool IsGameOver { get; private set; }

        public event Action<MoveResult> Moved;
        public event Action TrayRefilled;
        public event Action GameOver;

        public GameSession(int seed, int boardSize = 8, int paletteSize = 6)
        {
            Seed = seed;
            _random = new Random(seed);
            _dealer = new PieceDealer(_random);
            _paletteSize = Math.Max(1, paletteSize);

            Board = new BoardModel(boardSize);
            Tray = new TrayPiece[TraySlots];
            for (int i = 0; i < TraySlots; i++)
                Tray[i] = new TrayPiece();

            FillTray();
        }

        public static GameSession NewRandomRun(int boardSize = 8, int paletteSize = 6)
            => new GameSession(Environment.TickCount, boardSize, paletteSize);

        /// <summary>Same seed for everyone on a given calendar day — the basis of the daily challenge.</summary>
        public static GameSession NewDailyRun(DateTime utcDate, int boardSize = 8, int paletteSize = 6)
            => new GameSession(utcDate.Year * 10000 + utcDate.Month * 100 + utcDate.Day, boardSize, paletteSize);

        public bool CanPlace(int trayIndex, int col, int row)
        {
            if (IsGameOver) return false;
            var piece = GetPlayablePiece(trayIndex);
            return piece != null && Board.CanPlace(piece.Shape, col, row);
        }

        /// <summary>
        /// Places a tray piece. Returns null when the move is illegal, so callers can treat a
        /// rejected drop as "snap the piece back" without exception handling.
        /// </summary>
        public MoveResult TryPlace(int trayIndex, int col, int row)
        {
            var piece = GetPlayablePiece(trayIndex);
            if (piece == null || IsGameOver) return null;
            if (!Board.CanPlace(piece.Shape, col, row)) return null;

            var clear = Board.Place(piece.Shape, col, row, piece.ColorIndex);
            piece.Used = true;

            // The streak advances only while consecutive placements keep clearing lines.
            ComboStreak = clear.Any ? ComboStreak + 1 : 0;

            var result = new MoveResult
            {
                Clear = clear,
                PlacementScore = ScoreRules.PlacementScore(piece.Shape),
                ClearScore = ScoreRules.ClearScore(clear, ComboStreak),
                ComboStreak = ComboStreak,
                PlacedShape = piece.Shape,
                PlacedColumn = col,
                PlacedRow = row,
                PlacedColorIndex = piece.ColorIndex
            };

            Score += result.TotalScore;

            if (IsTrayEmpty())
            {
                FillTray();
                result.TrayRefilled = true;
            }

            result.GameOver = !HasAnyLegalMove();

            Moved?.Invoke(result);

            if (result.TrayRefilled)
                TrayRefilled?.Invoke();

            if (result.GameOver)
            {
                IsGameOver = true;
                GameOver?.Invoke();
            }

            return result;
        }

        /// <summary>True while at least one unused tray piece still fits somewhere.</summary>
        public bool HasAnyLegalMove()
        {
            for (int i = 0; i < Tray.Length; i++)
            {
                var piece = Tray[i];
                if (!piece.Used && Board.HasAnyPlacement(piece.Shape))
                    return true;
            }

            return false;
        }

        TrayPiece GetPlayablePiece(int trayIndex)
        {
            if (trayIndex < 0 || trayIndex >= Tray.Length) return null;
            var piece = Tray[trayIndex];
            return piece.Used ? null : piece;
        }

        bool IsTrayEmpty()
        {
            for (int i = 0; i < Tray.Length; i++)
                if (!Tray[i].Used) return false;
            return true;
        }

        /// <summary>Assist applied to the most recent deal, 0..1. Surfaced for tuning and tests.</summary>
        public float LastAssist => _dealer.LastAssist;

        void FillTray() => _dealer.Deal(Board, Tray, _paletteSize, Score);
    }
}
