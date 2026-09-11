using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Short haptic pulses. Unity's Handheld.Vibrate is a fixed buzz of close to half a second on
    /// Android — far too heavy to fire on every clear — so this goes to the platform vibrator
    /// directly with a duration and strength of our choosing. Silent in the Editor.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static bool _looked;

        static AndroidJavaObject Vibrator
        {
            get
            {
                if (_looked) return _vibrator;
                _looked = true;

                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                catch (System.Exception)
                {
                    _vibrator = null;
                }

                return _vibrator;
            }
        }
#endif

        /// <summary>
        /// One pulse. <paramref name="amplitude"/> is 1..255, or -1 for the device default.
        /// Falls back to Handheld.Vibrate — which is also what makes Unity declare the vibrate
        /// permission in the Android manifest.
        /// </summary>
        public static void Pulse(long milliseconds, int amplitude = -1)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var vibrator = Vibrator;
            if (vibrator == null)
            {
                Handheld.Vibrate();
                return;
            }

            try
            {
                using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                    vibrator.Call("vibrate", effect);
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
