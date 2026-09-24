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
        const float VignetteAlpha = 0.55f;
        const float GrainAlpha = 0.022f;

        // A single breath, well over a minute long and only a few percent deep. Below the level
        // where the eye reads it as animation; just enough that the page is never quite still.
        const float BreatheSpeed = 0.055f;
        const float BreatheDepth = 0.06f;

        Image _glow;
        RectTransform _glowRect;
        Image _aura;
        Image _floor;
        Image _deep;
        Image _grid;

        /// <summary>The one backdrop on screen. Replaced when the interface is rebuilt for a theme.</summary>
        public static Backdrop Current { get; private set; }

        // ------------------------------------------------------------------ light
        //
        // The run's light (Spectrum) lives here. A target colour and a heat are set; the actual
        // light eases towards them over a second or so, so a change of stage reads as the room
        // changing rather than a switch being thrown. Flashes and the rainbow pass ride on top
        // and fade back to the target on their own.

        Color? _tintTarget;
        Color _tint;
        float _tintAmount;
        float _heatTarget;
        float _heat;
        bool _prismTop;

        Color _flash;
        float _flashLeft;
        float _flashLength;
        float _rainbowLeft;
        const float RainbowLength = 1.4f;

        bool _dirty = true;

        /// <summary>
        /// The light to settle into. Null returns to the theme's own. <paramref name="turning"/>
        /// makes it walk the whole spectrum — the top stage.
        /// </summary>
        public void SetLight(Color? tint, bool turning = false)
        {
            _tintTarget = tint;
            _prismTop = turning;
            _dirty = true;
        }

        /// <summary>0..1: how hot the run is — the combo streak. Brightens and warms the light.</summary>
        public void SetHeat(float heat)
        {
            _heatTarget = Mathf.Clamp01(heat);
            _dirty = true;
        }

        /// <summary>A brief wash of one colour — a perfect clear's gold — fading back on its own.</summary>
        public void Flash(Color color, float seconds = 0.9f)
        {
            _flash = color;
            _flashLeft = _flashLength = seconds;
        }

        /// <summary>The light runs once through the whole spectrum — a single-colour line.</summary>
        public void Rainbow() => _rainbowLeft = RainbowLength;

        /// <summary>Settles at once, without easing — for a screen shown fresh with its own light.</summary>
        public void ResetLight(Color? tint = null)
        {
            _tintTarget = tint;
            _tint = tint ?? Design.BgGlow;
            _tintAmount = tint == null ? 0f : 1f;
            _heat = _heatTarget = 0f;
            _flashLeft = _rainbowLeft = 0f;
            _prismTop = false;
            _dirty = true;
        }

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
            _deep = UiBuilder.Image(root, "Deep", Art.VerticalFade, Design.BgBottom);
            _deep.type = Image.Type.Simple;
            UiBuilder.Stretch(_deep.rectTransform);

            BuildGlow(root);

            _grid = UiBuilder.Image(root, "Grid", Art.Grid, Design.BgGrid);
            _grid.type = Image.Type.Simple;
            UiBuilder.Stretch(_grid.rectTransform);

            var vignette = UiBuilder.Image(root, "Vignette", Art.Vignette,
                Design.BgVignette.WithAlpha(VignetteAlpha));
            vignette.type = Image.Type.Simple;
            UiBuilder.Stretch(vignette.rectTransform);

            var grain = UiBuilder.Image(root, "Grain", Art.Grain, Color.white.WithAlpha(GrainAlpha));
            grain.type = Image.Type.Tiled;
            UiBuilder.Stretch(grain.rectTransform);

            Current = this;
            _tint = Design.BgGlow;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void BuildGlow(RectTransform root)
        {
            // Centred on the board, which is also where the grid converges: one focal point, so the
            // eye is pulled to the same place by the light and by the perspective.
            _glow = UiBuilder.Image(root, "Glow", Art.Pool, Design.BgGlow.WithAlpha(Design.GlowAlpha));
            _glow.type = Image.Type.Simple;

            _glowRect = _glow.rectTransform;
            _glowRect.sizeDelta = new Vector2(GlowSize, GlowSize);
            // The backdrop is full-bleed while the page is inset, and the insets are not symmetric
            // (a cutout on top, the gesture bar below), so the page centre is not the screen centre.
            var insets = AppController.SafeInsets;
            _glowRect.anchoredPosition = new Vector2(0f, GameScreen.BoardCenterY + (insets.y - insets.w) * 0.5f);

            // A second, wider pool high up behind the score: the stage colour needs somewhere to
            // show that the board does not cover. Invisible until a run has a light of its own.
            _aura = UiBuilder.Image(root, "Aura", Art.Pool, Color.clear);
            _aura.type = Image.Type.Simple;
            _aura.rectTransform.sizeDelta = new Vector2(GlowSize * 1.25f, GlowSize * 0.9f);
            _aura.rectTransform.anchoredPosition = new Vector2(0f, GameScreen.BoardCenterY + 820f + (insets.y - insets.w) * 0.5f);

            // And one low, behind the tray, so the colour holds the page from top to bottom.
            _floor = UiBuilder.Image(root, "Floor", Art.Pool, Color.clear);
            _floor.type = Image.Type.Simple;
            // Both pools are fully clear outside a lit run; a clear mesh is then not drawn at all.
            _floor.canvasRenderer.cullTransparentMesh = true;
            _aura.canvasRenderer.cullTransparentMesh = true;
            _floor.rectTransform.sizeDelta = new Vector2(GlowSize * 1.3f, GlowSize * 0.75f);
            _floor.rectTransform.anchoredPosition = new Vector2(0f, GameScreen.BoardCenterY - 900f + (insets.y - insets.w) * 0.5f);
        }

        void Update()
        {
            // An Editor domain reload wipes these plain fields without re-running Build, so the
            // backdrop idles instead of throwing every frame.
            if (_glowRect == null) return;

            // Heat breathes a little larger: the room leaning in with the streak.
            float breathe = 1f + BreatheDepth * Mathf.Sin(Time.unscaledTime * BreatheSpeed) + 0.08f * _heat;
            _glowRect.localScale = new Vector3(breathe, breathe, 1f);

            UpdateLight(Time.unscaledDeltaTime);
        }

        void UpdateLight(float dt)
        {
            float wantAmount = _tintTarget == null ? 0f : 1f;
            bool moving = _dirty || _flashLeft > 0f || _rainbowLeft > 0f || _prismTop
                          || !Mathf.Approximately(_tintAmount, wantAmount) || !Mathf.Approximately(_heat, _heatTarget);
            if (!moving) return;
            _dirty = false;

            // About a second and a half to settle: a change of stage should be watched, not noticed late.
            float ease = 1f - Mathf.Exp(-dt * 2.2f);
            _tintAmount = Mathf.MoveTowards(_tintAmount, wantAmount, dt * 0.8f);
            if (_tintTarget != null)
            {
                var target = _prismTop ? Spectrum.Prism(Time.unscaledTime * 0.05f) : _tintTarget.Value;
                _tint = Color.Lerp(_tint, target, _prismTop ? 1f : ease);
                if (!_prismTop && Vector4.Distance(_tint, target) > 0.002f) _dirty = true;
            }
            _heat = Mathf.MoveTowards(_heat, _heatTarget, dt * (_heatTarget > _heat ? 1.5f : 0.5f));

            // Heat pulls the light towards gold: a warm room while the streak lasts.
            var light = Color.Lerp(_tint, Design.Gold, 0.35f * _heat);
            float amount = Mathf.Max(_tintAmount, 0.7f * _heat);

            if (_rainbowLeft > 0f)
            {
                _rainbowLeft -= dt;
                float k = 1f - Mathf.Clamp01(_rainbowLeft / RainbowLength);
                light = Color.Lerp(Spectrum.Prism(k), light, k * k);
                amount = Mathf.Max(amount, 1f - k * k);
            }

            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                float k = Mathf.Clamp01(_flashLeft / _flashLength);
                light = Color.Lerp(light, _flash, k);
                amount = Mathf.Max(amount, k);
            }

            // Mixed into the theme's glow rather than replacing it: the theme stays recognisable,
            // the stage reads as the light it is lit by. Alphas are low on purpose: the project
            // blends in linear space, where a little alpha shows far brighter than it reads here.
            var glow = Color.Lerp(Design.BgGlow, light, 0.6f * amount);
            _glow.color = glow.WithAlpha(Mathf.Clamp01(Design.GlowAlpha * (1f + 0.5f * _heat + 0.25f * amount)));

            // Measured off screenshots on the bright default ground: at 0.2 the aura read as a
            // white haze and a stage change as nothing. The light has to reach the ground itself.
            _aura.color = light.WithAlpha(0.36f * amount + 0.08f * _heat);
            // Kept under the aura: the tray pieces sit on this part of the ground and must not sink into it.
            _floor.color = light.WithAlpha(0.2f * amount + 0.05f * _heat);
            _deep.color = Color.Lerp(Design.BgBottom, light * 0.55f, 0.3f * amount).WithAlpha(Design.BgBottom.a);

            var grid = Design.BgGrid;
            _grid.color = Color.Lerp(grid, light, 0.45f * amount).WithAlpha(grid.a * (1f + 0.6f * amount));
        }
    }
}
