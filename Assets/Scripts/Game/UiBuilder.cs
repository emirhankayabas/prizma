using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Helpers for assembling the UI from code against the <see cref="Design"/> tokens.
    /// The whole hierarchy is built at runtime, so there is no scene wiring to keep in sync.
    /// </summary>
    public static class UiBuilder
    {
        public static RectTransform Child(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        public static RectTransform Node(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        public static Image Image(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A filled squircle surface.</summary>
        public static Image Panel(RectTransform parent, string name, Vector2 size, Color color, float radius)
        {
            var image = Image(parent, name, Art.Panel(radius), color);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        /// <summary>A hairline squircle outline, used to lift surfaces off a dark background.</summary>
        public static Image Hairline(RectTransform parent, string name, Vector2 size, float radius, Color? color = null)
        {
            var image = Image(parent, name, Art.Stroke(radius), color ?? Design.Hairline);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        /// <summary>
        /// A blurred ambient shadow behind an element. Add it before the element so it renders
        /// underneath, and give it the element's own size — the sprite handles the spread.
        /// </summary>
        public static Image Shadow(RectTransform parent, string name, Vector2 size, float radius,
            Design.Elevation elevation, Color? tint = null)
        {
            var color = (tint ?? Color.black).WithAlpha(elevation.Alpha);
            var image = Image(parent, name, Art.Shadow(radius, elevation.Spread), color);

            float pad = Art.ShadowPad(elevation.Spread);
            image.rectTransform.sizeDelta = size + new Vector2(pad * 2f, pad * 2f);
            image.rectTransform.anchoredPosition = new Vector2(0f, -elevation.OffsetY);
            return image;
        }

        /// <summary>The soft shadow of a disc <paramref name="diameter"/> across — round, where <see cref="Shadow"/> is a squircle.</summary>
        public static Image DiscShadow(RectTransform parent, string name, float diameter, float spread, float alpha,
            float offsetY, Color? tint = null)
        {
            var image = Image(parent, name, Art.DiscShadow(diameter, spread), (tint ?? Color.black).WithAlpha(alpha));
            image.type = UnityEngine.UI.Image.Type.Simple;
            float size = diameter + Art.ShadowPad(spread) * 2f;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition = new Vector2(0f, -offsetY);
            return image;
        }

        public static TextMeshProUGUI Label(RectTransform parent, string name, string content, float fontSize,
            Color color, TMP_FontAsset font = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            float tracking = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(960f, fontSize * 1.5f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font ?? Design.FontDisplay;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = tracking;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static UiButton Button(RectTransform parent, string name, Vector2 size, UiButton.Style style,
            string label = null, float fontSize = Design.Headline, Sprite icon = null)
            => UiButton.Create(parent, name, size, style, label, fontSize, icon);

        /// <summary>
        /// A soft drop shadow on text. Kept very subtle — heavy outlines are the main thing that
        /// dates game typography, so display text leans on weight and contrast instead.
        /// </summary>
        public static void TextShadow(TMP_Text text, float alpha = 0.35f, float offsetY = -0.28f, float softness = 0.35f)
        {
            var material = text.fontMaterial;
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, alpha));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offsetY);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        }

        /// <summary>
        /// Stretches over the parent and then back out over the safe-area inset, so a full-screen
        /// dim actually reaches the screen edges. Pages are inset inside the notch and the gesture
        /// bar; a scrim that stopped at that inset left a bright strip along the bottom of every
        /// dimmed screen.
        /// </summary>
        public static void StretchFullScreen(RectTransform rect)
        {
            Stretch(rect);

            var insets = AppController.SafeInsets;
            rect.offsetMin = new Vector2(-insets.x, -insets.y);
            rect.offsetMax = new Vector2(insets.z, insets.w);
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A vertical two-stop gradient stretched over its parent.</summary>
        public static Image Gradient(RectTransform parent, string name, Color top, Color bottom)
        {
            var image = Image(parent, name, GradientSprite(top, bottom), Color.white);
            image.type = UnityEngine.UI.Image.Type.Simple;
            Stretch(image.rectTransform);
            return image;
        }

        static Sprite GradientSprite(Color top, Color bottom)
        {
            const int height = 128;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "GradientTex",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < height; y++)
            {
                float k = y / (float)(height - 1);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, k));
            }

            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
            sprite.name = "GradientSprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
