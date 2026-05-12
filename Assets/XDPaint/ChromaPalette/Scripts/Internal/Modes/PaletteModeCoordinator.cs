using UnityEngine;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.Core.Modes;
using XDPaint.ChromaPalette.Internal.Rendering;
using XDPaint.ChromaPalette.Internal.UI;

namespace XDPaint.ChromaPalette.Internal.Modes
{
    /// <summary>
    /// Service responsible for managing palette mode switching and configuration.
    /// Handles mode-specific settings, visibility, and texture management.
    /// </summary>
    public class PaletteModeCoordinator
    {
        private PaletteShaderBridge shaderBridge;
        private PaletteInteraction paletteInteraction;
        
        private GameObject hueSliderObject;
        private GameObject valueSliderObject;
        
        private ColorPickerMode currentMode;
        private TextureModeSettings textureSettings;
        private PaletteModeSettings paletteSettings;

        public void Initialize(
            PaletteShaderBridge shaderBridgeParam,
            PaletteInteraction paletteInteractionParam,
            GameObject hueSlider,
            GameObject valueSlider)
        {
            shaderBridge = shaderBridgeParam;
            paletteInteraction = paletteInteractionParam;
            hueSliderObject = hueSlider;
            valueSliderObject = valueSlider;
        }
        
        public void SetModeSettings(TextureModeSettings textureMode, PaletteModeSettings paletteMode)
        {
            textureSettings = textureMode;
            paletteSettings = paletteMode;
        }
        
        public void SetMode(ColorPickerMode newMode, bool forceUpdate = false)
        {
            if (currentMode != newMode || forceUpdate)
            {
                currentMode = newMode;
                UpdateMode();
            }
        }
        
        public void UpdateMode()
        {
            paletteInteraction?.SetMode(currentMode);
            shaderBridge?.UpdatePaletteMode(currentMode);
            SetModeVisibility(currentMode);
        }
        
        private void SetModeVisibility(ColorPickerMode mode)
        {
            if (mode == ColorPickerMode.Rectangle)
            {
                SetSliderVisibility(hueSliderObject, true);
                SetSliderVisibility(valueSliderObject, false);
            }
            else if (mode == ColorPickerMode.Circle)
            {
                SetSliderVisibility(hueSliderObject, false);
                SetSliderVisibility(valueSliderObject, true);
            }
            else if (mode == ColorPickerMode.CircleFull)
            {
                SetSliderVisibility(hueSliderObject, false);
                SetSliderVisibility(valueSliderObject, false);
            }
            else if (mode == ColorPickerMode.CircleTriangle || mode == ColorPickerMode.CircleCircle)
            {
                SetSliderVisibility(hueSliderObject, false);
                SetSliderVisibility(valueSliderObject, false);
            }
            else if (mode is ColorPickerMode.Texture or ColorPickerMode.Palette)
            {
                SetSliderVisibility(hueSliderObject, false);
                SetSliderVisibility(valueSliderObject, false);
            }
        }
        
        private void SetSliderVisibility(GameObject sliderObject, bool visible)
        {
            if (sliderObject != null)
            {
                sliderObject.SetActive(visible);
            }
        }
        
        public Texture2D GetActiveTexture()
        {
            return GetModeTexture(currentMode);
        }
        
        public Texture2D GetModeTexture(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Rectangle or ColorPickerMode.Circle => null,
                ColorPickerMode.CircleFull => null,
                ColorPickerMode.CircleTriangle => null,
                ColorPickerMode.CircleCircle => null,
                ColorPickerMode.Texture => textureSettings?.GetTexture(),
                ColorPickerMode.Palette => paletteSettings?.GetTexture(),
                _ => null
            };
        }
        
        public bool GetBicubicSampling()
        {
            return GetModeBicubicSampling(currentMode);
        }
        
        public bool GetModeBicubicSampling(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Rectangle or ColorPickerMode.Circle or ColorPickerMode.CircleFull or ColorPickerMode.CircleTriangle or ColorPickerMode.CircleCircle => false,
                ColorPickerMode.Texture => textureSettings?.UseBicubicSampling ?? true,
                ColorPickerMode.Palette => paletteSettings?.UseBicubicSampling ?? false,
                _ => false
            };
        }
        
        public bool GetCursorSnapping()
        {
            return GetModeCursorSnapping(currentMode);
        }
        
        public bool GetModeCursorSnapping(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Rectangle => false,
                ColorPickerMode.Circle => false,
                ColorPickerMode.CircleFull => false,
                ColorPickerMode.CircleCircle => false,
                ColorPickerMode.CircleTriangle => false,
                ColorPickerMode.Texture => textureSettings?.SnapCursor ?? true,
                ColorPickerMode.Palette => paletteSettings?.SnapCursor ?? true,
                _ => false
            };
        }

        public bool GetIgnoreTransparent() => GetModeIgnoreTransparent(currentMode);
        public float GetTransparentAlphaThreshold() => GetModeTransparentAlphaThreshold(currentMode);
        public Color[] GetIgnoredColors() => GetModeIgnoredColors(currentMode);
        public float GetIgnoreColorTolerance() => GetModeIgnoreColorTolerance(currentMode);

        public bool GetModeIgnoreTransparent(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Texture => textureSettings?.IgnoreTransparent ?? true,
                ColorPickerMode.Palette => paletteSettings?.IgnoreTransparent ?? true,
                _ => false
            };
        }

        public float GetModeTransparentAlphaThreshold(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Texture => textureSettings?.TransparentAlphaThreshold ?? 0.01f,
                ColorPickerMode.Palette => paletteSettings?.TransparentAlphaThreshold ?? 0.01f,
                _ => 0f
            };
        }

        public Color[] GetModeIgnoredColors(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Texture => textureSettings?.IgnoredColors ?? System.Array.Empty<Color>(),
                ColorPickerMode.Palette => paletteSettings?.IgnoredColors ?? System.Array.Empty<Color>(),
                _ => System.Array.Empty<Color>()
            };
        }

        public float GetModeIgnoreColorTolerance(ColorPickerMode mode)
        {
            return mode switch
            {
                ColorPickerMode.Texture => textureSettings?.IgnoreColorTolerance ?? 0.02f,
                ColorPickerMode.Palette => paletteSettings?.IgnoreColorTolerance ?? 0.02f,
                _ => 0f
            };
        }
        
        public void SetPaletteTexture(Texture2D texture)
        {
            if (textureSettings == null)
            {
                textureSettings = new TextureModeSettings();
            }

            textureSettings.Texture = texture;
            SetMode(ColorPickerMode.Texture);
        }

        public Color[] GetAvailableColors()
        {
            return GetModeAvailableColors(currentMode);
        }

        public Color[] GetModeAvailableColors(ColorPickerMode mode)
        {
            switch (mode)
            {
                case ColorPickerMode.Palette:
                    if (paletteSettings?.ColorPalette != null && paletteSettings.ColorPalette.GeneratedColors != null)
                    {
                        return paletteSettings.ColorPalette.GeneratedColors;
                    }
                    break;

                case ColorPickerMode.Texture:
                    var texture = textureSettings?.GetTexture();
                    if (texture != null && texture.isReadable)
                    {
                        return ExtractUniqueColorsFromTexture(texture);
                    }
                    break;
            }

            return System.Array.Empty<Color>();
        }

        private Color[] ExtractUniqueColorsFromTexture(Texture2D texture)
        {
            var uniqueColors = new System.Collections.Generic.HashSet<Color>();
            var pixels = texture.GetPixels();

            foreach (var pixel in pixels)
            {
                if (pixel.a > 0.01f)
                {
                    uniqueColors.Add(pixel);
                }
            }

            var result = new Color[uniqueColors.Count];
            uniqueColors.CopyTo(result);
            return result;
        }
    }
}
