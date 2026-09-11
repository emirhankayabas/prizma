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
        const float HudHeight = 440f;
        const float TrayHeight = 280f;
        const float TrayBottomOffset = 60f;
        const float PowerBarHeight = 110f;
        const float PowerBarGap = 20f;

        /// <summary>Everything stacked under the board: the tray and the power bar over it.</summary>
        const float BottomStack = TrayBottomOffset + TrayHeight + PowerBarGap + PowerBarHeight;

        /// <summary>
        /// The board is centred in the band the HUD and the bottom stack leave behind, not on the
        /// canvas. Written as a difference so it follows those when they move, and because the
        /// canvas is taller than 1920 on a long phone — the surplus has to split evenly above and
        /// below the board instead of pooling under it.
        /// </summary>
        public const float BoardCenterY = (BottomStack - HudHeight) * 0.5f;

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

        Image _hand;
        Coroutine _tutorial;
        Coroutine _powerHint;

        readonly List<TextMeshProUGUI> _popupPool = new List<TextMeshProUGUI>();
        readonly List<Image> _flyPool = new List<Image>();

        public GameSession Session => _session;

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            float boardSize = 1080f - Design.Gutter * 2f;

            BuildBoard(boardSize);

            _hud = new GameHud();
            _hud.Build(Root, HudHeight, () => { Audio.PlayClick(); App.OpenPause(); });

            _powers = new PowerBar();
            _powers.Build(Root, boardSize, PowerBarHeight, TrayBottomOffset + TrayHeight + PowerBarGap);
            _powers.PowerClicked += OnPowerClicked;
            _powers.EndClicked += () => { Audio.PlayClick(); _session?.Concede(); };

            BuildTray(boardSize);

            _popupLayer = UiBuilder.Child(Root, "Popups");
            _flyLayer = UiBuilder.Child(Root, "Fly");
            _dragLayer = UiBuilder.Child(Root, "DragLayer");

            // The held piece moves every frame; on its own canvas that re-batches one piece
            // instead of the board, the HUD and the tray along with it. Flying crystals likewise.
            _dragLayer.gameObject.AddComponent<Canvas>();
            _flyLayer.gameObject.AddComponent<Canvas>();

            _hand = UiBuilder.Image(Root, "TutorialHand", Icons.Hand, Color.white);
            _hand.type = Image.Type.Simple;
            _hand.rectTransform.sizeDelta = new Vector2(150f, 150f);
            // The fingertip, not the middle of the glyph, is what lands on the target.
            _hand.rectTransform.pivot = new Vector2(0.45f, 0.94f);
            _hand.gameObject.AddComponent<Canvas>();
            _hand.gameObject.SetActive(false);

            _result = new ResultCard();
            _result.Build(Root);
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

        void BuildTray(float boardSize)
        {
            var trayGo = new GameObject("Tray", typeof(RectTransform));
            trayGo.transform.SetParent(Root, false);

            var trayRect = (RectTransform)trayGo.transform;
            trayRect.anchorMin = trayRect.anchorMax = new Vector2(0.5f, 0f);
            trayRect.pivot = new Vector2(0.5f, 0f);
            trayRect.anchoredPosition = new Vector2(0f, TrayBottomOffset);

            _tray = trayGo.AddComponent<TrayView>();
            _tray.Build(boardSize, TrayHeight, _board.CellSize, _board.Gap, GameSession.TraySlots);
        }

        // ------------------------------------------------------------------ run lifecycle

        protected override void OnShow()
        {
            PointerRouter.Fallback = this;
            Progress.Changed += OnProgressChanged;
        }

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

            _board.Bind(session.Board);
            _tray.Refresh(session);

            _displayedScore = session.Score;
            _hud.Bind(session);
            _hud.Refresh(_displayedScore);

            _result.Hide();
            _aiming = null;
            _bombHeld = false;
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
        }

        // ------------------------------------------------------------------ moves

        void OnMoved(MoveResult result)
        {
            // The drop already happened, so whatever was being previewed is now history.
            // Cleared here rather than only in OnPointerUp so any caller leaves a clean board.
            _board.HideLinePreview();

            _board.Refresh();
            _board.PlayPlacePop(result.PlacedShape, result.PlacedColumn, result.PlacedRow);
            Audio.PlayPlace();
            App.Tick();

            if (_tutorial != null || !Progress.TutorialSeen)
            {
                StopTutorial();
                Progress.TutorialSeen = true;
            }

            var clear = result.Clear;
            if (clear.Any)
            {
                var tint = Design.Blocks[result.PlacedColorIndex % Design.Blocks.Length];
                _board.PlayLineSweep(clear, Color.white);
                _board.PlayClearBurst(clear.ClearedCells, Color.white);
                _board.PulseEdge(tint);

                if (clear.MonoLines > 0)
                {
                    _board.PlayPrismSweep(clear);
                    Audio.PlayPrism();
                }

                if (clear.CrackedIce.Count > 0)
                {
                    _board.PlayIceCrack(clear.CrackedIce);
                    Audio.PlayIce();
                }

                if (clear.CollectedGems.Count > 0)
                    FlyCrystals(clear.CollectedGems);

                if (clear.PerfectClear)
                {
                    _board.PlayBoardWave();
                    _board.PulseEdge(Design.Gold);
                    Audio.PlayFanfare();
                }

                _hud.ShowCombo(this, result.ComboStreak);

                var popupCells = clear.ClearedCells.Count > 0 ? clear.ClearedCells : clear.CrackedIce;
                ShowScorePopup(result.ClearScore, popupCells, clear.PerfectClear ? Design.Gold : tint, clear.PerfectClear);

                if (result.ComboStreak > 1) Audio.PlayCombo(result.ComboStreak);
                else Audio.PlayClear();

                if (clear.LinesCleared > 1 || clear.PerfectClear)
                {
                    // The board itself takes the hit: a small swell that grows with the clear.
                    float punch = clear.PerfectClear ? 0.045f : 0.012f * Mathf.Min(clear.LinesCleared, 4);
                    StartCoroutine(Tween.Punch(_board.transform, punch, 0.24f));
                    App.Vibrate();
                }
            }
            else
            {
                _hud.HideCombo();
            }

            if (result.ChargesGained > 0)
            {
                _powers.CelebrateCharge(this, _session.Charges);
                Audio.PlayCharge();
            }

            if (result.TrayRefilled)
                _tray.Refresh(_session);

            UpdateScore();
            SyncState();
            SaveRun();
        }

        void UpdateScore()
        {
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = StartCoroutine(ScoreRoutine(_displayedScore, _session.Score));
        }

        IEnumerator ScoreRoutine(int from, int to)
        {
            StartCoroutine(Tween.Punch(_hud.Readout, 0.12f, 0.24f));

            yield return Tween.CountUp(from, to, 0.32f, v =>
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
            label.rectTransform.sizeDelta = new Vector2(600f, 140f);
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
                crystal.rectTransform.sizeDelta = new Vector2(_board.CellSize * 0.66f, _board.CellSize * 0.66f);
                crystal.rectTransform.position = _board.CellWorldPosition(cells[i].X, cells[i].Y);
                crystal.gameObject.SetActive(true);
                StartCoroutine(FlyRoutine(crystal, target, i * 0.08f, toGoal));
            }
        }

        IEnumerator FlyRoutine(Image crystal, Vector3 target, float delay, bool toGoal)
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
            Audio.PlayGem();
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
                    Audio.PlayBomb();
                    App.Vibrate();
                    UpdateScore();
                    break;
            }

            _hud.Refresh(_displayedScore);
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
            int left = Mathf.Max(0, session.Level.Target - session.GoalProgress);

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

            _result.Show(this, "Hamle Bitti", left.ToString(), $"{PowerRules.ExtraMovesCost} ŞARJ", Design.Prism, Art.Crystal,
                -1, $"+{PowerRules.ExtraMoves} HAMLE", "BİTİR");
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
            RunStore.Clear(session.Mode);
            Progress.RecordRun(session);
            SyncState();

            switch (session.Mode)
            {
                case GameMode.Level:
                {
                    int n = session.Level.Number;
                    bool outOfMoves = session.MovesLeft <= 0;
                    int left = Mathf.Max(0, session.Level.Target - session.GoalProgress);

                    _resultPrimary = () => App.PlayLevel(n);
                    _resultSecondary = App.ShowLevelSelect;
                    _result.Show(this, $"Bölüm {n}", left.ToString(),
                        outOfMoves ? "HAMLE BİTTİ" : "YER KALMADI", Design.PreviewTint(3), GoalSprite(session.Level.Goal),
                        -1, "TEKRAR DENE", "HARİTA");
                    Audio.PlayGameOver();
                    break;
                }

                case GameMode.Daily:
                {
                    bool best = Progress.RecordDaily(session.Score);
                    int streak = Progress.DailyStreak;

                    _resultPrimary = () => App.PlayDaily(fresh: true);
                    _resultSecondary = App.ShowMenu;
                    _result.Show(this, best ? "Bugünün Rekoru" : "Günlük Bulmaca", session.Score.ToString(),
                        $"{streak} GÜN SERİ", Design.Gold, Icons.Flame, -1, "TEKRAR OYNA", "ANA MENÜ");

                    if (best) Audio.PlayFanfare();
                    else Audio.PlayGameOver();
                    break;
                }

                default:
                {
                    int rank = HighScores.Submit(session.Score, session.LinesCleared);
                    string note;
                    Color color;

                    if (rank == 1) { note = "YENİ REKOR"; color = Design.Gold; Audio.PlayFanfare(); }
                    else if (rank > 1) { note = $"{rank}. SIRA"; color = Design.Mint; Audio.PlayGameOver(); }
                    else { note = $"{session.LinesCleared} SATIR"; color = Design.TextTertiary; Audio.PlayGameOver(); }

                    _resultPrimary = () => App.PlayClassic(fresh: true);
                    _resultSecondary = App.ShowMenu;
                    _result.Show(this, "Oyun Bitti", session.Score.ToString(), note, color, null, -1,
                        "TEKRAR OYNA", "ANA MENÜ");
                    break;
                }
            }

            App.Vibrate();
        }

        void OnLevelWon()
        {
            CancelDrag();
            CancelAim();

            var session = _session;
            int n = session.Level.Number;
            int stars = session.StarsEarned;

            int starsBefore = Progress.TotalStars;
            Progress.RecordLevel(n, stars);
            var unlocked = NewlyUnlockedTheme(starsBefore, Progress.TotalStars);
            Progress.RecordRun(session);
            RunStore.Clear(GameMode.Level);
            SyncState();

            // Built while the stars pop, so "next" starts without a pause.
            System.Threading.ThreadPool.QueueUserWorkItem(_ => LevelGenerator.Generate(n + 1, Design.PaletteSize));

            _resultPrimary = () => App.PlayLevel(n + 1);
            _resultSecondary = App.ShowLevelSelect;

            // A theme crossing its star threshold is news worth the note line; otherwise the moves saved.
            if (unlocked != null)
            {
                _result.Show(this, $"Bölüm {n}", session.Score.ToString(),
                    "YENİ TEMA: " + unlocked.Name.ToUpper(new System.Globalization.CultureInfo("tr-TR")),
                    Design.Gold, Icons.Palette, stars, "SONRAKİ", "HARİTA", i => Audio.PlayStar(i));
            }
            else
            {
                _result.Show(this, $"Bölüm {n}", session.Score.ToString(),
                    $"{session.MovesLeft} HAMLE ARTTI", Design.Mint, Icons.Check, stars, "SONRAKİ", "HARİTA",
                    i => Audio.PlayStar(i));
            }

            _board.PlayBoardWave();
            Audio.PlayFanfare();
            App.Vibrate(strong: true);
        }

        static Themes.Theme NewlyUnlockedTheme(int before, int after)
        {
            foreach (var theme in Themes.All)
                if (theme.StarsToUnlock > before && theme.StarsToUnlock <= after)
                    return theme;
            return null;
        }

        static Sprite GoalSprite(GoalKind goal)
        {
            switch (goal)
            {
                case GoalKind.Lines: return Icons.Rows;
                case GoalKind.Score: return Icons.Star;
                default: return Art.Crystal;
            }
        }

        // ------------------------------------------------------------------ pointer

        public void OnPointerDown(Vector2 screenPoint)
        {
            if (_result.Visible || _session == null) return;

            if (_aiming == PowerKind.Bomb)
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

                if (_board.TryGetCellFromScreenPoint(screenPoint, out int bc, out int br) && _session.CanBomb(bc, br))
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
            if (_board.TryGetCellFromScreenPoint(screenPoint, out int col, out int row))
                _board.ShowBombPreview(col, row);
            else
                _board.HideGhost();
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
#endif
    }
}
