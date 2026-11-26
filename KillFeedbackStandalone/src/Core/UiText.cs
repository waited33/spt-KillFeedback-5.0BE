using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using KillFeedback.Attributes;

namespace KillFeedback.Core
{
    /// <summary>Language of the settings panel.</summary>
    public enum HudLanguage
    {
        English = 0,
        Chinese = 1
    }

    /// <summary>
    /// Translates the Configuration Manager panel.
    /// <para>
    /// The manager draws the config <em>key</em> and the section name, so the obvious way to
    /// translate a panel is to rename them - which orphans every saved value, because the config
    /// file is keyed by "section/key". This build of the manager reflects over the tag object of a
    /// <see cref="ConfigDescription"/> and copies any field whose name matches one of its own
    /// properties (<c>DispName</c>, <c>Category</c>, <c>Description</c>, ...), so the keys can stay
    /// in English and only the displayed text changes. Switching language therefore keeps every
    /// setting exactly where it was.
    /// </para>
    /// </summary>
    internal static class UiText
    {
        public static HudLanguage Language = HudLanguage.English;

        public static bool IsChinese => Language == HudLanguage.Chinese;

        /// <summary>Re-applies the labels that were built while a different language was active.</summary>
        private static readonly List<Action> Refreshers = new List<Action>();

        /// <summary>
        /// Next position value handed out. The manager sorts a section by <c>Order</c> and, on this
        /// build, does it <em>descending</em> (a larger value ends up higher up) with a sort that is
        /// not stable - so entries left at the default 0 come out in an arbitrary order. Giving
        /// every entry an explicit, decreasing value restores the order in which they are bound,
        /// which is the order they are written in below.
        /// </summary>
        private static int _nextOrder = 1000;

        private static readonly Dictionary<string, string> Sections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Main Settings", "主要设置" },
            { "Position Rotation Scale Settings", "位置 / 尺寸 / 缩放" },
            { "Animation Speed Settings", "动画速度" },
            { "Distance Settings", "距离设置" },
            { "Font Styles Settings", "字体样式" },
            { "Kill Color Settings", "颜色设置" }
        };

        /// <summary>Chinese name of a section, or the English one when there is no translation.</summary>
        public static string Section(string section)
        {
            if (IsChinese && Sections.TryGetValue(section, out var translated))
            {
                return translated;
            }

            return section;
        }

        /// <summary>Returns the Chinese text when Chinese is active and a translation exists.</summary>
        public static string Pick(string english, string chinese)
        {
            return IsChinese && !string.IsNullOrEmpty(chinese) ? chinese : english;
        }

        /// <summary>
        /// Builds the description of a setting, including the tags the Configuration Manager reads
        /// to draw a translated label, a translated tooltip and a translated section header.
        /// </summary>
        /// <param name="section">Config section; stays English in the file, translated on screen.</param>
        /// <param name="key">Config key; stays English in the file, translated on screen.</param>
        /// <param name="chineseName">Chinese label of the setting.</param>
        /// <param name="englishDescription">Tooltip in English, also written to the config file.</param>
        /// <param name="chineseDescription">Tooltip in Chinese.</param>
        /// <param name="acceptableValues">Optional range or list shown by the manager.</param>
        /// <param name="drawer">Optional custom control instead of the default one.</param>
        /// <param name="hideDefaultButton">Hides the "Reset" button next to the setting.</param>
        /// <param name="order">
        /// Position inside its section; larger is higher up on this build. Left null, the entry
        /// takes the next value from the binding sequence, which keeps the panel in the same order
        /// as the calls below.
        /// </param>
        public static ConfigDescription Describe(string section, string key, string chineseName,
            string englishDescription, string chineseDescription,
            AcceptableValueBase acceptableValues = null,
            Action<ConfigEntryBase> drawer = null,
            bool hideDefaultButton = false,
            int? order = null)
        {
            order ??= _nextOrder--;

            var attributes = new ConfigurationManagerAttributes
            {
                CustomDrawer = drawer,
                HideDefaultButton = drawer != null || hideDefaultButton ? true : (bool?)null,
                Order = order
            };

            void Refresh()
            {
                attributes.Category = Section(section);
                attributes.DispName = Pick(key, chineseName);
                attributes.Description = Pick(englishDescription, chineseDescription);
            }

            Refresh();
            Refreshers.Add(Refresh);

            // The English text stays in the config file as the key comment, so the file remains
            // readable no matter which language the panel is currently in.
            return new ConfigDescription(englishDescription, acceptableValues, attributes);
        }

        /// <summary>Recomputes every label; call after <see cref="Language"/> changed.</summary>
        public static void Relocalize()
        {
            for (var i = 0; i < Refreshers.Count; i++)
            {
                try
                {
                    Refreshers[i]();
                }
                catch
                {
                    // A label is never worth breaking the panel over.
                }
            }
        }
    }
}
