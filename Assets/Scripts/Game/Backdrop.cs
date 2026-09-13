using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The ground the game is played on: a saturated blue field, deepening towards the tray, with a
    /// perspective grid receding to a vanishing point behind the board and the corners drawn down
    /// by a vignette. Static apart from one very slow breath in the light behind the board —
    /// a background that moves competes with the pieces, and the pieces have to win.
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        const float GlowSize = 1560f;
        const float GlowAlpha = 0.42f;
        const float VignetteAlpha = 0.55f;
        const float GrainAlpha = 0.022f;

        // A single breath, well over a minute long and only a few percent deep. Below the level
        // where the eye reads it as animation; just enough that the page is never quite still.
        const float BreatheSpeed = 0.055f;
        const float BreatheDepth = 0.06f;

        Image _glow;
        RectTransform _glowRect;

        public void Build(RectTransform parent)
        {
            var root = (RectTransform)transform;
            root.SetParent(parent, false);
            UiBuilder.Stretch(root);

            // Its own canvas: the glow breathes every frame, and on the shared canvas that
            // re-batched the whole UI every frame too. Nested this way only these few layers do.
            gameObject.AddComponent<Canvas>();

            // The field itself is the camera's clear colour (AppController.ConfigureCamera) — the
            // same Design.BgTop, but a clear is free where a full-screen blended layer is not.
            // Then the deeper blue faded in from the bottom, as a flat layer rather than a
            // gradient texture, so the colours stay editable as tokens.
            var deep = UiBuilder.Image(root, "Deep", Art.VerticalFade, Design.BgBottom);
            deep.type = Image.Type.Simple;
            UiBuilder.Stretch(deep.rectTransform);

            BuildGlow(root);

            var grid = UiBuilder.Image(root, "Grid", Art.Grid, Design.BgGrid);
            grid.type = Image.Type.Simple;
            UiBuilder.Stretch(grid.rectTransform);

            var vignette = UiBuilder.Image(root, "Vignette", Art.Vignette,
                Design.BgVignette.WithAlpha(VignetteAlpha));
            vignette.type = Image.Type.Simple;
            UiBuilder.Stretch(vignette.rectTransform);

            var grain = UiBuilder.Image(root, "Grain", Art.Grain, Color.white.WithAlpha(GrainAlpha));
            grain.type = Image.Type.Tiled;
            UiBuilder.Stretch(grain.rectTransform);
        }

        void BuildGlow(RectTransform root)
        {
            // Centred on the board, which is also where the grid converges: one focal point, so the
            // eye is pulled to the same place by the light and by the perspective.
            _glow = UiBuilder.Image(root, "Glow", Art.Pool, Design.BgGlow.WithAlpha(GlowAlpha));
            _glow.type = Image.Type.Simple;

            _glowRect = _glow.rectTransform;
            _glowRect.sizeDelta = new Vector2(GlowSize, GlowSize);
            // The backdrop is full-bleed while the page is inset, and the insets are not symmetric
            // (a cutout on top, the gesture bar below), so the page centre is not the screen centre.
            var insets = AppController.SafeInsets;
            _glowRect.anchoredPosition = new Vector2(0f, GameScreen.BoardCenterY + (insets.y - insets.w) * 0.5f);
        }

        void Update()
        {
            // An Editor domain reload wipes these plain fields without re-running Build, so the
            // backdrop idles instead of throwing every frame.
            if (_glowRect == null) return;

            float breathe = 1f + BreatheDepth * Mathf.Sin(Time.unscaledTime * BreatheSpeed);
            _glowRect.localScale = new Vector3(breathe, breathe, 1f);
        }
    }
}
