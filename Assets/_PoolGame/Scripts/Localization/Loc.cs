using System;
using System.Collections.Generic;
using UnityEngine;
using VTG.Pool.Save;

namespace VTG.Pool.Localization
{
    public enum Language
    {
        English,
        Vietnamese
    }

    /// <summary>
    /// String tables (English, Vietnamese) for every player-facing text: menus, HUD, rules messages, callouts.
    /// The language comes from <see cref="GameSettings.language"/> (-1 = system language) and can change live;
    /// <see cref="Changed"/> lets built UI refresh (see <see cref="LocalizedText"/>). Unknown keys fall back to
    /// English, then to the key itself.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(256);
        private static readonly Dictionary<string, string> Vietnamese = new Dictionary<string, string>(256);
        private static Language lastRaised;
        private static bool subscribed;

        static Loc()
        {
            LocTables.Fill(English, Vietnamese);
        }

        /// <summary>Forces a language regardless of settings (tests).</summary>
        public static Language? Override { get; set; }

        /// <summary>Raised after the effective language changed.</summary>
        public static event Action Changed;

        public static Language Current
        {
            get
            {
                EnsureSubscribed();
                return Override ?? Resolve(SaveSystem.Settings.language);
            }
        }

        /// <summary>Setting value -1 follows the device language.</summary>
        public static Language Resolve(int setting)
        {
            switch (setting)
            {
                case 0: return Language.English;
                case 1: return Language.Vietnamese;
                default: return Application.systemLanguage == SystemLanguage.Vietnamese ? Language.Vietnamese : Language.English;
            }
        }

        public static string T(string key)
        {
            Dictionary<string, string> table = Current == Language.Vietnamese ? Vietnamese : English;
            if (table.TryGetValue(key, out string value) || English.TryGetValue(key, out value))
            {
                return value;
            }

            return key;
        }

        public static string T(string key, params object[] args)
        {
            string format = T(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>English text of a key (stable object names, logs).</summary>
        public static string InEnglish(string key) => English.TryGetValue(key, out string value) ? value : key;

        public static bool Has(string key) => English.ContainsKey(key);

        /// <summary>Keys present in one table but missing in the other (tests).</summary>
        public static List<string> MissingTranslations()
        {
            var missing = new List<string>();
            foreach (string key in English.Keys)
            {
                if (!Vietnamese.ContainsKey(key)) missing.Add("vi:" + key);
            }

            foreach (string key in Vietnamese.Keys)
            {
                if (!English.ContainsKey(key)) missing.Add("en:" + key);
            }

            return missing;
        }

        /// <summary>Re-evaluates the language (call after changing <see cref="Override"/>).</summary>
        public static void Refresh()
        {
            Language current = Current;
            if (current != lastRaised)
            {
                lastRaised = current;
                Changed?.Invoke();
            }
        }

        private static void EnsureSubscribed()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
            SaveSystem.SettingsChanged += _ => Refresh();
            lastRaised = Override ?? Resolve(SaveSystem.Settings.language);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            subscribed = false;
        }
    }
}
