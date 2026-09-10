using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The three-slot rack under the board. Slots keep their position when a piece leaves, so a
    /// cancelled drag can always fly back to where it came from.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        /// <summary>Pieces sit smaller in the tray than on the board, then grow when picked up.</summary>
        public const float TrayScale = 0.68f;

        RectTransform _rect;
        RectTransform[] _slots;
        PieceView[] _pieces;

        float _boardCellSize;
        float _boardGap;

        public RectTransform Rect => _rect;
        public int SlotCount => _slots.Length;

        public void Build(float width, float height, float boardCellSize, float boardGap, int slotCount)
        {
            _rect = GetComponent<RectTransform>();
            _boardCellSize = boardCellSize;
            _boardGap = boardGap;

            _rect.sizeDelta = new Vector2(width, height);

            _slots = new RectTransform[slotCount];
            _pieces = new PieceView[slotCount];

            float slotWidth = width / slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                var slot = UiBuilder.Node(_rect, $"Slot_{i}");
                slot.sizeDelta = new Vector2(slotWidth, height);
                slot.anchoredPosition = new Vector2(-width * 0.5f + slotWidth * (i + 0.5f), 0f);
                _slots[i] = slot;
            }
        }

        public RectTransform Slot(int index) => _slots[index];

        public PieceView Piece(int index) => _pieces[index];

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

                _pieces[i] = CreatePiece(trayPiece, _slots[i], i);
            }
        }

        PieceView CreatePiece(TrayPiece trayPiece, RectTransform slot, int slotIndex)
        {
            var go = new GameObject("Piece", typeof(RectTransform));
            go.transform.SetParent(slot, false);

            var color = Design.Blocks[trayPiece.ColorIndex % Design.Blocks.Length];

            var view = go.AddComponent<PieceView>();
            view.Build(trayPiece.Shape, trayPiece.ColorIndex, color, _boardCellSize, _boardGap);
            view.Rect.anchoredPosition = Vector2.zero;

            // Staggered so a refill reads as three pieces arriving, not one popping in triplicate.
            StartCoroutine(Tween.Scale(view.Rect, Vector3.zero, Vector3.one * TrayScale, 0.28f, Ease.OutBack, slotIndex * 0.06f));
            return view;
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
            piece.Rect.localScale = Vector3.one * TrayScale;
            piece.SetAlpha(1f);
            piece.Rect.position = droppedAt;

            StartCoroutine(Tween.MoveAnchored(piece.Rect, piece.Rect.anchoredPosition, Vector2.zero, 0.16f, Ease.OutCubic));
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
