using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// The release build: one press makes the two files a release needs, and refuses to hand over
    /// either if something is wrong with them. Every version gets a folder of its own, named after
    /// the version (Player Settings → Version, e.g. 1.0.0), and the newest good one is marked active:
    ///
    ///   Builds/1.0.0/                  an earlier release, kept as it was shipped
    ///   Builds/1.1.0-active/           the current one
    ///       PRIZMA-1.1.0.aab           what is uploaded to Google Play. Play builds the per-phone APKs
    ///                                  from it and signs them with its own key ("Play App Signing")
    ///       PRIZMA-1.1.0.apk           the same bundle as one APK that installs on any phone, for
    ///                                  trying the release on a real device before uploading it
    ///       build-report.txt           what was checked, and how it came out
    ///   Builds/1.2.0-failed/           a build that did not pass: only its report, the active one untouched
    ///
    /// Building a version that already has a folder replaces that folder.
    ///
    /// Both are signed with the upload key (<see cref="Signing"/>), never the debug key. The version
    /// code counts up by itself (<see cref="VersionCode"/>). Development packages are left out of the
    /// player (<see cref="DevAssemblyFilter"/>). Afterwards the APK is opened and checked
    /// (<see cref="Checks"/>): target API, permissions, signature, 16 KB pages, no development code.
    ///
    /// The whole game hangs off one component in the scene (<c>GameRoot</c> → AppController). An APK
    /// once went out where the build could not resolve that script: it built "successfully", with
    /// only a warning in the log, and on the phone the Unity splash gave way to the empty default
    /// sky. So this checks the script before building and watches the build for a missing-script
    /// warning. Any failure deletes the outputs and leaves the active version where it was.
    ///
    /// Three ways in: the menu, the command line on the closed project
    ///   Unity -batchmode -quit -projectPath &lt;project&gt; -buildTarget Android
    ///         -executeMethod BlockPuzzle.EditorTools.ReleaseBuild.BuildAndroid [-releaseBuildDir &lt;Builds folder&gt;]
    /// or, with the Editor already open, an empty file at Temp/prizma-build-request, which the next
    /// script reload picks up (<see cref="BuildRequest"/>).
    /// </summary>
    public static class ReleaseBuild
    {
        const string DefaultDir = "Builds";
        const string Scene = "Assets/Scenes/SampleScene.unity";
        const string RootScript = "Assets/Scripts/Game/AppController.cs";

        [MenuItem("PRIZMA/Android Yayın Build'i (AAB + APK)")]
        public static void BuildAndroidFromMenu() => Build(DefaultDir, exit: false);

        public static void BuildAndroid() => Build(Arg("-releaseBuildDir") ?? DefaultDir, exit: true);

        const string ActiveSuffix = "-active";
        const string FailedSuffix = "-failed";
        const string BuildingSuffix = "-building";

        static void Build(string root, bool exit)
        {
            root = Path.GetFullPath(root);
            string version = PlayerSettings.bundleVersion.Trim();

            // Built into a folder of its own first; it only takes the version's name once it passes.
            string dir = Path.Combine(root, version + BuildingSuffix);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            string aab = Path.Combine(dir, $"PRIZMA-{version}.aab");
            string apk = Path.Combine(dir, $"PRIZMA-{version}.apk");

            var problems = new List<string>();
            var notes = new List<string>();

            // The component the scene depends on must resolve to a class before anything is built.
            CheckRoot(problems);

            var signing = Signing.Load(problems);
            int versionCode = VersionCode.Next();

            BuildResult result = BuildResult.Failed;
            if (problems.Count == 0)
                result = BuildBundle(aab, signing, versionCode, problems);

            if (result == BuildResult.Succeeded && problems.Count == 0)
                Bundle.MakeUniversalApk(aab, apk, signing, problems);

            if (problems.Count == 0 && File.Exists(apk))
                Checks.Run(apk, versionCode, problems, notes);

            bool ok = result == BuildResult.Succeeded && problems.Count == 0;

            // A build that failed a check is not a build: take the files away so the broken one
            // cannot be installed or uploaded by mistake.
            if (!ok)
                foreach (var file in new[] { aab, apk })
                    if (File.Exists(file)) File.Delete(file);

            // The folder's final name: the new active version, or a failure beside the active one.
            string final = Path.Combine(root, version + (ok ? ActiveSuffix : FailedSuffix));

            var lines = new List<string>
            {
                $"PRIZMA release build — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Sonuç: {(ok ? "BAŞARILI" : "BAŞARISIZ")} (Unity: {result})",
                $"Sürüm: {version} · versionCode {versionCode}",
                $"Klasör: {final}",
                $"Mağaza paketi (Google Play'e yüklenir): {Path.GetFileName(aab)}" + (File.Exists(aab) ? $" — {new FileInfo(aab).Length / (1024 * 1024)} MB" : " — yok"),
                $"Telefon APK'sı (doğrudan kurulur): {Path.GetFileName(apk)}" + (File.Exists(apk) ? $" — {new FileInfo(apk).Length / (1024 * 1024)} MB" : " — yok"),
                "Development Build: kapalı"
            };
            lines.AddRange(notes.Select(n => "  " + n));
            lines.AddRange(problems.Select(p => "Sorun: " + p));

            File.WriteAllLines(Path.Combine(dir, "build-report.txt"), lines);
            Publish(root, version, dir, final, ok);

            foreach (var line in lines)
                if (ok) Debug.Log("[ReleaseBuild] " + line);
                else Debug.LogError("[ReleaseBuild] " + line);

            if (exit) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Gives the finished folder its name. A good build takes the active mark from whichever
        /// version had it and replaces any earlier folder of the same version; a failed one replaces
        /// only an earlier failure of the same version, so the active build is never disturbed by it.
        /// </summary>
        static void Publish(string root, string version, string dir, string final, bool ok)
        {
            if (ok)
            {
                foreach (var other in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(other);
                    if (!name.EndsWith(ActiveSuffix) || other == dir) continue;

                    string plain = Path.Combine(root, name.Substring(0, name.Length - ActiveSuffix.Length));
                    if (Directory.Exists(plain)) Directory.Delete(plain, true);
                    Directory.Move(other, plain);
                }

                // An earlier build of this same version, active or not, or its failure: replaced.
                foreach (var stale in new[] { version, version + FailedSuffix })
                {
                    string path = Path.Combine(root, stale);
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }
            }

            if (Directory.Exists(final)) Directory.Delete(final, true);
            Directory.Move(dir, final);
        }

        static void CheckRoot(List<string> problems)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(RootScript);
            if (script != null && script.GetClass() != null) return;

            // Say which link is broken: the script asset, its class, or the type itself.
            bool loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetType("BlockPuzzle.Game.AppController", false) != null);
            problems.Add($"{RootScript} bir sınıfa çözümlenmiyor — sahnedeki GameRoot boş kalır. " +
                         $"(betik varlığı: {(script == null ? "yok" : "var")}, " +
                         $"tip derlemede: {(loaded ? "var" : "yok")})");
        }

        /// <summary>
        /// Builds the bundle with the release settings in place, and puts the project's own settings
        /// back afterwards: the keystore path and the version code are this build's business, not
        /// something to leave changed in ProjectSettings for every build after it.
        /// </summary>
        static BuildResult BuildBundle(string path, Signing signing, int versionCode, List<string> problems)
        {
            var android = new
            {
                custom = PlayerSettings.Android.useCustomKeystore,
                keystore = PlayerSettings.Android.keystoreName,
                alias = PlayerSettings.Android.keyaliasName,
                code = PlayerSettings.Android.bundleVersionCode,
                bundle = EditorUserBuildSettings.buildAppBundle,
                development = EditorUserBuildSettings.development
            };

            // Kept off for good: with them on, the Data safety form could not honestly say nothing is collected.
            TurnOffUnityDataCollection();

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            signing.Apply();

            void Watch(string message, string stack, LogType type)
            {
                if (message.Contains("missing or no valid script")) problems.Add(message);
            }

            Application.logMessageReceived += Watch;
            DevAssemblyFilter.Active = true;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = path,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });

                foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.content.Contains("missing or no valid script") && !problems.Contains(message.content))
                        problems.Add(message.content);

                return report.summary.result;
            }
            finally
            {
                DevAssemblyFilter.Active = false;
                Application.logMessageReceived -= Watch;

                PlayerSettings.Android.useCustomKeystore = android.custom;
                PlayerSettings.Android.keystoreName = android.keystore;
                PlayerSettings.Android.keyaliasName = android.alias;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.bundleVersionCode = android.code;
                EditorUserBuildSettings.buildAppBundle = android.bundle;
                EditorUserBuildSettings.development = android.development;
            }
        }

        /// <summary>
        /// What Unity itself would send home from the player, turned off: hardware statistics
        /// ("submitAnalytics") and engine diagnostics. Both are on by default and neither has a public
        /// API, so they are written the way the settings windows write them. The game collects
        /// nothing, and the privacy policy and Data safety form say so — this keeps that true.
        /// </summary>
        static void TurnOffUnityDataCollection()
        {
            Turn("ProjectSettings/ProjectSettings.asset", "submitAnalytics");
            Turn("ProjectSettings/UnityConnectSettings.asset", "InsightsSettings.m_EngineDiagnosticsEnabled");
        }

        static void Turn(string asset, string path)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath(asset).FirstOrDefault();
            if (settings == null) return;

            var serialized = new SerializedObject(settings);
            var property = serialized.FindProperty(path);
            if (property == null || !property.boolValue) return;

            property.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ReleaseBuild] Kapatıldı: {asset} → {path}");
        }

        // ------------------------------------------------------------------ signing

        /// <summary>
        /// The upload key. It lives outside the project — in %USERPROFILE%\.prizma — so it never
        /// reaches git or the synced project folder, and its passwords are never written into
        /// ProjectSettings. <c>signing.json</c> there names the keystore, the alias and the passwords;
        /// an empty keystore path means the keystore beside it. PRIZMA_SIGNING points elsewhere.
        ///
        /// Lose it and Play can still be asked for a new upload key (Play App Signing keeps the real
        /// key on Google's side), but that takes days; keep a copy somewhere safe.
        /// </summary>
        sealed class Signing
        {
            [Serializable]
            sealed class File_
            {
                public string keystore;
                public string alias;
                public string storePass;
                public string keyPass;
            }

            public string Keystore;
            public string Alias;
            public string StorePass;
            public string KeyPass;

            public static string ConfigPath =>
                Environment.GetEnvironmentVariable("PRIZMA_SIGNING") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".prizma", "signing.json");

            public static Signing Load(List<string> problems)
            {
                string config = ConfigPath;
                if (!File.Exists(config))
                {
                    problems.Add($"İmza ayarı yok: {config}. Yükleme anahtarı olmadan yayın paketi imzalanamaz.");
                    return null;
                }

                File_ data;
                try { data = JsonUtility.FromJson<File_>(File.ReadAllText(config)); }
                catch (Exception e)
                {
                    problems.Add($"İmza ayarı okunamadı ({config}): {e.Message}");
                    return null;
                }

                string keystore = string.IsNullOrEmpty(data?.keystore)
                    ? Path.Combine(Path.GetDirectoryName(config) ?? ".", "prizma-upload.jks")
                    : data.keystore;

                if (!File.Exists(keystore))
                {
                    problems.Add($"Anahtar dosyası bulunamadı: {keystore}");
                    return null;
                }

                if (string.IsNullOrEmpty(data.alias) || string.IsNullOrEmpty(data.storePass))
                {
                    problems.Add($"İmza ayarında alias ya da şifre eksik: {config}");
                    return null;
                }

                return new Signing
                {
                    Keystore = keystore,
                    Alias = data.alias,
                    StorePass = data.storePass,
                    KeyPass = string.IsNullOrEmpty(data.keyPass) ? data.storePass : data.keyPass
                };
            }

            public void Apply()
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Keystore;
                PlayerSettings.Android.keystorePass = StorePass;
                PlayerSettings.Android.keyaliasName = Alias;
                PlayerSettings.Android.keyaliasPass = KeyPass;
            }
        }

        // ------------------------------------------------------------------ version code

        /// <summary>
        /// Play refuses an upload whose versionCode is not above every one before it, and a phone
        /// refuses to install an APK over a newer one. Counted in minutes since 1 Jan 2026 (UTC), it
        /// only ever climbs, needs no file to remember it, and two builds a minute apart still differ.
        /// It reaches Play's ceiling (2 100 000 000) in about four thousand years.
        /// </summary>
        static class VersionCode
        {
            static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public static int Next() => Math.Max(2, (int)(DateTime.UtcNow - Epoch).TotalMinutes);
        }

        // ------------------------------------------------------------------ bundle → apk

        static class Bundle
        {
            /// <summary>
            /// One APK for every phone, made from the very bundle that goes to Play, so what is tried
            /// on the phone is what is uploaded. bundletool ships with Unity's Android support.
            /// </summary>
            public static void MakeUniversalApk(string aab, string apk, Signing signing, List<string> problems)
            {
                string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
                string jar = Directory.Exists(Path.Combine(engine, "Tools"))
                    ? Directory.GetFiles(Path.Combine(engine, "Tools"), "bundletool-all-*.jar").OrderBy(f => f).LastOrDefault()
                    : null;
                string java = Tool.Java();

                if (jar == null || java == null)
                {
                    problems.Add("bundletool ya da Java bulunamadı; APK üretilemedi (AAB yine de hazır).");
                    return;
                }

                string apks = Path.Combine(Path.GetTempPath(), "prizma-universal.apks");
                if (File.Exists(apks)) File.Delete(apks);

                // The passwords travel as arguments to a local process and are never logged.
                int code = Tool.Run(java,
                    $"-jar \"{jar}\" build-apks --mode=universal --overwrite --bundle=\"{aab}\" --output=\"{apks}\" " +
                    $"--ks=\"{signing.Keystore}\" --ks-key-alias={signing.Alias} " +
                    $"--ks-pass=pass:{signing.StorePass} --key-pass=pass:{signing.KeyPass}",
                    out string output, secret: true);

                if (code != 0 || !File.Exists(apks))
                {
                    problems.Add("bundletool APK üretemedi: " + Last(output));
                    return;
                }

                using (var zip = ZipFile.OpenRead(apks))
                {
                    var entry = zip.GetEntry("universal.apk");
                    if (entry == null)
                    {
                        problems.Add("bundletool çıktısında universal.apk yok.");
                        return;
                    }

                    entry.ExtractToFile(apk, true);
                }

                File.Delete(apks);
            }
        }

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// Opens the finished APK and checks what Play and the phone will see — the same checks
        /// that were once done by hand (Docs/ReleaseReadiness.md), now on every build.
        /// </summary>
        static class Checks
        {
            /// <summary>Play's minimum for new apps and updates since 31 August 2026.</summary>
            const int MinTargetSdk = 36;

            /// <summary>
            /// Every permission the game is allowed to ask for, and why. Anything else fails the
            /// build: a permission nobody chose is a question on the Data safety form nobody can
            /// answer. When ads arrive, their SDK's permissions (AD_ID and the like) go here, with the
            /// form and the privacy policy updated in the same change.
            /// </summary>
            static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
            {
                { "android.permission.POST_NOTIFICATIONS", "günlük hatırlatıcı (Android 13+)" },
                { "android.permission.RECEIVE_BOOT_COMPLETED", "telefon yeniden başlayınca hatırlatıcıyı geri kurmak" },
                { "android.permission.VIBRATE", "titreşim" },
                { "android.permission.INTERNET", "Unity çalışma zamanı; reklamla birlikte gerçekten kullanılacak" },
                { "android.permission.ACCESS_NETWORK_STATE", "Unity çalışma zamanı; reklamla birlikte gerçekten kullanılacak" },
            };

            /// <summary>Names that belong to development tools and must not be in the player.</summary>
            static readonly string[] DevMarkers = { "Unity.AI.", "Unity.Pipeline", "VisualScripting", "Microsoft.CodeAnalysis" };

            public static void Run(string apk, int versionCode, List<string> problems, List<string> notes)
            {
                Badging(apk, versionCode, problems, notes);
                Signature(apk, problems, notes);
                Alignment(apk, problems, notes);
                Assemblies(apk, problems, notes);
            }

            static void Badging(string apk, int versionCode, List<string> problems, List<string> notes)
            {
                string aapt2 = Tool.BuildTool("aapt2");
                if (aapt2 == null) { problems.Add("aapt2 bulunamadı; manifest kontrol edilemedi."); return; }

                if (Tool.Run(aapt2, $"dump badging \"{apk}\"", out string badging) != 0)
                {
                    problems.Add("aapt2 APK'yı okuyamadı: " + Last(badging));
                    return;
                }

                var package = Regex.Match(badging, @"package: name='([^']+)' versionCode='(\d+)' versionName='([^']*)'");
                if (package.Success)
                {
                    notes.Add($"Paket: {package.Groups[1].Value} · versionName {package.Groups[3].Value} · versionCode {package.Groups[2].Value}");
                    if (package.Groups[2].Value != versionCode.ToString())
                        problems.Add($"versionCode {package.Groups[2].Value}, beklenen {versionCode}.");
                }

                var target = Regex.Match(badging, @"targetSdkVersion:'(\d+)'");
                int targetSdk = target.Success ? int.Parse(target.Groups[1].Value) : 0;
                notes.Add($"Hedef API: {targetSdk} (Play en az {MinTargetSdk} istiyor)");
                if (targetSdk < MinTargetSdk) problems.Add($"Hedef API {targetSdk} < {MinTargetSdk}: Play yüklemeyi reddeder.");

                if (badging.Contains("application-debuggable")) problems.Add("Uygulama debuggable işaretli.");

                foreach (Match permission in Regex.Matches(badging, @"uses-permission: name='([^']+)'"))
                {
                    string name = permission.Groups[1].Value;
                    // AndroidX declares this one in the app's own namespace so its receivers stay private
                    // to the app; it is not a permission the player is ever asked for.
                    if (name.EndsWith(".DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION")) continue;
                    if (Allowed.TryGetValue(name, out string why)) notes.Add($"İzin: {name} — {why}");
                    else problems.Add($"Beklenmeyen izin: {name}. Bilerek eklendiyse ReleaseBuild.Checks.Allowed'a nedeniyle yaz.");
                }
            }

            static void Signature(string apk, List<string> problems, List<string> notes)
            {
                string apksigner = Tool.BuildTool("apksigner");
                if (apksigner == null) { problems.Add("apksigner bulunamadı; imza kontrol edilemedi."); return; }

                int code = Tool.Run(apksigner, $"verify --print-certs \"{apk}\"", out string output);
                var signer = Regex.Match(output, @"certificate DN: (.+)");
                if (code != 0) problems.Add("İmza doğrulanamadı: " + Last(output));
                else if (!signer.Success) problems.Add("İmzalayan sertifika okunamadı.");
                else if (signer.Groups[1].Value.Contains("Android Debug")) problems.Add("APK debug anahtarıyla imzalı.");
                else notes.Add("İmza: " + signer.Groups[1].Value.Trim());
            }

            /// <summary>
            /// Android 15+ phones with 16 KB memory pages need every native library aligned to 16 KB,
            /// both inside the zip and in its ELF load segments.
            /// </summary>
            static void Alignment(string apk, List<string> problems, List<string> notes)
            {
                string zipalign = Tool.BuildTool("zipalign");
                if (zipalign == null) problems.Add("zipalign bulunamadı; 16 KB hizası kontrol edilemedi.");
                else if (Tool.Run(zipalign, $"-c -P 16 4 \"{apk}\"", out string output) != 0)
                    problems.Add("16 KB zip hizası tutmuyor: " + Last(output));

                int libraries = 0;
                using (var zip = ZipFile.OpenRead(apk))
                foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("lib/") && e.FullName.EndsWith(".so")))
                {
                    libraries++;
                    using (var stream = entry.Open())
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        long align = SmallestLoadAlignment(memory.ToArray());
                        if (align >= 0 && align < 16384)
                            problems.Add($"{entry.FullName}: LOAD hizası 0x{align:X}, 16 KB sayfalarda açılmaz.");
                    }
                }

                notes.Add($"16 KB sayfa uyumu: {libraries} yerel kütüphane kontrol edildi");
            }

            /// <summary>The smallest p_align of the PT_LOAD segments of a 64-bit ELF; -1 if it is not one.</summary>
            static long SmallestLoadAlignment(byte[] elf)
            {
                if (elf.Length < 64 || elf[0] != 0x7f || elf[1] != 'E' || elf[2] != 'L' || elf[3] != 'F' || elf[4] != 2)
                    return -1;

                long phoff = BitConverter.ToInt64(elf, 0x20);
                int phentsize = BitConverter.ToUInt16(elf, 0x36);
                int phnum = BitConverter.ToUInt16(elf, 0x38);
                long smallest = long.MaxValue;

                for (int i = 0; i < phnum; i++)
                {
                    long at = phoff + (long)i * phentsize;
                    if (at + 56 > elf.Length) break;
                    if (BitConverter.ToUInt32(elf, (int)at) != 1) continue; // PT_LOAD
                    smallest = Math.Min(smallest, BitConverter.ToInt64(elf, (int)at + 48));
                }

                return smallest == long.MaxValue ? -1 : smallest;
            }

            static void Assemblies(string apk, List<string> problems, List<string> notes)
            {
                using (var zip = ZipFile.OpenRead(apk))
                {
                    var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("ScriptingAssemblies.json"));
                    if (entry == null) { notes.Add("ScriptingAssemblies.json bulunamadı; derlemeler kontrol edilemedi."); return; }

                    string json;
                    using (var reader = new StreamReader(entry.Open())) json = reader.ReadToEnd();

                    var names = Regex.Matches(json, "\"([^\"]+\\.dll)\"").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
                    var dev = names.Where(n => DevMarkers.Any(n.Contains)).ToList();
                    notes.Add($"Oyuna giren derleme: {names.Count}");
                    foreach (var name in dev) problems.Add($"Geliştirme aracı oyuna girmiş: {name}");
                }
            }
        }

        // ------------------------------------------------------------------ tools

        static class Tool
        {
            public static string Java()
            {
                string jdk = AndroidExternalToolsSettings.jdkRootPath;
                if (string.IsNullOrEmpty(jdk)) return null;
                string java = Path.Combine(jdk, "bin", Application.platform == RuntimePlatform.WindowsEditor ? "java.exe" : "java");
                return File.Exists(java) ? java : null;
            }

            /// <summary>The newest build-tools' copy of a tool from the Android SDK Unity uses.</summary>
            public static string BuildTool(string name)
            {
                string sdk = AndroidExternalToolsSettings.sdkRootPath;
                string root = string.IsNullOrEmpty(sdk) ? null : Path.Combine(sdk, "build-tools");
                if (root == null || !Directory.Exists(root)) return null;

                bool windows = Application.platform == RuntimePlatform.WindowsEditor;
                foreach (var dir in Directory.GetDirectories(root).OrderByDescending(d => Version.TryParse(Path.GetFileName(d), out var v) ? v : new Version(0, 0)))
                foreach (var file in windows ? new[] { name + ".exe", name + ".bat" } : new[] { name })
                {
                    string path = Path.Combine(dir, file);
                    if (File.Exists(path)) return path;
                }

                return null;
            }

            public static int Run(string file, string arguments, out string output, bool secret = false)
            {
                bool batch = file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
                var info = new ProcessStartInfo
                {
                    FileName = batch ? "cmd.exe" : file,
                    Arguments = batch ? $"/c \"\"{file}\" {arguments}\"" : arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // apksigner and bundletool run on Java; point them at the JDK Unity uses.
                string jdk = AndroidExternalToolsSettings.jdkRootPath;
                if (!string.IsNullOrEmpty(jdk)) info.EnvironmentVariables["JAVA_HOME"] = jdk;

                try
                {
                    using (var process = Process.Start(info))
                    {
                        var stderr = process.StandardError.ReadToEndAsync();
                        string stdout = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        output = stdout + stderr.Result;
                        return process.ExitCode;
                    }
                }
                catch (Exception e)
                {
                    output = secret ? e.GetType().Name : e.Message;
                    return -1;
                }
            }
        }

        static string Last(string output)
        {
            var lines = (output ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            return lines.Length == 0 ? "(çıktı yok)" : string.Join(" | ", lines.Skip(Math.Max(0, lines.Length - 3)));
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
