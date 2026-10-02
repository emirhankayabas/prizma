using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The one shape every modal takes: a sheet that rises from the bottom edge over a dimmed page.
    ///
    /// It speaks the game's language, not a settings app's: a glossy medallion with the sheet's
    /// sign rides over its top edge, the title sits centred under it, the top of the sheet is lit
    /// in the sheet's own colour, and everything arrives with a little spring — the sheet
    /// overshoots and settles, the medallion pops, the content follows in a quick cascade. The
    /// first version was a grabber, a left-aligned title and a grey close disc over grouped lists
    /// with hairlines between rows, and the player said it looked like a mobile web page.
    /// It closes four ways — the corner button, a tap on the dimmed page, a swipe down on its top
    /// strip, and the back button — so no sheet spends a row on a "close" button.
    ///
    /// Its bottom corners sit below the screen edge and its fill runs under the gesture bar, the
    /// way the platform's own sheets do. The body is laid out from its top edge; the sheet takes
    /// its height from the body, never more than the page leaves (<see cref="MaxBody"/>).
    /// </summary>
    public sealed class Sheet
    {
        public const float Width = AppController.ReferenceWidth;

        /// <summary>Between the body's last item and the top of the gesture bar.</summary>
        public const float BottomPad = Design.Space4;

        public RectTransform Panel { get; private set; }

        /// <summary>The content area: <see cref="Design.ContentWidth"/> wide, top-anchored.</summary>
        public RectTransform Body { get; private set; }

        public TextMeshProUGUI Title { get; private set; }

        /// <summary>Null on a sheet that has to be answered.</summary>
        public UiButton CloseButton { get; private set; }

        MonoBehaviour _host;
        Action _onClose;
        RectTransform _hero;
        Image _heroDisc;
        Image _heroGlyph;
        Image _wash;
        float _heroOut;
        Image _scrim;
        float _scrimAlpha;
        float _header;
        float _pageHeight;
        float _bleed;
        float _visible;
        float _offset;
        Coroutine _motion;

        /// <summary>Tallest body this page allows under the header.</summary>
        public float MaxBody => _pageHeight - Design.SheetTopGap - _heroOut - _header - BottomPad;

        /// <summary>Visible height, from the top of the gesture bar to the sheet's top edge.</summary>
        public float VisibleHeight => _visible;

        float RestY => -_bleed;
        float HiddenY => -_bleed - _visible - Design.E3.Spread;

        /// <param name="title">Null for a sheet without a title row (the result sheet builds its own).</param>
        /// <param name="onClose">Null for a sheet that must be answered: no close button, no swipe, no outside tap.</param>
        /// <param name="icon">The sheet's sign, shown in the medallion over its top edge. Null for none.</param>
        /// <param name="tint">The sheet's colour: the medallion, and the light across the top of the sheet.</param>
        public static Sheet Build(MonoBehaviour host, RectTransform root, float pageHeight, string title, Action onClose,
            Sprite icon = null, Color? tint = null)
        {
            var sheet = new Sheet();
            sheet.Construct(host, root, pageHeight, title, onClose, icon, tint ?? Design.AccentA);
            return sheet;
        }

        void Construct(MonoBehaviour host, RectTransform root, float pageHeight, string title, Action onClose, Sprite icon, Color tint)
        {
            _host = host;
            _onClose = onClose;
            _pageHeight = pageHeight;
            _bleed = AppController.SafeInsets.y + Design.SheetRadius;

            _scrim = UiBuilder.Image(root, "Scrim", Art.Panel(0f), Design.Scrim);
            _scrim.type = Image.Type.Simple;
            UiBuilder.StretchFullScreen(_scrim.rectTransform);
            _scrimAlpha = _scrim.color.a;

            UiHit outside = null;
            if (onClose != null)
            {
                // Before the panel in the hierarchy, so every widget on the sheet is found first.
                outside = UiHit.Create(root, "Outside");
                UiBuilder.StretchFullScreen(outside.Rect);
                outside.Clicked += () => _onClose?.Invoke();
            }

            Panel = UiBuilder.Node(root, "Sheet");
            Panel.anchorMin = Panel.anchorMax = new Vector2(0.5f, 0f);
            Panel.pivot = new Vector2(0.5f, 0f);
            if (outside != null) outside.Exclude = Panel;

            var shadow = UiBuilder.Shadow(Panel, "Shadow", Vector2.one, Design.SheetRadius, Design.E3);
            float pad = Art.ShadowPad(Design.E3.Spread);
            UiBuilder.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-pad, -pad - Design.E3.OffsetY);
            shadow.rectTransform.offsetMax = new Vector2(pad, pad - Design.E3.OffsetY);

            var fill = UiBuilder.Image(Panel, "Fill", Art.Panel(Design.SheetRadius), Design.SurfaceSheet);
            UiBuilder.Stretch(fill.rectTransform);

            // The sheet's own light across its top, fading down: the colour of what it is about.
            _wash = UiBuilder.Image(Panel, "Wash", Art.TopWash(Design.SheetRadius), tint.WithAlpha(0.34f));
            _wash.rectTransform.anchorMin = new Vector2(0f, 1f);
            _wash.rectTransform.anchorMax = new Vector2(1f, 1f);
            _wash.rectTransform.pivot = new Vector2(0.5f, 1f);
            _wash.rectTransform.sizeDelta = new Vector2(0f, 460f);
            _wash.rectTransform.anchoredPosition = Vector2.zero;

            var edge = UiBuilder.Hairline(Panel, "Edge", Vector2.one, Design.SheetRadius, Color.white.WithAlpha(0.16f));
            UiBuilder.Stretch(edge.rectTransform);

            if (title != null)
            {
                bool hero = icon != null;
                _header = hero ? Design.SheetHeroHeader : Design.SheetHeader;
                _heroOut = hero ? Design.SheetHero * 0.5f : 0f;

                if (onClose != null)
                {
                    // The whole top strip takes the swipe; the close button, built after it, wins its own corner.
                    var drag = UiSheetDrag.Create(Panel, this);
                    drag.Rect.anchorMin = drag.Rect.anchorMax = new Vector2(0.5f, 1f);
                    drag.Rect.pivot = new Vector2(0.5f, 1f);
                    drag.Rect.sizeDelta = new Vector2(Width, _header);
                    drag.Rect.anchoredPosition = Vector2.zero;
                }

                if (hero) BuildHero(icon, tint);

                // Centred, between the close button's column and its mirror on the left.
                float titleLine = hero ? Design.SheetHeroTitleLine : Design.SheetTitleLine;
                float titleWidth = Design.ContentWidth - (Design.TouchTarget + Design.Space3) * 2f;
                Title = UiBuilder.Label(Panel, "Title", title, Design.Title, Design.TextPrimary, Design.FontDisplay,
                    TextAlignmentOptions.Center, Design.TrackingDisplay * 0.5f);
                Title.rectTransform.sizeDelta = new Vector2(titleWidth, Design.TouchTarget);
                Title.enableAutoSizing = true;
                Title.fontSizeMin = Design.Headline;
                Title.fontSizeMax = Design.Title;
                Title.textWrappingMode = TextWrappingModes.NoWrap;
                UiBuilder.TextShadow(Title, 0.4f, -0.3f, 0.45f);
                SheetKit.PinTop(Title.rectTransform, titleLine - Design.TouchTarget * 0.5f);

                if (onClose != null)
                {
                    CloseButton = UiBuilder.Button(Panel, "Close", new Vector2(Design.TouchTarget, Design.TouchTarget),
                        UiButton.Style.Round, null, Design.Body, Icons.Close);
                    // The disc's right edge on the content edge; the touch target runs past it into the margin.
                    SheetKit.PinTop(CloseButton.Rect, Design.SheetCloseLine - Design.TouchTarget * 0.5f,
                        Design.ContentWidth * 0.5f - Design.CloseDisc * 0.5f);
                    CloseButton.Clicked += () => _onClose?.Invoke();
                }
            }
            else
            {
                _header = Design.Space5;
            }

            Body = UiBuilder.Node(Panel, "Body");
            SheetKit.PinTop(Body, _header);
            Body.sizeDelta = new Vector2(Design.ContentWidth, 0f);

            SetBodyHeight(0f);
            Settle();
        }

        /// <summary>
        /// The medallion: the sheet's sign in white on a glossy disc of its colour, on a white rim,
        /// half over the sheet's top edge — the one thing that says "game" before a word is read.
        /// </summary>
        void BuildHero(Sprite icon, Color tint)
        {
            float size = Design.SheetHero;
            _hero = UiBuilder.Node(Panel, "Hero");
            _hero.anchorMin = _hero.anchorMax = new Vector2(0.5f, 1f);
            _hero.pivot = new Vector2(0.5f, 0.5f);
            _hero.sizeDelta = new Vector2(size, size);
            _hero.anchoredPosition = Vector2.zero;

            var glow = UiBuilder.Image(_hero, "Glow", Art.Pool, tint.WithAlpha(0.5f));
            glow.type = Image.Type.Simple;
            glow.rectTransform.sizeDelta = new Vector2(size * 2.2f, size * 2.2f);

            var shadow = UiBuilder.Image(_hero, "Shadow", Art.Shadow(size * 0.5f, 36f), Color.black.WithAlpha(0.4f));
            shadow.type = Image.Type.Simple;
            float pad = Art.ShadowPad(36f);
            shadow.rectTransform.sizeDelta = new Vector2(size + pad * 2f, size + pad * 2f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -10f);

            var rim = UiBuilder.Image(_hero, "Rim", Art.Disc, Color.white);
            rim.type = Image.Type.Simple;
            rim.rectTransform.sizeDelta = new Vector2(size, size);

            _heroDisc = UiBuilder.Image(_hero, "Disc", Art.Node, tint);
            _heroDisc.type = Image.Type.Simple;
            _heroDisc.rectTransform.sizeDelta = new Vector2(size - 16f, size - 16f);

            _heroGlyph = UiBuilder.Image(_hero, "Glyph", icon, Color.white);
            _heroGlyph.type = Image.Type.Simple;
            _heroGlyph.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
            _heroGlyph.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        }

        /// <summary>Changes the medallion and the sheet's light — a level's sheet takes its world's colour.</summary>
        public void SetHero(Sprite icon, Color tint)
        {
            if (_hero == null) return;
            if (icon != null) _heroGlyph.sprite = icon;
            _heroDisc.color = tint;
            _hero.Find("Glow").GetComponent<Image>().color = tint.WithAlpha(0.5f);
            _wash.color = tint.WithAlpha(0.34f);
        }

        /// <summary>
        /// Sizes the sheet to a body of <paramref name="height"/>. A body taller than the page allows
        /// is scaled down whole rather than cut: a short phone gets the same sheet a little smaller.
        /// </summary>
        public void SetBodyHeight(float height, bool animate = false)
        {
            float room = Mathf.Max(1f, MaxBody);
            float scale = height > room ? room / height : 1f;
            Body.localScale = Vector3.one * scale;
            Body.sizeDelta = new Vector2(Design.ContentWidth, height);

            float target = _header + height * scale + BottomPad;
            if (!animate || !_host.isActiveAndEnabled)
            {
                ApplyVisible(target);
                return;
            }

            _host.StartCoroutine(ResizeRoutine(target));
        }

        void ApplyVisible(float visible)
        {
            _visible = visible;
            Panel.sizeDelta = new Vector2(Width, _visible + _bleed);
        }

        IEnumerator ResizeRoutine(float target)
        {
            float from = _visible;
            const float duration = 0.26f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                ApplyVisible(Mathf.Lerp(from, target, Ease.OutCubic(t / duration)));
                yield return null;
            }

            ApplyVisible(target);
        }

        // ------------------------------------------------------------------ motion

        void SetY(float y) => Panel.anchoredPosition = new Vector2(0f, y);

        /// <summary>The finished look, with no entrance: the sheet at rest, the page dimmed.</summary>
        public void Settle()
        {
            _offset = 0f;
            SetY(RestY);
            _scrim.color = _scrim.color.WithAlpha(_scrimAlpha);
            Cascade.Settle(Body);
            if (Title != null) Title.rectTransform.localScale = Vector3.one;
            if (CloseButton != null) CloseButton.Content.localScale = Vector3.one;
            if (_hero != null)
            {
                _hero.localScale = Vector3.one;
                _hero.localEulerAngles = Vector3.zero;
            }
        }

        /// <summary>
        /// The way in: the sheet springs up past its rest and settles, the medallion pops in with
        /// a turn, the title and then the body's parts follow one after another. All of it inside
        /// half a second, so it reads as liveliness, not as a wait.
        /// </summary>
        public IEnumerator Enter(bool overModal)
        {
            _offset = 0f;
            float from = HiddenY;
            SetY(from);
            float scrimFrom = overModal ? _scrimAlpha : 0f;
            _scrim.color = _scrim.color.WithAlpha(scrimFrom);

            if (_hero != null)
            {
                _hero.localScale = Vector3.zero;
                _host.StartCoroutine(HeroIn());
            }

            if (Title != null) _host.StartCoroutine(Cascade.In(Title.rectTransform, 0.1f));
            if (CloseButton != null) _host.StartCoroutine(Tween.Scale(CloseButton.Content, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, 0.16f));
            Cascade.Children(_host, Body, 0.14f);

            const float duration = 0.46f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                SetY(Mathf.LerpUnclamped(from, RestY, Ease.OutBackSoft(k)));
                _scrim.color = _scrim.color.WithAlpha(Mathf.Lerp(scrimFrom, _scrimAlpha, Ease.OutQuad(Mathf.Min(1f, k * 1.6f))));
                yield return null;
            }

            // Only the sheet itself lands here; the medallion and the cascade finish on their own.
            _offset = 0f;
            SetY(RestY);
            _scrim.color = _scrim.color.WithAlpha(_scrimAlpha);
        }

        IEnumerator HeroIn()
        {
            yield return new WaitForSecondsRealtime(0.08f);
            const float duration = 0.42f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float s = Mathf.LerpUnclamped(0f, 1f, Ease.OutBack(k));
                _hero.localScale = new Vector3(s, s, 1f);
                _hero.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-28f, 0f, Ease.OutCubic(k)));
                yield return null;
            }

            _hero.localScale = Vector3.one;
            _hero.localEulerAngles = Vector3.zero;
        }

        /// <summary>A small float of the medallion while the sheet is open — motion, not a blink.</summary>
        public void Idle(float time)
        {
            if (_hero != null && _hero.localScale.x >= 0.999f)
                _hero.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 2f) * 5f);
        }

        /// <param name="revealsModal">A modal beneath comes back: the page stays dimmed.</param>
        public IEnumerator Exit(bool revealsModal)
        {
            float from = Panel.anchoredPosition.y;
            float to = HiddenY;
            float scrimFrom = _scrim.color.a;

            const float duration = 0.2f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                SetY(Mathf.Lerp(from, to, Ease.InQuad(k)));
                if (!revealsModal) _scrim.color = _scrim.color.WithAlpha(Mathf.Lerp(scrimFrom, 0f, k));
                yield return null;
            }

            SetY(to);
        }

        // ------------------------------------------------------------------ swipe to close

        float _lastOffset;
        float _lastTime;
        float _speed;

        internal void DragBegin()
        {
            if (_motion != null) _host.StopCoroutine(_motion);
            _motion = null;
            _lastOffset = _offset;
            _lastTime = Time.unscaledTime;
            _speed = 0f;
        }

        /// <summary><paramref name="pulled"/> is how far the finger has moved down, in canvas units.</summary>
        internal void DragTo(float pulled)
        {
            // Down follows the finger; up gives a little and resists, so the sheet feels held, not stuck.
            _offset = pulled >= 0f ? pulled : -Mathf.Sqrt(-pulled) * 4f;
            SetY(RestY - _offset);

            float dt = Time.unscaledTime - _lastTime;
            if (dt > 0.0001f)
            {
                _speed = Mathf.Lerp(_speed, (_offset - _lastOffset) / dt, 0.6f);
                _lastOffset = _offset;
                _lastTime = Time.unscaledTime;
            }
        }

        internal void DragEnd()
        {
            if (_offset > _visible * 0.22f || (_speed > 1600f && _offset > Design.Space4))
            {
                _onClose?.Invoke();
                return;
            }

            _motion = _host.StartCoroutine(SpringBack());
        }

        IEnumerator SpringBack()
        {
            float from = _offset;
            const float duration = 0.24f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _offset = Mathf.Lerp(from, 0f, Ease.OutCubic(t / duration));
                SetY(RestY - _offset);
                yield return null;
            }

            _offset = 0f;
            SetY(RestY);
            _motion = null;
        }

        /// <summary>Converts a screen point to canvas units on the sheet's own parent.</summary>
        internal float LocalY(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Panel.parent, screenPoint, null, out var local);
            return local.y;
        }
    }

    /// <summary>
    /// A modal page built as a <see cref="Sheet"/>. Subclasses call <see cref="BuildSheet"/>, fill
    /// <see cref="Body"/> from its top edge down, and size it with <see cref="Sheet.SetBodyHeight"/>.
    /// </summary>
    public abstract class SheetScreen : AppScreen
    {
        public override bool IsModal => true;

        protected Sheet Sheet;

        protected RectTransform Body => Sheet.Body;

        protected void BuildSheet(string title, Sprite icon = null, Color? tint = null, bool dismissible = true)
        {
            Sheet = Sheet.Build(this, Root, App.PageHeight, title, dismissible ? (Action)Close : null, icon, tint);
        }

        protected virtual void Update()
        {
            if (Sheet != null) Sheet.Idle(Time.unscaledTime);
        }

        void Close()
        {
            Audio.PlayClick();
            App.CloseModal();
        }

        protected override IEnumerator Enter(bool overModal)
        {
            Group.alpha = 1f;
            return Sheet.Enter(overModal);
        }

        protected override void Settle()
        {
            Group.alpha = 1f;
            Sheet.Settle();
        }

        public override void Dismiss(bool revealsModal, Action done)
        {
            StopAllCoroutines();
            StartCoroutine(DismissRoutine(revealsModal, done));
        }

        IEnumerator DismissRoutine(bool revealsModal, Action done)
        {
            yield return Sheet.Exit(revealsModal);
            done();
        }
    }

    /// <summary>Layout pieces shared by every sheet: grouped lists, row icons, separators, progress bars.</summary>
    public static class SheetKit
    {
        /// <summary>Anchors a rect to its parent's top edge, <paramref name="top"/> units down, centred on <paramref name="x"/>.</summary>
        public static RectTransform PinTop(RectTransform rect, float top, float x = 0f)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            return rect;
        }

        /// <summary>
        /// A grouped list's card, full content width, pinned <paramref name="top"/> units under the
        /// top of <paramref name="parent"/>. Rows inside are placed from its centre with <see cref="RowY"/>.
        /// </summary>
        public static RectTransform Group(RectTransform parent, string name, float top, float height, Color? color = null)
        {
            // A raised card of the same soft plastic as the buttons: lit from the top, a light
            // edge, a soft shadow under it. A flat grey rectangle is what made the lists read as
            // an app's settings page.
            var size = new Vector2(Design.ContentWidth, height);
            var holder = UiBuilder.Node(parent, name);
            holder.sizeDelta = size;
            PinTop(holder, top);
            UiBuilder.Shadow(holder, "Shadow", size, Design.RadiusMd, Design.E1);
            var card = UiBuilder.Image(holder, "Fill", Art.PanelGradient(Design.RadiusMd), SheetKit.Lifted(color ?? Design.SurfaceGroup));
            card.rectTransform.sizeDelta = size;
            UiBuilder.Hairline(holder, "Edge", size, Design.RadiusMd, Color.white.WithAlpha(0.12f));
            return holder;
        }

        /// <summary>
        /// A surface colour a step lighter, for the gradient sprite: its bottom is darker than its
        /// top, and the average has to land on the colour the design asked for.
        /// </summary>
        public static Color Lifted(Color colour) => Tinted(colour, Color.white, 0.08f);

        /// <summary>Centre of row <paramref name="index"/> in a group of <paramref name="height"/>.</summary>
        public static float RowY(float height, int index, float rowHeight = Design.RowHeight) =>
            height * 0.5f - rowHeight * (index + 0.5f);

        /// <summary>Left edge of a row's content inside a group.</summary>
        public static float RowLeft => -Design.ContentWidth * 0.5f + Design.GroupPadding;

        /// <summary>Right edge of a row's content inside a group.</summary>
        public static float RowRight => Design.ContentWidth * 0.5f - Design.GroupPadding;

        /// <summary>Where a row's text starts, after its icon tile.</summary>
        public static float RowText => RowLeft + Design.RowIcon + Design.Space3;

        /// <summary>A small coloured squircle with a white glyph: what a row is, before its word is read.</summary>
        public static Image RowIcon(RectTransform parent, string name, Sprite icon, Color tint, float x, float y,
            float size = Design.RowIcon)
        {
            // Glossy, like a block: a gradient squircle with a lighter lip.
            var tile = UiBuilder.Image(parent, name, Art.PanelGradient(size * 0.3f), Tinted(tint, Color.white, 0.12f));
            tile.rectTransform.sizeDelta = new Vector2(size, size);
            tile.rectTransform.anchoredPosition = new Vector2(x, y);

            var glyph = UiBuilder.Image(tile.rectTransform, "Glyph", icon, Color.white);
            glyph.type = Image.Type.Simple;
            glyph.rectTransform.sizeDelta = new Vector2(size * 0.6f, size * 0.6f);
            return glyph;
        }

        /// <summary>A row's name, left-aligned, starting at <paramref name="left"/>.</summary>
        public static TextMeshProUGUI RowLabel(RectTransform parent, string name, string text, float left, float width, float y,
            float size = Design.Label, Color? color = null)
        {
            var label = UiBuilder.Label(parent, name, text, size, color ?? Design.TextPrimary, Design.FontDisplay,
                TextAlignmentOptions.Left, Design.TrackingLabel * 0.5f);
            label.rectTransform.sizeDelta = new Vector2(width, Design.RowHeight * 0.6f);
            label.rectTransform.anchoredPosition = new Vector2(left + width * 0.5f, y);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = Design.Caption;
            label.fontSizeMax = size;
            return label;
        }

        /// <summary>
        /// Between two rows: a short, soft groove inset from both ends, rather than the full-width
        /// hairline of a settings list. The rows' own big icons carry the separation.
        /// </summary>
        public static void Separator(RectTransform parent, float y, float left)
        {
            float right = RowRight;
            var line = UiBuilder.Image(parent, "Separator", Art.Panel(3f), Color.black.WithAlpha(0.14f));
            line.rectTransform.sizeDelta = new Vector2(right - left, 6f);
            line.rectTransform.anchoredPosition = new Vector2((left + right) * 0.5f, y);
        }

        /// <summary>A pill progress track; returns the fill, sized with <see cref="SetBar"/>.</summary>
        public static Image Bar(RectTransform parent, string name, float width, Vector2 centre, Color fillColor)
        {
            var track = UiBuilder.Panel(parent, name, new Vector2(width, Design.BarHeight), Tinted(Design.SurfaceControl, Color.black, 0.2f), Design.BarHeight * 0.5f);
            track.rectTransform.anchoredPosition = centre;

            var fill = UiBuilder.Image(track.rectTransform, "Fill", Art.PanelGradient(Design.BarHeight * 0.5f), fillColor);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = new Vector2(0f, Design.BarHeight);
            return fill;
        }

        public static void SetBar(Image fill, float k)
        {
            float width = ((RectTransform)fill.rectTransform.parent).sizeDelta.x;
            k = Mathf.Clamp01(k);
            // Never thinner than its own height: a sliver of 2% reads as a speck, not as a start.
            fill.rectTransform.sizeDelta = new Vector2(k <= 0f ? 0f : Mathf.Max(Design.BarHeight, width * k), Design.BarHeight);
        }

        /// <summary>A quiet unseen-item dot — steady, never pulsing. It waits for the player.</summary>
        public static Image Dot(RectTransform parent, Vector2 position)
        {
            var dot = UiBuilder.Image(parent, "NewDot", Art.Disc, Design.Mint);
            dot.type = Image.Type.Simple;
            dot.rectTransform.sizeDelta = new Vector2(Design.Space3 + 4f, Design.Space3 + 4f);
            dot.rectTransform.anchoredPosition = position;
            return dot;
        }

        /// <summary>A colour on a surface, mixed opaque — the project renders in Linear, where a translucent tint reads far lighter.</summary>
        public static Color Tinted(Color surface, Color tint, float amount) =>
            new Color(Mathf.Lerp(surface.r, tint.r, amount), Mathf.Lerp(surface.g, tint.g, amount), Mathf.Lerp(surface.b, tint.b, amount), 1f);
    }

    /// <summary>
    /// A big square-ish tile: a glossy icon medallion over its name. As a switch it is lit in its
    /// own colour when on and quiet when off, with a tick in the corner; as an action it is always
    /// lit and simply pressed. What a game uses where an app would put a row in a list.
    /// </summary>
    public sealed class UiTile
    {
        UiButton _button;
        Image _fill;
        Image _medal;
        Image _glyph;
        Image _check;
        TextMeshProUGUI _label;
        Color _tint;
        bool _on;
        bool _toggle;

        public RectTransform Rect => _button.Rect;
        public bool IsOn => _on;
        public event Action<bool> Changed;
        public event Action Clicked;

        public static UiTile Create(RectTransform parent, string name, Vector2 size, Sprite icon, Color tint, string label)
            => Build(parent, name, size, icon, tint, label, toggle: true);

        public static UiTile CreateAction(RectTransform parent, string name, Vector2 size, Sprite icon, Color tint, string label)
            => Build(parent, name, size, icon, tint, label, toggle: false);

        static UiTile Build(RectTransform parent, string name, Vector2 size, Sprite icon, Color tint, string label, bool toggle)
        {
            var tile = new UiTile { _tint = tint, _toggle = toggle };
            var button = UiBuilder.Button(parent, name, size, UiButton.Style.Bare);
            tile._button = button;
            var content = button.Content;

            UiBuilder.Shadow(content, "Shadow", size, Design.RadiusLg, Design.E1).transform.SetAsFirstSibling();
            tile._fill = UiBuilder.Image(content, "Fill", Art.PanelGradient(Design.RadiusLg), Color.white);
            tile._fill.rectTransform.sizeDelta = size;
            UiBuilder.Hairline(content, "Edge", size, Design.RadiusLg, Color.white.WithAlpha(0.14f));

            float medal = Mathf.Min(116f, size.y * 0.52f);
            bool wide = size.x > size.y * 1.6f;
            var medalPos = wide ? new Vector2(-size.x * 0.5f + Design.Space4 + medal * 0.5f, 0f) : new Vector2(0f, size.y * 0.14f);

            tile._medal = UiBuilder.Image(content, "Medal", Art.Node, tint);
            tile._medal.type = Image.Type.Simple;
            tile._medal.rectTransform.sizeDelta = new Vector2(medal, medal);
            tile._medal.rectTransform.anchoredPosition = medalPos;
            tile._glyph = UiBuilder.Image(tile._medal.rectTransform, "Glyph", icon, Color.white);
            tile._glyph.type = Image.Type.Simple;
            tile._glyph.rectTransform.sizeDelta = new Vector2(medal * 0.54f, medal * 0.54f);

            if (wide)
            {
                float left = -size.x * 0.5f + Design.Space4 + medal + Design.Space3;
                // Room kept on the right for the tick in the corner.
                float width = size.x * 0.5f - Design.Space5 - left;
                tile._label = UiBuilder.Label(content, "Label", label, Design.Label, Color.white, Design.FontDisplay,
                    TextAlignmentOptions.Left, Design.TrackingLabel * 0.3f);
                tile._label.rectTransform.sizeDelta = new Vector2(width, size.y - Design.Space4);
                tile._label.rectTransform.anchoredPosition = new Vector2(left + width * 0.5f, 0f);
            }
            else
            {
                tile._label = UiBuilder.Label(content, "Label", label, Design.Caption, Color.white, Design.FontDisplay,
                    TextAlignmentOptions.Center, Design.TrackingLabel * 0.3f);
                tile._label.rectTransform.sizeDelta = new Vector2(size.x - Design.Space3, 70f);
                tile._label.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.32f);
            }

            // Two words may take two lines; one word never breaks — it shrinks instead
            // ("NOTIFICATION / S" was what wrapping did to it).
            tile._label.textWrappingMode = label.Contains(" ") ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tile._label.lineSpacing = -14f;
            tile._label.enableAutoSizing = true;
            tile._label.fontSizeMin = Design.Caption * 0.7f;
            tile._label.fontSizeMax = tile._label.fontSize;

            if (toggle)
            {
                tile._check = UiBuilder.Image(content, "Check", Art.Disc, Color.white);
                tile._check.type = Image.Type.Simple;
                tile._check.rectTransform.sizeDelta = new Vector2(52f, 52f);
                tile._check.rectTransform.anchoredPosition = new Vector2(size.x * 0.5f - 40f, size.y * 0.5f - 40f);
                var tick = UiBuilder.Image(tile._check.rectTransform, "Tick", Icons.Check, SheetKit.Tinted(tint, Color.black, 0.2f));
                tick.type = Image.Type.Simple;
                tick.rectTransform.sizeDelta = new Vector2(34f, 34f);
            }

            button.Clicked += tile.OnClicked;
            tile.Paint();
            return tile;
        }

        void OnClicked()
        {
            if (!_toggle)
            {
                Clicked?.Invoke();
                return;
            }

            SetOn(!_on, animate: true);
            Changed?.Invoke(_on);
        }

        public void SetIcon(Sprite icon) => _glyph.sprite = icon;

        public void SetOn(bool on, bool animate)
        {
            _on = on;
            Paint();
            if (!animate || !_button.isActiveAndEnabled) return;

            // The medallion does a little flip, the tick pops: the tile answers the tap.
            _button.StartCoroutine(Flip());
            if (_on && _check != null)
                _button.StartCoroutine(Tween.Scale(_check.rectTransform, Vector3.zero, Vector3.one, 0.3f, Ease.OutBack, 0.08f));
        }

        IEnumerator Flip()
        {
            var rect = _medal.rectTransform;
            const float duration = 0.36f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rect.localScale = new Vector3(Mathf.Cos(k * Mathf.PI * 2f) * 0.2f + 0.8f + 0.2f * k, 1f + 0.12f * Mathf.Sin(k * Mathf.PI), 1f);
                yield return null;
            }

            rect.localScale = Vector3.one;
        }

        void Paint()
        {
            bool lit = !_toggle || _on;
            // Lit: the tile takes its own colour, deep enough that white reads on it. Off: the
            // sheet's surface, the medallion dimmed and its word quiet.
            // An action tile stays on the surface and lets its medallion carry the colour; a switch
            // that is on is its colour, deepened so white reads on it. Mixing the colour into the
            // surface instead turned gold into mustard and orange into brown.
            bool coloured = _toggle && _on;
            _fill.color = coloured ? SheetKit.Tinted(_tint, Color.black, 0.2f) : SheetKit.Lifted(Design.SurfaceGroup);
            _medal.color = !lit ? SheetKit.Tinted(Design.SurfaceControl, _tint, 0.25f)
                : coloured ? SheetKit.Tinted(_tint, Color.white, 0.22f) : SheetKit.Tinted(_tint, Color.white, 0.1f);
            _glyph.color = lit ? Color.white : Design.TextTertiary;
            _label.color = lit ? Color.white : Design.TextSecondary;
            if (_check != null) _check.gameObject.SetActive(_on);
        }
    }

    /// <summary>
    /// A full-screen catch for taps that land beside a sheet. A press and a release both outside
    /// <see cref="Exclude"/> count; a drag that started on the sheet and ended off it does not.
    /// </summary>
    public sealed class UiHit : MonoBehaviour, IPointerWidget
    {
        public RectTransform Rect { get; private set; }
        public bool Interactable { get; set; } = true;
        public RectTransform Exclude;

        public event Action Clicked;

        bool _pressedOutside;

        public static UiHit Create(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hit = go.AddComponent<UiHit>();
            hit.Rect = (RectTransform)go.transform;
            return hit;
        }

        bool Outside(Vector2 point) =>
            Exclude == null || !RectTransformUtility.RectangleContainsScreenPoint(Exclude, point, null);

        void OnEnable() => PointerRouter.Register(this);
        void OnDisable() => PointerRouter.Unregister(this);

        public void OnPress(Vector2 screenPoint) => _pressedOutside = Outside(screenPoint);
        public void OnDrag(Vector2 screenPoint) { }

        public void OnRelease(Vector2 screenPoint, bool inside)
        {
            if (_pressedOutside && Outside(screenPoint)) Clicked?.Invoke();
            _pressedOutside = false;
        }
    }

    /// <summary>The top strip of a sheet: pulled down, the sheet follows and closes past a point.</summary>
    public sealed class UiSheetDrag : MonoBehaviour, IPointerWidget
    {
        public RectTransform Rect { get; private set; }
        public bool Interactable { get; set; } = true;

        Sheet _sheet;
        float _startY;

        public static UiSheetDrag Create(RectTransform parent, Sheet sheet)
        {
            var go = new GameObject("DragStrip", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var drag = go.AddComponent<UiSheetDrag>();
            drag.Rect = (RectTransform)go.transform;
            drag._sheet = sheet;
            return drag;
        }

        void OnEnable() => PointerRouter.Register(this);
        void OnDisable() => PointerRouter.Unregister(this);

        public void OnPress(Vector2 screenPoint)
        {
            _startY = _sheet.LocalY(screenPoint);
            _sheet.DragBegin();
        }

        public void OnDrag(Vector2 screenPoint) => _sheet.DragTo(_startY - _sheet.LocalY(screenPoint));

        public void OnRelease(Vector2 screenPoint, bool inside)
        {
            _sheet.DragTo(_startY - _sheet.LocalY(screenPoint));
            _sheet.DragEnd();
        }
    }

    /// <summary>
    /// A segmented control: two or three choices in one pill, the chosen one under a thumb that
    /// slides to it. Replaces the two separate buttons the tabs used to be, which read as two
    /// actions rather than one choice.
    /// </summary>
    public sealed class UiSegmented : MonoBehaviour
    {
        RectTransform _rect;
        RectTransform _thumb;
        UiButton[] _segments;
        Image[] _dots;
        float _segmentWidth;
        int _index = -1;
        Coroutine _slide;

        public event Action<int> Changed;

        public RectTransform Rect => _rect;
        public int Index => _index;

        public static UiSegmented Create(RectTransform parent, string name, float width, params string[] labels)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var control = go.AddComponent<UiSegmented>();
            control.Build(width, Design.SegmentHeight, labels);
            return control;
        }

        void Build(float width, float height, string[] labels)
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(width, height);

            // A recessed track and a lit accent thumb: the choice is the brightest thing in it.
            UiBuilder.Panel(_rect, "Track", _rect.sizeDelta, SheetKit.Tinted(Design.SurfaceGroup, Color.black, 0.25f), height * 0.5f);

            const float inset = 10f;
            _segmentWidth = width / labels.Length;
            var thumbSize = new Vector2(_segmentWidth - inset * 2f, height - inset * 2f);
            float thumbRadius = thumbSize.y * 0.5f;

            _thumb = UiBuilder.Node(_rect, "Thumb");
            _thumb.sizeDelta = thumbSize;
            UiBuilder.Shadow(_thumb, "Shadow", thumbSize, thumbRadius, Design.E1,
                new Color(Design.AccentB.r * 0.45f, Design.AccentB.g * 0.45f, Design.AccentB.b * 0.6f));
            var thumbFill = UiBuilder.Image(_thumb, "Fill", Art.PanelGradient(thumbRadius), Design.AccentA);
            thumbFill.rectTransform.sizeDelta = thumbSize;
            UiBuilder.Hairline(_thumb, "Edge", thumbSize, thumbRadius, Color.white.WithAlpha(0.25f));

            _segments = new UiButton[labels.Length];
            _dots = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var segment = UiBuilder.Button(_rect, "Segment" + i, new Vector2(_segmentWidth, height), UiButton.Style.Bare,
                    labels[i], Design.Label);
                segment.Rect.anchoredPosition = new Vector2(SegmentX(i), 0f);
                segment.Label.characterSpacing = Design.TrackingLabel * 0.5f;
                segment.Label.enableAutoSizing = true;
                segment.Label.fontSizeMin = Design.Caption;
                segment.Label.fontSizeMax = Design.Label;
                segment.Label.textWrappingMode = TextWrappingModes.NoWrap;
                segment.Label.rectTransform.sizeDelta = new Vector2(_segmentWidth - Design.Space5 * 2f, height);
                segment.Clicked += () => Select(index, true, notify: true);
                _segments[i] = segment;

                var dot = SheetKit.Dot(segment.Content, new Vector2(_segmentWidth * 0.5f - Design.Space5 + 4f, 0f));
                dot.gameObject.SetActive(false);
                _dots[i] = dot;
            }

            Select(0, false, notify: false);
        }

        float SegmentX(int i) => -_rect.sizeDelta.x * 0.5f + _segmentWidth * (i + 0.5f);

        public void SetDot(int index, bool on) => _dots[index].gameObject.SetActive(on);

        public void Select(int index, bool animate, bool notify = false)
        {
            bool changed = index != _index;
            _index = index;

            for (int i = 0; i < _segments.Length; i++)
                _segments[i].Label.color = i == index ? Design.OnAccent : Design.TextSecondary;

            float x = SegmentX(index);
            if (_slide != null) StopCoroutine(_slide);
            if (animate && isActiveAndEnabled)
            {
                _slide = StartCoroutine(Tween.MoveAnchored(_thumb, _thumb.anchoredPosition, new Vector2(x, 0f), 0.32f, Ease.OutBackSoft));
                StartCoroutine(Tween.Punch(_segments[index].Content, 0.12f, 0.22f));
            }
            else
                _thumb.anchoredPosition = new Vector2(x, 0f);

            if (notify && changed) Changed?.Invoke(index);
        }
    }

    /// <summary>
    /// A vertical list that scrolls under the finger, with a fling and a soft stop at either end.
    /// The UI has no EventSystem, so this is the one scroll view: it claims the pointer like any
    /// widget, so what it holds should not be buttons.
    /// </summary>
    public sealed class UiScroll : MonoBehaviour, IPointerWidget
    {
        RectTransform _viewport;
        RectTransform _content;
        float _position;
        float _max;
        float _velocity;
        bool _dragging;
        float _pressY;
        float _pressPosition;
        float _lastY;
        float _lastTime;

        public RectTransform Rect => _viewport;
        public RectTransform Content => _content;
        public bool Interactable { get; set; } = true;

        public static UiScroll Create(RectTransform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var scroll = go.AddComponent<UiScroll>();
            scroll.Build(size);
            return scroll;
        }

        void Build(Vector2 size)
        {
            _viewport = (RectTransform)transform;
            _viewport.anchorMin = _viewport.anchorMax = _viewport.pivot = new Vector2(0.5f, 0.5f);
            _viewport.sizeDelta = size;
            gameObject.AddComponent<RectMask2D>();

            _content = UiBuilder.Node(_viewport, "Content");
            SheetKit.PinTop(_content, 0f);
            _content.sizeDelta = new Vector2(size.x, size.y);
        }

        public void SetContentHeight(float height)
        {
            _content.sizeDelta = new Vector2(_viewport.sizeDelta.x, height);
            _max = Mathf.Max(0f, height - _viewport.sizeDelta.y);
            ScrollTo(Mathf.Clamp(_position, 0f, _max));
        }

        public void ScrollTo(float position)
        {
            _velocity = 0f;
            _position = position;
            Apply();
        }

        void Apply() => _content.anchoredPosition = new Vector2(0f, _position);

        float LocalY(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPoint, null, out var local);
            return local.y;
        }

        void OnEnable() => PointerRouter.Register(this);

        void OnDisable()
        {
            PointerRouter.Unregister(this);
            _dragging = false;
        }

        public void OnPress(Vector2 screenPoint)
        {
            _dragging = true;
            _velocity = 0f;
            _pressY = _lastY = LocalY(screenPoint);
            _pressPosition = _position;
            _lastTime = Time.unscaledTime;
        }

        public void OnDrag(Vector2 screenPoint)
        {
            float y = LocalY(screenPoint);
            float target = _pressPosition + (y - _pressY);

            // Past either end the list gives at a third of the finger's pace.
            if (target < 0f) target *= 0.33f;
            else if (target > _max) target = _max + (target - _max) * 0.33f;
            _position = target;
            Apply();

            float dt = Time.unscaledTime - _lastTime;
            if (dt > 0.0001f)
            {
                _velocity = Mathf.Lerp(_velocity, (y - _lastY) / dt, 0.5f);
                _lastY = y;
                _lastTime = Time.unscaledTime;
            }
        }

        public void OnRelease(Vector2 screenPoint, bool inside) => _dragging = false;

        void Update()
        {
            if (_dragging) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (_position < 0f || _position > _max)
            {
                float edge = _position < 0f ? 0f : _max;
                _velocity = 0f;
                _position = Mathf.Lerp(_position, edge, 1f - Mathf.Exp(-14f * dt));
                if (Mathf.Abs(_position - edge) < 0.5f) _position = edge;
                Apply();
                return;
            }

            if (Mathf.Abs(_velocity) < 8f) return;

            _position += _velocity * dt;
            _velocity *= Mathf.Exp(-3.2f * dt);
            if (_position < 0f || _position > _max) _velocity *= 0.3f;
            Apply();
        }
    }

    /// <summary>
    /// The cascade a sheet's content arrives in: each part swells up from a little smaller and
    /// fades in, a beat after the one above it. Scale and alpha only — the layout is never
    /// touched, so an entrance cut short (a sheet reopened mid-way) cannot leave a part out of
    /// place. What a part rests at (a greyed-out button's alpha) is remembered, not guessed.
    /// </summary>
    public static class Cascade
    {
        const float Step = 0.045f;
        const float Duration = 0.36f;

        sealed class Rest : MonoBehaviour
        {
            public float Alpha = 1f;
            public bool Running;
        }

        public static void Children(MonoBehaviour host, RectTransform parent, float delay)
        {
            var items = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child != null && child.gameObject.activeSelf) items.Add(child);
            }

            // Top to bottom, whatever order they were built in.
            items.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
            for (int n = 0; n < items.Count; n++)
                host.StartCoroutine(In(items[n], delay + Mathf.Min(n, 9) * Step));
        }

        public static IEnumerator In(RectTransform item, float delay)
        {
            if (item == null) yield break;
            var group = item.GetComponent<CanvasGroup>();
            if (group == null) group = item.gameObject.AddComponent<CanvasGroup>();
            var rest = item.GetComponent<Rest>();
            if (rest == null) rest = item.gameObject.AddComponent<Rest>();
            if (!rest.Running) rest.Alpha = group.alpha > 0.01f ? group.alpha : 1f;
            rest.Running = true;

            group.alpha = 0f;
            item.localScale = Vector3.one * 0.86f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
            {
                if (item == null) yield break;
                float k = t / Duration;
                float s = Mathf.LerpUnclamped(0.86f, 1f, Ease.OutBack(k));
                item.localScale = new Vector3(s, s, 1f);
                group.alpha = rest.Alpha * Ease.OutQuad(Mathf.Min(1f, k * 1.8f));
                yield return null;
            }

            Finish(item);
        }

        /// <summary>Puts every part of a body back at rest, for a sheet shown without its entrance.</summary>
        public static void Settle(RectTransform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
                Finish(parent.GetChild(i) as RectTransform);
        }

        static void Finish(RectTransform item)
        {
            if (item == null) return;
            var rest = item.GetComponent<Rest>();
            if (rest == null) return;
            item.localScale = Vector3.one;
            var group = item.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = rest.Alpha;
            rest.Running = false;
        }
    }
}
