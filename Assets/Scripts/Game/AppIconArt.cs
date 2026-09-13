using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The app icon, drawn with <see cref="Raster"/> like everything else. A small board on the
    /// blue ground with a staircase of blocks climbing through it — five of the block colours,
    /// read top-left to bottom-right like light splitting through a prism — and a crystal on the
    /// middle block. At launcher size it has to read as "this game" from one glance.
    ///
    /// The editor bakes these into Assets/Generated (Editor/AppIconGenerator) because Android
    /// takes its icon from texture assets; this code stays the source.
    /// </summary>
    public static class AppIconArt
    {
        const int Super = 2;

        enum Mask { None, Squircle, Circle }

        /// <summary>The legacy launcher icon: full art inside a squircle.</summary>
        public static Texture2D Full(int size) => Draw(size, background: true, mark: 0.64f, Mask.Squircle);

        /// <summary>The round launcher icon.</summary>
        public static Texture2D Round(int size) => Draw(size, background: true, mark: 0.58f, Mask.Circle);

        /// <summary>Adaptive icon, back layer: the ground only.</summary>
        public static Texture2D Background(int size) => Draw(size, background: true, mark: 0f, Mask.None);

        /// <summary>
        /// Adaptive icon, front layer. The launcher may crop the outer third, so the board stays
        /// inside the middle two thirds.
        /// </summary>
        public static Texture2D Foreground(int size) => Draw(size, background: false, mark: 0.46f, Mask.None);

        static Texture2D Draw(int size, bool background, float mark, Mask mask)
        {
            int s = size * Super;
            var raster = new Raster(s, s);

            if (background) PaintGround(raster, s);
            if (mark > 0f) PaintMark(raster, s, mark);

            var texture = raster.Downsample(Super).ToTexture("AppIcon");
            ApplyMask(texture, mask);
            return texture;
        }

        static void PaintGround(Raster raster, int s)
        {
            var glow = Themes.Default.BgGlow;
            raster.Paint((x, y) =>
            {
                float k = y / s;
                var c = Color.Lerp(Themes.Default.BgBottom, Themes.Default.BgTop, k);

                float dx = x / s - 0.5f;
                float dy = y / s - 0.56f;
                float g = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.6f);
                c = Color.Lerp(c, glow, g * g * 0.4f);
                c.a = 1f;
                return c;
            });
        }

        static void PaintMark(Raster raster, int s, float scale)
        {
            float board = s * scale;
            float x0 = (s - board) * 0.5f;
            float y0 = (s - board) * 0.5f;

            // A soft shadow under the board, offset slightly down, like the in-game E1 elevation.
            raster.FillSquircle(x0 - s * 0.012f, y0 - s * 0.03f, board + s * 0.024f, board + s * 0.024f,
                board * 0.2f, new Color(0f, 0f, 0f, 0.22f));
            raster.FillSquircle(x0, y0, board, board, board * 0.18f, Themes.Default.BoardSurface);

            float pad = board * 0.085f;
            float gap = board * 0.045f;
            float cell = (board - pad * 2f - gap * 2f) / 3f;

            var colours = Themes.Default.Blocks;
            // Row from the top, column from the left: a staircase of five colours.
            var filled = new[,]
            {
                { 0, 1, -1 },
                { -1, 2, 3 },
                { -1, -1, 4 }
            };

            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                float px = x0 + pad + col * (cell + gap);
                float py = y0 + board - pad - (row + 1) * cell - row * gap;

                int colour = filled[row, col];
                if (colour < 0) raster.FillSquircle(px, py, cell, cell, cell * 0.22f, Themes.Default.SurfaceInset);
                else PaintBlock(raster, px, py, cell, colours[colour]);
            }

            // The crystal on the middle block.
            float cx = x0 + pad + (cell + gap);
            float cy = y0 + board - pad - 2f * cell - gap;
            PaintCrystal(raster, cx + cell * 0.18f, cy + cell * 0.16f, cell * 0.64f);
        }

        static void PaintBlock(Raster raster, float px, float py, float size, Color colour)
        {
            float radius = size * 0.235f;
            float band = size * 0.09f;

            raster.Paint((x, y) =>
            {
                float lx = x - px;
                float ly = y - py;
                if (lx < 0f || ly < 0f || lx > size || ly > size) return Color.clear;
                if (!Raster.SquircleInside(lx, ly, size, size, radius, 4.5f)) return Color.clear;

                // The same light as the in-game block: a lifted top edge, a shaded bottom.
                float lum = Mathf.Lerp(0.72f, 0.97f, ly / size);
                float fromTop = size - ly;
                if (fromTop < band) lum = Mathf.Lerp(1.1f, lum, fromTop / band);
                if (ly < band) lum = Mathf.Lerp(0.5f, lum, ly / band);

                return new Color(Mathf.Clamp01(colour.r * lum), Mathf.Clamp01(colour.g * lum), Mathf.Clamp01(colour.b * lum), 1f);
            });
        }

        static void PaintCrystal(Raster raster, float x, float y, float size)
        {
            Vector2 P(float u, float v) => new Vector2(x + u * size, y + v * size);

            var tint = Design.Crystal;
            raster.FillPolygon(new[] { P(0.30f, 0.84f), P(0.70f, 0.84f), P(0.90f, 0.62f), P(0.10f, 0.62f) }, tint.WithAlpha(0.85f));
            raster.FillPolygon(new[] { P(0.38f, 0.84f), P(0.62f, 0.84f), P(0.70f, 0.62f), P(0.30f, 0.62f) }, tint);
            raster.FillPolygon(new[] { P(0.10f, 0.62f), P(0.50f, 0.62f), P(0.50f, 0.08f) }, tint.WithAlpha(0.95f));
            raster.FillPolygon(new[] { P(0.50f, 0.62f), P(0.90f, 0.62f), P(0.50f, 0.08f) }, tint.WithAlpha(0.7f));
        }

        static void ApplyMask(Texture2D texture, Mask mask)
        {
            if (mask == Mask.None) return;

            int n = texture.width;
            var pixels = texture.GetPixels();
            float radius = n * 0.225f;

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Four samples per pixel keep the silhouette's edge smooth.
                float coverage = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float px = x + 0.25f + sx * 0.5f;
                    float py = y + 0.25f + sy * 0.5f;
                    bool inside = mask == Mask.Circle
                        ? (px - n * 0.5f) * (px - n * 0.5f) + (py - n * 0.5f) * (py - n * 0.5f) <= n * n * 0.25f
                        : Raster.SquircleInside(px, py, n, n, radius, 4.5f);
                    if (inside) coverage += 0.25f;
                }

                int i = y * n + x;
                var c = pixels[i];
                pixels[i] = new Color(c.r, c.g, c.b, c.a * coverage);
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }
    }
}
