using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>What a single placement did to the board.</summary>
    public sealed class ClearResult
    {
        public readonly List<int> ClearedRows = new List<int>();
        public readonly List<int> ClearedColumns = new List<int>();

        /// <summary>Every cell that got wiped, de-duplicated across intersecting rows and columns.</summary>
        public readonly List<CellOffset> ClearedCells = new List<CellOffset>();

        public int LinesCleared => ClearedRows.Count + ClearedColumns.Count;
        public bool Any => LinesCleared > 0;
    }

    /// <summary>
    /// The playfield. Pure logic, no Unity types, so the rules can be unit tested and the view can
    /// stay a dumb renderer. Cells hold a colour index, or <see cref="Empty"/> when free.
    ///
    /// Per-row and per-column occupancy tallies are maintained as cells change. The dealer probes
    /// thousands of hypothetical placements per tray, and rescanning the grid for each one was the
    /// difference between a deal costing microseconds and costing a visible stutter.
    /// </summary>
    public sealed class BoardModel
    {
        public const int Empty = -1;

        readonly int[,] _cells;
        readonly int[] _rowCount;
        readonly int[] _colCount;

        // Scratch buffers for line counting, reused so probing allocates nothing.
        readonly int[] _rowAdd;
        readonly int[] _colAdd;

        public int Size { get; }

        public BoardModel(int size = 8)
        {
            if (size < 2) throw new ArgumentOutOfRangeException(nameof(size), "Board must be at least 2x2.");

            Size = size;
            _cells = new int[size, size];
            _rowCount = new int[size];
            _colCount = new int[size];
            _rowAdd = new int[size];
            _colAdd = new int[size];

            Clear();
        }

        public int OccupiedCount { get; private set; }

        public void Clear()
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                _cells[x, y] = Empty;

            Array.Clear(_rowCount, 0, Size);
            Array.Clear(_colCount, 0, Size);
            OccupiedCount = 0;
        }

        /// <summary>A detached copy, for looking ahead without touching the live board.</summary>
        public BoardModel Clone()
        {
            var copy = new BoardModel(Size);
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(BoardModel other)
        {
            if (other == null || other.Size != Size)
                throw new ArgumentException("Boards must be the same size to copy.", nameof(other));

            Array.Copy(other._cells, _cells, _cells.Length);
            Array.Copy(other._rowCount, _rowCount, Size);
            Array.Copy(other._colCount, _colCount, Size);
            OccupiedCount = other.OccupiedCount;
        }

        public bool InBounds(int col, int row) => col >= 0 && col < Size && row >= 0 && row < Size;

        public int GetCell(int col, int row)
        {
            if (!InBounds(col, row)) throw new ArgumentOutOfRangeException($"Cell ({col},{row}) is off the board.");
            return _cells[col, row];
        }

        public bool IsOccupied(int col, int row) => _cells[col, row] != Empty;

        public int RowCount(int row) => _rowCount[row];
        public int ColumnCount(int col) => _colCount[col];

        /// <summary>True when every cell of the shape lands in bounds and on a free cell.</summary>
        public bool CanPlace(PieceShape shape, int col, int row)
        {
            if (shape == null) return false;
            if (col < 0 || row < 0 || col + shape.Width > Size || row + shape.Height > Size) return false;

            foreach (var cell in shape.Cells)
                if (_cells[col + cell.X, row + cell.Y] != Empty) return false;

            return true;
        }

        /// <summary>True when the shape fits anywhere at all. This is what decides game over.</summary>
        public bool HasAnyPlacement(PieceShape shape)
        {
            if (shape == null) return false;

            int maxCol = Size - shape.Width;
            int maxRow = Size - shape.Height;

            for (int row = 0; row <= maxRow; row++)
            for (int col = 0; col <= maxCol; col++)
                if (CanPlace(shape, col, row))
                    return true;

            return false;
        }

        /// <summary>
        /// How many lines a placement would complete, without mutating anything.
        /// Costs a handful of operations thanks to the maintained tallies.
        /// </summary>
        public int CountCompletedLines(PieceShape shape, int col, int row)
        {
            foreach (var cell in shape.Cells)
            {
                _rowAdd[row + cell.Y]++;
                _colAdd[col + cell.X]++;
            }

            int lines = 0;

            for (int y = row; y < row + shape.Height; y++)
            {
                if (_rowAdd[y] > 0 && _rowCount[y] + _rowAdd[y] == Size) lines++;
                _rowAdd[y] = 0;
            }

            for (int x = col; x < col + shape.Width; x++)
            {
                if (_colAdd[x] > 0 && _colCount[x] + _colAdd[x] == Size) lines++;
                _colAdd[x] = 0;
            }

            return lines;
        }

        /// <summary>
        /// Which rows and columns a placement would complete. Uses the same tallies as
        /// <see cref="CountCompletedLines"/>, but reports the lines themselves so the view can
        /// point at them before the player commits to the drop.
        /// </summary>
        public void GetCompletedLines(PieceShape shape, int col, int row, List<int> rows, List<int> columns)
        {
            rows.Clear();
            columns.Clear();

            foreach (var cell in shape.Cells)
            {
                _rowAdd[row + cell.Y]++;
                _colAdd[col + cell.X]++;
            }

            for (int y = row; y < row + shape.Height; y++)
            {
                if (_rowAdd[y] > 0 && _rowCount[y] + _rowAdd[y] == Size) rows.Add(y);
                _rowAdd[y] = 0;
            }

            for (int x = col; x < col + shape.Width; x++)
            {
                if (_colAdd[x] > 0 && _colCount[x] + _colAdd[x] == Size) columns.Add(x);
                _colAdd[x] = 0;
            }
        }

        /// <summary>
        /// How many of a piece's edges would touch an occupied cell or a wall at this position.
        /// Snug placements score higher, which is what "fits nicely" means numerically.
        /// </summary>
        public int ContactScore(PieceShape shape, int col, int row)
        {
            int contact = 0;

            foreach (var cell in shape.Cells)
            {
                int x = col + cell.X;
                int y = row + cell.Y;

                contact += EdgeContact(shape, col, row, x - 1, y);
                contact += EdgeContact(shape, col, row, x + 1, y);
                contact += EdgeContact(shape, col, row, x, y - 1);
                contact += EdgeContact(shape, col, row, x, y + 1);
            }

            return contact;
        }

        int EdgeContact(PieceShape shape, int col, int row, int x, int y)
        {
            if (!InBounds(x, y)) return 1; // a wall counts as support
            if (shape.Contains(x - col, y - row)) return 0; // the piece touching itself does not
            return _cells[x, y] != Empty ? 1 : 0;
        }

        /// <summary>
        /// Stamps the shape onto the board and resolves any full rows and columns.
        /// Lines are detected before anything is wiped, so a placement that completes a row and a
        /// column at the same time scores both.
        /// </summary>
        public ClearResult Place(PieceShape shape, int col, int row, int colorIndex)
        {
            if (!CanPlace(shape, col, row))
                throw new InvalidOperationException($"{shape} does not fit at ({col},{row}).");

            foreach (var cell in shape.Cells)
                SetCell(col + cell.X, row + cell.Y, colorIndex);

            return ResolveLines();
        }

        void SetCell(int x, int y, int value)
        {
            int previous = _cells[x, y];
            if (previous == value) return;

            if (previous == Empty)
            {
                _rowCount[y]++;
                _colCount[x]++;
                OccupiedCount++;
            }
            else if (value == Empty)
            {
                _rowCount[y]--;
                _colCount[x]--;
                OccupiedCount--;
            }

            _cells[x, y] = value;
        }

        ClearResult ResolveLines()
        {
            var result = new ClearResult();

            for (int y = 0; y < Size; y++)
                if (_rowCount[y] == Size) result.ClearedRows.Add(y);

            for (int x = 0; x < Size; x++)
                if (_colCount[x] == Size) result.ClearedColumns.Add(x);

            if (!result.Any) return result;

            var wiped = new HashSet<CellOffset>();

            foreach (int y in result.ClearedRows)
            for (int x = 0; x < Size; x++)
                wiped.Add(new CellOffset(x, y));

            foreach (int x in result.ClearedColumns)
            for (int y = 0; y < Size; y++)
                wiped.Add(new CellOffset(x, y));

            foreach (var cell in wiped)
            {
                SetCell(cell.X, cell.Y, Empty);
                result.ClearedCells.Add(cell);
            }

            return result;
        }
    }
}
