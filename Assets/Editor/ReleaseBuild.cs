using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// A release APK from the command line — never a development build. Used when the Editor has
    /// the project open, by building from a copy:
    ///
    ///   Unity -batchmode -quit -projectPath &lt;copy&gt; -buildTarget Android
    ///         -executeMethod BlockPuzzle.EditorTools.ReleaseBuild.BuildAndroid
    ///         -releaseBuildPath &lt;dir&gt;/PRIZMA.apk
    /// </summary>
    public static class ReleaseBuild
    {
        [MenuItem("PRIZMA/Android APK Al (Development kapalı)")]
        public static void BuildAndroidFromMenu() => Build("Builds/PRIZMA.apk", exit: false);

        public static void BuildAndroid() => Build(Arg("-releaseBuildPath") ?? "Builds/PRIZMA.apk", exit: true);

        static void Build(string path, bool exit)
        {
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[ReleaseBuild] {report.summary.result}, {report.summary.totalErrors} errors, " +
                      $"{report.summary.totalSize / (1024 * 1024)} MB -> {path}");

            if (exit) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
