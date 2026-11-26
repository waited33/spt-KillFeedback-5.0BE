using System.Collections.Generic;
using UnityEngine;

namespace KillFeedback.Utils
{
    /// <summary>Converts colours to the HTML hex format TextMeshPro understands.</summary>
    internal static class ColorHelper
    {
        private static readonly Dictionary<Color, string> HtmlColors = new Dictionary<Color, string>();

        public static string ColorToHtml(this Color color)
        {
            if (HtmlColors.TryGetValue(color, out var htmlColor))
            {
                return htmlColor;
            }

            htmlColor = $"#{ColorUtility.ToHtmlStringRGBA(color)}";
            HtmlColors[color] = htmlColor;
            return htmlColor;
        }
    }
}
