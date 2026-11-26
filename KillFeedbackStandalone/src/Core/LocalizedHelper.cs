using System;
using EFT;

namespace KillFeedback.Core
{
    /// <summary>
    /// Thin wrapper over the game's own localization functions.
    /// <para>
    /// The original mod reached these through <c>KmyTarkovApi</c>'s reflection-based helper, which
    /// located the localization type by scanning for a method called "ParseLocalization". On the
    /// 5.0 client the type is <c>EFT.LocalizationExtensions</c> and is referenced directly, so no
    /// reflection is needed here.
    /// </para>
    /// </summary>
    internal static class LocalizedHelper
    {
        /// <summary>Looks up a localization key. Unknown keys are returned unchanged.</summary>
        public static string Localized(string id, string prefix = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            try
            {
                return LocalizationExtensions.Localized(id, prefix);
            }
            catch (Exception)
            {
                // A missing key must never take the HUD down mid-raid.
                return id;
            }
        }

        /// <summary>
        /// True when a lookup produced real text instead of the key it was given.
        /// <para>
        /// Item names are stored as <c>"&lt;templateId&gt; ShortName"</c>; when the dictionary has
        /// no entry for that key the lookup hands the key straight back, which is what made the
        /// HUD print a 24 character hex id followed by "ShortName".
        /// </para>
        /// </summary>
        public static bool IsResolved(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return !text.EndsWith(" ShortName", StringComparison.Ordinal) &&
                   !text.EndsWith(" Name", StringComparison.Ordinal);
        }

        /// <summary>Converts a non-latin nickname into its latin transliteration.</summary>
        public static string Transliterate(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            try
            {
                return Transliteration.Transliterate(text);
            }
            catch (Exception)
            {
                return text;
            }
        }
    }
}
