using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The daily calendar, opened from the daily card. A month of days: the ones played carry a
    /// mint tick, today is lit in the accent, the future is dimmed. Any past day can be played —
    /// its puzzle is the one everyone had that day — so a missed day is caught up on rather than
    /// lost, which is what keeps a broken streak from ending the habit. Only today moves the
    /// streak (<see cref="Progress.RecordDaily"/>).
    /// </summary>
    public sealed class DailyScreen : AppScreen
    {
        public override bool IsModal => true;

        const int Columns = 7;
        const int Rows = 6;
        const float WeekdayRow = 64f;
        const float StreakRow = 96f;

        /// <summary>How far back the calendar goes. A year of catch-up is plenty and keeps the list of months bounded.</summary>
        const int MonthsBack = 12;

        readonly List<UiButton> _days = new List<UiButton>();
        readonly List<Image> _ticks = new List<Image>();
        TextMeshProUGUI _heading;
        TextMeshProUGUI _streak;
        UiButton _prev;
        UiButton _next;
        UiButton _play;

        int _year;
        int _month;

        protected override void Build()
        {
            float inner = Design.ContentWidth - Design.CardPadding * 2f;
            float cell = inner / Columns;

            float body = WeekdayRow + cell * Rows + StreakRow;
            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(body, Design.ButtonLg));
            var card = ModalCard.Build(Root, "", size, out var content);
            _heading = card.Find("Heading").GetComponent<TextMeshProUGUI>();

            float headingY = size.y * 0.5f - Design.CardHeading;
            float arrowX = inner * 0.5f - Design.TouchTarget * 0.5f;

            _prev = UiBuilder.Button(content, "Prev", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon,
                null, Design.Body, Icons.ChevronLeft);
            _prev.Rect.anchoredPosition = new Vector2(-arrowX, headingY);
            _prev.Clicked += () => { Audio.PlayClick(); Step(-1); };

            _next = UiBuilder.Button(content, "Next", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon,
                null, Design.Body, Icons.ChevronRight);
            _next.Rect.anchoredPosition = new Vector2(arrowX, headingY);
            _next.Clicked += () => { Audio.PlayClick(); Step(1); };

            float top = ModalCard.ContentTop(size);
            float left = -inner * 0.5f;

            var weekdays = Str.Weekdays;
            for (int i = 0; i < Columns; i++)
            {
                var label = UiBuilder.Label(content, "Weekday" + i, weekdays[i], Design.Caption, Design.TextTertiary, Design.FontDisplay);
                label.rectTransform.sizeDelta = new Vector2(cell, WeekdayRow);
                label.rectTransform.anchoredPosition = new Vector2(left + cell * (i + 0.5f), top - WeekdayRow * 0.5f);
            }

            float gridTop = top - WeekdayRow;
            float tile = cell - Design.Space2;
            for (int i = 0; i < Columns * Rows; i++)
            {
                int slot = i;
                var day = UiBuilder.Button(content, "Day" + i, new Vector2(tile, tile), UiButton.Style.Secondary, "0", Design.Body);
                day.Rect.anchoredPosition = new Vector2(left + cell * (i % Columns + 0.5f), gridTop - cell * (i / Columns + 0.5f));
                day.Label.rectTransform.anchoredPosition = new Vector2(0f, tile * 0.08f);
                day.Clicked += () => OnDay(slot);

                var tick = UiBuilder.Image(day.Content, "Tick", Icons.Check, Design.Mint);
                tick.type = Image.Type.Simple;
                tick.rectTransform.sizeDelta = new Vector2(tile * 0.34f, tile * 0.34f);
                tick.rectTransform.anchoredPosition = new Vector2(0f, -tile * 0.28f);

                _days.Add(day);
                _ticks.Add(tick);
            }

            float streakY = gridTop - cell * Rows - StreakRow * 0.5f;
            var flame = UiBuilder.Image(content, "Flame", Icons.Flame, Design.Gold);
            flame.type = Image.Type.Simple;
            flame.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            flame.rectTransform.anchoredPosition = new Vector2(-150f, streakY);
            _streak = UiBuilder.Label(content, "Streak", "", Design.Body, Design.Gold, Design.FontDisplay, TextAlignmentOptions.Left);
            _streak.rectTransform.sizeDelta = new Vector2(420f, StreakRow);
            _streak.rectTransform.anchoredPosition = new Vector2(-150f + Design.IconSm * 0.5f + Design.Space2 + 210f, streakY + 3f);

            _play = UiBuilder.Button(content, "Play", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary, Str.PlayToday, Design.Headline);
            _play.Clicked += () => { Audio.PlayClick(); App.PlayDaily(); };
            ModalCard.StackFromBottom(content, Design.Space3, _play.Rect);
        }

        protected override void OnShow()
        {
            var now = DateTime.Now;
            _year = now.Year;
            _month = now.Month;
            Populate();
        }

        void Step(int months)
        {
            var target = new DateTime(_year, _month, 1).AddMonths(months);
            if (!InRange(target)) return;
            _year = target.Year;
            _month = target.Month;
            Populate();
        }

        static bool InRange(DateTime month)
        {
            var now = DateTime.Now;
            var current = new DateTime(now.Year, now.Month, 1);
            return month <= current && month >= current.AddMonths(-MonthsBack);
        }

        DateTime First => new DateTime(_year, _month, 1);

        /// <summary>Monday-first column of the month's first day.</summary>
        int Offset => ((int)First.DayOfWeek + 6) % 7;

        void Populate()
        {
            _heading.text = Str.MonthHeading(_year, _month);

            var today = DateTime.Now.Date;
            int daysInMonth = DateTime.DaysInMonth(_year, _month);

            for (int i = 0; i < _days.Count; i++)
            {
                int dayNumber = i - Offset + 1;
                var button = _days[i];
                bool inMonth = dayNumber >= 1 && dayNumber <= daysInMonth;
                button.gameObject.SetActive(inMonth);
                if (!inMonth) continue;

                var date = new DateTime(_year, _month, dayNumber);
                bool isToday = date == today;
                bool future = date > today;
                bool played = Progress.PlayedDaily(date);

                button.Label.text = dayNumber.ToString();
                button.Label.color = isToday ? Design.OnAccent : played ? Design.Mint : Design.TextPrimary;
                button.SetHighlighted(isToday, Design.AccentA);
                button.SetEnabled(!future);
                _ticks[i].gameObject.SetActive(played);
                _ticks[i].color = isToday ? Design.OnAccent : Design.Mint;
            }

            _prev.SetEnabled(InRange(First.AddMonths(-1)));
            _next.SetEnabled(InRange(First.AddMonths(1)));

            _streak.text = Str.DayStreak(Progress.DailyStreak);

            var saved = RunStore.Peek(Core.GameMode.Daily);
            bool resumable = saved != null && saved.Seed == Core.GameSession.DailySeed(today);
            _play.Label.text = resumable ? Str.ContinueToday : Str.PlayToday;
        }

        void OnDay(int slot)
        {
            int dayNumber = slot - Offset + 1;
            var date = new DateTime(_year, _month, dayNumber);
            if (date > DateTime.Now.Date) return;

            Audio.PlayClick();
            App.PlayDaily(date);
        }
    }
}
