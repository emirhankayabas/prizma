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
            Icon
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

            // Shadow first so it renders beneath everything else.
            var elevation = style == Style.Primary ? Design.E2 : Design.E1;

            // The primary button casts a darkened version of its own colour, so the glow belongs to
            // the button instead of looking like a second violet shape behind it.
            var shadowTint = style == Style.Primary
                ? new Color(Design.AccentB.r * 0.45f, Design.AccentB.g * 0.45f, Design.AccentB.b * 0.6f)
                : Color.black;
            _shadow = UiBuilder.Shadow(_rect, "Shadow", size, radius, elevation, shadowTint);
            _shadowAlpha = _shadow.color.a;

            _content = UiBuilder.Node(_rect, "Content");
            _content.sizeDelta = size;

            switch (style)
            {
                case Style.Primary:
                    _fill = UiBuilder.Image(_content, "Fill", Art.PanelGradient(radius), Design.AccentA);
                    _fill.rectTransform.sizeDelta = size;
                    break;

                default:
                    _fill = UiBuilder.Image(_content, "Fill", Art.Panel(radius), Design.SurfaceButton);
                    _fill.rectTransform.sizeDelta = size;
                    UiBuilder.Hairline(_content, "Hairline", size, radius);
                    break;
            }

            _restFill = _fill.color;

            if (icon != null)
            {
                var iconColor = style == Style.Primary ? Design.OnAccent : Design.TextPrimary;
                _icon = UiBuilder.Image(_content, "Icon", icon, iconColor);
                _icon.type = Image.Type.Simple;

                float glyph = Mathf.Min(size.x, size.y) * (string.IsNullOrEmpty(label) ? 0.44f : 0.38f);
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

        public void OnPress(Vector2 screenPoint) => Animate(0.955f, 0.45f);

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

            float fromAlpha = _shadow.color.a;
            float toAlpha = _shadowAlpha * shadowFactor;

            const float duration = 0.09f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Ease.OutQuad(t / duration);
                _content.localScale = Vector3.LerpUnclamped(from, to, k);
                _shadow.color = _shadow.color.WithAlpha(Mathf.Lerp(fromAlpha, toAlpha, k));
                yield return null;
            }

            _content.localScale = to;
            _shadow.color = _shadow.color.WithAlpha(toAlpha);
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

            var track = UiBuilder.Panel(_rect, "Track", new Vector2(width, height), Design.SurfaceTrack, radius);
            _track = track.rectTransform;

            _fill = UiBuilder.Image(_track, "Fill", Art.Panel(radius), Design.AccentA);
            _fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _fill.rectTransform.anchoredPosition = Vector2.zero;

            float knob = height * 2.3f;
            UiBuilder.Shadow(_rect, "HandleShadow", new Vector2(knob, knob), knob * 0.5f, Design.E1);

            var handle = UiBuilder.Image(_rect, "Handle", Art.Disc, Color.white);
            handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(knob, knob);
            _handle = handle.rectTransform;

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

        public void OnPress(Vector2 screenPoint) => SetFromPointer(screenPoint);
        public void OnDrag(Vector2 screenPoint) => SetFromPointer(screenPoint);
        public void OnRelease(Vector2 screenPoint, bool inside) => SetFromPointer(screenPoint);

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

            _pill = UiBuilder.Panel(_rect, "Pill", new Vector2(width, height), Design.SurfaceTrack, height * 0.5f);

            float knob = height - 12f;
            _knobShadow = UiBuilder.Shadow(_rect, "KnobShadow", new Vector2(knob, knob), knob * 0.5f, Design.E1).rectTransform;

            var handle = UiBuilder.Image(_rect, "Knob", Art.Disc, Color.white);
            handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(knob, knob);
            _knob = handle.rectTransform;

            _on = value;
            Apply(instant: true);
        }

        float Travel => _width * 0.5f - _knob.sizeDelta.x * 0.5f - 6f;

        void Apply(bool instant)
        {
            _pill.color = _on ? Design.AccentA : Design.SurfaceTrack;

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
            const float duration = 0.16f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float x = Mathf.Lerp(from, targetX, Ease.OutCubic(t / duration));
                _knob.anchoredPosition = new Vector2(x, 0f);
                _knobShadow.anchoredPosition = new Vector2(x, -Design.E1.OffsetY);
                yield return null;
            }

            _knob.anchoredPosition = new Vector2(targetX, 0f);
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
