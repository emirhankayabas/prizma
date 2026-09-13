using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Play always starts the real game, whatever scene the Editor happens to have open.
    ///
    /// The whole game hangs off one object in one scene (SampleScene → GameRoot → AppController).
    /// After a crash recovery the Editor came back with an untitled empty scene open; Play then
    /// showed nothing but the default sky, and it read like the game was broken when nothing was.
    /// Unity's play-mode start scene removes the failure mode entirely: whatever is open, Play
    /// loads SampleScene — the same scene the APK ships (see <see cref="ReleaseBuild"/>).
    ///
    /// Turn it off from the menu if a scene ever needs testing on its own.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromGameScene
    {
        const string Scene = "Assets/Scenes/SampleScene.unity";
        const string MenuPath = "PRIZMA/Play Her Zaman Oyun Sahnesini Açsın";
        const string Pref = "PRIZMA.PlayFromGameScene";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(Pref, true);
            set => EditorPrefs.SetBool(Pref, value);
        }

        static PlayFromGameScene() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            if (Application.isBatchMode) return;

            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            if (scene == null)
            {
                Debug.LogWarning($"[PRIZMA] {Scene} bulunamadı — Play açık olan sahneyi çalıştıracak.");
                return;
            }

            EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
