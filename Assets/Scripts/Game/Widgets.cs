using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// A button that sits on a blurred ambient shadow and sinks slightly when pressed.
    /// The press is a scale and a shadow change, not a downward offset onto a hard lip.
    /// </summary>
    public sealed class UiButton : MonoBehaviour, IPointerWidget
    {
        public enum Style
        {
            /// <summary>The one action a screen wants you to take. Accent gradient.</summary>
            Primary,

            /// <summary>Everything else. Raised surface with a hairline.</summary>
            Secondary,

            /// <summary>Square icon-only control, same treatment as Secondary.</summary>
            Icon,

            /// <summary>
            /// No surface of its own: a row of a list, a segment, a tile on a card. It still sinks
            /// when pressed, and <see cref="SetHighlighted"/> gives it a fill.
            /// </summary>
            Bare,

            /// <summary>
            /// A small disc with a glyph, inside a full touch target — a sheet's close button. The
            /// hit area stays 48dp; only what is drawn is smaller.
            /// </summary>
            Round
        }

        RectTransform _rect;
        RectTransform _content;
        Image _shadow;
        Image _fill;
        TextMeshProUGUI _label;
        Image _icon;

        Coroutine _press;
        float _shadowAlpha;
        Color _restFill;
        CanvasGroup _group;

        public event Action Clicked;

        /// <summary>Swaps the fill colour, for a button that stays "on" — a power waiting for its target.</summary>
        public void SetHighlighted(bool on, Color color)
        {
            if (_fill != null) _fill.color = on ? color : _restFill;
        }

        /// <summary>Greys the button out and stops it taking presses. Distinct from hiding it.</summary>
        public void SetEnabled(bool enabled)
        {
            Interactable = enabled;
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = enabled ? 1f : 0.35f;
        }

        public Image Icon => _icon;

        /// <summary>The surface, for a button whose colour says something — a theme's own ground.</summary>
        public Image Fill => _fill;

        /// <summary>Changes the colour the button rests at, so a later highlight returns to it.</summary>
        public void SetRestColor(Color color)
        {
            _restFill = color;
            if (_fill != null) _fill.color = color;
        }

        /// <summary>The part that sinks on press. Custom content added here moves with the button.</summary>
        public RectTransform Content => _content;

        public RectTransform Rect => _rect;
        public bool Interactable { get; set; } = true;
        public TextMeshProUGUI Label => _label;

        public static UiButton Create(RectTransform parent, string name, Vector2 size, Style style,
            string label = null, float fontSize = Design.Headline, Sprite icon = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var button = go.AddComponent<UiButton>();
            button.Build(size, style, label, fontSize, icon);
            return button;
        }

        void Build(Vector2 size, Style style, string label, float fontSize, Sprite icon)
        {
            _rect = GetComponent<RectTransform>();
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = size;

            float radius = style == Style.Icon ? Design.RadiusMd : Mathf.Min(Design.RadiusLg, size.y * 0.42f);
            bool flat = style == Style.Bare || style == Style.Round;

            // Shadow first so it renders beneath everything else.
            if (!flat)
            {
                var elevation = style == Style.Primary ? Design.E2 : Design.E1;

                // The primary button casts a darkened version of its own colour, so the glow belongs to
                // the button instead of looking like a second violet shape behind it.
                var shadowTint = style == Style.Primary
                    ? new Color(Design.AccentB.r * 0.45f, Design.AccentB.g * 0.45f, Design.AccentB.b * 0.6f)
                    : Color.black;
                _shadow = UiBuilder.Shadow(_rect, "Shadow", size, radius, elevation, shadowTint);
                _shadowAlpha = _shadow.color.a;
            }

            _content = UiBuilder.Node(_rect, "Content");
            _content.sizeDelta = size;

            switch (style)
            {
                case Style.Primary:
                    _fill = UiBuilder.Image(_content, "Fill", Art.PanelGradient(radius), Design.AccentA);
                    _fill.rectTransform.sizeDelta = size;
                    break;

                case Style.Bare:
                    _fill = UiBuilder.Image(_content, "Fill", Art.Panel(Mathf.Min(Design.RadiusMd, size.y * 0.42f)), Color.clear);
                    _fill.rectTransform.sizeDelta = size;
                    break;

                case Style.Round:
                    // A small plastic button of its own, not a grey dot: a soft shadow and a lit disc.
                    var roundShadow = UiBuilder.Image(_content, "Shadow", Art.Shadow(Design.CloseDisc * 0.5f, 24f), Color.black.WithAlpha(0.3f));
                    roundShadow.type = Image.Type.Simple;
                    float roundPad = Art.ShadowPad(24f);
                    roundShadow.rectTransform.sizeDelta = new Vector2(Design.CloseDisc + roundPad * 2f, Design.CloseDisc + roundPad * 2f);
                    roundShadow.rectTransform.anchoredPosition = new Vector2(0f, -6f);
                    _fill = UiBuilder.Image(_content, "Fill", Art.Node, SheetKit.Tinted(Design.SurfaceControl, Color.white, 0.14f));
                    _fill.type = Image.Type.Simple;
                    _fill.rectTransform.sizeDelta = new Vector2(Design.CloseDisc, Design.CloseDisc);
                    break;

                default:
                    // Soft plastic, lit from the top like the blocks, with a light lip along the top.
                    _fill = UiBuilder.Image(_content, "Fill", Art.PanelGradient(radius), SheetKit.Lifted(Design.SurfaceButton));
                    _fill.rectTransform.sizeDelta = size;
                    UiBuilder.Hairline(_content, "Hairline", size, radius, Color.white.WithAlpha(0.14f));
                    break;
            }

            _restFill = _fill.color;

            if (icon != null)
            {
                var iconColor = style == Style.Primary ? Design.OnAccent : Design.TextPrimary;
                _icon = UiBuilder.Image(_content, "Icon", icon, iconColor);
                _icon.type = Image.Type.Simple;

                float glyph = style == Style.Round
                    ? Design.CloseDisc * 0.42f
                    : Mathf.Min(size.x, size.y) * (string.IsNullOrEmpty(label) ? Design.GlyphFill : 0.42f);
                _icon.rectTransform.sizeDelta = new Vector2(glyph, glyph);

                if (!string.IsNullOrEmpty(label))
                    _icon.rectTransform.anchoredPosition = new Vector2(-size.x * 0.5f + glyph * 0.5f + Design.Space4, 0f);
            }

            if (!string.IsNullOrEmpty(label))
            {
                var textColor = style == Style.Primary ? Design.OnAccent : Design.TextPrimary;
                _label = UiBuilder.Label(_content, "Label", label, fontSize, textColor, Design.FontDisplay);
                _label.rectTransform.sizeDelta = new Vector2(size.x, size.y);
                if (_icon != null) _label.rectTransform.anchoredPosition = new Vector2(Design.Space3, 0f);
            }
        }

        void OnEnable() => PointerRouter.Register(this);

        void OnDisable()
        {
            PointerRouter.Unregister(this);
            Release();
        }

        public void OnPress(Vector2 screenPoint) => Animate(0.94f, 0.45f);

        public void OnDrag(Vector2 screenPoint) { }

        public void OnRelease(Vector2 screenPoint, bool inside)
        {
            Release();
            if (inside) Clicked?.Invoke();
        }

        void Release() => Animate(1f, 1f);

        void Animate(float scale, float shadowFactor)
        {
            if (_press != null) StopCoroutine(_press);
            if (!gameObject.activeInHierarchy)
            {
                _content.localScale = Vector3.one * scale;
                return;
            }

            _press = StartCoroutine(PressRoutine(scale, shadowFactor));
        }

        IEnumerator PressRoutine(float targetScale, float shadowFactor)
        {
            var from = _content.localScale;
            var to = Vector3.one * targetScale;

            float fromAlpha = _shadow != null ? _shadow.color.a : 0f;
            float toAlpha = _shadowAlpha * shadowFactor;

            // Down fast and flat; back up on a spring, so letting go feels like a pop.
            bool release = targetScale >= 1f;
            float duration = release ? 0.26f : 0.08f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = release ? Ease.OutBack(t / duration) : Ease.OutQuad(t / duration);
                _content.localScale = Vector3.LerpUnclamped(from, to, k);
                if (_shadow != null) _shadow.color = _shadow.color.WithAlpha(Mathf.Lerp(fromAlpha, toAlpha, k));
                yield return null;
            }

            _content.localScale = to;
            if (_shadow != null) _shadow.color = _shadow.color.WithAlpha(toAlpha);
            _press = null;
        }
    }

    /// <summary>A pill slider. Pressing anywhere on the track jumps the handle there.</summary>
    public sealed class UiSlider : MonoBehaviour, IPointerWidget
    {
        RectTransform _rect;
        RectTransform _track;
        Image _fill;
        RectTransform _handle;

        float _value;
        float _trackWidth;

        public event Action<float> ValueChanged;

        public RectTransform Rect => _rect;
        public bool Interactable { get; set; } = true;

        public float Value
        {
            get => _value;
            set { _value = Mathf.Clamp01(value); Apply(); }
        }

        public static UiSlider Create(RectTransform parent, string name, float width, float height, float value)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var slider = go.AddComponent<UiSlider>();
            slider.Build(width, height, value);
            return slider;
        }

        void Build(float width, float height, float value)
        {
            _rect = GetComponent<RectTransform>();
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            // Taller than the visible track so it is comfortable under a thumb.
            _rect.sizeDelta = new Vector2(width, Mathf.Max(height * 3f, Design.TouchTarget));

            _trackWidth = width;
            float radius = height * 0.5f;

            // A recessed groove, a lit accent fill, and a plastic knob with an accent heart.
            var track = UiBuilder.Panel(_rect, "Track", new Vector2(width, height), SheetKit.Tinted(Design.SurfaceControl, Color.black, 0.25f), radius);
            _track = track.rectTransform;

            _fill = UiBuilder.Image(_track, "Fill", Art.PanelGradient(radius), SheetKit.Tinted(Design.AccentA, Color.white, 0.1f));
            _fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _fill.rectTransform.anchoredPosition = Vector2.zero;

            float knob = height * 2.3f;
            UiBuilder.Shadow(_rect, "HandleShadow", new Vector2(knob, knob), knob * 0.5f, Design.E1);

            var handle = UiBuilder.Image(_rect, "Handle", Art.Node, Color.white);
            handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(knob, knob);
            _handle = handle.rectTransform;
            var heart = UiBuilder.Image(_handle, "Heart", Art.Disc, Design.AccentA);
            heart.type = Image.Type.Simple;
            heart.rectTransform.sizeDelta = new Vector2(knob * 0.34f, knob * 0.34f);

            _value = Mathf.Clamp01(value);
            Apply();
        }

        void Apply()
        {
            _fill.rectTransform.sizeDelta = new Vector2(_trackWidth * _value, _track.sizeDelta.y);
            _handle.anchoredPosition = new Vector2(-_trackWidth * 0.5f + _trackWidth * _value, 0f);

            var shadow = _rect.Find("HandleShadow") as RectTransform;
            if (shadow != null)
                shadow.anchoredPosition = new Vector2(_handle.anchoredPosition.x, -Design.E1.OffsetY);
        }

        void OnEnable() => PointerRouter.Register(this);
        void OnDisable() => PointerRouter.Unregister(this);

        public void OnPress(Vector2 screenPoint)
        {
            // The knob swells under the thumb while it is held.
            _handle.localScale = Vector3.one * 1.14f;
            SetFromPointer(screenPoint);
        }

        public void OnDrag(Vector2 screenPoint) => SetFromPointer(screenPoint);

        public void OnRelease(Vector2 screenPoint, bool inside)
        {
            SetFromPointer(screenPoint);
            if (isActiveAndEnabled) StartCoroutine(Tween.Scale(_handle, _handle.localScale, Vector3.one, 0.24f, Ease.OutBack));
            else _handle.localScale = Vector3.one;
        }

        void SetFromPointer(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, null, out var local))
                return;

            float v = Mathf.Clamp01((local.x + _trackWidth * 0.5f) / _trackWidth);
            if (Mathf.Approximately(v, _value)) return;

            _value = v;
            Apply();
            ValueChanged?.Invoke(_value);
        }
    }

    /// <summary>A pill toggle whose knob slides between the two ends.</summary>
    public sealed class UiToggle : MonoBehaviour, IPointerWidget
    {
        RectTransform _rect;
        Image _pill;
        Image _tick;
        RectTransform _knob;
        RectTransform _knobShadow;

        bool _on;
        float _width;
        Coroutine _slide;

        public event Action<bool> ValueChanged;

        public RectTransform Rect => _rect;
        public bool Interactable { get; set; } = true;

        public bool IsOn
        {
            get => _on;
            set { _on = value; Apply(instant: true); }
        }

        public static UiToggle Create(RectTransform parent, string name, float width, float height, bool value)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var toggle = go.AddComponent<UiToggle>();
            toggle.Build(width, height, value);
            return toggle;
        }

        void Build(float width, float height, bool value)
        {
            _rect = GetComponent<RectTransform>();
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.sizeDelta = new Vector2(width, Mathf.Max(height, Design.TouchTarget));

            _width = width;

            // A chunky game switch: a lit pill, mint when on, with a tick on the side the knob left;
            // a plastic knob that springs across.
            _pill = UiBuilder.Image(_rect, "Pill", Art.PanelGradient(height * 0.5f), Design.SurfaceControl);
            _pill.rectTransform.sizeDelta = new Vector2(width, height);

            _tick = UiBuilder.Image(_rect, "Tick", Icons.Check, Color.white);
            _tick.type = Image.Type.Simple;
            _tick.rectTransform.sizeDelta = new Vector2(height * 0.46f, height * 0.46f);
            _tick.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + height * 0.52f, 0f);

            float knob = height - 12f;
            _knobShadow = UiBuilder.Shadow(_rect, "KnobShadow", new Vector2(knob, knob), knob * 0.5f, Design.E1).rectTransform;

            var handle = UiBuilder.Image(_rect, "Knob", Art.Node, Color.white);
            handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(knob, knob);
            _knob = handle.rectTransform;

            _on = value;
            Apply(instant: true);
        }

        float Travel => _width * 0.5f - _knob.sizeDelta.x * 0.5f - 6f;

        void Apply(bool instant)
        {
            _pill.color = _on ? SheetKit.Tinted(Design.Mint, Color.white, 0.08f) : SheetKit.Tinted(Design.SurfaceControl, Color.black, 0.18f);
            _tick.gameObject.SetActive(_on);

            float x = _on ? Travel : -Travel;
            if (instant)
            {
                _knob.anchoredPosition = new Vector2(x, 0f);
                _knobShadow.anchoredPosition = new Vector2(x, -Design.E1.OffsetY);
                return;
            }

            if (_slide != null) StopCoroutine(_slide);
            _slide = StartCoroutine(SlideRoutine(x));
        }

        IEnumerator SlideRoutine(float targetX)
        {
            float from = _knob.anchoredPosition.x;
            const float duration = 0.3f;
            if (_tick.gameObject.activeSelf) StartCoroutine(Tween.Scale(_tick.rectTransform, Vector3.zero, Vector3.one, 0.26f, Ease.OutBack, 0.08f));

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float x = Mathf.LerpUnclamped(from, targetX, Ease.OutBackSoft(k));
                // Stretched along the way, round again at rest: the squash of a quick move.
                float stretch = 1f + 0.22f * Mathf.Sin(Mathf.Clamp01(k * 1.6f) * Mathf.PI);
                _knob.localScale = new Vector3(stretch, 2f - stretch, 1f);
                _knob.anchoredPosition = new Vector2(x, 0f);
                _knobShadow.anchoredPosition = new Vector2(x, -Design.E1.OffsetY);
                yield return null;
            }

            _knob.anchoredPosition = new Vector2(targetX, 0f);
            _knob.localScale = Vector3.one;
            _knobShadow.anchoredPosition = new Vector2(targetX, -Design.E1.OffsetY);
            _slide = null;
        }

        void OnEnable() => PointerRouter.Register(this);
        void OnDisable() => PointerRouter.Unregister(this);

        public void OnPress(Vector2 screenPoint) { }
        public void OnDrag(Vector2 screenPoint) { }

        public void OnRelease(Vector2 screenPoint, bool inside)
        {
            if (!inside) return;

            _on = !_on;
            Apply(instant: false);
            ValueChanged?.Invoke(_on);
        }
    }
}
