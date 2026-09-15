using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Hands a line of text to the phone's own share sheet — messages, social apps, the clipboard.
    /// Uses the platform intent directly, so it needs no plugin and no network permission. Off
    /// Android (the editor, the desktop test build) the text goes to the clipboard instead.
    /// </summary>
    public static class ShareSheet
    {
        public static string StoreLink => "https://play.google.com/store/apps/details?id=" + Application.identifier;

        public static void ShareText(string text, string title)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();

                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title))
                        activity.Call("startActivity", chooser);
                }
                return;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ShareSheet] " + e.Message);
            }
#endif
            GUIUtility.systemCopyBuffer = text;
        }
    }
}
