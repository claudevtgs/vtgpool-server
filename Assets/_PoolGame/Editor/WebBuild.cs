using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// Browser build (WebGL) for hosting behind nginx next to the room server: Builds/WebGL/ (index.html, Build/,
    /// TemplateData/). Gzip with decompression fallback, so it also runs if the web server sends no
    /// Content-Encoding headers. Bridge command: "build-webgl".
    /// </summary>
    public static class WebBuild
    {
        public const string OutputPath = "Builds/WebGL";

        [MenuItem("VTG Pool/Build/WebGL (browser)", priority = 51)]
        public static void BuildFromMenu() => Build();

        public static bool Build()
        {
            var log = new StringBuilder();
            DateTime start = DateTime.Now;
            log.AppendLine($"started {start:O}");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                log.AppendLine("FAILED: WebGL Build Support is not installed for this editor.");
                Write(log);
                return false;
            }

            PlayerSettings.companyName = "VTG";
            PlayerSettings.productName = "VTG Pool 3D";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                log.AppendLine("switching active build target to WebGL");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(OutputPath);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            });

            // Unchanged files (index.html, loader) are not rewritten; the data / wasm files are, on every build.
            string buildFolder = Path.Combine(OutputPath, "Build");
            bool produced = File.Exists(Path.Combine(OutputPath, "index.html")) && Directory.Exists(buildFolder)
                && Directory.GetFiles(buildFolder).Any(f => File.GetLastWriteTime(f) >= start);
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

            if (!produced)
            {
                log.AppendLine("FAILED: the WebGL player was not written.");
            }

            log.AppendLine($"finished {DateTime.Now:O} -> {Path.GetFullPath(OutputPath)}");
            Write(log);
            return produced && report.summary.result == BuildResult.Succeeded;
        }

        private static void Write(StringBuilder log)
        {
            Directory.CreateDirectory(PoolAgentBridge.Directory);
            File.WriteAllText(Path.Combine(PoolAgentBridge.Directory, "build-webgl.txt"), log.ToString());
        }
    }
}
