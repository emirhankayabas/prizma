using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Root of the whole game. Owns the canvas, the shared backdrop, audio, and the screen stack,
    /// and is the single component that has to exist in the scene.
    /// </summary>
    public sealed class AppController : MonoBehaviour
    {
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;

        [Header("Setup")]
        [SerializeField] int _boardSize = 8;

        Canvas _canvas;
        RectTransform _root;
        RectTransform _pageLayer;
        RectTransform _modalLayer;

        MainMenuScreen _menu;
        GameScreen _game;
        SettingsScreen _settings;
        ScoresScreen _scores;
        PauseScreen _pause;

        AppScreen _currentPage;

        // A stack, not a single slot: settings opened from the pause menu must return to it.
        readonly System.Collections.Generic.List<AppScreen> _modals = new System.Collections.Generic.List<AppScreen>();

        public AudioKit Audio { get; private set; }
        public MusicPlayer Music { get; private set; }

        public int BoardSize => _boardSize;
        public float CanvasScale => _canvas == null || _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;

        void Awake()
        {
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
            _settings = CreateScreen<SettingsScreen>("Settings", _modalLayer);
            _scores = CreateScreen<ScoresScreen>("Scores", _modalLayer);
            _pause = CreateScreen<PauseScreen>("Pause", _modalLayer);

            ShowMenu();
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

        T CreateScreen<T>(string name, RectTransform layer) where T : AppScreen
        {
            var go = new GameObject(name, typeof(RectTransform));
            var screen = go.AddComponent<T>();
            screen.Init(this, layer);
            return screen;
        }

        // ------------------------------------------------------------------ navigation

        public void ShowMenu() => ShowPage(_menu);

        public void StartGame()
        {
            ShowPage(_game);
            _game.StartNewRun();
        }

        public void OpenSettings() => ShowModal(_settings);

        public void OpenScores() => ShowModal(_scores);

        public void OpenPause() => ShowModal(_pause);

        /// <summary>Dismisses the top modal, falling back to the one beneath it if there is one.</summary>
        public void CloseModal()
        {
            if (_modals.Count == 0) return;

            int top = _modals.Count - 1;
            _modals[top].Hide();
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
                _modals[i].Hide();

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

            // A modal owns the pointer outright: the board must not react behind it.
            PointerRouter.Fallback = null;

            _modals.Add(modal);
            modal.Show();
        }

        // ------------------------------------------------------------------ platform

        /// <summary>Short haptic tap, honouring the player's setting. No-op in the Editor.</summary>
        public void Vibrate()
        {
            if (!GameSettings.Haptics) return;

#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor) Handheld.Vibrate();
#endif
        }

        void Update()
        {
            // Android back button: close the top modal, or pause from within a run.
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_modals.Count > 0) CloseModal();
                else if (_currentPage == _game) OpenPause();
            }
        }
    }
}
