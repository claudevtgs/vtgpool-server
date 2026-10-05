using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VTG.Pool.Aiming;
using VTG.Pool.Balls;
using VTG.Pool.CameraSystem;
using VTG.Pool.Core;
using VTG.Pool.Cue;
using VTG.Pool.Diagnostics;
using VTG.Pool.Inputs;
using VTG.Pool.Rules.EightBall;
using VTG.Pool.Rules.NineBall;
using VTG.Pool.UI;
using VTG.Pool.Match;
using VTG.Pool.Simulation;
using VTG.Pool.Simulation.Testing;
using VTG.Pool.Table;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// Idempotent project bootstrap for Milestone 1: project settings, layers, collision matrix,
    /// physics materials, profiles, ball assets, prefabs and the 03_PhysicsTest scene.
    /// Menu: VTG Pool/Setup/... ; batch: -executeMethod VTG.Pool.EditorTools.PoolProjectSetup.RunFullSetupBatch
    /// </summary>
    public static class PoolProjectSetup
    {
        public const string Root = "Assets/_PoolGame";
        public const string PhysicsTestScenePath = Root + "/Scenes/03_PhysicsTest.unity";
        public const string MatchScenePath = Root + "/Scenes/02_Match.unity";

        private enum SceneKind
        {
            PhysicsTest,
            Match
        }

        private const string PhysicsMaterialsFolder = Root + "/Materials/Physics";
        private const string ProfilesFolder = Root + "/ScriptableObjects/Physics";
        private const string AIProfilesFolder = Root + "/ScriptableObjects/AI";
        private const string BallDefinitionsFolder = Root + "/ScriptableObjects/Balls";
        private const string BallTexturesFolder = Root + "/Art/Textures/Balls";
        private const string BallMaterialsFolder = Root + "/Art/Materials/Balls";
        private const string MaterialsFolder = Root + "/Art/Materials";
        private const string InputAssetPath = Root + "/Input/PoolInput.inputactions";
        private const string RulesFolder = Root + "/ScriptableObjects/Rules";

        // ---------------------------------------------------------------- menu

        [MenuItem("VTG Pool/Setup/Run Full Setup", priority = 0)]
        public static void RunFullSetup()
        {
            ApplyProjectSettings();
            CreateOrUpdateAssets();
            CreatePhysicsTestScene(false);
            CreateMatchScene(false);
            MenuSceneSetup.CreateMenuScenes();
            Debug.Log("[VTG Setup] Full setup complete.");
        }

        [MenuItem("VTG Pool/Setup/Apply Project Settings", priority = 1)]
        public static void ApplyProjectSettingsMenu() => ApplyProjectSettings();

        [MenuItem("VTG Pool/Setup/Create Or Update Assets", priority = 2)]
        public static void CreateOrUpdateAssetsMenu() => CreateOrUpdateAssets();

        [MenuItem("VTG Pool/Setup/Rebuild Match Scene", priority = 4)]
        public static void RebuildMatchSceneMenu()
        {
            CreateOrUpdateAssets();
            CreateMatchScene(true);
        }

        [MenuItem("VTG Pool/Setup/Rebuild PhysicsTest Scene", priority = 3)]
        public static void RebuildPhysicsTestSceneMenu()
        {
            CreateOrUpdateAssets();
            CreatePhysicsTestScene(true);
        }

        /// <summary>Entry point for -batchmode -executeMethod.</summary>
        public static void RunFullSetupBatch()
        {
            try
            {
                RunFullSetup();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- settings

        public static void ApplyProjectSettings()
        {
            Time.fixedDeltaTime = PoolConstants.TargetFixedTimestep;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.defaultSolverIterations = 10;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.bounceThreshold = 0.02f;
            Physics.defaultContactOffset = 0.002f;
            Physics.sleepThreshold = 0.0001f;
            Physics.defaultMaxAngularSpeed = 1500f;
            Physics.queriesHitTriggers = false;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            EnsureLayers();
            ConfigureCollisionMatrix();
            ConfigureUrp();
            AssetDatabase.SaveAssets();
            Debug.Log($"[VTG Setup] Project settings applied: fixedDeltaTime={Time.fixedDeltaTime:F6}, solver=10/4, bounceThreshold=0.02, linear colour space.");
        }

        private static void EnsureLayers()
        {
            UnityEngine.Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < PoolLayers.All.Length; i++)
            {
                int index = PoolLayers.FirstLayerIndex + i;
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                string wanted = PoolLayers.All[i];
                if (layer.stringValue == wanted)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    Debug.LogWarning($"[VTG Setup] Layer {index} is '{layer.stringValue}', overwriting with '{wanted}'.");
                }

                layer.stringValue = wanted;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCollisionMatrix()
        {
            int ball = LayerMask.NameToLayer(PoolLayers.BallName);
            int cueBall = LayerMask.NameToLayer(PoolLayers.CueBallName);
            string[] nonColliding = { PoolLayers.CueName, PoolLayers.AimHelperName, PoolLayers.UI3DName };
            for (int layer = 0; layer < 32; layer++)
            {
                foreach (string name in nonColliding)
                {
                    int index = LayerMask.NameToLayer(name);
                    if (index >= 0)
                    {
                        Physics.IgnoreLayerCollision(index, layer, true);
                    }
                }
            }

            // Balls collide with balls, cushions, slate, pocket liners, environment and Default.
            string[] ballContacts = { PoolLayers.CushionName, PoolLayers.TableName, PoolLayers.PocketName, PoolLayers.EnvironmentName };
            foreach (string name in ballContacts)
            {
                int index = LayerMask.NameToLayer(name);
                Physics.IgnoreLayerCollision(ball, index, false);
                Physics.IgnoreLayerCollision(cueBall, index, false);
            }

            // Ball-ball contacts are solved by PoolPhysicsSystem (event-driven), not by PhysX.
            Physics.IgnoreLayerCollision(ball, ball, true);
            Physics.IgnoreLayerCollision(ball, cueBall, true);
            Physics.IgnoreLayerCollision(cueBall, cueBall, true);

            // Static geometry never needs to collide with itself.
            string[] statics = { PoolLayers.CushionName, PoolLayers.TableName, PoolLayers.PocketName, PoolLayers.EnvironmentName };
            foreach (string a in statics)
            {
                foreach (string b in statics)
                {
                    Physics.IgnoreLayerCollision(LayerMask.NameToLayer(a), LayerMask.NameToLayer(b), true);
                }
            }
        }

        private static void ConfigureUrp()
        {
            var assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset)
            {
                assets.Add(defaultAsset);
            }

            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset levelAsset)
                {
                    assets.Add(levelAsset);
                }
            }

            if (assets.Count == 0)
            {
                Debug.LogError("[VTG Setup] URP validation failed: no UniversalRenderPipelineAsset assigned.");
                return;
            }

            foreach (UniversalRenderPipelineAsset asset in assets)
            {
                bool mobile = asset.name.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0;
                int msaa = mobile ? 2 : 4;
                if (asset.msaaSampleCount != msaa)
                {
                    asset.msaaSampleCount = msaa;
                    EditorUtility.SetDirty(asset);
                }

                Debug.Log($"[VTG Setup] URP asset '{asset.name}': MSAA {asset.msaaSampleCount}x, HDR {asset.supportsHDR}.");
            }

            Debug.Log($"[VTG Setup] URP validated ({assets.Count} assets, quality level {QualitySettings.names[currentLevel]}).");
        }

        // ---------------------------------------------------------------- assets

        public static void CreateOrUpdateAssets()
        {
            EnsureFolder(PhysicsMaterialsFolder);
            EnsureFolder(ProfilesFolder);
            EnsureFolder(AIProfilesFolder);
            EnsureFolder(BallDefinitionsFolder);
            EnsureFolder(BallTexturesFolder);
            EnsureFolder(BallMaterialsFolder);
            EnsureFolder(Root + "/Prefabs/Balls");
            EnsureFolder(Root + "/Prefabs/Cue");
            EnsureFolder(Root + "/Scenes");

            PhysicsMaterial ballPm = PhysicsMaterialAsset("PM_Ball", 0.06f, 0.93f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average);
            PhysicsMaterial clothPm = PhysicsMaterialAsset("PM_Cloth", 0f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);
            PhysicsMaterial cushionPm = PhysicsMaterialAsset("PM_Cushion", 0f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);

            BallPhysicsProfile profile = LoadOrCreate<BallPhysicsProfile>(ProfilesFolder + "/BallPhysicsProfile.asset");
            profile.ballMaterial = ballPm;
            EditorUtility.SetDirty(profile);

            CueStrikeProfile strike = LoadOrCreate<CueStrikeProfile>(ProfilesFolder + "/CueStrikeProfile.asset");
            EditorUtility.SetDirty(strike);
            EditorUtility.SetDirty(LoadOrCreate<VTG.Pool.AI.AIProfile>(AIProfilesFolder + "/AIProfile.asset"));

            Material cloth = LitMaterial("M_Cloth", new Color(0.06f, 0.36f, 0.20f), 0.08f, 0f);
            Material cushion = LitMaterial("M_Cushion", new Color(0.05f, 0.30f, 0.17f), 0.12f, 0f);
            Material rail = LitMaterial("M_RailWood", new Color(0.30f, 0.15f, 0.07f), 0.55f, 0f);
            Material pocket = LitMaterial("M_Pocket", new Color(0.02f, 0.02f, 0.02f), 0.05f, 0f);
            LitMaterial("M_Floor", new Color(0.12f, 0.11f, 0.10f), 0.25f, 0f);
            LitMaterial("M_CueShaft", new Color(0.86f, 0.74f, 0.55f), 0.6f, 0f);
            LitMaterial("M_CueButt", new Color(0.12f, 0.06f, 0.03f), 0.7f, 0f);
            LitMaterial("M_CueTip", new Color(0.15f, 0.35f, 0.75f), 0.1f, 0f);
            LitMaterial("M_CueFerrule", new Color(0.95f, 0.95f, 0.92f), 0.6f, 0f);
            TransparentMaterial("M_GhostBall", new Color(1f, 1f, 1f, 0.3f));
            LineMaterial("M_DebugLine");

            TableGeometry geometry = LoadOrCreate<TableGeometry>(ProfilesFolder + "/TableGeometry_9ft.asset");
            geometry.clothPhysicsMaterial = clothPm;
            geometry.cushionPhysicsMaterial = cushionPm;
            geometry.clothMaterial = cloth;
            geometry.cushionMaterial = cushion;
            geometry.railMaterial = rail;
            geometry.pocketMaterial = pocket;
            EditorUtility.SetDirty(geometry);

            EnsureFolder(RulesFolder);
            EditorUtility.SetDirty(LoadOrCreate<EightBallRulesConfig>(RulesFolder + "/EightBallRules.asset"));
            EditorUtility.SetDirty(LoadOrCreate<NineBallRulesConfig>(RulesFolder + "/NineBallRules.asset"));

            PhysicsTestPresetLibrary library = LoadOrCreate<PhysicsTestPresetLibrary>(ProfilesFolder + "/PhysicsTestPresets.asset");
            // Presets are code-defined test fixtures: always refresh them from PhysicsTestPresetLibrary.CreateDefaults.
            library.presets = PhysicsTestPresetLibrary.CreateDefaults();
            EditorUtility.SetDirty(library);

            for (int number = 0; number <= 15; number++)
            {
                CreateBallAssets(number);
            }

            CreateBallPrefab(profile);
            CreateCuePrefab();
            PresentationSetup.CreateAssets(geometry);
            PresentationSetup.CreateCuePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VTG Setup] Assets created/updated.");
        }

        private static void CreateBallAssets(int number)
        {
            string id = number.ToString("00");
            Color color = BallDefinition.StandardColor(number);
            string texturePath = $"{BallTexturesFolder}/T_Ball_{id}.png";
            if (!File.Exists(texturePath))
            {
                Texture2D generated = BallTextureGenerator.Generate(number, color);
                File.WriteAllBytes(texturePath, generated.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(generated);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            string materialPath = $"{BallMaterialsFolder}/M_Ball_{id}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Complex Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.9f);
            material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_ClearCoatMask"))
            {
                material.SetFloat("_ClearCoat", 1f);
                material.SetFloat("_ClearCoatMask", 0.6f);
                material.SetFloat("_ClearCoatSmoothness", 0.95f);
                material.EnableKeyword("_CLEARCOAT");
            }

            EditorUtility.SetDirty(material);

            string definitionPath = $"{BallDefinitionsFolder}/Ball_{id}.asset";
            BallDefinition definition = LoadOrCreate<BallDefinition>(definitionPath);
            definition.Initialize(number, color, material);
            EditorUtility.SetDirty(definition);
        }

        private static void CreateBallPrefab(BallPhysicsProfile profile)
        {
            string path = Root + "/Prefabs/Balls/PoolBall.prefab";
            var root = new GameObject("PoolBall");
            var body = root.AddComponent<Rigidbody>();
            body.mass = profile.ballMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = profile.ballRadius;
            sphere.sharedMaterial = profile.ballMaterial;
            PoolBall ball = root.AddComponent<PoolBall>();
            ball.SetDefinition(null, profile);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * (profile.ballRadius * 2f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateCuePrefab()
        {
            string path = Root + "/Prefabs/Cue/Cue.prefab";
            var root = new GameObject("CueVisual");
            // Tip at the origin, cue extends along -Z. Lengths in metres (1.47 m cue).
            AddCueSegment(root.transform, "Tip", 0.0f, 0.012f, 0.0130f, "M_CueTip");
            AddCueSegment(root.transform, "Ferrule", 0.012f, 0.025f, 0.0132f, "M_CueFerrule");
            AddCueSegment(root.transform, "Shaft", 0.037f, 0.70f, 0.0150f, "M_CueShaft");
            AddCueSegment(root.transform, "Butt", 0.737f, 0.733f, 0.0280f, "M_CueButt");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void AddCueSegment(Transform parent, string name, float start, float length, float diameter, string materialName)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = name;
            UnityEngine.Object.DestroyImmediate(segment.GetComponent<Collider>());
            segment.transform.SetParent(parent, false);
            segment.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            segment.transform.localPosition = new Vector3(0f, 0f, -(start + length * 0.5f));
            segment.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            segment.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{materialName}.mat");
            segment.layer = LayerMask.NameToLayer(PoolLayers.CueName);
        }

        // ---------------------------------------------------------------- scene

        public static void CreatePhysicsTestScene(bool openAfterwards) => CreateScene(SceneKind.PhysicsTest, openAfterwards);

        public static void CreateMatchScene(bool openAfterwards) => CreateScene(SceneKind.Match, openAfterwards);

        private static void CreateScene(SceneKind kind, bool openAfterwards)
        {
            if (SceneManager.GetActiveScene().isDirty && !Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[VTG Setup] Scene creation cancelled: current scene has unsaved changes.");
                    return;
                }
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BallPhysicsProfile profile = AssetDatabase.LoadAssetAtPath<BallPhysicsProfile>(ProfilesFolder + "/BallPhysicsProfile.asset");
            CueStrikeProfile strike = AssetDatabase.LoadAssetAtPath<CueStrikeProfile>(ProfilesFolder + "/CueStrikeProfile.asset");
            TableGeometry geometry = AssetDatabase.LoadAssetAtPath<TableGeometry>(ProfilesFolder + "/TableGeometry_9ft.asset");
            PhysicsTestPresetLibrary library = AssetDatabase.LoadAssetAtPath<PhysicsTestPresetLibrary>(ProfilesFolder + "/PhysicsTestPresets.asset");
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            Material lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_DebugLine.mat");
            Material ghostMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_GhostBall.mat");
            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Balls/PoolBall.prefab");
            GameObject cuePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Cue/Cue.prefab");
            if (inputAsset == null)
            {
                Debug.LogError("[VTG Setup] PoolInput.inputactions could not be loaded.");
            }

            // Lighting ----------------------------------------------------------
            Transform lighting = new GameObject("Lighting").transform;
            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.transform.SetParent(lighting);
            sun.type = LightType.Directional;
            sun.intensity = 0.6f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(60f, -35f, 0f);

            float surface = geometry.surfaceHeight;
            for (int i = 0; i < 2; i++)
            {
                var lamp = new GameObject($"Table Lamp {i}").AddComponent<Light>();
                lamp.transform.SetParent(lighting);
                lamp.type = LightType.Spot;
                lamp.intensity = 4f;
                lamp.range = 3f;
                lamp.spotAngle = 110f;
                lamp.innerSpotAngle = 70f;
                lamp.shadows = LightShadows.Soft;
                lamp.color = new Color(1f, 0.95f, 0.85f);
                lamp.transform.SetPositionAndRotation(new Vector3(0f, surface + 1.1f, (i == 0 ? -1f : 1f) * 0.6f), Quaternion.Euler(90f, 0f, 0f));
            }

            var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(lighting);
            probe.transform.position = new Vector3(0f, surface + 0.5f, 0f);
            probe.size = new Vector3(5f, 2.5f, 6f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.24f);

            // Environment -------------------------------------------------------
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.layer = LayerMask.NameToLayer(PoolLayers.EnvironmentName);
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Floor.mat");

            // Table -------------------------------------------------------------
            var tableObject = new GameObject("Table");
            PocketManager pocketManager = tableObject.AddComponent<PocketManager>();
            TableBuilder table = tableObject.AddComponent<TableBuilder>();
            table.SetGeometry(geometry);
            SetField(table, "pocketManager", pocketManager);
            table.Build();

            // Balls -------------------------------------------------------------
            Transform ballsRoot = new GameObject("Balls").transform;
            var balls = new List<PoolBall>(16);
            var rack = new Vector3[15];
            RackLayout.GetTrianglePositions(table.FootSpot + Vector3.up * profile.ballRadius, profile.ballRadius, 0.0002f, rack);
            for (int number = 0; number <= 15; number++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, scene);
                instance.name = $"Ball_{number:00}";
                instance.transform.SetParent(ballsRoot, false);
                BallDefinition definition = AssetDatabase.LoadAssetAtPath<BallDefinition>($"{BallDefinitionsFolder}/Ball_{number:00}.asset");
                PoolBall ball = instance.GetComponent<PoolBall>();
                ball.SetDefinition(definition, profile);
                instance.layer = LayerMask.NameToLayer(number == 0 ? PoolLayers.CueBallName : PoolLayers.BallName);
                instance.GetComponentInChildren<MeshRenderer>().sharedMaterial = definition.Material;
                Vector3 position = number == 0
                    ? table.HeadSpot + Vector3.up * profile.ballRadius
                    : rack[Array.IndexOf(RackLayout.EightBallOrder, number)];
                instance.transform.position = position;
                EditorUtility.SetDirty(ball);
                balls.Add(ball);
            }

            // Cue ---------------------------------------------------------------
            var cueRoot = new GameObject("Cue");
            CueController cueController = cueRoot.AddComponent<CueController>();
            var cueVisual = (GameObject)PrefabUtility.InstantiatePrefab(cuePrefab, scene);
            cueVisual.transform.SetParent(cueRoot.transform, false);
            SetField(cueController, "cueVisual", cueVisual.transform);

            // Systems -----------------------------------------------------------
            Transform systems = new GameObject("Systems").transform;
            PoolPhysicsSystem physicsSystem = AddSystem<PoolPhysicsSystem>(systems, "PoolPhysicsSystem");
            physicsSystem.Configure(profile, pocketManager, table.SurfaceHeight);

            PoolInputReader inputReader = AddSystem<PoolInputReader>(systems, "InputManager");
            inputReader.SetActions(inputAsset);

            AimSystem aimSystem = AddSystem<AimSystem>(systems, "AimSystem");
            SetField(aimSystem, "inputSource", inputReader);
            SetField(aimSystem, "lineMaterial", lineMaterial);
            SetField(aimSystem, "ghostMaterial", ghostMaterial);

            CueBallSpinController spin = AddSystem<CueBallSpinController>(systems, "CueBallSpinController");
            SetField(spin, "inputSource", inputReader);

            ShotTracker tracker = AddSystem<ShotTracker>(systems, "ShotTracker");

            ShotController shotController = AddSystem<ShotController>(systems, "ShotController");
            SetField(shotController, "physicsSystem", physicsSystem);
            SetField(shotController, "aimSystem", aimSystem);
            SetField(shotController, "spinController", spin);
            SetField(shotController, "cueController", cueController);
            SetField(shotController, "strikeProfile", strike);
            SetField(shotController, "table", table);
            SetField(shotController, "cueBall", balls[0]);
            SetField(shotController, "inputSource", inputReader);

            if (kind == SceneKind.PhysicsTest)
            {
                PhysicsTestController testController = AddSystem<PhysicsTestController>(systems, "PhysicsTestController");
                SetField(testController, "library", library);
                SetField(testController, "table", table);
                SetField(testController, "shotController", shotController);
                SetField(testController, "aimSystem", aimSystem);
                SetField(testController, "spinController", spin);
                SetList(testController, "balls", balls);
            }

            // Cameras -----------------------------------------------------------
            Transform cameras = new GameObject("Cameras").transform;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameras);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 45f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, surface + 0.6f, -2.2f), Quaternion.Euler(15f, 0f, 0f));

            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(cameras);
            CameraController cameraController = rig.AddComponent<CameraController>();
            SetField(cameraController, "inputSource", inputReader);
            SetField(cameraController, "aimSystem", aimSystem);
            SetField(cameraController, "table", table);
            Type cinemachineOutput = Type.GetType("VTG.Pool.CameraSystem.CinemachineCameraOutput, VTG.Pool.Cinemachine");
            Component output = cinemachineOutput != null ? rig.AddComponent(cinemachineOutput) : rig.AddComponent<DirectCameraOutput>();
            SetField(output, "targetCamera", camera);
            SetField(cameraController, "outputBehaviour", output);
            Debug.Log($"[VTG Setup] Camera output: {output.GetType().Name}");

            // Debug -------------------------------------------------------------
            var debugRoot = new GameObject("Debug");
            PoolDebugOverlay overlay = debugRoot.AddComponent<PoolDebugOverlay>();
            SetField(overlay, "shotController", shotController);
            SetField(overlay, "spinController", spin);
            SetField(overlay, "physicsSystem", physicsSystem);
            SetField(overlay, "shotTracker", tracker);
            BallDebugDrawer drawer = debugRoot.AddComponent<BallDebugDrawer>();
            SetField(drawer, "lineMaterial", lineMaterial);

            if (kind == SceneKind.Match)
            {
                SetBool(overlay, "visible", false);
                SetBool(drawer, "visible", false); // F2 shows the physics debug lines in a match
                SetBool(shotController, "autoRespawnCueBall", false);

                CueBallPlacementController placement = AddSystem<CueBallPlacementController>(systems, "CueBallPlacement");
                SetField(placement, "shotController", shotController);
                SetField(placement, "table", table);
                SetField(placement, "inputSource", inputReader);
                SetField(placement, "viewCamera", camera);

                RackManager rackManager = AddSystem<RackManager>(systems, "RackManager");
                SetField(rackManager, "table", table);
                SetList(rackManager, "balls", balls);

                TurnManager turnManager = AddSystem<TurnManager>(systems, "TurnManager");
                SetField(turnManager, "shotController", shotController);
                SetField(turnManager, "placement", placement);
                SetField(turnManager, "rackManager", rackManager);

                MatchManager matchManager = AddSystem<MatchManager>(systems, "MatchManager");
                SetField(matchManager, "eightBallRules", AssetDatabase.LoadAssetAtPath<EightBallRulesConfig>(RulesFolder + "/EightBallRules.asset"));
                SetField(matchManager, "nineBallRules", AssetDatabase.LoadAssetAtPath<NineBallRulesConfig>(RulesFolder + "/NineBallRules.asset"));
                SetField(matchManager, "turnManager", turnManager);
                SetField(matchManager, "shotController", shotController);
                SetField(matchManager, "aiProfile", LoadOrCreate<VTG.Pool.AI.AIProfile>(AIProfilesFolder + "/AIProfile.asset"));

                PracticeSession practice = AddSystem<PracticeSession>(systems, "PracticeSession");
                SetField(practice, "matchManager", matchManager);
                SetField(practice, "turnManager", turnManager);
                SetField(practice, "rackManager", rackManager);
                SetField(practice, "shotController", shotController);

                PracticeLayoutEditor layoutEditor = AddSystem<PracticeLayoutEditor>(systems, "PracticeLayoutEditor");
                SetField(layoutEditor, "session", practice);
                SetField(layoutEditor, "shotController", shotController);
                SetField(layoutEditor, "table", table);
                SetField(layoutEditor, "cameraController", cameraController);
                SetField(layoutEditor, "inputSource", inputReader);
                SetField(layoutEditor, "viewCamera", camera);

                var ui = new GameObject("UI");
                PoolHud hud = ui.AddComponent<PoolHud>();
                SetField(hud, "aimSystem", aimSystem);
                SetField(hud, "practiceSession", practice);
                SetField(hud, "practiceEditor", layoutEditor);
                SetField(hud, "matchManager", matchManager);
                SetField(hud, "shotController", shotController);
                SetField(hud, "spinController", spin);
                SetField(hud, "placement", placement);
                SetField(hud, "cameraController", cameraController);
                SetField(hud, "inputSource", inputReader);
            }

            PresentationSetup.DecorateActiveScene(kind == SceneKind.Match);

            string scenePath = kind == SceneKind.Match ? MatchScenePath : PhysicsTestScenePath;
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);
            Debug.Log($"[VTG Setup] Scene saved: {scenePath}");

            if (openAfterwards && !Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static T AddSystem<T>(Transform parent, string name) where T : Component
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            return gameObject.AddComponent<T>();
        }

        private static void SetField(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[VTG Setup] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[VTG Setup] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetList<T>(UnityEngine.Object target, string field, List<T> values) where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
            {
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static PhysicsMaterial PhysicsMaterialAsset(string name, float friction, float bounciness, PhysicsMaterialCombine frictionCombine, PhysicsMaterialCombine bounceCombine)
        {
            string path = $"{PhysicsMaterialsFolder}/{name}.asset";
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = friction;
            material.staticFriction = friction;
            material.bounciness = bounciness;
            material.frictionCombine = frictionCombine;
            material.bounceCombine = bounceCombine;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LitMaterial(string name, Color color, float smoothness, float metallic)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TransparentMaterial(string name, Color color)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.9f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LineMaterial(string name)
        {
            // Sprites/Default is unlit, transparent and uses vertex colours (LineRenderer colours).
            Material material = LoadOrCreateMaterial(name, "Sprites/Default");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[VTG Setup] Shader '{shaderName}' not found.");
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
