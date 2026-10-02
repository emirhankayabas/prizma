using System;
using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The daily puzzle's sheet, opened from the menu. Two halves under one segmented control.
    ///
    /// Today, as three blocks with air between them: the streak with this week under it; today's
    /// puzzle — its day, how hard that day is, and three numbers (what it asks, in how many moves,
    /// how long so far; once solved: the time, the stars, the tries); and the countdown to the next
    /// puzzle in a pill of its own. The first version stacked all of it in one column of rows with
    /// the countdown pressed against the numbers above it and a reminder switch under it.
    ///
    /// Badges: sixteen medallions and a card about the one picked — the closest to being earned
    /// opens first, because what can be reached next is the interesting one.
    ///
    /// There is no calendar and no reminder switch. The puzzle is today's or nothing: a missed day
    /// stays missed, which is the whole weight of a streak. The week strip shows the days; it does
    /// not open them. Notifications are asked for once at first launch and live in settings.
    /// </summary>
    public sealed class DailyScreen : SheetScreen
    {
        const float StreakCard = 466f;
        const float PuzzleCard = 320f;
        const float CountdownPill = 96f;
        const float PanelHeight = StreakCard + Design.Space3 + PuzzleCard + Design.Space4 + CountdownPill;
        const float StreakType = 160f;
        const float Flame = 120f;
        const float WeekDisc = 84f;
        const float DetailCard = 236f;
        const int BadgeColumns = 4;

        UiSegmented _tabs;
        RectTransform _todayPanel;
        RectTransform _badgePanel;
        UiButton _play;

        // Today
        Image _flame;
        TextMeshProUGUI _streak;
        TextMeshProUGUI _streakCaption;
        readonly Image[] _weekDiscs = new Image[7];
        readonly Image[] _weekTicks = new Image[7];
        readonly TextMeshProUGUI[] _weekLabels = new TextMeshProUGUI[7];
        TextMeshProUGUI _dayName;
        Image _gradeChip;
        TextMeshProUGUI _grade;
        readonly TextMeshProUGUI[] _statValues = new TextMeshProUGUI[3];
        readonly TextMeshProUGUI[] _statLabels = new TextMeshProUGUI[3];
        Image _goalIcon;
        RectTransform _starRow;
        readonly Image[] _stars = new Image[3];
        RectTransform _countdownPill;
        Image _clock;
        TextMeshProUGUI _countdownLabel;
        TextMeshProUGUI _countdown;
        float _countdownTimeWidth;

        // Badges
        readonly List<UiButton> _badgeTiles = new List<UiButton>();
        readonly List<Image> _medals = new List<Image>();
        readonly List<Image> _medalIcons = new List<Image>();
        Image _detailMedal;
        Image _detailIcon;
        TextMeshProUGUI _detailName;
        TextMeshProUGUI _detailGoal;
        TextMeshProUGUI _detailProgress;
        Image _detailBar;
        int _selected;

        DateTime _shownDay;
        int _countdownShown = -1;
        LevelDefinition _puzzle;

        float _statY;
        float _column;

        protected override void Build()
        {
            BuildSheet(Str.DailyPuzzle, Icons.Calendar, Design.Mint);

            _tabs = UiSegmented.Create(Body, "Tabs", Design.ContentWidth, Str.TodayTab, Str.BadgesTab);
            SheetKit.PinTop(_tabs.Rect, 0f);
            _tabs.Changed += i => { Audio.PlayClick(); ShowTab(i == 1); };

            float panelTop = Design.SegmentHeight + Design.Space4;

            _todayPanel = UiBuilder.Node(Body, "Today");
            _todayPanel.sizeDelta = new Vector2(Design.ContentWidth, PanelHeight);
            SheetKit.PinTop(_todayPanel, panelTop);
            BuildToday(_todayPanel);

            _badgePanel = UiBuilder.Node(Body, "Badges");
            _badgePanel.sizeDelta = new Vector2(Design.ContentWidth, PanelHeight);
            SheetKit.PinTop(_badgePanel, panelTop);
            BuildBadges(_badgePanel);

            float playTop = panelTop + PanelHeight + Design.Space4;
            _play = UiBuilder.Button(Body, "Play", new Vector2(Design.ContentWidth, Design.ButtonLg), UiButton.Style.Primary,
                Str.StartPuzzle, Design.Headline);
            SheetKit.PinTop(_play.Rect, playTop);
            _play.Clicked += OnPlay;

            Sheet.SetBodyHeight(playTop + Design.ButtonLg);
        }

        // ------------------------------------------------------------------ today

        void BuildToday(RectTransform panel)
        {
            float inner = Design.ContentWidth - Design.GroupPadding * 2f;

            // The streak: the one number this sheet is really about. The week sits under it, in
            // the same card, because the week is what the streak is made of.
            var streak = SheetKit.Group(panel, "Streak", 0f, StreakCard);
            float cardTop = StreakCard * 0.5f;
            float heroY = cardTop - 110f;

            _flame = UiBuilder.Image(streak, "Flame", Icons.Flame, Design.Gold);
            _flame.type = Image.Type.Simple;
            _flame.rectTransform.sizeDelta = new Vector2(Flame, Flame);

            _streak = UiBuilder.Label(streak, "Streak", "0", StreakType, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingDisplay);
            _streak.rectTransform.sizeDelta = new Vector2(420f, 180f);
            UiBuilder.TextShadow(_streak, 0.4f, -0.3f, 0.45f);
            _heroY = heroY;

            _streakCaption = UiBuilder.Label(streak, "StreakCaption", "", Design.Caption, Design.TextSecondary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            _streakCaption.rectTransform.sizeDelta = new Vector2(inner, 56f);
            _streakCaption.rectTransform.anchoredPosition = new Vector2(0f, cardTop - 206f);

            var rule = UiBuilder.Image(streak, "Rule", Art.Panel(0f), Design.Hairline);
            rule.type = Image.Type.Simple;
            rule.rectTransform.sizeDelta = new Vector2(inner, 3f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, cardTop - 256f);

            // This week, Monday first: solved days in mint with a tick, today ringed in the accent.
            float cell = inner / 7f;
            float labelY = cardTop - 300f;
            float discY = cardTop - 388f;
            var weekdays = Str.Weekdays;
            for (int i = 0; i < 7; i++)
            {
                float x = -inner * 0.5f + cell * (i + 0.5f);
                var label = UiBuilder.Label(streak, "Weekday" + i, weekdays[i], Design.Caption, Design.TextTertiary, Design.FontDisplay);
                label.rectTransform.sizeDelta = new Vector2(cell, 52f);
                label.rectTransform.anchoredPosition = new Vector2(x, labelY);
                _weekLabels[i] = label;

                var disc = UiBuilder.Image(streak, "Day" + i, Art.Disc, Design.SurfaceControl);
                disc.type = Image.Type.Simple;
                disc.rectTransform.sizeDelta = new Vector2(WeekDisc, WeekDisc);
                disc.rectTransform.anchoredPosition = new Vector2(x, discY);
                _weekDiscs[i] = disc;

                var tick = UiBuilder.Image(disc.rectTransform, "Tick", Icons.Check, Color.white);
                tick.type = Image.Type.Simple;
                tick.rectTransform.sizeDelta = new Vector2(WeekDisc * 0.56f, WeekDisc * 0.56f);
                _weekTicks[i] = tick;
            }

            // Today's puzzle: the day and its grade across the top, three numbers under a hairline.
            float puzzleTop = StreakCard + Design.Space3;
            var puzzle = SheetKit.Group(panel, "Puzzle", puzzleTop, PuzzleCard);
            float headerY = PuzzleCard * 0.5f - 72f;

            _dayName = UiBuilder.Label(puzzle, "Day", "", Design.Label, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _dayName.rectTransform.sizeDelta = new Vector2(inner * 0.6f, 70f);
            _dayName.rectTransform.anchoredPosition = new Vector2(-inner * 0.5f + inner * 0.3f, headerY);

            _gradeChip = UiBuilder.Panel(puzzle, "GradeChip", new Vector2(220f, 76f), Design.SurfaceControl, 38f);
            _grade = UiBuilder.Label(_gradeChip.rectTransform, "Grade", "", Design.Caption, Design.Mint, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _grade.rectTransform.sizeDelta = new Vector2(400f, 76f);
            _grade.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            _gradeChipY = headerY;

            float lineY = PuzzleCard * 0.5f - 144f;
            var line = UiBuilder.Image(puzzle, "Line", Art.Panel(0f), Design.Hairline);
            line.type = Image.Type.Simple;
            line.rectTransform.sizeDelta = new Vector2(inner, 3f);
            line.rectTransform.anchoredPosition = new Vector2(0f, lineY);

            _column = inner / 3f;
            _statY = lineY - 66f;
            for (int i = 0; i < 3; i++)
            {
                float x = -inner * 0.5f + _column * (i + 0.5f);
                var value = UiBuilder.Label(puzzle, "Stat" + i, "", Design.Headline, Design.TextPrimary, Design.FontDisplay);
                value.rectTransform.sizeDelta = new Vector2(_column, 90f);
                value.rectTransform.anchoredPosition = new Vector2(x, _statY);
                _statValues[i] = value;

                var caption = UiBuilder.Label(puzzle, "StatLabel" + i, "", Design.Caption, Design.TextTertiary, Design.FontDisplay,
                    tracking: Design.TrackingLabel * 0.5f);
                caption.rectTransform.sizeDelta = new Vector2(_column, 52f);
                caption.rectTransform.anchoredPosition = new Vector2(x, _statY - 62f);
                _statLabels[i] = caption;

                if (i > 0)
                {
                    var divider = UiBuilder.Image(puzzle, "Divider" + i, Art.Panel(0f), Design.Hairline);
                    divider.type = Image.Type.Simple;
                    divider.rectTransform.sizeDelta = new Vector2(3f, 110f);
                    divider.rectTransform.anchoredPosition = new Vector2(-inner * 0.5f + _column * i, _statY - 30f);
                }
            }

            _goalIcon = UiBuilder.Image(puzzle, "GoalIcon", Art.Crystal, Design.Crystal);
            _goalIcon.type = Image.Type.Simple;
            _goalIcon.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);

            // Once solved, the middle column shows the stars instead of the move budget.
            _starRow = UiBuilder.Node(puzzle, "Stars");
            _starRow.anchoredPosition = new Vector2(0f, _statY + 2f);
            for (int i = 0; i < 3; i++)
            {
                var star = UiBuilder.Image(_starRow, "Star" + i, Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                float size = i == 1 ? 72f : 56f;
                star.rectTransform.sizeDelta = new Vector2(size, size);
                star.rectTransform.anchoredPosition = new Vector2((i - 1) * 68f, i == 1 ? 8f : 0f);
                _stars[i] = star;
            }

            // The next puzzle, in a pill of its own.
            float pillTop = puzzleTop + PuzzleCard + Design.Space4;
            _countdownPill = UiBuilder.Panel(panel, "Countdown", new Vector2(600f, CountdownPill), Design.SurfaceGroup, CountdownPill * 0.5f).rectTransform;
            SheetKit.PinTop(_countdownPill, pillTop);

            _clock = UiBuilder.Image(_countdownPill, "Clock", Icons.Clock, Design.TextSecondary);
            _clock.type = Image.Type.Simple;
            _clock.rectTransform.sizeDelta = new Vector2(56f, 56f);

            _countdownLabel = UiBuilder.Label(_countdownPill, "Label", Str.NextPuzzle, Design.Caption, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _countdownLabel.rectTransform.sizeDelta = new Vector2(400f, CountdownPill);

            _countdown = UiBuilder.Label(_countdownPill, "Time", "00:00:00", Design.Label, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left);
            _countdown.rectTransform.sizeDelta = new Vector2(300f, CountdownPill);

            // The time is measured once at its widest, so the pill does not twitch every second.
            _countdownTimeWidth = _countdown.GetPreferredValues("00:00:00").x + 8f;
            LayoutCountdown();
        }

        float _heroY;
        float _gradeChipY;

        void LayoutCountdown()
        {
            float label = _countdownLabel.GetPreferredValues(_countdownLabel.text).x;
            const float icon = 56f;
            float group = icon + Design.Space2 + label + Design.Space3 + _countdownTimeWidth;
            float width = group + Design.Space5 * 2f;
            _countdownPill.sizeDelta = new Vector2(width, CountdownPill);

            float x = -group * 0.5f;
            _clock.rectTransform.anchoredPosition = new Vector2(x + icon * 0.5f, 0f);
            x += icon + Design.Space2;
            _countdownLabel.rectTransform.anchoredPosition = new Vector2(x + _countdownLabel.rectTransform.sizeDelta.x * 0.5f, 2f);
            x += label + Design.Space3;
            _countdown.rectTransform.anchoredPosition = new Vector2(x + _countdown.rectTransform.sizeDelta.x * 0.5f, 2f);
        }

        /// <summary>Flame and number centred as one group, whatever the number's width.</summary>
        void LayoutHero()
        {
            float text = _streak.GetPreferredValues(_streak.text).x;
            float group = Flame + Design.Space2 + text;
            _flame.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + Flame * 0.5f, _heroY);
            _streak.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + Flame + Design.Space2 + _streak.rectTransform.sizeDelta.x * 0.5f, _heroY + 6f);
        }

        // ------------------------------------------------------------------ badges

        void BuildBadges(RectTransform panel)
        {
            int rows = Mathf.CeilToInt(DailyBadges.All.Length / (float)BadgeColumns);
            float gap = Design.Space2;
            float gridHeight = PanelHeight - DetailCard - Design.Space3;
            float tileW = (Design.ContentWidth - gap * (BadgeColumns - 1)) / BadgeColumns;
            float tileH = (gridHeight - gap * (rows - 1)) / rows;
            float medal = Mathf.Min(tileW, tileH) * 0.8f;

            for (int i = 0; i < DailyBadges.All.Length; i++)
            {
                int slot = i;
                var badge = DailyBadges.All[i];
                int col = i % BadgeColumns, row = i / BadgeColumns;

                var tile = UiBuilder.Button(panel, "Badge" + i, new Vector2(tileW, tileH), UiButton.Style.Bare, null, Design.Body);
                SheetKit.PinTop(tile.Rect, row * (tileH + gap), -Design.ContentWidth * 0.5f + tileW * 0.5f + col * (tileW + gap));
                tile.SetRestColor(Design.SurfaceGroup);
                tile.Clicked += () => { Audio.PlayClick(); Select(slot); };

                var medalImage = UiBuilder.Image(tile.Content, "Medal", Art.Medal, Color.white);
                medalImage.type = Image.Type.Simple;
                medalImage.rectTransform.sizeDelta = new Vector2(medal, medal);

                var icon = UiBuilder.Image(tile.Content, "Icon", badge.Icon(), Color.white);
                icon.type = Image.Type.Simple;
                icon.rectTransform.sizeDelta = new Vector2(medal * 0.46f, medal * 0.46f);

                _badgeTiles.Add(tile);
                _medals.Add(medalImage);
                _medalIcons.Add(icon);
            }

            // The card about the picked badge: its medal, its name, what it asks, how far along.
            var card = SheetKit.Group(panel, "Detail", PanelHeight - DetailCard, DetailCard);
            const float big = 148f;
            float left = -Design.ContentWidth * 0.5f + Design.GroupPadding;
            float right = Design.ContentWidth * 0.5f - Design.GroupPadding;

            _detailMedal = UiBuilder.Image(card, "Medal", Art.Medal, Color.white);
            _detailMedal.type = Image.Type.Simple;
            _detailMedal.rectTransform.sizeDelta = new Vector2(big, big);
            _detailMedal.rectTransform.anchoredPosition = new Vector2(left + big * 0.5f, 0f);
            _detailIcon = UiBuilder.Image(_detailMedal.rectTransform, "Icon", Icons.Star, Color.white);
            _detailIcon.type = Image.Type.Simple;
            _detailIcon.rectTransform.sizeDelta = new Vector2(big * 0.46f, big * 0.46f);

            float textLeft = left + big + Design.Space4;
            float textWidth = right - textLeft;
            _detailName = SheetKit.RowLabel(card, "Name", "", textLeft, textWidth, 66f, Design.Body);

            _detailGoal = UiBuilder.Label(card, "Goal", "", Design.Caption, Design.TextSecondary, Design.FontDisplay, TextAlignmentOptions.Left);
            _detailGoal.rectTransform.sizeDelta = new Vector2(textWidth, 96f);
            _detailGoal.rectTransform.anchoredPosition = new Vector2(textLeft + textWidth * 0.5f, 0f);
            _detailGoal.textWrappingMode = TextWrappingModes.Normal;
            _detailGoal.lineSpacing = -14f;
            _detailGoal.enableAutoSizing = true;
            _detailGoal.fontSizeMin = Design.Caption * 0.86f;
            _detailGoal.fontSizeMax = Design.Caption;

            const float countWidth = 170f;
            float barWidth = textWidth - countWidth - Design.Space3;
            _detailBar = SheetKit.Bar(card, "Bar", barWidth, new Vector2(textLeft + barWidth * 0.5f, -68f), Design.Mint);
            _detailProgress = UiBuilder.Label(card, "Progress", "", Design.Label, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Right);
            _detailProgress.rectTransform.sizeDelta = new Vector2(countWidth, 70f);
            _detailProgress.rectTransform.anchoredPosition = new Vector2(right - countWidth * 0.5f, -66f);
            _detailProgress.enableAutoSizing = true;
            _detailProgress.fontSizeMin = Design.Caption;
            _detailProgress.fontSizeMax = Design.Label;
        }

        void PopulateBadges()
        {
            for (int i = 0; i < DailyBadges.All.Length; i++)
            {
                var badge = DailyBadges.All[i];
                bool earned = badge.Earned;
                _medals[i].color = earned ? badge.Tint() : Design.SurfaceControl;
                _medalIcons[i].color = earned ? Color.white : Design.TextTertiary;
                // A punch cut short by the sheet closing would leave a medal a size too large.
                _medals[i].rectTransform.localScale = Vector3.one;
                // Lit in the accent, not in the control surface: an unearned medal is that very
                // colour, and on the picked tile it vanished into its own highlight.
                _badgeTiles[i].SetHighlighted(i == _selected, SheetKit.Tinted(Design.SurfaceGroup, Design.AccentA, 0.24f));
            }

            var picked = DailyBadges.All[_selected];
            bool done = picked.Earned;
            _detailMedal.color = done ? picked.Tint() : Design.SurfaceControl;
            _detailIcon.sprite = picked.Icon();
            _detailIcon.color = done ? Color.white : Design.TextTertiary;
            _detailName.text = picked.Name;
            _detailName.color = done ? picked.Tint() : Design.TextPrimary;
            _detailGoal.text = picked.Goal;
            _detailProgress.text = done ? Str.Earned : $"{Math.Min(picked.Value(), picked.Target)}/{picked.Target}";
            _detailProgress.color = done ? picked.Tint() : Design.TextSecondary;
            _detailBar.color = done ? picked.Tint() : Design.Mint;
            SheetKit.SetBar(_detailBar, picked.Progress);
        }

        void Select(int index)
        {
            _selected = index;
            PopulateBadges();
            StartCoroutine(Tween.Punch(_medals[index].transform, 0.14f, 0.24f));
        }

        // ------------------------------------------------------------------ state

        public void ShowTab(bool badges)
        {
            _todayPanel.gameObject.SetActive(!badges);
            _badgePanel.gameObject.SetActive(badges);
            _tabs.Select(badges ? 1 : 0, animate: true);

            if (badges)
            {
                PopulateBadges();
                Progress.MarkBadgesSeen();
                _tabs.SetDot(1, false);
            }
        }

        protected override void OnShow()
        {
            _selected = DailyBadges.Closest();
            ShowTab(false);
            _tabs.Select(0, animate: false);
            _tabs.SetDot(1, Progress.HasUnseenBadges);
            Populate();

            // A streak that moved since the sheet last showed it counts up to the new number, once.
            int streak = Progress.DailyStreak;
            int shown = Progress.DailyStreakShown;
            if (streak > shown && shown >= 0)
                StartCoroutine(CountStreak(shown, streak));
            Progress.DailyStreakShown = streak;
        }

        IEnumerator CountStreak(int from, int to)
        {
            _streak.text = from.ToString();
            LayoutHero();
            yield return new WaitForSecondsRealtime(0.45f);

            _streak.text = to.ToString();
            LayoutHero();
            Audio.PlayStreak();
            StartCoroutine(Tween.Punch(_streak.transform, 0.3f, 0.32f));
            yield return Tween.Punch(_flame.transform, 0.4f, 0.36f);
        }

        void Populate()
        {
            var today = DateTime.Now.Date;
            _shownDay = today;
            _puzzle = LevelGenerator.GenerateDaily(today, Design.PaletteSize);

            Sheet.Title.text = Str.DailyTitle(_puzzle.Number);

            int streak = Progress.DailyStreak;
            _streak.rectTransform.localScale = Vector3.one;
            _flame.rectTransform.localScale = Vector3.one;
            _streak.text = streak.ToString();
            _streak.color = streak > 0 ? Design.TextPrimary : Design.TextTertiary;
            _flame.color = streak > 0 ? Design.Gold : Design.SurfaceControl;
            _streakCaption.text = Str.StreakWord + "  ·  " + Str.BestStreak(Progress.DailyBestStreak);
            LayoutHero();

            // The week, Monday to Sunday.
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            for (int i = 0; i < 7; i++)
            {
                var day = monday.AddDays(i);
                bool solved = Progress.PlayedDaily(day);
                bool isToday = day == today;
                bool future = day > today;

                _weekDiscs[i].color = solved ? Design.Mint : isToday ? Design.AccentA : Design.SurfaceControl;
                _weekDiscs[i].color = _weekDiscs[i].color.WithAlpha(future ? 0.5f : 1f);
                _weekTicks[i].gameObject.SetActive(solved);
                _weekLabels[i].color = isToday ? Design.TextPrimary : Design.TextTertiary;
            }

            // Today's puzzle.
            int grade = LevelGenerator.DailyGrade(today.DayOfWeek);
            var gradeColor = GradeColor(grade);
            _dayName.text = Str.DayName(today.DayOfWeek);
            _grade.text = Str.Grade(grade);
            _grade.color = gradeColor;
            _gradeChip.color = SheetKit.Tinted(Design.SurfaceGroup, gradeColor, 0.2f);
            float chipWidth = _grade.GetPreferredValues(_grade.text).x + Design.Space5;
            float inner = Design.ContentWidth - Design.GroupPadding * 2f;
            _gradeChip.rectTransform.sizeDelta = new Vector2(chipWidth, 76f);
            _gradeChip.rectTransform.anchoredPosition = new Vector2(inner * 0.5f - chipWidth * 0.5f, _gradeChipY);

            // How it went, or how it is going.
            var solve = Progress.TodaySolve;
            _starRow.gameObject.SetActive(solve != null);
            _goalIcon.gameObject.SetActive(solve == null);
            if (solve != null)
            {
                SetStat(0, Str.Clock(solve.Seconds), Str.TimeLabel);
                SetStat(1, "", Str.StarsLabel);
                SetStat(2, solve.Attempts.ToString(), Str.AttemptsLabel);
                for (int i = 0; i < 3; i++) _stars[i].color = i < solve.Stars ? Design.Gold : Design.SurfaceControl;
            }
            else
            {
                var saved = RunStore.Peek(GameMode.Daily);
                bool resumable = saved != null && saved.LevelNumber == _puzzle.Number && saved.LevelMoveLimit > 0;
                int attempts = Progress.DailyAttempts(today);
                float seconds = Progress.DailyBankedSeconds(today) + (resumable ? saved.PlaySeconds : 0f);

                SetStat(0, _puzzle.Target.ToString(), Str.GoalWord(_puzzle.Goal));
                SetStat(1, _puzzle.MoveLimit.ToString(), Str.MovesLabel);
                SetStat(2, attempts > 0 ? Str.Clock(seconds) : "—", Str.TimeLabel);

                // The goal's icon beside its number, the pair centred in the column.
                bool lines = _puzzle.Goal == GoalKind.Lines;
                _goalIcon.sprite = lines ? Icons.Rows : Art.Crystal;
                _goalIcon.color = lines ? Design.Mint : Design.Crystal;
                float number = _statValues[0].GetPreferredValues(_statValues[0].text).x;
                float group = Design.IconSm + Design.Space2 + number;
                float centre = -inner * 0.5f + _column * 0.5f;
                _goalIcon.rectTransform.anchoredPosition = new Vector2(centre - group * 0.5f + Design.IconSm * 0.5f, _statY + 2f);
                _statValues[0].rectTransform.anchoredPosition = new Vector2(centre + group * 0.5f - number * 0.5f, _statY);

                _play.Label.text = resumable ? Str.Continue : attempts > 0 ? Str.TryAgain : Str.StartPuzzle;
            }

            if (solve != null)
            {
                _statValues[0].rectTransform.anchoredPosition = new Vector2(-inner * 0.5f + _column * 0.5f, _statY);
                _play.Label.text = Str.Share;
            }

            _countdownShown = -1;
            UpdateCountdown();
        }

        void SetStat(int i, string value, string label)
        {
            _statValues[i].text = value;
            _statLabels[i].text = label;
        }

        static Color GradeColor(int grade)
        {
            switch (grade)
            {
                case 0: return Design.Mint;
                case 1: return Design.Gold;
                case 2: return Design.PreviewTint(3);
                default: return Design.Prism;
            }
        }

        void UpdateCountdown()
        {
            var now = DateTime.Now;
            var left = now.Date.AddDays(1) - now;
            int seconds = (int)left.TotalSeconds;
            if (seconds == _countdownShown) return;
            _countdownShown = seconds;
            _countdown.text = Str.Countdown(left);
        }

        protected override void Update()
        {
            base.Update();

            // Midnight while the sheet is open: a new puzzle, a new day on the strip.
            if (DateTime.Now.Date != _shownDay)
            {
                Populate();
                return;
            }

            UpdateCountdown();
        }

        void OnPlay()
        {
            Audio.PlayClick();

            var solve = Progress.TodaySolve;
            if (solve != null)
            {
                ShareSheet.ShareText(Str.ShareText(_puzzle.Number, solve.Seconds, solve.Stars, solve.Attempts,
                    Progress.DailyStreak, ShareSheet.StoreLink), Str.Share);
                return;
            }

            App.PlayDaily();
        }
    }
}
