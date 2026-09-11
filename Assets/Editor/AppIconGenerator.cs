using System.IO;
using BlockPuzzle.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Bakes the code-drawn app icon (<see cref="AppIconArt"/>) into Assets/Generated and assigns
    /// it — legacy, round and adaptive layers for Android — along with a splash background in
    /// the game's own blue. Android only takes icons from texture assets, so these PNGs are the
    /// one exception to "no art files": they are derived, and this script is their source.
    ///
    /// Runs when the Editor loads if the icons are missing; "PRIZMA/Uygulama İkonunu Yeniden Üret"
    /// redraws them after a change to the art.
    /// </summary>
    [InitializeOnLoad]
    static class AppIconGenerator
    {
        const string Folder = "Assets/Generated";
        const string FullPath = Folder + "/AppIcon.png";
        const string RoundPath = Folder + "/AppIconRound.png";
        const string BackgroundPath = Folder + "/AppIconBackground.png";
        const string ForegroundPath = Folder + "/AppIconForeground.png";

        static AppIconGenerator() => EditorApplication.delayCall += Ensure;

        static void Ensure()
        {
            if (!File.Exists(FullPath) || !File.Exists(RoundPath) || !File.Exists(BackgroundPath) || !File.Exists(ForegroundPath))
                Generate();
            else
                Assign();
        }

        [MenuItem("PRIZMA/Uygulama İkonunu Yeniden Üret")]
        static void Generate()
        {
            Directory.CreateDirectory(Folder);

            Write(FullPath, AppIconArt.Full(1024));
            Write(RoundPath, AppIconArt.Round(1024));
            Write(BackgroundPath, AppIconArt.Background(432));
            Write(ForegroundPath, AppIconArt.Foreground(432));

            AssetDatabase.Refresh();
            foreach (var path in new[] { FullPath, RoundPath, BackgroundPath, ForegroundPath })
                ConfigureImporter(path);

            Assign();
            Debug.Log("[PRIZMA] Uygulama ikonu üretildi: " + Folder);
        }

        static void Write(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static void ConfigureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void Assign()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(FullPath);
            var round = AssetDatabase.LoadAssetAtPath<Texture2D>(RoundPath);
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);
            if (full == null || round == null || background == null || foreground == null) return;

            bool dirty = false;

            var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (current == null || current.Length == 0 || current[0] != full)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
                dirty = true;
            }

#if UNITY_ANDROID
            // Unity 6 builds Android icons from the adaptive layers alone; the old round and
            // legacy kinds are gone. Launchers without adaptive support fall back to the default
            // icon set above.
            dirty |= SetAndroid(UnityEditor.Android.AndroidPlatformIconKind.Adaptive, background, foreground);
#endif

            // The splash sits in the game's own blue rather than Unity's charcoal, so launching
            // does not flash a colour that belongs to nothing.
            var splash = Design.BgBottom;
            if (PlayerSettings.SplashScreen.backgroundColor != splash)
            {
                PlayerSettings.SplashScreen.backgroundColor = splash;
                PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
                dirty = true;
            }

            // Written to ProjectSettings now, not whenever the project next happens to be saved,
            // so the assignment is in version control with the icons it points at. The file on
            // disk is checked too: an assignment made in an earlier session can sit in memory,
            // unchanged and therefore not "dirty" here, without ever having been written.
            if (dirty || !OnDisk(AssetDatabase.AssetPathToGUID(FullPath))) AssetDatabase.SaveAssets();
        }

        static bool OnDisk(string guid)
        {
            const string settings = "ProjectSettings/ProjectSettings.asset";
            return string.IsNullOrEmpty(guid) || !File.Exists(settings) || File.ReadAllText(settings).Contains(guid);
        }

#if UNITY_ANDROID
        static bool SetAndroid(PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            bool changed = false;

            foreach (var icon in icons)
            {
                var existing = icon.GetTextures();
                bool same = existing != null && existing.Length == layers.Length;
                for (int i = 0; same && i < layers.Length; i++) same = existing[i] == layers[i];
                if (same) continue;

                icon.SetTextures(layers);
                changed = true;
            }

            if (changed) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            return changed;
        }
#endif
    }
}
