using UnityEngine;
using XDPaint.ChromaPalette.Core;

namespace XDPaint.ChromaPalette.Utilities
{
    /// <summary>
    /// Utility class for color space conversions.
    /// Follows Single Responsibility Principle - only handles color conversions.
    /// </summary>
    public static class ColorConversionUtility
    {
        public static ColorCMYK RGBToCMYK(Color rgb)
        {
            var k = 1f - Mathf.Max(rgb.r, rgb.g, rgb.b);
            if (k >= 0.999f)
            {
                return new ColorCMYK(0f, 0f, 0f, k);
            }

            var invK = 1f - k;
            var c = (1f - rgb.r - k) / invK;
            var m = (1f - rgb.g - k) / invK;
            var y = (1f - rgb.b - k) / invK;
            return new ColorCMYK(c, m, y, k);
        }
        
        public static Color CMYKToRGB(ColorCMYK cmyk)
        {
            var invK = 1f - cmyk.K;
            return new Color(
                (1f - cmyk.C) * invK,
                (1f - cmyk.M) * invK,
                (1f - cmyk.Y) * invK
            );
        }
        
        public static string ColorToHex(Color color, bool includeAlpha = true)
        {
            var hex = ColorUtility.ToHtmlStringRGBA(color);
            if (!includeAlpha && color.a >= 0.999f)
            {
                hex = hex.Substring(0, 6);
            }
            
            return "#" + hex;
        }
        
        public static bool TryParseHex(string hex, out Color color)
        {
            hex = hex.Trim();
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
            }

            if (hex.Length == 6)
            {
                hex += "FF";
            }
            
            return ColorUtility.TryParseHtmlString("#" + hex, out color);
        }
        
        public static ColorHSV RGBToHSV(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            return new ColorHSV(h, s, v, color.a);
        }
        
        public static Color HSVToRGB(ColorHSV hsv)
        {
            var color = Color.HSVToRGB(hsv.H, hsv.S, hsv.V);
            color.a = hsv.A;
            return color;
        }

        public static float CalculateColorDistance(Color a, Color b)
        {
            var dr = a.r - b.r;
            var dg = a.g - b.g;
            var db = a.b - b.b;
            var da = a.a - b.a;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db + da * da);
        }

        public static Color FindNearestColor(Color targetColor, Color[] availableColors)
        {
            if (availableColors == null || availableColors.Length == 0)
                return targetColor;

            var nearestColor = availableColors[0];
            var minDistance = CalculateColorDistance(targetColor, nearestColor);

            for (int i = 1; i < availableColors.Length; i++)
            {
                var distance = CalculateColorDistance(targetColor, availableColors[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestColor = availableColors[i];
                }
            }

            return nearestColor;
        }
    }
}