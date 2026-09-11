using System;
using BlockPuzzle.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Root of the whole game. Owns the canvas, the shared backdrop, audio, and the screen stack,
    /// and is the single component that has to exist in the scene. Every way into a run goes
    /// through here, so resuming a saved run and starting a fresh one live in one place.
    /// </summary>
    public sealed class AppController : MonoBehaviour
    {
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;

        /// <summary>
        /// Android runs an app at 30 fps unless it asks for more — this line is the difference
        /// between a drag that follows the finger and one that trails it.
        /// </summary>
        const int TargetFrameRate = 60;

        [Header("Setup")]
        [SerializeField] int _boardSize = 8;

        Canvas _canvas;
        RectTransform _root;
        RectTransform _pageLayer;
        RectTransform _modalLayer;

        MainMenuScreen _menu;
        GameScreen _game;
        LevelSelectScreen _levels;
        SettingsScreen _settings;
        ScoresScreen _scores;
        PauseScreen _pause;
        StatsScreen _stats;
        ThemesScreen _themes;

        AppScreen _currentPage;

        // A stack, not a single slot: settings opened from the pause menu must return to it.
        readonly System.Collections.Generic.List<AppScreen> _modals = new System.Collections.Generic.List<AppScreen>();

        public AudioKit Audio { get; private set; }
        public MusicPlayer Music { get; private set; }

        public int BoardSize => _boardSize;
        public float CanvasScale => _canvas == null || _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;

        void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;

            ConfigureCamera();
            BuildCanvas();

            Audio = gameObject.AddComponent<AudioKit>();
            Music = gameObject.AddComponent<MusicPlayer>();
            gameObject.AddComponent<PointerRouter>();

            var backdropGo = new GameObject("Backdrop", typeof(RectTransform));
            backdropGo.AddComponent<Backdrop>().Build(_root);

            _pageLayer = UiBuilder.Child(_root, "Pages");
            _modalLayer = UiBuilder.Child(_root, "Modals");

            _menu = CreateScreen<MainMenuScreen>("MainMenu", _pageLayer);
            _game = CreateScreen<GameScreen>("Game", _pageLayer);
            _levels = CreateScreen<LevelSelectScreen>("Levels", _pageLayer);
            _settings = CreateScreen<SettingsScreen>("Settings", _modalLayer);
            _scores = CreateScreen<ScoresScreen>("Scores", _modalLayer);
            _pause = CreateScreen<PauseScreen>("Pause", _modalLayer);
            _stats = CreateScreen<StatsScreen>("Stats", _modalLayer);
            _themes = CreateScreen<ThemesScreen>("Themes", _modalLayer);

            ShowMenu();

#if PRIZMA_AUTOTEST
            gameObject.AddComponent<AutoTest>();
#endif
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("AppCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            // Expand never crops: it takes the smaller of the two axis ratios, so the whole
            // 1080x1920 reference area is on screen whatever the aspect and the surplus becomes
            // margin. Matching height instead looked right at 16:9 and cut 52 units off each side
            // of the board on a 20:9 phone — along with the best-score readout and the pause button.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _root = (RectTransform)canvasGo.transform;
        }

        /// <summary>
        /// The whole game is an overlay canvas, so the camera's only job is to clear the screen.
        /// Left at its defaults it drew a skybox into an HDR buffer at 80% scale, ran bloom,
        /// tonemapping and the rest of the post stack over it, then upscaled the result — every
        /// frame, entirely hidden behind the backdrop. Now it clears to the ground colour, which
        /// also stands in for the backdrop's bottom layer.
        /// </summary>
        void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Camera");
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Design.BgTop;
            cam.cullingMask = 0;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.volumeLayerMask = 0;
            cam.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
        }

        T CreateScreen<T>(string name, RectTransform layer) where T : AppScreen
        {
            var go = new GameObject(name, typeof(RectTransform));
            var screen = go.AddComponent<T>();
            screen.Init(this, layer);
            return screen;
        }

        // ------------------------------------------------------------------ runs

        /// <summary>Classic: resumes the saved run if there is one, unless a fresh start was asked for.</summary>
        public void PlayClassic(bool fresh = false)
        {
            if (fresh) RunStore.Clear(GameMode.Classic);

            var session = RunStore.Load(GameMode.Classic)
                          ?? GameSession.NewRandomRun(_boardSize, Design.PaletteSize);
            Begin(session);
        }

        /// <summary>The daily puzzle. A saved daily run only resumes on the day it was started.</summary>
        public void PlayDaily(bool fresh = false)
        {
            if (fresh) RunStore.Clear(GameMode.Daily);

            var session = RunStore.Load(GameMode.Daily);
            if (session == null || session.Seed != GameSession.DailySeed(DateTime.Now))
                session = GameSession.NewDailyRun(DateTime.Now, _boardSize, Design.PaletteSize);

            Begin(session);
        }

        /// <summary>A level. Resumes a saved attempt at the same level; anything else starts clean.</summary>
        public void PlayLevel(int number)
        {
            number = Mathf.Clamp(number, 1, Progress.UnlockedLevel);

            var saved = RunStore.Load(GameMode.Level);
            GameSession session = saved != null && saved.Level != null && saved.Level.Number == number
                ? saved
                : GameSession.NewLevelRun(LevelGenerator.Generate(number, Design.PaletteSize), _boardSize, Design.PaletteSize);

            Begin(session);
        }

        /// <summary>Throws the current run away and starts the same kind of run again.</summary>
        public void RestartRun()
        {
            var session = _game.Session;
            if (session == null)
            {
                PlayClassic(fresh: true);
                return;
            }

            RunStore.Clear(session.Mode);
            switch (session.Mode)
            {
                case GameMode.Daily: PlayDaily(fresh: true); break;
                case GameMode.Level: PlayLevel(session.Level.Number); break;
                default: PlayClassic(fresh: true); break;
            }
        }

        /// <summary>Back out of a run to where it was started from. The run stays saved.</summary>
        public void LeaveRun()
        {
            var session = _game.Session;
            if (session != null && session.Mode == GameMode.Level) ShowLevelSelect();
            else ShowMenu();
        }

        void Begin(GameSession session)
        {
            ShowPage(_game);
            _game.Begin(session);
        }

        // ------------------------------------------------------------------ navigation

        public void ShowMenu() => ShowPage(_menu);

        public void ShowLevelSelect() => ShowPage(_levels);

        public void OpenSettings() => ShowModal(_settings);

        public void OpenScores() => ShowModal(_scores);

        public void OpenPause() => ShowModal(_pause);

        public void OpenStats() => ShowModal(_stats);

        public void OpenThemes() => ShowModal(_themes);

        /// <summary>Dismisses the top modal, falling back to the one beneath it if there is one.</summary>
        public void CloseModal()
        {
            if (_modals.Count == 0) return;

            int top = _modals.Count - 1;
            _modals[top].Hide();
            PointerRouter.PopBlocker(_modals[top].RootRect);
            _modals.RemoveAt(top);

            if (_modals.Count > 0)
            {
                _modals[_modals.Count - 1].Show();
                return;
            }

            // Hand board input back to whatever page is underneath.
            if (_currentPage is IPointerFallback fallback)
                PointerRouter.Fallback = fallback;
        }

        void CloseAllModals()
        {
            for (int i = _modals.Count - 1; i >= 0; i--)
            {
                _modals[i].Hide();
                PointerRouter.PopBlocker(_modals[i].RootRect);
            }

            _modals.Clear();
        }

        void ShowPage(AppScreen page)
        {
            CloseAllModals();

            if (_currentPage != null && _currentPage != page)
                _currentPage.Hide();

            _currentPage = page;
            page.Show();
        }

        void ShowModal(AppScreen modal)
        {
            if (_modals.Contains(modal)) return;

            if (_modals.Count > 0) _modals[_modals.Count - 1].Hide();

            // A modal owns the pointer outright: neither the board nor any button behind it reacts.
            PointerRouter.Fallback = null;

            _modals.Add(modal);
            modal.Show();
            PointerRouter.PushBlocker(modal.RootRect);
        }

        // ------------------------------------------------------------------ platform

        /// <summary>A haptic pulse for a big moment — multi-line clears, the end of a run. Honours the setting.</summary>
        public void Vibrate(bool strong = false)
        {
            if (GameSettings.Haptics) Haptics.Pulse(strong ? 45 : 22);
        }

        /// <summary>The lightest tick there is, for a piece landing. Felt more than noticed.</summary>
        public void Tick()
        {
            if (GameSettings.Haptics) Haptics.Pulse(8, 70);
        }

        void Update()
        {
            // Android back button: close the top modal; in a run, let the page back out of a result
            // card or an armed power before pausing; elsewhere go home.
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_modals.Count > 0) CloseModal();
                else if (_currentPage == _game) { if (!_game.HandleBack()) OpenPause(); }
                else if (_currentPage == _levels) ShowMenu();
                else if (_currentPage == _menu) SendToBackground();
            }
        }

        /// <summary>
        /// Back on the title page leaves the app the way Android expects: sent to the background,
        /// not killed, so returning to it is instant and nothing is lost.
        /// </summary>
        static void SendToBackground()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    activity.Call<bool>("moveTaskToBack", true);
            }
            catch (System.Exception)
            {
                Application.Quit();
            }
#endif
        }
    }
}
