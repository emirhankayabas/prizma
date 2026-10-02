using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The sheet at the end of a run or a level: a heading, an optional row of stars, the big
    /// number, one line of note, and two actions. One sheet for every ending so they all feel like
    /// the same product; only the words and the stars change.
    ///
    /// It rises from the bottom edge like every other modal, and leaves the top of the board in
    /// view above it: the last position is part of how a run ended. It has no close button and
    /// does not swipe away. A run's ending is answered with one of its two actions, or back.
    /// </summary>
    public sealed class ResultCard
    {
        const float TitleRow = 112f;
        const float StarRow = 200f;
        const float ScoreRow = 236f;
        const float NoteRow = 88f;
        const float NoteIconSize = 68f;
        const float NoteIconGap = 16f;

        Sheet _sheet;
        RectTransform _panel;
        TextMeshProUGUI _title;
        TextMeshProUGUI _score;
        TextMeshProUGUI _note;
        Image _noteIcon;
        readonly Image[] _stars = new Image[3];
        RectTransform _starRow;
        UiButton _primary;
        UiButton _secondary;
        Coroutine _motion;

        public event Action PrimaryClicked;
        public event Action SecondaryClicked;

        public bool Visible => _panel != null && _panel.gameObject.activeSelf;

        public void Build(MonoBehaviour host, RectTransform root, float pageHeight)
        {
            _panel = UiBuilder.Node(root, "Result");
            UiBuilder.Stretch(_panel);

            _sheet = Sheet.Build(host, _panel, pageHeight, null, null);
            var body = _sheet.Body;

            // Between the dim and the sheet: the paper falls behind the result, never over its words.
            _confettiRoot = UiBuilder.Child(_panel, "Confetti");
            _confettiRoot.gameObject.AddComponent<Canvas>();
            _confettiRoot.SetSiblingIndex(_sheet.Panel.GetSiblingIndex());

            float titleWidth = Design.ContentWidth - Design.TouchTarget * 2f;
            _title = UiBuilder.Label(body, "Title", "", Design.Title, Design.TextPrimary, Design.FontDisplay);
            _title.rectTransform.sizeDelta = new Vector2(titleWidth, TitleRow);
            _title.enableAutoSizing = true;
            _title.fontSizeMin = Design.Headline;
            _title.fontSizeMax = Design.Title;
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            SheetKit.PinTop(_title.rectTransform, 0f);

            // Top-right, only on endings worth telling someone about.
            _share = UiBuilder.Button(body, "Share", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Round,
                null, Design.Body, Icons.Share);
            SheetKit.PinTop(_share.Rect, TitleRow * 0.5f - Design.TouchTarget * 0.5f,
                Design.ContentWidth * 0.5f - Design.CloseDisc * 0.5f);
            _share.Clicked += () => _shareAction?.Invoke();

            _starRow = UiBuilder.Node(body, "Stars");
            _starRow.sizeDelta = new Vector2(Design.ContentWidth, StarRow);
            for (int i = 0; i < _stars.Length; i++)
            {
                var star = UiBuilder.Image(_starRow, "Star" + i, Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                // The middle star sits higher and larger, the way a podium does.
                float size = i == 1 ? 176f : 140f;
                star.rectTransform.sizeDelta = new Vector2(size, size);
                star.rectTransform.anchoredPosition = new Vector2((i - 1) * 196f, i == 1 ? 18f : -8f);
                _stars[i] = star;
            }

            _score = UiBuilder.Label(body, "Score", "0", Design.Readout, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _score.rectTransform.sizeDelta = new Vector2(Design.ContentWidth, ScoreRow);
            _score.enableAutoSizing = true;
            _score.fontSizeMin = Design.Title;
            _score.fontSizeMax = Design.Readout;
            _score.textWrappingMode = TextWrappingModes.NoWrap;
            UiBuilder.TextShadow(_score, 0.4f, -0.3f, 0.45f);

            _noteIcon = UiBuilder.Image(body, "NoteIcon", Icons.Flame, Design.Gold);
            _noteIcon.type = Image.Type.Simple;
            _noteIcon.rectTransform.sizeDelta = new Vector2(NoteIconSize, NoteIconSize);

            _note = UiBuilder.Label(body, "Note", "", Design.Body, Design.Mint,
                Design.FontDisplay, tracking: Design.TrackingLabel);
            _note.rectTransform.sizeDelta = new Vector2(Design.ContentWidth - NoteIconSize * 2f, NoteRow);
            _note.enableAutoSizing = true;
            _note.fontSizeMin = Design.Label;
            _note.fontSizeMax = Design.Body;
            _note.textWrappingMode = TextWrappingModes.NoWrap;

            _primary = UiBuilder.Button(body, "Primary", new Vector2(Design.ContentWidth, Design.ButtonLg), UiButton.Style.Primary,
                Str.PlayAgain, Design.Headline);
            _primary.Clicked += () => PrimaryClicked?.Invoke();

            _secondary = UiBuilder.Button(body, "Secondary", new Vector2(Design.ContentWidth, Design.ButtonMd), UiButton.Style.Secondary,
                Str.MainMenu, Design.Body);
            _secondary.Clicked += () => SecondaryClicked?.Invoke();

            _panel.gameObject.SetActive(false);
        }

        UiButton _share;
        Action _shareAction;

        // ------------------------------------------------------------------ confetti

        sealed class Paper
        {
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Flutter;
            public float Phase;
            public float Age;
            public float Life;
        }

        RectTransform _confettiRoot;
        readonly System.Collections.Generic.List<Paper> _papers = new System.Collections.Generic.List<Paper>();
        Coroutine _confetti;
        uint _seed = 88172645u;

        public Transform Note => _note.transform;
        public Transform NoteIcon => _noteIcon.transform;

        /// <summary>
        /// Paper falling behind the card — a solved puzzle, a level won, a new record. Once, for
        /// about two seconds, and then the card is left alone: a moment, not a loop.
        /// </summary>
        public void Celebrate(MonoBehaviour host, int amount = 46)
        {
            if (_confettiRoot == null) return;

            var colours = Design.Blocks;
            float halfWidth = 1080f * 0.5f;
            float top = _panel.rect.height * 0.5f;

            for (int i = 0; i < amount; i++)
            {
                var paper = i < _papers.Count ? _papers[i] : NewPaper();
                var rect = paper.Image.rectTransform;

                // Two bursts from the upper corners, thrown inwards and up, then drifting down.
                bool left = i % 2 == 0;
                rect.anchoredPosition = new Vector2(left ? -halfWidth : halfWidth, top * (0.35f + Random01() * 0.3f));
                rect.sizeDelta = new Vector2(20f + Random01() * 16f, 11f + Random01() * 9f);
                rect.localEulerAngles = new Vector3(0f, 0f, Random01() * 360f);

                paper.Velocity = new Vector2((left ? 1f : -1f) * (380f + Random01() * 620f), 500f + Random01() * 700f);
                paper.Spin = (Random01() - 0.5f) * 720f;
                paper.Flutter = 6f + Random01() * 8f;
                paper.Phase = Random01() * 6.28f;
                paper.Age = -Random01() * 0.25f;
                paper.Life = 1.8f + Random01() * 0.8f;
                paper.Image.color = (i % 5 == 0 ? Design.Gold : colours[i % colours.Length]).WithAlpha(0f);
                paper.Image.gameObject.SetActive(true);
            }

            for (int i = amount; i < _papers.Count; i++) _papers[i].Image.gameObject.SetActive(false);

            if (_confetti != null) host.StopCoroutine(_confetti);
            _confetti = host.StartCoroutine(ConfettiRoutine(amount));
        }

        Paper NewPaper()
        {
            var image = UiBuilder.Image(_confettiRoot, "Paper", Art.Panel(4f), Color.white);
            image.gameObject.SetActive(false);
            var paper = new Paper { Image = image };
            _papers.Add(paper);
            return paper;
        }

        IEnumerator ConfettiRoutine(int count)
        {
            bool alive = true;
            while (alive)
            {
                alive = false;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

                for (int i = 0; i < count && i < _papers.Count; i++)
                {
                    var paper = _papers[i];
                    if (!paper.Image.gameObject.activeSelf) continue;

                    paper.Age += dt;
                    if (paper.Age < 0f) { alive = true; continue; }

                    float k = paper.Age / paper.Life;
                    if (k >= 1f)
                    {
                        paper.Image.gameObject.SetActive(false);
                        continue;
                    }

                    alive = true;

                    // Paper, not stone: strong drag and a low terminal speed, so it floats down.
                    paper.Velocity += new Vector2(0f, -1500f * dt);
                    paper.Velocity *= 1f - 2.6f * dt;

                    var rect = paper.Image.rectTransform;
                    rect.anchoredPosition += paper.Velocity * dt;
                    rect.localEulerAngles += new Vector3(0f, 0f, paper.Spin * dt);

                    // The flip of a falling scrap: its width swings through zero and back.
                    float flip = Mathf.Cos(paper.Phase + paper.Age * paper.Flutter);
                    rect.localScale = new Vector3(flip, 1f, 1f);

                    var c = paper.Image.color;
                    paper.Image.color = c.WithAlpha(k < 0.05f ? k / 0.05f : 1f - Ease.InQuad(Mathf.InverseLerp(0.7f, 1f, k)));
                }

                yield return null;
            }

            _confetti = null;
        }

        float Random01()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>
        /// Shows the card. <paramref name="stars"/> is -1 to leave the star row out, otherwise
        /// 0..3 earned stars, which pop in one by one through <paramref name="onStar"/>.
        /// </summary>
        public void Show(MonoBehaviour host, string title, string score, string note, Color noteColor, Sprite noteIcon,
            int stars, string primary, string secondary, Action<int> onStar = null, Action share = null)
        {
            _shareAction = share;
            _share.gameObject.SetActive(share != null);
            _title.text = title;
            _score.text = score;
            _note.text = note;
            _note.color = noteColor;
            _primary.Label.text = primary;
            _secondary.Label.text = secondary;

            bool withStars = stars >= 0;
            _starRow.gameObject.SetActive(withStars);

            // Top down; without stars the number moves up into the room they would have used.
            float top = TitleRow + Design.Space2;
            if (withStars)
            {
                SheetKit.PinTop(_starRow, top);
                top += StarRow;
            }

            SheetKit.PinTop(_score.rectTransform, top);
            top += ScoreRow;

            _noteIcon.gameObject.SetActive(noteIcon != null && !string.IsNullOrEmpty(note));
            if (noteIcon != null)
            {
                _noteIcon.sprite = noteIcon;
                _noteIcon.color = noteColor;

                float width = Mathf.Min(_note.GetPreferredValues(note).x, _note.rectTransform.sizeDelta.x);
                float group = NoteIconSize + NoteIconGap + width;
                SheetKit.PinTop(_noteIcon.rectTransform, top + (NoteRow - NoteIconSize) * 0.5f, -group * 0.5f + NoteIconSize * 0.5f);
                SheetKit.PinTop(_note.rectTransform, top, -group * 0.5f + NoteIconSize + NoteIconGap + width * 0.5f);
            }
            else
            {
                SheetKit.PinTop(_note.rectTransform, top);
            }
            top += NoteRow + Design.Space5;

            SheetKit.PinTop(_primary.Rect, top);
            top += Design.ButtonLg + Design.Space3;
            SheetKit.PinTop(_secondary.Rect, top);
            top += Design.ButtonMd;
            _sheet.SetBodyHeight(top);

            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);

            // The sheet owns the pointer: the pause button and the powers behind the dim stay inert.
            PointerRouter.PushBlocker(_panel);

            if (_motion != null) host.StopCoroutine(_motion);
            _motion = host.StartCoroutine(_sheet.Enter(false));

            if (withStars) host.StartCoroutine(StarRoutine(stars, onStar));
        }

        IEnumerator StarRoutine(int earned, Action<int> onStar)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = Design.SurfaceControl;
                _stars[i].rectTransform.localScale = Vector3.one;
            }

            yield return new WaitForSecondsRealtime(0.35f);

            for (int i = 0; i < earned && i < _stars.Length; i++)
            {
                _stars[i].color = Design.Gold;
                onStar?.Invoke(i);
                yield return Tween.Scale(_stars[i].rectTransform, Vector3.one * 0.3f, Vector3.one, 0.26f, Ease.OutBack);
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        public void Hide()
        {
            if (_panel == null) return;
            foreach (var paper in _papers) paper.Image.gameObject.SetActive(false);
            _panel.gameObject.SetActive(false);
            PointerRouter.PopBlocker(_panel);
        }
    }
}
