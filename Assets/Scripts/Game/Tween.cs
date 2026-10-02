using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>Easing curves. Kept tiny and dependency-free rather than pulling in a tween library.</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;

        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float OutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float InQuad(float t) => t * t;

        public static float InCubic(float t) => t * t * t;

        /// <summary>A fast start that settles long and soft — how a sheet arrives.</summary>
        public static float OutQuint(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv * inv * inv;
        }

        /// <summary>Overshoots past the target and settles back. This is what makes a placement feel solid.</summary>
        /// <summary>A gentler overshoot than <see cref="OutBack"/>: a spring that settles, not a bounce.</summary>
        public static float OutBackSoft(float t)
        {
            const float c1 = 0.9f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t);
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }
    }

    /// <summary>Coroutine tweens driven by unscaled time, so a paused game still animates its UI.</summary>
    public static class Tween
    {
        public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float duration, Func<float, float> ease = null, float delay = 0f)
        {
            if (target == null) yield break;
            ease ??= Ease.OutQuad;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (target == null) yield break;

            target.localScale = from;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.localScale = Vector3.LerpUnclamped(from, to, ease(t / duration));
                yield return null;
            }

            if (target != null) target.localScale = to;
        }

        public static IEnumerator MoveAnchored(RectTransform target, Vector2 from, Vector2 to, float duration, Func<float, float> ease = null)
        {
            if (target == null) yield break;
            ease ??= Ease.OutCubic;

            target.anchoredPosition = from;

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, ease(t / duration));
                yield return null;
            }

            if (target != null) target.anchoredPosition = to;
        }

        /// <summary>A quick swell and return. Used on the score whenever it changes.</summary>
        public static IEnumerator Punch(Transform target, float amount = 0.18f, float duration = 0.22f)
        {
            if (target == null) yield break;

            var baseScale = Vector3.one;
            float half = duration * 0.4f;

            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.localScale = baseScale * (1f + amount * Ease.OutQuad(t / half));
                yield return null;
            }

            float rest = duration - half;
            for (float t = 0f; t < rest; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.localScale = baseScale * (1f + amount * (1f - Ease.OutBack(t / rest)));
                yield return null;
            }

            if (target != null) target.localScale = baseScale;
        }

        public static IEnumerator FadeGraphic(Graphic graphic, float from, float to, float duration, float delay = 0f)
        {
            if (graphic == null) yield break;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (graphic == null) yield break;

            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, from);

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                if (graphic == null) yield break;
                graphic.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }

            if (graphic != null) graphic.color = new Color(c.r, c.g, c.b, to);
        }

        /// <summary>Counts a label from one number to another so the score never just snaps.</summary>
        public static IEnumerator CountUp(int from, int to, float duration, Action<int> apply)
        {
            if (from == to)
            {
                apply(to);
                yield break;
            }

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                apply(Mathf.RoundToInt(Mathf.Lerp(from, to, Ease.OutCubic(t / duration))));
                yield return null;
            }

            apply(to);
        }
    }
}
