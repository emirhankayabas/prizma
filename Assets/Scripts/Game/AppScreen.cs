using System;
using System.Collections;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Base for every full-screen page and modal. Each screen builds its own subtree once and is
    /// then just switched on and off, so navigation never rebuilds UI.
    /// </summary>
    public abstract class AppScreen : MonoBehaviour
    {
        protected AppController App;
        protected RectTransform Root;
        protected CanvasGroup Group;

        protected AudioKit Audio => App.Audio;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        /// <summary>The screen's root, for things outside it that need to know its bounds.</summary>
        public RectTransform RootRect => Root;

        /// <summary>True for screens that float over another page and swallow board input.</summary>
        public virtual bool IsModal => false;

        public void Init(AppController app, RectTransform parent)
        {
            App = app;
            Root = (RectTransform)transform;
            Root.SetParent(parent, false);
            UiBuilder.Stretch(Root);

            Group = gameObject.AddComponent<CanvasGroup>();

            Build();
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build();

        /// <param name="animate">
        /// False when the screen is replacing an identical one — a rebuild for a new theme — where a
        /// fade from nothing would flash the ground through the page.
        /// </param>
        /// <param name="overModal">
        /// True when another modal was on screen a moment ago (settings opened from the pause
        /// sheet, or the pause sheet coming back): the page is already dimmed, so the dim must not
        /// fade out and in again between the two.
        /// </param>
        public void Show(bool animate = true, bool overModal = false)
        {
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);

            // Before OnShow, not after: anything a screen starts while showing — the title's drift,
            // the daily streak counting up — used to be stopped the moment it began.
            StopAllCoroutines();
            OnShow();

            if (animate) StartCoroutine(Enter(overModal));
            else Settle();
        }

        /// <summary>
        /// Plays the screen's way out, then calls <paramref name="done"/>; the caller hides it.
        /// Pages leave at once. <paramref name="revealsModal"/> is true when a modal beneath will
        /// show again, so the dim stays.
        /// </summary>
        public virtual void Dismiss(bool revealsModal, Action done) => done();

        public void Hide()
        {
            StopAllCoroutines();
            Root.gameObject.SetActive(false);
            OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>The finished look of a screen that appears without its entrance.</summary>
        protected virtual void Settle() => Group.alpha = 1f;

        /// <summary>A short fade. Enough to make navigation feel deliberate, not instant.</summary>
        protected virtual IEnumerator Enter(bool overModal)
        {
            Group.alpha = 0f;
            const float duration = 0.22f;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Group.alpha = Ease.OutQuad(t / duration);
                yield return null;
            }

            Group.alpha = 1f;
        }
    }
}
