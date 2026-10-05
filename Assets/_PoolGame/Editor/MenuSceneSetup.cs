using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VTG.Pool.Audio;
using VTG.Pool.Balls;
using VTG.Pool.Core;
using VTG.Pool.Simulation;
using VTG.Pool.Table;
using VTG.Pool.UI;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// Generates 00_Bootstrap and 01_MainMenu (lounge table with a racked set as the backdrop, slow orbit camera,
    /// menu UI) and orders the build settings: Bootstrap, MainMenu, Match, PhysicsTest.
    /// </summary>
    public static class MenuSceneSetup
    {
        public const string BootstrapScenePath = PoolProjectSetup.Root + "/Scenes/00_Bootstrap.unity";
        public const string MainMenuScenePath = PoolProjectSetup.Root + "/Scenes/01_MainMenu.unity";

        [MenuItem("VTG Pool/Setup/Rebuild Menu Scenes", priority = 5)]
        public static void CreateMenuScenes()
        {
            CreateBootstrapScene();
            CreateMainMenuScene();
            OrderBuildSettings();
            Debug.Log("[VTG Setup] Menu scenes created.");
        }

        private static void CreateBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string root = PoolProjectSetup.Root;
            BallPhysicsProfile profile = AssetDatabase.LoadAssetAtPath<BallPhysicsProfile>(root + "/ScriptableObjects/Physics/BallPhysicsProfile.asset");
            TableGeometry geometry = AssetDatabase.LoadAssetAtPath<TableGeometry>(root + "/ScriptableObjects/Physics/TableGeometry_9ft.asset");
            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/Balls/PoolBall.prefab");

            // Floor (restyled by the environment builder).
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);

            // Table with a racked set: a static backdrop (kinematic balls, no physics system).
            var tableObject = new GameObject("Table");
            tableObject.AddComponent<PocketManager>();
            TableBuilder table = tableObject.AddComponent<TableBuilder>();
            table.SetGeometry(geometry);
            table.Build();

            Transform ballsRoot = new GameObject("Balls").transform;
            var rack = new Vector3[15];
            RackLayout.GetTrianglePositions(table.FootSpot + Vector3.up * profile.ballRadius, profile.ballRadius, 0.0002f, rack);
            RackLayout.Jitter(rack, 15, profile.ballRadius, 0.0003f, 0.00005f, new System.Random(5));
            for (int number = 0; number <= 15; number++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, scene);
                instance.name = $"Ball_{number:00}";
                instance.transform.SetParent(ballsRoot, false);
                BallDefinition definition = AssetDatabase.LoadAssetAtPath<BallDefinition>($"{root}/ScriptableObjects/Balls/Ball_{number:00}.asset");
                instance.GetComponent<PoolBall>().SetDefinition(definition, profile);
                instance.GetComponentInChildren<MeshRenderer>().sharedMaterial = definition.Material;
                // Live balls: the intro plays a real break on this table.
                instance.layer = LayerMask.NameToLayer(number == 0 ? PoolLayers.CueBallName : PoolLayers.BallName);
                instance.GetComponent<Rigidbody>().isKinematic = false;
                instance.transform.position = number == 0
                    ? table.HeadSpot + Vector3.up * profile.ballRadius + Vector3.right * 0.12f
                    : rack[System.Array.IndexOf(RackLayout.EightBallOrder, number)];
                instance.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), 0f);
            }

            // Physics (cloth, cushions, ball-ball collisions) for the intro break.
            var physicsObject = new GameObject("PoolPhysicsSystem");
            PoolPhysicsSystem physics = physicsObject.AddComponent<PoolPhysicsSystem>();
            physics.Configure(profile, tableObject.GetComponent<PocketManager>(), table.SurfaceHeight);
            EditorUtility.SetDirty(physics);

            // Camera.
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 42f;
            cameraObject.AddComponent<AudioListener>();
            MenuCameraOrbit orbit = cameraObject.AddComponent<MenuCameraOrbit>();
            orbit.Configure(table.SurfaceCenter);

            // Environment, audio, graphics, settings.
            Transform systems = new GameObject("Systems").transform;
            PresentationSetup.DecorateMenuScene(table, camera, systems, out AudioManager audio);

            var menuObject = new GameObject("MainMenu");
            MainMenuController menu = menuObject.AddComponent<MainMenuController>();
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("audioManager").objectReferenceValue = audio;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void OrderBuildSettings()
        {
            string[] ordered =
            {
                BootstrapScenePath,
                MainMenuScenePath,
                PoolProjectSetup.MatchScenePath,
                PoolProjectSetup.PhysicsTestScenePath
            };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in ordered)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!scenes.Exists(s => s.path == existing.path) && existing.path.StartsWith(PoolProjectSetup.Root) && System.IO.File.Exists(existing.path))
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
