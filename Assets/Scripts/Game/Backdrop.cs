using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// A mesh gradient: a near-black base with a few very large, very soft colour pools drifting
    /// slowly behind everything, finished with film grain so the gradients never band.
    /// Deliberately quiet — the board and the buttons are what should hold attention.
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        const int PoolCount = 3;
        const int StarCount = 18;

        struct Pool
        {
            public RectTransform Rect;
            public Vector2 Home;
            public float Phase;
            public float Speed;
            public Vector2 Drift;
        }

        struct Star
        {
            public Image Image;
            public float Phase;
            public float Speed;
            public float BaseAlpha;
        }

        Pool[] _pools;
        Star[] _stars;

        public void Build(RectTransform parent)
        {
            var root = (RectTransform)transform;
            root.SetParent(parent, false);
            UiBuilder.Stretch(root);

            var baseFill = UiBuilder.Image(root, "Base", Art.Panel(0f), Design.BgBase);
            baseFill.type = Image.Type.Simple;
            UiBuilder.Stretch(baseFill.rectTransform);

            BuildPools(root);
            BuildStars(root);
            BuildGrain(root);
        }

        void BuildPools(RectTransform root)
        {
            // Large, low-opacity and far apart: the eye should read one continuous wash, not
            // three separate blobs.
            var tints = new[] { Design.BgPoolA, Design.BgPoolB, Design.BgPoolC };
            var homes = new[]
            {
                new Vector2(-340f, 660f),
                new Vector2(400f, -600f),
                new Vector2(160f, 40f)
            };
            var sizes = new[] { 1750f, 1650f, 1450f };
            var alphas = new[] { 0.9f, 0.85f, 0.5f };

            _pools = new Pool[PoolCount];
            var rng = new System.Random(11);

            for (int i = 0; i < PoolCount; i++)
            {
                var image = UiBuilder.Image(root, $"Pool_{i}", Art.Pool, tints[i].WithAlpha(alphas[i]));
                image.type = Image.Type.Simple;

                var rect = image.rectTransform;
                rect.sizeDelta = new Vector2(sizes[i], sizes[i]);
                rect.anchoredPosition = homes[i];

                _pools[i] = new Pool
                {
                    Rect = rect,
                    Home = homes[i],
                    Phase = (float)rng.NextDouble() * 6.28f,
                    Speed = 0.055f + (float)rng.NextDouble() * 0.045f,
                    Drift = new Vector2(90f + (float)rng.NextDouble() * 70f, 70f + (float)rng.NextDouble() * 60f)
                };
            }
        }

        void BuildStars(RectTransform root)
        {
            _stars = new Star[StarCount];
            var rng = new System.Random(29);

            for (int i = 0; i < StarCount; i++)
            {
                float alpha = 0.14f + (float)rng.NextDouble() * 0.22f;
                var image = UiBuilder.Image(root, $"Star_{i}", Art.SoftCircle, Color.white.WithAlpha(alpha));
                image.type = Image.Type.Simple;

                float size = 4f + (float)rng.NextDouble() * 6f;
                image.rectTransform.sizeDelta = new Vector2(size, size);
                image.rectTransform.anchoredPosition = new Vector2(
                    (float)(rng.NextDouble() * 1300d - 650d),
                    (float)(rng.NextDouble() * 2100d - 1050d));

                _stars[i] = new Star
                {
                    Image = image,
                    Phase = (float)rng.NextDouble() * 6.28f,
                    Speed = 0.4f + (float)rng.NextDouble() * 0.9f,
                    BaseAlpha = alpha
                };
            }
        }

        void BuildGrain(RectTransform root)
        {
            var grain = UiBuilder.Image(root, "Grain", Art.Grain, Color.white.WithAlpha(0.028f));
            grain.type = Image.Type.Tiled;
            UiBuilder.Stretch(grain.rectTransform);
        }

        void Update()
        {
            // An Editor domain reload wipes these plain arrays without re-running Build, so the
            // backdrop idles instead of throwing every frame.
            if (_pools == null || _stars == null) return;

            float t = Time.unscaledTime;

            for (int i = 0; i < _pools.Length; i++)
            {
                var p = _pools[i];
                float k = t * p.Speed + p.Phase;
                p.Rect.anchoredPosition = p.Home + new Vector2(
                    Mathf.Sin(k) * p.Drift.x,
                    Mathf.Cos(k * 0.73f) * p.Drift.y);
            }

            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                float twinkle = 0.6f + 0.4f * Mathf.Sin(t * s.Speed + s.Phase);
                s.Image.color = Color.white.WithAlpha(s.BaseAlpha * twinkle);
            }
        }
    }
}
