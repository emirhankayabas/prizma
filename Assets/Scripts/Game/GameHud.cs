using System;
using BlockPuzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The strip above the board. One layout, filled differently per mode:
    /// classic shows the score against the all-time best; the daily shows it against today's best
    /// and the date; a level shows what is left of its goal, and the moves left to do it in.
    /// </summary>
    public sealed class GameHud
    {
        const float GoalIconSize = 92f;
        const float GoalIconGap = 18f;

        static readonly string[] Months =
        {
            "OCAK", "ŞUBAT", "MART", "NİSAN", "MAYIS", "HAZİRAN",
            "TEMMUZ", "AĞUSTOS", "EYLÜL", "EKİM", "KASIM", "ARALIK"
        };

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

        public RectTransform Root => _root;
        public Transform Readout => _readout.transform;

        /// <summary>Where freed crystals fly to.</summary>
        public Vector3 GoalAnchor => _goalIcon.gameObject.activeSelf ? _goalIcon.rectTransform.position : _readout.rectTransform.position;

        public Transform GoalIcon => _goalIcon.transform;

        public void Build(RectTransform parent, float height, Action onPause)
        {
            _root = UiBuilder.Node(parent, "Hud");
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(1080f, height);

            _leftIcon = UiBuilder.Image(_root, "LeftIcon", Icons.Gem, Design.Gold);
            _leftIcon.type = Image.Type.Simple;
            _leftIcon.rectTransform.anchorMin = _leftIcon.rectTransform.anchorMax = new Vector2(0f, 1f);
            _leftIcon.rectTransform.pivot = new Vector2(0f, 1f);
            _leftIcon.rectTransform.sizeDelta = new Vector2(44f, 44f);
            _leftIcon.rectTransform.anchoredPosition = new Vector2(Design.Gutter, -Design.Space6);

            _leftLabel = UiBuilder.Label(_root, "LeftLabel", "0", Design.Headline, Design.Gold,
                Design.FontDisplay, TextAlignmentOptions.Left);
            _leftLabel.rectTransform.anchorMin = _leftLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            _leftLabel.rectTransform.pivot = new Vector2(0f, 1f);
            _leftLabel.rectTransform.sizeDelta = new Vector2(360f, 64f);
            _leftLabel.rectTransform.anchoredPosition = new Vector2(Design.Gutter + 62f, -Design.Space6 + 8f);

            // One control in the corner. It pauses; settings and quitting live inside that menu,
            // which is where a player looks for them mid-run.
            var pause = UiBuilder.Button(_root, "Pause", new Vector2(104f, 104f), UiButton.Style.Icon,
                null, Design.Body, Icons.Pause);
            pause.Rect.anchorMin = pause.Rect.anchorMax = new Vector2(1f, 1f);
            pause.Rect.pivot = new Vector2(1f, 1f);
            pause.Rect.anchoredPosition = new Vector2(-Design.Gutter, -Design.Space5);
            pause.Clicked += () => onPause?.Invoke();

            // A small chip above the readout: moves left in a level, the date in the daily.
            _chip = UiBuilder.Node(_root, "Chip");
            _chip.anchorMin = _chip.anchorMax = new Vector2(0.5f, 1f);
            _chip.pivot = new Vector2(0.5f, 1f);
            _chip.sizeDelta = new Vector2(300f, 64f);
            _chip.anchoredPosition = new Vector2(0f, -Design.Space5 - 12f);
            UiBuilder.Panel(_chip, "Fill", _chip.sizeDelta, Design.SurfaceInset, 32f);
            UiBuilder.Hairline(_chip, "Edge", _chip.sizeDelta, 32f);
            _chipLabel = UiBuilder.Label(_chip, "Label", "", Design.Label, Design.TextPrimary,
                Design.FontDisplay, tracking: 4f);
            _chipLabel.rectTransform.sizeDelta = _chip.sizeDelta;

            _readout = UiBuilder.Label(_root, "Readout", "0", Design.Readout, Design.TextPrimary,
                Design.FontDisplay, tracking: Design.TrackingDisplay);
            _readout.rectTransform.anchorMin = _readout.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _readout.rectTransform.pivot = new Vector2(0.5f, 1f);
            // A tight box: the default line box would reserve enough room to collide with the chip.
            _readout.rectTransform.sizeDelta = new Vector2(960f, 170f);
            _readout.rectTransform.anchoredPosition = new Vector2(0f, -170f);
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
            _comboChip.sizeDelta = new Vector2(330f, 72f);
            _comboChip.anchoredPosition = new Vector2(0f, -352f);

            UiBuilder.Panel(_comboChip, "Fill", _comboChip.sizeDelta, Design.SurfaceInset, 36f);
            _comboChipEdge = UiBuilder.Hairline(_comboChip, "Edge", _comboChip.sizeDelta, 36f, Design.Mint.WithAlpha(0.5f));

            _comboLabel = UiBuilder.Label(_comboChip, "Label", "", Design.Body, Design.Mint,
                Design.FontDisplay, tracking: 4f);
            _comboLabel.rectTransform.sizeDelta = _comboChip.sizeDelta;

            _comboChip.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ binding

        public void Bind(GameSession session)
        {
            _session = session;
            HideCombo();

            switch (session.Mode)
            {
                case GameMode.Daily:
                    SetLeft(Icons.Calendar, Design.Mint, Design.FontDisplay, Design.Headline);
                    var today = DateTime.Now;
                    ShowChip($"{today.Day} {Months[today.Month - 1]}", Design.TextPrimary);
                    _goalIcon.gameObject.SetActive(false);
                    break;

                case GameMode.Level:
                    SetLeft(Icons.Flag, Design.TextPrimary, Design.FontDisplay, Design.Label);
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
                    _leftLabel.text = Mathf.Max(Progress.DailyBestToday, _session.Score).ToString();
                    SetReadout(displayedScore.ToString());
                    break;

                case GameMode.Level:
                {
                    var level = _session.Level;
                    _leftLabel.text = $"BÖLÜM {level.Number}";

                    int moves = _session.MovesLeft;
                    // Low on moves: the chip turns rose. A steady colour, not a pulse.
                    ShowChip($"{moves} HAMLE", moves <= 5 ? Design.PreviewTint(3) : Design.TextPrimary);

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

        void SetReadout(string text)
        {
            _readout.text = text;

            if (!_goalIcon.gameObject.activeSelf)
            {
                _readout.rectTransform.anchoredPosition = new Vector2(0f, -170f);
                return;
            }

            // Icon and number centred as one group.
            float width = _readout.GetPreferredValues(text).x;
            float group = GoalIconSize + GoalIconGap + width;
            float left = -group * 0.5f;

            _goalIcon.rectTransform.anchoredPosition = new Vector2(left + GoalIconSize * 0.5f, -170f - 84f);
            _readout.rectTransform.anchoredPosition = new Vector2(left + GoalIconSize + GoalIconGap + width * 0.5f, -170f);
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
            var tint = streak >= 4 ? Design.Gold : Design.Mint;

            _comboLabel.text = $"COMBO ×{multiplier:0.#}";
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
