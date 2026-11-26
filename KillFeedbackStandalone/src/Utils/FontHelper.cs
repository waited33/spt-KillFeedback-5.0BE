using System;
using TMPro;
using UnityEngine;

namespace KillFeedback.Utils
{
    /// <summary>
    /// Puts the game's own font on the HUD texts.
    /// <para>
    /// The bundle's texts reference a font asset from a separate <c>fontasset</c> bundle that the
    /// standalone plugin does not ship, so the reference comes back null and nothing renders.
    /// The original mod replaced every font with <c>EFTFontHelper.BenderNormal</c>, which is
    /// loaded from the game's resources at <c>ui/fonts/jovanny lemonad - bender normal sdf</c>.
    /// That path is used here too; <c>TMP_Settings.defaultFontAsset</c> is only a fallback,
    /// because EFT does not populate the TMP settings asset.
    /// </para>
    /// </summary>
    internal static class FontHelper
    {
        private static readonly string[] FontResourcePaths =
        {
            "ui/fonts/jovanny lemonad - bender normal sdf",
            "ui/fonts/jovanny lemonad - bender outline sdf",
            "ui/fonts/jovanny lemonad - bender overlay sdf"
        };

        private static TMP_FontAsset _defaultFont;
        private static float _nextSearch;
        private static int _searches;
        private static bool _failureLogged;

        private const float SearchInterval = 2f;
        private const int MaxSearches = 20;

        /// <summary>The game's default TMP font, or null when no font could be located.</summary>
        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (_defaultFont != null)
                {
                    return _defaultFont;
                }

                // The resource may not be loadable while the game is still starting up, so a
                // failed lookup is retried (throttled) instead of being cached.
                if (_searches >= MaxSearches || Time.unscaledTime < _nextSearch)
                {
                    return null;
                }

                _searches++;
                _nextSearch = Time.unscaledTime + SearchInterval;

                var font = FindFont();

                if (font != null)
                {
                    _defaultFont = font;

                    KillFeedbackPlugin.Logger?.LogInfo($"Kill HUD font: '{font.name}'.");

                    return font;
                }

                if (!_failureLogged)
                {
                    _failureLogged = true;

                    KillFeedbackPlugin.Logger?.LogWarning(
                        "No TMP font asset found yet; the kill HUD text will stay invisible until one is.");
                }

                return null;
            }
        }

        private static TMP_FontAsset FindFont()
        {
            for (var i = 0; i < FontResourcePaths.Length; i++)
            {
                try
                {
                    var font = Resources.Load<TMP_FontAsset>(FontResourcePaths[i]);

                    if (font != null)
                    {
                        return font;
                    }
                }
                catch (Exception)
                {
                    // Resource paths differ between client builds; try the next one.
                }
            }

            try
            {
                var fallback = TMP_Settings.defaultFontAsset;

                if (fallback != null)
                {
                    return fallback;
                }
            }
            catch (Exception)
            {
                // TMP settings are not always initialised this early.
            }

            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                TMP_FontAsset any = null;

                for (var i = 0; i < all.Length; i++)
                {
                    var font = all[i];

                    if (font == null)
                    {
                        continue;
                    }

                    if (font.name.IndexOf("bender", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        font.name.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return font;
                    }

                    if (any == null)
                    {
                        any = font;
                    }
                }

                return any;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Replaces the font of every TMP text under <paramref name="root"/>.</summary>
        public static void ReplaceAllFont(GameObject root)
        {
            var font = DefaultFont;

            if (font == null || root == null)
            {
                return;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);

            for (var i = 0; i < texts.Length; i++)
            {
                Apply(texts[i], font);
            }
        }

        /// <summary>
        /// Puts <paramref name="font"/> on one text, including its material.
        /// <para>
        /// The prefab's texts point at a font asset <em>and</em> a material that both live in the
        /// missing dependency bundle, so the material has to be replaced as well - a text with a
        /// font but no material still renders nothing.
        /// </para>
        /// </summary>
        public static void Apply(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null)
            {
                return;
            }

            if (text.font != font)
            {
                text.font = font;
            }

            var material = text.fontSharedMaterial;

            if (material == null || material.shader == null)
            {
                text.fontSharedMaterial = font.material;
            }
        }

        /// <summary>Counts texts under <paramref name="root"/> that still have no font.</summary>
        public static int CountWithoutFont(GameObject root, out int total)
        {
            total = 0;

            if (root == null)
            {
                return 0;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            var missing = 0;

            for (var i = 0; i < texts.Length; i++)
            {
                total++;

                if (texts[i] != null && texts[i].font == null)
                {
                    missing++;
                }
            }

            return missing;
        }
    }
}
