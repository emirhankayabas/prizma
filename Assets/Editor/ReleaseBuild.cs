using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// A release APK — never a development build — that refuses to ship a game that cannot start.
    ///
    /// The whole game hangs off one component in the scene (<c>GameRoot</c> → AppController). An APK
    /// once went out where the build could not resolve that script: it built "successfully", with
    /// only a warning in the log, and on the phone the Unity splash gave way to the empty default
    /// sky. So this checks the script before building, watches the build for a missing-script
    /// warning, and fails loudly on either. The outcome is written next to the APK.
    ///
    /// Three ways in: the menu, the command line on a copy of the project
    ///   Unity -batchmode -quit -projectPath &lt;copy&gt; -buildTarget Android
    ///         -executeMethod BlockPuzzle.EditorTools.ReleaseBuild.BuildAndroid -releaseBuildPath &lt;apk&gt;
    /// or, with the Editor already open, an empty file at Temp/prizma-build-request, which the next
    /// script reload picks up (<see cref="BuildRequest"/>).
    /// </summary>
    public static class ReleaseBuild
    {
        const string DefaultPath = "Builds/PRIZMA.apk";
        const string Scene = "Assets/Scenes/SampleScene.unity";
        const string RootScript = "Assets/Scripts/Game/AppController.cs";

        [MenuItem("PRIZMA/Android APK Al (Development kapalı)")]
        public static void BuildAndroidFromMenu() => Build(DefaultPath, exit: false);

        public static void BuildAndroid() => Build(Arg("-releaseBuildPath") ?? DefaultPath, exit: true);

        static void Build(string path, bool exit)
        {
            var problems = new List<string>();

            // The component the scene depends on must resolve to a class before anything is built.
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(RootScript);
            if (script == null || script.GetClass() == null)
                problems.Add($"{RootScript} bir sınıfa çözümlenmiyor — sahnedeki GameRoot boş kalır.");

            BuildResult result = BuildResult.Failed;
            ulong size = 0;

            if (problems.Count == 0)
            {
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.buildAppBundle = false;

                void Watch(string message, string stack, LogType type)
                {
                    if (message.Contains("missing or no valid script")) problems.Add(message);
                }

                Application.logMessageReceived += Watch;
                try
                {
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { Scene },
                        locationPathName = path,
                        target = BuildTarget.Android,
                        options = BuildOptions.None
                    });

                    result = report.summary.result;
                    size = report.summary.totalSize;

                    foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.content.Contains("missing or no valid script") && !problems.Contains(message.content))
                            problems.Add(message.content);
                }
                finally
                {
                    Application.logMessageReceived -= Watch;
                }
            }

            bool ok = result == BuildResult.Succeeded && problems.Count == 0;

            // A build that would not start the game is not a build: take the APK away so the
            // broken one cannot be installed by mistake.
            if (!ok && result == BuildResult.Succeeded && File.Exists(path))
                File.Delete(path);

            var lines = new List<string>
            {
                $"PRIZMA release build — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Sonuç: {(ok ? "BAŞARILI" : "BAŞARISIZ")} (Unity: {result})",
                $"APK: {Path.GetFullPath(path)}",
                $"Boyut (paketlenmemiş): {size / (1024 * 1024)} MB",
                "Development Build: kapalı"
            };
            foreach (var problem in problems) lines.Add("Sorun: " + problem);

            string reportPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "PRIZMA-build.txt");
            File.WriteAllLines(reportPath, lines);

            foreach (var line in lines)
                if (ok) Debug.Log("[ReleaseBuild] " + line);
                else Debug.LogError("[ReleaseBuild] " + line);

            if (exit) EditorApplication.Exit(ok ? 0 : 1);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>
        /// Starts a release build in an Editor that is already open, when asked to by an empty file
        /// at Temp/prizma-build-request. The file is removed first, so a build runs once per request.
        /// </summary>
        [InitializeOnLoad]
        static class BuildRequest
        {
            const string Trigger = "Temp/prizma-build-request";

            static BuildRequest() => EditorApplication.delayCall += Check;

            static void Check()
            {
                if (!File.Exists(Trigger) || Application.isBatchMode) return;

                File.Delete(Trigger);
                Debug.Log("[ReleaseBuild] İstek dosyası bulundu, Android build başlıyor.");
                BuildAndroidFromMenu();
            }
        }
    }
}
