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

            if (session.State == SessionState.OutOfMoves)
            {
                if (session.TryBuyMoves()) return true;
                session.Concede();
                return false;
            }

            if (session.State != SessionState.Playing) return false;

            _moves.Clear();
            var board = session.Board;

            // A player chasing a goal builds towards the lines that hold it, not only takes it when
            // a clear happens to pass through. Without this the computer freed crystals by accident,
            // and budgets sized from it were both loose and erratic. Every goal and every obstacle
            // is one weight per cell: what clearing that cell is worth.
            bool targeted = BuildWeights(session);

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
                    if (targeted)
                    {
                        if (lines > 0) value += WeightInLines(board.Size);
                        value += CellsOnTargetLines(shape, col, row) * 10f;
                        if (_orderColor >= 0 && piece.ColorIndex == _orderColor) value += CellsOnTargetLines(shape, col, row) * 4f;
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

        float[,] _weight = new float[0, 0];
        bool[] _targetRows = new bool[0];
        bool[] _targetCols = new bool[0];
        int _orderColor = -1;

        /// <summary>
        /// What clearing each cell is worth to this level: its crystal, its glow tile, a block of the
        /// order colour, shade, and — most of all — a timer about to run out. False when nothing on
        /// the board is worth more than any other cell, which is the whole of a classic run.
        /// </summary>
        bool BuildWeights(GameSession session)
        {
            var board = session.Board;
            var level = session.Level;
            int n = board.Size;
            if (level == null && board.TimerCount == 0) return false;

            if (_targetRows.Length != n)
            {
                _weight = new float[n, n];
                _targetRows = new bool[n];
                _targetCols = new bool[n];
            }

            System.Array.Clear(_targetRows, 0, n);
            System.Array.Clear(_targetCols, 0, n);

            var goal = level != null ? level.Goal : GoalKind.Score;
            _orderColor = goal == GoalKind.Colors && level != null ? level.OrderColor : -1;
            bool any = false;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float w = 0f;
                int cell = board.GetCell(x, y);

                if (board.HasGem(x, y)) w += goal == GoalKind.Gems ? 160f : 30f;
                if (goal == GoalKind.Tiles && board.HasTile(x, y)) w += 150f;
                if (_orderColor >= 0 && cell == _orderColor) w += 60f;
                if (cell == BoardModel.Shade) w += goal == GoalKind.Shade ? 150f : 40f;

                int timer = board.TimerAt(x, y);
                if (timer > 0) w += 120f + 900f / timer;

                _weight[x, y] = w;
                if (w > 0f)
                {
                    _targetRows[y] = true;
                    _targetCols[x] = true;
                    any = true;
                }
            }

            return any;
        }

        /// <summary>The worth of every cell in the lines just found, each cell counted once.</summary>
        float WeightInLines(int n)
        {
            float total = 0f;
            foreach (int y in _rows)
                for (int x = 0; x < n; x++)
                    total += _weight[x, y];

            foreach (int x in _cols)
                for (int y = 0; y < n; y++)
                    if (!_rows.Contains(y)) total += _weight[x, y];

            return total;
        }

        int CellsOnTargetLines(PieceShape shape, int col, int row)
        {
            int count = 0;
            foreach (var cell in shape.Cells)
            {
                if (_targetRows[row + cell.Y]) count++;
                if (_targetCols[col + cell.X]) count++;
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
                    value += board.HasGem(x, y) || board.TimerAt(x, y) > 0 ? 4 : board.IsStone(x, y) ? 3 : 1;
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
