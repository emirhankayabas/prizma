using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Builds a Windows player with the PRIZMA_AUTOTEST define, which walks through every screen
    /// on its own and saves a screenshot of each. Lets the UI be checked without a person at the
    /// keyboard. Run from the command line:
    ///
    ///   Unity -batchmode -quit -projectPath &lt;copy&gt; -buildTarget Win64
    ///         -executeMethod BlockPuzzle.EditorTools.AutoTestBuild.BuildWindows
    ///         -autotestBuildPath &lt;dir&gt;/PRIZMA.exe
    ///
    /// then launch the player with -autotestOut &lt;dir&gt;. The define never reaches a real build.
    /// </summary>
    public static class AutoTestBuild
    {
        public static void BuildWindows()
        {
            string path = Arg("-autotestBuildPath") ?? "Builds/AutoTest/PRIZMA.exe";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "PRIZMA_AUTOTEST" }
            };

            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[AutoTestBuild] {report.summary.result}, {report.summary.totalErrors} errors -> {path}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
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
