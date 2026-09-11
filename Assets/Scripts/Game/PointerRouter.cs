using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockPuzzle.Game
{
    /// <summary>A widget that can claim the pointer.</summary>
    public interface IPointerWidget
    {
        RectTransform Rect { get; }
        bool Interactable { get; }
        void OnPress(Vector2 screenPoint);
        void OnDrag(Vector2 screenPoint);
        void OnRelease(Vector2 screenPoint, bool inside);
    }

    /// <summary>Whatever handles pointer input when no widget claimed it — in practice, the board.</summary>
    public interface IPointerFallback
    {
        void OnPointerDown(Vector2 screenPoint);
        void OnPointerDrag(Vector2 screenPoint);
        void OnPointerUp(Vector2 screenPoint);
    }

    /// <summary>
    /// The single place pointer input is read. Widgets get first refusal on a press; anything they
    /// do not claim falls through to the board. Keeping mouse and touch on one polled path avoids
    /// an EventSystem entirely, which is what lets the whole UI be built from code.
    /// </summary>
    public sealed class PointerRouter : MonoBehaviour
    {
        static readonly List<IPointerWidget> Widgets = new List<IPointerWidget>();

        /// <summary>
        /// Surfaces that own the pointer outright — an open modal, the result card. While one is up,
        /// only widgets inside it can be pressed. Without this a tap beside a modal's buttons fell
        /// through to whatever sat behind the scrim: "play" under the settings card started a run.
        /// </summary>
        static readonly List<RectTransform> Blockers = new List<RectTransform>();

        public static IPointerFallback Fallback;

        IPointerWidget _captured;
        bool _fallbackActive;

        public static void Register(IPointerWidget widget)
        {
            if (!Widgets.Contains(widget)) Widgets.Add(widget);
        }

        public static void Unregister(IPointerWidget widget) => Widgets.Remove(widget);

        public static void PushBlocker(RectTransform root)
        {
            if (root == null) return;
            Blockers.Remove(root);
            Blockers.Add(root);
        }

        public static void PopBlocker(RectTransform root) => Blockers.Remove(root);

        /// <summary>The topmost blocker still on screen. Hidden ones are skipped rather than trusted to pop.</summary>
        static RectTransform TopBlocker
        {
            get
            {
                for (int i = Blockers.Count - 1; i >= 0; i--)
                {
                    var blocker = Blockers[i];
                    if (blocker != null && blocker.gameObject.activeInHierarchy) return blocker;
                }

                return null;
            }
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 position = pointer.position.ReadValue();
            bool held = pointer.press.isPressed;

            if (pointer.press.wasPressedThisFrame)
                Begin(position);

            if (_captured != null)
            {
                if (held)
                {
                    _captured.OnDrag(position);
                }
                else
                {
                    _captured.OnRelease(position, Contains(_captured, position));
                    _captured = null;
                }
            }
            else if (_fallbackActive)
            {
                if (held)
                {
                    Fallback?.OnPointerDrag(position);
                }
                else
                {
                    Fallback?.OnPointerUp(position);
                    _fallbackActive = false;
                }
            }
        }

        void Begin(Vector2 position)
        {
            var blocker = TopBlocker;

            // Later registrations sit on top, so walk backwards and take the first hit.
            for (int i = Widgets.Count - 1; i >= 0; i--)
            {
                var widget = Widgets[i];
                if (widget == null || !widget.Interactable) continue;
                if (widget.Rect == null || !widget.Rect.gameObject.activeInHierarchy) continue;
                if (blocker != null && !widget.Rect.IsChildOf(blocker)) continue;
                if (!Contains(widget, position)) continue;

                _captured = widget;
                widget.OnPress(position);
                return;
            }

            if (blocker == null && Fallback != null)
            {
                _fallbackActive = true;
                Fallback.OnPointerDown(position);
            }
        }

        static bool Contains(IPointerWidget widget, Vector2 position)
            => widget.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(widget.Rect, position, null);
    }
}
