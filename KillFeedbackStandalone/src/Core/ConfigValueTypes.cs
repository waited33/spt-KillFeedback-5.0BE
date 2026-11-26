using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace KillFeedback.Core
{
    /// <summary>
    /// A <see cref="Vector2"/> setting split into two float entries.
    /// <para>
    /// The BepInEx build shipped with this client only accepts primitives, strings, enums and
    /// <c>KeyboardShortcut</c> in the config system; binding a <c>Vector2</c> throws
    /// <c>ArgumentException</c> and the whole plugin fails to load. Keeping the pair behind a
    /// wrapper means call sites still read a single <c>.Value</c>.
    /// </para>
    /// </summary>
    internal sealed class Vector2Setting
    {
        private readonly ConfigEntry<float> _x;
        private readonly ConfigEntry<float> _y;

        public Vector2Setting(ConfigFile configFile, string section, string name, string chineseName,
            Vector2 defaultValue, string englishDescription, string chineseDescription)
        {
            _x = configFile.Bind(section, name + " X", defaultValue.x,
                UiText.Describe(section, name + " X", chineseName + " X", englishDescription, chineseDescription));
            _y = configFile.Bind(section, name + " Y", defaultValue.y,
                UiText.Describe(section, name + " Y", chineseName + " Y", englishDescription, chineseDescription));
        }

        public Vector2 Value => new Vector2(_x.Value, _y.Value);
    }

    /// <summary>
    /// A <see cref="Color"/> setting stored as a hex string such as <c>#E3E3D6</c>.
    /// </summary>
    internal sealed class ColorSetting
    {
        private readonly ConfigEntry<string> _entry;
        private readonly Color _fallback;

        private string _cachedRaw;
        private Color _cached;

        public ColorSetting(ConfigFile configFile, string section, string name, string chineseName,
            Color defaultValue)
        {
            _fallback = defaultValue;

            var hex = ToHex(defaultValue);

            _entry = configFile.Bind(section, name, hex,
                UiText.Describe(section, name, chineseName,
                    $"Hex color without the # sign, for example {hex} (add two more digits for transparency)",
                    $"十六进制颜色，不带 # 号，例如 {hex}（末尾可再加两位表示透明度）"));

            _cachedRaw = hex;
            _cached = defaultValue;
        }

        public Color Value
        {
            get
            {
                var raw = _entry.Value;

                if (raw == _cachedRaw)
                {
                    return _cached;
                }

                _cachedRaw = raw;
                _cached = Parse(raw, _fallback);

                return _cached;
            }
        }

        /// <summary>Accepts <c>#RRGGBB</c>, <c>RRGGBB</c>, <c>#RRGGBBAA</c> and named colors.</summary>
        public static Color Parse(string raw, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            raw = raw.Trim();

            if (raw[0] != '#')
            {
                if (ColorUtility.TryParseHtmlString("#" + raw, out var withoutHash))
                {
                    return withoutHash;
                }
            }

            return ColorUtility.TryParseHtmlString(raw, out var parsed) ? parsed : fallback;
        }

        public static string ToHex(Color color)
        {
            var r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            var g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            var b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);

            // No leading '#': the config file parser would otherwise be free to treat the value
            // as a comment on some builds.
            return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}", r, g, b);
        }
    }
}
