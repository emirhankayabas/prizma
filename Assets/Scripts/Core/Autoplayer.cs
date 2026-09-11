using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// A computer player. Used to size the move budget of generated levels, to measure the balance
    /// of the rules, and to drive automated test runs.
    ///
    /// <see cref="Skill"/> is the chance of taking its best-looking move; otherwise it takes a
    /// legal move at random, which is a fair model of a casual player who mostly plays well and
    /// sometimes drops a piece wherever it goes.
    /// </summary>
    public sealed class Autoplayer
    {
        readonly Rng _rng;
        readonly List<Move> _moves = new List<Move>(256);
        readonly List<int> _rows = new List<int>(8);
        readonly List<int> _cols = new List<int>(8);

        public float Skill { get; }

        struct Move
        {
            public int Slot;
            public int Col;
            public int Row;
            public float Value;
        }

        public Autoplayer(int seed, float skill)
        {
            _rng = new Rng(seed);
            Skill = skill;
        }

        /// <summary>Makes one move, or uses one power when stuck. Returns false once nothing can be done.</summary>
        public bool Step(GameSession session)
        {
            if (session.State == SessionState.Stuck) return UsePower(session);
            if (session.State != SessionState.Playing) return false;

            _moves.Clear();
            var board = session.Board;
            bool wantsGems = session.Level != null && session.Level.Goal == GoalKind.Gems;

            // A player chasing crystals builds towards the lines that hold them, not only takes
            // them when a clear happens to pass through. Without this the computer freed crystals
            // by accident, and budgets sized from it were both loose and erratic.
            if (wantsGems) MarkGemLines(board);

            for (int slot = 0; slot < session.Tray.Length; slot++)
            {
                var piece = session.Tray[slot];
                if (piece.Used) continue;

                var shape = piece.Shape;
                for (int row = 0; row <= board.Size - shape.Height; row++)
                for (int col = 0; col <= board.Size - shape.Width; col++)
                {
                    if (!board.CanPlace(shape, col, row)) continue;

                    board.GetCompletedLines(shape, col, row, _rows, _cols);
                    int lines = _rows.Count + _cols.Count;

                    float value = lines * 100f + board.ContactScore(shape, col, row) * 4f + shape.CellCount * 2f;
                    if (wantsGems)
                    {
                        if (lines > 0) value += board.CountGemsInLines(_rows, _cols) * 160f;
                        value += CellsOnGemLines(shape, col, row) * 10f;
                    }

                    // Tiny noise so equal moves are not always resolved the same way.
                    value += (float)_rng.NextDouble();

                    _moves.Add(new Move { Slot = slot, Col = col, Row = row, Value = value });
                }
            }

            if (_moves.Count == 0) return false;

            Move chosen;
            if (_rng.NextDouble() < Skill)
            {
                chosen = _moves[0];
                for (int i = 1; i < _moves.Count; i++)
                    if (_moves[i].Value > chosen.Value) chosen = _moves[i];
            }
            else
            {
                chosen = _moves[_rng.Next(_moves.Count)];
            }

            return session.TryPlace(chosen.Slot, chosen.Col, chosen.Row) != null;
        }

        bool[] _gemRows = new bool[0];
        bool[] _gemCols = new bool[0];

        void MarkGemLines(BoardModel board)
        {
            if (_gemRows.Length != board.Size)
            {
                _gemRows = new bool[board.Size];
                _gemCols = new bool[board.Size];
            }

            System.Array.Clear(_gemRows, 0, _gemRows.Length);
            System.Array.Clear(_gemCols, 0, _gemCols.Length);

            for (int y = 0; y < board.Size; y++)
            for (int x = 0; x < board.Size; x++)
                if (board.HasGem(x, y))
                {
                    _gemRows[y] = true;
                    _gemCols[x] = true;
                }
        }

        int CellsOnGemLines(PieceShape shape, int col, int row)
        {
            int count = 0;
            foreach (var cell in shape.Cells)
            {
                if (_gemRows[row + cell.Y]) count++;
                if (_gemCols[col + cell.X]) count++;
            }

            return count;
        }

        bool UsePower(GameSession session)
        {
            // Cheapest rescue first: a rotation that makes a piece fit.
            if (session.CanUsePower(PowerKind.Rotate))
            {
                for (int slot = 0; slot < session.Tray.Length; slot++)
                {
                    var rotated = session.RotatedShape(slot);
                    if (rotated != null && session.Board.HasAnyPlacement(rotated))
                        return session.TryRotate(slot) != null;
                }
            }

            if (session.CanUsePower(PowerKind.Bomb) && TryBestBomb(session)) return true;

            if (session.CanReroll()) return session.TryReroll() != null;

            session.Concede();
            return false;
        }

        bool TryBestBomb(GameSession session)
        {
            var board = session.Board;
            int bestCol = -1, bestRow = -1, best = 0;

            for (int row = 0; row < board.Size; row++)
            for (int col = 0; col < board.Size; col++)
            {
                int value = 0;
                for (int y = row - 1; y <= row + 1; y++)
                for (int x = col - 1; x <= col + 1; x++)
                {
                    if (!board.InBounds(x, y) || !board.IsOccupied(x, y)) continue;
                    value += board.HasGem(x, y) ? 4 : 1;
                }

                if (value > best)
                {
                    best = value;
                    bestCol = col;
                    bestRow = row;
                }
            }

            return bestCol >= 0 && session.TryBomb(bestCol, bestRow) != null;
        }
    }
}
