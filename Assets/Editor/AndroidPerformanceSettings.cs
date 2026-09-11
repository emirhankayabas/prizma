using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Android player settings the game needs for a steady 60 fps, applied from code so they
    /// cannot quietly drift back in the Player Settings window. Runs once when the Editor loads
    /// and again before every Android build.
    /// </summary>
    [InitializeOnLoad]
    sealed class AndroidPerformanceSettings : IPreprocessBuildWithReport
    {
        static AndroidPerformanceSettings() => EditorApplication.delayCall += Apply;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            Apply();

            if (EditorUserBuildSettings.development)
                Debug.LogWarning("PRIZMA: Development Build açık. Profil için iyi, ama FPS ölçümü " +
                                 "yanıltır — cihazda akıcılığı test etmek için kapatıp derle.");
        }

        static void Apply()
        {
            // Swappy: hands frames to the display on its vsync instead of whenever they finish,
            // which is what turns an average of 60 into sixty evenly spaced frames.
            if (!PlayerSettings.Android.optimizedFramePacing)
                PlayerSettings.Android.optimizedFramePacing = true;

            // "Always" renders every frame offscreen and then copies it to the screen — a full
            // extra pass per frame. Auto only does that when something actually requires it.
            if (PlayerSettings.Android.blitType != AndroidBlitType.Auto)
                PlayerSettings.Android.blitType = AndroidBlitType.Auto;
        }
    }
}
