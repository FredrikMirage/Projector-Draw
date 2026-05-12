using System;
using UnityEngine;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.Utilities;

namespace XDPaint.ChromaPalette.Internal.State
{
    /// <summary>
    /// Service responsible for managing color state and conversions.
    /// Maintains color in multiple formats (RGB, HSV, CMYK) and ensures consistency.
    /// </summary>
    public class ColorStateStore
    {
        public event Action<Color> OnColorChanged;
        public event Action<Color> OnColorChanging;
        
        private Color currentColor = Color.white;
        private ColorHSV colorHSV = new(0f, 1f, 1f, 1f);
        private ColorCMYK colorCMYK = new(0f, 0f, 0f, 0f);
        
        public Color CurrentColor => currentColor;
        public ColorHSV ColorHSV => colorHSV;
        public ColorCMYK ColorCMYK => colorCMYK;
        
        public float Hue => colorHSV.H;
        public float Saturation => colorHSV.S;
        public float Value => colorHSV.V;
        public float Alpha => colorHSV.A;
        
        public ColorStateStore()
        {
            SetColorFromRGB(Color.white, false);
        }
        
        public void SetColorFromRGB(Color color, bool notify = true)
        {
            currentColor = color;
            colorHSV = ColorConversionUtility.RGBToHSV(color);
            colorCMYK = ColorConversionUtility.RGBToCMYK(color);
            if (notify)
            {
                OnColorChanged?.Invoke(currentColor);
            }
        }
        
        public void SetColorFromHSV(ColorHSV hsv, bool notifyChanging = false)
        {
            colorHSV = hsv;
            currentColor = ColorConversionUtility.HSVToRGB(colorHSV);
            colorCMYK = ColorConversionUtility.RGBToCMYK(currentColor);
            if (notifyChanging)
            {
                OnColorChanging?.Invoke(currentColor);
            }
        }
        
        public void SetColorFromCMYK(ColorCMYK cmyk)
        {
            colorCMYK = cmyk;
            currentColor = ColorConversionUtility.CMYKToRGB(colorCMYK);
            currentColor.a = colorHSV.A;
            var hsv = ColorConversionUtility.RGBToHSV(currentColor);
            colorHSV = new ColorHSV(hsv.H, hsv.S, hsv.V, colorHSV.A);
        }
        
        public void SetHue(float hue, bool notifyChanging = false)
        {
            if (Mathf.Approximately(colorHSV.H, hue))
                return;

            colorHSV = new ColorHSV(hue, colorHSV.S, colorHSV.V, colorHSV.A);
            currentColor = ColorConversionUtility.HSVToRGB(colorHSV);
            colorCMYK = ColorConversionUtility.RGBToCMYK(currentColor);
            if (notifyChanging)
            {
                OnColorChanging?.Invoke(currentColor);
            }
        }
        
        public void SetSaturation(float saturation, bool notifyChanging = false)
        {
            if (Mathf.Approximately(colorHSV.S, saturation))
                return;

            colorHSV = new ColorHSV(colorHSV.H, saturation, colorHSV.V, colorHSV.A);
            currentColor = ColorConversionUtility.HSVToRGB(colorHSV);
            colorCMYK = ColorConversionUtility.RGBToCMYK(currentColor);
            if (notifyChanging)
            {
                OnColorChanging?.Invoke(currentColor);
            }
        }
        
        public void SetValue(float value, bool notifyChanging = false)
        {
            if (Mathf.Approximately(colorHSV.V, value))
                return;
            
            colorHSV = new ColorHSV(colorHSV.H, colorHSV.S, value, colorHSV.A);
            currentColor = ColorConversionUtility.HSVToRGB(colorHSV);
            colorCMYK = ColorConversionUtility.RGBToCMYK(currentColor);
            if (notifyChanging)
            {
                OnColorChanging?.Invoke(currentColor);
            }
        }
        
        public void SetAlpha(float alpha, bool notifyChanging = false)
        {
            if (Mathf.Approximately(colorHSV.A, alpha))
                return;

            colorHSV = new ColorHSV(colorHSV.H, colorHSV.S, colorHSV.V, alpha);
            currentColor.a = alpha;
            if (notifyChanging)
            {
                OnColorChanging?.Invoke(currentColor);
            }
        }
        
        public void SetRed(float r)
        {
            var color = currentColor;
            color.r = r;
            SetColorFromRGB(color, false);
        }
        
        public void SetGreen(float g)
        {
            var color = currentColor;
            color.g = g;
            SetColorFromRGB(color, false);
        }
        
        public void SetBlue(float b)
        {
            var color = currentColor;
            color.b = b;
            SetColorFromRGB(color, false);
        }

        public void NotifyColorChanged()
        {
            OnColorChanged?.Invoke(currentColor);
        }
        
        public string GetHexString(bool includeAlpha = true)
        {
            return ColorConversionUtility.ColorToHex(currentColor, includeAlpha || colorHSV.A < 0.999f);
        }
    }
}