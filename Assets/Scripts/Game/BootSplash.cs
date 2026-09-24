using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The game's own opening screen, shown over the menu for about a second on launch.
    ///
    /// It is the same mark as the app icon — a small board with a staircase of blocks climbing
    /// through it, five colours read like light splitting through a prism — so the thing the
    /// player tapped on the launcher is the thing that greets them. The blocks arrive one at a
    /// time rather than all at once: that is the whole idea of the name, stated without a word
    /// of explanation, and it gives the first second a shape instead of a wait.
    ///
    /// Deliberately short and skippable. An opening screen earns its place the first few times
    /// and is a toll on every launch after that, so a touch anywhere ends it immediately.
    ///
    /// Not an <see cref="AppScreen"/>: it lives above both layers, outside the theme rebuild, and
    /// deletes itself when it is done. It reads the theme, so it opens in the player's colours.
    /// </summary>
    public sealed class BootSplash : MonoBehaviour
    {
        /// <summary>Longest the splash can hold the launch, skip aside. Kept under a second and a half.</summary>
        public const float Duration = 1.15f;

        const float MarkSize = 420f;
        const float BlockStep = 0.06f;

        RectTransform _root;
        CanvasGroup _group;
        bool _done;

        /// <summary>True while the splash is on screen. Read by the automated screenshot pass.</summary>
        public static bool Active { get; private set; }

        public static BootSplash Play(RectTransform parent)
        {
            var go = new GameObject("BootSplash", typeof(RectTransform));
            var splash = go.AddComponent<BootSplash>();
            splash.Build(parent);
            return splash;
        }

        void Build(RectTransform parent)
        {
            _root = (RectTransform)transform;
            _root.SetParent(parent, false);
            UiBuilder.StretchFullScreen(_root);
            _root.SetAsLastSibling();

            _group = gameObject.AddComponent<CanvasGroup>();

            // Nothing behind it may take a touch while it is up — the menu's play button sits
            // right under the mark. Same rule every modal and the result card follow.
            PointerRouter.PushBlocker(_root);

            // The ground, opaque and full-bleed, so the splash covers the notch and the gesture
            // bar as well: a launch that shows a strip of menu around the edges reads as a glitch.
            UiBuilder.Gradient(_root, "Ground", Design.BgTop, Design.BgBottom);

            var glow = UiBuilder.Image(_root, "Glow", Art.Pool, Design.BgGlow.WithAlpha(Design.GlowAlpha));
            glow.type = Image.Type.Simple;
            glow.rectTransform.sizeDelta = new Vector2(1400f, 1400f);
            glow.rectTransform.anchoredPosition = new Vector2(0f, 120f);

            BuildMark();
            BuildWordmark();

            StartCoroutine(Run());
        }

        /// <summary>
        /// The icon's mark, rebuilt from the same primitives the board uses rather than baked to a
        /// texture: it costs nothing at launch and it follows the theme.
        /// </summary>
        RectTransform[] _blocks;

        void BuildMark()
        {
            var panel = UiBuilder.Panel(_root, "Mark", new Vector2(MarkSize, MarkSize),
                Design.BoardSurface, Design.RadiusLg);
            panel.rectTransform.anchoredPosition = new Vector2(0f, 150f);

            UiBuilder.Shadow(_root, "MarkShadow", new Vector2(MarkSize, MarkSize), Design.RadiusLg, Design.E1)
                .rectTransform.anchoredPosition = new Vector2(0f, 150f);
            panel.transform.SetAsLastSibling();

            float pad = MarkSize * 0.085f;
            float gap = MarkSize * 0.045f;
            float cell = (MarkSize - pad * 2f - gap * 2f) / 3f;

            // Row from the top, column from the left: the staircase of five colours, exactly as
            // AppIconArt lays it out. -1 is an empty cell of the little board.
            var filled = new[,]
            {
                { 0, 1, -1 },
                { -1, 2, 3 },
                { -1, -1, 4 }
            };

            var colours = Design.Blocks;
            _blocks = new RectTransform[5];

            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                int index = filled[row, col];
                if (index < 0) continue;

                var block = UiBuilder.Image(panel.rectTransform, $"Block{index}", Art.Block,
                    colours[index % colours.Length]);
                block.type = Image.Type.Sliced;
                block.rectTransform.sizeDelta = new Vector2(cell, cell);
                block.rectTransform.anchoredPosition = new Vector2(
                    -MarkSize * 0.5f + pad + cell * 0.5f + col * (cell + gap),
                    MarkSize * 0.5f - pad - cell * 0.5f - row * (cell + gap));
                block.rectTransform.localScale = Vector3.zero;

                _blocks[index] = block.rectTransform;
            }
        }

        TMPro.TextMeshProUGUI _word;
        Vector2 _wordHome;

        void BuildWordmark()
        {
            var word = UiBuilder.Label(_root, "Wordmark", MainMenuScreen.GameName, Design.Display,
                Design.TextPrimary, Design.FontDisplay, tracking: Design.TrackingDisplay);
            word.rectTransform.sizeDelta = new Vector2(1000f, Design.Display * 1.35f);
            word.rectTransform.anchoredPosition = new Vector2(0f, -230f);
            UiBuilder.TextShadow(word, 0.4f, -0.3f, 0.45f);
            word.alpha = 0f;

            _word = word;
            _wordHome = word.rectTransform.anchoredPosition;
        }

        IEnumerator Run()
        {
            Active = true;

            // The blocks climb in, one step at a time.
            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] != null)
                    StartCoroutine(Tween.Scale(_blocks[i], Vector3.zero, Vector3.one, 0.26f,
                        Ease.OutBack, i * BlockStep));
            }

            float wordAt = _blocks.Length * BlockStep + 0.04f;
            StartCoroutine(RiseWordmark(wordAt));

            yield return new WaitForSecondsRealtime(Duration);
            Finish();
        }

        IEnumerator RiseWordmark(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            var rect = _word.rectTransform;
            var from = _wordHome + new Vector2(0f, -26f);

            const float duration = 0.26f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Ease.OutCubic(t / duration);
                _word.alpha = k;
                rect.anchoredPosition = Vector2.Lerp(from, _wordHome, k);
                yield return null;
            }

            _word.alpha = 1f;
            rect.anchoredPosition = _wordHome;
        }

        /// <summary>A touch anywhere cuts it short. Launch two onwards, nobody wants to watch this.</summary>
        void Update()
        {
            if (_done) return;

            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) Finish();
        }

        void Finish()
        {
            if (_done) return;
            _done = true;

            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            const float duration = 0.28f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - Ease.OutQuad(t / duration);
                yield return null;
            }

            Dismiss();
        }

        void Dismiss()
        {
            Active = false;
            PointerRouter.PopBlocker(_root);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Covers the theme rebuild and a quit mid-splash: the blocker must never outlive the
            // object that pushed it, or the whole interface stops taking touches.
            Active = false;
            if (_root != null) PointerRouter.PopBlocker(_root);
        }
    }
}
