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

        // Drawn by one shader in one opaque pass (PrizmaBackdrop) where the device runs it; the
        // separate images above are the fallback. Either way the same layers, colours and order.
        RectTransform _root;
        Material _single;
        float _breathe = 1f;
        Vector2 _glowAt, _auraAt, _floorAt;
        Vector2 _glowSize, _auraSize, _floorSize;

        static readonly int DeepId = Shader.PropertyToID("_Deep");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int AuraId = Shader.PropertyToID("_Aura");
        static readonly int FloorId = Shader.PropertyToID("_Floor");
        static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        static readonly int GlowRectId = Shader.PropertyToID("_GlowRect");
        static readonly int AuraRectId = Shader.PropertyToID("_AuraRect");
        static readonly int FloorRectId = Shader.PropertyToID("_FloorRect");
        static readonly int SizeId = Shader.PropertyToID("_Size");

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
            _root = root;

            // Its own canvas: the glow breathes every frame, and on the shared canvas that
            // re-batched the whole UI every frame too. Nested this way only these few layers do.
            var canvas = gameObject.AddComponent<Canvas>();

            PlacePools();

            var shader = Resources.Load<Shader>("Shaders/PrizmaBackdrop");
            if (shader != null && shader.isSupported) BuildSingle(root, shader, canvas);
            else BuildLayers(root);

            Current = this;
            _tint = Design.BgGlow;
        }

        /// <summary>
        /// Where the three light pools sit. Centred on the board, which is also where the grid
        /// converges: one focal point, so the eye is pulled to the same place by the light and by
        /// the perspective. The backdrop is full-bleed while the page is inset, and the insets are
        /// not symmetric (a cutout on top, the gesture bar below), so the page centre is not the
        /// screen centre.
        /// </summary>
        void PlacePools()
        {
            var insets = AppController.SafeInsets;
            float centre = GameScreen.BoardCenterY + (insets.y - insets.w) * 0.5f;

            _glowSize = new Vector2(GlowSize, GlowSize);
            _glowAt = new Vector2(0f, centre);

            // A second, wider pool high up behind the score: the stage colour needs somewhere to
            // show that the board does not cover. Invisible until a run has a light of its own.
            _auraSize = new Vector2(GlowSize * 1.25f, GlowSize * 0.9f);
            _auraAt = new Vector2(0f, centre + 820f);

            // And one low, behind the tray, so the colour holds the page from top to bottom.
            _floorSize = new Vector2(GlowSize * 1.3f, GlowSize * 0.75f);
            _floorAt = new Vector2(0f, centre - 900f);
        }

        /// <summary>
        /// Every layer in one opaque pass. Stacked as images they were seven full-screen blends a
        /// frame — on a weak phone GPU more fill rate than the whole rest of the interface.
        /// </summary>
        void BuildSingle(RectTransform root, Shader shader, Canvas canvas)
        {
            _single = new Material(shader) { name = "Backdrop", hideFlags = HideFlags.HideAndDontSave };
            _single.SetTexture("_Fade", Art.VerticalFade.texture);
            _single.SetTexture("_Pool", Art.Pool.texture);
            _single.SetTexture("_Grid", Art.Grid.texture);
            _single.SetTexture("_Vignette", Art.Vignette.texture);
            _single.SetTexture("_Grain", Art.Grain.texture);
            // Under everything: the camera's clear colour, which is what the image layers lay over.
            SetColour("_Base", Design.BgTop);
            SetColour("_VignetteColor", Design.BgVignette.WithAlpha(VignetteAlpha));
            SetColour("_GrainColor", Color.white.WithAlpha(GrainAlpha));

            // The grain was a tiled image: one tile is the sprite's size at the canvas's pixel density.
            var grain = Art.Grain;
            float density = grain.pixelsPerUnit / Mathf.Max(0.0001f, canvas.referencePixelsPerUnit);
            var tile = grain.rect.size / density;
            _single.SetVector("_GrainTile", new Vector4(tile.x, tile.y, 0f, 0f));

            var surface = UiBuilder.Image(root, "Surface", null, Color.white);
            surface.type = Image.Type.Simple;
            surface.material = _single;
            UiBuilder.Stretch(surface.rectTransform);

            Paint(Design.BgBottom, Design.BgGlow.WithAlpha(Design.GlowAlpha), Color.clear, Color.clear, Design.BgGrid);
            PlaceSingle();
        }

        /// <summary>The layers as separate images — for a device that cannot run the backdrop shader.</summary>
        void BuildLayers(RectTransform root)
        {

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
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_single != null) Destroy(_single);
        }

        /// <summary>The colours of the layers the light moves.</summary>
        void Paint(Color deep, Color glow, Color aura, Color floor, Color grid)
        {
            if (_single != null)
            {
                SetColour(DeepId, deep);
                SetColour(GlowId, glow);
                SetColour(AuraId, aura);
                SetColour(FloorId, floor);
                SetColour(GridColorId, grid);
                return;
            }

            _deep.color = deep;
            _glow.color = glow;
            _aura.color = aura;
            _floor.color = floor;
            _grid.color = grid;
        }

        // In linear space, as the canvas hands its vertex colours to the GPU. SetColor converts only
        // the properties a shader declares as colours; these are not declared, and passed as they
        // were, every layer came out a gamma step too light.
        void SetColour(int id, Color colour) => _single.SetVector(id, colour.linear);
        void SetColour(string name, Color colour) => _single.SetVector(name, colour.linear);

        /// <summary>The pools' rectangles, measured from the backdrop's bottom-left corner.</summary>
        void PlaceSingle()
        {
            var size = _root.rect.size;
            _single.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
            _single.SetVector(GlowRectId, PoolRect(size, _glowAt, _glowSize * _breathe));
            _single.SetVector(AuraRectId, PoolRect(size, _auraAt, _auraSize));
            _single.SetVector(FloorRectId, PoolRect(size, _floorAt, _floorSize));
        }

        static Vector4 PoolRect(Vector2 size, Vector2 at, Vector2 extent)
        {
            var min = size * 0.5f + at - extent * 0.5f;
            return new Vector4(min.x, min.y, extent.x, extent.y);
        }

        void BuildGlow(RectTransform root)
        {
            _glow = UiBuilder.Image(root, "Glow", Art.Pool, Design.BgGlow.WithAlpha(Design.GlowAlpha));
            _glow.type = Image.Type.Simple;
            _glowRect = _glow.rectTransform;
            _glowRect.sizeDelta = _glowSize;
            _glowRect.anchoredPosition = _glowAt;

            _aura = UiBuilder.Image(root, "Aura", Art.Pool, Color.clear);
            _aura.type = Image.Type.Simple;
            _aura.rectTransform.sizeDelta = _auraSize;
            _aura.rectTransform.anchoredPosition = _auraAt;

            _floor = UiBuilder.Image(root, "Floor", Art.Pool, Color.clear);
            _floor.type = Image.Type.Simple;
            // Both pools are fully clear outside a lit run; a clear mesh is then not drawn at all.
            _floor.canvasRenderer.cullTransparentMesh = true;
            _aura.canvasRenderer.cullTransparentMesh = true;
            _floor.rectTransform.sizeDelta = _floorSize;
            _floor.rectTransform.anchoredPosition = _floorAt;
        }

        void Update()
        {
            // An Editor domain reload wipes these plain fields without re-running Build, so the
            // backdrop idles instead of throwing every frame.
            if (_glowRect == null && _single == null) return;

            // Heat breathes a little larger: the room leaning in with the streak.
            _breathe = 1f + BreatheDepth * Mathf.Sin(Time.unscaledTime * BreatheSpeed) + 0.08f * _heat;
            if (_single != null) PlaceSingle();
            else _glowRect.localScale = new Vector3(_breathe, _breathe, 1f);

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
            var glow = Color.Lerp(Design.BgGlow, light, 0.6f * amount)
                .WithAlpha(Mathf.Clamp01(Design.GlowAlpha * (1f + 0.5f * _heat + 0.25f * amount)));

            // Measured off screenshots on the bright default ground: at 0.2 the aura read as a
            // white haze and a stage change as nothing. The light has to reach the ground itself.
            var aura = light.WithAlpha(0.36f * amount + 0.08f * _heat);
            // Kept under the aura: the tray pieces sit on this part of the ground and must not sink into it.
            var floor = light.WithAlpha(0.2f * amount + 0.05f * _heat);
            var deep = Color.Lerp(Design.BgBottom, light * 0.55f, 0.3f * amount).WithAlpha(Design.BgBottom.a);

            var grid = Design.BgGrid;
            grid = Color.Lerp(grid, light, 0.45f * amount).WithAlpha(grid.a * (1f + 0.6f * amount));

            Paint(deep, glow, aura, floor, grid);
        }
    }
}
