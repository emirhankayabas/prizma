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
    /// it, and the collection pages as a quiet row at the bottom. Settings sit in the corner where
    /// the game screen also keeps its one control.
    /// </summary>
    public sealed class MainMenuScreen : AppScreen
    {
        public const string GameName = "PRIZMA";
        public const string GameTagline = "BLOK BULMACA";

        TextMeshProUGUI _best;
        RectTransform _titleGroup;
        UiButton _play;
        TextMeshProUGUI _levelNote;
        TextMeshProUGUI _dailyNote;
        Image _dailyIcon;

        protected override void Build()
        {
            _titleGroup = UiBuilder.Node(Root, "TitleGroup");
            _titleGroup.sizeDelta = new Vector2(1080f, 400f);
            _titleGroup.anchoredPosition = new Vector2(0f, 540f);

            var title = UiBuilder.Label(_titleGroup, "Title", GameName, Design.Display, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            title.rectTransform.anchoredPosition = new Vector2(0f, Design.Space4);
            UiBuilder.TextShadow(title, 0.4f, -0.3f, 0.45f);

            var tagline = UiBuilder.Label(_titleGroup, "Tagline", GameTagline, Design.Label, Design.TextOnGround,
                Design.FontMedium, tracking: Design.TrackingLabel);
            tagline.rectTransform.anchoredPosition = new Vector2(0f, -Design.Space5);

            BuildBestBadge();

            // The main action sits low on the screen, inside comfortable thumb reach on a phone.
            _play = UiBuilder.Button(Root, "Play", new Vector2(660f, 180f), UiButton.Style.Primary,
                "OYNA", Design.Title);
            _play.Rect.anchoredPosition = new Vector2(0f, 40f);
            _play.Clicked += () => { Audio.PlayClick(); App.PlayClassic(); };

            _levelNote = BuildModeCard("Adventure", Icons.Flag, Design.TextPrimary, "MACERA", -170f, out _,
                () => { Audio.PlayClick(); App.ShowLevelSelect(); });
            _dailyNote = BuildModeCard("Daily", Icons.Calendar, Design.Mint, "GÜNLÜK", 170f, out _dailyIcon,
                () => { Audio.PlayClick(); App.PlayDaily(); });

            BuildShortcut("Scores", Icons.List, "SKORLAR", -220f, () => { Audio.PlayClick(); App.OpenScores(); });
            BuildShortcut("Stats", Icons.Chart, "İSTATİSTİK", 0f, () => { Audio.PlayClick(); App.OpenStats(); });
            BuildShortcut("Themes", Icons.Palette, "TEMALAR", 220f, () => { Audio.PlayClick(); App.OpenThemes(); });

            var settings = UiBuilder.Button(Root, "Settings", new Vector2(112f, 112f), UiButton.Style.Icon,
                null, Design.Body, Icons.Gear);
            settings.Rect.anchorMin = settings.Rect.anchorMax = new Vector2(1f, 1f);
            settings.Rect.pivot = new Vector2(1f, 1f);
            settings.Rect.anchoredPosition = new Vector2(-Design.Gutter, -Design.Space6);
            settings.Clicked += () => { Audio.PlayClick(); App.OpenSettings(); };
        }

        void BuildBestBadge()
        {
            var size = new Vector2(420f, 104f);
            var badge = UiBuilder.Panel(Root, "BestBadge", size, Design.SurfaceInset, 52f);
            badge.rectTransform.anchoredPosition = new Vector2(0f, 290f);
            UiBuilder.Hairline(badge.rectTransform, "Hairline", size, 52f);

            var gem = UiBuilder.Image(badge.rectTransform, "Gem", Icons.Gem, Design.Gold);
            gem.type = Image.Type.Simple;
            gem.rectTransform.sizeDelta = new Vector2(52f, 52f);
            gem.rectTransform.anchoredPosition = new Vector2(-128f, 0f);

            _best = UiBuilder.Label(badge.rectTransform, "Best", "0", Design.Headline, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Left);
            _best.rectTransform.sizeDelta = new Vector2(240f, 72f);
            _best.rectTransform.anchoredPosition = new Vector2(46f, 0f);
        }

        /// <summary>A tall secondary button: an icon, the mode's name, and one line of status under it.</summary>
        TextMeshProUGUI BuildModeCard(string name, Sprite icon, Color iconColor, string label, float x,
            out Image iconImage, System.Action onClick)
        {
            var size = new Vector2(316f, 236f);
            var card = UiBuilder.Button(Root, name, size, UiButton.Style.Secondary, null, Design.Body);
            card.Rect.anchoredPosition = new Vector2(x, -230f);
            card.Clicked += onClick;

            iconImage = UiBuilder.Image(card.Content, "Icon", icon, iconColor);
            iconImage.type = Image.Type.Simple;
            iconImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 58f);

            var title = UiBuilder.Label(card.Content, "Label", label, Design.Headline, Design.TextPrimary, Design.FontDisplay);
            title.rectTransform.sizeDelta = new Vector2(size.x, 64f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            var note = UiBuilder.Label(card.Content, "Note", "", Design.Caption, Design.TextSecondary,
                Design.FontMedium, tracking: Design.TrackingLabel * 0.5f);
            note.rectTransform.sizeDelta = new Vector2(size.x, 44f);
            note.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            return note;
        }

        void BuildShortcut(string name, Sprite icon, string caption, float x, System.Action onClick)
        {
            var button = UiBuilder.Button(Root, name, new Vector2(128f, 128f), UiButton.Style.Icon, null, Design.Body, icon);
            button.Rect.anchoredPosition = new Vector2(x, -520f);
            button.Clicked += onClick;

            var label = UiBuilder.Label(Root, name + "Caption", caption, Design.Caption, Design.TextOnGround,
                Design.FontMedium, tracking: Design.TrackingLabel * 0.5f);
            label.rectTransform.sizeDelta = new Vector2(220f, 44f);
            label.rectTransform.anchoredPosition = new Vector2(x, -618f);
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

        UiSlider _music;
        UiSlider _sfx;
        UiToggle _haptics;
        UiToggle _mute;
        UiToggle _colorBlind;
        Image _muteIcon;

        readonly List<Graphic> _audioRowGraphics = new List<Graphic>();
        readonly List<float> _audioRowAlphas = new List<float>();

        protected override void Build()
        {
            ModalCard.Build(Root, "Ayarlar", new Vector2(900f, 1120f), out var content);

            // Master mute first: it is the control someone reaches for in a hurry.
            _muteIcon = BuildLabelRow(content, "Mute", "SESİ KAPAT", Icons.SpeakerMuted, 360f, track: false);
            _mute = UiToggle.Create(content, "MuteToggle", 156f, 88f, GameSettings.Muted);
            _mute.Rect.anchoredPosition = new Vector2(280f, 360f);
            _mute.ValueChanged += v =>
            {
                GameSettings.Muted = v;
                ApplyMuteState();
                Audio.PlayClick(); // silent when muting, audible when unmuting
            };

            _music = BuildSliderRow(content, "Music", "MÜZİK", Icons.Speaker, 164f, GameSettings.MusicVolume);
            _music.ValueChanged += v => GameSettings.MusicVolume = v;

            _sfx = BuildSliderRow(content, "Sfx", "EFEKTLER", Icons.Speaker, -6f, GameSettings.SfxVolume);
            _sfx.ValueChanged += v =>
            {
                GameSettings.SfxVolume = v;
                Audio.PlayClick(); // immediate feedback at the new level
            };

            BuildLabelRow(content, "Haptic", "TİTREŞİM", Icons.Vibrate, -130f, track: false);
            _haptics = UiToggle.Create(content, "HapticToggle", 156f, 88f, GameSettings.Haptics);
            _haptics.Rect.anchoredPosition = new Vector2(280f, -130f);
            _haptics.ValueChanged += v => { GameSettings.Haptics = v; Audio.PlayClick(); };

            // Colour-blind mode marks every block colour with its own small shape.
            BuildLabelRow(content, "ColorBlind", "RENK KÖRÜ MODU", Icons.Palette, -250f, track: false);
            _colorBlind = UiToggle.Create(content, "ColorBlindToggle", 156f, 88f, Progress.ColorBlind);
            _colorBlind.Rect.anchoredPosition = new Vector2(280f, -250f);
            _colorBlind.ValueChanged += v => { Progress.ColorBlind = v; Audio.PlayClick(); };

            var close = UiBuilder.Button(content, "Close", new Vector2(420f, 140f), UiButton.Style.Primary,
                "TAMAM", Design.Headline);
            close.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            ModalCard.StackFromBottom(content, Design.Space3, close.Rect);
        }

        /// <summary>Builds an icon plus label, and returns the icon so callers can swap it later.</summary>
        Image BuildLabelRow(RectTransform parent, string name, string label, Sprite icon, float y, bool track = true)
        {
            var glyph = UiBuilder.Image(parent, name + "Icon", icon, Design.TextTertiary);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(46f, 46f);
            glyph.rectTransform.anchoredPosition = new Vector2(-330f, y);

            var text = UiBuilder.Label(parent, name + "Label", label, Design.Label, Design.TextSecondary,
                Design.FontMedium, TextAlignmentOptions.Left, Design.TrackingLabel);
            text.rectTransform.sizeDelta = new Vector2(440f, 56f);
            text.rectTransform.anchoredPosition = new Vector2(-50f, y);

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

        UiSlider BuildSliderRow(RectTransform parent, string name, string label, Sprite icon, float y, float value)
        {
            BuildLabelRow(parent, name, label, icon, y + 76f);

            var slider = UiSlider.Create(parent, name + "Slider", 700f, 22f, value);
            slider.Rect.anchoredPosition = new Vector2(0f, y);
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
            ModalCard.Build(Root, "Duraklatıldı", new Vector2(880f, 860f), out var content);

            var resume = UiBuilder.Button(content, "Resume", new Vector2(600f, 156f), UiButton.Style.Primary,
                "DEVAM ET", Design.Headline);
            resume.Clicked += () => { Audio.PlayClick(); App.CloseModal(); };

            var restart = UiBuilder.Button(content, "Restart", new Vector2(600f, 136f), UiButton.Style.Secondary,
                "YENİDEN BAŞLA", Design.Body);
            restart.Clicked += () => { Audio.PlayClick(); App.RestartRun(); };

            var settings = UiBuilder.Button(content, "Settings", new Vector2(600f, 136f), UiButton.Style.Secondary,
                "AYARLAR", Design.Body);
            settings.Clicked += () => { Audio.PlayClick(); App.OpenSettings(); };

            var quit = UiBuilder.Button(content, "Quit", new Vector2(600f, 136f), UiButton.Style.Secondary,
                "ANA SAYFAYA DÖN", Design.Body);
            quit.Clicked += () => { Audio.PlayClick(); App.LeaveRun(); };

            ModalCard.StackFromBottom(content, Design.Space3, resume.Rect, restart.Rect, settings.Rect, quit.Rect);
        }
    }

    /// <summary>The local top ten.</summary>
    public sealed class ScoresScreen : AppScreen
    {
        public override bool IsModal => true;

        const float RowHeight = 88f;

        RectTransform _list;
        readonly List<GameObject> _rows = new List<GameObject>();
        TextMeshProUGUI _empty;

        protected override void Build()
        {
            ModalCard.Build(Root, "En İyi Skorlar", new Vector2(900f, 1240f), out var content);

            _list = UiBuilder.Node(content, "List");
            _list.sizeDelta = new Vector2(780f, RowHeight * HighScores.Capacity);
            _list.anchoredPosition = new Vector2(0f, 20f);

            _empty = UiBuilder.Label(content, "Empty", "Henüz skor yok.\nİlk oyununu oyna.",
                Design.Body, Design.TextTertiary, Design.FontBody);
            _empty.rectTransform.sizeDelta = new Vector2(700f, 200f);
            _empty.rectTransform.anchoredPosition = new Vector2(0f, 40f);

            var close = UiBuilder.Button(content, "Close", new Vector2(420f, 140f), UiButton.Style.Primary,
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

            float top = _list.sizeDelta.y * 0.5f - RowHeight * 0.5f;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                var row = UiBuilder.Node(_list, $"Row_{i}");
                row.sizeDelta = new Vector2(_list.sizeDelta.x, RowHeight);
                row.anchoredPosition = new Vector2(0f, top - i * RowHeight);
                _rows.Add(row.gameObject);

                // Only the leader gets the accent; a rainbow of highlights would flatten the ranking.
                bool leader = i == 0;

                if (leader)
                {
                    UiBuilder.Panel(row, "Highlight", new Vector2(_list.sizeDelta.x, RowHeight - 10f),
                        Design.SurfaceLeader, Design.RadiusSm);
                    UiBuilder.Hairline(row, "HighlightEdge", new Vector2(_list.sizeDelta.x, RowHeight - 10f),
                        Design.RadiusSm, Design.Gold.WithAlpha(0.22f));
                }

                var rank = UiBuilder.Label(row, "Rank", (i + 1).ToString(), Design.Label,
                    leader ? Design.Gold : Design.TextTertiary, Design.FontMedium);
                rank.rectTransform.sizeDelta = new Vector2(80f, RowHeight);
                rank.rectTransform.anchoredPosition = new Vector2(-330f, 0f);

                var score = UiBuilder.Label(row, "Score", entry.Score.ToString(), Design.Headline,
                    leader ? Design.Gold : Design.TextPrimary, Design.FontDisplay, TextAlignmentOptions.Left);
                score.rectTransform.sizeDelta = new Vector2(320f, RowHeight);
                score.rectTransform.anchoredPosition = new Vector2(-70f, 0f);

                var when = entry.When;
                string date = when == System.DateTime.MinValue ? "" : when.ToString("dd.MM.yyyy");
                var dateLabel = UiBuilder.Label(row, "Date", date, Design.Caption, Design.TextTertiary,
                    Design.FontBody, TextAlignmentOptions.Right);
                dateLabel.rectTransform.sizeDelta = new Vector2(280f, RowHeight);
                dateLabel.rectTransform.anchoredPosition = new Vector2(240f, 0f);
            }
        }
    }

    /// <summary>
    /// The shared modal chrome: a scrim, an elevated card with a hairline, and a heading.
    /// Having one builder for it is what keeps the modals looking like the same product.
    /// </summary>
    static class ModalCard
    {
        public static RectTransform Build(RectTransform root, string heading, Vector2 size, out RectTransform content)
        {
            var scrim = UiBuilder.Image(root, "Scrim", Art.Panel(0f), Design.Scrim);
            scrim.type = Image.Type.Simple;
            UiBuilder.Stretch(scrim.rectTransform);

            var holder = UiBuilder.Node(root, "Card");
            holder.sizeDelta = size;

            UiBuilder.Shadow(holder, "Shadow", size, Design.RadiusLg, Design.E3);
            UiBuilder.Panel(holder, "Fill", size, Design.SurfaceHigh, Design.RadiusLg);
            UiBuilder.Hairline(holder, "Hairline", size, Design.RadiusLg);

            var title = UiBuilder.Label(holder, "Heading", heading, Design.Title, Design.TextPrimary, Design.FontDisplay);
            title.rectTransform.anchoredPosition = new Vector2(0f, size.y * 0.5f - Design.Space6);

            content = holder;
            return holder;
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
