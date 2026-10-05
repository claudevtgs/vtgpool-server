using System;
using UnityEngine;
using UnityEngine.UI;
using VTG.Pool.Inputs;
using VTG.Pool.Save;

namespace VTG.Pool.UI
{
    /// <summary>
    /// Keeps a runtime canvas usable on any screen: scaler settings for desktop or touch (larger UI on phones,
    /// width- or height-matched by aspect) and a <see cref="SafeRoot"/> child that avoids notches and rounded
    /// corners (Screen.safeArea). Re-applies when the touch setting or the screen changes.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveCanvas : MonoBehaviour
    {
        private CanvasScaler scaler;
        private Canvas canvas;
        private RectTransform safeRoot;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        /// <summary>UI content parent inside the safe area.</summary>
        public RectTransform SafeRoot
        {
            get
            {
                EnsureRoot();
                return safeRoot;
            }
        }

        /// <summary>Touch layout in use (on-screen controls, larger UI).</summary>
        public bool TouchLayout { get; private set; }

        /// <summary>Raised after the scaler or touch layout changed.</summary>
        public event Action LayoutChanged;

        private void Awake()
        {
            EnsureRoot();
            Apply();
        }

        private void OnEnable() => SaveSystem.SettingsChanged += HandleSettingsChanged;

        private void OnDisable() => SaveSystem.SettingsChanged -= HandleSettingsChanged;

        private void HandleSettingsChanged(GameSettings settings)
        {
            if (UiKit.TouchMode != TouchLayout)
            {
                Apply();
            }
            else
            {
                LayoutChanged?.Invoke();
            }
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || PixelSize() != lastScreen)
            {
                Apply();
            }
        }

        public void Apply()
        {
            EnsureRoot();
            TouchLayout = UiKit.TouchMode;
            lastScreen = PixelSize();
            UiKit.ConfigureScaler(scaler, TouchLayout, lastScreen.y > 0 ? (float)lastScreen.x / lastScreen.y : 16f / 9f);
            lastSafeArea = Screen.safeArea;
            TouchGestures.SafeAreaAnchors(lastSafeArea, new Vector2(Screen.width, Screen.height), out Vector2 min, out Vector2 max);
            safeRoot.anchorMin = min;
            safeRoot.anchorMax = max;
            safeRoot.offsetMin = Vector2.zero;
            safeRoot.offsetMax = Vector2.zero;
            LayoutChanged?.Invoke();
        }

        /// <summary>Size the canvas renders at (the screen, or a camera's target texture in camera space).</summary>
        private Vector2Int PixelSize()
        {
            Rect rect = canvas != null ? canvas.pixelRect : default;
            return rect.width > 0f && rect.height > 0f
                ? new Vector2Int(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height))
                : new Vector2Int(Screen.width, Screen.height);
        }

        private void EnsureRoot()
        {
            if (safeRoot != null)
            {
                return;
            }

            scaler = GetComponent<CanvasScaler>();
            canvas = GetComponent<Canvas>();
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            safeRoot = go.GetComponent<RectTransform>();
            safeRoot.anchorMin = Vector2.zero;
            safeRoot.anchorMax = Vector2.one;
            safeRoot.sizeDelta = Vector2.zero;
        }
    }
}
