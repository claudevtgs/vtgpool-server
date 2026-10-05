using UnityEngine;
using VTG.Pool.Aiming;
using VTG.Pool.Audio;
using VTG.Pool.CameraSystem;
using VTG.Pool.Inputs;
using VTG.Pool.Match;
using VTG.Pool.Presentation;

namespace VTG.Pool.Save
{
    /// <summary>
    /// Applies <see cref="GameSettings"/> to the systems of the current scene at start-up and whenever the
    /// settings change (audio mix, graphics tier, default camera, aim assist, aim sensitivity, player names).
    /// Any reference may be left empty (e.g. the main menu has no aim system).
    /// </summary>
    public sealed class SettingsBinder : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private GraphicsQualityManager graphicsQuality;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private AimSystem aimSystem;
        [SerializeField] private PoolInputReader inputReader;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private Cue.CueBallPlacementController placement;
        [SerializeField] private Cue.ShotController shotController;

        public void Configure(AudioManager audio, GraphicsQualityManager quality, CameraController cameras, AimSystem aim, PoolInputReader input, MatchManager match)
        {
            audioManager = audio;
            graphicsQuality = quality;
            cameraController = cameras;
            aimSystem = aim;
            inputReader = input;
            matchManager = match;
        }

        private void Awake()
        {
            if (placement == null)
            {
                placement = FindAnyObjectByType<Cue.CueBallPlacementController>();
            }

            if (shotController == null)
            {
                shotController = FindAnyObjectByType<Cue.ShotController>();
            }

            // Before MatchManager.Start so the first match already uses the configured names.
            Apply(SaveSystem.Settings, true);
        }

        private void OnEnable() => SaveSystem.SettingsChanged += HandleChanged;

        private void OnDisable() => SaveSystem.SettingsChanged -= HandleChanged;

        private void HandleChanged(GameSettings settings) => Apply(settings, false);

        private void Apply(GameSettings settings, bool startup)
        {
            if (audioManager != null)
            {
                audioManager.SetVolumes(settings.masterVolume, settings.effectsVolume, settings.musicVolume, settings.ambienceVolume);
            }

            if (graphicsQuality != null)
            {
                // "Auto" (-1) resolves by platform; apply without persisting so the setting stays Auto.
                GraphicsTier tier = GraphicsQualityManager.ResolveTier(settings.graphicsTier);
                if (graphicsQuality.Tier != tier)
                {
                    graphicsQuality.Apply(tier, false);
                }
            }

            if (aimSystem != null)
            {
                aimSystem.AssistLevel = (AimAssistLevel)settings.aimAssist;
            }

            if (inputReader != null)
            {
                inputReader.SensitivityScale = settings.aimSensitivity;
            }

            if (placement != null)
            {
                placement.PointerSensitivity = settings.placementSensitivity;
            }

            if (shotController != null)
            {
                shotController.MouseStrokeEnabled = settings.mouseStroke;
            }

            CameraSystem.ShotCinematics.Enabled = settings.cinematicCamera;
            Replay.ShotReplay.Setting = (Replay.ShotReplay.Mode)settings.replayMode;

            if (matchManager != null)
            {
                matchManager.SetPlayerNames(settings.playerName, settings.secondPlayerName);
            }

            if (startup && cameraController != null)
            {
                cameraController.SetStartMode((CameraMode)settings.defaultCamera);
            }
        }
    }
}
