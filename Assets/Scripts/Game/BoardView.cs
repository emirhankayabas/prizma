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
        RectTransform _clearRoot;
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
        readonly List<Image> _clearPool = new List<Image>();

        // The clear currently playing out: the blocks, the colour each started from, and when each
        // begins. Reused lists, so a clear allocates nothing beyond the pool's first growth.
        readonly List<Image> _clearShards = new List<Image>();
        readonly List<Color> _clearFrom = new List<Color>();
        readonly List<float> _clearDelays = new List<float>();
        Coroutine _clearRoutine;

        // Debris a clear throws off: small pieces of the blocks it took, flung out from the drop
        // and falling away. One pool and one routine for all of them.
        sealed class Shard
        {
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            public float Life;
            public float Size;
            public Color Color;
        }

        const int MaxShards = 72;
        const float ShardGravity = -2600f;

        readonly List<Shard> _shards = new List<Shard>();
        readonly List<Shard> _shardPool = new List<Shard>();
        RectTransform _shardRoot;
        Coroutine _shardRoutine;
        uint _shardSeed = 2463534242u;

        Image _wave;
        Coroutine _shake;
        Vector2 _shakeHome;

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

        static Color EmptyCellColor => Design.SurfaceInset;
        static readonly Color GlyphColor = new Color(0f, 0f, 0f, 0.30f);
        static readonly Color BombHot = new Color(1f, 0.55f, 0.28f, 0.5f);
        static readonly Color BombCold = new Color(1f, 0.55f, 0.28f, 0.16f);

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
            // The blocks a clear is taking out, drawn over the cells they came from so they can
            // finish their exit after the model has already emptied them.
            _clearRoot = UiBuilder.Child(_rect, "ClearOut");
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

            _wave = UiBuilder.Image(_burstRoot, "Wave", Art.SoftCircle, Color.white);
            _wave.type = Image.Type.Simple;
            _wave.gameObject.SetActive(false);

            // Debris moves every frame for most of a second; on its own canvas that re-batches a
            // few dozen quads instead of the whole board with them.
            _shardRoot = UiBuilder.Child(_rect, "Shards");
            _shardRoot.gameObject.AddComponent<Canvas>();

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
            HideClearOut();
            HideShards();
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

                // A stone is a grey block under a dark frost: no block colour, so it never reads as
                // part of a single-colour line the player could be building.
                bool stone = _board.IsStone(col, row);
                if (stone) image.color = Design.Stone;

                bool glyph = _colorBlind && value != BoardModel.Empty && !stone;
                _glyphs[col, row].gameObject.SetActive(glyph);
                if (glyph) _glyphs[col, row].sprite = Art.Glyph(value);

                _gems[col, row].gameObject.SetActive(_board.HasGem(col, row));

                int ice = _board.IceAt(col, row);
                _ice[col, row].gameObject.SetActive(ice > 0);
                if (ice > 0)
                {
                    _ice[col, row].sprite = Art.Ice(stone ? 2 : ice);
                    _ice[col, row].color = stone ? Design.StoneFrost : Design.Ice;
                }
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
            //
            // Widening the bleed to spill light past the board's rim was tried and measured off a
            // screenshot: it changes nothing. The board's own padding swallows it, and at 0.13 the
            // soft square's outer edge is already below one value of 255 by the time it gets
            // there. The two alphas below are the only real loudness knob; making the preview
            // easier to catch means moving them, and that is a judgement to make on a phone.
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

        /// <summary>
        /// Plays the blocks a clear is about to remove out on their own: each one flares to light,
        /// swells a little, then collapses to nothing, radiating from the cell the piece landed on.
        ///
        /// Must be called *before* <see cref="Refresh"/>, while the model still holds the blocks —
        /// the effect copies their colour off the cells. Without it the blocks simply disappeared
        /// on the refresh and every flourish that followed (the sweep, the bursts) played over
        /// cells that were already empty, so a clear read as something happening *above* the board
        /// rather than to it. This is the one animation the player is actually looking at.
        /// </summary>
        public void PlayClearOut(IReadOnlyList<CellOffset> cells, int fromCol, int fromRow, Color placed)
        {
            if (cells == null || cells.Count == 0) return;

            // One clear supersedes another: a combo can land the next drop before this finishes.
            HideClearOut();

            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i].X;
                int y = cells[i].Y;
                if (x < 0 || x >= _size || y < 0 || y >= _size) continue;

                var colour = BlockColour(x, y, placed);

                var shard = RentClear();
                shard.sprite = Art.Block;
                shard.type = Image.Type.Sliced;
                shard.color = colour;

                var rect = shard.rectTransform;
                rect.sizeDelta = new Vector2(_cellSize, _cellSize);
                rect.anchoredPosition = CellAnchoredPosition(x, y);
                rect.localScale = Vector3.one;
                shard.gameObject.SetActive(true);

                _clearShards.Add(shard);
                _clearFrom.Add(colour);

                // Radiating from the drop rather than firing at once ties the light to the move
                // the player just made. Capped so a full row never trails behind its own sound.
                _clearDelays.Add(Mathf.Min((Mathf.Abs(x - fromCol) + Mathf.Abs(y - fromRow)) * 0.017f, 0.15f));
            }

            if (_clearShards.Count > 0) _clearRoutine = StartCoroutine(ClearOutRoutine());
        }

        /// <summary>
        /// One routine for the whole clear rather than one per block: a single handle to stop, and
        /// a single coroutine's worth of garbage on a move that can light up sixteen cells.
        /// </summary>
        IEnumerator ClearOutRoutine()
        {
            const float flare = 0.07f;
            const float collapse = 0.17f;

            float longest = 0f;
            for (int i = 0; i < _clearDelays.Count; i++) longest = Mathf.Max(longest, _clearDelays[i]);
            float total = longest + flare + collapse;

            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < _clearShards.Count; i++)
                {
                    float local = t - _clearDelays[i];
                    if (local < 0f) continue;

                    var shard = _clearShards[i];
                    var rect = shard.rectTransform;

                    // Keeps a trace of the block's own colour on the way to white, so a clear
                    // still reads as this theme's clear rather than a generic flash.
                    var lit = Color.Lerp(_clearFrom[i], Color.white, 0.82f);

                    if (local < flare)
                    {
                        float k = local / flare;
                        shard.color = Color.Lerp(_clearFrom[i], lit, k);
                        float s = Mathf.Lerp(1f, 1.1f, Ease.OutQuad(k));
                        rect.localScale = new Vector3(s, s, 1f);
                    }
                    else
                    {
                        float k = Mathf.Clamp01((local - flare) / collapse);
                        float s = Mathf.Lerp(1.1f, 0.12f, Ease.OutCubic(k));
                        rect.localScale = new Vector3(s, s, 1f);
                        shard.color = lit.WithAlpha(1f - Ease.InQuad(k));
                    }
                }

                yield return null;
            }

            _clearRoutine = null;
            HideClearOut();
        }

        /// <summary>
        /// Drops any blocks still on their way out. A run can be replaced while a clear is playing
        /// — "play again" straight off a finished board — and without this the old board's last
        /// line would go on dissolving over the new one.
        /// </summary>
        public void HideClearOut()
        {
            if (_clearRoutine != null)
            {
                StopCoroutine(_clearRoutine);
                _clearRoutine = null;
            }

            for (int i = 0; i < _clearShards.Count; i++)
            {
                _clearShards[i].rectTransform.localScale = Vector3.one;
                _clearShards[i].gameObject.SetActive(false);
            }

            _clearShards.Clear();
            _clearFrom.Clear();
            _clearDelays.Clear();
        }

        Image RentClear()
        {
            for (int i = 0; i < _clearPool.Count; i++)
                if (!_clearPool[i].gameObject.activeSelf)
                    return _clearPool[i];

            var image = UiBuilder.Image(_clearRoot, "ClearShard", Art.Block, Color.white);
            image.gameObject.SetActive(false);
            _clearPool.Add(image);
            return image;
        }

        // ------------------------------------------------------------------ debris

        /// <summary>
        /// Throws small pieces of the cleared blocks out from the drop, to fall away under gravity.
        /// Like <see cref="PlayClearOut"/> it must run before <see cref="Refresh"/>: the pieces
        /// take their colour from the blocks. <paramref name="perCell"/> grows with the size of the
        /// move, <paramref name="power"/> with the streak — a long combo throws harder.
        /// </summary>
        public void PlayShards(IReadOnlyList<CellOffset> cells, int fromCol, int fromRow, int perCell, float power, Color placed)
        {
            if (cells == null || cells.Count == 0 || perCell <= 0) return;

            var origin = CellAnchoredPosition(fromCol, fromRow);
            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i].X, y = cells[i].Y;
                if (x < 0 || x >= _size || y < 0 || y >= _size) continue;

                var at = CellAnchoredPosition(x, y);
                var colour = BlockColour(x, y, placed);
                var away = at - origin;
                away = away.sqrMagnitude < 1f ? new Vector2(Random01() - 0.5f, 1f) : away.normalized;

                for (int k = 0; k < perCell && _shards.Count < MaxShards; k++)
                {
                    var shard = RentShard();
                    float speed = (380f + Random01() * 520f) * power;
                    var spread = new Vector2(Random01() - 0.5f, Random01() - 0.5f) * 0.9f;
                    shard.Velocity = (away + spread).normalized * speed + new Vector2(0f, 420f + Random01() * 380f) * power;
                    shard.Spin = (Random01() - 0.5f) * 900f;
                    shard.Age = -k * 0.02f;
                    shard.Life = 0.55f + Random01() * 0.3f;
                    shard.Size = _cellSize * (0.22f + Random01() * 0.16f);
                    shard.Color = Color.Lerp(colour, Color.white, 0.15f);

                    var rect = shard.Image.rectTransform;
                    rect.anchoredPosition = at + new Vector2(Random01() - 0.5f, Random01() - 0.5f) * _cellSize * 0.5f;
                    rect.sizeDelta = new Vector2(shard.Size, shard.Size);
                    rect.localEulerAngles = new Vector3(0f, 0f, Random01() * 90f);
                    rect.localScale = Vector3.one;
                    shard.Image.color = shard.Color.WithAlpha(0f);
                    shard.Image.gameObject.SetActive(true);
                    _shards.Add(shard);
                }
            }

            if (_shardRoutine == null && _shards.Count > 0) _shardRoutine = StartCoroutine(ShardRoutine());
        }

        IEnumerator ShardRoutine()
        {
            while (_shards.Count > 0)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

                for (int i = _shards.Count - 1; i >= 0; i--)
                {
                    var shard = _shards[i];
                    shard.Age += dt;
                    if (shard.Age < 0f) continue;

                    float k = shard.Age / shard.Life;
                    if (k >= 1f)
                    {
                        shard.Image.gameObject.SetActive(false);
                        _shards.RemoveAt(i);
                        _shardPool.Add(shard);
                        continue;
                    }

                    shard.Velocity += new Vector2(0f, ShardGravity * dt);
                    shard.Velocity *= 1f - 1.2f * dt;

                    var rect = shard.Image.rectTransform;
                    rect.anchoredPosition += shard.Velocity * dt;
                    rect.localEulerAngles += new Vector3(0f, 0f, shard.Spin * dt);
                    rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, k);
                    shard.Image.color = shard.Color.WithAlpha(k < 0.08f ? k / 0.08f : 1f - Ease.InQuad(Mathf.InverseLerp(0.45f, 1f, k)));
                }

                yield return null;
            }

            _shardRoutine = null;
        }

        /// <summary>Drops every piece of debris at once — a new run must not inherit the last one's.</summary>
        public void HideShards()
        {
            if (_shardRoutine != null)
            {
                StopCoroutine(_shardRoutine);
                _shardRoutine = null;
            }

            foreach (var shard in _shards)
            {
                shard.Image.gameObject.SetActive(false);
                _shardPool.Add(shard);
            }
            _shards.Clear();
        }

        Shard RentShard()
        {
            if (_shardPool.Count > 0)
            {
                var reused = _shardPool[_shardPool.Count - 1];
                _shardPool.RemoveAt(_shardPool.Count - 1);
                return reused;
            }

            var image = UiBuilder.Image(_shardRoot, "Shard", Art.Block, Color.white);
            image.type = Image.Type.Simple;
            image.gameObject.SetActive(false);
            return new Shard { Image = image };
        }

        /// <summary>
        /// The colour of the block a clear is taking from a cell. The view still shows the board as
        /// it was before the move, so the cells the piece itself just filled read as empty there —
        /// those take the piece's colour instead of the empty cell's.
        /// </summary>
        Color BlockColour(int x, int y, Color placed)
        {
            var cell = _cells[x, y];
            return cell.sprite == Art.Cell ? placed : cell.color;
        }

        /// <summary>A cheap xorshift so debris costs no allocation and never touches UnityEngine.Random's state.</summary>
        float Random01()
        {
            _shardSeed ^= _shardSeed << 13;
            _shardSeed ^= _shardSeed >> 17;
            _shardSeed ^= _shardSeed << 5;
            return (_shardSeed & 0xFFFFFF) / (float)0x1000000;
        }

        // ------------------------------------------------------------------ impact

        /// <summary>
        /// A short, decaying shake of the whole board — the weight of a big clear. Small on
        /// purpose: a few units, over in a quarter of a second, so it is felt rather than watched.
        /// </summary>
        public void Shake(float amplitude, float duration = 0.26f)
        {
            if (_shake != null) StopCoroutine(_shake);
            else _shakeHome = _rect.anchoredPosition;
            _shake = StartCoroutine(ShakeRoutine(amplitude, duration));
        }

        IEnumerator ShakeRoutine(float amplitude, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float fade = 1f - t / duration;
                fade *= fade;
                float a = amplitude * fade;
                _rect.anchoredPosition = _shakeHome + new Vector2((Random01() * 2f - 1f) * a, (Random01() * 2f - 1f) * a);
                yield return null;
            }

            _rect.anchoredPosition = _shakeHome;
            _shake = null;
        }

        /// <summary>
        /// A soft ring of light spreading from a cell — the landmark of a long streak, or the
        /// middle of the board when it is wiped clean.
        /// </summary>
        public void PlayWave(Vector2 at, Color color, float reach)
        {
            StartCoroutine(WaveRoutine(at, color, reach));
        }

        IEnumerator WaveRoutine(Vector2 at, Color color, float reach)
        {
            var rect = _wave.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(_cellSize * 2f, _cellSize * 2f);
            _wave.gameObject.SetActive(true);
            _wave.transform.SetAsLastSibling();

            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float s = Mathf.Lerp(0.5f, reach, Ease.OutCubic(k));
                rect.localScale = new Vector3(s, s, 1f);
                _wave.color = color.WithAlpha(Mathf.Lerp(0.6f, 0f, Ease.InQuad(k)));
                yield return null;
            }

            _wave.gameObject.SetActive(false);
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

        /// <summary>
        /// Pops a short flash on every cell a clear wiped. Backing light for
        /// <see cref="PlayClearOut"/>, which now carries the moment — at the old 0.8 the two
        /// flashes summed into a white patch where the line used to be.
        /// </summary>
        public void PlayClearBurst(IReadOnlyList<CellOffset> cells, Color tint)
        {
            for (int i = 0; i < cells.Count; i++)
                Burst(cells[i].X, cells[i].Y, tint, 0.5f, 1.6f, i * 0.011f);
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

        // What the rim settles back to after a pulse: the plain hairline, or the run's light.
        Color _rimRest = Design.Hairline;

        /// <summary>
        /// The rim's resting colour for the rest of the run: the stage's light, or gold once the
        /// record has fallen. Null is the plain hairline. Steady — it is a state, not an event.
        /// </summary>
        public void SetRimTint(Color? tint, float alpha = 0.4f)
        {
            _rimRest = tint == null ? Design.Hairline : tint.Value.WithAlpha(alpha);
            if (_pulse == null && _edge != null) _edge.color = _rimRest;
        }

        /// <summary>
        /// A new stage of light arriving: the board is washed from the bottom row to the top in
        /// the new colour, then the rim takes it on. The room's light changes around it at the
        /// same time (Backdrop.SetLight).
        /// </summary>
        public void PlayStageWash(Color color)
        {
            for (int row = _size - 1; row >= 0; row--)
                Sweep(row, horizontal: true, color, 0.5f, (_size - 1 - row) * 0.045f);
            PulseEdge(color);
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
                _edge.color = Color.Lerp(_rimRest, hot, t / up);
                yield return null;
            }

            const float down = 0.6f;
            for (float t = 0f; t < down; t += Time.unscaledDeltaTime)
            {
                _edge.color = Color.Lerp(hot, _rimRest, Ease.OutCubic(t / down));
                yield return null;
            }

            _edge.color = _rimRest;
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
