using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Draws the grid and translates between screen points and board coordinates.
    /// It holds no rules: <see cref="GameSession"/> decides what is legal, this only shows it.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        RectTransform _rect;
        RectTransform _cellRoot;
        RectTransform _ghostRoot;
        RectTransform _previewRoot;
        RectTransform _sweepRoot;
        RectTransform _burstRoot;

        Image[,] _cells;
        Image[,] _glyphs;
        Image[,] _gems;
        Image[,] _ice;

        readonly List<int> _previewRows = new List<int>();
        readonly List<int> _previewColumns = new List<int>();
        readonly List<Image> _previewBars = new List<Image>();
        readonly List<Image> _previewGlows = new List<Image>();
        Color _previewTint = Color.white;

        readonly List<Image> _ghostPool = new List<Image>();
        readonly List<Image> _burstPool = new List<Image>();
        readonly List<Image> _sweepPool = new List<Image>();

        BoardModel _board;
        Image _edge;
        Image _ring;
        Coroutine _pulse;
        bool _colorBlind;

        int _size;
        float _cellSize;
        float _gap;
        float _content;
        Vector2 _origin;

        static readonly Color EmptyCellColor = Design.SurfaceInset;
        static readonly Color GlyphColor = new Color(0f, 0f, 0f, 0.30f);
        static readonly Color BombHot = new Color(1f, 0.55f, 0.28f, 0.38f);
        static readonly Color BombCold = new Color(1f, 0.55f, 0.28f, 0.12f);

        public float CellSize => _cellSize;
        public float Gap => _gap;
        public float Pitch => _cellSize + _gap;
        public RectTransform Rect => _rect;

        /// <summary>
        /// Creates the grid once. The model is attached separately with <see cref="Bind"/> so a new
        /// run reuses the same cells instead of rebuilding the hierarchy.
        /// </summary>
        public void Build(int boardSize, float boardPixelSize, float padding, float gap)
        {
            _size = boardSize;
            _gap = gap;

            _rect = GetComponent<RectTransform>();
            _rect.sizeDelta = new Vector2(boardPixelSize, boardPixelSize);

            var size = new Vector2(boardPixelSize, boardPixelSize);

            UiBuilder.Shadow(_rect, "Shadow", size, Design.RadiusLg, Design.E1);

            var surface = gameObject.AddComponent<Image>();
            surface.sprite = Art.Panel(Design.RadiusLg);
            surface.type = Image.Type.Sliced;
            surface.color = Design.BoardSurface;
            surface.raycastTarget = false;

            _content = boardPixelSize - padding * 2f;
            _cellSize = (_content - gap * (_size - 1)) / _size;
            _origin = new Vector2(
                -_content * 0.5f + _cellSize * 0.5f,
                _content * 0.5f - _cellSize * 0.5f);

            _cellRoot = UiBuilder.Child(_rect, "Cells");
            // Above the cells so it lights them, below the ghost so the held piece stays readable.
            _previewRoot = UiBuilder.Child(_rect, "LinePreview");
            _ghostRoot = UiBuilder.Child(_rect, "Ghosts");
            _sweepRoot = UiBuilder.Child(_rect, "Sweeps");
            _burstRoot = UiBuilder.Child(_rect, "Bursts");

            _cells = new Image[_size, _size];
            _glyphs = new Image[_size, _size];
            _gems = new Image[_size, _size];
            _ice = new Image[_size, _size];

            for (int row = 0; row < _size; row++)
            for (int col = 0; col < _size; col++)
            {
                var image = UiBuilder.Image(_cellRoot, $"Cell_{col}_{row}", Art.Cell, EmptyCellColor);
                image.rectTransform.sizeDelta = new Vector2(_cellSize, _cellSize);
                image.rectTransform.anchoredPosition = CellAnchoredPosition(col, row);
                _cells[col, row] = image;

                // Overlays ride on the cell, so the landing pop and the clear bursts carry them too.
                // Order matters: the ice sits over the crystal, so a crystal under ice reads as trapped.
                _glyphs[col, row] = Overlay(image, "Glyph", Art.Glyph(0), GlyphColor, 0.34f, sliced: false);
                _gems[col, row] = Overlay(image, "Crystal", Art.Crystal, Design.Crystal, 0.66f, sliced: false);
                _ice[col, row] = Overlay(image, "Ice", Art.Ice(1), Design.Ice, 1f, sliced: true);
            }

            _ring = UiBuilder.Image(_burstRoot, "Ring", Art.SoftCircle, Color.white);
            _ring.type = Image.Type.Simple;
            _ring.gameObject.SetActive(false);

            // The rim is the board's one piece of reactive decoration: each clear pulses it in the
            // colour of the piece that caused it, then it settles back to a plain hairline.
            _edge = UiBuilder.Hairline(_rect, "Edge", size, Design.RadiusLg);
        }

        Image Overlay(Image cell, string name, Sprite sprite, Color color, float scale, bool sliced)
        {
            var overlay = UiBuilder.Image(cell.rectTransform, name, sprite, color);
            overlay.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            overlay.rectTransform.sizeDelta = new Vector2(_cellSize * scale, _cellSize * scale);
            overlay.gameObject.SetActive(false);
            return overlay;
        }

        public void Bind(BoardModel board)
        {
            _board = board;
            HideGhost();
            HideLinePreview();
            Refresh();
        }

        public void SetColorBlind(bool on)
        {
            if (_colorBlind == on) return;
            _colorBlind = on;
            Refresh();
        }

        public Vector2 CellAnchoredPosition(int col, int row)
            => new Vector2(_origin.x + col * Pitch, _origin.y - row * Pitch);

        /// <summary>World (overlay: screen) position of a cell's centre.</summary>
        public Vector3 CellWorldPosition(int col, int row) => _cells[col, row].rectTransform.position;

        /// <summary>Repaints every cell from the model. Cheap enough at 8x8 to just do wholesale.</summary>
        public void Refresh()
        {
            if (_board == null) return;

            for (int row = 0; row < _size; row++)
            for (int col = 0; col < _size; col++)
            {
                int value = _board.GetCell(col, row);
                var image = _cells[col, row];

                // Clears any scale left behind by an interrupted pop animation.
                image.rectTransform.localScale = Vector3.one;

                if (value == BoardModel.Empty)
                {
                    image.sprite = Art.Cell;
                    image.color = EmptyCellColor;
                }
                else
                {
                    image.sprite = Art.Block;
                    image.color = Design.Blocks[value % Design.Blocks.Length];
                }

                image.type = Image.Type.Sliced;

                bool glyph = _colorBlind && value != BoardModel.Empty;
                _glyphs[col, row].gameObject.SetActive(glyph);
                if (glyph) _glyphs[col, row].sprite = Art.Glyph(value);

                _gems[col, row].gameObject.SetActive(_board.HasGem(col, row));

                int ice = _board.IceAt(col, row);
                _ice[col, row].gameObject.SetActive(ice > 0);
                if (ice > 0) _ice[col, row].sprite = Art.Ice(ice);
            }
        }

        /// <summary>
        /// Converts a screen point to the cell containing it. Returns false when the point is off
        /// the grid; the coordinates are still written, so callers can let CanPlace do the judging.
        /// </summary>
        public bool TryGetCellFromScreenPoint(Vector2 screenPoint, out int col, out int row)
        {
            col = row = -1;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, null, out var local))
                return false;

            col = Mathf.RoundToInt((local.x - _origin.x) / Pitch);
            row = Mathf.RoundToInt((_origin.y - local.y) / Pitch);

            return col >= 0 && col < _size && row >= 0 && row < _size;
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
            => RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, null);

        public void ShowGhost(PieceShape shape, int col, int row, bool valid)
        {
            HideGhost();
            if (shape == null) return;

            var tint = valid ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 0.35f, 0.45f, 0.18f);

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                int x = col + shape.Cells[i].X;
                int y = row + shape.Cells[i].Y;
                if (x < 0 || x >= _size || y < 0 || y >= _size) continue;

                PlaceGhost(x, y, tint);
            }
        }

        /// <summary>The bomb's 3x3 footprint under the finger, warm where it would remove a block.</summary>
        public void ShowBombPreview(int col, int row)
        {
            HideGhost();
            if (_board == null) return;

            int r = PowerRules.BombRadius;
            for (int y = row - r; y <= row + r; y++)
            for (int x = col - r; x <= col + r; x++)
            {
                if (x < 0 || x >= _size || y < 0 || y >= _size) continue;
                PlaceGhost(x, y, _board.IsOccupied(x, y) ? BombHot : BombCold);
            }
        }

        void PlaceGhost(int x, int y, Color tint)
        {
            var ghost = RentGhost();
            ghost.color = tint;
            ghost.rectTransform.sizeDelta = new Vector2(_cellSize, _cellSize);
            ghost.rectTransform.anchoredPosition = CellAnchoredPosition(x, y);
            ghost.gameObject.SetActive(true);
        }

        public void HideGhost()
        {
            for (int i = 0; i < _ghostPool.Count; i++)
                _ghostPool[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Tints whole rows and columns the held piece would complete, before it is dropped.
        ///
        /// Deliberately quiet: a steady wash, no pulse, no label. The player should register it
        /// out of the corner of their eye and work out what it means on their own — a blinking
        /// highlight with a caption reads as the game playing itself.
        /// Returns how many lines are previewed.
        /// </summary>
        public int PreviewLines(PieceShape shape, int col, int row)
        {
            HideLinePreview();
            if (_board == null || shape == null) return 0;

            _board.GetCompletedLines(shape, col, row, _previewRows, _previewColumns);

            int lines = _previewRows.Count + _previewColumns.Count;
            if (lines == 0) return 0;

            _previewTint = Design.PreviewTint(lines);

            foreach (int r in _previewRows)
                AddPreviewBar(new Vector2(0f, _origin.y - r * Pitch), new Vector2(_content, _cellSize));

            foreach (int c in _previewColumns)
                AddPreviewBar(new Vector2(_origin.x + c * Pitch, 0f), new Vector2(_cellSize, _content));

            return lines;
        }

        void AddPreviewBar(Vector2 position, Vector2 size)
        {
            // Low alpha and a tight bleed. Enough to notice, not enough to announce itself.
            var glow = RentPreview(soft: true);
            glow.rectTransform.sizeDelta = size + new Vector2(30f, 30f);
            glow.rectTransform.anchoredPosition = position;
            glow.color = _previewTint.WithAlpha(0.13f);
            glow.gameObject.SetActive(true);

            var bar = RentPreview(soft: false);
            bar.rectTransform.sizeDelta = size;
            bar.rectTransform.anchoredPosition = position;
            bar.color = _previewTint.WithAlpha(0.22f);
            bar.gameObject.SetActive(true);
        }

        public void HideLinePreview()
        {
            for (int i = 0; i < _previewBars.Count; i++) _previewBars[i].gameObject.SetActive(false);
            for (int i = 0; i < _previewGlows.Count; i++) _previewGlows[i].gameObject.SetActive(false);

            _previewRows.Clear();
            _previewColumns.Clear();
        }

        Image RentPreview(bool soft)
        {
            var pool = soft ? _previewGlows : _previewBars;

            for (int i = 0; i < pool.Count; i++)
                if (!pool[i].gameObject.activeSelf)
                    return pool[i];

            var image = soft
                ? UiBuilder.Image(_previewRoot, "PreviewGlow", Art.SoftSquare, Color.white)
                : UiBuilder.Image(_previewRoot, "PreviewBar", Art.Panel(Design.RadiusSm), Color.white);

            if (soft) image.type = Image.Type.Simple;
            image.gameObject.SetActive(false);
            pool.Add(image);
            return image;
        }

        /// <summary>Bounces the cells a piece just filled, so a drop reads as landed, not appeared.</summary>
        public void PlayPlacePop(PieceShape shape, int col, int row)
        {
            if (shape == null) return;

            for (int i = 0; i < shape.Cells.Length; i++)
            {
                int x = col + shape.Cells[i].X;
                int y = row + shape.Cells[i].Y;
                if (x < 0 || x >= _size || y < 0 || y >= _size) continue;

                StartCoroutine(Tween.Scale(_cells[x, y].transform, Vector3.one * 0.62f, Vector3.one,
                    0.19f, Ease.OutBack, i * 0.016f));
            }
        }

        /// <summary>Sweeps a soft bar along every cleared row and column.</summary>
        public void PlayLineSweep(ClearResult clear, Color tint)
        {
            if (clear == null || !clear.Any) return;

            foreach (int row in clear.ClearedRows) Sweep(row, horizontal: true, tint, 0.7f, 0f);
            foreach (int col in clear.ClearedColumns) Sweep(col, horizontal: false, tint, 0.7f, 0f);
        }

        /// <summary>
        /// A single-colour line splits into every block colour in turn, like light through a prism.
        /// The one flourish that belongs to this game and no other.
        /// </summary>
        public void PlayPrismSweep(ClearResult clear)
        {
            if (clear == null || clear.MonoLines == 0) return;

            var colours = Design.Blocks;
            for (int k = 0; k < colours.Length; k++)
            {
                float delay = 0.07f + k * 0.055f;
                foreach (int row in clear.MonoRows) Sweep(row, horizontal: true, colours[k], 0.55f, delay);
                foreach (int col in clear.MonoColumns) Sweep(col, horizontal: false, colours[k], 0.55f, delay);
            }
        }

        void Sweep(int index, bool horizontal, Color tint, float alpha, float delay)
        {
            var sweep = RentSweep();
            sweep.color = tint.WithAlpha(0f);
            if (horizontal)
            {
                sweep.rectTransform.sizeDelta = new Vector2(_content, _cellSize);
                sweep.rectTransform.anchoredPosition = new Vector2(0f, _origin.y - index * Pitch);
            }
            else
            {
                sweep.rectTransform.sizeDelta = new Vector2(_cellSize, _content);
                sweep.rectTransform.anchoredPosition = new Vector2(_origin.x + index * Pitch, 0f);
            }

            sweep.gameObject.SetActive(true);
            StartCoroutine(SweepRoutine(sweep, horizontal, tint, alpha, delay));
        }

        IEnumerator SweepRoutine(Image sweep, bool horizontal, Color color, float alpha, float delay)
        {
            var rect = sweep.rectTransform;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float grow = Mathf.Lerp(0.2f, 1f, Ease.OutCubic(k));
                rect.localScale = horizontal ? new Vector3(grow, 1f, 1f) : new Vector3(1f, grow, 1f);
                sweep.color = color.WithAlpha(Mathf.Lerp(alpha, 0f, Ease.InQuad(k)));
                yield return null;
            }

            rect.localScale = Vector3.one;
            sweep.gameObject.SetActive(false);
        }

        /// <summary>Pops a short flash on every cell a clear wiped.</summary>
        public void PlayClearBurst(IReadOnlyList<CellOffset> cells, Color tint)
        {
            for (int i = 0; i < cells.Count; i++)
                Burst(cells[i].X, cells[i].Y, tint, 0.8f, 1.85f, i * 0.011f);
        }

        /// <summary>A small cold flash on each iced cell that took a hit.</summary>
        public void PlayIceCrack(IReadOnlyList<CellOffset> cells)
        {
            for (int i = 0; i < cells.Count; i++)
                Burst(cells[i].X, cells[i].Y, Design.Ice, 0.9f, 1.35f, 0.05f + i * 0.02f);
        }

        /// <summary>A diagonal wave of light across the whole board — the perfect clear.</summary>
        public void PlayBoardWave()
        {
            for (int row = 0; row < _size; row++)
            for (int col = 0; col < _size; col++)
                Burst(col, row, Design.Gold, 0.55f, 1.5f, (col + row) * 0.03f);
        }

        /// <summary>The bomb: every blasted cell flashes and a soft shock ring spreads from the centre.</summary>
        public void PlayBlast(ClearResult blast, int col, int row)
        {
            var warm = new Color(1f, 0.72f, 0.4f, 1f);
            if (blast != null)
                for (int i = 0; i < blast.ClearedCells.Count; i++)
                    Burst(blast.ClearedCells[i].X, blast.ClearedCells[i].Y, warm, 0.9f, 2.1f, i * 0.012f);

            StartCoroutine(RingRoutine(CellAnchoredPosition(col, row)));
            PulseEdge(warm);
        }

        IEnumerator RingRoutine(Vector2 at)
        {
            var rect = _ring.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(_cellSize * 3f, _cellSize * 3f);
            _ring.gameObject.SetActive(true);
            _ring.transform.SetAsLastSibling();

            const float duration = 0.42f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float s = Mathf.Lerp(0.4f, 2.4f, Ease.OutCubic(k));
                rect.localScale = new Vector3(s, s, 1f);
                _ring.color = new Color(1f, 0.8f, 0.55f, Mathf.Lerp(0.75f, 0f, k));
                yield return null;
            }

            _ring.gameObject.SetActive(false);
        }

        void Burst(int col, int row, Color tint, float alpha, float growTo, float delay)
        {
            var burst = RentBurst();
            burst.color = tint.WithAlpha(0f);
            burst.rectTransform.sizeDelta = new Vector2(_cellSize, _cellSize);
            burst.rectTransform.anchoredPosition = CellAnchoredPosition(col, row);
            burst.rectTransform.localScale = Vector3.one;
            burst.gameObject.SetActive(true);
            StartCoroutine(BurstRoutine(burst, tint, alpha, growTo, delay));
        }

        IEnumerator BurstRoutine(Image burst, Color color, float alpha, float growTo, float delay)
        {
            var rect = burst.rectTransform;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            const float duration = 0.3f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float scale = Mathf.Lerp(1f, growTo, Ease.OutCubic(k));
                rect.localScale = new Vector3(scale, scale, 1f);
                burst.color = color.WithAlpha(Mathf.Lerp(alpha, 0f, k));
                yield return null;
            }

            rect.localScale = Vector3.one;
            burst.gameObject.SetActive(false);
        }

        /// <summary>Flashes the board rim, then settles back to its resting hairline.</summary>
        public void PulseEdge(Color color)
        {
            if (_edge == null) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine(color));
        }

        IEnumerator PulseRoutine(Color color)
        {
            var hot = color.WithAlpha(0.85f);

            const float up = 0.08f;
            for (float t = 0f; t < up; t += Time.unscaledDeltaTime)
            {
                _edge.color = Color.Lerp(Design.Hairline, hot, t / up);
                yield return null;
            }

            const float down = 0.6f;
            for (float t = 0f; t < down; t += Time.unscaledDeltaTime)
            {
                _edge.color = Color.Lerp(hot, Design.Hairline, Ease.OutCubic(t / down));
                yield return null;
            }

            _edge.color = Design.Hairline;
            _pulse = null;
        }

        Image RentGhost()
        {
            for (int i = 0; i < _ghostPool.Count; i++)
                if (!_ghostPool[i].gameObject.activeSelf)
                    return _ghostPool[i];

            var image = UiBuilder.Image(_ghostRoot, "Ghost", Art.Cell, Color.white);
            image.gameObject.SetActive(false);
            _ghostPool.Add(image);
            return image;
        }

        Image RentBurst()
        {
            for (int i = 0; i < _burstPool.Count; i++)
                if (!_burstPool[i].gameObject.activeSelf)
                    return _burstPool[i];

            var image = UiBuilder.Image(_burstRoot, "Burst", Art.SoftSquare, Color.white);
            image.type = Image.Type.Simple;
            image.gameObject.SetActive(false);
            _burstPool.Add(image);
            return image;
        }

        Image RentSweep()
        {
            for (int i = 0; i < _sweepPool.Count; i++)
                if (!_sweepPool[i].gameObject.activeSelf)
                    return _sweepPool[i];

            var image = UiBuilder.Image(_sweepRoot, "Sweep", Art.SoftSquare, Color.white);
            image.type = Image.Type.Simple;
            image.gameObject.SetActive(false);
            _sweepPool.Add(image);
            return image;
        }
    }
}
