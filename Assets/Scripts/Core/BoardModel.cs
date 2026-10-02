using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>What a single placement (or a blast) did to the board.</summary>
    public sealed class ClearResult
    {
        public readonly List<int> ClearedRows = new List<int>();
        public readonly List<int> ClearedColumns = new List<int>();

        /// <summary>Every cell that got wiped, de-duplicated across intersecting rows and columns.</summary>
        public readonly List<CellOffset> ClearedCells = new List<CellOffset>();

        /// <summary>Crystals freed by this clear. Each one also appears in <see cref="ClearedCells"/>.</summary>
        public readonly List<CellOffset> CollectedGems = new List<CellOffset>();

        /// <summary>Iced cells whose ice took a hit. They stay on the board, one layer thinner.</summary>
        public readonly List<CellOffset> CrackedIce = new List<CellOffset>();

        /// <summary>The colour each of <see cref="ClearedCells"/> had, in the same order. Shade is <see cref="BoardModel.Shade"/>.</summary>
        public readonly List<int> ClearedColors = new List<int>();

        /// <summary>Glow tiles this clear put out. Each one also appears in <see cref="ClearedCells"/>.</summary>
        public readonly List<CellOffset> CollectedTiles = new List<CellOffset>();

        /// <summary>Timer blocks taken before their clock ran out.</summary>
        public readonly List<CellOffset> DefusedTimers = new List<CellOffset>();

        /// <summary>Shade blocks among <see cref="ClearedCells"/>.</summary>
        public int ClearedShade;

        /// <summary>Cleared lines that were a single colour end to end.</summary>
        public int MonoLines;

        /// <summary>Which of the cleared rows and columns were single-colour, for the view.</summary>
        public readonly List<int> MonoRows = new List<int>();
        public readonly List<int> MonoColumns = new List<int>();

        /// <summary>The clear left the board completely empty.</summary>
        public bool PerfectClear;

        /// <summary>
        /// Empties this result so it can be filled again. The dealer plays out tens of candidate
        /// trays per deal and only ever reads <see cref="LinesCleared"/>; letting it hand the same
        /// instance back in kept a hundred-odd of these off the heap every deal.
        /// </summary>
        public void Reset()
        {
            ClearedRows.Clear();
            ClearedColumns.Clear();
            ClearedCells.Clear();
            CollectedGems.Clear();
            CrackedIce.Clear();
            ClearedColors.Clear();
            CollectedTiles.Clear();
            DefusedTimers.Clear();
            ClearedShade = 0;
            MonoRows.Clear();
            MonoColumns.Clear();
            MonoLines = 0;
            PerfectClear = false;
        }

        public int LinesCleared => ClearedRows.Count + ClearedColumns.Count;
        public bool Any => LinesCleared > 0;

        /// <summary>Cells the clear touched in any way — wiped or cracked. This is what scores.</summary>
        public int AffectedCells => ClearedCells.Count + CrackedIce.Count;
    }

    /// <summary>
    /// The playfield. Pure logic, no Unity types, so the rules can be unit tested and the view can
    /// stay a dumb renderer. Cells hold a colour index, or <see cref="Empty"/> when free.
    ///
    /// Two optional layers sit on top of occupied cells, used by the level mode:
    /// a crystal (collected when its cell is cleared) and ice (each clear knocks off one layer,
    /// and the block underneath only goes once the ice is gone).
    ///
    /// Per-row and per-column occupancy tallies are maintained as cells change. The dealer probes
    /// thousands of hypothetical placements per tray, and rescanning the grid for each one was the
    /// difference between a deal costing microseconds and costing a visible stutter.
    /// </summary>
    public sealed class BoardModel
    {
        public const int Empty = -1;

        /// <summary>
        /// The ice layer value that marks a stone. A stone is a block that counts towards filling
        /// its row and column but is never cleared by them — only the bomb breaks it. Stored in the
        /// ice layer so saves, the dealer and the computer player all handle it unchanged.
        /// </summary>
        public const int Stone = 9;

        public bool IsStone(int col, int row) => InBounds(col, row) && _ice[col, row] >= Stone;

        /// <summary>
        /// The cell value of a shade block: a colourless block that fills its lines and clears like
        /// any other, but creeps into a free neighbour when the player leaves it alone (the session
        /// decides when). Stored as a cell value rather than a layer, so saves, the dealer and the
        /// computer player carry it unchanged — and it can never make a single-colour line.
        /// </summary>
        public const int Shade = -2;

        public bool IsShade(int col, int row) => InBounds(col, row) && _cells[col, row] == Shade;

        readonly int[,] _cells;
        readonly bool[,] _gems;
        readonly int[,] _ice;
        readonly bool[,] _tiles;
        readonly int[,] _timers;

        // Where shade could creep to, gathered without allocating.
        readonly int[] _spreadScratch;
        readonly int[] _rowCount;
        readonly int[] _colCount;

        // Scratch buffers for line counting, reused so probing allocates nothing.
        readonly int[] _rowAdd;
        readonly int[] _colAdd;

        /// <summary>
        /// Which cells a clear is about to wipe. This used to be a <c>HashSet</c> built from
        /// scratch on every resolved placement — including every one of the hundred-odd
        /// simulated placements the dealer makes per deal — which is where most of the garbage
        /// in a move was coming from.
        /// </summary>
        readonly bool[,] _wiped;

        public int Size { get; }

        public BoardModel(int size = 8)
        {
            if (size < 2) throw new ArgumentOutOfRangeException(nameof(size), "Board must be at least 2x2.");

            Size = size;
            _cells = new int[size, size];
            _gems = new bool[size, size];
            _ice = new int[size, size];
            _tiles = new bool[size, size];
            _timers = new int[size, size];
            _spreadScratch = new int[size * size];
            _rowCount = new int[size];
            _colCount = new int[size];
            _rowAdd = new int[size];
            _colAdd = new int[size];
            _wiped = new bool[size, size];

            Clear();
        }

        public int OccupiedCount { get; private set; }

        /// <summary>Crystals still on the board.</summary>
        public int GemCount { get; private set; }

        /// <summary>Glow tiles still lit.</summary>
        public int TileCount { get; private set; }

        /// <summary>Shade blocks on the board.</summary>
        public int ShadeCount { get; private set; }

        /// <summary>Timer blocks still ticking.</summary>
        public int TimerCount { get; private set; }

        public void Clear()
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                _cells[x, y] = Empty;

            Array.Clear(_gems, 0, _gems.Length);
            Array.Clear(_ice, 0, _ice.Length);
            Array.Clear(_tiles, 0, _tiles.Length);
            Array.Clear(_timers, 0, _timers.Length);
            Array.Clear(_rowCount, 0, Size);
            Array.Clear(_colCount, 0, Size);
            OccupiedCount = 0;
            GemCount = 0;
            TileCount = 0;
            ShadeCount = 0;
            TimerCount = 0;
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
            Array.Copy(other._gems, _gems, _gems.Length);
            Array.Copy(other._ice, _ice, _ice.Length);
            Array.Copy(other._tiles, _tiles, _tiles.Length);
            Array.Copy(other._timers, _timers, _timers.Length);
            Array.Copy(other._rowCount, _rowCount, Size);
            Array.Copy(other._colCount, _colCount, Size);
            OccupiedCount = other.OccupiedCount;
            GemCount = other.GemCount;
            TileCount = other.TileCount;
            ShadeCount = other.ShadeCount;
            TimerCount = other.TimerCount;
        }

        public bool InBounds(int col, int row) => col >= 0 && col < Size && row >= 0 && row < Size;

        public int GetCell(int col, int row)
        {
            if (!InBounds(col, row)) throw new ArgumentOutOfRangeException($"Cell ({col},{row}) is off the board.");
            return _cells[col, row];
        }

        public bool IsOccupied(int col, int row) => _cells[col, row] != Empty;

        public bool HasGem(int col, int row) => InBounds(col, row) && _gems[col, row];

        /// <summary>Layers of ice on a cell; 0 for none.</summary>
        public int IceAt(int col, int row) => InBounds(col, row) ? _ice[col, row] : 0;

        public bool HasTile(int col, int row) => InBounds(col, row) && _tiles[col, row];

        /// <summary>Moves left on a timer block's clock; 0 when the cell has none.</summary>
        public int TimerAt(int col, int row) => InBounds(col, row) ? _timers[col, row] : 0;

        /// <summary>Lays or lifts a glow tile. Tiles belong to the floor, so an empty cell can hold one.</summary>
        public void SetTile(int col, int row, bool tile)
        {
            if (!InBounds(col, row) || _tiles[col, row] == tile) return;
            _tiles[col, row] = tile;
            TileCount += tile ? 1 : -1;
        }

        void SetTimer(int col, int row, int moves)
        {
            moves = Math.Max(0, moves);
            if (_timers[col, row] > 0) TimerCount--;
            _timers[col, row] = moves;
            if (moves > 0) TimerCount++;
        }

        /// <summary>
        /// One move passes: every timer block's clock goes down by one. Returns true when one of
        /// them reached zero — the run is lost unless the same move met the level's goal.
        /// </summary>
        public bool TickTimers()
        {
            if (TimerCount == 0) return false;

            bool burst = false;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (_timers[x, y] <= 0) continue;
                _timers[x, y]--;
                if (_timers[x, y] == 0)
                {
                    TimerCount--;
                    burst = true;
                }
            }

            return burst;
        }

        /// <summary>
        /// Shade creeps into one free cell beside it, chosen by <paramref name="rng"/>. Returns the
        /// cell it took, or (-1, -1) when it is boxed in or there is none.
        /// </summary>
        public CellOffset SpreadShade(Rng rng)
        {
            if (ShadeCount == 0) return new CellOffset(-1, -1);

            int count = 0;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (_cells[x, y] != Empty) continue;
                if (IsShade(x - 1, y) || IsShade(x + 1, y) || IsShade(x, y - 1) || IsShade(x, y + 1))
                    _spreadScratch[count++] = y * Size + x;
            }

            if (count == 0) return new CellOffset(-1, -1);

            int pick = _spreadScratch[rng.Next(count)];
            int px = pick % Size, py = pick / Size;
            SetCell(px, py, Shade);
            return new CellOffset(px, py);
        }

        public int RowCount(int row) => _rowCount[row];
        public int ColumnCount(int col) => _colCount[col];

        /// <summary>
        /// Puts a pre-placed block on the board — how a level lays out its starting position and
        /// how a saved run is restored. Crystals and ice only ever sit on an occupied cell.
        /// </summary>
        public void SetPrefill(int col, int row, int colorIndex, bool gem = false, int ice = 0, int timer = 0)
        {
            if (!InBounds(col, row)) throw new ArgumentOutOfRangeException($"Cell ({col},{row}) is off the board.");
            if (colorIndex < 0 && colorIndex != Shade) throw new ArgumentOutOfRangeException(nameof(colorIndex), "Prefill needs a colour.");

            SetCell(col, row, colorIndex);

            if (_gems[col, row] != gem)
            {
                _gems[col, row] = gem;
                GemCount += gem ? 1 : -1;
            }

            _ice[col, row] = Math.Max(0, ice);
            SetTimer(col, row, timer);
        }

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

        /// <summary>Crystals sitting in the given rows and columns, each counted once.</summary>
        public int CountGemsInLines(List<int> rows, List<int> columns)
        {
            if (GemCount == 0) return 0;

            int gems = 0;
            foreach (int y in rows)
                for (int x = 0; x < Size; x++)
                    if (_gems[x, y]) gems++;

            foreach (int x in columns)
                for (int y = 0; y < Size; y++)
                    if (_gems[x, y] && !rows.Contains(y)) gems++;

            return gems;
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
        public ClearResult Place(PieceShape shape, int col, int row, int colorIndex) =>
            Place(shape, col, row, colorIndex, null);

        /// <summary>
        /// Places a piece and resolves whatever it completes.
        ///
        /// Pass <paramref name="reuse"/> to have the result written into an instance you own
        /// instead of a fresh one. Only do that when nothing keeps the result past the call: the
        /// game hands its <see cref="ClearResult"/> to animations that outlive the frame, so it
        /// takes a new one every move, while the dealer — which throws tens of them away per deal
        /// and reads a single integer off each — hands back the same one every time.
        /// </summary>
        public ClearResult Place(PieceShape shape, int col, int row, int colorIndex, ClearResult reuse)
        {
            if (!CanPlace(shape, col, row))
                throw new InvalidOperationException($"{shape} does not fit at ({col},{row}).");

            foreach (var cell in shape.Cells)
                SetCell(col + cell.X, row + cell.Y, colorIndex);

            return ResolveLines(reuse);
        }

        /// <summary>
        /// Wipes every block in the square of the given radius around a cell — ice, crystals and
        /// all. The bomb power. Does not complete lines; it only makes room.
        /// </summary>
        public ClearResult Blast(int col, int row, int radius)
        {
            var result = new ClearResult();

            for (int y = row - radius; y <= row + radius; y++)
            for (int x = col - radius; x <= col + radius; x++)
            {
                if (!InBounds(x, y) || _cells[x, y] == Empty) continue;

                _ice[x, y] = 0;
                Wipe(x, y, result);
            }

            result.PerfectClear = result.ClearedCells.Count > 0 && OccupiedCount == 0;
            return result;
        }

        /// <summary>
        /// Takes a block off the board and writes down everything that went with it: its colour,
        /// its crystal, the glow tile under it, a timer it was carrying.
        /// </summary>
        void Wipe(int x, int y, ClearResult result)
        {
            var cell = new CellOffset(x, y);
            int colour = _cells[x, y];

            if (_gems[x, y])
            {
                _gems[x, y] = false;
                GemCount--;
                result.CollectedGems.Add(cell);
            }

            if (_tiles[x, y])
            {
                _tiles[x, y] = false;
                TileCount--;
                result.CollectedTiles.Add(cell);
            }

            if (_timers[x, y] > 0)
            {
                SetTimer(x, y, 0);
                result.DefusedTimers.Add(cell);
            }

            if (colour == Shade) result.ClearedShade++;

            SetCell(x, y, Empty);
            result.ClearedCells.Add(cell);
            result.ClearedColors.Add(colour);
        }

        void SetCell(int x, int y, int value)
        {
            int previous = _cells[x, y];
            if (previous == value) return;

            if (previous == Shade) ShadeCount--;
            if (value == Shade) ShadeCount++;

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

        bool RowIsMono(int y)
        {
            int colour = _cells[0, y];
            if (colour < 0) return false;
            for (int x = 1; x < Size; x++)
                if (_cells[x, y] != colour) return false;
            return true;
        }

        bool ColumnIsMono(int x)
        {
            int colour = _cells[x, 0];
            if (colour < 0) return false;
            for (int y = 1; y < Size; y++)
                if (_cells[x, y] != colour) return false;
            return true;
        }

        ClearResult ResolveLines(ClearResult reuse)
        {
            ClearResult result;
            if (reuse == null)
            {
                result = new ClearResult();
            }
            else
            {
                result = reuse;
                result.Reset();
            }

            for (int y = 0; y < Size; y++)
                if (_rowCount[y] == Size) result.ClearedRows.Add(y);

            for (int x = 0; x < Size; x++)
                if (_colCount[x] == Size) result.ClearedColumns.Add(x);

            if (!result.Any) return result;

            // Colour is judged before anything is wiped, while the full line is still there.
            foreach (int y in result.ClearedRows)
                if (RowIsMono(y)) result.MonoRows.Add(y);
            foreach (int x in result.ClearedColumns)
                if (ColumnIsMono(x)) result.MonoColumns.Add(x);
            result.MonoLines = result.MonoRows.Count + result.MonoColumns.Count;

            // A mask rather than a set: crossing rows and columns must not wipe a cell twice, and
            // marking a flag is both cheaper and allocation-free next to hashing a struct.
            Array.Clear(_wiped, 0, _wiped.Length);

            foreach (int y in result.ClearedRows)
            for (int x = 0; x < Size; x++)
                _wiped[x, y] = true;

            foreach (int x in result.ClearedColumns)
            for (int y = 0; y < Size; y++)
                _wiped[x, y] = true;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (!_wiped[x, y]) continue;

                // Ice absorbs the clear: the block stays and only the ice gets thinner. A cell on a
                // crossing row and column still loses a single layer — it was hit by one move.
                // Stone takes no hit at all: the line still clears around it. Only the bomb moves it.
                if (_ice[x, y] >= Stone) continue;

                if (_ice[x, y] > 0)
                {
                    _ice[x, y]--;
                    result.CrackedIce.Add(new CellOffset(x, y));
                    continue;
                }

                Wipe(x, y, result);
            }

            result.PerfectClear = OccupiedCount == 0;
            return result;
        }
    }
}
