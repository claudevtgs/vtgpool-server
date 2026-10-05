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
    /// Windows desktop build (64-bit, Mono — no C++ toolchain needed) to Builds/Windows/VTGPool3D.exe, plus a zip
    /// next to it for sharing. Bridge command: "build-windows".
    /// </summary>
    public static class PcBuild
    {
        public const string Folder = "Builds/Windows";
        public const string ExePath = Folder + "/VTGPool3D.exe";

        [MenuItem("VTG Pool/Build/Windows PC", priority = 52)]
        public static void BuildFromMenu() => Build();

        public static bool Build()
        {
            var log = new StringBuilder();
            DateTime start = DateTime.Now;
            log.AppendLine($"started {start:O}");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                log.AppendLine("FAILED: Windows Build Support is not installed for this editor.");
                Write(log);
                return false;
            }

            PlayerSettings.companyName = "VTG";
            PlayerSettings.productName = "VTG Pool 3D";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                log.AppendLine("switching active build target to Windows");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            Directory.CreateDirectory(Folder);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });

            bool produced = File.Exists(ExePath) && Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Any(f => File.GetLastWriteTime(f) >= start);
            log.AppendLine($"result={report.summary.result} size={report.summary.totalSize / (1024f * 1024f):0.0} MB time={report.summary.totalTime} errors={report.summary.totalErrors}");
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

            if (produced)
            {
                string zip = Path.Combine("Builds", "VTGPool3D-Windows.zip");
                try
                {
                    if (File.Exists(zip)) File.Delete(zip);
                    // Leave out the debug-symbol folder Unity writes next to the player.
                    using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                        {
                            string relative = file.Substring(Folder.Length + 1).Replace('\\', '/');
                            if (relative.Contains("_BurstDebugInformation_DoNotShip") || relative.Contains("_BackUpThisFolder_ButDontShipItWithYourGame"))
                            {
                                continue;
                            }

                            System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archive, file, "VTGPool3D/" + relative);
                        }
                    }

                    log.AppendLine($"zip: {Path.GetFullPath(zip)} ({new FileInfo(zip).Length / (1024f * 1024f):0.0} MB)");
                }
                catch (Exception exception)
                {
                    log.AppendLine("zip failed: " + exception.Message);
                }
            }
            else
            {
                log.AppendLine("FAILED: the Windows player was not written.");
            }

            log.AppendLine($"finished {DateTime.Now:O} -> {Path.GetFullPath(ExePath)}");
            Write(log);
            return produced && report.summary.result == BuildResult.Succeeded;
        }

        private static void Write(StringBuilder log)
        {
            Directory.CreateDirectory(PoolAgentBridge.Directory);
            File.WriteAllText(Path.Combine(PoolAgentBridge.Directory, "build-windows.txt"), log.ToString());
        }
    }
}
