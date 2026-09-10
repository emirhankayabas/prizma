using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>A single cell offset inside a piece. X is the column, Y is the row (0 = top).</summary>
    public readonly struct CellOffset : IEquatable<CellOffset>
    {
        public readonly int X;
        public readonly int Y;

        public CellOffset(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(CellOffset other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is CellOffset other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>
    /// Immutable cell layout of a placeable piece, normalized so its bounding box starts at (0,0).
    /// Pieces never rotate at runtime — every rotation the game uses is authored as its own shape,
    /// which is what keeps the Block Blast style of planning ahead meaningful.
    /// </summary>
    public sealed class PieceShape
    {
        public readonly string Id;
        public readonly int Width;
        public readonly int Height;
        public readonly CellOffset[] Cells;

        /// <summary>Occupancy grid over the bounding box, so membership is a lookup, not a search.</summary>
        readonly bool[,] _mask;

        public int CellCount => Cells.Length;

        public PieceShape(string id, CellOffset[] cells)
        {
            if (cells == null || cells.Length == 0)
                throw new ArgumentException("A piece needs at least one cell.", nameof(cells));

            Id = id;
            Cells = Normalize(cells, out Width, out Height);

            _mask = new bool[Width, Height];
            foreach (var cell in Cells)
                _mask[cell.X, cell.Y] = true;
        }

        /// <summary>Whether the piece covers this offset from its own top-left corner.</summary>
        public bool Contains(int localX, int localY)
        {
            if (localX < 0 || localX >= Width || localY < 0 || localY >= Height) return false;
            return _mask[localX, localY];
        }

        /// <summary>
        /// Builds a shape from a visual pattern, one string per row, top row first.
        /// Any character other than '.' or ' ' counts as a filled cell, so "XX." and "##." both work.
        /// </summary>
        public static PieceShape FromPattern(string id, params string[] rows)
        {
            if (rows == null || rows.Length == 0)
                throw new ArgumentException("A pattern needs at least one row.", nameof(rows));

            var cells = new List<CellOffset>();
            for (int y = 0; y < rows.Length; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c != '.' && c != ' ')
                        cells.Add(new CellOffset(x, y));
                }
            }

            return new PieceShape(id, cells.ToArray());
        }

        static CellOffset[] Normalize(CellOffset[] cells, out int width, out int height)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.X < minX) minX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.X > maxX) maxX = c.X;
                if (c.Y > maxY) maxY = c.Y;
            }

            width = maxX - minX + 1;
            height = maxY - minY + 1;

            var normalized = new CellOffset[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                normalized[i] = new CellOffset(cells[i].X - minX, cells[i].Y - minY);

            return normalized;
        }

        public override string ToString() => $"{Id} [{Width}x{Height}, {CellCount} cells]";
    }
}
