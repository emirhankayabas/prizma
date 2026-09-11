using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The adventure map: levels as a grid of tiles, twenty to a page. Completed levels show their
    /// stars, the next one is lit in the accent, and everything past it is locked. The mode is
    /// endless — pages go on for as long as the player keeps unlocking them.
    /// </summary>
    public sealed class LevelSelectScreen : AppScreen
    {
        const int Columns = 4;
        const int Rows = 5;
        const int PerPage = Columns * Rows;
        const float Tile = 196f;
        const float TileGap = 28f;

        readonly List<UiButton> _tiles = new List<UiButton>();
        readonly List<Image[]> _tileStars = new List<Image[]>();
        readonly List<Image> _tileLocks = new List<Image>();

        TextMeshProUGUI _pageLabel;
        TextMeshProUGUI _starTotal;
        UiButton _prev;
        UiButton _next;
        int _page;

        protected override void Build()
        {
            var back = UiBuilder.Button(Root, "Back", new Vector2(104f, 104f), UiButton.Style.Icon,
                null, Design.Body, Icons.ChevronLeft);
            back.Rect.anchorMin = back.Rect.anchorMax = new Vector2(0f, 1f);
            back.Rect.pivot = new Vector2(0f, 1f);
            back.Rect.anchoredPosition = new Vector2(Design.Gutter, -Design.Space5);
            back.Clicked += () => { Audio.PlayClick(); App.ShowMenu(); };

            var heading = UiBuilder.Label(Root, "Heading", "MACERA", Design.Title, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -Design.Space5 - 10f);
            UiBuilder.TextShadow(heading, 0.4f, -0.3f, 0.45f);

            // Total stars, top right: what the themes are unlocked with.
            var chip = UiBuilder.Node(Root, "Stars");
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
            chip.pivot = new Vector2(1f, 1f);
            chip.sizeDelta = new Vector2(200f, 88f);
            chip.anchoredPosition = new Vector2(-Design.Gutter, -Design.Space5 - 8f);
            UiBuilder.Panel(chip, "Fill", chip.sizeDelta, Design.SurfaceInset, 44f);
            var star = UiBuilder.Image(chip, "Icon", Icons.Star, Design.Gold);
            star.type = Image.Type.Simple;
            star.rectTransform.sizeDelta = new Vector2(46f, 46f);
            star.rectTransform.anchoredPosition = new Vector2(-42f, 0f);
            _starTotal = UiBuilder.Label(chip, "Total", "0", Design.Headline, Design.Gold, Design.FontDisplay,
                TextAlignmentOptions.Left);
            _starTotal.rectTransform.sizeDelta = new Vector2(110f, 70f);
            _starTotal.rectTransform.anchoredPosition = new Vector2(43f, 0f);

            float gridWidth = Columns * Tile + (Columns - 1) * TileGap;
            float gridHeight = Rows * Tile + (Rows - 1) * TileGap;
            var grid = UiBuilder.Node(Root, "Grid");
            grid.sizeDelta = new Vector2(gridWidth, gridHeight);
            grid.anchoredPosition = new Vector2(0f, 10f);

            for (int i = 0; i < PerPage; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                int slot = i;

                var tile = UiBuilder.Button(grid, $"Tile_{i}", new Vector2(Tile, Tile), UiButton.Style.Secondary,
                    "0", Design.Title);
                tile.Rect.anchoredPosition = new Vector2(
                    -gridWidth * 0.5f + Tile * 0.5f + column * (Tile + TileGap),
                    gridHeight * 0.5f - Tile * 0.5f - row * (Tile + TileGap));
                tile.Label.rectTransform.anchoredPosition = new Vector2(0f, 16f);
                tile.Clicked += () => OnTile(slot);

                var stars = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    var icon = UiBuilder.Image(tile.Content, "Star" + s, Icons.Star, Design.Gold);
                    icon.type = Image.Type.Simple;
                    icon.rectTransform.sizeDelta = new Vector2(38f, 38f);
                    icon.rectTransform.anchoredPosition = new Vector2((s - 1) * 44f, -54f);
                    stars[s] = icon;
                }

                var locked = UiBuilder.Image(tile.Content, "Lock", Icons.Lock, Design.TextTertiary);
                locked.type = Image.Type.Simple;
                locked.rectTransform.sizeDelta = new Vector2(64f, 64f);

                _tiles.Add(tile);
                _tileStars.Add(stars);
                _tileLocks.Add(locked);
            }

            _prev = UiBuilder.Button(Root, "Prev", new Vector2(120f, 120f), UiButton.Style.Icon, null, Design.Body, Icons.ChevronLeft);
            _prev.Rect.anchoredPosition = new Vector2(-300f, -gridHeight * 0.5f - 90f);
            _prev.Clicked += () => { Audio.PlayClick(); _page--; Populate(); };

            _next = UiBuilder.Button(Root, "Next", new Vector2(120f, 120f), UiButton.Style.Icon, null, Design.Body, Icons.ChevronRight);
            _next.Rect.anchoredPosition = new Vector2(300f, -gridHeight * 0.5f - 90f);
            _next.Clicked += () => { Audio.PlayClick(); _page++; Populate(); };

            _pageLabel = UiBuilder.Label(Root, "Page", "", Design.Body, Design.TextOnGround, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _pageLabel.rectTransform.sizeDelta = new Vector2(360f, 70f);
            _pageLabel.rectTransform.anchoredPosition = new Vector2(0f, -gridHeight * 0.5f - 90f);
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

        const float RowHeight = 78f;

        readonly List<TextMeshProUGUI> _values = new List<TextMeshProUGUI>();

        static readonly string[] Labels =
        {
            "OYNANAN OYUN", "EN İYİ SKOR", "TOPLAM PUAN", "TEMİZLENEN SATIR", "YERLEŞTİRİLEN PARÇA",
            "EN UZUN COMBO", "TEK RENK SATIR", "TAHTAYI SIFIRLAMA", "SERBEST KRİSTAL", "KULLANILAN GÜÇ",
            "BÖLÜM YILDIZI", "EN UZUN GÜNLÜK SERİ"
        };

        protected override void Build()
        {
            var size = new Vector2(900f, 1400f);
            ModalCard.Build(Root, "İstatistikler", size, out var content);

            float top = size.y * 0.5f - 190f;
            for (int i = 0; i < Labels.Length; i++)
            {
                float y = top - i * RowHeight;

                if (i % 2 == 0)
                {
                    var band = UiBuilder.Panel(content, "Band" + i, new Vector2(790f, RowHeight - 8f), Design.SurfaceInset, Design.RadiusSm);
                    band.rectTransform.anchoredPosition = new Vector2(0f, y);
                }

                var label = UiBuilder.Label(content, "Label" + i, Labels[i], Design.Caption, Design.TextSecondary,
                    Design.FontMedium, TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
                label.rectTransform.sizeDelta = new Vector2(520f, RowHeight);
                label.rectTransform.anchoredPosition = new Vector2(-110f, y);

                var value = UiBuilder.Label(content, "Value" + i, "0", Design.Body, Design.TextPrimary,
                    Design.FontDisplay, TextAlignmentOptions.Right);
                value.rectTransform.sizeDelta = new Vector2(260f, RowHeight);
                value.rectTransform.anchoredPosition = new Vector2(240f, y);
                _values.Add(value);
            }

            var close = UiBuilder.Button(content, "Close", new Vector2(420f, 140f), UiButton.Style.Primary,
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
    /// Block palettes. Each row shows the palette itself — seven blocks — so the choice is made by
    /// looking, not by reading names. Locked rows show the stars still needed.
    /// </summary>
    public sealed class ThemesScreen : AppScreen
    {
        public override bool IsModal => true;

        const float RowHeight = 170f;

        readonly List<UiButton> _rows = new List<UiButton>();
        readonly List<Image> _checks = new List<Image>();
        readonly List<GameObject> _locks = new List<GameObject>();
        readonly List<TextMeshProUGUI> _lockLabels = new List<TextMeshProUGUI>();
        readonly List<CanvasGroup> _swatches = new List<CanvasGroup>();

        protected override void Build()
        {
            var size = new Vector2(900f, 1340f);
            ModalCard.Build(Root, "Temalar", size, out var content);

            float top = size.y * 0.5f - 230f;
            for (int i = 0; i < Themes.All.Length; i++)
            {
                int index = i;
                var theme = Themes.All[i];

                var row = UiBuilder.Button(content, "Theme" + i, new Vector2(790f, RowHeight - 20f), UiButton.Style.Secondary,
                    null, Design.Body);
                row.Rect.anchoredPosition = new Vector2(0f, top - i * RowHeight);
                row.Clicked += () => Select(index);

                var name = UiBuilder.Label(row.Content, "Name", theme.Name, Design.Body, Design.TextPrimary,
                    Design.FontDisplay, TextAlignmentOptions.Left);
                name.rectTransform.sizeDelta = new Vector2(300f, 60f);
                name.rectTransform.anchoredPosition = new Vector2(-220f, 28f);

                var swatchRoot = UiBuilder.Node(row.Content, "Swatches");
                swatchRoot.anchoredPosition = new Vector2(-120f, -28f);
                var group = swatchRoot.gameObject.AddComponent<CanvasGroup>();
                for (int c = 0; c < theme.Blocks.Length; c++)
                {
                    var block = UiBuilder.Image(swatchRoot, "B" + c, Art.Block, theme.Blocks[c]);
                    block.rectTransform.sizeDelta = new Vector2(44f, 44f);
                    block.rectTransform.anchoredPosition = new Vector2(-150f + c * 50f, 0f);
                }

                var check = UiBuilder.Image(row.Content, "Check", Icons.Check, Design.Mint);
                check.type = Image.Type.Simple;
                check.rectTransform.sizeDelta = new Vector2(64f, 64f);
                check.rectTransform.anchoredPosition = new Vector2(320f, 0f);

                var lockRoot = UiBuilder.Node(row.Content, "Locked");
                lockRoot.anchoredPosition = new Vector2(290f, 0f);
                var lockIcon = UiBuilder.Image(lockRoot, "Star", Icons.Star, Design.Gold);
                lockIcon.type = Image.Type.Simple;
                lockIcon.rectTransform.sizeDelta = new Vector2(40f, 40f);
                lockIcon.rectTransform.anchoredPosition = new Vector2(-40f, 0f);
                var lockLabel = UiBuilder.Label(lockRoot, "Need", theme.StarsToUnlock.ToString(), Design.Body, Design.Gold,
                    Design.FontDisplay, TextAlignmentOptions.Left);
                lockLabel.rectTransform.sizeDelta = new Vector2(110f, 60f);
                lockLabel.rectTransform.anchoredPosition = new Vector2(38f, 0f);

                _rows.Add(row);
                _checks.Add(check);
                _locks.Add(lockRoot.gameObject);
                _lockLabels.Add(lockLabel);
                _swatches.Add(group);
            }

            var close = UiBuilder.Button(content, "Close", new Vector2(420f, 140f), UiButton.Style.Primary,
                "KAPAT", Design.Headline);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };
            ModalCard.StackFromBottom(content, Design.Space3, close.Rect);
        }

        protected override void OnShow() => Populate();

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
        }

        void Select(int index)
        {
            if (!Themes.IsUnlocked(index))
            {
                Audio.PlayInvalid();
                return;
            }

            Audio.PlayClick();
            Progress.Theme = index;
            Populate();
        }
    }
}
