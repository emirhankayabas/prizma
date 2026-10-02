using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Settings, as a game shows them: the four switches as big tiles two by two — lit in their own
    /// colour when on, quiet when off — then the two volumes on thick sliders, then the language.
    /// The first version was a phone's settings list (icon, name, switch, hairline), and that is
    /// exactly what it looked like.
    /// </summary>
    public sealed class SettingsScreen : SheetScreen
    {
        const float TileHeight = 196f;
        const float LabelWidth = 250f;
        const float SliderTrack = 40f;

        UiSlider _music;
        UiSlider _sfx;
        UiTile _mute;
        UiTile _haptics;
        UiTile _notifications;
        UiTile _colorBlind;
        readonly List<CanvasGroup> _volumeRows = new List<CanvasGroup>();

        protected override void Build()
        {
            BuildSheet(Str.Settings, Icons.Gear, Design.TintBlue);
            float top = 0f;
            float tileWidth = (Design.ContentWidth - Design.Space3) * 0.5f;
            float leftX = -Design.ContentWidth * 0.5f + tileWidth * 0.5f;
            float rightX = -leftX;

            // Mute first: it is the control someone reaches for in a hurry.
            _mute = UiTile.Create(Body, "Mute", new Vector2(tileWidth, TileHeight), Icons.SpeakerMuted, Design.TintRose, Str.Mute);
            SheetKit.PinTop(_mute.Rect, top, leftX);
            _mute.Changed += v =>
            {
                GameSettings.Muted = v;
                ApplyMuteState();
                Audio.PlayClick(); // silent when muting, audible when unmuting
            };

            _haptics = UiTile.Create(Body, "Haptic", new Vector2(tileWidth, TileHeight), Icons.Vibrate, Design.TintOrange, Str.Vibration);
            SheetKit.PinTop(_haptics.Rect, top, rightX);
            _haptics.Changed += v => { GameSettings.Haptics = v; Audio.PlayClick(); if (v) App.Vibrate(); };
            top += TileHeight + Design.Space3;

            // The daily reminder. Asked for once at first launch with the system's own dialog; here
            // it can be changed. Turning it on asks the system again where it still may.
            _notifications = UiTile.Create(Body, "Notify", new Vector2(tileWidth, TileHeight), Icons.Bell, Design.Gold, Str.Notifications);
            SheetKit.PinTop(_notifications.Rect, top, leftX);
            _notifications.Changed += OnNotificationsToggled;

            // Colour-blind mode marks every block colour with its own small shape.
            _colorBlind = UiTile.Create(Body, "ColorBlind", new Vector2(tileWidth, TileHeight), Icons.Palette, Design.TintTeal, Str.ColorBlind);
            SheetKit.PinTop(_colorBlind.Rect, top, rightX);
            _colorBlind.Changed += v => { Progress.ColorBlind = v; Audio.PlayClick(); };
            top += TileHeight + Design.GroupGap;

            // The two volumes.
            float sound = Design.RowHeight * 2f;
            var soundGroup = SheetKit.Group(Body, "Sound", top, sound);
            _music = SliderRow(soundGroup, sound, 0, "Music", Icons.Note, Design.TintViolet, Str.Music, GameSettings.MusicVolume);
            _music.ValueChanged += v => GameSettings.MusicVolume = v;

            _sfx = SliderRow(soundGroup, sound, 1, "Sfx", Icons.Speaker, Design.TintBlue, Str.Effects, GameSettings.SfxVolume);
            _sfx.ValueChanged += v =>
            {
                GameSettings.SfxVolume = v;
                Audio.PlayClick(); // immediate feedback at the new level
            };
            top += sound + Design.GroupGap;

            // Language: named in both, and the choice spelled in each language's own words, so a
            // player who cannot read the current one still finds theirs.
            float language = Design.RowHeight + Design.SegmentHeight + Design.GroupPadding;
            var languageGroup = SheetKit.Group(Body, "Language", top, language);
            float rowY = SheetKit.RowY(language, 0);
            SheetKit.RowIcon(languageGroup, "LanguageIcon", Icons.Globe, Design.Mint, SheetKit.RowLeft + Design.RowIcon * 0.5f, rowY);
            SheetKit.RowLabel(languageGroup, "LanguageLabel", Str.LanguageLabel, SheetKit.RowText, SheetKit.RowRight - SheetKit.RowText, rowY);

            var choice = UiSegmented.Create(languageGroup, "LanguageChoice", Design.ContentWidth - Design.GroupPadding * 2f,
                Str.LanguageName(Language.Turkish), Str.LanguageName(Language.English));
            choice.Rect.anchoredPosition = new Vector2(0f, -language * 0.5f + Design.GroupPadding + Design.SegmentHeight * 0.5f);
            choice.Select(GameSettings.Language == Language.English ? 1 : 0, false);
            choice.Changed += i =>
            {
                Audio.PlayClick();
                App.SetLanguage(i == 1 ? Language.English : Language.Turkish);
            };
            top += language;

            Sheet.SetBodyHeight(top);
        }

        UiSlider SliderRow(RectTransform group, float height, int index, string name, Sprite icon, Color tint, string label, float value)
        {
            float y = SheetKit.RowY(height, index);
            if (index > 0) SheetKit.Separator(group, y + Design.RowHeight * 0.5f, SheetKit.RowText);

            // One node per row, so muting can dim the whole row as one.
            var row = UiBuilder.Node(group, name + "Row");
            row.sizeDelta = new Vector2(Design.ContentWidth, Design.RowHeight);
            row.anchoredPosition = new Vector2(0f, y);
            _volumeRows.Add(row.gameObject.AddComponent<CanvasGroup>());

            SheetKit.RowIcon(row, name + "Icon", icon, tint, SheetKit.RowLeft + Design.RowIcon * 0.5f, 0f);
            SheetKit.RowLabel(row, name + "Label", label, SheetKit.RowText, LabelWidth, 0f);

            // The knob overhangs the track by about its radius at either end; inset so it stays inside the group.
            float knob = SliderTrack * 2.3f;
            float left = SheetKit.RowText + LabelWidth + Design.Space3 + knob * 0.5f;
            float right = SheetKit.RowRight - knob * 0.5f;
            var slider = UiSlider.Create(row, name + "Slider", right - left, SliderTrack, value);
            slider.Rect.anchoredPosition = new Vector2((left + right) * 0.5f, 0f);
            return slider;
        }

        void OnNotificationsToggled(bool on)
        {
            Audio.PlayClick();
            if (!on)
            {
                Reminder.Disable();
                return;
            }

            Reminder.Enable(allowed =>
            {
                if (this == null) return;
                _notifications.SetOn(allowed, animate: true);
            });
        }

        void ApplyMuteState()
        {
            bool muted = GameSettings.Muted;
            _mute.SetIcon(muted ? Icons.SpeakerMuted : Icons.Speaker);

            // Volume rows dim while muted, so the sliders read as parked rather than broken.
            foreach (var row in _volumeRows) row.alpha = muted ? 0.4f : 1f;
        }

        protected override void OnShow()
        {
            _music.Value = GameSettings.MusicVolume;
            _sfx.Value = GameSettings.SfxVolume;
            _haptics.SetOn(GameSettings.Haptics, false);
            _mute.SetOn(GameSettings.Muted, false);
            _notifications.SetOn(GameSettings.Reminder, false);
            _colorBlind.SetOn(Progress.ColorBlind, false);
            ApplyMuteState();
        }
    }

    /// <summary>
    /// Mid-run menu. Opening it is the only way to reach settings, restart or leave a game, so a
    /// player never has to guess whether tapping something will cost them their run. Leaving
    /// keeps the run: it is saved and waiting on the title page.
    ///
    /// The three ways out are three big tiles side by side; carrying on is the one big button, at
    /// the bottom, under the thumb. They used to be a list with chevrons, like an app's menu.
    /// </summary>
    public sealed class PauseScreen : SheetScreen
    {
        const float TileHeight = 232f;

        protected override void Build()
        {
            BuildSheet(Str.Paused, Icons.Pause, Design.TintViolet);

            float width = (Design.ContentWidth - Design.Space3 * 2f) / 3f;
            ActionTile(0, width, "Restart", Icons.Replay, Design.TintOrange, Str.Restart, () => App.RestartRun());
            ActionTile(1, width, "Settings", Icons.Gear, Design.TintBlue, Str.SettingsButton, () => App.OpenSettings());
            ActionTile(2, width, "Quit", Icons.Home, Design.TintViolet, Str.Home, () => App.LeaveRun());

            var resume = UiBuilder.Button(Body, "Resume", new Vector2(Design.ContentWidth, Design.ButtonLg), UiButton.Style.Primary,
                Str.Continue, Design.Headline, Icons.Play);
            SheetKit.PinTop(resume.Rect, TileHeight + Design.Space5);
            resume.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            Sheet.SetBodyHeight(TileHeight + Design.Space5 + Design.ButtonLg);
        }

        void ActionTile(int index, float width, string name, Sprite icon, Color tint, string label, System.Action action)
        {
            var tile = UiTile.CreateAction(Body, name, new Vector2(width, TileHeight), icon, tint, label);
            SheetKit.PinTop(tile.Rect, 0f, -Design.ContentWidth * 0.5f + width * 0.5f + index * (width + Design.Space3));
            tile.Clicked += () => { Audio.PlayClick(); action(); };
        }
    }

    /// <summary>The local top ten: a medal for the first three, the leader in gold.</summary>
    public sealed class ScoresScreen : SheetScreen
    {
        const float Row = 124f;
        const float Rank = 80f;
        const float EmptyHeight = 460f;

        RectTransform _group;
        readonly List<RectTransform> _rows = new List<RectTransform>();
        readonly List<Image> _rankDiscs = new List<Image>();
        readonly List<TextMeshProUGUI> _ranks = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> _scores = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> _dates = new List<TextMeshProUGUI>();
        RectTransform _empty;

        protected override void Build()
        {
            BuildSheet(Str.BestScores, Icons.Crown, Design.Gold);

            _group = SheetKit.Group(Body, "List", 0f, Row * HighScores.Capacity);
            float left = SheetKit.RowLeft;
            for (int i = 0; i < HighScores.Capacity; i++)
            {
                var row = UiBuilder.Node(_group, "Row" + i);
                row.sizeDelta = new Vector2(Design.ContentWidth, Row);
                SheetKit.PinTop(row, i * Row);
                _rows.Add(row);

                if (i > 0) SheetKit.Separator(row, Row * 0.5f, left + Rank + Design.Space3);

                var disc = UiBuilder.Image(row, "RankDisc", Art.Node, Design.SurfaceControl);
                disc.type = Image.Type.Simple;
                disc.rectTransform.sizeDelta = new Vector2(Rank, Rank);
                disc.rectTransform.anchoredPosition = new Vector2(left + Rank * 0.5f, 0f);
                _rankDiscs.Add(disc);

                var rank = UiBuilder.Label(disc.rectTransform, "Rank", (i + 1).ToString(), Design.Label, Design.TextSecondary, Design.FontDisplay);
                rank.rectTransform.sizeDelta = new Vector2(Rank, Rank);
                rank.rectTransform.anchoredPosition = new Vector2(0f, 2f);
                _ranks.Add(rank);

                const float scoreWidth = 420f;
                var score = UiBuilder.Label(row, "Score", "", Design.Headline, Design.TextPrimary, Design.FontDisplay, TextAlignmentOptions.Left);
                score.rectTransform.sizeDelta = new Vector2(scoreWidth, Row);
                score.rectTransform.anchoredPosition = new Vector2(left + Rank + Design.Space3 + scoreWidth * 0.5f, 2f);
                _scores.Add(score);

                const float dateWidth = 300f;
                var date = UiBuilder.Label(row, "Date", "", Design.Caption, Design.TextTertiary, Design.FontDisplay, TextAlignmentOptions.Right);
                date.rectTransform.sizeDelta = new Vector2(dateWidth, Row);
                date.rectTransform.anchoredPosition = new Vector2(SheetKit.RowRight - dateWidth * 0.5f, 0f);
                _dates.Add(date);
            }

            // No runs yet: a picture of an empty list and one line under it.
            _empty = UiBuilder.Node(Body, "Empty");
            _empty.sizeDelta = new Vector2(Design.ContentWidth, EmptyHeight);
            SheetKit.PinTop(_empty, 0f);
            var disc2 = UiBuilder.Image(_empty, "Disc", Art.Disc, Design.SurfaceGroup);
            disc2.type = Image.Type.Simple;
            disc2.rectTransform.sizeDelta = new Vector2(220f, 220f);
            disc2.rectTransform.anchoredPosition = new Vector2(0f, 90f);
            var glyph = UiBuilder.Image(disc2.rectTransform, "Glyph", Icons.List, Design.TextTertiary);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(Design.IconMd, Design.IconMd);
            var text = UiBuilder.Label(_empty, "Text", Str.NoScores, Design.Body, Design.TextSecondary, Design.FontDisplay);
            text.rectTransform.sizeDelta = new Vector2(Design.ContentWidth, 160f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -140f);
        }

        protected override void OnShow()
        {
            var entries = HighScores.All;
            int count = Mathf.Min(entries.Count, HighScores.Capacity);

            _empty.gameObject.SetActive(count == 0);
            _group.gameObject.SetActive(count > 0);
            _group.sizeDelta = new Vector2(Design.ContentWidth, Row * Mathf.Max(1, count));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool shown = i < count;
                _rows[i].gameObject.SetActive(shown);
                if (!shown) continue;

                var entry = entries[i];
                Color medal = i == 0 ? Design.Gold : i == 1 ? Design.Silver : i == 2 ? Design.Bronze : Color.clear;
                bool podium = i < 3;

                _rankDiscs[i].color = podium ? SheetKit.Tinted(Design.SurfaceGroup, medal, 0.3f) : Design.SurfaceControl;
                _ranks[i].color = podium ? medal : Design.TextSecondary;
                _scores[i].text = entry.Score.ToString("N0", Str.Culture);
                _scores[i].color = i == 0 ? Design.Gold : Design.TextPrimary;

                var when = entry.When;
                _dates[i].text = when == System.DateTime.MinValue ? "" : when.ToString("dd.MM.yyyy");
            }

            Sheet.SetBodyHeight(count == 0 ? EmptyHeight : Row * count);

            // The table fills in from the top, one place after another.
            for (int i = 0; i < count; i++) StartCoroutine(Cascade.In(_rows[i], 0.2f + i * 0.05f));
        }
    }

    /// <summary>
    /// Lifetime numbers and the achievements built on them, the two halves of one sheet. The
    /// numbers are a grid of tiles, read at a glance; achievements a scrolling list with how far
    /// along each one is. Achievements open first while there is a tier the player has not seen.
    /// </summary>
    public sealed class StatsScreen : SheetScreen
    {
        const int Columns = 3;
        const float TileHeight = 208f;
        const float TileGap = 16f;
        const float AchievementRow = 184f;
        const float Medal = 108f;
        const float StarSize = 40f;

        UiSegmented _tabs;
        RectTransform _statsPanel;
        RectTransform _achievementPanel;
        UiScroll _scroll;
        float _statsHeight;
        float _achievementsHeight;

        readonly List<TextMeshProUGUI> _values = new List<TextMeshProUGUI>();
        readonly List<Image> _medals = new List<Image>();
        readonly List<TextMeshProUGUI> _goals = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> _counts = new List<TextMeshProUGUI>();
        readonly List<Image> _bars = new List<Image>();
        readonly List<Image[]> _stars = new List<Image[]>();

        protected override void Build()
        {
            BuildSheet(Str.StatsHeading, Icons.Chart, Design.TintTeal);

            _tabs = UiSegmented.Create(Body, "Tabs", Design.ContentWidth, Str.StatsTab, Str.AchievementsTab);
            SheetKit.PinTop(_tabs.Rect, 0f);
            _tabs.Changed += i => { Audio.PlayClick(); ShowTab(i == 1, animate: true); };

            float panelTop = Design.SegmentHeight + Design.Space4;

            // Numbers: tiles, three to a row.
            var labels = Str.StatLabels;
            int rows = Mathf.CeilToInt(labels.Length / (float)Columns);
            float tileWidth = (Design.ContentWidth - TileGap * (Columns - 1)) / Columns;
            float gridHeight = rows * TileHeight + (rows - 1) * TileGap;

            _statsPanel = UiBuilder.Node(Body, "Stats");
            _statsPanel.sizeDelta = new Vector2(Design.ContentWidth, gridHeight);
            SheetKit.PinTop(_statsPanel, panelTop);
            for (int i = 0; i < labels.Length; i++)
            {
                int column = i % Columns, row = i / Columns;
                var tile = UiBuilder.Image(_statsPanel, "Tile" + i, Art.PanelGradient(Design.RadiusMd), SheetKit.Lifted(Design.SurfaceGroup));
                tile.rectTransform.sizeDelta = new Vector2(tileWidth, TileHeight);
                UiBuilder.Hairline(tile.rectTransform, "Edge", new Vector2(tileWidth, TileHeight), Design.RadiusMd, Color.white.WithAlpha(0.12f));
                SheetKit.PinTop(tile.rectTransform, row * (TileHeight + TileGap),
                    -Design.ContentWidth * 0.5f + tileWidth * 0.5f + column * (tileWidth + TileGap));

                var value = UiBuilder.Label(tile.rectTransform, "Value", "0", Design.Headline, Design.TextPrimary, Design.FontDisplay);
                value.rectTransform.sizeDelta = new Vector2(tileWidth - Design.Space4, 96f);
                value.rectTransform.anchoredPosition = new Vector2(0f, 38f);
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.enableAutoSizing = true;
                value.fontSizeMin = Design.Label;
                value.fontSizeMax = Design.Headline;
                _values.Add(value);

                var caption = UiBuilder.Label(tile.rectTransform, "Caption", labels[i], Design.Caption, Design.TextTertiary, Design.FontDisplay);
                caption.rectTransform.sizeDelta = new Vector2(tileWidth - Design.Space3, 100f);
                caption.rectTransform.anchoredPosition = new Vector2(0f, -50f);
                caption.textWrappingMode = TextWrappingModes.Normal;
                caption.lineSpacing = -18f;
                caption.enableAutoSizing = true;
                caption.fontSizeMin = Design.Caption * 0.86f;
                caption.fontSizeMax = Design.Caption;
            }

            _statsHeight = panelTop + gridHeight;

            // Achievements: one scrolling list.
            int count = Achievements.All.Length;
            float listHeight = count * AchievementRow;
            float viewport = Mathf.Min(listHeight, Sheet.MaxBody - panelTop);
            _achievementsHeight = panelTop + viewport;

            _scroll = UiScroll.Create(Body, "Achievements", new Vector2(Design.ContentWidth, viewport));
            SheetKit.PinTop(_scroll.Rect, panelTop);
            _achievementPanel = _scroll.Rect;
            _scroll.SetContentHeight(listHeight);

            var list = SheetKit.Group(_scroll.Content, "List", 0f, listHeight);
            for (int i = 0; i < count; i++) BuildAchievementRow(list, listHeight, i);

            // More below: the list fades into the sheet at its bottom edge rather than being cut.
            if (listHeight > viewport)
            {
                var fade = UiBuilder.Image(_scroll.Rect, "Fade", Art.VerticalFade, Design.SurfaceSheet);
                fade.type = Image.Type.Simple;
                fade.rectTransform.anchorMin = new Vector2(0f, 0f);
                fade.rectTransform.anchorMax = new Vector2(1f, 0f);
                fade.rectTransform.pivot = new Vector2(0.5f, 0f);
                fade.rectTransform.sizeDelta = new Vector2(0f, 96f);
                fade.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        void BuildAchievementRow(RectTransform list, float listHeight, int i)
        {
            var entry = Achievements.All[i];
            float y = SheetKit.RowY(listHeight, i, AchievementRow);
            float left = SheetKit.RowLeft;
            float right = SheetKit.RowRight;
            float textLeft = left + Medal + Design.Space3;
            const float starsWidth = StarSize * 3f + 8f * 2f;

            if (i > 0) SheetKit.Separator(list, y + AchievementRow * 0.5f, textLeft);

            var medal = UiBuilder.Image(list, "Medal" + i, Art.Node, Design.SurfaceControl);
            medal.type = Image.Type.Simple;
            medal.rectTransform.sizeDelta = new Vector2(Medal, Medal);
            medal.rectTransform.anchoredPosition = new Vector2(left + Medal * 0.5f, y);
            var glyph = UiBuilder.Image(medal.rectTransform, "Glyph", entry.Icon(), Color.white);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(Medal * 0.52f, Medal * 0.52f);
            _medals.Add(medal);

            float nameWidth = right - starsWidth - Design.Space3 - textLeft;
            SheetKit.RowLabel(list, "Name" + i, entry.Name, textLeft, nameWidth, y + 44f);

            var goal = UiBuilder.Label(list, "Goal" + i, "", Design.Caption, Design.TextSecondary, Design.FontDisplay, TextAlignmentOptions.Left);
            goal.rectTransform.sizeDelta = new Vector2(right - textLeft, 56f);
            goal.rectTransform.anchoredPosition = new Vector2(textLeft + (right - textLeft) * 0.5f, y - 4f);
            goal.textWrappingMode = TextWrappingModes.NoWrap;
            _goals.Add(goal);

            const float countWidth = 250f;
            float barWidth = right - textLeft - countWidth - Design.Space3;
            _bars.Add(SheetKit.Bar(list, "Bar" + i, barWidth, new Vector2(textLeft + barWidth * 0.5f, y - 50f), Design.Mint));

            var count = UiBuilder.Label(list, "Count" + i, "", Design.Caption, Design.TextSecondary, Design.FontDisplay, TextAlignmentOptions.Right);
            count.rectTransform.sizeDelta = new Vector2(countWidth, 56f);
            count.rectTransform.anchoredPosition = new Vector2(right - countWidth * 0.5f, y - 50f);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.enableAutoSizing = true;
            count.fontSizeMin = Design.Caption * 0.8f;
            count.fontSizeMax = Design.Caption;
            _counts.Add(count);

            var stars = new Image[entry.Tiers.Length];
            for (int s = 0; s < stars.Length; s++)
            {
                var star = UiBuilder.Image(list, $"Star{i}_{s}", Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                star.rectTransform.sizeDelta = new Vector2(StarSize, StarSize);
                star.rectTransform.anchoredPosition = new Vector2(right - starsWidth + StarSize * 0.5f + s * (StarSize + 8f), y + 44f);
                stars[s] = star;
            }
            _stars.Add(stars);
        }

        public void ShowTab(bool achievements) => ShowTab(achievements, animate: false);

        void ShowTab(bool achievements, bool animate)
        {
            _statsPanel.gameObject.SetActive(!achievements);
            _achievementPanel.gameObject.SetActive(achievements);
            _tabs.Select(achievements ? 1 : 0, animate);
            Sheet.SetBodyHeight(achievements ? _achievementsHeight : _statsHeight, animate);
            if (achievements) Progress.MarkAchievementsSeen();
            if (achievements && _barTargets != null && isActiveAndEnabled) StartCoroutine(FillBars(_barTargets));
        }

        float[] _barTargets;

        protected override void OnShow()
        {
            _barTargets = new float[Achievements.All.Length];
            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var entry = Achievements.All[i];
                long target = entry.NextTarget;
                int reached = entry.Reached;
                bool done = reached == entry.Tiers.Length;
                long value = System.Math.Min(entry.Value(), target);

                // The disc takes the colour of the best tier reached: bronze, silver, gold.
                _medals[i].color = reached == 0 ? Design.SurfaceControl : reached == 1 ? Design.Bronze : reached == 2 ? Design.Silver : Design.Gold;
                _goals[i].text = Str.AchievementGoal(entry.Id, target);
                _counts[i].text = done ? Str.Earned : $"{value.ToString("N0", Str.Culture)} / {target.ToString("N0", Str.Culture)}";
                _counts[i].color = done ? Design.Gold : Design.TextSecondary;
                _bars[i].color = done ? Design.Gold : Design.Mint;
                _barTargets[i] = value / (float)System.Math.Max(1, target);
                SheetKit.SetBar(_bars[i], 0f);

                var stars = _stars[i];
                for (int s = 0; s < stars.Length; s++)
                    stars[s].color = s < reached ? Design.Gold : Design.SurfaceControl;
            }

            // Plain counts count up as the sheet opens; the two that are not plain counts are set.
            long[] counts =
            {
                Progress.Games, HighScores.Best, Progress.TotalScore, Progress.TotalLines, Progress.TotalPieces, -1,
                Progress.MonoLinesTotal, Progress.PerfectClears, Progress.GemsCollected, Progress.PowersUsed, -1,
                Progress.DailyBestStreak
            };
            _values[5].text = Progress.BestCombo > 1 ? $"×{ScoreRules.ComboMultiplier(Progress.BestCombo):0.#}" : "—";
            _values[10].text = $"{Progress.LevelStars}/{Progress.LevelsCompleted * 3}";
            StartCoroutine(CountUp(counts));

            _scroll.ScrollTo(0f);
            ShowTab(Progress.HasUnseenAchievements, animate: false);
        }

        IEnumerator CountUp(long[] counts)
        {
            const float duration = 0.8f;
            for (float t = 0f; t < duration + 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = Ease.OutCubic(Mathf.Clamp01((t - 0.2f) / duration));
                for (int i = 0; i < counts.Length && i < _values.Count; i++)
                    if (counts[i] >= 0) _values[i].text = ((long)Mathf.Round(counts[i] * k)).ToString("N0", Str.Culture);
                yield return null;
            }

            for (int i = 0; i < counts.Length && i < _values.Count; i++)
                if (counts[i] >= 0) _values[i].text = counts[i].ToString("N0", Str.Culture);
        }

        /// <summary>The progress bars fill from empty when the list comes into view.</summary>
        IEnumerator FillBars(float[] targets)
        {
            const float duration = 0.7f;
            for (float t = 0f; t < duration + 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = Ease.OutCubic(Mathf.Clamp01((t - 0.25f) / duration));
                for (int i = 0; i < targets.Length; i++) SheetKit.SetBar(_bars[i], targets[i] * k);
                yield return null;
            }

            for (int i = 0; i < targets.Length; i++) SheetKit.SetBar(_bars[i], targets[i]);
        }
    }

    /// <summary>
    /// Themes. A live preview of the whole look on top — ground, glow, board, blocks, the main
    /// button — and under it one row per theme, each row painted in that theme's own ground with
    /// its seven blocks on its own board colour. The list itself is a comparison of the moods.
    ///
    /// Picking an unlocked theme applies it at once: the interface is rebuilt around this sheet,
    /// so the page behind the dim and the sheet itself change in the same tap. Picking a locked
    /// one previews it with the stars it still needs, so a theme can be wanted before it is earned.
    /// </summary>
    public sealed class ThemesScreen : SheetScreen
    {
        const float Row = 168f;
        const float RowGap = 16f;
        const float Swatch = 44f;
        const float SwatchGap = 8f;
        const float Ring = 6f;

        readonly List<Image> _rings = new List<Image>();
        readonly List<GameObject> _checks = new List<GameObject>();
        readonly List<GameObject> _locks = new List<GameObject>();
        readonly List<CanvasGroup> _swatches = new List<CanvasGroup>();

        ThemePreview _preview;
        int _focus;

        protected override void Build()
        {
            BuildSheet(Str.ThemesHeading, Icons.Palette, Design.TintRose);

            int count = Themes.All.Length;
            float list = count * Row + (count - 1) * RowGap;

            // Rows get what they need; the preview takes the rest, up to its full size.
            float scale = Mathf.Clamp((Sheet.MaxBody - list - Design.Space5) / ThemePreview.Height, 0.7f, 1f);
            float previewHeight = ThemePreview.Height * scale;

            _preview = new ThemePreview();
            _preview.Build(Body, Design.ContentWidth, scale);
            SheetKit.PinTop(_preview.Holder, 0f);

            float top = previewHeight + Design.Space5;
            for (int i = 0; i < count; i++)
            {
                BuildRow(i, top);
                top += Row + RowGap;
            }

            Sheet.SetBodyHeight(top - RowGap);
        }

        void BuildRow(int index, float top)
        {
            var theme = Themes.All[index];
            var size = new Vector2(Design.ContentWidth, Row);

            // A ring just outside the row: mint on the theme in use, white on one being previewed.
            var ring = UiBuilder.Panel(Body, "Ring" + index, size + Vector2.one * Ring * 2f, Design.Mint, Design.RadiusMd + Ring);
            SheetKit.PinTop(ring.rectTransform, top - Ring);
            _rings.Add(ring);

            var row = UiBuilder.Button(Body, "Theme" + index, size, UiButton.Style.Bare, null, Design.Body);
            SheetKit.PinTop(row.Rect, top);
            row.SetRestColor(Color.Lerp(theme.BgTop, theme.BgBottom, 0.4f));
            row.Clicked += () => Select(index);

            float left = -Design.ContentWidth * 0.5f + Design.Space4;
            float right = Design.ContentWidth * 0.5f - Design.Space4;

            const float nameWidth = 480f;
            var name = UiBuilder.Label(row.Content, "Name", Str.ThemeName(theme.Name), Design.Body, theme.TextOnGround,
                Design.FontDisplay, TextAlignmentOptions.Left);
            name.rectTransform.sizeDelta = new Vector2(nameWidth, 80f);
            name.rectTransform.anchoredPosition = new Vector2(left + nameWidth * 0.5f, 36f);

            // The seven blocks on the theme's own board colour.
            float capsuleWidth = Swatch * 7f + SwatchGap * 6f + Design.Space2 * 2f;
            float capsuleHeight = Swatch + Design.Space2 * 2f;
            var capsule = UiBuilder.Panel(row.Content, "Board", new Vector2(capsuleWidth, capsuleHeight), theme.BoardSurface, capsuleHeight * 0.5f);
            capsule.rectTransform.anchoredPosition = new Vector2(left + capsuleWidth * 0.5f - Design.Space1, -36f);
            var group = capsule.gameObject.AddComponent<CanvasGroup>();
            for (int c = 0; c < theme.Blocks.Length; c++)
            {
                var block = UiBuilder.Image(capsule.rectTransform, "B" + c, Art.Block, theme.Blocks[c]);
                block.rectTransform.sizeDelta = new Vector2(Swatch, Swatch);
                block.rectTransform.anchoredPosition = new Vector2(-capsuleWidth * 0.5f + Design.Space2 + Swatch * 0.5f + c * (Swatch + SwatchGap), 0f);
            }
            _swatches.Add(group);

            // In use: a mint disc with a tick.
            var check = UiBuilder.Image(row.Content, "Check", Art.Disc, Design.Mint);
            check.type = Image.Type.Simple;
            check.rectTransform.sizeDelta = new Vector2(84f, 84f);
            check.rectTransform.anchoredPosition = new Vector2(right - 42f, 0f);
            var tick = UiBuilder.Image(check.rectTransform, "Tick", Icons.Check, Color.white);
            tick.type = Image.Type.Simple;
            tick.rectTransform.sizeDelta = new Vector2(52f, 52f);
            _checks.Add(check.gameObject);

            // Locked: what it costs, on a chip of the sheet's own colour so it reads on any ground.
            const float chipWidth = 232f;
            const float chipHeight = 92f;
            var chip = UiBuilder.Panel(row.Content, "Locked", new Vector2(chipWidth, chipHeight), Design.SurfaceSheet, chipHeight * 0.5f);
            chip.rectTransform.anchoredPosition = new Vector2(right - chipWidth * 0.5f, 0f);
            var padlock = UiBuilder.Image(chip.rectTransform, "Lock", Icons.Lock, Design.TextSecondary);
            padlock.type = Image.Type.Simple;
            padlock.rectTransform.sizeDelta = new Vector2(48f, 48f);
            padlock.rectTransform.anchoredPosition = new Vector2(-chipWidth * 0.5f + 52f, 0f);
            var star = UiBuilder.Image(chip.rectTransform, "Star", Icons.Star, Design.Gold);
            star.type = Image.Type.Simple;
            star.rectTransform.sizeDelta = new Vector2(46f, 46f);
            star.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            var need = UiBuilder.Label(chip.rectTransform, "Need", theme.StarsToUnlock.ToString(), Design.Body, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Left);
            need.rectTransform.sizeDelta = new Vector2(100f, chipHeight);
            need.rectTransform.anchoredPosition = new Vector2(70f, 2f);
            _locks.Add(chip.gameObject);
        }

        protected override void OnShow()
        {
            _focus = Progress.Theme;
            Populate();
        }

        void Populate()
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                bool open = Themes.IsUnlocked(i);
                bool current = Progress.Theme == i;
                bool focused = _focus == i && !current;

                _checks[i].SetActive(current);
                _locks[i].SetActive(!open);
                _swatches[i].alpha = open ? 1f : 0.45f;
                _rings[i].gameObject.SetActive(current || focused);
                _rings[i].color = current ? Design.Mint : Design.TextPrimary;
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
                // Rebuilds the interface, this sheet included; nothing on this instance is used after.
                App.SetTheme(index);
                return;
            }

            _focus = index;
            Populate();
            StartCoroutine(Tween.Punch(_preview.Root, 0.04f, 0.22f));
        }
    }
}
