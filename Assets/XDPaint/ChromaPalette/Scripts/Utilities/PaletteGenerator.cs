using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.ScriptableObjects;

namespace XDPaint.ChromaPalette.Utilities
{
    /// <summary>
    /// Static utility class for generating color palettes based on color theory.
    /// Implements various harmony algorithms and advanced generation techniques.
    /// </summary>
    public static class PaletteGenerator
    {
        public static Color[] GeneratePalette(PaletteType type, Color[] baseColors, PaletteSettings settings)
        {
            if (baseColors == null || baseColors.Length == 0)
                return Array.Empty<Color>();
            
            var primaryColor = baseColors[0];
            return type switch
            {
                PaletteType.Complementary => GenerateComplementary(primaryColor, settings),
                PaletteType.Triadic => GenerateTriadic(primaryColor, settings),
                PaletteType.Tetradic => GenerateTetradic(primaryColor, settings),
                PaletteType.Analogous => GenerateAnalogous(primaryColor, settings),
                PaletteType.SplitComplementary => GenerateSplitComplementary(primaryColor, settings),
                PaletteType.Monochromatic => GenerateMonochromatic(primaryColor, settings),
                PaletteType.Gradient => GenerateGradient(baseColors, settings),
                PaletteType.Custom => baseColors?.Length > 0 ? (Color[])baseColors.Clone() : new Color[0],
                _ => new Color[] { primaryColor }
            };
        }
        
        public static Color[] GenerateComplementary(Color baseColor, PaletteSettings settings)
        {
            var hsv = ColorConversionUtility.RGBToHSV(baseColor);
            
            var colors = new List<Color> { baseColor };

            var compH = (hsv.H + 0.5f) % 1f;
            colors.Add(ColorConversionUtility.HSVToRGB(new ColorHSV(compH, hsv.S, hsv.V)));
            
            return colors.ToArray();
        }
        
        public static Color[] GenerateTriadic(Color baseColor, PaletteSettings settings)
        {
            var hsv = ColorConversionUtility.RGBToHSV(baseColor);
            
            var colors = new List<Color>
            {
                baseColor,
                ColorConversionUtility.HSVToRGB(new ColorHSV((hsv.H + 120f/360f) % 1f, hsv.S, hsv.V)),
                ColorConversionUtility.HSVToRGB(new ColorHSV((hsv.H + 240f/360f) % 1f, hsv.S, hsv.V))
            };
            
            return colors.ToArray();
        }
        
        public static Color[] GenerateTetradic(Color baseColor, PaletteSettings settings)
        {
            var hsv = ColorConversionUtility.RGBToHSV(baseColor);
            
            var colors = new List<Color>
            {
                baseColor,
                ColorConversionUtility.HSVToRGB(new ColorHSV((hsv.H + 90f/360f) % 1f, hsv.S, hsv.V)),
                ColorConversionUtility.HSVToRGB(new ColorHSV((hsv.H + 180f/360f) % 1f, hsv.S, hsv.V)),
                ColorConversionUtility.HSVToRGB(new ColorHSV((hsv.H + 270f/360f) % 1f, hsv.S, hsv.V))
            };
            
            return colors.ToArray();
        }
        
        public static Color[] GenerateAnalogous(Color baseColor, PaletteSettings settings)
        {
            var hsv = ColorConversionUtility.RGBToHSV(baseColor);
            var colors = new List<Color>();
            var halfCount = settings.ColorCount / 2;
            var spacing = settings.HueShift > 0 ? settings.HueShift / 360f : 30f / 360f;
            
            for (var i = -halfCount; i <= halfCount; i++)
            {
                if (colors.Count >= settings.ColorCount)
                    break;
                
                // Use Mathf.Repeat to correctly wrap hue into [0,1) for large negative offsets, producing
                // negative H values which resulted in black colors
                var newH = Mathf.Repeat(hsv.H + i * spacing, 1f);
                var t = (i + halfCount) / (float)Mathf.Max(1, settings.ColorCount - 1);
                var newS = Mathf.Clamp01(hsv.S + (t - 0.5f) * settings.SaturationVariation * 0.5f);
                var newV = Mathf.Clamp01(hsv.V + (t - 0.5f) * settings.LightnessVariation * 0.5f);
                colors.Add(ColorConversionUtility.HSVToRGB(new ColorHSV(newH, newS, newV)));
            }
            
            return colors.Take(settings.ColorCount).ToArray();
        }
        
        public static Color[] GenerateSplitComplementary(Color baseColor, PaletteSettings settings)
        {
            var hsv = ColorConversionUtility.RGBToHSV(baseColor);
            var compH = (hsv.H + 0.5f) % 1f;
            var colors = new List<Color>
            {
                baseColor,
                ColorConversionUtility.HSVToRGB(new ColorHSV((compH - 30f/360f + 1f) % 1f, hsv.S, hsv.V)),
                ColorConversionUtility.HSVToRGB(new ColorHSV((compH + 30f/360f) % 1f, hsv.S, hsv.V))
            };
            
            return colors.ToArray();
        }
        
        public static Color[] GenerateMonochromatic(Color baseColor, PaletteSettings settings)
        {
            var hsv = ColorConversionUtility.RGBToHSV(baseColor);
            var colors = new List<Color>();
            for (var i = 0; i < settings.ColorCount; i++)
            {
                var t = i / (float)(settings.ColorCount - 1);
                var newS = Mathf.Clamp01(hsv.S + (t - 0.5f) * settings.SaturationVariation);
                var newV = Mathf.Clamp01(hsv.V + (t - 0.5f) * settings.LightnessVariation);
                colors.Add(ColorConversionUtility.HSVToRGB(new ColorHSV(hsv.H, newS, newV)));
            }
            
            return colors.ToArray();
        }
        
        public static Color[] GenerateGradient(Color[] baseColors, PaletteSettings settings)
        {
            if (baseColors.Length < 2)
                return baseColors;
            
            var colors = new List<Color>();
            for (var i = 0; i < settings.ColorCount; i++)
            {
                var t = i / (float)(settings.ColorCount - 1);
                var interpolated = InterpolateColors(baseColors, t, settings.SmoothTransitions);
                if (settings.SaturationVariation > 0 || settings.LightnessVariation > 0 || settings.HueShift > 0)
                {
                    var hsv = ColorConversionUtility.RGBToHSV(interpolated);
                    if (settings.HueShift > 0)
                    {
                        hsv = new ColorHSV((hsv.H + (t - 0.5f) * settings.HueShift / 360f + 1f) % 1f, hsv.S, hsv.V);
                    }
                    
                    if (settings.SaturationVariation > 0)
                    {
                        var curve = Mathf.Sin(t * Mathf.PI);
                        hsv = new ColorHSV(hsv.H, Mathf.Clamp01(hsv.S + curve * settings.SaturationVariation), hsv.V);
                    }
                    
                    if (settings.LightnessVariation > 0)
                    {
                        hsv = new ColorHSV(hsv.H, hsv.S, Mathf.Clamp01(hsv.V + (t - 0.5f) * settings.LightnessVariation));
                    }
                    
                    interpolated = ColorConversionUtility.HSVToRGB(hsv);
                }
                
                colors.Add(interpolated);
            }
            
            return colors.ToArray();
        }

        private static Color InterpolateColors(Color[] colors, float t, bool smooth)
        {
            if (colors.Length == 0)
                return Color.white;
            
            if (colors.Length == 1)
                return colors[0];
            
            t = Mathf.Clamp01(t);
            var segmentFloat = t * (colors.Length - 1);
            var segment = Mathf.FloorToInt(segmentFloat);
            var localT = segmentFloat - segment;
            if (segment >= colors.Length - 1)
                return colors[colors.Length - 1];
            
            var from = colors[segment];
            var to = colors[segment + 1];
            if (smooth)
            {
                localT = localT * localT * (3f - 2f * localT);
            }
            
            return Color.Lerp(from, to, localT);
        }
    }
}