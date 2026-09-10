using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Every sprite in the game, generated procedurally on first use.
    /// Shapes are squircles rather than rounded rectangles, and depth comes from blurred ambient
    /// shadows rather than offset lips — the two things that most separate a current mobile UI
    /// from a 2010-era one. Sprites are drawn white so one tint per colour produces the whole set.
    /// </summary>
    public static class Art
    {
        const int Super = 2; // drawn at 2x then box-filtered down

        static Sprite _block;
        static Sprite _cell;
        static Sprite _softSquare;
        static Sprite _softCircle;
        static Sprite _pool;
        static Sprite _sparkle;
        static Sprite _disc;
        static Sprite _grain;

        static readonly Dictionary<int, Sprite> Panels = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> Strokes = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> Shadows = new Dictionary<int, Sprite>();

        // ------------------------------------------------------------------ surfaces

        /// <summary>A filled squircle, nine-sliced. Radius is in canvas units.</summary>
        public static Sprite Panel(float radius)
        {
            int key = Mathf.RoundToInt(radius);
            if (!Panels.TryGetValue(key, out var sprite))
            {
                sprite = BuildPanel(key);
                Panels[key] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// A squircle with a vertical light ramp baked in. Tinted with one colour it reads as a
        /// gradient fill, which keeps a button from looking like a flat slab without a second draw.
        /// </summary>
        public static Sprite PanelGradient(float radius)
        {
            int key = -Mathf.RoundToInt(radius) - 1; // negative keys keep gradients out of the flat cache
            if (!Panels.TryGetValue(key, out var sprite))
            {
                sprite = BuildPanelGradient(Mathf.RoundToInt(radius));
                Panels[key] = sprite;
            }

            return sprite;
        }

        /// <summary>A hairline squircle outline, nine-sliced.</summary>
        public static Sprite Stroke(float radius)
        {
            int key = Mathf.RoundToInt(radius);
            if (!Strokes.TryGetValue(key, out var sprite))
            {
                sprite = BuildStroke(key);
                Strokes[key] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// A blurred squircle used as an ambient shadow. The sprite extends
        /// <see cref="ShadowPad"/> beyond the element on every side.
        /// </summary>
        public static Sprite Shadow(float radius, float spread)
        {
            int key = Mathf.RoundToInt(radius) * 1000 + Mathf.RoundToInt(spread);
            if (!Shadows.TryGetValue(key, out var sprite))
            {
                sprite = BuildShadow(radius, spread);
                Shadows[key] = sprite;
            }

            return sprite;
        }

        public static float ShadowPad(float spread) => spread + 20f;

        // ------------------------------------------------------------------ pieces

        public static Sprite Block
        {
            get { if (_block == null) _block = BuildBlock(); return _block; }
        }

        public static Sprite Cell
        {
            get { if (_cell == null) _cell = BuildCell(); return _cell; }
        }

        // ------------------------------------------------------------------ effects

        public static Sprite SoftSquare
        {
            get { if (_softSquare == null) _softSquare = BuildSoftSquare(); return _softSquare; }
        }

        public static Sprite SoftCircle
        {
            get { if (_softCircle == null) _softCircle = BuildSoftCircle(); return _softCircle; }
        }

        /// <summary>
        /// A radial wash with a much wider falloff than <see cref="SoftCircle"/>. Used for the
        /// backdrop's colour pools, where the tight core of a glow sprite reads as a blob.
        /// </summary>
        public static Sprite Pool
        {
            get { if (_pool == null) _pool = BuildPool(); return _pool; }
        }

        public static Sprite Sparkle
        {
            get { if (_sparkle == null) _sparkle = BuildSparkle(); return _sparkle; }
        }

        public static Sprite Disc
        {
            get { if (_disc == null) _disc = BuildDisc(); return _disc; }
        }

        /// <summary>Tileable noise. Laid over gradients at a few percent alpha to stop banding.</summary>
        public static Sprite Grain
        {
            get { if (_grain == null) _grain = BuildGrain(); return _grain; }
        }

        // ------------------------------------------------------------------ builders

        static Sprite BuildPanel(int radius)
        {
            int size = Mathf.Max(96, radius * 3) * Super;
            float r = radius * Super;

            var raster = new Raster(size, size);
            raster.FillSquircle(0, 0, size, size, r, Color.white);

            return raster.Downsample(Super).ToSprite($"Panel{radius}", (radius + 3f) / (size / Super));
        }

        static Sprite BuildPanelGradient(int radius)
        {
            int size = Mathf.Max(96, radius * 3) * Super;
            float r = radius * Super;

            var raster = new Raster(size, size);
            raster.Paint((x, y) =>
            {
                if (!Raster.SquircleInside(x, y, size, size, r, 4.5f)) return Color.clear;

                float v = y / size; // 0 bottom, 1 top
                float lum = Mathf.Lerp(0.74f, 1f, v);
                return new Color(lum, lum, lum, 1f);
            });

            // Vertically sliced only: stretching the middle horizontally keeps the ramp intact,
            // so the sprite is authored large enough to be used at its natural height.
            return raster.Downsample(Super).ToSprite($"PanelGrad{radius}", (radius + 3f) / (size / Super));
        }

        static Sprite BuildStroke(int radius)
        {
            int size = Mathf.Max(96, radius * 3) * Super;
            float r = radius * Super;

            var raster = new Raster(size, size);
            raster.StrokeSquircle(0, 0, size, size, r, 2f * Super, Color.white);

            return raster.Downsample(Super).ToSprite($"Stroke{radius}", (radius + 3f) / (size / Super));
        }

        static Sprite BuildShadow(float radius, float spread)
        {
            float pad = ShadowPad(spread);
            int size = Mathf.RoundToInt((radius + pad) * 2f + 48f);

            var raster = new Raster(size, size);
            float inset = pad;
            raster.FillSquircle(inset, inset, size - inset * 2f, size - inset * 2f, radius, Color.white);
            raster.BlurAlpha(Mathf.Max(1, Mathf.RoundToInt(spread * 0.42f)));

            return raster.ToSprite($"Shadow{radius}_{spread}", (radius + pad) / size);
        }

        /// <summary>
        /// A soft-plastic tile: gentle vertical gradient, a crisp highlight along the top edge and
        /// a shadow along the bottom. No chunky bevel — the depth cue is light direction, not a frame.
        /// </summary>
        static Sprite BuildBlock()
        {
            const int Base = 160;
            int s = Base * Super;
            var raster = new Raster(s, s);

            float radius = s * 0.235f;
            float band = s * 0.085f;

            float glossX = s * 0.36f;
            float glossY = s * 0.74f;
            float glossR = s * 0.34f;

            raster.Paint((x, y) =>
            {
                if (!Raster.SquircleInside(x, y, s, s, radius, 4.5f)) return Color.clear;

                float v = y / s; // 0 at the bottom, 1 at the top
                float lum = Mathf.Lerp(0.70f, 0.95f, v);

                float fromTop = s - y;
                if (fromTop < band)
                    lum = Mathf.Lerp(1f, lum, Ease.OutQuad(fromTop / band));

                if (y < band)
                    lum = Mathf.Lerp(0.46f, lum, Ease.OutQuad(y / band));

                float gd = Mathf.Sqrt((x - glossX) * (x - glossX) + (y - glossY) * (y - glossY));
                float gloss = Mathf.Clamp01(1f - gd / glossR);
                lum += gloss * gloss * 0.07f;

                lum = Mathf.Clamp01(lum);
                return new Color(lum, lum, lum, 1f);
            });

            return raster.Downsample(Super).ToSprite("Block", 0.34f);
        }

        /// <summary>An empty slot: a barely-there squircle, darker at the top so it reads as a recess.</summary>
        static Sprite BuildCell()
        {
            const int Base = 160;
            int s = Base * Super;
            var raster = new Raster(s, s);

            float radius = s * 0.215f;

            raster.Paint((x, y) =>
            {
                if (!Raster.SquircleInside(x, y, s, s, radius, 4.5f)) return Color.clear;

                float v = y / s;
                float lum = Mathf.Lerp(1.06f, 0.9f, v);
                return new Color(Mathf.Clamp01(lum), Mathf.Clamp01(lum), Mathf.Clamp01(lum), 1f);
            });

            return raster.Downsample(Super).ToSprite("Cell", 0.32f);
        }

        static Sprite BuildSoftSquare()
        {
            const int size = 128;
            var raster = new Raster(size, size);
            float half = size * 0.5f;

            raster.Paint((x, y) =>
            {
                float dx = Mathf.Abs(x - half) / half;
                float dy = Mathf.Abs(y - half) / half;
                float a = Mathf.Clamp01(1f - Mathf.Max(dx, dy));
                a *= a;
                return new Color(1f, 1f, 1f, a);
            });

            return raster.ToSprite("SoftSquare");
        }

        static Sprite BuildSoftCircle()
        {
            const int size = 128;
            var raster = new Raster(size, size);
            float half = size * 0.5f;

            raster.Paint((x, y) =>
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a; // tight core, long falloff
                return new Color(1f, 1f, 1f, a);
            });

            return raster.ToSprite("SoftCircle");
        }

        static Sprite BuildPool()
        {
            const int size = 192;
            var raster = new Raster(size, size);
            float half = size * 0.5f;

            raster.Paint((x, y) =>
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                // A gentle cosine falloff: broad in the middle, fading to nothing at the rim.
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Sin(a * Mathf.PI * 0.5f);
                a *= a;
                return new Color(1f, 1f, 1f, a);
            });

            return raster.ToSprite("Pool");
        }

        static Sprite BuildSparkle()
        {
            const int Base = 64;
            int s = Base * Super;
            var raster = new Raster(s, s);
            float c = s * 0.5f;

            var points = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * 0.25f * i;
                float r = i % 2 == 0 ? s * 0.5f : s * 0.072f;
                points[i] = new Vector2(c + Mathf.Cos(angle) * r, c + Mathf.Sin(angle) * r);
            }

            raster.FillPolygon(points, Color.white);
            return raster.Downsample(Super).ToSprite("Sparkle");
        }

        static Sprite BuildDisc()
        {
            const int Base = 96;
            int s = Base * Super;
            var raster = new Raster(s, s);
            raster.FillCircle(s * 0.5f, s * 0.5f, s * 0.5f - 2f, Color.white);
            return raster.Downsample(Super).ToSprite("Disc");
        }

        static Sprite BuildGrain()
        {
            const int size = 256;
            var raster = new Raster(size, size);
            raster.FillNoise(1f, 1337);

            var tex = raster.ToTexture("Grain");
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Grain";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
