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

        const float ShortcutHeight = 184f;
        const float ModeCardHeight = 300f;
        const float PlayHeight = 200f;
        const float BadgeHeight = 120f;

        TextMeshProUGUI _best;
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
            BuildShortcut("Scores", Icons.List, "SKORLAR", -(shortcutWidth + Design.Space3), shortcutY, shortcutWidth,
                () => { Audio.PlayClick(); App.OpenScores(); });
            BuildShortcut("Stats", Icons.Chart, "İSTATİSTİK", 0f, shortcutY, shortcutWidth,
                () => { Audio.PlayClick(); App.OpenStats(); });
            BuildShortcut("Themes", Icons.Palette, "TEMALAR", shortcutWidth + Design.Space3, shortcutY, shortcutWidth,
                () => { Audio.PlayClick(); App.OpenThemes(); });
            cursor += ShortcutHeight + Design.Space4;

            float cardWidth = (Design.ContentWidth - Design.Space4) * 0.5f;
            float cardX = (cardWidth + Design.Space4) * 0.5f;
            float cardY = cursor + ModeCardHeight * 0.5f;
            _levelNote = BuildModeCard("Adventure", Icons.Flag, Design.TextPrimary, "MACERA", -cardX, cardY, cardWidth, out _,
                () => { Audio.PlayClick(); App.ShowLevelSelect(); });
            _dailyNote = BuildModeCard("Daily", Icons.Calendar, Design.Mint, "GÜNLÜK", cardX, cardY, cardWidth, out _dailyIcon,
                () => { Audio.PlayClick(); App.PlayDaily(); });
            cursor += ModeCardHeight + Design.Space4;

            // The main action spans the page, low on the screen, inside comfortable thumb reach.
            _play = UiBuilder.Button(Root, "Play", new Vector2(Design.ContentWidth, PlayHeight), UiButton.Style.Primary,
                "OYNA", Design.Title);
            _play.Rect.anchoredPosition = new Vector2(0f, cursor + PlayHeight * 0.5f);
            _play.Clicked += () => { Audio.PlayClick(); App.PlayClassic(); };
            cursor += PlayHeight;

            var settings = UiBuilder.Button(Root, "Settings", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon,
                null, Design.Body, Icons.Gear);
            settings.Rect.anchorMin = settings.Rect.anchorMax = new Vector2(1f, 1f);
            settings.Rect.pivot = new Vector2(1f, 1f);
            settings.Rect.anchoredPosition = new Vector2(-Design.Gutter, -Design.Space2);
            settings.Clicked += () => { Audio.PlayClick(); App.OpenSettings(); };

            // Title, tagline and best score as one block, centred in what is left between the
            // settings button and the play button.
            const float titleBox = 180f;
            const float taglineBox = 60f;
            float block = titleBox + Design.Space2 + taglineBox + Design.Space5 + BadgeHeight;
            float top = half - Design.Space2 - Design.TouchTarget;
            float blockTop = (top + cursor) * 0.5f + block * 0.5f;

            _titleGroup = UiBuilder.Node(Root, "TitleGroup");
            _titleGroup.sizeDelta = new Vector2(1080f, titleBox + Design.Space2 + taglineBox);
            _titleGroup.anchoredPosition = new Vector2(0f, blockTop - _titleGroup.sizeDelta.y * 0.5f);

            var title = UiBuilder.Label(_titleGroup, "Title", GameName, Design.Display, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            title.rectTransform.sizeDelta = new Vector2(1000f, titleBox);
            title.rectTransform.anchoredPosition = new Vector2(0f, _titleGroup.sizeDelta.y * 0.5f - titleBox * 0.5f);
            UiBuilder.TextShadow(title, 0.4f, -0.3f, 0.45f);

            var tagline = UiBuilder.Label(_titleGroup, "Tagline", GameTagline, Design.Label, Design.TextOnGround,
                Design.FontMedium, tracking: Design.TrackingLabel);
            tagline.rectTransform.sizeDelta = new Vector2(1000f, taglineBox);
            tagline.rectTransform.anchoredPosition = new Vector2(0f, -_titleGroup.sizeDelta.y * 0.5f + taglineBox * 0.5f);

            BuildBestBadge(blockTop - block + BadgeHeight * 0.5f);
        }

        void BuildBestBadge(float y)
        {
            var size = new Vector2(500f, BadgeHeight);
            var badge = UiBuilder.Panel(Root, "BestBadge", size, Design.SurfaceInset, BadgeHeight * 0.5f);
            badge.rectTransform.anchoredPosition = new Vector2(0f, y);
            UiBuilder.Hairline(badge.rectTransform, "Hairline", size, BadgeHeight * 0.5f);

            var gem = UiBuilder.Image(badge.rectTransform, "Gem", Icons.Gem, Design.Gold);
            gem.type = Image.Type.Simple;
            gem.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            gem.rectTransform.anchoredPosition = new Vector2(-150f, 0f);

            _best = UiBuilder.Label(badge.rectTransform, "Best", "0", Design.Headline, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Left);
            _best.rectTransform.sizeDelta = new Vector2(300f, 90f);
            _best.rectTransform.anchoredPosition = new Vector2(50f, 2f);
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
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 76f);

            var title = UiBuilder.Label(card.Content, "Label", label, Design.Headline, Design.TextPrimary, Design.FontDisplay);
            title.rectTransform.sizeDelta = new Vector2(size.x, 80f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -14f);

            var note = UiBuilder.Label(card.Content, "Note", "", Design.Caption, Design.TextSecondary,
                Design.FontMedium, tracking: Design.TrackingLabel * 0.5f);
            note.rectTransform.sizeDelta = new Vector2(size.x - Design.Space3, 56f);
            note.rectTransform.anchoredPosition = new Vector2(0f, -86f);
            return note;
        }

        void BuildShortcut(string name, Sprite icon, string caption, float x, float y, float width, System.Action onClick)
        {
            var size = new Vector2(width, ShortcutHeight);
            var button = UiBuilder.Button(Root, name, size, UiButton.Style.Secondary, null, Design.Body);
            button.Rect.anchoredPosition = new Vector2(x, y);
            button.Clicked += onClick;

            var glyph = UiBuilder.Image(button.Content, "Icon", icon, Design.TextPrimary);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(68f, 68f);
            glyph.rectTransform.anchoredPosition = new Vector2(0f, 30f);

            var label = UiBuilder.Label(button.Content, "Caption", caption, Design.Caption, Design.TextSecondary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.3f);
            label.rectTransform.sizeDelta = new Vector2(width - Design.Space2, 56f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        }

        protected override void OnShow()
        {
            _best.text = HighScores.Best.ToString();

            // A run left mid-way is waiting: the main button says so.
            _play.Label.text = RunStore.Has(GameMode.Classic) ? "DEVAM ET" : "OYNA";

            _levelNote.text = $"BÖLÜM {Progress.UnlockedLevel}";

            int streak = Progress.DailyStreak;
            if (Progress.PlayedDailyToday)
            {
                _dailyNote.text = $"{streak} GÜN SERİ";
                _dailyNote.color = Design.Gold;
            }
            else
            {
                _dailyNote.text = streak > 0 ? $"SERİ {streak} · BUGÜN?" : "YENİ BULMACA";
                _dailyNote.color = Design.TextSecondary;
            }

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

    /// <summary>Audio, haptics and accessibility, opened over whatever page is showing.</summary>
    public sealed class SettingsScreen : AppScreen
    {
        public override bool IsModal => true;

        const float ToggleRow = 150f;
        const float SliderRow = 216f;
        const float ToggleWidth = 184f;
        const float ToggleHeight = 104f;

        UiSlider _music;
        UiSlider _sfx;
        UiToggle _haptics;
        UiToggle _mute;
        UiToggle _colorBlind;
        Image _muteIcon;

        readonly List<Graphic> _audioRowGraphics = new List<Graphic>();
        readonly List<float> _audioRowAlphas = new List<float>();

        float _inner;

        protected override void Build()
        {
            float body = ToggleRow * 3f + SliderRow * 2f;
            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(body, Design.ButtonLg));
            ModalCard.Build(Root, "Ayarlar", size, out var content);
            _inner = ModalCard.InnerWidth(size);

            float y = ModalCard.ContentTop(size);

            // Master mute first: it is the control someone reaches for in a hurry.
            _muteIcon = BuildToggleRow(content, "Mute", "SESİ KAPAT", Icons.SpeakerMuted, ref y, GameSettings.Muted, out _mute);
            _mute.ValueChanged += v =>
            {
                GameSettings.Muted = v;
                ApplyMuteState();
                Audio.PlayClick(); // silent when muting, audible when unmuting
            };

            _music = BuildSliderRow(content, "Music", "MÜZİK", Icons.Speaker, ref y, GameSettings.MusicVolume);
            _music.ValueChanged += v => GameSettings.MusicVolume = v;

            _sfx = BuildSliderRow(content, "Sfx", "EFEKTLER", Icons.Speaker, ref y, GameSettings.SfxVolume);
            _sfx.ValueChanged += v =>
            {
                GameSettings.SfxVolume = v;
                Audio.PlayClick(); // immediate feedback at the new level
            };

            BuildToggleRow(content, "Haptic", "TİTREŞİM", Icons.Vibrate, ref y, GameSettings.Haptics, out _haptics);
            _haptics.ValueChanged += v => { GameSettings.Haptics = v; Audio.PlayClick(); };

            // Colour-blind mode marks every block colour with its own small shape.
            BuildToggleRow(content, "ColorBlind", "RENK KÖRÜ MODU", Icons.Palette, ref y, Progress.ColorBlind, out _colorBlind);
            _colorBlind.ValueChanged += v => { Progress.ColorBlind = v; Audio.PlayClick(); };

            var close = UiBuilder.Button(content, "Close", new Vector2(_inner, Design.ButtonLg), UiButton.Style.Primary,
                "TAMAM", Design.Headline);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            ModalCard.StackFromBottom(content, Design.Space3, close.Rect);
        }

        Image BuildToggleRow(RectTransform parent, string name, string label, Sprite icon, ref float y, bool value, out UiToggle toggle)
        {
            float center = y - ToggleRow * 0.5f;
            var glyph = BuildLabelRow(parent, name, label, icon, center, track: false);

            toggle = UiToggle.Create(parent, name + "Toggle", ToggleWidth, ToggleHeight, value);
            toggle.Rect.anchoredPosition = new Vector2(_inner * 0.5f - ToggleWidth * 0.5f, center);

            y -= ToggleRow;
            return glyph;
        }

        /// <summary>Builds an icon plus label, and returns the icon so callers can swap it later.</summary>
        Image BuildLabelRow(RectTransform parent, string name, string label, Sprite icon, float y, bool track = true)
        {
            float left = -_inner * 0.5f;

            var glyph = UiBuilder.Image(parent, name + "Icon", icon, Design.TextTertiary);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(Design.IconSm, Design.IconSm);
            glyph.rectTransform.anchoredPosition = new Vector2(left + Design.IconSm * 0.5f, y);

            const float textWidth = 560f;
            var text = UiBuilder.Label(parent, name + "Label", label, Design.Label, Design.TextSecondary,
                Design.FontMedium, TextAlignmentOptions.Left, Design.TrackingLabel);
            text.rectTransform.sizeDelta = new Vector2(textWidth, 70f);
            text.rectTransform.anchoredPosition = new Vector2(left + Design.IconSm + Design.Space3 + textWidth * 0.5f, y);

            // Volume rows dim while muted, so the sliders read as parked rather than broken.
            if (track)
            {
                _audioRowGraphics.Add(glyph);
                _audioRowAlphas.Add(glyph.color.a);
                _audioRowGraphics.Add(text);
                _audioRowAlphas.Add(text.color.a);
            }

            return glyph;
        }

        UiSlider BuildSliderRow(RectTransform parent, string name, string label, Sprite icon, ref float y, float value)
        {
            BuildLabelRow(parent, name, label, icon, y - 56f);

            // The knob overhangs the track by half its size at either end; inset so it stays inside the card.
            var slider = UiSlider.Create(parent, name + "Slider", _inner - 64f, 26f, value);
            slider.Rect.anchoredPosition = new Vector2(0f, y - 152f);

            y -= SliderRow;
            return slider;
        }

        void ApplyMuteState()
        {
            bool muted = GameSettings.Muted;
            _muteIcon.sprite = muted ? Icons.SpeakerMuted : Icons.Speaker;

            float k = muted ? 0.35f : 1f;
            for (int i = 0; i < _audioRowGraphics.Count; i++)
                _audioRowGraphics[i].color = _audioRowGraphics[i].color.WithAlpha(_audioRowAlphas[i] * k);
        }

        protected override void OnShow()
        {
            _music.Value = GameSettings.MusicVolume;
            _sfx.Value = GameSettings.SfxVolume;
            _haptics.IsOn = GameSettings.Haptics;
            _mute.IsOn = GameSettings.Muted;
            _colorBlind.IsOn = Progress.ColorBlind;
            ApplyMuteState();
        }
    }

    /// <summary>
    /// Mid-run menu. Opening it is the only way to reach settings, restart or leave a game, so a
    /// player never has to guess whether tapping something will cost them their run. Leaving
    /// keeps the run: it is saved and waiting on the title page.
    /// </summary>
    public sealed class PauseScreen : AppScreen
    {
        public override bool IsModal => true;

        protected override void Build()
        {
            float stack = Design.ButtonMd * 3f + Design.Space3 * 3f;
            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(0f, Design.ButtonLg + stack, gap: 0f));
            ModalCard.Build(Root, "Duraklatıldı", size, out var content);
            float inner = ModalCard.InnerWidth(size);

            var resume = UiBuilder.Button(content, "Resume", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary,
                "DEVAM ET", Design.Headline);
            resume.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            var restart = UiBuilder.Button(content, "Restart", new Vector2(inner, Design.ButtonMd), UiButton.Style.Secondary,
                "YENİDEN BAŞLA", Design.Body);
            restart.Clicked += () => { Audio.PlayClick(); App.RestartRun(); };

            var settings = UiBuilder.Button(content, "Settings", new Vector2(inner, Design.ButtonMd), UiButton.Style.Secondary,
                "AYARLAR", Design.Body);
            settings.Clicked += () => { Audio.PlayClick(); App.OpenSettings(); };

            var quit = UiBuilder.Button(content, "Quit", new Vector2(inner, Design.ButtonMd), UiButton.Style.Secondary,
                "ANA SAYFAYA DÖN", Design.Body);
            quit.Clicked += () => { Audio.PlayClick(); App.LeaveRun(); };

            ModalCard.StackFromBottom(content, Design.Space3, resume.Rect, restart.Rect, settings.Rect, quit.Rect);
        }
    }

    /// <summary>The local top ten.</summary>
    public sealed class ScoresScreen : AppScreen
    {
        public override bool IsModal => true;

        const float PreferredRow = 108f;

        float _rowHeight;
        RectTransform _list;
        readonly List<GameObject> _rows = new List<GameObject>();
        TextMeshProUGUI _empty;

        protected override void Build()
        {
            _rowHeight = ModalCard.FitRows(App.PageHeight, HighScores.Capacity, PreferredRow, Design.ButtonLg);

            var size = new Vector2(Design.ContentWidth, ModalCard.HeightFor(_rowHeight * HighScores.Capacity, Design.ButtonLg));
            ModalCard.Build(Root, "En İyi Skorlar", size, out var content);

            _list = UiBuilder.Node(content, "List");
            _list.sizeDelta = new Vector2(ModalCard.InnerWidth(size), _rowHeight * HighScores.Capacity);
            _list.anchoredPosition = new Vector2(0f, ModalCard.ContentTop(size) - _list.sizeDelta.y * 0.5f);

            _empty = UiBuilder.Label(content, "Empty", "Henüz skor yok.\nİlk oyununu oyna.",
                Design.Body, Design.TextTertiary, Design.FontBody);
            _empty.rectTransform.sizeDelta = new Vector2(ModalCard.InnerWidth(size), 240f);
            _empty.rectTransform.anchoredPosition = _list.anchoredPosition;

            var close = UiBuilder.Button(content, "Close", new Vector2(ModalCard.InnerWidth(size), Design.ButtonLg), UiButton.Style.Primary,
                "KAPAT", Design.Headline);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            ModalCard.StackFromBottom(content, Design.Space3, close.Rect);
        }

        protected override void OnShow() => Populate();

        void Populate()
        {
            foreach (var row in _rows) Destroy(row);
            _rows.Clear();

            var entries = HighScores.All;
            _empty.gameObject.SetActive(entries.Count == 0);

            float width = _list.sizeDelta.x;
            float left = -width * 0.5f;
            float top = _list.sizeDelta.y * 0.5f - _rowHeight * 0.5f;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                var row = UiBuilder.Node(_list, $"Row_{i}");
                row.sizeDelta = new Vector2(width, _rowHeight);
                row.anchoredPosition = new Vector2(0f, top - i * _rowHeight);
                _rows.Add(row.gameObject);

                // Only the leader gets the accent; a rainbow of highlights would flatten the ranking.
                bool leader = i == 0;

                if (leader)
                {
                    UiBuilder.Panel(row, "Highlight", new Vector2(width, _rowHeight - 10f),
                        Design.SurfaceLeader, Design.RadiusSm);
                    UiBuilder.Hairline(row, "HighlightEdge", new Vector2(width, _rowHeight - 10f),
                        Design.RadiusSm, Design.Gold.WithAlpha(0.22f));
                }

                const float rankWidth = 100f;
                var rank = UiBuilder.Label(row, "Rank", (i + 1).ToString(), Design.Label,
                    leader ? Design.Gold : Design.TextTertiary, Design.FontMedium);
                rank.rectTransform.sizeDelta = new Vector2(rankWidth, _rowHeight);
                rank.rectTransform.anchoredPosition = new Vector2(left + rankWidth * 0.5f, 0f);

                const float scoreWidth = 400f;
                var score = UiBuilder.Label(row, "Score", entry.Score.ToString(), Design.Headline,
                    leader ? Design.Gold : Design.TextPrimary, Design.FontDisplay, TextAlignmentOptions.Left);
                score.rectTransform.sizeDelta = new Vector2(scoreWidth, _rowHeight);
                score.rectTransform.anchoredPosition = new Vector2(left + rankWidth + Design.Space3 + scoreWidth * 0.5f, 0f);

                var when = entry.When;
                string date = when == System.DateTime.MinValue ? "" : when.ToString("dd.MM.yyyy");
                const float dateWidth = 300f;
                var dateLabel = UiBuilder.Label(row, "Date", date, Design.Caption, Design.TextTertiary,
                    Design.FontBody, TextAlignmentOptions.Right);
                dateLabel.rectTransform.sizeDelta = new Vector2(dateWidth, _rowHeight);
                dateLabel.rectTransform.anchoredPosition = new Vector2(width * 0.5f - Design.Space3 - dateWidth * 0.5f, 0f);
            }
        }
    }

    /// <summary>
    /// The shared modal chrome: a scrim, an elevated card with a hairline, and a heading.
    /// Having one builder for it is what keeps the modals looking like the same product.
    ///
    /// Cards span the page between the gutters and take their height from what they hold, so a
    /// modal is sized by its content and not by a number someone picked for one phone.
    /// </summary>
    static class ModalCard
    {
        public static RectTransform Build(RectTransform root, string heading, Vector2 size, out RectTransform content)
        {
            var scrim = UiBuilder.Image(root, "Scrim", Art.Panel(0f), Design.Scrim);
            scrim.type = Image.Type.Simple;
            UiBuilder.StretchFullScreen(scrim.rectTransform);

            var holder = UiBuilder.Node(root, "Card");
            holder.sizeDelta = size;

            UiBuilder.Shadow(holder, "Shadow", size, Design.RadiusLg, Design.E3);
            UiBuilder.Panel(holder, "Fill", size, Design.SurfaceHigh, Design.RadiusLg);
            UiBuilder.Hairline(holder, "Hairline", size, Design.RadiusLg);

            var title = UiBuilder.Label(holder, "Heading", heading, Design.Title, Design.TextPrimary, Design.FontDisplay);
            title.rectTransform.sizeDelta = new Vector2(size.x - Design.CardPadding * 2f, 110f);
            title.rectTransform.anchoredPosition = new Vector2(0f, size.y * 0.5f - Design.CardHeading);

            content = holder;
            return holder;
        }

        /// <summary>Width available inside a card once its padding is taken off both sides.</summary>
        public static float InnerWidth(Vector2 size) => size.x - Design.CardPadding * 2f;

        /// <summary>Card-local y where the content under the heading begins.</summary>
        public static float ContentTop(Vector2 size) => size.y * 0.5f - Design.CardHeader;

        /// <summary>Card height for a body of <paramref name="body"/> units above a footer button.</summary>
        public static float HeightFor(float body, float footer, float gap = Design.Space4) =>
            Design.CardHeader + body + gap + footer + Design.CardPadding;

        /// <summary>
        /// Row height for a list of <paramref name="count"/> rows: the preferred height, or less
        /// when that would make the card taller than the page. A short phone gets tighter rows
        /// instead of a card that runs off the screen.
        /// </summary>
        public static float FitRows(float pageHeight, int count, float preferred, float footer)
        {
            float room = pageHeight - Design.Space4 * 2f - HeightFor(0f, footer);
            return Mathf.Min(preferred, room / Mathf.Max(1, count));
        }

        /// <summary>
        /// Stacks items up from the bottom edge of the card, last one first, so the space under the
        /// bottom item is <see cref="Design.CardPadding"/> instead of whatever the hand-written
        /// offsets happened to leave over. Items are passed in the order they read, top to bottom.
        /// </summary>
        public static void StackFromBottom(RectTransform card, float gap, params RectTransform[] items)
        {
            float edge = -card.sizeDelta.y * 0.5f + Design.CardPadding;

            for (int i = items.Length - 1; i >= 0; i--)
            {
                var rect = items[i];
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, edge + rect.sizeDelta.y * 0.5f);
                edge += rect.sizeDelta.y + gap;
            }
        }
    }
}
