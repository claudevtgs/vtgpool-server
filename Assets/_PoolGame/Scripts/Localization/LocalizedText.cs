using System;
using UnityEngine;
using UnityEngine.UI;

namespace VTG.Pool.Localization
{
    /// <summary>Keeps a UI Text in the current language: re-evaluates its provider whenever <see cref="Loc.Changed"/> fires.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        private Func<string> provider;
        private Text text;

        /// <summary>Sets the text now and keeps it translated.</summary>
        public static Text Bind(Text target, Func<string> textProvider)
        {
            LocalizedText localized = target.GetComponent<LocalizedText>();
            if (localized == null)
            {
                localized = target.gameObject.AddComponent<LocalizedText>();
            }

            localized.text = target;
            localized.provider = textProvider;
            localized.Apply();
            return target;
        }

        /// <summary>Shortcut for a plain key.</summary>
        public static Text Bind(Text target, string key) => Bind(target, () => Loc.T(key));

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Loc.Changed -= Apply;

        private void Apply()
        {
            if (text != null && provider != null)
            {
                text.text = provider();
            }
        }
    }
}
