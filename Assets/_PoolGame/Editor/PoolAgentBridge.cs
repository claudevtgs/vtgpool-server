using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// File-based automation bridge so coding agents can drive an editor that is already open
    /// (batch mode cannot open a project that is in use). Drop an empty "&lt;command&gt;.req" file into
    /// Temp/VTGPoolBridge/ and the editor executes it on its next update:
    ///   refresh, setup, settings, assets, scene, tests-editmode, tests-playmode, build-android
    /// Outputs: compile.log, results-EditMode.txt, results-PlayMode.txt, bridge.log, heartbeat.txt.
    /// Also performs the one-time automatic project setup (stamp: ProjectSettings/VTGPoolSetupVersion.txt).
    /// </summary>
    [InitializeOnLoad]
    public static class PoolAgentBridge
    {
        public const string Directory = "Temp/VTGPoolBridge";
        private const string SetupStampPath = "ProjectSettings/VTGPoolSetupVersion.txt";
        private const string SetupVersion = "M6.2";

        private static double nextPoll;
        private static string testFilter = string.Empty;
        private static double nextHeartbeat;
        private static readonly TestRunnerApi testApi;

        static PoolAgentBridge()
        {
            System.IO.Directory.CreateDirectory(Directory);
            EditorApplication.update += Poll;

            // Editor-only: keep Play mode ticking while the editor window is unfocused (agents drive it via MCP/bridge).
            // Does not touch PlayerSettings.runInBackground, so builds are unaffected.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Application.runInBackground = true;
                }
            };
            CompilationPipeline.compilationStarted += _ => File.WriteAllText(Path.Combine(Directory, "compile.log"), $"compilation started {DateTime.Now:O}\n");
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;

            testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            testApi.RegisterCallbacks(new ResultWriter());

            Log("bridge loaded");
            EditorApplication.delayCall += AutoSetupIfNeeded;
        }

        private static void AutoSetupIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += AutoSetupIfNeeded;
                return;
            }

            if (File.Exists(SetupStampPath) && File.ReadAllText(SetupStampPath).Trim() == SetupVersion)
            {
                return;
            }

            Log($"auto setup {SetupVersion} starting");
            try
            {
                PoolProjectSetup.RunFullSetup();
                File.WriteAllText(SetupStampPath, SetupVersion);
                Log("auto setup finished");
            }
            catch (Exception exception)
            {
                Log("auto setup FAILED: " + exception);
                Debug.LogException(exception);
            }
        }

        private static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextHeartbeat)
            {
                nextHeartbeat = now + 5.0;
                File.WriteAllText(Path.Combine(Directory, "heartbeat.txt"),
                    $"{DateTime.Now:O} compiling={EditorApplication.isCompiling} playing={EditorApplication.isPlaying}\n");
            }

            if (now < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            nextPoll = now + 0.5;
            string[] requests = System.IO.Directory.GetFiles(Directory, "*.req");
            if (requests.Length == 0)
            {
                return;
            }

            Array.Sort(requests, StringComparer.Ordinal);
            string request = requests[0];
            string command = Path.GetFileNameWithoutExtension(request);
            testFilter = File.ReadAllText(request).Trim();
            File.Delete(request);
            Execute(command);
        }

        private static void Execute(string command)
        {
            Log("executing " + command);
            try
            {
                switch (command)
                {
                    case "refresh":
                        AssetDatabase.Refresh();
                        break;
                    case "setup":
                        PoolProjectSetup.RunFullSetup();
                        File.WriteAllText(SetupStampPath, SetupVersion);
                        break;
                    case "settings":
                        PoolProjectSetup.ApplyProjectSettings();
                        break;
                    case "assets":
                        PoolProjectSetup.CreateOrUpdateAssets();
                        break;
                    case "scene":
                        PoolProjectSetup.CreatePhysicsTestScene(false);
                        PoolProjectSetup.CreateMatchScene(true);
                        break;
                    case "tests-editmode":
                        RunTests(TestMode.EditMode);
                        break;
                    case "tests-playmode":
                        RunTests(TestMode.PlayMode);
                        break;
                    case "build-windows":
                        PcBuild.Build();
                        break;
                    case "build-webgl":
                        WebBuild.Build();
                        break;
                    case "build-android":
                        // Request content "dev" makes a development build.
                        AndroidBuild.Build(testFilter == "dev");
                        break;
                    default:
                        Log("unknown command " + command);
                        break;
                }

                Log("done " + command);
            }
            catch (Exception exception)
            {
                Log($"FAILED {command}: {exception}");
                Debug.LogException(exception);
            }
        }

        private static void RunTests(TestMode mode)
        {
            File.WriteAllText(Path.Combine(Directory, $"results-{mode}.txt"), $"running {DateTime.Now:O}\n");
            // Optional filter: request file content = test group / class name regex (e.g. "AIMatchTests").
            var filter = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(testFilter))
            {
                filter.groupNames = new[] { testFilter };
            }

            testApi.Execute(new ExecutionSettings(filter));
        }

        private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            var builder = new StringBuilder();
            builder.Append(Path.GetFileName(assemblyPath)).Append(": ");
            int errors = 0;
            int warnings = 0;
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error) errors++;
                else warnings++;
            }

            builder.Append(errors).Append(" errors, ").Append(warnings).Append(" warnings\n");
            foreach (CompilerMessage message in messages)
            {
                builder.Append("  ").Append(message.type).Append(' ').Append(message.message).Append('\n');
            }

            File.AppendAllText(Path.Combine(Directory, "compile.log"), builder.ToString());
        }

        private static void Log(string message)
        {
            File.AppendAllText(Path.Combine(Directory, "bridge.log"), $"{DateTime.Now:HH:mm:ss} {message}\n");
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var builder = new StringBuilder();
                builder.Append($"finished {DateTime.Now:O}\n");
                builder.Append($"status={result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount} duration={result.Duration:F1}s\n");
                AppendFailures(result, builder);
                string mode = result.Test != null && result.Test.TestMode == TestMode.PlayMode ? "PlayMode" : "EditMode";
                File.WriteAllText(Path.Combine(Directory, $"results-{mode}.txt"), builder.ToString());
                Log($"tests finished ({mode}): {result.TestStatus}");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    File.AppendAllText(Path.Combine(Directory, "tests.log"), $"{result.TestStatus} {result.FullName} ({result.Duration:F2}s)\n");
                }
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder builder)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                    {
                        builder.Append("FAIL ").Append(result.FullName).Append('\n').Append("   ").Append(result.Message).Append('\n');
                    }
                    else
                    {
                        builder.Append(result.TestStatus).Append(' ').Append(result.FullName).Append('\n');
                    }

                    return;
                }

                foreach (ITestResultAdaptor child in result.Children)
                {
                    AppendFailures(child, builder);
                }
            }
        }
    }
}
