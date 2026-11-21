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
    /// The icon PNGs in Assets/Icons come from Art/make_stickman_icons.py (the "running" draft).
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

        static void ApplyIcons()
        {
            var legacy = LoadIcon("AppIcon.png");
            var round = LoadIcon("AppIcon_Round.png");
            var background = LoadIcon("AppIcon_Background.png");
            var foreground = LoadIcon("AppIcon_Foreground.png");
            if (legacy == null || round == null || background == null || foreground == null)
            {
                Debug.LogError($"App icon PNGs missing from {IconFolder}. Make them with Art/make_stickman_icons.py.");
                return;
            }

            var android = NamedBuildTarget.Android;
            SetAllSizes(android, AndroidPlatformIconKind.Legacy, legacy);
            SetAllSizes(android, AndroidPlatformIconKind.Round, round);
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive) icon.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
            AssetDatabase.SaveAssets();
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
            if (!File.Exists(path)) return null;
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
