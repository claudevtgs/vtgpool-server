using UnityEngine;
using UnityEngine.UI;

namespace VTG.Pool.Replay
{
    /// <summary>Blinking "recording" dot of the replay badge.</summary>
    public sealed class ReplayBlink : MonoBehaviour
    {
        private Image image;

        private void Awake() => image = GetComponent<Image>();

        private void Update()
        {
            if (image != null)
            {
                Color color = image.color;
                color.a = Mathf.PingPong(Time.unscaledTime * 2f, 1f) > 0.35f ? 1f : 0.15f;
                image.color = color;
            }
        }
    }
}
