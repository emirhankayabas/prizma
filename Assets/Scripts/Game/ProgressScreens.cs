using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// A small picture of the game in a given theme: the ground with its glow, a board with a few
    /// blocks in all seven colours, the best-score chip and the main buttons. Built once and
    /// recoloured, so previewing is free.
    /// </summary>
    sealed class ThemePreview
    {
        public const float Height = 400f;

        const int Cells = 6;
        const float Cell = 44f;
        const float CellGap = 6f;
        const float BoardPad = 14f;

        // Which colour sits in each cell; -1 is empty. Every colour appears at least twice.
        static readonly int[] Pattern =
        {
            -1, -1,  4,  4, -1, -1,
             0, -1,  4, -1, -1,  3,
             0,  0, -1, -1,  3,  3,
             1,  2,  2,  6,  6,  5,
             1, -1,  2, -1,  6,  5,
             1, -1, -1, -1, -1,  5,
        };

        /// <summary>Positioned and scaled by the card.</summary>
        public RectTransform Holder { get; private set; }

        /// <summary>The picture itself, at scale 1 inside the holder — what a punch animates.</summary>
        public RectTransform Root { get; private set; }

        Image _ground;
        Image _glow;
        Image _board;
        readonly Image[] _cells = new Image[Cells * Cells];
        Image _chip;
        Image _primary;
        Image _secondaryA;
        Image _secondaryB;
        GameObject _lock;
        Image _lockBadge;
        TextMeshProUGUI _lockLabel;
        readonly List<GameObject> _unlockedOnly = new List<GameObject>();

        public void Build(RectTransform parent, float width, float scale)
        {
            Holder = UiBuilder.Node(parent, "Preview");
            Holder.sizeDelta = new Vector2(width, Height);
            Holder.localScale = Vector3.one * scale;

            Root = UiBuilder.Node(Holder, "Picture");
            Root.sizeDelta = Holder.sizeDelta;

            var size = Root.sizeDelta;
            float radius = Design.RadiusLg;

            UiBuilder.Shadow(Root, "Shadow", size, radius, Design.E1);
            _ground = UiBuilder.Image(Root, "Ground", Art.PanelGradient(radius), Color.white);
            _ground.rectTransform.sizeDelta = size;

            float boardSize = Cells * Cell + (Cells - 1) * CellGap + BoardPad * 2f;
            float boardX = -width * 0.5f + Design.Space5 + boardSize * 0.5f;

            _glow = UiBuilder.Image(Root, "Glow", Art.Pool, Color.white);
            _glow.type = Image.Type.Simple;
            _glow.rectTransform.sizeDelta = new Vector2(Height - 20f, Height - 20f);
            _glow.rectTransform.anchoredPosition = new Vector2(boardX, 0f);

            _board = UiBuilder.Panel(Root, "Board", new Vector2(boardSize, boardSize), Color.white, Design.RadiusMd);
            _board.rectTransform.anchoredPosition = new Vector2(boardX, 0f);

            float origin = -boardSize * 0.5f + BoardPad + Cell * 0.5f;
            for (int y = 0; y < Cells; y++)
            for (int x = 0; x < Cells; x++)
            {
                var cell = UiBuilder.Image(_board.rectTransform, $"Cell_{x}_{y}", Art.Cell, Color.white);
                cell.rectTransform.sizeDelta = new Vector2(Cell, Cell);
                cell.rectTransform.anchoredPosition = new Vector2(origin + x * (Cell + CellGap), -origin - y * (Cell + CellGap));
                _cells[y * Cells + x] = cell;
            }

            // The page's own pieces, in miniature, down the right-hand side.
            float columnWidth = width * 0.5f - Design.Space4;
            float columnX = width * 0.5f - Design.Space5 - columnWidth * 0.5f;

            _chip = UiBuilder.Panel(Root, "Chip", new Vector2(columnWidth, 84f), Color.white, 42f);
            _chip.rectTransform.anchoredPosition = new Vector2(columnX, 118f);
            var gem = UiBuilder.Image(_chip.rectTransform, "Gem", Icons.Gem, Design.Gold);
            gem.type = Image.Type.Simple;
            gem.rectTransform.sizeDelta = new Vector2(48f, 48f);
            gem.rectTransform.anchoredPosition = new Vector2(-70f, 0f);
            var best = UiBuilder.Label(_chip.rectTransform, "Best", "1250", Design.Label, Design.Gold, Design.FontDisplay,
                TextAlignmentOptions.Left);
            best.rectTransform.sizeDelta = new Vector2(160f, 70f);
            best.rectTransform.anchoredPosition = new Vector2(46f, 2f);

            _primary = UiBuilder.Image(Root, "Primary", Art.PanelGradient(Design.RadiusMd), Color.white);
            _primary.rectTransform.sizeDelta = new Vector2(columnWidth, 104f);
            _primary.rectTransform.anchoredPosition = new Vector2(columnX, 6f);
            var play = UiBuilder.Label(_primary.rectTransform, "Label", Str.Play, Design.Headline, Design.OnAccent, Design.FontDisplay);
            play.rectTransform.sizeDelta = _primary.rectTransform.sizeDelta;

            float half = (columnWidth - Design.Space2) * 0.5f;
            _secondaryA = SmallButton("Adventure", Icons.Flag, Design.TextPrimary, columnX - (half + Design.Space2) * 0.5f, half);
            _secondaryB = SmallButton("Daily", Icons.Calendar, Design.Mint, columnX + (half + Design.Space2) * 0.5f, half);

            UiBuilder.Hairline(Root, "Hairline", size, radius);

            _unlockedOnly.Add(_chip.gameObject);
            _unlockedOnly.Add(_primary.gameObject);
            _unlockedOnly.Add(_secondaryA.gameObject);
            _unlockedOnly.Add(_secondaryB.gameObject);

            // A theme not earned yet keeps its ground and board in full colour — that is what makes
            // it worth earning — and the buttons' column gives way to what it costs.
            var lockRoot = UiBuilder.Node(Root, "Locked");
            lockRoot.anchoredPosition = new Vector2(columnX, 0f);
            var badge = UiBuilder.Panel(lockRoot, "Badge", new Vector2(300f, 120f), Design.SurfaceHigh, 60f);
            _lockBadge = badge;
            UiBuilder.Hairline(badge.rectTransform, "Edge", new Vector2(300f, 120f), 60f);
            var padlock = UiBuilder.Image(badge.rectTransform, "Lock", Icons.Lock, Design.TextPrimary);
            padlock.type = Image.Type.Simple;
            padlock.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            padlock.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
            var star = UiBuilder.Image(badge.rectTransform, "Star", Icons.Star, Design.Gold);
            star.type = Image.Type.Simple;
            star.rectTransform.sizeDelta = new Vector2(52f, 52f);
            star.rectTransform.anchoredPosition = new Vector2(-24f, 0f);
            _lockLabel = UiBuilder.Label(badge.rectTransform, "Need", "0", Design.Headline, Design.Gold, Design.FontDisplay,
                TextAlignmentOptions.Left);
            _lockLabel.rectTransform.sizeDelta = new Vector2(120f, 90f);
            _lockLabel.rectTransform.anchoredPosition = new Vector2(66f, 2f);
            _lock = lockRoot.gameObject;
        }

        Image SmallButton(string name, Sprite icon, Color iconColor, float x, float width)
        {
            var fill = UiBuilder.Panel(Root, name, new Vector2(width, 96f), Color.white, Design.RadiusMd);
            fill.rectTransform.anchoredPosition = new Vector2(x, -112f);
            UiBuilder.Hairline(fill.rectTransform, "Hairline", new Vector2(width, 96f), Design.RadiusMd);

            var glyph = UiBuilder.Image(fill.rectTransform, "Icon", icon, iconColor);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(52f, 52f);
            return fill;
        }

        public void Show(Themes.Theme theme, bool unlocked)
        {
            _ground.color = Color.Lerp(theme.BgTop, theme.BgBottom, 0.3f);
            _glow.color = theme.BgGlow.WithAlpha(theme.GlowAlpha);
            _board.color = theme.BoardSurface;

            for (int i = 0; i < _cells.Length; i++)
            {
                int colour = Pattern[i];
                var cell = _cells[i];
                cell.sprite = colour < 0 ? Art.Cell : Art.Block;
                cell.color = colour < 0 ? theme.SurfaceInset : theme.Blocks[colour % theme.Blocks.Length];
            }

            _chip.color = theme.SurfaceInset;
            _primary.color = theme.AccentA;
            _secondaryA.color = theme.SurfaceButton;
            _secondaryB.color = theme.SurfaceButton;

            _lockBadge.color = theme.SurfaceHigh;
            _lock.SetActive(!unlocked);
            foreach (var go in _unlockedOnly) go.SetActive(unlocked);
            _lockLabel.text = theme.StarsToUnlock.ToString();
        }
    }
}
