using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Title page. One primary action — play — then the two other ways to play as cards beneath
    /// it, and the collection pages as a row at the bottom. Settings sit in the corner where the
    /// game screen also keeps its one control.
    ///
    /// Laid out from the bottom edge up, because that is where the thumb is: the actions stack
    /// from the gesture bar, and the title takes whatever height the phone has left above them.
    /// The first version placed everything at fixed offsets around the centre, which on a 20:9
    /// phone left the bottom quarter of the screen empty and the buttons out of easy reach.
    /// </summary>
    public sealed class MainMenuScreen : AppScreen
    {
        public const string GameName = "PRIZMA";
        public const string GameTagline = "BLOK BULMACA";

        const float ShortcutHeight = 216f;
        const float ModeCardHeight = 340f;
        const float PlayHeight = 232f;
        const float BadgeHeight = 136f;

        TextMeshProUGUI _best;
        TextMeshProUGUI _title;
        RectTransform _titleGroup;
        UiButton _play;
        TextMeshProUGUI _levelNote;
        TextMeshProUGUI _dailyNote;
        Image _dailyIcon;

        protected override void Build()
        {
            float half = App.PageHeight * 0.5f;
            float cursor = -half + Design.Space3;

            // Collection pages: each a real button with its name inside it, not an icon with a
            // caption printed on the ground beneath — that caption was the smallest text in the game.
            float shortcutWidth = (Design.ContentWidth - Design.Space3 * 2f) / 3f;
            float shortcutY = cursor + ShortcutHeight * 0.5f;
            BuildShortcut("Scores", Icons.List, Str.Scores, -(shortcutWidth + Design.Space3), shortcutY, shortcutWidth,
                () => { Audio.PlayClick(); App.OpenScores(); });
            var stats = BuildShortcut("Stats", Icons.Chart, Str.Stats, 0f, shortcutY, shortcutWidth,
                () => { Audio.PlayClick(); App.OpenStats(); });
            BuildShortcut("Themes", Icons.Palette, Str.Themes, shortcutWidth + Design.Space3, shortcutY, shortcutWidth,
                () => { Audio.PlayClick(); App.OpenThemes(); });

            // A quiet mint dot while an achievement tier has been reached and not yet looked at.
            // Steady, not pulsing: it waits for the player rather than calling them.
            _achievementDot = UiBuilder.Image(stats.Content, "NewDot", Art.Disc, Design.Mint);
            _achievementDot.type = Image.Type.Simple;
            _achievementDot.rectTransform.sizeDelta = new Vector2(Design.Space4 * 0.8f, Design.Space4 * 0.8f);
            _achievementDot.rectTransform.anchoredPosition = new Vector2(shortcutWidth * 0.5f - Design.Space4, ShortcutHeight * 0.5f - Design.Space4);
            cursor += ShortcutHeight + Design.Space4;

            float cardWidth = (Design.ContentWidth - Design.Space4) * 0.5f;
            float cardX = (cardWidth + Design.Space4) * 0.5f;
            float cardY = cursor + ModeCardHeight * 0.5f;
            _levelNote = BuildModeCard("Adventure", Icons.Flag, Design.TextPrimary, Str.Adventure, -cardX, cardY, cardWidth, out _,
                () => { Audio.PlayClick(); App.ShowLevelSelect(); });
            // The daily card opens the daily's own card: the streak, today's puzzle, the badges.
            _dailyNote = BuildModeCard("Daily", Icons.Calendar, Design.Mint, Str.Daily, cardX, cardY, cardWidth, out _dailyIcon,
                () => { Audio.PlayClick(); App.OpenDaily(); });
            _badgeDot = UiBuilder.Image(_dailyIcon.rectTransform.parent as RectTransform, "NewDot", Art.Disc, Design.Mint);
            _badgeDot.type = Image.Type.Simple;
            _badgeDot.rectTransform.sizeDelta = new Vector2(Design.Space4 * 0.8f, Design.Space4 * 0.8f);
            _badgeDot.rectTransform.anchoredPosition = new Vector2(cardWidth * 0.5f - Design.Space4, ModeCardHeight * 0.5f - Design.Space4);
            cursor += ModeCardHeight + Design.Space4;

            // The main action spans the page, low on the screen, inside comfortable thumb reach.
            _play = UiBuilder.Button(Root, "Play", new Vector2(Design.ContentWidth, PlayHeight), UiButton.Style.Primary,
                Str.Play, Design.Title);
            _play.Rect.anchoredPosition = new Vector2(0f, cursor + PlayHeight * 0.5f);
            _play.Clicked += () => { Audio.PlayClick(); App.PlayClassic(); };
            UiBuilder.TextShadow(_play.Label, 0.3f, -0.35f, 0.3f);
            cursor += PlayHeight;

            var settings = UiBuilder.Button(Root, "Settings", new Vector2(Design.IconButton, Design.IconButton), UiButton.Style.Icon,
                null, Design.Body, Icons.Gear);
            settings.Rect.anchorMin = settings.Rect.anchorMax = new Vector2(1f, 1f);
            settings.Rect.pivot = new Vector2(1f, 1f);
            settings.Rect.anchoredPosition = new Vector2(-Design.Gutter, -Design.Space2);
            settings.Clicked += () => { Audio.PlayClick(); App.OpenSettings(); };

            // Title, tagline and best score as one block, centred in what is left between the
            // settings button and the play button.
            const float titleBox = 210f;
            const float taglineBox = 66f;
            float block = titleBox + Design.Space2 + taglineBox + Design.Space5 + BadgeHeight;
            float top = half - Design.Space2 - Design.IconButton;
            float blockTop = (top + cursor) * 0.5f + block * 0.5f;

            _titleGroup = UiBuilder.Node(Root, "TitleGroup");
            _titleGroup.sizeDelta = new Vector2(1080f, titleBox + Design.Space2 + taglineBox);
            _titleGroup.anchoredPosition = new Vector2(0f, blockTop - _titleGroup.sizeDelta.y * 0.5f);

            var title = UiBuilder.Label(_titleGroup, "Title", GameName, Design.Display, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _title = title;
            title.rectTransform.sizeDelta = new Vector2(1000f, titleBox);
            title.rectTransform.anchoredPosition = new Vector2(0f, _titleGroup.sizeDelta.y * 0.5f - titleBox * 0.5f);
            UiBuilder.TextShadow(title, 0.4f, -0.3f, 0.45f);

            var tagline = UiBuilder.Label(_titleGroup, "Tagline", Str.Tagline, Design.Body, Design.TextOnGround,
                Design.FontDisplay, tracking: Design.TrackingLabel * 2f);
            tagline.rectTransform.sizeDelta = new Vector2(1000f, taglineBox);
            tagline.rectTransform.anchoredPosition = new Vector2(0f, -_titleGroup.sizeDelta.y * 0.5f + taglineBox * 0.5f);

            BuildBestBadge(blockTop - block + BadgeHeight * 0.5f);
        }

        void BuildBestBadge(float y)
        {
            var size = new Vector2(BadgeWidth, BadgeHeight);
            var badge = UiBuilder.Panel(Root, "BestBadge", size, Design.SurfaceInset, BadgeHeight * 0.5f);
            badge.rectTransform.anchoredPosition = new Vector2(0f, y);
            _badge = badge.rectTransform;
            UiBuilder.Hairline(badge.rectTransform, "Hairline", size, BadgeHeight * 0.5f);

            _badgeGem = UiBuilder.Image(badge.rectTransform, "Gem", Icons.Gem, Design.Gold);
            _badgeGem.type = Image.Type.Simple;
            _badgeGem.rectTransform.sizeDelta = new Vector2(BadgeGem, BadgeGem);

            _best = UiBuilder.Label(badge.rectTransform, "Best", "0", Design.Headline, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Center);
            _best.rectTransform.sizeDelta = new Vector2(BadgeWidth, 100f);
        }

        const float BadgeWidth = 440f;
        const float BadgeGem = 80f;

        RectTransform _badge;
        Image _badgeGem;

        /// <summary>
        /// Gem and number centred as one group. With the gem pinned to the left, a best score of
        /// "0" sat alone at one end of a wide empty pill.
        /// </summary>
        void LayoutBestBadge()
        {
            float text = _best.GetPreferredValues(_best.text).x;
            float group = BadgeGem + Design.Space2 + text;
            float width = Mathf.Max(BadgeWidth * 0.62f, group + BadgeHeight);

            _badge.sizeDelta = new Vector2(width, BadgeHeight);
            foreach (RectTransform child in _badge)
                if (child.name == "Hairline") child.sizeDelta = _badge.sizeDelta;

            _badgeGem.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + BadgeGem * 0.5f, 0f);
            _best.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + BadgeGem + Design.Space2 + text * 0.5f, 3f);
        }

        /// <summary>A tall secondary button: an icon, the mode's name, and one line of status under it.</summary>
        TextMeshProUGUI BuildModeCard(string name, Sprite icon, Color iconColor, string label, float x, float y, float width,
            out Image iconImage, System.Action onClick)
        {
            var size = new Vector2(width, ModeCardHeight);
            var card = UiBuilder.Button(Root, name, size, UiButton.Style.Secondary, null, Design.Body);
            card.Rect.anchoredPosition = new Vector2(x, y);
            card.Clicked += onClick;

            iconImage = UiBuilder.Image(card.Content, "Icon", icon, iconColor);
            iconImage.type = Image.Type.Simple;
            iconImage.rectTransform.sizeDelta = new Vector2(Design.IconMd, Design.IconMd);
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 86f);

            var title = UiBuilder.Label(card.Content, "Label", label, Design.Headline, Design.TextPrimary, Design.FontDisplay);
            title.rectTransform.sizeDelta = new Vector2(size.x, 90f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -18f);

            var note = UiBuilder.Label(card.Content, "Note", "", Design.Caption, Design.TextSecondary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            note.rectTransform.sizeDelta = new Vector2(size.x - Design.Space3, 60f);
            note.rectTransform.anchoredPosition = new Vector2(0f, -98f);
            return note;
        }

        Image _achievementDot;
        Image _badgeDot;

        UiButton BuildShortcut(string name, Sprite icon, string caption, float x, float y, float width, System.Action onClick)
        {
            var size = new Vector2(width, ShortcutHeight);
            var button = UiBuilder.Button(Root, name, size, UiButton.Style.Secondary, null, Design.Body);
            button.Rect.anchoredPosition = new Vector2(x, y);
            button.Clicked += onClick;

            var glyph = UiBuilder.Image(button.Content, "Icon", icon, Design.TextPrimary);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(Design.IconMd * 0.8f, Design.IconMd * 0.8f);
            glyph.rectTransform.anchoredPosition = new Vector2(0f, 36f);

            var label = UiBuilder.Label(button.Content, "Caption", caption, Design.Caption, Design.TextPrimary,
                Design.FontDisplay);
            label.rectTransform.sizeDelta = new Vector2(width - Design.Space2, 60f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            return button;
        }

        protected override void OnShow()
        {
            _best.text = HighScores.Best.ToString();
            LayoutBestBadge();

            // A run left mid-way is waiting: the main button says so.
            _play.Label.text = RunStore.Has(GameMode.Classic) ? Str.Continue : Str.Play;
            _achievementDot.gameObject.SetActive(Progress.HasUnseenAchievements);

            _levelNote.text = Str.LevelN(Progress.UnlockedLevel);

            // The furthest light ever reached: the title lights a letter per stage, and the room
            // is lit in that stage's colour. Coming home after a good run, the menu remembers it.
            int best = Progress.BestSpectrum;
            _title.text = Spectrum.TitleMarkup(GameName, best);
            Backdrop.Current?.SetLight(Spectrum.ColorFor(best, Time.unscaledTime), turning: best >= Spectrum.MaxStage);
            Backdrop.Current?.SetHeat(0f);

            // A live streak shows as the flame; the note says whether today still needs doing.
            int streak = Progress.DailyStreak;
            _dailyIcon.sprite = streak > 0 ? Icons.Flame : Icons.Calendar;
            _dailyIcon.color = streak > 0 ? Design.Gold : Design.Mint;
            if (Progress.SolvedDailyToday)
            {
                _dailyNote.text = Str.DayStreak(streak);
                _dailyNote.color = Design.Gold;
            }
            else
            {
                _dailyNote.text = streak > 0 ? Str.StreakToday(streak) : Str.NewPuzzle;
                _dailyNote.color = Design.TextSecondary;
            }

            _badgeDot.gameObject.SetActive(Progress.HasUnseenBadges);

            StartCoroutine(BreatheTitle());
        }

        /// <summary>A very slow drift so the title never sits completely still.</summary>
        IEnumerator BreatheTitle()
        {
            var home = _titleGroup.anchoredPosition;

            while (true)
            {
                float k = Mathf.Sin(Time.unscaledTime * 0.9f);
                _titleGroup.anchoredPosition = home + new Vector2(0f, k * 7f);
                yield return null;
            }
        }
    }
}
