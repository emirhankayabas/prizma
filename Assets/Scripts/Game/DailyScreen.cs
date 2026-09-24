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
    /// The daily puzzle's card, opened from the menu. Two tabs.
    ///
    /// Today: the streak, this week's solved days, what today's puzzle is (its weekday sets how
    /// hard it is), and either how today's solve went or how the attempts are going — with the
    /// countdown to the next puzzle underneath. The reminder switch sits at the bottom.
    ///
    /// Badges: sixteen medallions and a line about the one picked — the closest to being earned
    /// opens first, because what can be reached next is the interesting one.
    ///
    /// There is no calendar. The puzzle is today's or nothing: a missed day stays missed, which is
    /// the whole weight of a streak. The week strip shows the days; it does not open them.
    /// </summary>
    public sealed class DailyScreen : AppScreen
    {
        public override bool IsModal => true;

        const float TabRow = 132f;
        const float HeroRow = 236f;
        const float WeekRow = 176f;
        const float InfoRow = 164f;
        const float StatusRow = 220f;
        const float ReminderRow = 148f;
        const float DetailRow = 200f;
        const int BadgeColumns = 4;

        TextMeshProUGUI _heading;
        UiButton _todayTab;
        UiButton _badgeTab;
        Image _badgeDot;
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
        TextMeshProUGUI _dayGrade;
        Image _goalIcon;
        TextMeshProUGUI _goal;
        readonly TextMeshProUGUI[] _statValues = new TextMeshProUGUI[3];
        readonly TextMeshProUGUI[] _statLabels = new TextMeshProUGUI[3];
        readonly Image[] _stars = new Image[3];
        RectTransform _starRow;
        TextMeshProUGUI _countdown;
        TextMeshProUGUI _reminderNote;
        UiToggle _reminderToggle;

        // Badges
        readonly List<UiButton> _badgeTiles = new List<UiButton>();
        readonly List<Image> _medals = new List<Image>();
        readonly List<Image> _medalIcons = new List<Image>();
        Image _detailMedal;
        Image _detailIcon;
        TextMeshProUGUI _detailName;
        TextMeshProUGUI _detailGoal;
        TextMeshProUGUI _detailProgress;
        int _selected;

        DateTime _shownDay;
        int _countdownShown = -1;
        LevelDefinition _puzzle;

        protected override void Build()
        {
            float preferred = TabRow + HeroRow + WeekRow + InfoRow + StatusRow + ReminderRow;

            // A short page takes the same card with every row a little tighter, never off-screen.
            float room = App.PageHeight - Design.Space4 * 2f - ModalCard.HeightFor(0f, Design.ButtonLg);
            float k = Mathf.Clamp(room / preferred, 0.78f, 1f);
            float body = preferred * k;

            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(body, Design.ButtonLg));
            var card = ModalCard.Build(Root, "", size, out var content);
            _heading = card.Find("Heading").GetComponent<TextMeshProUGUI>();
            // Clear of the close button in the corner, whatever the language.
            _heading.rectTransform.sizeDelta = new Vector2(size.x - (Design.TouchTarget + Design.Space3) * 2f - Design.Space3, 110f);
            _heading.enableAutoSizing = true;
            _heading.fontSizeMin = Design.Headline;
            _heading.fontSizeMax = Design.Title;

            float inner = ModalCard.InnerWidth(size);
            float top = ModalCard.ContentTop(size);

            // Top-right corner: the card has a main action, so closing gets the corner, like the
            // result card's share.
            var close = UiBuilder.Button(content, "Close", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon,
                null, Design.Body, Icons.Close);
            close.Rect.anchoredPosition = new Vector2(size.x * 0.5f - Design.Space3 - Design.TouchTarget * 0.5f,
                size.y * 0.5f - Design.Space3 - Design.TouchTarget * 0.5f);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            // Tabs.
            float tabWidth = (inner - Design.Space3) * 0.5f;
            float tabY = top - TabRow * k * 0.5f + Design.Space2;
            _todayTab = UiBuilder.Button(content, "TodayTab", new Vector2(tabWidth, TabRow - Design.Space4), UiButton.Style.Secondary,
                Str.TodayTab, Design.Label);
            _todayTab.Rect.anchoredPosition = new Vector2(-(tabWidth + Design.Space3) * 0.5f, tabY);
            _todayTab.Clicked += () => { Audio.PlayClick(); ShowTab(false); };

            _badgeTab = UiBuilder.Button(content, "BadgeTab", new Vector2(tabWidth, TabRow - Design.Space4), UiButton.Style.Secondary,
                Str.BadgesTab, Design.Label);
            _badgeTab.Rect.anchoredPosition = new Vector2((tabWidth + Design.Space3) * 0.5f, tabY);
            _badgeTab.Clicked += () => { Audio.PlayClick(); ShowTab(true); };

            // A steady mint dot while a badge has not been looked at. Waits; does not call.
            _badgeDot = UiBuilder.Image(_badgeTab.Content, "NewDot", Art.Disc, Design.Mint);
            _badgeDot.type = Image.Type.Simple;
            _badgeDot.rectTransform.sizeDelta = new Vector2(Design.Space4 * 0.7f, Design.Space4 * 0.7f);
            _badgeDot.rectTransform.anchoredPosition = new Vector2(tabWidth * 0.5f - Design.Space4, (TabRow - Design.Space4) * 0.5f - Design.Space3);

            float panelTop = top - TabRow * k;
            _todayPanel = UiBuilder.Node(content, "Today");
            BuildToday(_todayPanel, inner, panelTop, k);

            _badgePanel = UiBuilder.Node(content, "Badges");
            BuildBadges(_badgePanel, inner, panelTop, body - TabRow * k);

            _play = UiBuilder.Button(content, "Play", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary, Str.StartPuzzle, Design.Headline);
            _play.Clicked += OnPlay;
            ModalCard.StackFromBottom(content, Design.Space3, _play.Rect);
        }

        // ------------------------------------------------------------------ today

        void BuildToday(RectTransform panel, float inner, float y, float k)
        {
            float left = -inner * 0.5f;

            // The streak: the one number this card is really about.
            float heroY = y - HeroRow * k * 0.5f;
            _flame = UiBuilder.Image(panel, "Flame", Icons.Flame, Design.Gold);
            _flame.type = Image.Type.Simple;
            _flame.rectTransform.sizeDelta = new Vector2(132f, 132f);

            _streak = UiBuilder.Label(panel, "Streak", "0", 150f, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingDisplay);
            _streak.rectTransform.sizeDelta = new Vector2(420f, 170f);
            UiBuilder.TextShadow(_streak, 0.4f, -0.3f, 0.45f);

            _streakCaption = UiBuilder.Label(panel, "StreakCaption", "", Design.Caption, Design.TextSecondary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            _streakCaption.rectTransform.sizeDelta = new Vector2(inner, 60f);
            _streakCaption.rectTransform.anchoredPosition = new Vector2(0f, heroY - HeroRow * k * 0.5f + 40f);
            _heroY = heroY + 26f * k;

            // This week, Monday first: solved days in mint with a tick, today ringed in the accent.
            float weekY = y - HeroRow * k - WeekRow * k * 0.5f;
            float cell = inner / 7f;
            float disc = Mathf.Min(96f, cell - Design.Space2) * Mathf.Lerp(0.85f, 1f, (k - 0.78f) / 0.22f);
            var weekdays = Str.Weekdays;
            for (int i = 0; i < 7; i++)
            {
                float x = left + cell * (i + 0.5f);
                var label = UiBuilder.Label(panel, "Weekday" + i, weekdays[i], Design.Caption, Design.TextTertiary, Design.FontDisplay);
                label.rectTransform.sizeDelta = new Vector2(cell, 56f);
                label.rectTransform.anchoredPosition = new Vector2(x, weekY + disc * 0.5f + 10f);
                _weekLabels[i] = label;

                var circle = UiBuilder.Image(panel, "Day" + i, Art.Disc, Design.SurfaceInset);
                circle.type = Image.Type.Simple;
                circle.rectTransform.sizeDelta = new Vector2(disc, disc);
                circle.rectTransform.anchoredPosition = new Vector2(x, weekY - 24f);
                _weekDiscs[i] = circle;

                var tick = UiBuilder.Image(circle.rectTransform, "Tick", Icons.Check, Color.white);
                tick.type = Image.Type.Simple;
                tick.rectTransform.sizeDelta = new Vector2(disc * 0.56f, disc * 0.56f);
                _weekTicks[i] = tick;
            }

            // What today's puzzle is.
            float infoY = y - (HeroRow + WeekRow) * k - InfoRow * k * 0.5f;
            var band = UiBuilder.Panel(panel, "InfoBand", new Vector2(inner, InfoRow * k - Design.Space2), Design.SurfaceInset, Design.RadiusMd);
            band.rectTransform.anchoredPosition = new Vector2(0f, infoY);

            // Two centred lines: the day and how hard it is, then what it asks and in how many moves.
            float line = Mathf.Min(34f, (InfoRow * k - Design.Space2) * 0.24f);
            _dayGrade = UiBuilder.Label(panel, "DayGrade", "", Design.Label, Design.Mint, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _dayGrade.rectTransform.sizeDelta = new Vector2(inner - Design.Space4 * 2f, 64f);
            _dayGrade.rectTransform.anchoredPosition = new Vector2(0f, infoY + line);

            _goal = UiBuilder.Label(panel, "Goal", "", Design.Label, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _goal.rectTransform.sizeDelta = new Vector2(inner - Design.Space4 * 2f, 64f);

            _goalIcon = UiBuilder.Image(panel, "GoalIcon", Art.Crystal, Design.Crystal);
            _goalIcon.type = Image.Type.Simple;
            _goalIcon.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            _infoY = infoY - line;

            // How today went, or is going: three numbers, the stars once solved, and the countdown.
            float statusTop = y - (HeroRow + WeekRow + InfoRow) * k;
            float statY = statusTop - 78f * k;
            float column = inner / 3f;
            for (int i = 0; i < 3; i++)
            {
                float x = left + column * (i + 0.5f);
                var value = UiBuilder.Label(panel, "Stat" + i, "", Design.Headline, Design.TextPrimary, Design.FontDisplay);
                value.rectTransform.sizeDelta = new Vector2(column, 90f);
                value.rectTransform.anchoredPosition = new Vector2(x, statY);
                _statValues[i] = value;

                var caption = UiBuilder.Label(panel, "StatLabel" + i, "", Design.Caption, Design.TextTertiary, Design.FontDisplay,
                    tracking: Design.TrackingLabel * 0.5f);
                caption.rectTransform.sizeDelta = new Vector2(column, 56f);
                caption.rectTransform.anchoredPosition = new Vector2(x, statY - 58f);
                _statLabels[i] = caption;
            }

            // Once solved, the middle column shows the stars instead of the move budget.
            _starRow = UiBuilder.Node(panel, "Stars");
            _starRow.anchoredPosition = new Vector2(0f, statY + 4f);
            for (int i = 0; i < 3; i++)
            {
                var star = UiBuilder.Image(_starRow, "Star" + i, Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                float starSize = i == 1 ? 72f : 58f;
                star.rectTransform.sizeDelta = new Vector2(starSize, starSize);
                star.rectTransform.anchoredPosition = new Vector2((i - 1) * 70f, i == 1 ? 8f : 0f);
                _stars[i] = star;
            }

            _countdown = UiBuilder.Label(panel, "Countdown", "", Design.Label, Design.TextSecondary, Design.FontDisplay,
                tracking: Design.TrackingLabel * 0.5f);
            _countdown.rectTransform.sizeDelta = new Vector2(inner, 70f);
            _countdown.rectTransform.anchoredPosition = new Vector2(0f, statusTop - StatusRow * k + 30f);

            // The reminder switch.
            float reminderY = y - (HeroRow + WeekRow + InfoRow + StatusRow) * k - ReminderRow * k * 0.5f;
            var bell = UiBuilder.Image(panel, "Bell", Icons.Bell, Design.TextSecondary);
            bell.type = Image.Type.Simple;
            bell.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            bell.rectTransform.anchoredPosition = new Vector2(left + Design.IconSm * 0.5f, reminderY);

            const float textWidth = 520f;
            float textX = left + Design.IconSm + Design.Space3 + textWidth * 0.5f;
            var reminder = UiBuilder.Label(panel, "ReminderLabel", Str.ReminderLabel, Design.Label, Design.TextPrimary,
                Design.FontDisplay, TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            reminder.rectTransform.sizeDelta = new Vector2(textWidth, 64f);
            reminder.rectTransform.anchoredPosition = new Vector2(textX, reminderY + 24f);

            _reminderNote = UiBuilder.Label(panel, "ReminderNote", "", Design.Caption, Design.TextSecondary,
                Design.FontDisplay, TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            _reminderNote.rectTransform.sizeDelta = new Vector2(textWidth, 56f);
            _reminderNote.rectTransform.anchoredPosition = new Vector2(textX, reminderY - 28f);

            const float toggleWidth = 212f;
            _reminderToggle = UiToggle.Create(panel, "ReminderToggle", toggleWidth, 118f, GameSettings.Reminder);
            _reminderToggle.Rect.anchoredPosition = new Vector2(inner * 0.5f - toggleWidth * 0.5f, reminderY);
            _reminderToggle.ValueChanged += OnReminderToggled;
        }

        float _heroY;
        float _infoY;

        /// <summary>Flame and number centred as one group, whatever the number's width.</summary>
        void LayoutHero()
        {
            float text = _streak.GetPreferredValues(_streak.text).x;
            float group = 132f + Design.Space2 + text;
            _flame.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + 66f, _heroY);
            _streak.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + 132f + Design.Space2 + 210f, _heroY + 4f);
        }

        void OnReminderToggled(bool on)
        {
            Audio.PlayClick();
            if (!on)
            {
                Reminder.Disable();
                PopulateReminder();
                return;
            }

            Reminder.Enable(allowed =>
            {
                if (this == null) return;
                _reminderToggle.IsOn = allowed;
                PopulateReminder();
            });
        }

        void PopulateReminder()
        {
            _reminderToggle.IsOn = GameSettings.Reminder;
            _reminderNote.text = Progress.DailySolves > 0 && Progress.DailySolveMinute >= 0
                ? Str.ReminderAt(Progress.DailySolveMinute)
                : Str.ReminderAfterSolve;
        }

        // ------------------------------------------------------------------ badges

        void BuildBadges(RectTransform panel, float inner, float y, float height)
        {
            int rows = Mathf.CeilToInt(DailyBadges.All.Length / (float)BadgeColumns);
            float gridHeight = height - DetailRow - Design.Space3;
            float gap = Design.Space2;
            float tileW = (inner - gap * (BadgeColumns - 1)) / BadgeColumns;
            float tileH = Mathf.Min(tileW, (gridHeight - gap * (rows - 1)) / rows);
            float medal = Mathf.Min(tileW, tileH) * 0.78f;

            float gridTop = y - Design.Space1;
            for (int i = 0; i < DailyBadges.All.Length; i++)
            {
                int slot = i;
                var badge = DailyBadges.All[i];
                int col = i % BadgeColumns, row = i / BadgeColumns;

                var tile = UiBuilder.Button(panel, "Badge" + i, new Vector2(tileW, tileH), UiButton.Style.Secondary, null, Design.Body);
                tile.Rect.anchoredPosition = new Vector2(-inner * 0.5f + tileW * 0.5f + col * (tileW + gap),
                    gridTop - tileH * 0.5f - row * (tileH + gap));
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

            // The line about the picked badge: its medal, its name, what it asks, how far along.
            float detailY = y - height + DetailRow * 0.5f;
            var box = UiBuilder.Panel(panel, "Detail", new Vector2(inner, DetailRow - Design.Space2), Design.SurfaceInset, Design.RadiusMd);
            box.rectTransform.anchoredPosition = new Vector2(0f, detailY);

            const float big = 132f;
            float left = -inner * 0.5f + Design.Space4;
            _detailMedal = UiBuilder.Image(panel, "DetailMedal", Art.Medal, Color.white);
            _detailMedal.type = Image.Type.Simple;
            _detailMedal.rectTransform.sizeDelta = new Vector2(big, big);
            _detailMedal.rectTransform.anchoredPosition = new Vector2(left + big * 0.5f, detailY);
            _detailIcon = UiBuilder.Image(panel, "DetailIcon", Icons.Star, Color.white);
            _detailIcon.type = Image.Type.Simple;
            _detailIcon.rectTransform.sizeDelta = new Vector2(big * 0.46f, big * 0.46f);
            _detailIcon.rectTransform.anchoredPosition = _detailMedal.rectTransform.anchoredPosition;

            float textLeft = left + big + Design.Space3;
            float textWidth = inner * 0.5f - Design.Space4 - textLeft;
            _detailName = UiBuilder.Label(panel, "DetailName", "", Design.Body, Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left);
            _detailName.rectTransform.sizeDelta = new Vector2(textWidth, 72f);
            _detailName.rectTransform.anchoredPosition = new Vector2(textLeft + textWidth * 0.5f, detailY + 30f);

            _detailGoal = UiBuilder.Label(panel, "DetailGoal", "", Design.Caption, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Left);
            _detailGoal.rectTransform.sizeDelta = new Vector2(textWidth - 170f, 100f);
            _detailGoal.rectTransform.anchoredPosition = new Vector2(textLeft + (textWidth - 170f) * 0.5f, detailY - 36f);
            _detailGoal.textWrappingMode = TextWrappingModes.Normal;
            _detailGoal.enableAutoSizing = true;
            _detailGoal.fontSizeMin = Design.Caption;
            _detailGoal.fontSizeMax = Design.Caption;

            _detailProgress = UiBuilder.Label(panel, "DetailProgress", "", Design.Body, Design.TextSecondary, Design.FontDisplay,
                TextAlignmentOptions.Right);
            _detailProgress.rectTransform.sizeDelta = new Vector2(200f, 72f);
            _detailProgress.rectTransform.anchoredPosition = new Vector2(inner * 0.5f - Design.Space4 - 100f, detailY - 30f);
        }

        void PopulateBadges()
        {
            for (int i = 0; i < DailyBadges.All.Length; i++)
            {
                var badge = DailyBadges.All[i];
                bool earned = badge.Earned;
                _medals[i].color = earned ? badge.Tint() : Color.white.WithAlpha(0.10f);
                _medalIcons[i].color = earned ? Color.white : Color.white.WithAlpha(0.28f);
                _badgeTiles[i].SetHighlighted(i == _selected, Design.SurfaceLeader);
            }

            var picked = DailyBadges.All[_selected];
            bool done = picked.Earned;
            _detailMedal.color = done ? picked.Tint() : Color.white.WithAlpha(0.12f);
            _detailIcon.sprite = picked.Icon();
            _detailIcon.color = done ? Color.white : Color.white.WithAlpha(0.35f);
            _detailName.text = picked.Name;
            _detailName.color = done ? picked.Tint() : Design.TextPrimary;
            _detailGoal.text = picked.Goal;
            _detailProgress.text = done ? Str.Earned : $"{Math.Min(picked.Value(), picked.Target)}/{picked.Target}";
            _detailProgress.color = done ? Design.Mint : Design.TextSecondary;
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
            _todayTab.SetHighlighted(!badges, Design.SurfaceLeader);
            _badgeTab.SetHighlighted(badges, Design.SurfaceLeader);

            if (badges)
            {
                PopulateBadges();
                Progress.MarkBadgesSeen();
                _badgeDot.gameObject.SetActive(false);
            }
        }

        protected override void OnShow()
        {
            _selected = DailyBadges.Closest();
            ShowTab(false);
            _badgeDot.gameObject.SetActive(Progress.HasUnseenBadges);
            Populate();
            PopulateReminder();

            // A streak that moved since the card last showed it counts up to the new number, once.
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
            yield return new WaitForSecondsRealtime(0.35f);

            _streak.text = to.ToString();
            LayoutHero();
            Audio.PlayStreak();
            StartCoroutine(Tween.Punch(_streak.transform, 0.3f, 0.32f));
            yield return Tween.Punch(_flame.transform, 0.4f, 0.36f);
        }

        void Populate()
        {
            var now = DateTime.Now;
            var today = now.Date;
            _shownDay = today;
            _puzzle = LevelGenerator.GenerateDaily(today, Design.PaletteSize);

            _heading.text = Str.DailyTitle(_puzzle.Number);

            int streak = Progress.DailyStreak;
            _streak.text = streak.ToString();
            _streak.color = streak > 0 ? Design.TextPrimary : Design.TextTertiary;
            _flame.color = streak > 0 ? Design.Gold : Color.white.WithAlpha(0.2f);
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

                _weekDiscs[i].color = solved ? Design.Mint : isToday ? Design.AccentA : Design.SurfaceInset;
                _weekDiscs[i].color = _weekDiscs[i].color.WithAlpha(future ? 0.45f : 1f);
                _weekTicks[i].gameObject.SetActive(solved);
                _weekLabels[i].color = isToday ? Design.TextPrimary : Design.TextTertiary;
            }

            // Today's puzzle.
            int grade = LevelGenerator.DailyGrade(today.DayOfWeek);
            _dayGrade.text = Str.DayName(today.DayOfWeek) + "  ·  " + Str.Grade(grade);
            _dayGrade.color = GradeColor(grade);

            _goal.text = Str.GoalText(_puzzle.Goal, _puzzle.Target) + "  ·  " + Str.Moves(_puzzle.MoveLimit);
            _goalIcon.sprite = _puzzle.Goal == GoalKind.Lines ? Icons.Rows : Art.Crystal;
            _goalIcon.color = _puzzle.Goal == GoalKind.Lines ? Design.Mint : Design.Crystal;
            // Icon and words centred as one group.
            float goalWidth = _goal.GetPreferredValues(_goal.text).x;
            float group = Design.IconSm + Design.Space2 + goalWidth;
            _goalIcon.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + Design.IconSm * 0.5f, _infoY);
            _goal.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + Design.IconSm + Design.Space2 + _goal.rectTransform.sizeDelta.x * 0.5f, _infoY + 2f);

            // How it went, or how it is going.
            var solve = Progress.TodaySolve;
            _starRow.gameObject.SetActive(solve != null);
            if (solve != null)
            {
                SetStat(0, Str.Clock(solve.Seconds), Str.TimeLabel, Design.TextPrimary);
                SetStat(1, "", Str.StarsLabel, Design.TextPrimary);
                SetStat(2, solve.Attempts.ToString(), Str.AttemptsLabel, Design.TextPrimary);
                for (int i = 0; i < 3; i++) _stars[i].color = i < solve.Stars ? Design.Gold : Color.white.WithAlpha(0.16f);
            }
            else
            {
                int attempts = Progress.DailyAttempts(today);
                var saved = RunStore.Peek(GameMode.Daily);
                bool resumable = saved != null && saved.LevelNumber == _puzzle.Number && saved.LevelMoveLimit > 0;
                float seconds = Progress.DailyBankedSeconds(today) + (resumable ? saved.PlaySeconds : 0f);

                SetStat(0, attempts > 0 ? Str.Clock(seconds) : "—", Str.TimeLabel, Design.TextSecondary);
                SetStat(1, _puzzle.MoveLimit.ToString(), Str.MovesLabel, Design.TextSecondary);
                SetStat(2, attempts.ToString(), Str.AttemptsLabel, Design.TextSecondary);
            }

            // The main action: the puzzle while it is open, sharing once it is solved.
            if (solve != null) _play.Label.text = Str.Share;
            else
            {
                var saved = RunStore.Peek(GameMode.Daily);
                bool resumable = saved != null && saved.LevelNumber == _puzzle.Number && saved.LevelMoveLimit > 0;
                int attempts = Progress.DailyAttempts(today);
                _play.Label.text = resumable ? Str.Continue : attempts > 0 ? Str.TryAgain : Str.StartPuzzle;
            }

            _countdownShown = -1;
            UpdateCountdown();
        }

        void SetStat(int i, string value, string label, Color color)
        {
            _statValues[i].text = value;
            _statValues[i].color = color;
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
            _countdown.text = Str.NextPuzzleIn(Str.Countdown(left));
        }

        void Update()
        {
            // Midnight while the card is open: a new puzzle, a new day on the strip.
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

    /// <summary>
    /// The reminder offer, made once, over the result card of the first solve: the player has
    /// just seen what the daily is, so being told about tomorrow's means something. Yes asks
    /// Android for the permission; either answer is final until the switch on the daily card.
    /// </summary>
    public sealed class ReminderScreen : AppScreen
    {
        public override bool IsModal => true;

        TextMeshProUGUI _body;
        Image _bell;

        protected override void Build()
        {
            const float bellRow = 180f;
            const float textRow = 200f;
            float stack = Design.ButtonLg + Design.Space3 + Design.ButtonMd;
            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(bellRow + textRow, stack));
            ModalCard.Build(Root, Str.ReminderAskTitle, size, out var content);
            float inner = ModalCard.InnerWidth(size);
            float top = ModalCard.ContentTop(size);

            _bell = UiBuilder.Image(content, "Bell", Icons.Bell, Design.Gold);
            _bell.type = Image.Type.Simple;
            _bell.rectTransform.sizeDelta = new Vector2(150f, 150f);
            _bell.rectTransform.anchoredPosition = new Vector2(0f, top - bellRow * 0.5f);

            _body = UiBuilder.Label(content, "Body", "", Design.Body, Design.TextPrimary, Design.FontDisplay);
            _body.rectTransform.sizeDelta = new Vector2(inner, textRow);
            _body.rectTransform.anchoredPosition = new Vector2(0f, top - bellRow - textRow * 0.5f);
            _body.textWrappingMode = TextWrappingModes.Normal;

            var yes = UiBuilder.Button(content, "Yes", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary, Str.RemindMe, Design.Headline);
            yes.Clicked += () =>
            {
                Audio.PlayClick();
                App.CloseModal();
                Reminder.Enable(null);
            };

            var no = UiBuilder.Button(content, "No", new Vector2(inner, Design.ButtonMd), UiButton.Style.Secondary, Str.NotNow, Design.Body);
            no.Clicked += () =>
            {
                Audio.PlayClick();
                GameSettings.ReminderAsked = true;
                App.CloseModal();
            };

            ModalCard.StackFromBottom(content, Design.Space3, yes.Rect, no.Rect);
        }

        protected override void OnShow()
        {
            int minute = Progress.DailySolveMinute >= 0 ? Progress.DailySolveMinute : DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            _body.text = Str.ReminderAskBody(minute);

            // A small swing of the bell as the card lands. Once.
            StartCoroutine(Ring());
        }

        IEnumerator Ring()
        {
            var rect = _bell.rectTransform;
            const float duration = 0.7f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI * 5f) * 16f * (1f - k));
                yield return null;
            }
            rect.localEulerAngles = Vector3.zero;
        }
    }
}
