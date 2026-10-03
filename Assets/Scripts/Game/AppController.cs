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
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>
        /// Bottom margin kept clear for the system gesture bar, in canvas units.
        ///
        /// <c>Screen.safeArea</c> is not enough on its own: on Android it reports display cutouts
        /// but usually not the gesture handle, which sits over the app. A drag started on the
        /// bottom row of the tray was landing inside it — measured off a phone screenshot, the
        /// tray finished 89 px from the bottom edge, well inside the ~48dp handle.
        ///
        /// The figure is in canvas units rather than dp on purpose. The scaler matches width, so
        /// a canvas unit is a fixed fraction of the physical width whatever the density: 1080
        /// units span a phone about 392dp wide, which puts 48dp at roughly 130 units.
        /// </summary>
        const float GestureBarMargin = 130f;

        /// <summary>
        /// Android runs an app at 30 fps unless it asks for more — this line is the difference
        /// between a drag that follows the finger and one that trails it.
        /// </summary>
        const int TargetFrameRate = 60;

        [Header("Setup")]
        [SerializeField] int _boardSize = 8;

        Canvas _canvas;
        Camera _camera;
        RectTransform _root;
        GameObject _backdrop;
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
        LevelStartScreen _levelStart;
        ChestScreen _chest;

        AppScreen _currentPage;

        // A stack, not a single slot: settings opened from the pause menu must return to it.
        readonly System.Collections.Generic.List<AppScreen> _modals = new System.Collections.Generic.List<AppScreen>();

        public AudioKit Audio { get; private set; }
        public MusicPlayer Music { get; private set; }

        public int BoardSize => _boardSize;

        /// <summary>The play page. Replaced when the interface is rebuilt for a new theme.</summary>
        public GameScreen Game => _game;

#if PRIZMA_AUTOTEST
        public ThemesScreen ThemesPage => _themes;
#endif
        public float CanvasScale => _canvas == null || _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;

        /// <summary>
        /// Height of a page in canvas units once the notch and the gesture bar are taken out.
        /// Computed from the scaler's own rule rather than read off a RectTransform, because
        /// screens are built during Awake and layout has not run yet.
        /// </summary>
        public float PageHeight { get; private set; } = ReferenceHeight;

        /// <summary>
        /// Left, bottom, right, top — canvas units. Static because a full-screen scrim has to be
        /// able to bleed back out over the notch and the gesture bar, and there is only ever one
        /// of these.
        /// </summary>
        public static Vector4 SafeInsets { get; private set; }

        Vector4 _safeInsets;
        Rect _measuredSafeArea;
        Vector2Int _measuredScreen;

        void Awake()
        {
            // One breadcrumb in logcat. An APK once started into an empty sky because the scene
            // could not resolve this component; without a line like this, "the game never ran"
            // and "the game crashed on the first frame" look identical on a phone.
            Debug.Log($"[PRIZMA] AppController.Awake — {Application.version} / {Application.platform}");

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            var boot = System.Diagnostics.Stopwatch.StartNew();

            ConfigureCamera();
            BuildCanvas();
            MeasureSafeArea();

            // The page metrics have to be known before anything is built: the backdrop's glow and
            // the board both sit at GameScreen.BoardCenterY, which depends on them.
            GameScreen.SetPageHeight(PageHeight);

            Audio = gameObject.AddComponent<AudioKit>();
            Music = gameObject.AddComponent<MusicPlayer>();
            gameObject.AddComponent<PointerRouter>();
            Str.ApplyFont();
#if PRIZMA_AUTOTEST
            Debug.Log($"[PRIZMA] audio + canvas: {boot.ElapsedMilliseconds} ms");
#endif

            BuildInterface();
            ShowMenu();
            // The frozen frame behind the splash: everything drawn and built before the first one.
            Debug.Log($"[PRIZMA] boot: {boot.ElapsedMilliseconds} ms");

            // Today's puzzle is built while the menu is up, so the daily starts on the first tap.
            var today = DateTime.Now.Date;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => LevelGenerator.GenerateDaily(today, Design.PaletteSize));
            Reminder.Refresh();

            // Over the menu rather than before it: the menu is already built and laid out behind
            // the splash, so when the splash lifts there is nothing left to wait for.
            BootSplash.Play(_root);
            StartCoroutine(AskNotificationsOnFirstLaunch());

#if PRIZMA_AUTOTEST
            gameObject.AddComponent<AutoTest>();
#endif
        }

        /// <summary>
        /// The backdrop, the two layers and every screen, in the current theme. Colours are read
        /// while building, so this is also how a theme change reaches the whole screen.
        /// </summary>
        void BuildInterface()
        {
            if (_camera != null) _camera.backgroundColor = Design.BgTop;

#if PRIZMA_AUTOTEST
            var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
            _backdrop = new GameObject("Backdrop", typeof(RectTransform));
            _backdrop.AddComponent<Backdrop>().Build(_root);
#if PRIZMA_AUTOTEST
            Debug.Log($"[PRIZMA] built Backdrop: {watch.ElapsedMilliseconds} ms");
#endif

            _pageLayer = UiBuilder.Child(_root, "Pages");
            _modalLayer = UiBuilder.Child(_root, "Modals");
            ApplySafeArea();

            _menu = CreateScreen<MainMenuScreen>("MainMenu", _pageLayer);
            _game = CreateScreen<GameScreen>("Game", _pageLayer);
            _levels = CreateScreen<LevelSelectScreen>("Levels", _pageLayer);
            _settings = CreateScreen<SettingsScreen>("Settings", _modalLayer);
            _scores = CreateScreen<ScoresScreen>("Scores", _modalLayer);
            _pause = CreateScreen<PauseScreen>("Pause", _modalLayer);
            _stats = CreateScreen<StatsScreen>("Stats", _modalLayer);
            _themes = CreateScreen<ThemesScreen>("Themes", _modalLayer);
            _daily = CreateScreen<DailyScreen>("Daily", _modalLayer);
            _levelStart = CreateScreen<LevelStartScreen>("LevelStart", _modalLayer);
            _chest = CreateScreen<ChestScreen>("Chest", _modalLayer);
        }

        DailyScreen _daily;

        public void OpenDaily() => ShowModal(_daily);

        /// <summary>
        /// The notification permission, asked once, on the very first launch, while the splash is
        /// up: the system's own dialog and nothing of ours around it. The game used to offer the
        /// reminder on a card of its own over the first solve, and keep a switch for it on the
        /// daily card; both are gone. The answer can be changed in settings.
        ///
        /// Two frames in rather than from Awake: the permission request needs the activity to be
        /// in front, which it is not yet while the first scene is still loading.
        /// </summary>
        System.Collections.IEnumerator AskNotificationsOnFirstLaunch()
        {
            if (GameSettings.ReminderAsked) yield break;
            yield return null;
            yield return null;
            Reminder.Enable(null);
        }

        /// <summary>True while any modal is open — the daily's clock does not run behind one.</summary>
        public bool HasModal => _modals.Count > 0;

#if PRIZMA_AUTOTEST
        public StatsScreen StatsPage => _stats;
        public DailyScreen DailyPage => _daily;
        public LevelSelectScreen LevelsPage => _levels;
        public LevelStartScreen LevelStartPage => _levelStart;
#endif

        // ------------------------------------------------------------------ theme

        /// <summary>
        /// Switches to a theme and rebuilds the interface in it, leaving the player exactly where
        /// they were — the same page, the same modals open, a run carried over.
        ///
        /// A rebuild rather than recolouring in place: colours are taken at build time all over
        /// the UI (fills, rest colours kept for highlights, scrims, the backdrop), and one missed
        /// graphic would leave a patch of the old theme on screen. Building everything again is
        /// the one way that cannot miss anything, and it costs about what the first launch did,
        /// once, on a tap in a menu.
        /// </summary>
        public void SetTheme(int index)
        {
            if (index == Progress.Theme || !Themes.IsUnlocked(index)) return;

            Progress.Theme = index;
            RebuildInterface();
        }

        void RebuildInterface()
        {
            // Where the player is, by kind of screen: the screens themselves are about to go.
            bool onGame = _currentPage == _game;
            bool onLevels = _currentPage == _levels;
            var session = onGame ? _game.Session : null;

            var modalKinds = new System.Collections.Generic.List<Type>();
            foreach (var modal in _modals) modalKinds.Add(modal.GetType());

            CloseAllModals();
            _currentPage?.Hide(); // the play page saves its run on the way out
            _currentPage = null;
            PointerRouter.Fallback = null;

            // Switched off first: Destroy only lands at the end of the frame, and until then the
            // old widgets would still be registered for input and the old graphics still drawn.
            foreach (var old in new[] { _backdrop, _pageLayer.gameObject, _modalLayer.gameObject })
            {
                old.SetActive(false);
                Destroy(old);
            }

            BuildInterface();

            if (onGame && session != null)
            {
                ShowPage(_game, animate: false);
                _game.Begin(session);
            }
            else
            {
                ShowPage(onLevels ? (AppScreen)_levels : _menu, animate: false);
            }

            foreach (var kind in modalKinds)
            {
                var modal = ModalOfKind(kind);
                if (modal != null) ShowModal(modal, animate: false);
            }
        }

        AppScreen ModalOfKind(Type kind)
        {
            foreach (var modal in new AppScreen[] { _settings, _scores, _pause, _stats, _themes, _daily, _levelStart, _chest })
                if (modal.GetType() == kind) return modal;
            return null;
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

            _camera = cam;

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

            // Not from Awake: the volume system is set up by the render pipeline, which may not
            // exist yet this early, and URP logs an error if the mode is set before it does.
            StartCoroutine(StopVolumeUpdatesWhenReady(cam));
        }

        static System.Collections.IEnumerator StopVolumeUpdatesWhenReady(Camera cam)
        {
            for (int i = 0; i < 60 && !UnityEngine.Rendering.VolumeManager.instance.isInitialized; i++)
                yield return null;

            if (cam != null && UnityEngine.Rendering.VolumeManager.instance.isInitialized)
                cam.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
        }

        T CreateScreen<T>(string name, RectTransform layer) where T : AppScreen
        {
#if PRIZMA_AUTOTEST
            var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
            var go = new GameObject(name, typeof(RectTransform));
            var screen = go.AddComponent<T>();
            screen.Init(this, layer);
#if PRIZMA_AUTOTEST
            Debug.Log($"[PRIZMA] built {name}: {watch.ElapsedMilliseconds} ms");
#endif
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

        /// <summary>
        /// The day whose puzzle the daily run on the board is. Today when it started; a run carried
        /// past midnight keeps the day it began on, and is recorded for that day.
        /// </summary>
        public DateTime DailyDate { get; private set; } = DateTime.Now.Date;

        /// <summary>
        /// Today's puzzle — the only one there is. Past days cannot be played: a missed day stays
        /// missed, which is what gives the streak its weight. A solved day is not replayed either;
        /// the daily card, with the countdown to the next one, opens instead.
        /// </summary>
        public void PlayDaily(bool fresh = false)
        {
            var today = DateTime.Now.Date;
            if (Progress.SolvedDailyToday)
            {
                if (_currentPage == _game) ShowMenu();
                OpenDaily();
                return;
            }

            int number = LevelGenerator.DailyNumber(today);
            if (fresh) RunStore.Clear(GameMode.Daily);

            // A saved attempt resumes only on its own day, and only if it is a puzzle — a daily
            // saved by the old endless daily has no level and is dropped.
            var session = RunStore.Load(GameMode.Daily);
            if (session == null || session.Level == null || session.Level.Number != number)
            {
                RunStore.Clear(GameMode.Daily);
                session = GameSession.NewDailyRun(LevelGenerator.GenerateDaily(today, Design.PaletteSize), _boardSize, Design.PaletteSize);
                Progress.DailyAttemptStarted(today);
            }

            DailyDate = today;
            Begin(session);
        }

        /// <summary>Switches the interface language and rebuilds it in place, like a theme change.</summary>
        public void SetLanguage(string language)
        {
            if (GameSettings.Language == language || !Str.Has(language)) return;
            GameSettings.Language = language;
            Str.ApplyFont();
            RebuildInterface();
        }

        /// <summary>
        /// A level. Resumes a saved attempt at the same level; anything else starts clean, with the
        /// win streak's gift and whichever boosters the start sheet took — spent here, only when a
        /// fresh attempt really begins.
        /// </summary>
        public void PlayLevel(int number, bool boostMoves = false, bool boostCharge = false)
        {
            number = Mathf.Clamp(number, 1, Progress.UnlockedLevel);

            var saved = RunStore.Load(GameMode.Level);
            GameSession session;
            if (saved != null && saved.Level != null && saved.Level.Number == number)
            {
                session = saved;
            }
            else
            {
                var (moves, charges) = Progress.StreakBonus(Progress.WinStreak);
                if (boostMoves && Progress.SpendBooster(Booster.Moves)) moves += 3;
                if (boostCharge && Progress.SpendBooster(Booster.Charge)) charges += 1;

                session = GameSession.NewLevelRun(LevelGenerator.Generate(number, Design.PaletteSize), _boardSize,
                    Design.PaletteSize, charges, moves);
            }

            Begin(session);
        }

        /// <summary>A level's sheet: its goal, the streak and the boosters, and the button that starts it.</summary>
        public void OpenLevelStart(int number)
        {
            if (_levelWait != null) StopCoroutine(_levelWait);
            _levelWait = null;

            // The sheet reads the level as it opens. Usually it is built already (the map builds the
            // next one ahead); otherwise it is built on a worker and the sheet opens when it is ready,
            // instead of the build freezing the map for its whole length on a slow phone.
            if (LevelGenerator.IsReady(number)) ShowLevelStart(number);
            else _levelWait = StartCoroutine(OpenLevelStartWhenReady(number));
        }

        Coroutine _levelWait;

        System.Collections.IEnumerator OpenLevelStartWhenReady(int number)
        {
            var page = _currentPage;
            int modals = _modals.Count;
            LevelGenerator.Prefetch(number, Design.PaletteSize);

            // Bounded: the sheet would build it itself after that, as it always did.
            float waited = 0f;
            while (!LevelGenerator.IsReady(number) && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _levelWait = null;
            // The player went somewhere else while it was being built.
            if (_currentPage != page || _modals.Count != modals) yield break;
            ShowLevelStart(number);
        }

        void ShowLevelStart(int number)
        {
            _levelStart.Prepare(number);
            if (_modals.Contains(_levelStart)) CloseAllModals();
            ShowModal(_levelStart);
        }

        /// <summary>A world's chest, ready to be opened.</summary>
        public void OpenChest(int world)
        {
            _chest.Prepare(world);
            ShowModal(_chest);
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
                case GameMode.Daily:
                    // A thrown-away attempt still took time; it counts towards the day's clock.
                    if (!session.IsFinished) Progress.DailyAttemptEnded(DailyDate, session.PlaySeconds);
                    PlayDaily(fresh: true);
                    break;
                case GameMode.Level:
                    // Starting over part-way through is a loss as far as the win streak is concerned.
                    if (!session.IsFinished && session.MovesUsed > 0) Progress.RecordLevelResult(won: false);
                    PlayLevel(session.Level.Number);
                    break;
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

        /// <summary>The map straight after a win: it walks the road on to the next level and opens its sheet.</summary>
        public void ShowLevelSelect(int afterWin)
        {
            _levels.Prepare(afterWin);
            ShowPage(_levels);
        }

        public void OpenSettings() => ShowModal(_settings);

        public void OpenScores() => ShowModal(_scores);

        public void OpenPause() => ShowModal(_pause);

        public void OpenStats() => ShowModal(_stats);

        public void OpenThemes() => ShowModal(_themes);

        /// <summary>The modal on its way out, if one is: it stays on the stack until its sheet is down.</summary>
        AppScreen _dismissing;

        /// <summary>
        /// Dismisses the top modal, falling back to the one beneath it if there is one. The sheet
        /// slides down first; while it does, it still owns the pointer, so nothing behind it can be
        /// pressed through it on the way out.
        /// </summary>
        public void CloseModal()
        {
            // A second close while the first is still sliding (a double tap on back): land the
            // first at once and carry on with the next.
            if (_dismissing != null) FinishDismiss();
            if (_modals.Count == 0) return;

            var top = _modals[_modals.Count - 1];
            _dismissing = top;
            top.Dismiss(revealsModal: _modals.Count > 1, () =>
            {
                if (_dismissing == top) FinishDismiss();
            });
        }

        void FinishDismiss()
        {
            var top = _dismissing;
            _dismissing = null;
            if (top == null || !_modals.Contains(top)) return;

            top.Hide();
            PointerRouter.PopBlocker(top.RootRect);
            _modals.Remove(top);

            if (_modals.Count > 0)
            {
                _modals[_modals.Count - 1].Show(overModal: true);
                return;
            }

            // Hand board input back to whatever page is underneath.
            if (_currentPage is IPointerFallback fallback)
                PointerRouter.Fallback = fallback;
        }

        void CloseAllModals()
        {
            _dismissing = null;
            for (int i = _modals.Count - 1; i >= 0; i--)
            {
                _modals[i].Hide();
                PointerRouter.PopBlocker(_modals[i].RootRect);
            }

            _modals.Clear();
        }

        void ShowPage(AppScreen page, bool animate = true)
        {
            CloseAllModals();

            if (_currentPage != null && _currentPage != page)
                _currentPage.Hide();

            _currentPage = page;
            page.Show(animate);
        }

        void ShowModal(AppScreen modal, bool animate = true)
        {
            if (_dismissing != null) FinishDismiss();
            if (_modals.Contains(modal)) return;

            bool over = _modals.Count > 0;
            if (over) _modals[_modals.Count - 1].Hide();

            // A modal owns the pointer outright: neither the board nor any button behind it reacts.
            PointerRouter.Fallback = null;

            _modals.Add(modal);
            modal.Show(animate, overModal: over);
            PointerRouter.PushBlocker(modal.RootRect);
        }

        /// <summary>
        /// Works out how much of the screen the system keeps for itself. The backdrop deliberately
        /// ignores this and stays full-bleed — it is the notch that should be filled with colour,
        /// not left black — while everything the player touches is inset inside it.
        /// </summary>
        void MeasureSafeArea()
        {
            var safe = Screen.safeArea;
            _measuredSafeArea = safe;
            _measuredScreen = new Vector2Int(Screen.width, Screen.height);

            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            if (scale <= 0.0001f) scale = 1f;

            float left = safe.xMin / scale;
            float right = (Screen.width - safe.xMax) / scale;
            float top = (Screen.height - safe.yMax) / scale;
#if PRIZMA_AUTOTEST
            // The desktop player has no cutout; the test run asks for one so its shots show the
            // room a phone really has.
            top = Mathf.Max(top, AutoTest.TopInsetArg);
#endif
            float bottom = Mathf.Max(safe.yMin / scale, GestureBarMargin);

            _safeInsets = new Vector4(left, bottom, right, top);
            SafeInsets = _safeInsets;
            PageHeight = Screen.height / scale - top - bottom;
        }

        void ApplySafeArea()
        {
            foreach (var layer in new[] { _pageLayer, _modalLayer })
            {
                if (layer == null) continue;
                layer.offsetMin = new Vector2(_safeInsets.x, _safeInsets.y);
                layer.offsetMax = new Vector2(-_safeInsets.z, -_safeInsets.w);
            }
        }

        // ------------------------------------------------------------------ platform

        /// <summary>A haptic pulse for a big moment — multi-line clears, the end of a run. Honours the setting.</summary>
        public void Vibrate(bool strong = false)
        {
            if (GameSettings.Haptics) Haptics.Pulse(strong ? 45 : 22);
        }

        /// <summary>
        /// A clear, felt rather than heard. The pulse grows with the number of lines the one move
        /// took, so the hand is told the same thing the ear is told by <see cref="AudioKit.PlayClear"/>
        /// — and a player with the sound off still gets the difference between a tidy move and a
        /// very good one.
        /// </summary>
        public void VibrateClear(int lines, bool perfectClear)
        {
            if (!GameSettings.Haptics) return;

            if (perfectClear)
            {
                Haptics.Pulse(60, 255);
                return;
            }

            int steps = Mathf.Clamp(lines, 1, 4);
            Haptics.Pulse(14 + 10 * steps, Mathf.Min(255, 90 + 45 * steps));
        }

        /// <summary>The lightest tick there is, for a piece landing. Felt more than noticed.</summary>
        public void Tick()
        {
            if (GameSettings.Haptics) Haptics.Pulse(8, 70);
        }

        void OnApplicationPause(bool paused)
        {
            // Back from the background: a day may have turned, or the system may have dropped the alarm.
            if (!paused) Reminder.Refresh();
        }

        void Update()
        {
            // Rotation, a foldable opening, or the editor's game view being resized.
            if (Screen.width != _measuredScreen.x || Screen.height != _measuredScreen.y ||
                Screen.safeArea != _measuredSafeArea)
            {
                MeasureSafeArea();
                ApplySafeArea();
            }

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

            // The adventure map is built ahead while the menu sits idle, a part a frame, so the first
            // tap on it does not build it all in one long frame.
            if (_currentPage == _menu && _modals.Count == 0)
            {
                if (_menuIdleSince < 0f) _menuIdleSince = Time.unscaledTime;
                else if (Time.unscaledTime - _menuIdleSince > MapAheadDelay) _levels.BuildAhead();
            }
            else _menuIdleSince = -1f;
        }

        // Past the splash and the menu's own entrance, which should not share their frames with it.
        const float MapAheadDelay = 3f;
        float _menuIdleSince = -1f;

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
