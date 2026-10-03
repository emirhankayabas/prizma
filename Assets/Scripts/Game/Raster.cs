using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// A tiny software rasteriser. Everything the game draws — blocks, icons, frames — is generated
    /// through this at startup, so the project needs no imported art at all.
    /// Coordinates are pixel space with y = 0 at the bottom, matching Unity textures.
    ///
    /// All of it runs before the first frame, so it is the bulk of the start-up time. Two things
    /// keep that short without changing a single output pixel: a shape only samples the pixels
    /// inside its bounding box (outside it every sample is zero, and a zero blend is no blend), and
    /// rows are spread over the cores (every pixel is computed on its own, from nothing but its
    /// position, so the order does not matter). Shaders passed to <see cref="Paint"/> must stay
    /// pure for the second to hold.
    /// </summary>
    public sealed class Raster
    {
        /// <summary>Sub-samples per axis. 3 is enough to keep diagonal seams and thin icons clean.</summary>
        const int Samples = 3;

        readonly Color[] _pixels;

        public readonly int Width;
        public readonly int Height;

        public Raster(int width, int height)
        {
            Width = width;
            Height = height;
            _pixels = new Color[width * height];
        }

        public void Clear(Color color)
        {
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = color;
        }

        /// <summary>Alpha-blends a colour over a pixel at the given coverage.</summary>
        public void Blend(int x, int y, Color color, float coverage)
        {
            if (coverage <= 0f || x < 0 || x >= Width || y < 0 || y >= Height) return;

            float a = color.a * Mathf.Clamp01(coverage);
            if (a <= 0f) return;

            int i = y * Width + x;
            var dst = _pixels[i];

            float outA = a + dst.a * (1f - a);
            if (outA <= 0f)
            {
                _pixels[i] = Color.clear;
                return;
            }

            _pixels[i] = new Color(
                (color.r * a + dst.r * dst.a * (1f - a)) / outA,
                (color.g * a + dst.g * dst.a * (1f - a)) / outA,
                (color.b * a + dst.b * dst.a * (1f - a)) / outA,
                outA);
        }

        /// <summary>Multiplies a pixel's alpha down, cutting a hole in what is already drawn.</summary>
        public void Erase(int x, int y, float coverage)
        {
            if (coverage <= 0f || x < 0 || x >= Width || y < 0 || y >= Height) return;

            int i = y * Width + x;
            var c = _pixels[i];
            _pixels[i] = new Color(c.r, c.g, c.b, c.a * (1f - Mathf.Clamp01(coverage)));
        }

        // ------------------------------------------------------------------ shapes

        public void FillCircle(float cx, float cy, float radius, Color color) =>
            Shade((x, y) => CircleCoverage(x, y, cx, cy, radius), color, Box(cx - radius, cy - radius, cx + radius, cy + radius));

        public void EraseCircle(float cx, float cy, float radius) =>
            ShadeErase((x, y) => CircleCoverage(x, y, cx, cy, radius), Box(cx - radius, cy - radius, cx + radius, cy + radius));

        public void EraseRect(float x0, float y0, float w, float h) =>
            ShadeErase((x, y) => (x >= x0 && x < x0 + w && y >= y0 && y < y0 + h) ? 1f : 0f, Box(x0, y0, x0 + w, y0 + h));

        public void ErasePolygon(Vector2[] points) =>
            ShadeErase((x, y) => PointInPolygon(points, x, y) ? 1f : 0f, Bounds(points));

        /// <summary>Draws a thick line segment as a quad. The building block for most icon glyphs.</summary>
        public void Line(Vector2 a, Vector2 b, float thickness, Color color)
        {
            var dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            FillPolygon(new[] { a + normal, b + normal, b - normal, a - normal }, color);
            FillCircle(a.x, a.y, thickness * 0.5f, color);
            FillCircle(b.x, b.y, thickness * 0.5f, color);
        }

        public void FillRect(float x0, float y0, float w, float h, Color color) =>
            Shade((x, y) => (x >= x0 && x < x0 + w && y >= y0 && y < y0 + h) ? 1f : 0f, color, Box(x0, y0, x0 + w, y0 + h));

        public void FillRoundRect(float x0, float y0, float w, float h, float radius, Color color) =>
            Shade((x, y) => RoundRectInside(x - x0, y - y0, w, h, radius) ? 1f : 0f, color, Box(x0, y0, x0 + w, y0 + h));

        public void FillPolygon(Vector2[] points, Color color) =>
            Shade((x, y) => PointInPolygon(points, x, y) ? 1f : 0f, color, Bounds(points));

        /// <summary>
        /// A squircle: straight edges meeting corners shaped by a superellipse. This continuous
        /// curvature is what separates a modern iOS-style shape from a plain rounded rectangle,
        /// where the arc meets the edge at a visible seam.
        /// </summary>
        public void FillSquircle(float x0, float y0, float w, float h, float radius, Color color, float exponent = 4.5f) =>
            Shade((x, y) => SquircleInside(x - x0, y - y0, w, h, radius, exponent) ? 1f : 0f, color, Box(x0, y0, x0 + w, y0 + h));

        public void StrokeSquircle(float x0, float y0, float w, float h, float radius, float thickness, Color color,
            float exponent = 4.5f)
        {
            Shade((x, y) =>
            {
                bool outer = SquircleInside(x - x0, y - y0, w, h, radius, exponent);
                bool inner = SquircleInside(x - x0 - thickness, y - y0 - thickness,
                    w - thickness * 2f, h - thickness * 2f, Mathf.Max(0.01f, radius - thickness), exponent);
                return (outer && !inner) ? 1f : 0f;
            }, color, Box(x0, y0, x0 + w, y0 + h));
        }

        public static bool SquircleInside(float px, float py, float w, float h, float radius, float exponent)
        {
            if (px < 0f || px > w || py < 0f || py > h) return false;

            radius = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
            if (radius <= 0f) return true;

            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - radius);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - radius);

            // Only the corner quadrant curves; the edges stay perfectly straight.
            if (qx <= 0f || qy <= 0f) return true;

            return Mathf.Pow(qx / radius, exponent) + Mathf.Pow(qy / radius, exponent) <= 1f;
        }

        /// <summary>
        /// Separable box blur over the alpha channel, repeated to approximate a Gaussian.
        /// Used to bake the soft ambient shadows the UI sits on.
        /// </summary>
        public void BlurAlpha(int radius, int passes = 3)
        {
            if (radius <= 0) return;

            var src = new float[Width * Height];
            var dst = new float[Width * Height];

            for (int i = 0; i < _pixels.Length; i++)
                src[i] = _pixels[i].a;

            for (int p = 0; p < passes; p++)
            {
                BoxPass(src, dst, radius, horizontal: true);
                BoxPass(dst, src, radius, horizontal: false);
            }

            for (int i = 0; i < _pixels.Length; i++)
            {
                var c = _pixels[i];
                _pixels[i] = new Color(c.r, c.g, c.b, Mathf.Clamp01(src[i]));
            }
        }

        void BoxPass(float[] src, float[] dst, int radius, bool horizontal)
        {
            int outer = horizontal ? Height : Width;
            int inner = horizontal ? Width : Height;
            float norm = 1f / (radius * 2 + 1);

            Rows(outer, o =>
            {
                for (int i = 0; i < inner; i++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int s = Mathf.Clamp(i + k, 0, inner - 1);
                        sum += horizontal ? src[o * Width + s] : src[s * Width + o];
                    }

                    if (horizontal) dst[o * Width + i] = sum * norm;
                    else dst[i * Width + o] = sum * norm;
                }
            });
        }

        /// <summary>
        /// Fills with white noise. The variation is in alpha rather than colour, so tinting the
        /// sprite down gives an even film grain instead of bright speckles.
        /// </summary>
        public void FillNoise(float alpha, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = new Color(1f, 1f, 1f, (float)rng.NextDouble() * alpha);
        }

        public void FillTriangle(Vector2 a, Vector2 b, Vector2 c, Color color) =>
            FillPolygon(new[] { a, b, c }, color);

        /// <summary>Draws a ring by filling a disc and erasing a smaller one.</summary>
        public void StrokeCircle(float cx, float cy, float radius, float thickness, Color color)
        {
            Shade((x, y) =>
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                return (d <= radius && d >= radius - thickness) ? 1f : 0f;
            }, color, Box(cx - radius, cy - radius, cx + radius, cy + radius));
        }

        public void StrokeRoundRect(float x0, float y0, float w, float h, float radius, float thickness, Color color)
        {
            Shade((x, y) =>
            {
                bool outer = RoundRectInside(x - x0, y - y0, w, h, radius);
                bool inner = RoundRectInside(x - x0 - thickness, y - y0 - thickness,
                    w - thickness * 2f, h - thickness * 2f, Mathf.Max(0f, radius - thickness));
                return (outer && !inner) ? 1f : 0f;
            }, color, Box(x0, y0, x0 + w, y0 + h));
        }

        /// <summary>Fills the whole canvas from a callback that returns a colour per pixel.</summary>
        public void Paint(System.Func<float, float, Color> shader)
        {
            Rows(Height, y =>
            {
                for (int x = 0; x < Width; x++)
                    Blend(x, y, shader(x + 0.5f, y + 0.5f), 1f);
            });
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>
        /// Super-samples a coverage function across each pixel and blends the result. Only the pixels
        /// inside <paramref name="box"/> are visited: the shape covers nothing outside it.
        /// </summary>
        void Shade(System.Func<float, float, float> coverage, Color color, RectInt box)
        {
            float step = 1f / Samples;
            float weight = 1f / (Samples * Samples);

            Rows(box.height, row =>
            {
                int y = box.y + row;
                for (int x = box.x; x < box.xMax; x++)
                {
                    float total = 0f;
                    for (int sy = 0; sy < Samples; sy++)
                    for (int sx = 0; sx < Samples; sx++)
                        total += coverage(x + (sx + 0.5f) * step, y + (sy + 0.5f) * step) * weight;

                    if (total > 0f) Blend(x, y, color, total);
                }
            }, box.width);
        }

        void ShadeErase(System.Func<float, float, float> coverage, RectInt box)
        {
            float step = 1f / Samples;
            float weight = 1f / (Samples * Samples);

            Rows(box.height, row =>
            {
                int y = box.y + row;
                for (int x = box.x; x < box.xMax; x++)
                {
                    float total = 0f;
                    for (int sy = 0; sy < Samples; sy++)
                    for (int sx = 0; sx < Samples; sx++)
                        total += coverage(x + (sx + 0.5f) * step, y + (sy + 0.5f) * step) * weight;

                    if (total > 0f) Erase(x, y, total);
                }
            }, box.width);
        }

        /// <summary>
        /// The pixels a shape spanning [x0, x1] × [y0, y1] can touch, clipped to the canvas. A pixel's
        /// samples sit strictly inside it, so one pixel of margin on the low side is all it needs.
        /// </summary>
        RectInt Box(float x0, float y0, float x1, float y1)
        {
            if (float.IsNaN(x0) || float.IsNaN(y0) || float.IsNaN(x1) || float.IsNaN(y1))
                return new RectInt(0, 0, Width, Height);

            int px0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(Mathf.Min(x0, x1), -1f)) - 1);
            int py0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(Mathf.Min(y0, y1), -1f)) - 1);
            int px1 = Mathf.Min(Width, Mathf.CeilToInt(Mathf.Min(Mathf.Max(x0, x1), Width + 1f)) + 1);
            int py1 = Mathf.Min(Height, Mathf.CeilToInt(Mathf.Min(Mathf.Max(y0, y1), Height + 1f)) + 1);
            return new RectInt(px0, py0, Mathf.Max(0, px1 - px0), Mathf.Max(0, py1 - py0));
        }

        RectInt Bounds(Vector2[] points)
        {
            if (points.Length == 0) return new RectInt(0, 0, 0, 0);
            float x0 = points[0].x, x1 = x0, y0 = points[0].y, y1 = y0;
            foreach (var p in points)
            {
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
            }

            return Box(x0, y0, x1, y1);
        }

        /// <summary>
        /// Runs <paramref name="row"/> for 0..count-1, across the cores when there is enough work to
        /// be worth it. Each row writes only its own pixels.
        /// </summary>
        static void Rows(int count, System.Action<int> row, int width = 256)
        {
            if (count <= 0) return;
            if ((long)count * width < ParallelMinimum)
            {
                for (int i = 0; i < count; i++) row(i);
                return;
            }

            System.Threading.Tasks.Parallel.For(0, count, row);
        }

        // Below this many pixels a job finishes before the threads it would be spread over wake up.
        const int ParallelMinimum = 8192;

        static float CircleCoverage(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= radius * radius ? 1f : 0f;
        }

        public static bool RoundRectInside(float px, float py, float w, float h, float radius)
        {
            if (w <= 0f || h <= 0f) return false;
            radius = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
            return SignedRoundRect(px, py, w, h, radius) <= 0f;
        }

        /// <summary>Negative inside the rounded rectangle, positive outside, measured in pixels.</summary>
        public static float SignedRoundRect(float px, float py, float w, float h, float radius)
        {
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float qx = Mathf.Abs(px - halfW) - (halfW - radius);
            float qy = Mathf.Abs(py - halfH) - (halfH - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static bool PointInPolygon(Vector2[] points, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                if (points[i].y > y != points[j].y > y &&
                    x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                    inside = !inside;
            }

            return inside;
        }

        // ------------------------------------------------------------------ output

        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            tex.SetPixels(_pixels);
            tex.Apply();
#if PRIZMA_AUTOTEST
            // A fingerprint of every generated texture: a change to the rasteriser that is meant to
            // be invisible is checked by comparing these before and after.
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (var b in tex.GetRawTextureData()) hash = (hash ^ b) * 1099511628211L;
                Debug.Log($"[Raster] {name} {Width}x{Height} {hash:X16}");
            }
#endif
            return tex;
        }

        /// <summary>Box-downsamples by an integer factor. Used to render shapes at 2x then shrink for free AA.</summary>
        public Raster Downsample(int factor)
        {
            var result = new Raster(Width / factor, Height / factor);
            float weight = 1f / (factor * factor);

            Rows(result.Height, y =>
            {
            for (int x = 0; x < result.Width; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;

                for (int sy = 0; sy < factor; sy++)
                for (int sx = 0; sx < factor; sx++)
                {
                    var c = _pixels[(y * factor + sy) * Width + (x * factor + sx)];
                    // Premultiply so transparent pixels do not drag colour into the average.
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                }

                a *= weight;
                if (a <= 0.0001f)
                {
                    result._pixels[y * result.Width + x] = Color.clear;
                    continue;
                }

                float inv = weight / a;
                result._pixels[y * result.Width + x] = new Color(r * inv, g * inv, b * inv, a);
            }
            }, result.Width * factor * factor);

            return result;
        }

        public Sprite ToSprite(string name, float borderFraction = 0f)
        {
            var tex = ToTexture(name);
            var border = Vector4.zero;

            if (borderFraction > 0f)
            {
                float b = Mathf.Min(Width, Height) * borderFraction;
                border = new Vector4(b, b, b, b);
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, Width, Height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);

            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
