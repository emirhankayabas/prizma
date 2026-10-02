using System;
using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The play page: HUD, board, prism powers, tray and the result card, for every mode.
    /// Pointer input arrives through <see cref="PointerRouter"/> as the fallback handler, so
    /// buttons always win over the board.
    ///
    /// The page never creates a run itself. <see cref="AppController"/> hands it one — fresh or
    /// restored from a save — through <see cref="Begin"/>.
    /// </summary>
    public sealed class GameScreen : AppScreen, IPointerFallback
    {
        const float BoardPadding = 20f;
        const float BoardGap = 10f;

        /// <summary>
        /// The play page's vertical layout, solved once from the page height.
        ///
        /// It used to be fixed numbers for a 1920-unit page with the surplus of a tall phone split
        /// around them. That left two problems: a 20:9 phone showed a quarter of the screen empty
        /// under a tray of tiny pieces, and a 16:9 phone — whose page is shorter than 1920 once the
        /// gesture bar is out — had the stack clamped to 1920 and pushed into the gesture bar.
        ///
        /// Now every band has a size it wants and a size it can live with. Room left over goes
        /// into the tray and the gaps; room missing comes out of the gaps first, then the tray and
        /// the HUD, and the board — the one thing the player is here for — gives way last.
        /// </summary>
        public readonly struct PlayLayout
        {
            public readonly float Hud;
            public readonly float GapTop;
            public readonly float Board;
            public readonly float GapMid;
            public readonly float Power;
            public readonly float GapLow;
            public readonly float Tray;
            public readonly float GapBottom;
            public readonly float PageHeight;

            const float HudWant = GameHud.FullHeight, HudMin = 420f;
            const float GapTopWant = 24f, GapTopMin = 8f;
            const float GapMidWant = 32f, GapMidMin = 16f;
            const float GapLowWant = 12f, GapLowMin = 4f;
            const float TrayWant = 380f, TrayMin = 300f, TrayMax = 480f;
            const float BoardMin = 760f;
            const float PowerHeight = 172f;

            PlayLayout(float page, float hud, float gapTop, float board, float gapMid, float gapLow, float tray, float gapBottom)
            {
                PageHeight = page;
                Hud = hud;
                GapTop = gapTop;
                Board = board;
                GapMid = gapMid;
                Power = PowerHeight;
                GapLow = gapLow;
                Tray = tray;
                GapBottom = gapBottom;
            }

            public static PlayLayout Solve(float page)
            {
                float hud = HudWant, gapTop = GapTopWant, gapMid = GapMidWant, gapLow = GapLowWant;
                float tray = TrayWant, gapBottom = 0f;
                float board = 1080f - Design.BoardGutter * 2f;

                float slack = page - (hud + gapTop + board + gapMid + PowerHeight + gapLow + tray);

                if (slack >= 0f)
                {
                    // The tray takes a share first: a taller rack is a bigger target and sits the
                    // pieces lower, into the thumb's reach. Then the gaps, evenly.
                    float toTray = Mathf.Min(slack * 0.4f, TrayMax - tray);
                    tray += toTray;
                    slack -= toTray;

                    gapTop += slack * 0.34f;
                    gapMid += slack * 0.33f;
                    gapBottom += slack * 0.33f;
                }
                else
                {
                    float missing = -slack;
                    missing = Take(ref gapTop, GapTopMin, missing);
                    missing = Take(ref gapMid, GapMidMin, missing);
                    missing = Take(ref gapLow, GapLowMin, missing);
                    missing = Take(ref tray, TrayMin, missing);
                    missing = Take(ref hud, HudMin, missing);
                    Take(ref board, BoardMin, missing);
                }

                return new PlayLayout(page, hud, gapTop, board, gapMid, gapLow, tray, gapBottom);
            }

            static float Take(ref float value, float min, float amount)
            {
                float taken = Mathf.Min(amount, Mathf.Max(0f, value - min));
                value -= taken;
                return amount - taken;
            }

            /// <summary>Board centre, measured from the page centre.</summary>
            public float BoardCenterY => PageHeight * 0.5f - Hud - GapTop - Board * 0.5f;

            /// <summary>Tray's bottom edge above the page's bottom edge.</summary>
            public float TrayBottom => GapBottom;

            /// <summary>Power bar's bottom edge above the page's bottom edge.</summary>
            public float PowerBottom => GapBottom + Tray + GapLow;
        }

        static PlayLayout _layout = PlayLayout.Solve(AppController.ReferenceHeight);

        /// <summary>
        /// Told to the page by <see cref="AppController"/> before anything is built, because the
        /// backdrop's glow is positioned from <see cref="BoardCenterY"/> too.
        /// </summary>
        public static void SetPageHeight(float height) => _layout = PlayLayout.Solve(height);

        public static PlayLayout Layout => _layout;

        /// <summary>Board centre relative to the page centre. The popups and the backdrop glow follow it.</summary>
        public static float BoardCenterY => _layout.BoardCenterY;

        GameSession _session;

        BoardView _board;
        TrayView _tray;
        GameHud _hud;
        PowerBar _powers;
        ResultCard _result;
        Action _resultPrimary;
        Action _resultSecondary;

        RectTransform _dragLayer;
        RectTransform _popupLayer;
        RectTransform _flyLayer;

        PieceView _dragPiece;
        int _dragSlot = -1;
        bool _dragging;

        // The cell the ghost is currently drawn at, so it is only rebuilt when that changes.
        int _hoverCol;
        int _hoverRow;
        bool _hoverKnown;

        // A power waiting for its target: the piece to turn, or the cell to blast.
        PowerKind? _aiming;
        bool _bombHeld;
        bool _colorBlind;

        int _displayedScore;
        float _liftPixels;
        Coroutine _scoreRoutine;

        // Passing your own best is the one milestone a classic run has, and it used to go by in
        // silence — the readout simply carried on. One rim pulse in gold, once per run, no text
        // and no repeat: the same grammar the rest of the board speaks.
        int _recordToBeat;
        bool _recordBeaten;

        // The run's light (Spectrum): its stage in a classic run, its step towards the goal in a
        // level or the daily. Only ever climbs within a run.
        int _stage;

        Image _hand;
        Coroutine _tutorial;
        Coroutine _powerHint;

        readonly List<TextMeshProUGUI> _popupPool = new List<TextMeshProUGUI>();
        readonly List<Image> _flyPool = new List<Image>();

        public GameSession Session => _session;

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            var layout = _layout;
            float rowWidth = 1080f - Design.BoardGutter * 2f;

            BuildBoard(layout.Board);

            _hud = new GameHud();
            _hud.Build(Root, layout.Hud, () => { Audio.PlayClick(); App.OpenPause(); });
            _hud.HammerClicked += OnHammerClicked;

            _powers = new PowerBar();
            _powers.Build(Root, rowWidth, layout.Power, layout.PowerBottom);
            _powers.PowerClicked += OnPowerClicked;
            _powers.EndClicked += () => { Audio.PlayClick(); _session?.Concede(); };

            BuildTray(rowWidth, layout);

            _popupLayer = UiBuilder.Child(Root, "Popups");
            _flyLayer = UiBuilder.Child(Root, "Fly");
            _dragLayer = UiBuilder.Child(Root, "DragLayer");

            // The held piece moves every frame; on its own canvas that re-batches one piece
            // instead of the board, the HUD and the tray along with it. Flying crystals likewise.
            _dragLayer.gameObject.AddComponent<Canvas>();
            _flyLayer.gameObject.AddComponent<Canvas>();

            _hand = UiBuilder.Image(Root, "TutorialHand", Icons.Hand, Color.white);
            _hand.type = Image.Type.Simple;
            _hand.rectTransform.sizeDelta = new Vector2(200f, 200f);
            // The fingertip, not the middle of the glyph, is what lands on the target.
            _hand.rectTransform.pivot = new Vector2(0.45f, 0.94f);
            _hand.gameObject.AddComponent<Canvas>();
            _hand.gameObject.SetActive(false);

            _result = new ResultCard();
            _result.Build(this, Root, App.PageHeight);
            _result.PrimaryClicked += () => { Audio.PlayClick(); _resultPrimary?.Invoke(); };
            _result.SecondaryClicked += () => { Audio.PlayClick(); _resultSecondary?.Invoke(); };
        }

        void BuildBoard(float boardSize)
        {
            var boardGo = new GameObject("Board", typeof(RectTransform));
            boardGo.transform.SetParent(Root, false);

            var boardRect = (RectTransform)boardGo.transform;
            boardRect.anchorMin = boardRect.anchorMax = boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.anchoredPosition = new Vector2(0f, BoardCenterY);

            _board = boardGo.AddComponent<BoardView>();
            _board.Build(App.BoardSize, boardSize, BoardPadding, BoardGap);
            _liftPixels = _board.CellSize * 1.55f;
        }

        void BuildTray(float width, PlayLayout layout)
        {
            var trayGo = new GameObject("Tray", typeof(RectTransform));
            trayGo.transform.SetParent(Root, false);

            var trayRect = (RectTransform)trayGo.transform;
            trayRect.anchorMin = trayRect.anchorMax = new Vector2(0.5f, 0f);
            trayRect.pivot = new Vector2(0.5f, 0f);
            trayRect.anchoredPosition = new Vector2(0f, layout.TrayBottom);

            _tray = trayGo.AddComponent<TrayView>();
            _tray.Build(width, layout.Tray, _board.CellSize, _board.Gap, GameSession.TraySlots);
        }

        // ------------------------------------------------------------------ run lifecycle

        protected override void OnShow()
        {
            PointerRouter.Fallback = this;

            // Show runs again on a page that is already up — a restart, "play again" — so the
            // subscription is made idempotent. It used to stack one handler per restart and drop
            // only one on hide; harmless while this page lived forever, but a theme change destroys
            // it, and the leftover handler then reached into a destroyed board.
            Progress.Changed -= OnProgressChanged;
            Progress.Changed += OnProgressChanged;
        }

        void OnDestroy() => Progress.Changed -= OnProgressChanged;

        protected override void OnHide()
        {
            if (ReferenceEquals(PointerRouter.Fallback, this))
                PointerRouter.Fallback = null;

            Progress.Changed -= OnProgressChanged;
            CancelDrag();
            CancelAim();
            StopTutorial();
            StopPowerHint();
            SaveRun();
        }

        /// <summary>The colour-blind setting can be flipped from the pause menu mid-run; apply it at once.</summary>
        void OnProgressChanged()
        {
            bool colorBlind = Progress.ColorBlind;
            if (colorBlind == _colorBlind) return;

            _colorBlind = colorBlind;
            _board.SetColorBlind(colorBlind);
            _tray.SetColorBlind(colorBlind);
            if (_session != null && !_dragging) _tray.Refresh(_session);
        }

        /// <summary>
        /// The Android back button, asked before the pause menu opens: it backs out of a result
        /// card or an armed power first. Returns false when it has nothing to undo here.
        /// </summary>
        public bool HandleBack()
        {
            if (_result.Visible)
            {
                Audio.PlayClick();
                _resultSecondary?.Invoke();
                return true;
            }

            if (_aiming != null)
            {
                CancelAim();
                return true;
            }

            return false;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveRun();
        }

        /// <summary>Everything spent on today's puzzle: the attempts that ended, plus this one.</summary>
        float DailyClock => _session == null ? 0f : Progress.DailyBankedSeconds(App.DailyDate) + _session.PlaySeconds;

        /// <summary>
        /// The daily's clock. It runs only while the puzzle is actually in front of the player —
        /// not behind the pause menu or any other modal, not on a result card, not in the
        /// background (a backgrounded app gets no frames). Kept on the session, so it is saved
        /// with the run and a resumed attempt carries on from where it stopped.
        /// </summary>
        void Update()
        {
            if (_session == null || _session.Mode != GameMode.Daily) return;
            if (_session.IsFinished || _session.State == SessionState.OutOfMoves) return;
            if (_result.Visible || App.HasModal) return;

            _session.PlaySeconds += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            _hud.SetClock(DailyClock);
        }

        void OnApplicationQuit() => SaveRun();

        void SaveRun()
        {
            if (_session != null && !_session.IsFinished) RunStore.Save(_session);
        }

        /// <summary>Takes over a run — new or restored — and paints everything from it.</summary>
        public void Begin(GameSession session)
        {
            Detach();

            _session = session;
            _session.Moved += OnMoved;
            _session.PowerUsed += OnPowerUsed;
            _session.Stuck += OnStuck;
            _session.OutOfMoves += OnOutOfMoves;
            _session.GameOver += OnGameOver;
            _session.LevelWon += OnLevelWon;

            _colorBlind = Progress.ColorBlind;
            _board.SetColorBlind(_colorBlind);
            _tray.SetColorBlind(_colorBlind);

            // A hammer in stock can free a jammed board, so a jam waits for the player.
            session.HasBoosterRescue = HammerAvailable(session);

            _board.Bind(session.Board);
            _tray.Refresh(session);

            _displayedScore = session.Score;
            _hud.Bind(session);
            _hud.Refresh(_displayedScore);
            if (session.Mode == GameMode.Daily) _hud.SetClock(DailyClock);

            // The record to beat, frozen at the start of the run. A run resumed past it has
            // already had its moment and must not get a second one.
            _recordToBeat = session.Config.Mode == GameMode.Classic ? HighScores.Best : int.MaxValue;
            _recordBeaten = session.Score > _recordToBeat;

            // A run resumed half-way comes back in the light it had reached, without the ceremony.
            _stage = StageOf(session);
            Backdrop.Current?.ResetLight(LightOf(session, _stage));
            if (_stage >= Spectrum.MaxStage) Backdrop.Current?.SetLight(LightOf(session, _stage), turning: true);
            _board.SetRimTint(_recordBeaten ? Design.Gold : LightOf(session, _stage), _recordBeaten ? 0.55f : 0.4f);

            _result.Hide();
            _aiming = null;
            _bombHeld = false;
            // A hint left over from the run before would point at a power this run may not need.
            StopPowerHint();
            SyncState();

            RunStore.Save(session);
            MaybeStartTutorial();

            // A run saved at a decision point comes back to the same decision.
            if (session.State == SessionState.OutOfMoves) OnOutOfMoves();
            else if (session.State == SessionState.Stuck) MaybeHintPowers();
        }

        void Detach()
        {
            if (_session == null) return;

            _session.Moved -= OnMoved;
            _session.PowerUsed -= OnPowerUsed;
            _session.Stuck -= OnStuck;
            _session.OutOfMoves -= OnOutOfMoves;
            _session.GameOver -= OnGameOver;
            _session.LevelWon -= OnLevelWon;
        }

        /// <summary>Everything that follows the session's state: the tray dims and the power bar changes when stuck.</summary>
        void SyncState()
        {
            if (_session == null) return;

            _tray.SetDimmed(_session.State == SessionState.Stuck);
            _powers.Refresh(_session, _aiming);
            _tray.SetRotateHints(_session, _aiming == PowerKind.Rotate);

            bool live = _session.State == SessionState.Playing || _session.State == SessionState.Stuck;
            _hud.SetHammer(Progress.BoosterCount(Booster.Hammer), _aiming == PowerKind.Hammer, live);
        }

        static bool HammerAvailable(GameSession session) =>
            session.Mode == GameMode.Level && Progress.BoosterCount(Booster.Hammer) > 0;

        void OnHammerClicked()
        {
            if (_session == null || _dragging || _result.Visible) return;
            if (_session.State != SessionState.Playing && _session.State != SessionState.Stuck) return;
            if (Progress.BoosterCount(Booster.Hammer) <= 0) return;

            _aiming = _aiming == PowerKind.Hammer ? (PowerKind?)null : PowerKind.Hammer;
            _bombHeld = false;
            _board.HideGhost();
            Audio.PlayClick();
            SyncState();
        }

        // ------------------------------------------------------------------ moves

        void OnMoved(MoveResult result)
        {
            // The drop already happened, so whatever was being previewed is now history.
            // Cleared here rather than only in OnPointerUp so any caller leaves a clean board.
            _board.HideLinePreview();

            var clear = result.Clear;

            // Before the refresh, while the cells still hold the blocks: the clear copies their
            // colours and plays them out itself. Afterwards there would be nothing left to animate.
            if (clear.Any)
            {
                var placed = Design.Blocks[result.PlacedColorIndex % Design.Blocks.Length];
                _board.PlayClearOut(clear.ClearedCells, result.PlacedColumn, result.PlacedRow, placed);

                // Debris grows with the move — one piece a cell for a line, more for several — and
                // is thrown harder the longer the streak has run.
                int perCell = clear.PerfectClear ? 3 : clear.LinesCleared >= 2 ? 2 : 1;
                float power = 1f + 0.08f * Mathf.Min(result.ComboStreak - 1, 8);
                _board.PlayShards(clear.ClearedCells, result.PlacedColumn, result.PlacedRow, perCell, power, placed);
            }

            _board.Refresh();
            _board.PlayPlacePop(result.PlacedShape, result.PlacedColumn, result.PlacedRow);
            Audio.PlayPlace();
            App.Tick();

            if (_tutorial != null || !Progress.TutorialSeen)
            {
                StopTutorial();
                Progress.TutorialSeen = true;
            }

            if (clear.Any)
            {
                var tint = Design.Blocks[result.PlacedColorIndex % Design.Blocks.Length];

                // One call, first thing: the clear speaks as a single phrase, sized by how many
                // lines went in this one move and pitched by how long the streak has run. Before
                // this, a good move fired the clear, the combo, the prism line and the fanfare as
                // four separate one-shots that simply summed on top of each other.
                Audio.PlayClear(clear.LinesCleared, result.ComboStreak, clear.MonoLines, clear.PerfectClear);

                _board.PlayLineSweep(clear, Color.white);
                _board.PlayClearBurst(clear.ClearedCells, Color.white);
                _board.PulseEdge(tint);

                if (clear.MonoLines > 0)
                    _board.PlayPrismSweep(clear);

                if (clear.CrackedIce.Count > 0)
                {
                    _board.PlayIceCrack(clear.CrackedIce);
                    Audio.PlayIce();
                }

                if (clear.CollectedGems.Count > 0)
                    FlyCrystals(clear.CollectedGems);

                PlayLevelLayers(clear);

                if (clear.PerfectClear)
                {
                    _board.PlayBoardWave();
                    _board.PulseEdge(Design.Gold);
                    _board.PlayWave(Vector2.zero, Design.Gold, 5f);
                }

                // Every fifth clear in a row: a ring of light from the drop, the same landmark the
                // sound marks with its chord. Gold at five, the prism from ten.
                if (result.ComboStreak >= 5 && result.ComboStreak % 5 == 0)
                {
                    var colour = result.ComboStreak >= 10 ? Design.Prism : Design.Gold;
                    _board.PlayWave(_board.CellAnchoredPosition(result.PlacedColumn, result.PlacedRow), colour, 4f);
                    _board.PulseEdge(colour);
                }

                _hud.ShowCombo(this, result.ComboStreak);

                // The room warms with the streak, and a single-colour line or a wiped board lights it.
                Backdrop.Current?.SetHeat(Mathf.Clamp01((result.ComboStreak - 1) / 7f));
                if (clear.MonoLines > 0) Backdrop.Current?.Rainbow();
                if (clear.PerfectClear) Backdrop.Current?.Flash(Design.Gold, 1.2f);

                var popupCells = clear.ClearedCells.Count > 0 ? clear.ClearedCells : clear.CrackedIce;
                ShowScorePopup(result.ClearScore, popupCells, clear.PerfectClear ? Design.Gold : tint, clear.PerfectClear);

                if (clear.LinesCleared > 1 || clear.PerfectClear)
                {
                    // The board itself takes the hit: a small swell that grows with the clear.
                    float punch = clear.PerfectClear ? 0.045f : 0.012f * Mathf.Min(clear.LinesCleared, 4);
                    StartCoroutine(Tween.Punch(_board.transform, punch, 0.24f));
                    _board.Shake(clear.PerfectClear ? 16f : 4f + 3f * Mathf.Min(clear.LinesCleared, 4));

                    // The same ladder through the skin, so the hand is told what the ear was told.
                    App.VibrateClear(clear.LinesCleared, clear.PerfectClear);
                }
            }
            else
            {
                _hud.HideCombo();
                Backdrop.Current?.SetHeat(0f);
            }

            if (result.ChargesGained > 0)
            {
                _powers.CelebrateCharge(this, _session.Charges);
                Audio.PlayCharge();
            }

            // The board's answer to the move: shade creeping on, timers ticking down.
            if (result.ShadeSpread.X >= 0)
            {
                _board.PlayShadeSpread(result.ShadeSpread.X, result.ShadeSpread.Y);
                StartCoroutine(After(0.18f, Audio.PlayStuck));
            }

            if (_session.Board.TimerCount > 0) _board.NudgeUrgentTimers();

            if (result.TrayRefilled)
            {
                _tray.Refresh(_session);
                // After the clear has had its moment, as the pieces rise into their slots.
                StartCoroutine(After(0.18f, Audio.PlayDeal));
            }

            MaybeMarkRecord();
            MaybeAdvanceLight();
            UpdateScore();
            SyncState();
            SaveRun();
        }

        /// <summary>
        /// The move that takes a classic run past its own best. Fires once, on the rim, in gold —
        /// the board already pulses its rim on every clear, so this says "something happened here"
        /// in a language the player has been reading all run, with nothing new to learn and
        /// nothing to dismiss. The best-score chip is already tracking the live score, so the
        /// number itself explains what the light meant.
        /// </summary>
        void MaybeMarkRecord()
        {
            if (_recordBeaten || _session == null) return;
            if (_recordToBeat <= 0 || _session.Score <= _recordToBeat) return;

            _recordBeaten = true;
            _board.SetRimTint(Design.Gold, 0.55f);
            _board.PulseEdge(Design.Gold);
            StartCoroutine(Tween.Punch(_hud.LeftLabel, 0.3f, 0.3f));
            StartCoroutine(After(0.3f, Audio.PlayRecord));
        }

        // ------------------------------------------------------------------ light

        static int StageOf(GameSession session)
        {
            if (session.Level == null) return Spectrum.StageForScore(session.Score);
            // A goal run: quarters of the goal done, 0..4.
            float done = session.Level.Target <= 0 ? 0f : session.GoalProgress / (float)session.Level.Target;
            return Mathf.Clamp(Mathf.FloorToInt(done * 4f), 0, 4);
        }

        static Color? LightOf(GameSession session, int stage) => session.Level == null
            ? Spectrum.ColorFor(stage, Time.unscaledTime)
            : Spectrum.ColorForGoal(stage / 4f);

        /// <summary>
        /// The run has earned a new stage of light: the room changes colour around the board, the
        /// board is washed in it from the bottom row up, and the scale climbs. No words — the
        /// colour is the news. In a classic run the furthest stage ever reached is remembered;
        /// the title on the menu lights one letter for each.
        /// </summary>
        void MaybeAdvanceLight()
        {
            if (_session == null) return;
            int stage = StageOf(_session);
            if (stage <= _stage) return;

            _stage = stage;
            var light = LightOf(_session, stage);
            bool top = _session.Level == null && stage >= Spectrum.MaxStage;

            Backdrop.Current?.SetLight(light, turning: top);
            if (!_recordBeaten) _board.SetRimTint(light);

            // The goal's last quarter is the win itself, which has its own moment.
            if (_session.Level != null && stage >= 4) return;

            var wash = light ?? Design.Prism;
            StartCoroutine(After(0.25f, () =>
            {
                _board.PlayStageWash(wash);
                Audio.PlayStage();
            }));

            if (_session.Level == null) Progress.RecordSpectrum(stage);
        }

        static IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
        }

        void UpdateScore()
        {
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = StartCoroutine(ScoreRoutine(_displayedScore, _session.Score));
        }

        IEnumerator ScoreRoutine(int from, int to)
        {
            int gain = Mathf.Max(0, to - from);

            // A fixed roll counted 8 points and 800 in the same breath, so the score told the
            // player nothing about the size of what they had just done. Scaling the roll with the
            // gain makes a big clear feel big without putting anything new on screen; the ceiling
            // keeps the number from still climbing after the next piece is already in hand.
            // A bare placement (a few points) still lands almost instantly.
            float duration = Mathf.Clamp(0.22f + gain * 0.0016f, 0.22f, 1.05f);
            float punch = Mathf.Clamp(0.10f + gain * 0.00014f, 0.10f, 0.22f);

            StartCoroutine(Tween.Punch(_hud.Readout, punch, 0.24f));

            yield return Tween.CountUp(from, to, duration, v =>
            {
                _displayedScore = v;
                _hud.Refresh(v);
            });

            _scoreRoutine = null;
        }

        /// <summary>
        /// Floats the points earned up from where the clear happened. Putting the number at the
        /// clear rather than in the HUD is what ties the reward to the move that earned it.
        /// </summary>
        void ShowScorePopup(int amount, IReadOnlyList<CellOffset> cells, Color tint, bool big)
        {
            if (amount <= 0 || cells.Count == 0) return;

            // Anchor to the middle of what was cleared.
            var sum = Vector2.zero;
            for (int i = 0; i < cells.Count; i++)
                sum += _board.CellAnchoredPosition(cells[i].X, cells[i].Y);

            var boardLocal = sum / cells.Count;
            var start = boardLocal + new Vector2(0f, BoardCenterY);

            var label = RentPopup();
            label.text = "+" + amount;
            label.color = tint;
            label.fontSize = big ? Design.Display : Design.Title;
            label.gameObject.SetActive(true);

            StartCoroutine(PopupRoutine(label, big ? new Vector2(0f, BoardCenterY) : start));
        }

        IEnumerator PopupRoutine(TextMeshProUGUI label, Vector2 start)
        {
            var rect = label.rectTransform;
            var color = label.color;

            rect.anchoredPosition = start;
            rect.localScale = Vector3.one * 0.6f;

            const float rise = 150f;
            const float duration = 0.75f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;

                rect.anchoredPosition = start + new Vector2(0f, rise * Ease.OutCubic(k));

                // Overshoot on the way in, then hold: the pop is what draws the eye.
                float scale = k < 0.25f
                    ? Mathf.LerpUnclamped(0.6f, 1.12f, Ease.OutBack(k / 0.25f))
                    : Mathf.Lerp(1.12f, 1f, (k - 0.25f) / 0.75f);
                rect.localScale = Vector3.one * scale;

                label.color = color.WithAlpha(k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f);
                yield return null;
            }

            label.gameObject.SetActive(false);
        }

        TextMeshProUGUI RentPopup()
        {
            for (int i = 0; i < _popupPool.Count; i++)
                if (!_popupPool[i].gameObject.activeSelf)
                    return _popupPool[i];

            var label = UiBuilder.Label(_popupLayer, "Popup", "", Design.Title, Design.TextPrimary, Design.FontDisplay);
            label.rectTransform.sizeDelta = new Vector2(720f, 180f);
            UiBuilder.TextShadow(label, 0.5f, -0.3f, 0.4f);
            label.gameObject.SetActive(false);
            _popupPool.Add(label);
            return label;
        }

        /// <summary>
        /// Freed crystals fly off the board into the goal counter, which then ticks down. The
        /// counter changing on its own would be easy to miss; the flight shows where they went.
        /// </summary>
        void FlyCrystals(IReadOnlyList<CellOffset> cells)
        {
            bool toGoal = _session.Level != null && _session.Level.Goal == GoalKind.Gems;
            var target = toGoal ? _hud.GoalAnchor : _powers.CrystalPosition(Mathf.Max(0, _session.Charges - 1));

            for (int i = 0; i < cells.Count; i++)
            {
                var crystal = RentFly();
                crystal.sprite = Art.Crystal;
                crystal.color = Design.Crystal;
                crystal.rectTransform.sizeDelta = new Vector2(_board.CellSize * 0.66f, _board.CellSize * 0.66f);
                crystal.rectTransform.position = _board.CellWorldPosition(cells[i].X, cells[i].Y);
                crystal.gameObject.SetActive(true);
                StartCoroutine(FlyRoutine(crystal, target, i * 0.08f, toGoal));
            }
        }

        /// <summary>
        /// The level layers a clear or a blast took: glow tiles going out, timers defused, and the
        /// goal's own pieces flying to its counter — tiles, blocks of the order colour, shade.
        /// </summary>
        void PlayLevelLayers(ClearResult clear)
        {
            if (clear.CollectedTiles.Count > 0) _board.PlayTilesLit(clear.CollectedTiles);
            if (clear.DefusedTimers.Count > 0)
            {
                _board.PlayTimersDefused(clear.DefusedTimers);
                Audio.PlayCharge();
            }

            var level = _session.Level;
            if (level == null) return;

            switch (level.Goal)
            {
                case GoalKind.Tiles:
                    FlyToGoal(clear.CollectedTiles, Art.Tile, Design.TileGlow);
                    break;

                case GoalKind.Colors:
                case GoalKind.Shade:
                {
                    int wanted = level.Goal == GoalKind.Colors ? level.OrderColor : BoardModel.Shade;
                    _flyCells.Clear();
                    for (int i = 0; i < clear.ClearedCells.Count && i < clear.ClearedColors.Count; i++)
                        if (clear.ClearedColors[i] == wanted) _flyCells.Add(clear.ClearedCells[i]);
                    FlyToGoal(_flyCells, GameHud.GoalSprite(level), GameHud.GoalColor(level));
                    break;
                }
            }
        }

        readonly List<CellOffset> _flyCells = new List<CellOffset>();

        void FlyToGoal(IReadOnlyList<CellOffset> cells, Sprite sprite, Color color)
        {
            var target = _hud.GoalAnchor;
            // A long line of them reads as a stream; past a dozen it only piles up.
            int count = Mathf.Min(cells.Count, 12);
            for (int i = 0; i < count; i++)
            {
                var piece = RentFly();
                piece.sprite = sprite;
                piece.color = color;
                piece.type = Image.Type.Simple;
                piece.rectTransform.sizeDelta = new Vector2(_board.CellSize * 0.6f, _board.CellSize * 0.6f);
                piece.rectTransform.position = _board.CellWorldPosition(cells[i].X, cells[i].Y);
                piece.gameObject.SetActive(true);
                StartCoroutine(FlyRoutine(piece, target, i * 0.05f, true, silent: i > 0));
            }
        }

        IEnumerator FlyRoutine(Image crystal, Vector3 target, float delay, bool toGoal, bool silent = false)
        {
            var rect = crystal.rectTransform;
            var start = rect.position;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            // A curve that rises before it travels, so it reads as lifted out of the board.
            float scale = App.CanvasScale;
            var control = (start + target) * 0.5f + new Vector3(0f, 260f * scale, 0f);

            const float duration = 0.6f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Ease.OutQuad(t / duration);
                var a = Vector3.Lerp(start, control, k);
                var b = Vector3.Lerp(control, target, k);
                rect.position = Vector3.Lerp(a, b, k);
                rect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.7f, k);
                yield return null;
            }

            crystal.gameObject.SetActive(false);
            if (!silent) Audio.PlayGem();
            if (toGoal) StartCoroutine(Tween.Punch(_hud.GoalIcon, 0.3f, 0.3f));
        }

        Image RentFly()
        {
            for (int i = 0; i < _flyPool.Count; i++)
                if (!_flyPool[i].gameObject.activeSelf)
                    return _flyPool[i];

            var image = UiBuilder.Image(_flyLayer, "Crystal", Art.Crystal, Design.Crystal);
            image.type = Image.Type.Simple;
            image.gameObject.SetActive(false);
            _flyPool.Add(image);
            return image;
        }

        // ------------------------------------------------------------------ powers

        void OnPowerClicked(PowerKind kind)
        {
            if (_session == null || _dragging || _result.Visible) return;

            switch (kind)
            {
                case PowerKind.Reroll:
                    CancelAim();
                    if (_session.TryReroll() == null) Audio.PlayInvalid();
                    break;

                default:
                    // Rotate and bomb need a target: the first tap arms them, a second tap disarms.
                    _aiming = _aiming == kind ? (PowerKind?)null : kind;
                    _bombHeld = false;
                    _board.HideGhost();
                    Audio.PlayClick();
                    SyncState();
                    break;
            }
        }

        void CancelAim()
        {
            if (_aiming == null && !_bombHeld) return;

            _aiming = null;
            _bombHeld = false;
            _board.HideGhost();
            SyncState();
        }

        void OnPowerUsed(PowerResult result)
        {
            _aiming = null;
            _bombHeld = false;

            if (_powerHint != null || !Progress.PowersHinted)
            {
                StopPowerHint();
                Progress.PowersHinted = true;
            }

            switch (result.Kind)
            {
                case PowerKind.Rotate:
                    _tray.RotatePiece(result.Slot, _session);
                    Audio.PlayRotate();
                    break;

                case PowerKind.Reroll:
                    _tray.Refresh(_session);
                    Audio.PlayReroll();
                    break;

                case PowerKind.Bomb:
                    _board.HideGhost();
                    _board.Refresh();
                    _board.PlayBlast(result.Blast, result.Column, result.Row);
                    StartCoroutine(Tween.Punch(_board.transform, 0.035f, 0.26f));
                    if (result.Blast.CollectedGems.Count > 0) FlyCrystals(result.Blast.CollectedGems);
                    PlayLevelLayers(result.Blast);
                    Audio.PlayBomb();
                    App.Vibrate();
                    UpdateScore();
                    break;

                case PowerKind.Hammer:
                    _board.HideGhost();
                    _board.Refresh();
                    _board.PlayBlast(result.Blast, result.Column, result.Row);
                    if (result.Blast.CollectedGems.Count > 0) FlyCrystals(result.Blast.CollectedGems);
                    PlayLevelLayers(result.Blast);
                    StartCoroutine(Tween.Punch(_board.transform, 0.02f, 0.2f));
                    Audio.PlayBomb();
                    App.Tick();
                    UpdateScore();
                    break;
            }

            _hud.Refresh(_displayedScore);
            MaybeAdvanceLight();
            SyncState();
            SaveRun();
        }

        void OnStuck()
        {
            CancelDrag();
            Audio.PlayStuck();
            SyncState();
            MaybeHintPowers();
        }

        /// <summary>
        /// The first jam that a charge could rescue is where the powers are taught: the same hand
        /// as the drag tutorial taps the reroll button until the player uses a power. Once.
        /// </summary>
        void MaybeHintPowers()
        {
            if (Progress.PowersHinted || _session == null || !_session.CanReroll()) return;

            StopTutorial();
            StopPowerHint();
            _powerHint = StartCoroutine(PowerHintRoutine(_powers.ButtonPosition(PowerKind.Reroll)));
        }

        void StopPowerHint()
        {
            if (_powerHint == null) return;
            StopCoroutine(_powerHint);
            _powerHint = null;
            _hand.gameObject.SetActive(false);
            _hand.rectTransform.localScale = Vector3.one;
        }

        IEnumerator PowerHintRoutine(Vector3 target)
        {
            yield return new WaitForSecondsRealtime(0.6f);

            _hand.rectTransform.SetAsLastSibling();
            _hand.rectTransform.position = target;
            _hand.color = Color.white;
            _hand.gameObject.SetActive(true);

            while (true)
            {
                // A tap: press in, release, pause. Motion, not a blink.
                yield return Tween.Scale(_hand.rectTransform, Vector3.one, Vector3.one * 0.84f, 0.16f, Ease.OutQuad);
                yield return Tween.Scale(_hand.rectTransform, Vector3.one * 0.84f, Vector3.one, 0.22f, Ease.OutBack);
                yield return new WaitForSecondsRealtime(0.7f);
            }
        }

        /// <summary>
        /// A level ran out of moves with charges to spare: the card offers more moves for two of
        /// them. The price sits where the note goes, as crystals — the same currency as the prism.
        /// </summary>
        void OnOutOfMoves()
        {
            CancelDrag();
            CancelAim();
            SyncState();

            var session = _session;
            int left = session.GoalRemaining;

            _resultPrimary = () =>
            {
                if (!_session.TryBuyMoves()) return;

                _result.Hide();
                Audio.PlayCharge();
                _hud.Refresh(_displayedScore);
                StartCoroutine(Tween.Punch(_hud.Readout, 0.12f, 0.24f));
                SyncState();
                SaveRun();
            };
            _resultSecondary = () => _session.Concede();

            _result.Show(this, Str.OutOfMovesTitle, left.ToString(), Str.Charges(PowerRules.ExtraMovesCost), Design.Prism, Art.Crystal,
                -1, Str.PlusMoves(PowerRules.ExtraMoves), Str.End);
            Audio.PlayStuck();
            SaveRun();
        }

        // ------------------------------------------------------------------ endings

        void OnGameOver()
        {
            CancelDrag();
            CancelAim();
            StopTutorial();
            StopPowerHint();

            var session = _session;
            Backdrop.Current?.SetHeat(0f);
            RunStore.Clear(session.Mode);
            Progress.RecordRun(session);
            SyncState();

            switch (session.Mode)
            {
                case GameMode.Level:
                {
                    int n = session.Level.Number;
                    int left = session.GoalRemaining;
                    Progress.RecordLevelResult(won: false);

                    string reason = session.LossReason == LossReason.Timer ? Str.TimerBurst
                        : session.LossReason == LossReason.NoMoves || session.MovesLeft <= 0 ? Str.NoMovesLeft
                        : Str.NoRoom;
                    if (session.LossReason == LossReason.Timer) _board.PlayTimerBurst();

                    // Another try goes through the level's own sheet, where boosters can be picked.
                    _resultPrimary = () => App.OpenLevelStart(n);
                    _resultSecondary = App.ShowLevelSelect;
                    _result.Show(this, Str.LevelTitle(n), left.ToString(), reason, Design.PreviewTint(3),
                        session.LossReason == LossReason.Timer ? Icons.Clock : GameHud.GoalSprite(session.Level),
                        -1, Str.TryAgain, Str.Map);
                    Audio.PlayGameOver();
                    break;
                }

                case GameMode.Daily:
                {
                    // Not solved this time. The attempt's minutes join the day's clock and the card
                    // offers the next attempt straight away — the puzzle is there to be finished.
                    Progress.DailyAttemptEnded(App.DailyDate, session.PlaySeconds);
                    bool outOfMoves = session.MovesLeft <= 0;
                    int left = session.GoalRemaining;

                    _resultPrimary = () => App.PlayDaily(fresh: true);
                    _resultSecondary = App.ShowMenu;
                    _result.Show(this, Str.DailyTitle(session.Level.Number), left.ToString(),
                        outOfMoves ? Str.NoMovesLeft : Str.NoRoom, Design.PreviewTint(3), GameHud.GoalSprite(session.Level),
                        -1, Str.TryAgain, Str.MainMenu);
                    Audio.PlayGameOver();
                    break;
                }

                default:
                {
                    int rank = HighScores.Submit(session.Score, session.LinesCleared);
                    string note;
                    Color color;

                    if (rank == 1) { note = Str.NewRecord; color = Design.Gold; Audio.PlayFanfare(); }
                    else if (rank > 1) { note = Str.Rank(rank); color = Design.Mint; Audio.PlayGameOver(); }
                    else { note = Str.Lines(session.LinesCleared); color = Design.TextTertiary; Audio.PlayGameOver(); }

                    _resultPrimary = () => App.PlayClassic(fresh: true);
                    _resultSecondary = App.ShowMenu;
                    _result.Show(this, Str.GameOver, session.Score.ToString(), note, color, null, -1,
                        Str.PlayAgain, Str.MainMenu);
                    if (rank == 1) _result.Celebrate(this);
                    break;
                }
            }

            App.Vibrate();
        }

        void OnLevelWon()
        {
            CancelDrag();
            CancelAim();

            if (_session.Mode == GameMode.Daily)
            {
                OnDailySolved();
                return;
            }

            var session = _session;
            int n = session.Level.Number;
            int stars = session.StarsEarned;

            int starsBefore = Progress.TotalStars;
            Progress.RecordLevel(n, stars);
            Progress.RecordLevelResult(won: true);
            var unlocked = NewlyUnlockedTheme(starsBefore, Progress.TotalStars);
            Progress.RecordRun(session);
            RunStore.Clear(GameMode.Level);
            SyncState();

            bool last = n >= LevelGenerator.LevelCount;
            bool worldDone = LevelGenerator.PlaceInWorld(n) == LevelGenerator.WorldSize && Progress.ChestReady(LevelGenerator.WorldOf(n));

            // Built while the stars pop, so "next" starts without a pause.
            if (!last) System.Threading.ThreadPool.QueueUserWorkItem(_ => LevelGenerator.Generate(n + 1, Design.PaletteSize));

            // Next goes by way of the map: the road is drawn on to the next level and its sheet
            // opens there — the saga games' walk from one level to the next.
            _resultPrimary = () => App.ShowLevelSelect(afterWin: n);
            _resultSecondary = App.ShowLevelSelect;

            string title = last ? Str.AdventureDone : Str.LevelTitle(n);
            string primary = last || worldDone ? Str.Map : Str.Next;
            string secondary = last ? Str.MainMenu : Str.Map;
            if (last) _resultSecondary = App.ShowMenu;

            // A world's chest waiting beats a theme, and a theme beats the moves saved.
            if (worldDone)
            {
                _result.Show(this, title, session.Score.ToString(), Str.Upper(Str.WorldChest),
                    Design.Gold, Icons.Chest, stars, primary, secondary, i => Audio.PlayStar(i));
            }
            else if (unlocked != null)
            {
                _result.Show(this, title, session.Score.ToString(),
                    Str.NewTheme(unlocked.Name),
                    Design.Gold, Icons.Palette, stars, primary, secondary, i => Audio.PlayStar(i));
            }
            else
            {
                int spare = Mathf.Max(0, session.MovesLeft - session.Config.ExtraMoves);
                _result.Show(this, title, session.Score.ToString(),
                    Str.MovesSaved(spare), Design.Mint, Icons.Check, stars, primary, secondary,
                    i => Audio.PlayStar(i));
            }

            _board.PlayBoardWave();
            _result.Celebrate(this);
            Audio.PlayFanfare();
            App.Vibrate(strong: true);
        }

        /// <summary>
        /// Today's puzzle, solved. The card leads with the time — the number a daily puzzle is
        /// compared on — then the stars; the note is the streak, or a badge when one was earned
        /// (the streak is on the daily card a tap away). Sharing is the main action, the way it is
        /// on every daily puzzle people pass around.
        /// </summary>
        void OnDailySolved()
        {
            var session = _session;
            var date = App.DailyDate;
            int number = session.Level.Number;
            int stars = session.StarsEarned;
            float seconds = DailyClock;
            int attempts = Mathf.Max(1, Progress.DailyAttempts(date));

            int starsBefore = Progress.TotalStars;
            var badgesBefore = DailyBadges.Snapshot();
            int streak = Progress.RecordDailySolve(date, seconds, session.MovesUsed, stars, DateTime.Now);
            var newBadges = DailyBadges.NewSince(badgesBefore);
            var unlocked = NewlyUnlockedTheme(starsBefore, Progress.TotalStars);

            Progress.RecordRun(session);
            RunStore.Clear(GameMode.Daily);
            SyncState();
            Reminder.Refresh();

            _resultPrimary = () => ShareSheet.ShareText(
                Str.ShareText(number, seconds, stars, attempts, streak, ShareSheet.StoreLink), Str.Share);
            _resultSecondary = App.ShowMenu;

            string note;
            Color noteColor;
            Sprite noteIcon;
            if (newBadges.Count > 0)
            {
                var badge = newBadges[newBadges.Count - 1];
                note = Str.NewBadge(badge.Name);
                noteColor = badge.Tint();
                noteIcon = badge.Icon();
            }
            else if (unlocked != null)
            {
                note = Str.NewTheme(unlocked.Name);
                noteColor = Design.Gold;
                noteIcon = Icons.Palette;
            }
            else
            {
                note = Str.DayStreak(streak);
                noteColor = Design.Gold;
                noteIcon = Icons.Flame;
            }

            _result.Show(this, Str.DailyTitle(number), Str.Clock(seconds), note, noteColor, noteIcon, stars,
                Str.Share, Str.MainMenu, i => Audio.PlayStar(i));

            _board.PlayBoardWave();
            _result.Celebrate(this, 60);
            Audio.PlayFanfare();
            App.Vibrate(strong: true);

            // After the stars: the note line lands with its own sound — the streak ticking over,
            // or a badge.
            StartCoroutine(After(1.25f, () =>
            {
                if (!_result.Visible) return;
                StartCoroutine(Tween.Punch(_result.Note, 0.22f, 0.3f));
                StartCoroutine(Tween.Punch(_result.NoteIcon, 0.4f, 0.34f));
                if (newBadges.Count > 0) Audio.PlayBadge();
                else Audio.PlayStreak();
            }));

        }

        static Themes.Theme NewlyUnlockedTheme(int before, int after)
        {
            foreach (var theme in Themes.All)
                if (theme.StarsToUnlock > before && theme.StarsToUnlock <= after)
                    return theme;
            return null;
        }

        // ------------------------------------------------------------------ pointer

        public void OnPointerDown(Vector2 screenPoint)
        {
            if (_result.Visible || _session == null) return;

            if (_aiming == PowerKind.Bomb || _aiming == PowerKind.Hammer)
            {
                if (_board.ContainsScreenPoint(screenPoint))
                {
                    _bombHeld = true;
                    AimBomb(screenPoint);
                }
                else
                {
                    CancelAim();
                }

                return;
            }

            if (_aiming == PowerKind.Rotate)
            {
                int target = _tray.SlotAtScreenPoint(screenPoint);
                if (target >= 0 && _session.CanRotate(target)) _session.TryRotate(target);
                else CancelAim();
                return;
            }

            if (_session.State != SessionState.Playing) return;

            int slot = _tray.SlotAtScreenPoint(screenPoint);
            if (slot < 0) return;

            _dragPiece = _tray.TakePiece(slot);
            if (_dragPiece == null) return;

            _dragSlot = slot;
            _dragging = true;
            _hoverKnown = false;
            Audio.PlayPickup();
            if (_hand.gameObject.activeSelf) _hand.gameObject.SetActive(false);

            _dragPiece.Rect.SetParent(_dragLayer, false);
            StartCoroutine(Tween.Scale(_dragPiece.Rect, _dragPiece.Rect.localScale, Vector3.one, 0.12f, Ease.OutCubic));
            MoveDragPiece(screenPoint);
        }

        public void OnPointerDrag(Vector2 screenPoint)
        {
            if (_bombHeld)
            {
                AimBomb(screenPoint);
                return;
            }

            if (!_dragging) return;

            MoveDragPiece(screenPoint);

            // Out-of-range coordinates are still worth testing: CanPlace rejects them and the ghost
            // then shows the shape hanging off the edge in its invalid tint.
            _board.TryGetCellFromScreenPoint(_dragPiece.OriginCellScreenPoint, out int col, out int row);

            // The board cannot change mid-drag, so the ghost and the preview only change when the
            // anchor crosses into another cell. Rebuilding them every frame toggled a dozen images
            // on and off and dirtied the canvas for nothing.
            if (_hoverKnown && col == _hoverCol && row == _hoverRow) return;
            _hoverCol = col;
            _hoverRow = row;
            _hoverKnown = true;

            bool valid = _session.CanPlace(_dragSlot, col, row);
            _board.ShowGhost(_dragPiece.Shape, col, row, valid);

            // Light up whatever this drop would clear, while it can still be moved.
            if (valid)
            {
                _board.PreviewLines(_dragPiece.Shape, col, row);
            }
            else
            {
                _board.HideLinePreview();
            }
        }

        public void OnPointerUp(Vector2 screenPoint)
        {
            if (_bombHeld)
            {
                _bombHeld = false;
                _board.HideGhost();

                bool onCell = _board.TryGetCellFromScreenPoint(screenPoint, out int bc, out int br);
                if (_aiming == PowerKind.Hammer)
                {
                    if (onCell && _session.CanHammer(bc, br) && Progress.SpendBooster(Booster.Hammer))
                    {
                        _session.HasBoosterRescue = HammerAvailable(_session);
                        _session.TryHammer(bc, br);
                    }
                    else
                    {
                        Audio.PlayInvalid();
                    }

                    return;
                }

                if (onCell && _session.CanBomb(bc, br))
                    _session.TryBomb(bc, br);
                else
                    Audio.PlayInvalid();

                return;
            }

            if (!_dragging) return;

            MoveDragPiece(screenPoint);
            _board.HideGhost();
            _board.HideLinePreview();
            _board.TryGetCellFromScreenPoint(_dragPiece.OriginCellScreenPoint, out int col, out int row);

            // TryPlace can end the run synchronously, and OnGameOver calls CancelDrag — which would
            // hand this very piece back to the tray and null the field out from under us. Let go of
            // the drag state first, so the placement runs against a screen that is already settled.
            var piece = _dragPiece;
            int slot = _dragSlot;

            _dragPiece = null;
            _dragSlot = -1;
            _dragging = false;

            var result = _session.TryPlace(slot, col, row);
            if (result != null)
            {
                Destroy(piece.gameObject);
            }
            else
            {
                Audio.PlayInvalid();
                _tray.ReturnPiece(slot, piece);
                if (!Progress.TutorialSeen) MaybeStartTutorial();
            }
        }

        void AimBomb(Vector2 screenPoint)
        {
            if (!_board.TryGetCellFromScreenPoint(screenPoint, out int col, out int row))
                _board.HideGhost();
            else if (_aiming == PowerKind.Hammer)
                _board.ShowHammerPreview(col, row);
            else
                _board.ShowBombPreview(col, row);
        }

        /// <summary>Puts a held piece back where it came from, for when the screen loses focus.</summary>
        void CancelDrag()
        {
            if (_dragging && _dragPiece != null)
            {
                _board.HideGhost();
                _board.HideLinePreview();
                _tray.ReturnPiece(_dragSlot, _dragPiece);
            }

            _dragPiece = null;
            _dragSlot = -1;
            _dragging = false;
        }

        /// <summary>Keeps the held piece centred on the finger but lifted clear of it.</summary>
        void MoveDragPiece(Vector2 screenPosition)
        {
            if (_dragPiece == null) return;

            float scale = App.CanvasScale;
            _dragPiece.Rect.position = new Vector3(
                screenPosition.x,
                screenPosition.y + _liftPixels * scale,
                0f);
        }

        // ------------------------------------------------------------------ tutorial

        /// <summary>
        /// The first run teaches the one gesture without a word: a hand lifts the first piece out
        /// of the tray and sets it on the board, over and over, until the player does it.
        /// </summary>
        void MaybeStartTutorial()
        {
            if (Progress.TutorialSeen || _session == null || _session.Mode != GameMode.Classic || _session.PlacedPieces > 0)
                return;

            StopTutorial();
            _tutorial = StartCoroutine(TutorialRoutine());
        }

        void StopTutorial()
        {
            if (_tutorial != null) StopCoroutine(_tutorial);
            _tutorial = null;
            if (_hand != null) _hand.gameObject.SetActive(false);
        }

        IEnumerator TutorialRoutine()
        {
            yield return new WaitForSecondsRealtime(0.8f);

            while (true)
            {
                int slot = -1;
                for (int i = 0; i < _session.Tray.Length; i++)
                    if (!_session.Tray[i].Used) { slot = i; break; }
                if (slot < 0) yield break;

                var shape = _session.Tray[slot].Shape;
                int col = (_session.Board.Size - shape.Width) / 2;
                int row = (_session.Board.Size - shape.Height) / 2;

                var from = _tray.Slot(slot).position;
                var to = _board.CellWorldPosition(col, row);

                _hand.rectTransform.SetAsLastSibling();
                _hand.rectTransform.position = from;
                _hand.gameObject.SetActive(!_dragging);

                for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
                {
                    _hand.color = Color.white.WithAlpha(t / 0.25f);
                    yield return null;
                }

                for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
                {
                    _hand.rectTransform.position = Vector3.Lerp(from, to, Ease.OutCubic(t / 0.9f));
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(0.3f);

                for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                {
                    _hand.color = Color.white.WithAlpha(1f - t / 0.3f);
                    yield return null;
                }

                _hand.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(0.6f);
            }
        }

        // ------------------------------------------------------------------ automation

#if PRIZMA_AUTOTEST
        /// <summary>
        /// Test hook: plays one move the way the computer player would. The bot places straight
        /// into the session, so the tray view drops the pieces a finger would have carried off.
        /// </summary>
        public bool AutoStep(Autoplayer bot)
        {
            if (_session == null) return false;
            bool moved = bot.Step(_session);

            for (int i = 0; i < _session.Tray.Length; i++)
            {
                if (!_session.Tray[i].Used || _tray.Piece(i) == null) continue;
                Destroy(_tray.TakePiece(i).gameObject);
            }

            return moved;
        }

        /// <summary>Test hook: taps a power button.</summary>
        public void AutoPower(PowerKind kind) => OnPowerClicked(kind);

        /// <summary>Test hook: holds the armed bomb over a cell.</summary>
        public void AutoBombPreview(int col, int row) => _board.ShowBombPreview(col, row);

        /// <summary>Test hook: holds the armed hammer over a cell.</summary>
        public void AutoHammerPreview(int col, int row) => _board.ShowHammerPreview(col, row);

        /// <summary>Test hook: brings the hammer down on a cell, as a release there would.</summary>
        public bool AutoHammer(int col, int row)
        {
            if (_session == null || !_session.CanHammer(col, row) || !Progress.SpendBooster(Booster.Hammer)) return false;
            _session.HasBoosterRescue = HammerAvailable(_session);
            return _session.TryHammer(col, row) != null;
        }

        /// <summary>
        /// Test hook: holds a tray piece over a cell without a finger, so a shot can catch the
        /// ghost and the pre-clear preview — the one part of the board a still picture could never
        /// reach before, and the part hardest to judge on a desktop.
        /// </summary>
        public int AutoHover(int trayIndex, int col, int row)
        {
            if (_session == null) return 0;

            var shape = _session.Tray[trayIndex].Shape;
            if (shape == null) return 0;

            _board.ShowGhost(shape, col, row, _session.CanPlace(trayIndex, col, row));
            return _board.PreviewLines(shape, col, row);
        }

        /// <summary>
        /// Test hook: drops a tray piece on an exact cell. Unlike <see cref="AutoStep"/> the cell
        /// is chosen by the caller, so a shot can be timed against a clear it knows is coming.
        /// </summary>
        public bool AutoPlace(int trayIndex, int col, int row)
        {
            if (_session == null) return false;

            _board.HideGhost();
            if (_session.TryPlace(trayIndex, col, row) == null) return false;

            for (int i = 0; i < _session.Tray.Length; i++)
            {
                if (!_session.Tray[i].Used || _tray.Piece(i) == null) continue;
                Destroy(_tray.TakePiece(i).gameObject);
            }

            return true;
        }
#endif
    }
}
