using System.Collections;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The play page: HUD, board, tray and the game-over card. Pointer input arrives through
    /// <see cref="PointerRouter"/> as the fallback handler, so buttons always win over the board.
    /// </summary>
    public sealed class GameScreen : AppScreen, IPointerFallback
    {
        const float BoardPadding = 20f;
        const float BoardGap = 10f;
        const float TrayHeight = 300f;
        const float TrayBottomOffset = 100f;
        const float BoardCenterY = 30f;

        GameSession _session;

        BoardView _board;
        TrayView _tray;
        RectTransform _dragLayer;

        TextMeshProUGUI _scoreLabel;
        TextMeshProUGUI _bestLabel;

        RectTransform _comboChip;
        Image _comboChipFill;
        Image _comboChipEdge;
        TextMeshProUGUI _comboLabel;


        RectTransform _overPanel;
        Image _overScrim;
        RectTransform _overCard;
        TextMeshProUGUI _overScore;
        TextMeshProUGUI _overNote;

        PieceView _dragPiece;
        int _dragSlot = -1;
        bool _dragging;

        int _displayedScore;
        int _linesThisRun;
        float _liftPixels;

        Coroutine _scoreRoutine;

        RectTransform _popupLayer;
        readonly System.Collections.Generic.List<TextMeshProUGUI> _popupPool =
            new System.Collections.Generic.List<TextMeshProUGUI>();

        // ------------------------------------------------------------------ build

        protected override void Build()
        {
            float boardSize = 1080f - Design.Gutter * 2f;

            BuildBoard(boardSize);
            BuildHud();
            BuildTray(boardSize);


            _popupLayer = UiBuilder.Child(Root, "Popups");
            _dragLayer = UiBuilder.Child(Root, "DragLayer");

            BuildGameOver();
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

        void BuildHud()
        {
            var hud = UiBuilder.Node(Root, "Hud");
            hud.anchorMin = hud.anchorMax = new Vector2(0.5f, 1f);
            hud.pivot = new Vector2(0.5f, 1f);
            hud.sizeDelta = new Vector2(1080f, 440f);

            // Best score, marked with a gem rather than the crown the genre usually reaches for.
            var gem = UiBuilder.Image(hud, "Gem", Icons.Gem, Design.Gold);
            gem.type = Image.Type.Simple;
            gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0f, 1f);
            gem.rectTransform.pivot = new Vector2(0f, 1f);
            gem.rectTransform.sizeDelta = new Vector2(44f, 44f);
            gem.rectTransform.anchoredPosition = new Vector2(Design.Gutter, -Design.Space6);

            _bestLabel = UiBuilder.Label(hud, "Best", "0", Design.Headline, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Left);
            _bestLabel.rectTransform.anchorMin = _bestLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _bestLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _bestLabel.rectTransform.sizeDelta = new Vector2(300f, 64f);
            _bestLabel.rectTransform.anchoredPosition = new Vector2(Design.Gutter + 62f, -Design.Space6 + 8f);

            // One control in the corner. It pauses; settings and quitting live inside that menu,
            // which is where a player looks for them mid-run.
            var pause = UiBuilder.Button(hud, "Pause", new Vector2(104f, 104f), UiButton.Style.Icon,
                null, Design.Body, Icons.Pause);
            pause.Rect.anchorMin = pause.Rect.anchorMax = new Vector2(1f, 1f);
            pause.Rect.pivot = new Vector2(1f, 1f);
            pause.Rect.anchoredPosition = new Vector2(-Design.Gutter, -Design.Space5);
            pause.Clicked += () => { Audio.PlayClick(); App.OpenPause(); };

            _scoreLabel = UiBuilder.Label(hud, "Score", "0", Design.Readout, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _scoreLabel.rectTransform.anchorMin = _scoreLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _scoreLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            // A tight box: the default line box would reserve enough room to collide with the chip.
            _scoreLabel.rectTransform.sizeDelta = new Vector2(960f, 170f);
            _scoreLabel.rectTransform.anchoredPosition = new Vector2(0f, -170f);
            UiBuilder.TextShadow(_scoreLabel, 0.45f, -0.3f, 0.45f);

            // The combo chip stays on screen for as long as the streak is alive, rather than
            // flashing once. A multiplier the player cannot see is a multiplier they cannot chase.
            _comboChip = UiBuilder.Node(hud, "ComboChip");
            _comboChip.anchorMin = _comboChip.anchorMax = new Vector2(0.5f, 1f);
            _comboChip.pivot = new Vector2(0.5f, 1f);
            _comboChip.sizeDelta = new Vector2(330f, 72f);
            _comboChip.anchoredPosition = new Vector2(0f, -352f);

            _comboChipFill = UiBuilder.Panel(_comboChip, "Fill", _comboChip.sizeDelta,
                Design.SurfaceInset, 36f);
            _comboChipEdge = UiBuilder.Hairline(_comboChip, "Edge", _comboChip.sizeDelta, 36f, Design.Mint.WithAlpha(0.5f));

            _comboLabel = UiBuilder.Label(_comboChip, "Label", "", Design.Body, Design.Mint,
                Design.FontDisplay, tracking: 4f);
            _comboLabel.rectTransform.sizeDelta = _comboChip.sizeDelta;

            _comboChip.gameObject.SetActive(false);
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

        void BuildGameOver()
        {
            _overPanel = UiBuilder.Node(Root, "GameOver");
            UiBuilder.Stretch(_overPanel);

            _overScrim = UiBuilder.Image(_overPanel, "Scrim", Art.Panel(0f), new Color(0.02f, 0.02f, 0.05f, 0.8f));
            _overScrim.type = Image.Type.Simple;
            UiBuilder.Stretch(_overScrim.rectTransform);

            var size = new Vector2(880f, 800f);
            _overCard = UiBuilder.Node(_overPanel, "Card");
            _overCard.sizeDelta = size;

            UiBuilder.Shadow(_overCard, "Shadow", size, Design.RadiusLg, Design.E3);
            UiBuilder.Panel(_overCard, "Fill", size, Design.SurfaceHigh, Design.RadiusLg);
            UiBuilder.Hairline(_overCard, "Hairline", size, Design.RadiusLg);

            var title = UiBuilder.Label(_overCard, "Title", "Oyun Bitti", Design.Title, Design.TextSecondary,
                Design.FontMedium);
            title.rectTransform.anchoredPosition = new Vector2(0f, 270f);

            _overScore = UiBuilder.Label(_overCard, "Score", "0", Design.Readout, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _overScore.rectTransform.anchoredPosition = new Vector2(0f, 130f);

            _overNote = UiBuilder.Label(_overCard, "Note", "", Design.Label, Design.Mint,
                Design.FontMedium, tracking: Design.TrackingLabel);
            _overNote.rectTransform.anchoredPosition = new Vector2(0f, 24f);

            var replay = UiBuilder.Button(_overCard, "Replay", new Vector2(600f, 156f), UiButton.Style.Primary,
                "TEKRAR OYNA", Design.Headline);
            replay.Rect.anchoredPosition = new Vector2(0f, -110f);
            replay.Clicked += () => { Audio.PlayClick(); StartNewRun(); };

            var menu = UiBuilder.Button(_overCard, "Menu", new Vector2(600f, 136f), UiButton.Style.Secondary,
                "ANA MENÜ", Design.Body);
            menu.Rect.anchoredPosition = new Vector2(0f, -280f);
            menu.Clicked += () => { Audio.PlayClick(); App.ShowMenu(); };

            _overPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ run lifecycle

        protected override void OnShow()
        {
            PointerRouter.Fallback = this;
            if (_session == null) StartNewRun();
        }

        protected override void OnHide()
        {
            if (ReferenceEquals(PointerRouter.Fallback, this))
                PointerRouter.Fallback = null;

            CancelDrag();
        }

        public void StartNewRun()
        {
            _session = GameSession.NewRandomRun(App.BoardSize, Design.Blocks.Length);
            _session.Moved += OnMoved;
            _session.GameOver += OnGameOver;

            _linesThisRun = 0;
            _displayedScore = 0;

            _board.Bind(_session.Board);
            _tray.Refresh(_session);

            _overPanel.gameObject.SetActive(false);
            HideCombo();

            _scoreLabel.text = "0";
            _bestLabel.text = HighScores.Best.ToString();
        }

        void OnMoved(MoveResult result)
        {
            // The drop already happened, so whatever was being previewed is now history.
            // Cleared here rather than only in OnPointerUp so any caller leaves a clean board.
            _board.HideLinePreview();

            _board.Refresh();
            _board.PlayPlacePop(result.PlacedShape, result.PlacedColumn, result.PlacedRow);
            Audio.PlayPlace();

            if (result.Clear.Any)
            {
                _linesThisRun += result.Clear.LinesCleared;

                var tint = Design.Blocks[result.PlacedColorIndex % Design.Blocks.Length];
                _board.PlayLineSweep(result.Clear, Color.white);
                _board.PlayClearBurst(result.Clear.ClearedCells, Color.white);
                _board.PulseEdge(tint);

                ShowCombo(result);
                ShowScorePopup(result.ClearScore, result.Clear.ClearedCells, tint);

                if (result.ComboStreak > 1) Audio.PlayCombo(result.ComboStreak);
                else Audio.PlayClear();

                if (result.Clear.LinesCleared > 1) App.Vibrate();
            }
            else
            {
                HideCombo();
            }

            if (result.TrayRefilled)
                _tray.Refresh(_session);

            UpdateScore();
        }

        void ShowCombo(MoveResult result)
        {
            // A streak only exists from the second consecutive clear onwards.
            if (result.ComboStreak < 2)
            {
                HideCombo();
                return;
            }

            float multiplier = ScoreRules.ComboMultiplier(result.ComboStreak);
            var tint = result.ComboStreak >= 4 ? Design.Gold : Design.Mint;

            _comboLabel.text = $"COMBO ×{multiplier:0.#}";
            _comboLabel.color = tint;
            _comboChipEdge.color = tint.WithAlpha(0.55f);

            if (!_comboChip.gameObject.activeSelf)
            {
                _comboChip.gameObject.SetActive(true);
                StartCoroutine(Tween.Scale(_comboChip, Vector3.one * 0.7f, Vector3.one, 0.2f, Ease.OutBack));
            }
            else
            {
                StartCoroutine(Tween.Punch(_comboChip, 0.18f, 0.26f));
            }
        }

        /// <summary>
        /// Floats the points earned up from where the clear happened. Putting the number at the
        /// clear rather than in the HUD is what ties the reward to the move that earned it.
        /// </summary>
        void ShowScorePopup(int amount, System.Collections.Generic.IReadOnlyList<CellOffset> cells, Color tint)
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
            label.gameObject.SetActive(true);

            StartCoroutine(PopupRoutine(label, start));
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
            label.rectTransform.sizeDelta = new Vector2(360f, 90f);
            UiBuilder.TextShadow(label, 0.5f, -0.3f, 0.4f);
            label.gameObject.SetActive(false);
            _popupPool.Add(label);
            return label;
        }

        void HideCombo()
        {
            if (_comboChip != null) _comboChip.gameObject.SetActive(false);
        }

        void UpdateScore()
        {
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = StartCoroutine(ScoreRoutine(_displayedScore, _session.Score));

            if (_session.Score > HighScores.Best)
                _bestLabel.text = _session.Score.ToString();
        }

        IEnumerator ScoreRoutine(int from, int to)
        {
            StartCoroutine(Tween.Punch(_scoreLabel.transform, 0.12f, 0.24f));

            yield return Tween.CountUp(from, to, 0.32f, v =>
            {
                _displayedScore = v;
                _scoreLabel.text = v.ToString();
            });

            _scoreRoutine = null;
        }

        void OnGameOver()
        {
            CancelDrag();

            int rank = HighScores.Submit(_session.Score, _linesThisRun);
            _overScore.text = _session.Score.ToString();

            if (rank == 1)
            {
                _overNote.text = "YENİ REKOR";
                _overNote.color = Design.Gold;
                Audio.PlayFanfare();
            }
            else if (rank > 1)
            {
                _overNote.text = $"{rank}. SIRA";
                _overNote.color = Design.Mint;
                Audio.PlayGameOver();
            }
            else
            {
                _overNote.text = $"{_linesThisRun} SATIR";
                _overNote.color = Design.TextTertiary;
                Audio.PlayGameOver();
            }

            _overPanel.SetAsLastSibling();
            _overPanel.gameObject.SetActive(true);
            App.Vibrate();

            StartCoroutine(Tween.FadeGraphic(_overScrim, 0f, 0.8f, 0.25f));
            StartCoroutine(Tween.Scale(_overCard, Vector3.one * 0.86f, Vector3.one, 0.32f, Ease.OutBack));
        }

        // ------------------------------------------------------------------ pointer

        public void OnPointerDown(Vector2 screenPoint)
        {
            if (_overPanel.gameObject.activeSelf || _session == null) return;

            int slot = _tray.SlotAtScreenPoint(screenPoint);
            if (slot < 0) return;

            _dragPiece = _tray.TakePiece(slot);
            if (_dragPiece == null) return;

            _dragSlot = slot;
            _dragging = true;
            Audio.PlayPickup();

            _dragPiece.Rect.SetParent(_dragLayer, false);
            StartCoroutine(Tween.Scale(_dragPiece.Rect, _dragPiece.Rect.localScale, Vector3.one, 0.12f, Ease.OutCubic));
            MoveDragPiece(screenPoint);
        }

        public void OnPointerDrag(Vector2 screenPoint)
        {
            if (!_dragging) return;

            MoveDragPiece(screenPoint);

            // Out-of-range coordinates are still worth testing: CanPlace rejects them and the ghost
            // then shows the shape hanging off the edge in its invalid tint.
            _board.TryGetCellFromScreenPoint(_dragPiece.OriginCellScreenPoint, out int col, out int row);

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
            }
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
    }
}
