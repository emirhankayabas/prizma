using System;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The strip above the board. One layout, filled differently per mode:
    /// classic shows the score against the all-time best; a level shows what is left of its goal
    /// and the moves left to do it in; the daily puzzle shows the same as a level, with the clock
    /// running where the level number would be.
    /// </summary>
    public sealed class GameHud
    {
        const float GoalIconSize = 124f;
        const float GoalIconGap = 20f;

        /// <summary>The height the HUD is laid out for; a short page squeezes it towards its minimum.</summary>
        public const float FullHeight = 540f;

        /// <summary>Top row: best score on the left, the pause button on the right, a chip between.</summary>
        const float RowTop = 16f;
        const float RowCenter = RowTop + Design.IconButton * 0.5f;
        const float ReadoutBox = 220f;
        const float ChipHeight = 112f;

        RectTransform _root;

        Image _leftIcon;
        TextMeshProUGUI _leftLabel;

        TextMeshProUGUI _readout;
        Image _goalIcon;

        RectTransform _chip;
        TextMeshProUGUI _chipLabel;

        RectTransform _comboChip;
        Image _comboChipEdge;
        TextMeshProUGUI _comboLabel;

        GameSession _session;
        float _readoutTop;

        public RectTransform Root => _root;
        public Transform Readout => _readout.transform;

        /// <summary>The number beside the corner icon: the best score in classic, the clock in the daily.</summary>
        public Transform LeftLabel => _leftLabel.transform;

        /// <summary>Where freed crystals fly to.</summary>
        public Vector3 GoalAnchor => _goalIcon.gameObject.activeSelf ? _goalIcon.rectTransform.position : _readout.rectTransform.position;

        public Transform GoalIcon => _goalIcon.transform;

        public void Build(RectTransform parent, float height, Action onPause)
        {
            _root = UiBuilder.Node(parent, "Hud");
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(1080f, height);

            // What a short page takes from the HUD comes out of the air between its three rows.
            float squeeze = Mathf.Max(0f, FullHeight - height);
            // Never above the top row's chip, though: on a 16:9 page it used to touch the moves chip.
            _readoutTop = Mathf.Min(-(RowTop + Design.IconButton + 4f) + squeeze * 0.4f, -(RowCenter + ChipHeight * 0.5f));

            // The best score reads as the second number on the page, so it gets the second size.
            const float bestIcon = Design.IconMd * 0.8f;
            _leftIcon = UiBuilder.Image(_root, "LeftIcon", Icons.Gem, Design.Gold);
            _leftIcon.type = Image.Type.Simple;
            _leftIcon.rectTransform.anchorMin = _leftIcon.rectTransform.anchorMax = new Vector2(0f, 1f);
            _leftIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            _leftIcon.rectTransform.sizeDelta = new Vector2(bestIcon, bestIcon);
            _leftIcon.rectTransform.anchoredPosition = new Vector2(Design.Gutter, -RowCenter);

            _leftLabel = UiBuilder.Label(_root, "LeftLabel", "0", Design.Headline, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Left);
            _leftLabel.rectTransform.anchorMin = _leftLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _leftLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            _leftLabel.rectTransform.sizeDelta = new Vector2(360f, 100f);
            _leftLabel.rectTransform.anchoredPosition = new Vector2(Design.Gutter + bestIcon + Design.Space2, -RowCenter + 3f);

            // One control in the corner. It pauses; settings and quitting live inside that menu,
            // which is where a player looks for them mid-run.
            var pause = UiBuilder.Button(_root, "Pause", new Vector2(Design.IconButton, Design.IconButton), UiButton.Style.Icon,
                null, Design.Body, Icons.Pause);
            pause.Rect.anchorMin = pause.Rect.anchorMax = new Vector2(1f, 1f);
            pause.Rect.pivot = new Vector2(1f, 1f);
            pause.Rect.anchoredPosition = new Vector2(-Design.Gutter, -RowTop);
            pause.Clicked += () => onPause?.Invoke();

            // A chip in the top row: moves left in a level, the date in the daily.
            _chip = UiBuilder.Node(_root, "Chip");
            _chip.anchorMin = _chip.anchorMax = new Vector2(0.5f, 1f);
            _chip.pivot = new Vector2(0.5f, 0.5f);
            _chip.sizeDelta = new Vector2(340f, ChipHeight);
            _chip.anchoredPosition = new Vector2(0f, -RowCenter);
            UiBuilder.Panel(_chip, "Fill", _chip.sizeDelta, Design.SurfaceInset, ChipHeight * 0.5f);
            UiBuilder.Hairline(_chip, "Edge", _chip.sizeDelta, ChipHeight * 0.5f);
            _chipLabel = UiBuilder.Label(_chip, "Label", "", Design.Label, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            _chipLabel.rectTransform.sizeDelta = _chip.sizeDelta;

            _readout = UiBuilder.Label(_root, "Readout", "0", Design.Readout, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _readout.rectTransform.anchorMin = _readout.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _readout.rectTransform.pivot = new Vector2(0.5f, 1f);
            // A tight box: the default line box would reserve enough room to collide with the chip.
            _readout.rectTransform.sizeDelta = new Vector2(1000f, ReadoutBox);
            _readout.rectTransform.anchoredPosition = new Vector2(0f, _readoutTop);
            UiBuilder.TextShadow(_readout, 0.45f, -0.3f, 0.45f);

            _goalIcon = UiBuilder.Image(_root, "GoalIcon", Art.Crystal, Design.Crystal);
            _goalIcon.type = Image.Type.Simple;
            _goalIcon.rectTransform.anchorMin = _goalIcon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _goalIcon.rectTransform.sizeDelta = new Vector2(GoalIconSize, GoalIconSize);
            _goalIcon.gameObject.SetActive(false);

            // The combo chip stays on screen for as long as the streak is alive, rather than
            // flashing once. A multiplier the player cannot see is a multiplier they cannot chase.
            _comboChip = UiBuilder.Node(_root, "ComboChip");
            _comboChip.anchorMin = _comboChip.anchorMax = new Vector2(0.5f, 1f);
            _comboChip.pivot = new Vector2(0.5f, 1f);
            _comboChip.sizeDelta = new Vector2(460f, ChipHeight);
            // Under the score, or pinned to the HUD's bottom edge when a short page has pulled that up.
            _comboChip.anchoredPosition = new Vector2(0f,
                Mathf.Max(_readoutTop - ReadoutBox - 8f, -(height - _comboChip.sizeDelta.y - 8f)));

            UiBuilder.Panel(_comboChip, "Fill", _comboChip.sizeDelta, Design.SurfaceInset, ChipHeight * 0.5f);
            _comboChipEdge = UiBuilder.Hairline(_comboChip, "Edge", _comboChip.sizeDelta, ChipHeight * 0.5f, Design.Mint.WithAlpha(0.5f));

            _comboLabel = UiBuilder.Label(_comboChip, "Label", "", Design.Headline, Design.Mint,
                Design.FontDisplay, tracking: Design.TrackingLabel * 0.5f);
            _comboLabel.rectTransform.sizeDelta = _comboChip.sizeDelta;

            _comboChip.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ binding

        public void Bind(GameSession session)
        {
            _session = session;
            _clockShown = -1;
            HideCombo();

            switch (session.Mode)
            {
                case GameMode.Daily:
                    // The puzzle is timed: the clock takes the corner, the goal and the moves are a level's.
                    SetLeft(Icons.Clock, Design.Mint, Design.FontDisplay, Design.Headline);
                    _goalIcon.gameObject.SetActive(true);
                    ConfigureGoalIcon(session.Level.Goal);
                    break;

                case GameMode.Level:
                    SetLeft(Icons.Flag, Design.TextPrimary, Design.FontDisplay, Design.Headline);
                    _goalIcon.gameObject.SetActive(true);
                    ConfigureGoalIcon(session.Level.Goal);
                    break;

                default:
                    SetLeft(Icons.Gem, Design.Gold, Design.FontDisplay, Design.Headline);
                    _chip.gameObject.SetActive(false);
                    _goalIcon.gameObject.SetActive(false);
                    break;
            }

            Refresh(session.Score);
        }

        void SetLeft(Sprite icon, Color color, TMP_FontAsset font, float size)
        {
            _leftIcon.sprite = icon;
            _leftIcon.color = color;
            _leftLabel.color = color;
            _leftLabel.font = font;
            _leftLabel.fontSize = size;
        }

        void ConfigureGoalIcon(GoalKind goal)
        {
            switch (goal)
            {
                case GoalKind.Lines:
                    _goalIcon.sprite = Icons.Rows;
                    _goalIcon.color = Design.Mint;
                    break;
                case GoalKind.Score:
                    _goalIcon.sprite = Icons.Star;
                    _goalIcon.color = Design.Gold;
                    break;
                default:
                    _goalIcon.sprite = Art.Crystal;
                    _goalIcon.color = Design.Crystal;
                    break;
            }
        }

        void ShowChip(string text, Color color)
        {
            _chip.gameObject.SetActive(true);
            _chipLabel.text = text;
            _chipLabel.color = color;
        }

        /// <summary>
        /// Repaints everything from the session. <paramref name="displayedScore"/> is the score the
        /// count-up animation has reached, which may trail the session's.
        /// </summary>
        public void Refresh(int displayedScore)
        {
            if (_session == null) return;

            switch (_session.Mode)
            {
                case GameMode.Daily:
                case GameMode.Level:
                {
                    var level = _session.Level;
                    // The flag already says "level". The word beside it was the one thing that
                    // did not fit next to the moves chip once the type grew. The daily's corner
                    // is the clock, which ticks on its own (SetClock).
                    if (_session.Mode == GameMode.Level) _leftLabel.text = level.Number.ToString();

                    int moves = _session.MovesLeft;
                    // Low on moves: the chip turns rose. A steady colour, not a pulse.
                    ShowChip(Str.Moves(moves),moves <= 5 ? Design.PreviewTint(3) : Design.TextPrimary);

                    int progress = level.Goal == GoalKind.Score ? displayedScore : _session.GoalProgress;
                    SetReadout(Mathf.Max(0, level.Target - progress).ToString());
                    break;
                }

                default:
                    _leftLabel.text = Mathf.Max(HighScores.Best, _session.Score).ToString();
                    SetReadout(displayedScore.ToString());
                    break;
            }
        }

        /// <summary>The daily's clock, total seconds on today's puzzle. Only repaints when the second changes.</summary>
        public void SetClock(float seconds)
        {
            int whole = Mathf.FloorToInt(seconds);
            if (whole == _clockShown) return;
            _clockShown = whole;
            _leftLabel.text = Str.Clock(whole);
        }

        int _clockShown = -1;

        void SetReadout(string text)
        {
            _readout.text = text;

            if (!_goalIcon.gameObject.activeSelf)
            {
                _readout.rectTransform.anchoredPosition = new Vector2(0f, _readoutTop);
                return;
            }

            // Icon and number centred as one group.
            float width = _readout.GetPreferredValues(text).x;
            float group = GoalIconSize + GoalIconGap + width;
            float left = -group * 0.5f;

            _goalIcon.rectTransform.anchoredPosition = new Vector2(left + GoalIconSize * 0.5f, _readoutTop - ReadoutBox * 0.5f);
            _readout.rectTransform.anchoredPosition = new Vector2(left + GoalIconSize + GoalIconGap + width * 0.5f, _readoutTop);
        }

        // ------------------------------------------------------------------ combo

        public void ShowCombo(MonoBehaviour host, int streak)
        {
            // A streak only exists from the second consecutive clear onwards.
            if (streak < 2)
            {
                HideCombo();
                return;
            }

            float multiplier = ScoreRules.ComboMultiplier(streak);
            // The chip climbs the same ladder as the light: mint, gold, rose, then the prism itself.
            var tint = streak >= 8 ? Design.Prism : streak >= 6 ? Design.PreviewTint(3) : streak >= 4 ? Design.Gold : Design.Mint;

            _comboLabel.text = Str.Combo(multiplier);
            _comboLabel.color = tint;
            _comboChipEdge.color = tint.WithAlpha(0.55f);

            if (!_comboChip.gameObject.activeSelf)
            {
                _comboChip.gameObject.SetActive(true);
                host.StartCoroutine(Tween.Scale(_comboChip, Vector3.one * 0.7f, Vector3.one, 0.2f, Ease.OutBack));
            }
            else
            {
                host.StartCoroutine(Tween.Punch(_comboChip, 0.18f, 0.26f));
            }
        }

        public void HideCombo()
        {
            if (_comboChip != null) _comboChip.gameObject.SetActive(false);
        }
    }
}
