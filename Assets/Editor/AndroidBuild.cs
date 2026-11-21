using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PowderMan.EditorTools
{
    /// <summary>
    /// Android APK builds and the app icon.
    ///
    /// Tools > Powder Man > Build APK: apks/PowderMan.apk. Applies the app id, the version from BuildInfo.cs
    ///   and the icon first. Bump BuildInfo.VersionCode before each release.
    /// Tools > Powder Man > Redraw App Icon: regenerates the PNGs in Assets/Icons (pixel-art sand pile).
    ///
    /// Without the editor (Unity closed):
    ///   Unity -batchmode -nographics -projectPath . -executeMethod PowderMan.EditorTools.AndroidBuild.CommandLine -logFile Logs/android-build.log
    /// </summary>
    public static class AndroidBuild
    {
        public const string ApkPath = "apks/PowderMan.apk";
        const string AppId = "com.Tony3974.PowderMan";
        const string IconFolder = "Assets/Icons/";

        [MenuItem("Tools/Powder Man/Build APK")]
        static void MenuBuild() => Build();

        public static bool Build(string path = ApkPath)
        {
            ApplyPlayerSettings();
            ApplyIcons();
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"Powder Man: built {path} v{BuildInfo.Version} ({BuildInfo.VersionCode}), " +
                          $"{new FileInfo(path).Length / (1024f * 1024f):0.0} MB in {s.totalTime.TotalSeconds:0} s.");
                return true;
            }
            Debug.LogError($"Powder Man: Android build {s.result} with {s.totalErrors} errors.");
            return false;
        }

        /// <summary>Batch mode entry point. Exits 0 when the APK built.</summary>
        public static void CommandLine()
        {
            bool ok;
            try { ok = Build(); }
            catch (Exception e) { Debug.LogException(e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Tony3974";
            PlayerSettings.productName = "Powder Man";
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.bundleVersion = BuildInfo.Version;
            PlayerSettings.Android.bundleVersionCode = BuildInfo.VersionCode;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Landscape only, either way up.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SplashScreen.backgroundColor = new Color32(17, 19, 27, 255);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ icon

        [MenuItem("Tools/Powder Man/Redraw App Icon")]
        static void RedrawIcon()
        {
            DrawIconFiles();
            ApplyIcons();
        }

        static void ApplyIcons()
        {
            if (!File.Exists(IconFolder + "AppIcon.png")) DrawIconFiles();
            var legacy = LoadIcon("AppIcon.png");
            var round = LoadIcon("AppIcon_Round.png");
            var background = LoadIcon("AppIcon_Background.png");
            var foreground = LoadIcon("AppIcon_Foreground.png");

            var android = NamedBuildTarget.Android;
            SetAllSizes(android, AndroidPlatformIconKind.Legacy, legacy);
            SetAllSizes(android, AndroidPlatformIconKind.Round, round);
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive) icon.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
            AssetDatabase.SaveAssets();
        }

        /// <summary>A pixel-art sand pile with a stream of grains falling onto it, on the world's background colour.</summary>
        static void DrawIconFiles()
        {
            const int size = 432, cells = 36, cell = size / cells;
            var bg = new Color32[size * size];
            var fg = new Color32[size * size];
            Color32 bgTop = new Color32(30, 33, 46, 255), bgBottom = new Color32(14, 16, 23, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                bg[y * size + x] = Color32.Lerp(bgBottom, bgTop, y / (float)size);

            var sand = ElementDatabase.Table[El.Sand];
            var rng = new System.Random(7);
            Color32 Grain() => Color32.Lerp(sand.Color, sand.Color2, (float)rng.NextDouble());

            void Cell(int cx, int cy, Color32 c)
            {
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                    fg[(cy * cell + y) * size + cx * cell + x] = c;
            }

            // The pile: a mound in the adaptive-icon safe zone (cells ~7..29).
            const int baseRow = 9, centre = 18;
            for (int cx = 8; cx <= 28; cx++)
            {
                int height = Mathf.Max(1, 9 - Mathf.RoundToInt(Mathf.Abs(cx - centre) * 0.85f) + rng.Next(0, 2));
                for (int cy = baseRow; cy < baseRow + height; cy++) Cell(cx, cy, Grain());
            }
            // The falling stream, with gaps.
            for (int cy = baseRow + 10; cy <= 28; cy++)
            {
                if (rng.NextDouble() < 0.8) Cell(centre, cy, Grain());
                if (rng.NextDouble() < 0.35) Cell(centre + (rng.Next(2) == 0 ? -1 : 1), cy, Grain());
            }

            var legacy = new Color32[size * size];
            var round = new Color32[size * size];
            float r = size / 2f;
            for (int i = 0; i < legacy.Length; i++)
            {
                var c = fg[i].a > 0 ? fg[i] : bg[i];
                legacy[i] = c;
                float dx = i % size + 0.5f - r, dy = i / size + 0.5f - r;
                round[i] = dx * dx + dy * dy <= r * r ? c : new Color32(0, 0, 0, 0);
            }

            Directory.CreateDirectory(IconFolder);
            WritePng("AppIcon.png", legacy, size);
            WritePng("AppIcon_Round.png", round, size);
            WritePng("AppIcon_Background.png", bg, size);
            WritePng("AppIcon_Foreground.png", fg, size);
            AssetDatabase.Refresh();
        }

        static void WritePng(string file, Color32[] pixels, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(IconFolder + file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void SetAllSizes(NamedBuildTarget target, PlatformIconKind kind, Texture2D texture)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTexture(texture);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        /// <summary>Loads an icon PNG, importing it as a plain uncompressed texture (no sprite, no mipmaps).</summary>
        static Texture2D LoadIcon(string file)
        {
            string path = IconFolder + file;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                AssetDatabase.ImportAsset(path);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer != null && (importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                                     importer.npotScale != TextureImporterNPOTScale.None ||
                                     importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
