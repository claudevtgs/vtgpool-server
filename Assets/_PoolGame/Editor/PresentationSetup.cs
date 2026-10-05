using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VTG.Pool.Audio;
using VTG.Pool.Match;
using VTG.Pool.Presentation;
using VTG.Pool.Save;
using VTG.Pool.Simulation;
using VTG.Pool.Table;
using VTG.Pool.UI;
using VTG.Pool.VFX;

namespace VTG.Pool.EditorTools
{
    /// <summary>
    /// Milestone 5 presentation pipeline: turns Unity-AI generated textures into tileable PBR materials,
    /// builds the audio library, the turned cue mesh/prefab, and decorates generated scenes with the lounge
    /// lighting rig, panoramic skybox, reflection probe, post-processing, rug, table dressing and the
    /// audio/VFX/callout/graphics-quality systems. Called from <see cref="PoolProjectSetup"/>.
    /// </summary>
    public static class PresentationSetup
    {
        private const string Root = PoolProjectSetup.Root;
        private const string GeneratedTextures = Root + "/Art/Textures/Generated";
        private const string ProcessedTextures = Root + "/Art/Textures/Processed";
        private const string Materials = Root + "/Art/Materials/Lounge";
        private const string Environment = Root + "/Art/Environment";

        private static readonly Color ClothTint = new Color(0.42f, 0.6f, 1f);

        // ------------------------------------------------------------------ assets

        /// <summary>Creates/updates presentation assets and assigns table materials to the geometry asset.</summary>
        public static void CreateAssets(TableGeometry geometry)
        {
            EnsureFolder(ProcessedTextures);
            EnsureFolder(Materials);
            EnsureFolder(Environment);
            EnsureFolder(Root + "/Art/Cue");
            EnsureFolder(Root + "/ScriptableObjects/Audio");

            Material cloth = PbrMaterial("M_Lounge_Cloth", "T_AI_FeltFlux", ClothTint, 0.1f, 0f, Vector2.one, 0.25f, 0.35f);
            Material wood = PbrMaterial("M_Lounge_Walnut", "T_AI_WoodWalnut", Color.white, 0.62f, 0f, Vector2.one, 0.5f);
            Material leather = PbrMaterial("M_Lounge_Leather", "T_AI_LeatherBlack", Color.white, 0.35f, 0f, new Vector2(2f, 2f), 0.8f);
            PbrMaterial("M_Lounge_Rug", "T_AI_Carpet", new Color(0.75f, 0.75f, 0.8f), 0.08f, 0f, new Vector2(5f, 7f), 0.7f);
            PbrMaterial("M_Lounge_Floor", "T_AI_WoodWalnut", new Color(0.45f, 0.38f, 0.33f), 0.45f, 0f, new Vector2(12f, 12f), 0.4f);
            PbrMaterial("M_Cue_Maple", "T_AI_Maple", Color.white, 0.8f, 0f, new Vector2(1f, 1f), 0.15f);
            PbrMaterial("M_Cue_Ebony", "T_AI_Ebony", Color.white, 0.85f, 0f, new Vector2(1f, 1f), 0.2f);
            PbrMaterial("M_Cue_Wrap", "T_AI_WrapLinen", Color.white, 0.25f, 0f, new Vector2(3f, 6f), 0.8f);
            SimpleMaterial("M_Lounge_Chrome", new Color(0.55f, 0.56f, 0.6f), 0.86f, 1f);
            SimpleMaterial("M_Lounge_Pearl", new Color(0.93f, 0.92f, 0.88f), 0.92f, 0f);
            SimpleMaterial("M_Cue_Tip", new Color(0.12f, 0.3f, 0.62f), 0.15f, 0f);
            SimpleMaterial("M_Cue_Ferrule", new Color(0.95f, 0.93f, 0.86f), 0.75f, 0f);
            SimpleMaterial("M_Cue_Joint", new Color(0.8f, 0.78f, 0.72f), 0.9f, 1f);
            SimpleMaterial("M_Cue_Rubber", new Color(0.03f, 0.03f, 0.03f), 0.3f, 0f);
            SimpleMaterial("M_Lamp_Housing", new Color(0.05f, 0.05f, 0.06f), 0.6f, 0.8f);
            EmissiveMaterial("M_Lamp_Glow", new Color(1f, 0.92f, 0.78f), 2.2f);
            ParticleMaterial("M_FX_Particle");
            SkyboxMaterial();
            PostProcessProfile();
            CreateAudioLibrary();

            geometry.clothMaterial = cloth;
            geometry.cushionMaterial = cloth;
            geometry.railMaterial = wood;
            geometry.pocketMaterial = leather;
            EditorUtility.SetDirty(geometry);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Replaces the cue prefab with the turned multi-material cue.</summary>
        public static void CreateCuePrefab()
        {
            string meshPath = Root + "/Art/Cue/CueMesh.asset";
            Mesh mesh = ProceduralMeshes.CreateCue();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            var root = new GameObject("CueVisual");
            var body = new GameObject("Cue");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                Load("M_Cue_Tip"), Load("M_Cue_Ferrule"), Load("M_Cue_Maple"), Load("M_Cue_Joint"),
                Load("M_Cue_Ebony"), Load("M_Cue_Wrap"), Load("M_Cue_Ebony"), Load("M_Cue_Rubber")
            };
            body.layer = LayerMask.NameToLayer(Core.PoolLayers.CueName);
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Cue/Cue.prefab");
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ scene

        /// <summary>Adds the presentation layer to the scene currently being generated.</summary>
        public static void DecorateActiveScene(bool isMatch)
        {
            TableBuilder table = Object.FindAnyObjectByType<TableBuilder>();
            Camera camera = Camera.main;
            PoolPhysicsSystem physics = Object.FindAnyObjectByType<PoolPhysicsSystem>();
            if (table == null || camera == null)
            {
                Debug.LogError("[VTG Presentation] Table or camera missing; scene not decorated.");
                return;
            }

            float surface = table.Geometry.surfaceHeight;

            // Table dressing (visual only).
            TableDressing dressing = table.gameObject.AddComponent<TableDressing>();
            dressing.Configure(table, Load("M_Lounge_Cloth"), Load("M_Lounge_Walnut"), Load("M_Lounge_Leather"), Load("M_Lounge_Pearl"), Load("M_Lounge_Walnut"));
            dressing.Build();

            Light[] lamps = BuildEnvironment(surface, camera, out Volume volume);

            // Systems.
            GameObject systemsObject = GameObject.Find("Systems");
            Transform systems = systemsObject != null ? systemsObject.transform : null;
            AudioManager audio = AddComponent<AudioManager>(systems, "AudioManager");
            SetField(audio, "library", AssetDatabase.LoadAssetAtPath<AudioLibrary>(Root + "/ScriptableObjects/Audio/AudioLibrary.asset"));
            SetField(audio, "physicsSystem", physics);

            VFXManager vfx = AddComponent<VFXManager>(systems, "VFXManager");
            SetField(vfx, "particleMaterial", Load("M_FX_Particle"));

            GraphicsQualityManager quality = AddComponent<GraphicsQualityManager>(systems, "GraphicsQuality");
            quality.Configure(camera, volume, vfx, lamps);
            EditorUtility.SetDirty(quality);

            SettingsBinder settingsBinder = AddComponent<SettingsBinder>(systems, "SettingsBinder");
            settingsBinder.Configure(audio, quality, Object.FindAnyObjectByType<CameraSystem.CameraController>(), Object.FindAnyObjectByType<Aiming.AimSystem>(),
                Object.FindAnyObjectByType<Inputs.PoolInputReader>(), Object.FindAnyObjectByType<MatchManager>());
            EditorUtility.SetDirty(settingsBinder);

            if (isMatch)
            {
                TurnManager turns = Object.FindAnyObjectByType<TurnManager>();
                StatsTracker stats = AddComponent<StatsTracker>(systems, "StatsTracker");
                stats.Configure(turns);
                EditorUtility.SetDirty(stats);
                ShotCallouts callouts = AddComponent<ShotCallouts>(systems, "ShotCallouts");
                SetField(callouts, "turnManager", turns);

                PoolHud hud = Object.FindAnyObjectByType<PoolHud>();
                if (hud != null)
                {
                    SetField(hud, "callouts", callouts);
                    SetField(hud, "graphicsQuality", quality);
                    SetField(hud, "audioManager", audio);
                }
            }

            Debug.Log($"[VTG Presentation] Scene decorated (match={isMatch}).");
        }

        /// <summary>Lounge environment: lamp rig, fill light, reflection probe, skybox, floor/rug and post-processing.</summary>
        public static Light[] BuildEnvironment(float surface, Camera camera, out Volume volume)
        {
            // Environment: replace the placeholder lighting.
            GameObject oldLighting = GameObject.Find("Lighting");
            if (oldLighting != null)
            {
                Object.DestroyImmediate(oldLighting);
            }

            Transform lighting = new GameObject("Lighting").transform;
            Light[] lamps = BuildLampRig(lighting, surface);

            var fill = new GameObject("Room Fill").AddComponent<Light>();
            fill.transform.SetParent(lighting);
            fill.type = LightType.Directional;
            fill.intensity = 0.12f;
            fill.color = new Color(0.75f, 0.8f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(55f, 140f, 0f);

            var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(lighting);
            probe.transform.position = new Vector3(0f, surface + 0.35f, 0f);
            probe.size = new Vector3(6f, 3f, 7f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.intensity = 1f;

            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Environment + "/M_Sky_Lounge.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.75f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            RenderSettings.fog = false;

            GameObject floor = GameObject.Find("Floor");
            if (floor != null)
            {
                floor.GetComponent<MeshRenderer>().sharedMaterial = Load("M_Lounge_Floor");
            }

            GameObject rug = GameObject.CreatePrimitive(PrimitiveType.Plane);
            rug.name = "Rug";
            Object.DestroyImmediate(rug.GetComponent<Collider>());
            rug.transform.position = new Vector3(0f, 0.004f, 0f);
            rug.transform.localScale = new Vector3(0.42f, 1f, 0.56f);
            rug.GetComponent<MeshRenderer>().sharedMaterial = Load("M_Lounge_Rug");
            rug.isStatic = true;

            // Post-processing.
            var volumeObject = new GameObject("Post Processing");
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Environment + "/PP_Lounge.asset");
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            camera.allowHDR = true;

            return lamps;
        }

        /// <summary>Main-menu decoration: table dressing, environment, audio, graphics quality and settings binding.</summary>
        public static void DecorateMenuScene(TableBuilder table, Camera camera, Transform systems, out AudioManager audio)
        {
            TableDressing dressing = table.gameObject.AddComponent<TableDressing>();
            dressing.Configure(table, Load("M_Lounge_Cloth"), Load("M_Lounge_Walnut"), Load("M_Lounge_Leather"), Load("M_Lounge_Pearl"), Load("M_Lounge_Walnut"));
            dressing.Build();
            Light[] lamps = BuildEnvironment(table.Geometry.surfaceHeight, camera, out Volume volume);

            audio = AddComponent<AudioManager>(systems, "AudioManager");
            SetField(audio, "library", AssetDatabase.LoadAssetAtPath<AudioLibrary>(Root + "/ScriptableObjects/Audio/AudioLibrary.asset"));
            GraphicsQualityManager quality = AddComponent<GraphicsQualityManager>(systems, "GraphicsQuality");
            quality.Configure(camera, volume, null, lamps);
            EditorUtility.SetDirty(quality);
            SettingsBinder binder = AddComponent<SettingsBinder>(systems, "SettingsBinder");
            binder.Configure(audio, quality, null, null, null, null);
            EditorUtility.SetDirty(binder);
        }

        private static Light[] BuildLampRig(Transform parent, float surface)
        {
            float height = surface + 1.05f;
            var lamp = new GameObject("Table Lamp").transform;
            lamp.SetParent(parent);
            lamp.position = new Vector3(0f, height, 0f);

            // Single-sided, downward-facing panels: visible from the player's cameras below, back-face culled
            // (invisible) from the overhead Top camera so the lamp never hides the table.
            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Quad);
            housing.name = "Housing";
            Object.DestroyImmediate(housing.GetComponent<Collider>());
            housing.transform.SetParent(lamp, false);
            housing.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            housing.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            housing.transform.localScale = new Vector3(0.42f, 2.1f, 1f);
            housing.GetComponent<MeshRenderer>().sharedMaterial = Load("M_Lamp_Housing");
            housing.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var lights = new List<Light>(3);
            float[] offsets = { 0f, -0.7f, 0.7f };
            foreach (float z in offsets)
            {
                GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glow.name = "Glow";
                Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.transform.SetParent(lamp, false);
                glow.transform.localPosition = new Vector3(0f, -0.034f, z);
                glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                glow.transform.localScale = new Vector3(0.36f, 0.5f, 1f);
                MeshRenderer glowRenderer = glow.GetComponent<MeshRenderer>();
                glowRenderer.sharedMaterial = Load("M_Lamp_Glow");
                glowRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var spot = new GameObject($"Spot {z:+0.0;-0.0;0}").AddComponent<Light>();
                spot.transform.SetParent(lamp, false);
                spot.transform.localPosition = new Vector3(0f, -0.05f, z);
                spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                spot.type = LightType.Spot;
                spot.spotAngle = 105f;
                spot.innerSpotAngle = 65f;
                spot.range = 3.2f;
                spot.intensity = 3.2f;
                spot.color = new Color(1f, 0.94f, 0.84f);
                spot.shadows = LightShadows.Soft;
                spot.shadowStrength = 0.85f;
                spot.shadowBias = 0.02f;
                spot.shadowNormalBias = 0.2f;
                lights.Add(spot);
            }

            return lights.ToArray();
        }

        // ------------------------------------------------------------------ materials

        private static Material PbrMaterial(string name, string textureName, Color tint, float smoothness, float metallic, Vector2 tiling, float normalStrength, float contrast = 1f)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            string source = $"{GeneratedTextures}/{textureName}.png";
            if (File.Exists(source))
            {
                ProcessTexture(source, textureName, normalStrength, contrast, out Texture2D albedo, out Texture2D normal);
                material.SetTexture("_BaseMap", albedo);
                material.SetTextureScale("_BaseMap", tiling);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", 1f);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
            else
            {
                Debug.LogWarning($"[VTG Presentation] Texture {source} missing; {name} uses a flat colour.");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SimpleMaterial(string name, Color color, float smoothness, float metallic)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EmissiveMaterial(string name, Color color, float intensity)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * intensity);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
        }

        private static void ParticleMaterial(string name)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetTexture("_BaseMap", SoftDot());
            EditorUtility.SetDirty(material);
        }

        private static Texture2D SoftDot()
        {
            string path = ProcessedTextures + "/T_FX_SoftDot.png";
            if (!File.Exists(path))
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }

                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SkyboxMaterial()
        {
            string panoramaPath = Environment + "/Generated/Sky_AI_Lounge.png";
            var importer = AssetImporter.GetAtPath(panoramaPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            string path = Environment + "/M_Sky_Lounge.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(panoramaPath));
            material.SetFloat("_Exposure", 0.85f);
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(material);
        }

        private static void PostProcessProfile()
        {
            string path = Environment + "/PP_Lounge.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile, path);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = GetOrAdd<Bloom>(profile, path);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.28f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);

            Vignette vignette = GetOrAdd<Vignette>(profile, path);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile, path);
            color.postExposure.Override(0.25f);
            color.contrast.Override(10f);
            color.saturation.Override(6f);

            EditorUtility.SetDirty(profile);
        }

        private static T GetOrAdd<T>(VolumeProfile profile, string path) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, path);
            }

            component.active = true;
            return component;
        }

        private static void CreateAudioLibrary()
        {
            string path = Root + "/ScriptableObjects/Audio/AudioLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }

            string audio = Root + "/Audio";
            library.ballClicks = Clips($"{audio}/BallHits/SFX_AI_BallClick_02", $"{audio}/BallHits/SFX_AI_BallClick_01", $"{audio}/BallHits/SFX_AI_BallClick_03");
            library.breakClatter = Clip($"{audio}/BallHits/SFX_AI_Break");
            library.rollLoop = Clip($"{audio}/BallHits/SFX_AI_RollLoop");
            library.cushionHits = Clips($"{audio}/CushionHits/SFX_AI_Cushion_01", $"{audio}/CushionHits/SFX_AI_Cushion_02");
            library.pocketDrops = Clips($"{audio}/Pocket/SFX_AI_PocketDrop_01", $"{audio}/Pocket/SFX_AI_PocketDrop_02");
            library.cueStrikeSoft = Clip($"{audio}/Cue/SFX_AI_CueStrike_Soft");
            library.cueStrikeHard = Clip($"{audio}/Cue/SFX_AI_CueStrike_Hard");
            library.chalk = Clip($"{audio}/Cue/SFX_AI_Chalk");
            library.uiClick = Clip($"{audio}/UI/SFX_AI_UIClick");
            library.foul = Clip($"{audio}/UI/SFX_AI_Foul");
            library.win = Clip($"{audio}/UI/SFX_AI_Win");
            library.callout = Clip($"{audio}/UI/SFX_AI_Callout");
            library.ambience = Clip($"{audio}/SFX_AI_LoungeAmbience");
            library.music = Clip($"{audio}/Music_AI_LoungeJazz");
            EditorUtility.SetDirty(library);

            // Short effects: decompress on load for zero-latency playback; long loops stream.
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { audio }))
            {
                string clipPath = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(clipPath) as AudioImporter;
                if (importer == null)
                {
                    continue;
                }

                bool longClip = clipPath.Contains("Music") || clipPath.Contains("Ambience") || clipPath.Contains("RollLoop");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = longClip ? 0.6f : 0.85f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !clipPath.Contains("Music");
                importer.SaveAndReimport();
            }
        }

        private static AudioClip[] Clips(params string[] stems)
        {
            var clips = new List<AudioClip>(stems.Length);
            foreach (string stem in stems)
            {
                AudioClip clip = Clip(stem);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return clips.ToArray();
        }

        private static AudioClip Clip(string stem)
        {
            foreach (string extension in new[] { ".wav", ".mp3", ".ogg" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(stem + extension);
                if (clip != null)
                {
                    return clip;
                }
            }

            Debug.LogWarning($"[VTG Presentation] Audio clip missing: {stem}");
            return null;
        }

        // ------------------------------------------------------------------ texture processing

        /// <summary>
        /// Makes a generated texture tileable (half-offset copy cross-faded over the borders) and derives a
        /// tangent-space normal map from its luminance. Results are cached in Art/Textures/Processed.
        /// </summary>
        private static void ProcessTexture(string sourcePath, string baseName, float normalStrength, float contrast, out Texture2D albedo, out Texture2D normal)
        {
            string suffix = contrast < 0.999f ? $"_c{Mathf.RoundToInt(contrast * 100f)}" : string.Empty;
            string albedoPath = $"{ProcessedTextures}/{baseName}{suffix}_Albedo.png";
            string normalPath = $"{ProcessedTextures}/{baseName}{suffix}_Normal.png";
            if (!File.Exists(albedoPath) || !File.Exists(normalPath))
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(sourcePath));
                int width = source.width;
                int height = source.height;
                Color[] pixels = source.GetPixels();
                Object.DestroyImmediate(source);

                var tiled = new Color[pixels.Length];
                const float border = 0.22f;
                for (int y = 0; y < height; y++)
                {
                    float fy = Mathf.Min(y, height - 1 - y) / (float)height;
                    for (int x = 0; x < width; x++)
                    {
                        float fx = Mathf.Min(x, width - 1 - x) / (float)width;
                        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((border - Mathf.Min(fx, fy)) / border));
                        Color original = pixels[y * width + x];
                        Color shifted = pixels[((y + height / 2) % height) * width + (x + width / 2) % width];
                        tiled[y * width + x] = Color.Lerp(original, shifted, weight);
                    }
                }

                if (contrast < 0.999f)
                {
                    // Pull every texel toward the mean colour: keeps micro detail, removes blotchy streaks.
                    Color mean = Color.black;
                    for (int i = 0; i < tiled.Length; i++)
                    {
                        mean += tiled[i];
                    }

                    mean /= tiled.Length;
                    for (int i = 0; i < tiled.Length; i++)
                    {
                        tiled[i] = Color.Lerp(mean, tiled[i], contrast);
                    }
                }

                var normalPixels = new Color[pixels.Length];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float left = Luminance(tiled, width, height, x - 1, y);
                        float right = Luminance(tiled, width, height, x + 1, y);
                        float down = Luminance(tiled, width, height, x, y - 1);
                        float up = Luminance(tiled, width, height, x, y + 1);
                        var n = new Vector3((left - right) * normalStrength * 4f, (down - up) * normalStrength * 4f, 1f).normalized;
                        normalPixels[y * width + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
                }

                SavePng(albedoPath, tiled, width, height);
                SavePng(normalPath, normalPixels, width, height);
                ConfigureImporter(albedoPath, false);
                ConfigureImporter(normalPath, true);
            }

            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        }

        private static float Luminance(Color[] pixels, int width, int height, int x, int y)
        {
            x = (x % width + width) % width;
            y = (y % height + height) % height;
            Color c = pixels[y * width + x];
            return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        }

        private static void SavePng(string path, Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        private static void ConfigureImporter(string path, bool isNormal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ helpers

        private static T AddComponent<T>(Transform parent, string name) where T : Component
        {
            var gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent);
            }

            return gameObject.AddComponent<T>();
        }

        private static void SetField(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[VTG Presentation] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{name}.mat");
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{Materials}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
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
