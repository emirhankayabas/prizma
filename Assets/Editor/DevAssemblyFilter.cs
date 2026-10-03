using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Keeps the Editor's development packages out of the release player. They are used while
    /// working on the game (the AI assistant, the pipeline server the editor tools talk to) and
    /// have runtime assemblies of their own, which Unity puts in every build: an APK once carried
    /// a log-capturing bootstrap that ran before the scene, an HTTP server and a C# compiler —
    /// in a game that never uses any of them. Only <see cref="ReleaseBuild"/> turns this on.
    ///
    /// What the rest of the player really uses is read from the compiled assemblies' own
    /// references, not from the compile settings: Assembly-CSharp is compiled against every package
    /// that allows it, so on paper it "references" all of them. An assembly something kept still
    /// needs is kept too, with a note in the log, rather than breaking the build.
    /// </summary>
    sealed class DevAssemblyFilter : IFilterBuildAssemblies
    {
        public static bool Active;

        public int callbackOrder => 0;

        static readonly string[] DevPackages =
        {
            "com.unity.ai.assistant",
            "com.unity.pipeline",
            "com.unity.ai.navigation",
            "com.unity.visualscripting",
        };

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            if (!Active) return assemblies;

            // Assemblies compiled from a dev package's sources, by name, and precompiled DLLs that
            // ship inside one (the pipeline's Roslyn), by path.
            var fromSource = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Where(a => InDevPackage(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(a.name)))
                .Select(a => a.name));

            var dev = new HashSet<string>(assemblies
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => fromSource.Contains(name)));
            foreach (var path in assemblies)
                if (InDevPackage(path)) dev.Add(Path.GetFileNameWithoutExtension(path));

            // Never drop what something kept still uses — and once a dev assembly is kept, what it
            // uses is needed too, so repeat until nothing changes.
            var references = LoadedReferences();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var path in assemblies)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (dev.Contains(name) || !references.TryGetValue(name, out var uses)) continue;

                    foreach (var used in uses)
                        if (dev.Remove(used))
                        {
                            Debug.LogWarning($"[DevAssemblyFilter] {name}, {used}'i gerçekten kullanıyor; oyunda bırakıldı.");
                            changed = true;
                        }
                }
            }

            var kept = assemblies.Where(path => !dev.Contains(Path.GetFileNameWithoutExtension(path))).ToArray();
            Debug.Log($"[DevAssemblyFilter] {dev.Count} geliştirme derlemesi çıkarıldı: {string.Join(", ", dev.OrderBy(n => n))}");
            return kept;
        }

        /// <summary>Each loaded assembly's name, with the names its compiled code references.</summary>
        static Dictionary<string, string[]> LoadedReferences()
        {
            var map = new Dictionary<string, string[]>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { map[assembly.GetName().Name] = assembly.GetReferencedAssemblies().Select(r => r.Name).ToArray(); }
                catch (Exception) { /* dynamic or unloadable: nothing to learn from it */ }
            }

            return map;
        }

        static bool InDevPackage(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            path = path.Replace('\\', '/');
            return DevPackages.Any(p => path.Contains("/" + p + "/") || path.Contains("/" + p + "@") || path.StartsWith(p + "/"));
        }
    }
}
