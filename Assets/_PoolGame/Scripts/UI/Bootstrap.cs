using UnityEngine;
using UnityEngine.SceneManagement;
using VTG.Pool.Save;

namespace VTG.Pool.UI
{
    /// <summary>00_Bootstrap: loads settings, sets frame pacing and opens the main menu.</summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string firstScene = "01_MainMenu";

        private void Start()
        {
            GameSettings settings = SaveSystem.Settings;
            QualitySettings.vSyncCount = Application.isMobilePlatform ? 0 : 1;
            Application.targetFrameRate = Application.isMobilePlatform ? 60 : -1;
            Debug.Log($"[Bootstrap] Settings loaded (graphics {settings.graphicsTier}, assist {settings.aimAssist}). Loading {firstScene}.");
            IntroDirector.PlayOnNextMenu = true;
            SceneManager.LoadScene(firstScene);
        }
    }
}
