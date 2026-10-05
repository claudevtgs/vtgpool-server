using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// Android test build: applies mobile player settings (package id, landscape, IL2CPP ARM64, 60 fps friendly)
    /// and builds a side-loadable APK to Builds/Android/VTGPool3D.apk (debug-signed by Unity).
    /// Also available to agents through the bridge command "build-android".
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Android/VTGPool3D.apk";
        public const string PackageId = "com.vtg.pool3d";

        [MenuItem("VTG Pool/Build/Android APK (test)", priority = 50)]
        public static void BuildFromMenu() => Build(false);

        public static bool IsModuleInstalled()
        {
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
        }

        /// <summary>Applies the Android player settings used for test builds.</summary>
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "VTG";
            PlayerSettings.productName = "VTG Pool 3D";
            PlayerSettings.bundleVersion = "0.5.0";
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Pool is played in landscape; allow both landscape directions.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            // Draw behind the notch; the UI keeps to Screen.safeArea (ResponsiveCanvas).
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.runInBackground = false;
            // Online rooms need network access even if Unity's API scan misses the WebSocket use.
            PlayerSettings.Android.forceInternetPermission = true;
        }

        /// <summary>Builds the APK. Returns true on success; writes a summary to Temp/VTGPoolBridge/build-android.txt.</summary>
        public static bool Build(bool development)
        {
            DateTime startTime = DateTime.Now;
            var log = new StringBuilder();
            log.AppendLine($"started {DateTime.Now:O}");
            if (!IsModuleInstalled())
            {
                log.AppendLine("FAILED: Android Build Support is not installed for this editor.");
                WriteLog(log);
                Debug.LogError("[AndroidBuild] Android Build Support is not installed.");
                return false;
            }

            ApplyPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                log.AppendLine("switching active build target to Android");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            log.AppendLine("scenes: " + string.Join(", ", scenes));
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            log.AppendLine($"result={summary.result} size={summary.totalSize / (1024f * 1024f):0.0} MB time={summary.totalTime} errors={summary.totalErrors} warnings={summary.totalWarnings}");
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                    {
                        log.AppendLine($"[{step.name}] {message.content}");
                    }
                }
            }

            // The report can say Succeeded while the Android post-process (Gradle) never ran, e.g. when the module was
            // installed after the editor started. Only an APK on disk counts.
            bool produced = File.Exists(OutputPath) && File.GetLastWriteTime(OutputPath) >= startTime;
            if (!produced)
            {
                log.AppendLine("FAILED: no APK was written. If the log mentions \"Build target 'Android' not supported\", restart the Unity Editor so it loads the Android module.");
            }

            log.AppendLine($"finished {DateTime.Now:O} -> {Path.GetFullPath(OutputPath)}");
            WriteLog(log);
            return produced && summary.result == BuildResult.Succeeded;
        }

        private static void WriteLog(StringBuilder log)
        {
            Directory.CreateDirectory(PoolAgentBridge.Directory);
            File.WriteAllText(Path.Combine(PoolAgentBridge.Directory, "build-android.txt"), log.ToString());
        }
    }
}
