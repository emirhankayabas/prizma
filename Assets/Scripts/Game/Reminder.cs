using System;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// The daily puzzle's reminder: one notification, the next day, at the time the player last
    /// solved it. Someone who solved at noon hears from the game at noon — the habit they already
    /// have, rather than a time the game picked.
    ///
    /// Scheduling lives in Java (Assets/Plugins/Android/PrizmaReminder*.java, wired into the
    /// manifest by Editor/ReminderManifest): an alarm wakes a receiver that posts the notification
    /// and arms the next day, and gives up after three unanswered ones. This side works out when,
    /// writes the words in the player's language, and asks Android 13+ for the permission — once,
    /// on the first launch, and again only from the settings switch.
    /// Everywhere but on an Android device it does nothing.
    /// </summary>
    public static class Reminder
    {
        const string Permission = "android.permission.POST_NOTIFICATIONS";
        const string JavaClass = "com.emirhankayabas.prizma.reminder.PrizmaReminder";

        /// <summary>
        /// When the next reminder is due: the time of day of the last solve, today if today is
        /// still open and that time is still ahead, otherwise tomorrow. Null before any solve.
        /// </summary>
        public static DateTime? NextTime(DateTime now)
        {
            int minute = Progress.DailySolveMinute;
            if (Progress.DailySolves <= 0 || minute < 0) return null;

            var at = now.Date.AddMinutes(minute);
            if (Progress.SolvedDailyToday || at <= now) at = at.AddDays(1);
            return at;
        }

        /// <summary>
        /// Brings the pending reminder in line with the setting and the latest solve. Called after
        /// a solve, when the app comes back to the front, and when the switch is flipped.
        /// </summary>
        public static void Refresh()
        {
            if (!GameSettings.Reminder)
            {
                Cancel();
                return;
            }

            var next = NextTime(DateTime.Now);
            if (next == null) return;

            // The day it will be when the reminder lands: tomorrow's puzzle carries the streak one on.
            int streak = Progress.DailyStreak;
            int day = streak + 1;
            Schedule(next.Value, Str.ReminderTitle, Str.ReminderBody(day, streak > 0), Str.ReminderFollowUp, Str.ReminderChannel);
        }

        /// <summary>
        /// Switches the reminder on: asks for the permission where the platform wants one, and
        /// reports whether the player ended up with notifications allowed.
        /// </summary>
        public static void Enable(Action<bool> done)
        {
            GameSettings.ReminderAsked = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (SdkInt() >= 33 && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(Permission))
            {
                // The answer comes back through a Java proxy, which need not be Unity's main
                // thread; PlayerPrefs and the settings tile may only be touched from that one.
                var main = System.Threading.SynchronizationContext.Current;
                void Answer(bool allowed)
                {
                    if (main != null) main.Post(_ => Finish(allowed, done), null);
                    else Finish(allowed, done);
                }

                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => Answer(true);
                callbacks.PermissionDenied += _ => Answer(false);
                UnityEngine.Android.Permission.RequestUserPermission(Permission, callbacks);
                return;
            }

            Finish(SystemAllows(), done);
#else
            Finish(true, done);
#endif
        }

        static void Finish(bool allowed, Action<bool> done)
        {
            GameSettings.Reminder = allowed;
            Refresh();
            done?.Invoke(allowed);
        }

        public static void Disable()
        {
            GameSettings.Reminder = false;
            Cancel();
        }

        static void Schedule(DateTime localTime, string title, string body, string followUp, string channel)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                long millis = new DateTimeOffset(localTime).ToUnixTimeMilliseconds();
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var reminder = new AndroidJavaClass(JavaClass))
                    reminder.CallStatic("schedule", activity, millis, title, body, followUp, channel);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Reminder] " + e.Message);
            }
#else
            Debug.Log($"[Reminder] {localTime:yyyy-MM-dd HH:mm} — {title}: {body}");
#endif
        }

        static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var reminder = new AndroidJavaClass(JavaClass))
                    reminder.CallStatic("cancel", activity);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Reminder] " + e.Message);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static int SdkInt()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                return version.GetStatic<int>("SDK_INT");
        }

        /// <summary>Below Android 13 there is no prompt; the app's notifications can still be off in the system settings.</summary>
        static bool SystemAllows()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var reminder = new AndroidJavaClass(JavaClass))
                    return reminder.CallStatic<bool>("enabled", activity);
            }
            catch (Exception)
            {
                return true;
            }
        }
#endif
    }
}
