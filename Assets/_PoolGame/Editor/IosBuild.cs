using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// iOS build: applies the iPhone player settings and exports an Xcode project. Apple only compiles that project on
    /// macOS, so this runs in CI (.github/workflows/ios.yml: GameCI exports on Linux, a macOS runner archives an
    /// unsigned .ipa) or on a Mac. The .ipa is signed at install time, e.g. Sideloadly with a free Apple ID.
    /// </summary>
    public static class IosBuild
    {
        public const string OutputPath = "Builds/iOS";
        public const string BundleId = AndroidBuild.PackageId;

        [MenuItem("VTG Pool/Build/iOS Xcode project", priority = 52)]
        public static void BuildFromMenu() => Build(OutputPath);

        public static bool IsModuleInstalled()
        {
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);
        }

        /// <summary>Applies the iOS player settings (bundle id, landscape, IL2CPP device SDK, no automatic signing).</summary>
        [MenuItem("VTG Pool/Build/Apply iOS player settings", priority = 53)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "VTG";
            PlayerSettings.productName = "VTG Pool 3D";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            // Signed when installed (Sideloadly / Xcode), so the exported project must not ask for a team.
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.requiresFullScreen = true;

            // Same as Android: landscape only, both directions; the UI keeps to Screen.safeArea.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
        }

        /// <summary>Entry point for CI (-executeMethod / GameCI buildMethod). Uses -customBuildPath when given.</summary>
        public static void BuildForCi()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-customBuildPath");
            string path = index >= 0 && index + 1 < args.Length ? args[index + 1] : OutputPath;
            bool ok = Build(path);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        /// <summary>Exports the Xcode project to <paramref name="path"/>. Returns true on success.</summary>
        public static bool Build(string path)
        {
            if (!IsModuleInstalled())
            {
                Debug.LogError("[IosBuild] iOS Build Support is not installed for this editor.");
                return false;
            }

            ApplyPlayerSettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            });
            BuildSummary summary = report.summary;
            Debug.Log($"[IosBuild] result={summary.result} time={summary.totalTime} errors={summary.totalErrors} -> {Path.GetFullPath(path)}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
