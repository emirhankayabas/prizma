using System.Collections;
using BlockPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The three-slot rack under the board. Slots keep their position when a piece leaves, so a
    /// cancelled drag can always fly back to where it came from.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        /// <summary>
        /// Largest scale a piece sits at in the tray; long pieces shrink further to fit their
        /// slot. Pieces grow back to board size when picked up.
        ///
        /// It was 0.66, and next to published block puzzles the rack looked sparse — small
        /// pieces floating in a wide band. 0.74 is the most a 3x3 piece can take and still
        /// leave air between neighbouring slots.
        /// </summary>
        public const float MaxScale = 0.74f;

        const float SlotInset = 18f;

        RectTransform _rect;
        RectTransform[] _slots;
        PieceView[] _pieces;
        float[] _scales;
        Image[] _hints;
        CanvasGroup _group;

        float _boardCellSize;
        float _boardGap;
        float _slotWidth;
        float _height;
        bool _colorBlind;

        public RectTransform Rect => _rect;
        public int SlotCount => _slots.Length;

        public void Build(float width, float height, float boardCellSize, float boardGap, int slotCount)
        {
            _rect = GetComponent<RectTransform>();
            _boardCellSize = boardCellSize;
            _boardGap = boardGap;
            _height = height;

            _rect.sizeDelta = new Vector2(width, height);
            _group = gameObject.AddComponent<CanvasGroup>();

            _slots = new RectTransform[slotCount];
            _pieces = new PieceView[slotCount];
            _scales = new float[slotCount];
            _hints = new Image[slotCount];

            _slotWidth = width / slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = UiBuilder.Node(_rect, $"Slot_{i}");
                slot.sizeDelta = new Vector2(_slotWidth, height);
                slot.anchoredPosition = new Vector2(-width * 0.5f + _slotWidth * (i + 0.5f), 0f);
                _slots[i] = slot;

                // Shown behind a piece the rotate power can turn. A steady wash, like the line preview.
                var hint = UiBuilder.Panel(slot, "Hint", new Vector2(_slotWidth - 20f, height - 12f),
                    Design.Prism.WithAlpha(0.14f), Design.RadiusMd);
                hint.gameObject.SetActive(false);
                _hints[i] = hint;
            }
        }

        public RectTransform Slot(int index) => _slots[index];

        public PieceView Piece(int index) => _pieces[index];

        public float ScaleOf(int index) => _scales[index] <= 0f ? MaxScale : _scales[index];

        public void SetColorBlind(bool on) => _colorBlind = on;

        /// <summary>Rebuilds every slot from the session tray. Used pieces leave an empty slot.</summary>
        public void Refresh(GameSession session)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_pieces[i] != null)
                {
                    Destroy(_pieces[i].gameObject);
                    _pieces[i] = null;
                }

                var trayPiece = session.Tray[i];
                if (trayPiece.Used) continue;

                _pieces[i] = CreatePiece(trayPiece, i);

                // Staggered so a refill reads as three pieces arriving, not one popping in triplicate.
                // Each rises into its slot as it grows: dealt from below, the way a hand of cards is.
                StartCoroutine(DealIn(_pieces[i].Rect, _scales[i], i * 0.06f));
            }
        }

        IEnumerator DealIn(RectTransform rect, float scale, float delay)
        {
            const float rise = 90f;
            const float duration = 0.3f;

            // A piece picked up mid-deal belongs to the drag from then on: stop touching it.
            var slot = rect.parent;
            rect.localScale = Vector3.zero;
            rect.anchoredPosition = new Vector2(0f, -rise);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (rect == null || rect.parent != slot) yield break;
                float k = t / duration;
                rect.localScale = Vector3.one * scale * Mathf.LerpUnclamped(0f, 1f, Ease.OutBack(k));
                rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-rise, 0f, Ease.OutCubic(k)));
                yield return null;
            }

            if (rect == null || rect.parent != slot) yield break;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = Vector2.zero;
        }

        PieceView CreatePiece(TrayPiece trayPiece, int slotIndex)
        {
            var go = new GameObject("Piece", typeof(RectTransform));
            go.transform.SetParent(_slots[slotIndex], false);

            var color = Design.Blocks[trayPiece.ColorIndex % Design.Blocks.Length];

            var view = go.AddComponent<PieceView>();
            view.Build(trayPiece.Shape, trayPiece.ColorIndex, color, _boardCellSize, _boardGap, _colorBlind);
            view.Rect.anchoredPosition = Vector2.zero;

            var size = view.Rect.sizeDelta;
            _scales[slotIndex] = Mathf.Min(MaxScale,
                (_slotWidth - SlotInset) / Mathf.Max(1f, size.x),
                (_height - SlotInset) / Mathf.Max(1f, size.y));

            view.Rect.localScale = Vector3.one * _scales[slotIndex];
            return view;
        }

        /// <summary>The rotate power: the new shape turns into place from a quarter turn back.</summary>
        public void RotatePiece(int index, GameSession session)
        {
            if (_pieces[index] != null) Destroy(_pieces[index].gameObject);

            _pieces[index] = CreatePiece(session.Tray[index], index);
            StartCoroutine(SpinIn(_pieces[index].Rect, _scales[index]));
        }

        IEnumerator SpinIn(RectTransform rect, float scale)
        {
            // Picked up mid-turn, the piece is the drag's: it was being shrunk back to tray size
            // under the finger.
            var slot = rect.parent;
            const float duration = 0.24f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (rect == null || rect.parent != slot) yield break;
                float k = Ease.OutBack(t / duration);
                rect.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(90f, 0f, k));
                rect.localScale = Vector3.one * scale * Mathf.LerpUnclamped(0.8f, 1f, k);
                yield return null;
            }

            if (rect == null) yield break;
            rect.localEulerAngles = Vector3.zero;
            if (rect.parent == slot) rect.localScale = Vector3.one * scale;
        }

        /// <summary>Detaches a slot's piece so the drag layer can own it. Returns null on an empty slot.</summary>
        public PieceView TakePiece(int index)
        {
            var piece = _pieces[index];
            _pieces[index] = null;
            return piece;
        }

        /// <summary>Puts a piece back into its slot, flying home from where it was dropped.</summary>
        public void ReturnPiece(int index, PieceView piece)
        {
            if (piece == null) return;

            _pieces[index] = piece;

            var droppedAt = piece.Rect.position;
            piece.Rect.SetParent(_slots[index], false);
            piece.Rect.localScale = Vector3.one * ScaleOf(index);
            piece.SetAlpha(1f);
            piece.Rect.position = droppedAt;

            StartCoroutine(FlyHome(piece.Rect));
        }

        /// <summary>
        /// The short flight back into the slot. A piece grabbed again on its way home belongs to
        /// the new drag: a plain tween kept pulling it towards the slot under the finger.
        /// </summary>
        IEnumerator FlyHome(RectTransform rect)
        {
            var slot = rect.parent;
            var from = rect.anchoredPosition;
            const float duration = 0.16f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (rect == null || rect.parent != slot) yield break;
                rect.anchoredPosition = Vector2.LerpUnclamped(from, Vector2.zero, Ease.OutCubic(t / duration));
                yield return null;
            }

            if (rect != null && rect.parent == slot) rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>Faded while no piece fits, so the eye goes to the powers instead.</summary>
        public void SetDimmed(bool dimmed)
        {
            if (_group != null) _group.alpha = dimmed ? 0.4f : 1f;
        }

        public void SetRotateHints(GameSession session, bool on)
        {
            for (int i = 0; i < _hints.Length; i++)
                _hints[i].gameObject.SetActive(on && session != null && session.CanRotate(i));
        }

        /// <summary>Index of the slot under a screen point, or -1. Slots are generous on purpose.</summary>
        public int SlotAtScreenPoint(Vector2 screenPoint)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_pieces[i] == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(_slots[i], screenPoint, null))
                    return i;
            }

            return -1;
        }
    }
}
