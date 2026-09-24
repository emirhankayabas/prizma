using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The card at the end of a run or a level: a heading, the big number, an optional row of
    /// stars, one line of note, and two actions. One card for every ending so they all feel like
    /// the same product; only the words and the stars change.
    /// </summary>
    public sealed class ResultCard
    {
        static readonly Vector2 CardSize = new Vector2(Design.ContentWidth, 1180f);

        /// <summary>
        /// Without the star row the card is shorter by what the stars took, rather than keeping
        /// their height as an empty band between the note and the buttons.
        /// </summary>
        const float HeightNoStars = 1100f;

        // Distances from the card's top edge to the centre of each line.
        const float TitleLine = Design.CardHeading;
        const float StarLine = 280f;
        const float ScoreLine = 470f, ScoreLineNoStars = 380f;
        const float NoteLine = 612f, NoteLineNoStars = 522f;
        const float NoteIconSize = 68f;
        const float NoteIconGap = 16f;

        float Top => _card.sizeDelta.y * 0.5f;

        Image _shadow;
        Image _fill;
        Image _hairline;

        RectTransform _panel;
        Image _scrim;
        RectTransform _card;
        TextMeshProUGUI _title;
        TextMeshProUGUI _score;
        TextMeshProUGUI _note;
        Image _noteIcon;
        readonly Image[] _stars = new Image[3];
        RectTransform _starRow;
        UiButton _primary;
        UiButton _secondary;

        public event Action PrimaryClicked;
        public event Action SecondaryClicked;

        public bool Visible => _panel != null && _panel.gameObject.activeSelf;

        public void Build(RectTransform root)
        {
            _panel = UiBuilder.Node(root, "Result");
            UiBuilder.Stretch(_panel);

            _scrim = UiBuilder.Image(_panel, "Scrim", Art.Panel(0f), Design.Scrim);
            _scrim.type = Image.Type.Simple;
            UiBuilder.StretchFullScreen(_scrim.rectTransform);

            // Between the scrim and the card: the paper falls behind the result, never over its words.
            _confettiRoot = UiBuilder.Child(_panel, "Confetti");
            _confettiRoot.gameObject.AddComponent<Canvas>();

            _card = UiBuilder.Node(_panel, "Card");
            _card.sizeDelta = CardSize;

            _shadow = UiBuilder.Shadow(_card, "Shadow", CardSize, Design.RadiusLg, Design.E3);
            _fill = UiBuilder.Panel(_card, "Fill", CardSize, Design.SurfaceHigh, Design.RadiusLg);
            _hairline = UiBuilder.Hairline(_card, "Hairline", CardSize, Design.RadiusLg);

            _title = UiBuilder.Label(_card, "Title", "", Design.Title, Design.TextSecondary, Design.FontMedium);
            _title.rectTransform.sizeDelta = new Vector2(CardSize.x - Design.CardPadding * 2f, 110f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, Top - TitleLine);

            _starRow = UiBuilder.Node(_card, "Stars");
            _starRow.anchoredPosition = new Vector2(0f, Top - StarLine);
            for (int i = 0; i < _stars.Length; i++)
            {
                var star = UiBuilder.Image(_starRow, "Star" + i, Icons.Star, Design.Gold);
                star.type = Image.Type.Simple;
                // The middle star sits higher and larger, the way a podium does.
                float size = i == 1 ? 180f : 144f;
                star.rectTransform.sizeDelta = new Vector2(size, size);
                star.rectTransform.anchoredPosition = new Vector2((i - 1) * 200f, i == 1 ? 26f : 0f);
                _stars[i] = star;
            }

            _score = UiBuilder.Label(_card, "Score", "0", Design.Readout, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _score.rectTransform.sizeDelta = new Vector2(CardSize.x, 230f);
            UiBuilder.TextShadow(_score, 0.4f, -0.3f, 0.45f);

            _noteIcon = UiBuilder.Image(_card, "NoteIcon", Icons.Flame, Design.Gold);
            _noteIcon.type = Image.Type.Simple;
            _noteIcon.rectTransform.sizeDelta = new Vector2(NoteIconSize, NoteIconSize);

            _note = UiBuilder.Label(_card, "Note", "", Design.Body, Design.Mint,
                Design.FontDisplay, tracking: Design.TrackingLabel);
            _note.rectTransform.sizeDelta = new Vector2(CardSize.x - Design.CardPadding * 2f, 80f);

            float inner = CardSize.x - Design.CardPadding * 2f;

            _primary = UiBuilder.Button(_card, "Primary", new Vector2(inner, Design.ButtonLg), UiButton.Style.Primary,
                Str.PlayAgain, Design.Headline);
            _primary.Clicked += () => PrimaryClicked?.Invoke();

            _secondary = UiBuilder.Button(_card, "Secondary", new Vector2(inner, Design.ButtonMd), UiButton.Style.Secondary,
                Str.MainMenu, Design.Body);
            _secondary.Clicked += () => SecondaryClicked?.Invoke();

            ModalCard.StackFromBottom(_card, Design.Space3, _primary.Rect, _secondary.Rect);

            // Top-right corner of the card, only on endings worth telling someone about.
            _share = UiBuilder.Button(_card, "Share", new Vector2(Design.TouchTarget, Design.TouchTarget), UiButton.Style.Icon,
                null, Design.Body, Icons.Share);
            _share.Clicked += () => _shareAction?.Invoke();

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
            SetHeight(withStars ? CardSize.y : HeightNoStars);

            // Without stars the number moves up into the space they would have used.
            float scoreY = Top - (withStars ? ScoreLine : ScoreLineNoStars);
            float noteY = Top - (withStars ? NoteLine : NoteLineNoStars);
            _score.rectTransform.anchoredPosition = new Vector2(0f, scoreY);

            _noteIcon.gameObject.SetActive(noteIcon != null && !string.IsNullOrEmpty(note));
            if (noteIcon != null)
            {
                _noteIcon.sprite = noteIcon;
                _noteIcon.color = noteColor;

                float width = _note.GetPreferredValues(note).x;
                float group = NoteIconSize + NoteIconGap + width;
                _noteIcon.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + NoteIconSize * 0.5f, noteY);
                _note.rectTransform.anchoredPosition = new Vector2(-group * 0.5f + NoteIconSize + NoteIconGap + width * 0.5f, noteY);
            }
            else
            {
                _note.rectTransform.anchoredPosition = new Vector2(0f, noteY);
            }

            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);

            // The card owns the pointer: the pause button and the powers behind the scrim stay inert.
            PointerRouter.PushBlocker(_panel);

            host.StartCoroutine(Tween.FadeGraphic(_scrim, 0f, Design.Scrim.a, 0.25f));
            host.StartCoroutine(Tween.Scale(_card, Vector3.one * 0.86f, Vector3.one, 0.32f, Ease.OutBack));

            if (withStars) host.StartCoroutine(StarRoutine(stars, onStar));
        }

        void SetHeight(float height)
        {
            var size = new Vector2(CardSize.x, height);
            var shadowPad = _shadow.rectTransform.sizeDelta - _card.sizeDelta;

            _card.sizeDelta = size;
            _fill.rectTransform.sizeDelta = size;
            _hairline.rectTransform.sizeDelta = size;
            _shadow.rectTransform.sizeDelta = size + shadowPad;

            _title.rectTransform.anchoredPosition = new Vector2(0f, Top - TitleLine);
            _starRow.anchoredPosition = new Vector2(0f, Top - StarLine);
            ModalCard.StackFromBottom(_card, Design.Space3, _primary.Rect, _secondary.Rect);
            _share.Rect.anchoredPosition = new Vector2(CardSize.x * 0.5f - Design.Space3 - Design.TouchTarget * 0.5f,
                Top - Design.Space3 - Design.TouchTarget * 0.5f);
        }

        IEnumerator StarRoutine(int earned, Action<int> onStar)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = Color.white.WithAlpha(0.14f);
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
