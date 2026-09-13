using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The adventure map: levels as a grid of tiles, a page at a time. Completed levels show their
    /// stars, the next one is lit in the accent, and everything past it is locked. The mode is
    /// endless — pages go on for as long as the player keeps unlocking them.
    ///
    /// The number of rows follows the page: a tall phone shows six rows of big tiles instead of
    /// five rows with a quarter of the screen left empty under them.
    /// </summary>
    public sealed class LevelSelectScreen : AppScreen
    {
        const int Columns = 4;
        const int MinRows = 3;
        const int MaxRows = 6;
        const float TileGap = 24f;

        readonly List<UiButton> _tiles = new List<UiButton>();
        readonly List<Image[]> _tileStars = new List<Image[]>();
        readonly List<Image> _tileLocks = new List<Image>();

        TextMeshProUGUI _pageLabel;
        TextMeshProUGUI _starTotal;
        UiButton _prev;
        UiButton _next;
        int _page;
        int _perPage = Columns * 5;

        int PerPage => _perPage;

        protected override void Build()
        {
            float half = App.PageHeight * 0.5f;
            float headerCenter = -(Design.Space2 + Design.TouchTarget * 0.5f);

            var back = UiBuilder.Button(Root, "Back", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon,
                null, Design.Body, Icons.ChevronLeft);
            back.Rect.anchorMin = back.Rect.anchorMax = new Vector2(0f, 1f);
            back.Rect.pivot = new Vector2(0f, 0.5f);
            back.Rect.anchoredPosition = new Vector2(Design.Gutter, headerCenter);
            back.Clicked += () => { Audio.PlayClick(); App.ShowMenu(); };

            var heading = UiBuilder.Label(Root, "Heading", "MACERA", Design.Title, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            heading.rectTransform.sizeDelta = new Vector2(500f, 110f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, headerCenter);
            UiBuilder.TextShadow(heading, 0.4f, -0.3f, 0.45f);

            // Total stars, top right: what the themes are unlocked with.
            var chip = UiBuilder.Node(Root, "Stars");
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
            chip.pivot = new Vector2(1f, 0.5f);
            chip.sizeDelta = new Vector2(240f, 112f);
            chip.anchoredPosition = new Vector2(-Design.Gutter, headerCenter);
            UiBuilder.Panel(chip, "Fill", chip.sizeDelta, Design.SurfaceInset, 56f);
            var star = UiBuilder.Image(chip, "Icon", Icons.Star, Design.Gold);
            star.type = Image.Type.Simple;
            star.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            star.rectTransform.anchoredPosition = new Vector2(-56f, 0f);
            _starTotal = UiBuilder.Label(chip, "Total", "0", Design.Headline, Design.Gold, Design.FontDisplay,
                TextAlignmentOptions.Left);
            _starTotal.rectTransform.sizeDelta = new Vector2(130f, 90f);
            _starTotal.rectTransform.anchoredPosition = new Vector2(51f, 2f);

            // Footer: page arrows along the bottom edge, in thumb reach.
            float footerCenter = -half + Design.Space3 + Design.TouchTarget * 0.5f;
            float arrowX = Design.ContentWidth * 0.5f - Design.TouchTarget * 0.5f;

            _prev = UiBuilder.Button(Root, "Prev", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon, null, Design.Body, Icons.ChevronLeft);
            _prev.Rect.anchoredPosition = new Vector2(-arrowX, footerCenter);
            _prev.Clicked += () => { Audio.PlayClick(); _page--; Populate(); };

            _next = UiBuilder.Button(Root, "Next", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon, null, Design.Body, Icons.ChevronRight);
            _next.Rect.anchoredPosition = new Vector2(arrowX, footerCenter);
            _next.Clicked += () => { Audio.PlayClick(); _page++; Populate(); };

            _pageLabel = UiBuilder.Label(Root, "Page", "", Design.Body, Design.TextOnGround, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _pageLabel.rectTransform.sizeDelta = new Vector2(420f, 80f);
            _pageLabel.rectTransform.anchoredPosition = new Vector2(0f, footerCenter);

            // The grid fills the band between header and footer with as many rows as fit.
            float tile = (Design.ContentWidth - TileGap * (Columns - 1)) / Columns;
            float bandTop = half - Design.Space2 - Design.TouchTarget - Design.Space4;
            float bandBottom = -half + Design.Space3 + Design.TouchTarget + Design.Space4;
            int rows = Mathf.Clamp(Mathf.FloorToInt((bandTop - bandBottom + TileGap) / (tile + TileGap)), MinRows, MaxRows);
            _perPage = Columns * rows;

            float gridWidth = Columns * tile + (Columns - 1) * TileGap;
            float gridHeight = rows * tile + (rows - 1) * TileGap;
            var grid = UiBuilder.Node(Root, "Grid");
            grid.sizeDelta = new Vector2(gridWidth, gridHeight);
            grid.anchoredPosition = new Vector2(0f, (bandTop + bandBottom) * 0.5f);

            for (int i = 0; i < PerPage; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                int slot = i;

                var button = UiBuilder.Button(grid, $"Tile_{i}", new Vector2(tile, tile), UiButton.Style.Secondary,
                    "0", Design.Title);
                button.Rect.anchoredPosition = new Vector2(
                    -gridWidth * 0.5f + tile * 0.5f + column * (tile + TileGap),
                    gridHeight * 0.5f - tile * 0.5f - row * (tile + TileGap));
                button.Label.rectTransform.anchoredPosition = new Vector2(0f, 20f);
                button.Clicked += () => OnTile(slot);

                var stars = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    var icon = UiBuilder.Image(button.Content, "Star" + s, Icons.Star, Design.Gold);
                    icon.type = Image.Type.Simple;
                    icon.rectTransform.sizeDelta = new Vector2(46f, 46f);
                    icon.rectTransform.anchoredPosition = new Vector2((s - 1) * 54f, -tile * 0.29f);
                    stars[s] = icon;
                }

                var locked = UiBuilder.Image(button.Content, "Lock", Icons.Lock, Design.TextTertiary);
                locked.type = Image.Type.Simple;
                locked.rectTransform.sizeDelta = new Vector2(Design.IconMd, Design.IconMd);

                _tiles.Add(button);
                _tileStars.Add(stars);
                _tileLocks.Add(locked);
            }
        }

        int MaxPage => (Progress.UnlockedLevel - 1) / PerPage + 1;

        protected override void OnShow()
        {
            _page = (Progress.UnlockedLevel - 1) / PerPage;
            Populate();

            // The next level is generated in the background, so tapping it starts instantly.
            int next = Progress.UnlockedLevel;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => LevelGenerator.Generate(next, Design.PaletteSize));
        }

        void Populate()
        {
            _page = Mathf.Clamp(_page, 0, MaxPage);
            int unlocked = Progress.UnlockedLevel;

            for (int i = 0; i < PerPage; i++)
            {
                int level = _page * PerPage + i + 1;
                var tile = _tiles[i];
                bool open = level <= unlocked;
                int stars = Progress.StarsFor(level);

                tile.Label.text = open ? level.ToString() : "";
                tile.Label.color = level == unlocked ? Design.OnAccent : Design.TextPrimary;
                tile.SetHighlighted(level == unlocked, Design.AccentA);
                tile.Interactable = open;

                _tileLocks[i].gameObject.SetActive(!open);

                var row = _tileStars[i];
                for (int s = 0; s < row.Length; s++)
                {
                    row[s].gameObject.SetActive(open && level < unlocked);
                    row[s].color = s < stars ? Design.Gold : Color.white.WithAlpha(0.16f);
                }
            }

            int first = _page * PerPage + 1;
            _pageLabel.text = $"{first} – {first + PerPage - 1}";
            _prev.SetEnabled(_page > 0);
            _next.SetEnabled(_page < MaxPage);
            _starTotal.text = Progress.TotalStars.ToString();
        }

        void OnTile(int slot)
        {
            int level = _page * PerPage + slot + 1;
            if (level > Progress.UnlockedLevel) return;

            Audio.PlayClick();
            App.PlayLevel(level);
        }
    }

    /// <summary>Lifetime numbers. Read-only, one quiet table.</summary>
    public sealed class StatsScreen : AppScreen
    {
        public override bool IsModal => true;

        const float PreferredRow = 100f;

        readonly List<TextMeshProUGUI> _values = new List<TextMeshProUGUI>();

        static readonly string[] Labels =
        {
            "OYNANAN OYUN", "EN İYİ SKOR", "TOPLAM PUAN", "TEMİZLENEN SATIR", "YERLEŞTİRİLEN PARÇA",
            "EN UZUN COMBO", "TEK RENK SATIR", "TAHTAYI SIFIRLAMA", "SERBEST KRİSTAL", "KULLANILAN GÜÇ",
            "BÖLÜM YILDIZI", "EN UZUN GÜNLÜK SERİ"
        };

        protected override void Build()
        {
            float rowHeight = ModalCard.FitRows(App.PageHeight, Labels.Length, PreferredRow, Design.ButtonLg);
            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(rowHeight * Labels.Length, Design.ButtonLg));
            ModalCard.Build(Root, "İstatistikler", size, out var content);

            float inner = ModalCard.InnerWidth(size);
            float left = -inner * 0.5f;
            const float valueWidth = 300f;
            float labelWidth = inner - valueWidth - Design.Space3 * 2f;

            float top = ModalCard.ContentTop(size) - rowHeight * 0.5f;
            for (int i = 0; i < Labels.Length; i++)
            {
                float y = top - i * rowHeight;

                if (i % 2 == 0)
                {
                    var band = UiBuilder.Panel(content, "Band" + i, new Vector2(inner, rowHeight - 8f), Design.SurfaceInset, Design.RadiusSm);
                    band.rectTransform.anchoredPosition = new Vector2(0f, y);
                }

                var label = UiBuilder.Label(content, "Label" + i, Labels[i], Design.Caption, Design.TextSecondary,
                    Design.FontMedium, TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
                label.rectTransform.sizeDelta = new Vector2(labelWidth, rowHeight);
                label.rectTransform.anchoredPosition = new Vector2(left + Design.Space3 + labelWidth * 0.5f, y);

                var value = UiBuilder.Label(content, "Value" + i, "0", Design.Body, Design.TextPrimary,
                    Design.FontDisplay, TextAlignmentOptions.Right);
                value.rectTransform.sizeDelta = new Vector2(valueWidth, rowHeight);
                value.rectTransform.anchoredPosition = new Vector2(inner * 0.5f - Design.Space3 - valueWidth * 0.5f, y);
                _values.Add(value);
            }

            var close = UiBuilder.Button(content, "Close", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary,
                "KAPAT", Design.Headline);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };
            ModalCard.StackFromBottom(content, Design.Space3, close.Rect);
        }

        protected override void OnShow()
        {
            var numbers = new[]
            {
                Progress.Games.ToString(),
                HighScores.Best.ToString(),
                Progress.TotalScore.ToString(),
                Progress.TotalLines.ToString(),
                Progress.TotalPieces.ToString(),
                Progress.BestCombo > 1 ? $"×{ScoreRules.ComboMultiplier(Progress.BestCombo):0.#}" : "—",
                Progress.MonoLinesTotal.ToString(),
                Progress.PerfectClears.ToString(),
                Progress.GemsCollected.ToString(),
                Progress.PowersUsed.ToString(),
                $"{Progress.TotalStars} / {Progress.LevelsCompleted * 3}",
                Progress.DailyBestStreak.ToString()
            };

            for (int i = 0; i < _values.Count && i < numbers.Length; i++)
                _values[i].text = numbers[i];
        }
    }

    /// <summary>
    /// Themes. A live preview of the whole look sits on top — ground, glow, board, blocks, the
    /// main button — and the list of themes below it, each row showing its seven blocks.
    ///
    /// Picking an unlocked theme applies it at once: the interface is rebuilt around this card,
    /// so the page behind the scrim and the card itself change in the same tap. Picking a locked
    /// one previews it with the stars it still needs, so a theme can be wanted before it is earned.
    /// </summary>
    public sealed class ThemesScreen : AppScreen
    {
        public override bool IsModal => true;

        const float MaxRow = 176f;
        const float MinRow = 150f;
        const float SwatchSize = 52f;
        const float SwatchStep = 60f;

        readonly List<UiButton> _rows = new List<UiButton>();
        readonly List<Image> _checks = new List<Image>();
        readonly List<GameObject> _locks = new List<GameObject>();
        readonly List<CanvasGroup> _swatches = new List<CanvasGroup>();

        ThemePreview _preview;
        int _focus;

        protected override void Build()
        {
            int count = Themes.All.Length;
            float previewGap = Design.Space5;

            // Rows first get what they need, the preview takes the rest up to its full size; on a
            // short page both give a little rather than the card running off the screen.
            float room = App.PageHeight - Design.Space4 * 2f - ModalCard.HeightFor(0f, Design.ButtonLg) - previewGap;
            float rowHeight = Mathf.Clamp((room - ThemePreview.Height) / count, MinRow, MaxRow);
            float previewHeight = Mathf.Clamp(room - rowHeight * count + Design.Space3, 280f, ThemePreview.Height);

            float body = previewHeight + previewGap + rowHeight * count - Design.Space3;
            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(body, Design.ButtonLg));
            ModalCard.Build(Root, "Temalar", size, out var content);

            float inner = ModalCard.InnerWidth(size);
            float top = ModalCard.ContentTop(size);

            _preview = new ThemePreview();
            _preview.Build(content, inner, previewHeight / ThemePreview.Height);
            _preview.Holder.anchoredPosition = new Vector2(0f, top - previewHeight * 0.5f);

            float rowTall = rowHeight - Design.Space3;
            float left = -inner * 0.5f + Design.Space4;
            float right = inner * 0.5f - Design.Space4;
            float line = rowTall * 0.22f;

            float firstRow = top - previewHeight - previewGap - rowTall * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var theme = Themes.All[i];

                var row = UiBuilder.Button(content, "Theme" + i, new Vector2(inner, rowTall), UiButton.Style.Secondary,
                    null, Design.Body);
                row.Rect.anchoredPosition = new Vector2(0f, firstRow - i * rowHeight);
                row.Clicked += () => Select(index);

                const float nameWidth = 440f;
                var name = UiBuilder.Label(row.Content, "Name", theme.Name, Design.Body, Design.TextPrimary,
                    Design.FontDisplay, TextAlignmentOptions.Left);
                name.rectTransform.sizeDelta = new Vector2(nameWidth, 70f);
                name.rectTransform.anchoredPosition = new Vector2(left + nameWidth * 0.5f, line + 4f);

                var swatchRoot = UiBuilder.Node(row.Content, "Swatches");
                swatchRoot.anchoredPosition = new Vector2(left + SwatchSize * 0.5f, -line - 4f);
                var group = swatchRoot.gameObject.AddComponent<CanvasGroup>();
                for (int c = 0; c < theme.Blocks.Length; c++)
                {
                    var block = UiBuilder.Image(swatchRoot, "B" + c, Art.Block, theme.Blocks[c]);
                    block.rectTransform.sizeDelta = new Vector2(SwatchSize, SwatchSize);
                    block.rectTransform.anchoredPosition = new Vector2(c * SwatchStep, 0f);
                }

                var check = UiBuilder.Image(row.Content, "Check", Icons.Check, Design.Mint);
                check.type = Image.Type.Simple;
                check.rectTransform.sizeDelta = new Vector2(Design.IconMd, Design.IconMd);
                check.rectTransform.anchoredPosition = new Vector2(right - Design.IconMd * 0.5f, 0f);

                const float lockWidth = 190f;
                var lockRoot = UiBuilder.Node(row.Content, "Locked");
                lockRoot.anchoredPosition = new Vector2(right - lockWidth * 0.5f, 0f);
                var lockIcon = UiBuilder.Image(lockRoot, "Star", Icons.Star, Design.Gold);
                lockIcon.type = Image.Type.Simple;
                lockIcon.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
                lockIcon.rectTransform.anchoredPosition = new Vector2(-lockWidth * 0.5f + Design.IconSm * 0.5f, 0f);
                var lockLabel = UiBuilder.Label(lockRoot, "Need", theme.StarsToUnlock.ToString(), Design.Headline, Design.Gold,
                    Design.FontDisplay, TextAlignmentOptions.Right);
                lockLabel.rectTransform.sizeDelta = new Vector2(lockWidth - Design.IconSm - Design.Space2, 80f);
                lockLabel.rectTransform.anchoredPosition = new Vector2((Design.IconSm + Design.Space2) * 0.5f, 2f);

                _rows.Add(row);
                _checks.Add(check);
                _locks.Add(lockRoot.gameObject);
                _swatches.Add(group);
            }

            var close = UiBuilder.Button(content, "Close", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary,
                "KAPAT", Design.Headline);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };
            ModalCard.StackFromBottom(content, Design.Space3, close.Rect);
        }

        protected override void OnShow()
        {
            _focus = Progress.Theme;
            Populate();
        }

        void Populate()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool open = Themes.IsUnlocked(i);
                bool current = Progress.Theme == i;

                _checks[i].gameObject.SetActive(current);
                _locks[i].SetActive(!open);
                _swatches[i].alpha = open ? 1f : 0.35f;
                _rows[i].SetHighlighted(current, Design.SurfaceLeader);
            }

            _preview.Show(Themes.All[_focus], Themes.IsUnlocked(_focus));
        }

#if PRIZMA_AUTOTEST
        public void AutoFocus(int index)
        {
            _focus = index;
            Populate();
        }
#endif

        void Select(int index)
        {
            Audio.PlayClick();

            if (index != Progress.Theme && Themes.IsUnlocked(index))
            {
                // Rebuilds the interface, this card included; nothing on this instance is used after.
                App.SetTheme(index);
                return;
            }

            _focus = index;
            Populate();
            StartCoroutine(Tween.Punch(_preview.Root, 0.04f, 0.22f));
        }
    }

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
            var play = UiBuilder.Label(_primary.rectTransform, "Label", "OYNA", Design.Headline, Design.OnAccent, Design.FontDisplay);
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
