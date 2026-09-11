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

        public void Show()
        {
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            OnShow();

            StopAllCoroutines();
            StartCoroutine(EnterRoutine());
        }

        public void Hide()
        {
            StopAllCoroutines();
            Root.gameObject.SetActive(false);
            OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>A short fade and rise. Enough to make navigation feel deliberate, not instant.</summary>
        IEnumerator EnterRoutine()
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
