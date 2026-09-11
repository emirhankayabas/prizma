using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Renders one piece as a block of images laid out on the same pitch as the board, so a piece
    /// held over the grid lines up with it exactly.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        readonly List<Image> _blocks = new List<Image>();

        RectTransform _rect;
        float _cellSize;
        float _gap;

        public RectTransform Rect => _rect;
        public PieceShape Shape { get; private set; }
        public int ColorIndex { get; private set; }

        public float Pitch => _cellSize + _gap;

        public void Build(PieceShape shape, int colorIndex, Color color, float cellSize, float gap, bool glyphs = false)
        {
            Shape = shape;
            ColorIndex = colorIndex;
            _cellSize = cellSize;
            _gap = gap;

            _rect = GetComponent<RectTransform>();
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(
                shape.Width * cellSize + (shape.Width - 1) * gap,
                shape.Height * cellSize + (shape.Height - 1) * gap);

            foreach (var image in _blocks)
                Destroy(image.gameObject);
            _blocks.Clear();

            foreach (var cell in shape.Cells)
            {
                var image = UiBuilder.Image(_rect, $"B_{cell.X}_{cell.Y}", Art.Block, color);
                image.rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                image.rectTransform.anchoredPosition = LocalCellPosition(cell.X, cell.Y);
                _blocks.Add(image);

                if (glyphs)
                {
                    var glyph = UiBuilder.Image(image.rectTransform, "Glyph", Art.Glyph(colorIndex), new Color(0f, 0f, 0f, 0.30f));
                    glyph.type = Image.Type.Simple;
                    glyph.rectTransform.sizeDelta = new Vector2(cellSize * 0.34f, cellSize * 0.34f);
                }
            }
        }

        public void SetAlpha(float alpha)
        {
            foreach (var image in _blocks)
            {
                var c = image.color;
                image.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        /// <summary>Local position of a cell centre, relative to the piece's own centre.</summary>
        public Vector2 LocalCellPosition(int x, int y)
        {
            var size = _rect.sizeDelta;
            return new Vector2(
                -size.x * 0.5f + _cellSize * 0.5f + x * Pitch,
                size.y * 0.5f - _cellSize * 0.5f - y * Pitch);
        }

        /// <summary>
        /// Screen position of the piece's top-left cell centre. The drag controller maps this onto
        /// a board cell, which becomes the anchor the shape is placed from.
        /// </summary>
        public Vector2 OriginCellScreenPoint => _rect.TransformPoint(LocalCellPosition(0, 0));
    }
}
