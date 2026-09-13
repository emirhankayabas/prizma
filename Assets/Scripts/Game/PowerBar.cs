using System;
using BlockPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The prism and its three powers, between the board and the tray.
    ///
    /// Left: three crystals, lit for each charge held, over a thin meter that fills with cleared
    /// lines. Right: rotate, reroll and bomb, each marked with its cost in small dots. When no
    /// piece fits, the prism gives way to a quiet "end" button — the run is not over while a
    /// charge can still save it, and the player decides.
    /// </summary>
    public sealed class PowerBar
    {
        const float MeterWidth = 300f;
        const float TrackWidth = 250f;
        const float TrackHeight = 16f;
        const float CrystalSize = 72f;
        const float ButtonGap = 24f;

        float _buttonWidth;

        RectTransform _root;
        RectTransform _meter;
        readonly Image[] _crystals = new Image[PowerRules.MaxCharges];
        Image _meterFill;

        UiButton _rotate;
        UiButton _reroll;
        UiButton _bomb;
        UiButton _end;

        public event Action<PowerKind> PowerClicked;
        public event Action EndClicked;

        public RectTransform Root => _root;

        public void Build(RectTransform parent, float width, float height, float bottom)
        {
            _root = UiBuilder.Node(parent, "PowerBar");
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.sizeDelta = new Vector2(width, height);
            _root.anchoredPosition = new Vector2(0f, bottom);

            float left = -width * 0.5f;

            _meter = UiBuilder.Node(_root, "Prism");
            _meter.sizeDelta = new Vector2(MeterWidth, height);
            _meter.anchoredPosition = new Vector2(left + MeterWidth * 0.5f, 0f);

            for (int i = 0; i < _crystals.Length; i++)
            {
                var crystal = UiBuilder.Image(_meter, $"Charge{i}", Art.Crystal, Design.Prism);
                crystal.type = Image.Type.Simple;
                crystal.rectTransform.sizeDelta = new Vector2(CrystalSize, CrystalSize);
                crystal.rectTransform.anchoredPosition = new Vector2((i - 1) * 86f, 20f);
                _crystals[i] = crystal;
            }

            var track = UiBuilder.Panel(_meter, "Track", new Vector2(TrackWidth, TrackHeight), Design.SurfaceTrack, TrackHeight * 0.5f);
            track.rectTransform.anchoredPosition = new Vector2(0f, -44f);

            _meterFill = UiBuilder.Image(track.rectTransform, "Fill", Art.Panel(TrackHeight * 0.5f), Design.Prism);
            _meterFill.rectTransform.anchorMin = _meterFill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _meterFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _meterFill.rectTransform.sizeDelta = new Vector2(0f, TrackHeight);

            _end = UiBuilder.Button(_root, "End", new Vector2(MeterWidth - 16f, height - 12f), UiButton.Style.Secondary,
                "BİTİR", Design.Body);
            _end.Rect.anchoredPosition = _meter.anchoredPosition;
            _end.Clicked += () => EndClicked?.Invoke();
            _end.gameObject.SetActive(false);

            // The three powers share whatever the prism leaves of the row.
            _buttonWidth = (width - MeterWidth - ButtonGap * 3f) / 3f;
            float x = left + MeterWidth + ButtonGap + _buttonWidth * 0.5f;
            _rotate = BuildPower(PowerKind.Rotate, Icons.Rotate, x, height);
            _reroll = BuildPower(PowerKind.Reroll, Icons.Dice, x + _buttonWidth + ButtonGap, height);
            _bomb = BuildPower(PowerKind.Bomb, Icons.Bomb, x + (_buttonWidth + ButtonGap) * 2f, height);
        }

        UiButton BuildPower(PowerKind kind, Sprite icon, float x, float height)
        {
            var button = UiBuilder.Button(_root, kind.ToString(), new Vector2(_buttonWidth, height - 6f),
                UiButton.Style.Icon, null, Design.Body, icon);
            button.Rect.anchoredPosition = new Vector2(x, 0f);
            button.Icon.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            button.Clicked += () => PowerClicked?.Invoke(kind);

            // The price in dots under the glyph, so cost is read the same way charges are.
            int cost = PowerRules.Cost(kind);
            for (int i = 0; i < cost; i++)
            {
                var pip = UiBuilder.Image(button.Rect, "Cost" + i, Art.Disc, Design.Prism);
                pip.type = Image.Type.Simple;
                pip.rectTransform.sizeDelta = new Vector2(16f, 16f);
                pip.rectTransform.anchoredPosition = new Vector2((i - (cost - 1) * 0.5f) * 26f, -46f);
            }

            return button;
        }

        /// <summary>Repaints from the session. <paramref name="aiming"/> is the power waiting for a target, if any.</summary>
        public void Refresh(GameSession session, PowerKind? aiming)
        {
            bool enabled = session != null && session.Config.PowersEnabled;
            _root.gameObject.SetActive(enabled);
            if (!enabled) return;

            for (int i = 0; i < _crystals.Length; i++)
                _crystals[i].color = i < session.Charges ? Design.Prism : Color.white.WithAlpha(0.14f);

            float fill = session.Charges >= PowerRules.MaxCharges
                ? 1f
                : Mathf.Clamp01(session.ChargeProgress / (float)Mathf.Max(1, session.ChargeTarget));
            _meterFill.rectTransform.sizeDelta = new Vector2(TrackWidth * fill, TrackHeight);

            bool stuck = session.State == SessionState.Stuck;
            _meter.gameObject.SetActive(!stuck);
            _end.gameObject.SetActive(stuck);

            _rotate.SetEnabled(CanRotateAny(session));
            _reroll.SetEnabled(session.CanReroll());
            _bomb.SetEnabled(session.CanUsePower(PowerKind.Bomb));

            _rotate.SetHighlighted(aiming == PowerKind.Rotate, Design.AccentA);
            _bomb.SetHighlighted(aiming == PowerKind.Bomb, Design.AccentA);
        }

        static bool CanRotateAny(GameSession session)
        {
            for (int i = 0; i < session.Tray.Length; i++)
                if (session.CanRotate(i)) return true;
            return false;
        }

        /// <summary>A charge was just earned: the crystal that lit up swells once.</summary>
        public void CelebrateCharge(MonoBehaviour host, int charges)
        {
            int index = Mathf.Clamp(charges - 1, 0, _crystals.Length - 1);
            host.StartCoroutine(Tween.Punch(_crystals[index].transform, 0.45f, 0.4f));
        }

        public Vector3 CrystalPosition(int index) => _crystals[Mathf.Clamp(index, 0, _crystals.Length - 1)].rectTransform.position;

        public Vector3 ButtonPosition(PowerKind kind)
        {
            switch (kind)
            {
                case PowerKind.Rotate: return _rotate.Rect.position;
                case PowerKind.Bomb: return _bomb.Rect.position;
                default: return _reroll.Rect.position;
            }
        }
    }
}
