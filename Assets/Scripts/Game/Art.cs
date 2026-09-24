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
        static Sprite _grid;
        static Sprite _vignette;
        static Sprite _verticalFade;

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

        /// <summary>
        /// A perspective grid receding to a vanishing point in the middle: rays out to the edges
        /// crossed by rectangles that close in on the centre. Stretched over the whole page.
        /// </summary>
        public static Sprite Grid
        {
            get { if (_grid == null) _grid = BuildGrid(); return _grid; }
        }

        /// <summary>Clear in the middle, solid at the rim. Darkens the corners of the page.</summary>
        public static Sprite Vignette
        {
            get { if (_vignette == null) _vignette = BuildVignette(); return _vignette; }
        }

        /// <summary>Transparent at the top, solid at the bottom. Fades one ground colour into another.</summary>
        public static Sprite VerticalFade
        {
            get { if (_verticalFade == null) _verticalFade = BuildVerticalFade(); return _verticalFade; }
        }

        static Sprite _crystal;
        static Sprite _ice1;
        static Sprite _ice2;
        static readonly Sprite[] Glyphs = new Sprite[8];

        /// <summary>
        /// The crystal a level asks the player to free: a faceted prism, drawn in white with the
        /// facets as alpha steps so a single tint shades it. Sits on top of a block.
        /// </summary>
        public static Sprite Crystal
        {
            get { if (_crystal == null) _crystal = BuildCrystal(); return _crystal; }
        }

        /// <summary>Frost over a block. Two layers look different so a cracked cell reads as weaker.</summary>
        public static Sprite Ice(int layers)
        {
            if (layers >= 2) { if (_ice2 == null) _ice2 = BuildIce(true); return _ice2; }
            if (_ice1 == null) _ice1 = BuildIce(false);
            return _ice1;
        }

        /// <summary>
        /// A small distinct mark per block colour, for the colour-blind setting: two blocks that
        /// look alike in colour still look different in shape.
        /// </summary>
        public static Sprite Glyph(int colorIndex)
        {
            int i = ((colorIndex % Glyphs.Length) + Glyphs.Length) % Glyphs.Length;
            if (Glyphs[i] == null) Glyphs[i] = BuildGlyph(i);
            return Glyphs[i];
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

        static Sprite BuildGrid()
        {
            const int width = 512;
            const int height = 910;
            const int Rays = 32;
            const float RingStep = 1.34f;   // each rectangle this much larger than the last
            const float LineHalf = 1.1f;    // half thickness, in sprite pixels

            var raster = new Raster(width, height);
            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float raySpacing = Mathf.PI * 2f / Rays;
            float ringSpacing = Mathf.Log(RingStep);

            // Drawn analytically in a single pass. The stroke helpers each walk the whole canvas
            // with supersampling, and sixty of those passes would take long enough to hang the
            // Editor — here every pixel decides for itself whether a line runs through it.
            raster.Paint((x, y) =>
            {
                float dx = x - cx;
                float dy = y - cy;

                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                if (radius < 1f) return new Color(1f, 1f, 1f, 0f);

                // Rays: constant angle apart, so their spacing on screen opens up with distance.
                float steps = Mathf.Atan2(dy, dx) / raySpacing;
                float rayGap = Mathf.Abs(steps - Mathf.Round(steps)) * raySpacing * radius;
                float ray = 1f - Mathf.Clamp01(rayGap / LineHalf);

                // Rings: rectangles, not circles, so the ground reads as a room rather than a
                // tunnel. A geometric progression is what makes them look like even spacing
                // receding into the distance.
                float reach = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                float rings = Mathf.Log(Mathf.Max(reach, 1f)) / ringSpacing;
                float ringGap = Mathf.Abs(rings - Mathf.Round(rings)) * ringSpacing * reach;
                float ring = 1f - Mathf.Clamp01(ringGap / LineHalf);

                // Everything dissolves as it approaches the vanishing point, where the lines would
                // otherwise pile into a solid blot.
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(radius / (cx * 0.95f)));

                return new Color(1f, 1f, 1f, Mathf.Max(ray, ring) * fade);
            });

            return raster.ToSprite("Grid");
        }

        static Sprite BuildVignette()
        {
            const int size = 256;
            var raster = new Raster(size, size);
            float half = size * 0.5f;

            raster.Paint((x, y) =>
            {
                float u = (x - half) / half;
                float v = (y - half) / half;
                float d = Mathf.Sqrt(u * u + v * v) / 1.414f;

                // Nothing at all across the middle, then a long soft climb into the corners.
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.34f) / 0.66f));
                return new Color(1f, 1f, 1f, a * a);
            });

            return raster.ToSprite("Vignette");
        }

        static Sprite BuildVerticalFade()
        {
            const int width = 8;
            const int height = 256;
            var raster = new Raster(width, height);

            raster.Paint((x, y) =>
            {
                // y counts up from the bottom of the texture, so this lands solid at the bottom.
                float a = Mathf.SmoothStep(0f, 1f, 1f - y / height);
                return new Color(1f, 1f, 1f, a);
            });

            return raster.ToSprite("VerticalFade");
        }

        static Sprite BuildCrystal()
        {
            const int Base = 96;
            int s = Base * Super;
            var raster = new Raster(s, s);

            // A cut gem seen from the front: a table on top, a crown, and a long pavilion point.
            var top = new[] { V(s, 0.30f, 0.84f), V(s, 0.70f, 0.84f), V(s, 0.90f, 0.62f), V(s, 0.10f, 0.62f) };
            var leftPavilion = new[] { V(s, 0.10f, 0.62f), V(s, 0.50f, 0.62f), V(s, 0.50f, 0.08f) };
            var rightPavilion = new[] { V(s, 0.50f, 0.62f), V(s, 0.90f, 0.62f), V(s, 0.50f, 0.08f) };
            var table = new[] { V(s, 0.38f, 0.84f), V(s, 0.62f, 0.84f), V(s, 0.70f, 0.62f), V(s, 0.30f, 0.62f) };

            raster.FillPolygon(top, new Color(1f, 1f, 1f, 0.80f));
            raster.FillPolygon(table, Color.white);
            raster.FillPolygon(leftPavilion, new Color(1f, 1f, 1f, 0.92f));
            raster.FillPolygon(rightPavilion, new Color(1f, 1f, 1f, 0.66f));

            return raster.Downsample(Super).ToSprite("Crystal");
        }

        static Sprite BuildIce(bool thick)
        {
            const int Base = 160;
            int s = Base * Super;
            var raster = new Raster(s, s);
            float radius = s * 0.235f;

            float rim = s * 0.07f;

            raster.Paint((x, y) =>
            {
                if (!Raster.SquircleInside(x, y, s, s, radius, 4.5f)) return Color.clear;

                // Mostly opaque: a thin wash let the block colour through, and ice over a red block
                // read as a pink stain rather than as ice.
                float k = (x / s + y / s) * 0.5f;
                float a = (thick ? 0.80f : 0.62f) + 0.10f * k;

                // A brighter rim, like the edge of a slab of ice catching light.
                if (!Raster.SquircleInside(x - rim, y - rim, s - rim * 2f, s - rim * 2f, radius - rim, 4.5f)) a += 0.2f;

                // Diagonal glints.
                float band = Mathf.Repeat((x + (s - y)) / s * 2.4f, 1f);
                if (band < 0.06f) a += 0.18f;

                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            });

            // The thinner layer is cracked, so "hit once" is visible at a glance. The crack is cut
            // out of the frost, so it shows the block beneath as a dark line.
            if (!thick)
            {
                float t = s * 0.03f;
                var crack = new Color(0.55f, 0.72f, 0.85f, 1f);
                raster.Line(V(s, 0.16f, 0.74f), V(s, 0.44f, 0.52f), t, crack);
                raster.Line(V(s, 0.44f, 0.52f), V(s, 0.38f, 0.22f), t, crack);
                raster.Line(V(s, 0.44f, 0.52f), V(s, 0.80f, 0.42f), t, crack);
                raster.Line(V(s, 0.62f, 0.47f), V(s, 0.70f, 0.70f), t * 0.8f, crack);
            }

            return raster.Downsample(Super).ToSprite(thick ? "Ice2" : "Ice1", 0.34f);
        }

        static Sprite BuildGlyph(int index)
        {
            const int Base = 64;
            int s = Base * Super;
            var raster = new Raster(s, s);
            var white = Color.white;
            float c = s * 0.5f;

            switch (index)
            {
                case 0: raster.FillCircle(c, c, s * 0.30f, white); break;
                case 1: raster.FillPolygon(new[] { V(s, 0.5f, 0.84f), V(s, 0.84f, 0.20f), V(s, 0.16f, 0.20f) }, white); break;
                case 2: raster.FillRect(s * 0.22f, s * 0.22f, s * 0.56f, s * 0.56f, white); break;
                case 3: raster.FillPolygon(new[] { V(s, 0.5f, 0.88f), V(s, 0.88f, 0.5f), V(s, 0.5f, 0.12f), V(s, 0.12f, 0.5f) }, white); break;
                case 4:
                    raster.Line(V(s, 0.22f, 0.22f), V(s, 0.78f, 0.78f), s * 0.16f, white);
                    raster.Line(V(s, 0.78f, 0.22f), V(s, 0.22f, 0.78f), s * 0.16f, white);
                    break;
                case 5: raster.StrokeCircle(c, c, s * 0.32f, s * 0.13f, white); break;
                case 6:
                    raster.FillRect(s * 0.42f, s * 0.14f, s * 0.16f, s * 0.72f, white);
                    raster.FillRect(s * 0.14f, s * 0.42f, s * 0.72f, s * 0.16f, white);
                    break;
                default:
                    raster.FillRect(s * 0.16f, s * 0.24f, s * 0.68f, s * 0.14f, white);
                    raster.FillRect(s * 0.16f, s * 0.62f, s * 0.68f, s * 0.14f, white);
                    break;
            }

            return raster.Downsample(Super).ToSprite("Glyph" + index);
        }

        static Vector2 V(int s, float x, float y) => new Vector2(s * x, s * y);

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

        static Sprite _medal;

        /// <summary>
        /// A badge medallion: a hexagon with a solid rim and a half-strength face, so one tint
        /// shades it in two tones and a white glyph on the face stays readable. The shape is the
        /// daily's own — nothing else in the game is six-sided.
        /// </summary>
        public static Sprite Medal
        {
            get { if (_medal == null) _medal = BuildMedal(); return _medal; }
        }

        static Sprite BuildMedal()
        {
            const int Base = 160;
            int s = Base * Super;
            var raster = new Raster(s, s);
            float c = s * 0.5f;

            Vector2[] Hex(float radius)
            {
                var points = new Vector2[6];
                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.PI * 0.5f + i * Mathf.PI / 3f;
                    points[i] = new Vector2(c + Mathf.Cos(angle) * radius, c + Mathf.Sin(angle) * radius);
                }
                return points;
            }

            raster.FillPolygon(Hex(s * 0.49f), Color.white);
            raster.ErasePolygon(Hex(s * 0.41f));
            raster.FillPolygon(Hex(s * 0.41f), new Color(1f, 1f, 1f, 0.5f));
            return raster.Downsample(Super).ToSprite("Medal");
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
