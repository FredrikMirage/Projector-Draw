using System;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.Utilities;

namespace XDPaint.ChromaPalette.ScriptableObjects
{
    /// <summary>
    /// ScriptableObject representing a color palette with generation capabilities.
    /// Supports various color harmony theories and advanced generation methods.
    /// </summary>
    [CreateAssetMenu(fileName = "New Color Palette", menuName = "XDPaint/Color Palette", order = 101)]
    public class ColorPalette : ScriptableObject
    {
        [SerializeField] private string paletteName = "New Palette";
        [SerializeField] private PaletteType paletteType = PaletteType.Complementary;
        [SerializeField] private Color[] baseColors = { Color.red };
        [SerializeField] private Color[] generatedColors = Array.Empty<Color>();
        [SerializeField] private List<PaletteHandleOverride> handleOverrides = new();
        [SerializeField] private PaletteSettings settings = new();
        
        [NonSerialized] private Texture2D cachedTexture;
        
        public string PaletteName => paletteName;
        public PaletteType Type => paletteType;
        public Color[] BaseColors => baseColors;
        public Color[] GeneratedColors => generatedColors;
        public PaletteSettings Settings => settings;
        public Texture2D GeneratedTexture => GetOrCreateTexture();
        public IReadOnlyList<PaletteHandleOverride> HandleOverrides => handleOverrides;
        
        public void GeneratePalette()
        {
            if (baseColors == null || baseColors.Length == 0)
            {
                Debug.LogWarning($"ColorPalette '{paletteName}': No base colors defined.");
                return;
            }

            generatedColors = PaletteGenerator.GeneratePalette(paletteType, baseColors, settings);
            ApplyHandleOverrides();
            RegenerateTexture();

#if UNITY_EDITOR
            if (this != null)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }
        
        public Texture2D GetOrCreateTexture()
        {
            if (cachedTexture != null)
            {
                bool unreadable;
                try
                {
                    unreadable = !cachedTexture.isReadable;
                }
                catch
                {
                    unreadable = true;
                }
                
                if (unreadable)
                {
                    GeneratePalette();
                }
            }

            if (cachedTexture != null && generatedColors is { Length: > 0 })
                return cachedTexture;

            if (generatedColors != null && generatedColors.Length > 0)
            {
                try
                {
                    cachedTexture = PaletteTextureGenerator.CreatePaletteTexture(generatedColors, settings);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error generating palette texture: {e.Message}");
                    cachedTexture = null;
                }
            }
            
            return cachedTexture;
        }
        
        public void RegenerateTexture()
        {
            if (cachedTexture != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Destroy(cachedTexture);
                }
                else
                {
                    DestroyImmediate(cachedTexture);
                }
#else
                UnityEngine.Object.Destroy(cachedTexture);
#endif
                cachedTexture = null;
            }
        }
        
        public void SetBaseColors(params Color[] colors)
        {
            baseColors = colors;
            GeneratePalette();
        }
        
        public void SetPaletteType(PaletteType type)
        {
            paletteType = type;
            handleOverrides.Clear();
            GeneratePalette();
        }

        public void SetHandleOverride(int index, float? saturation, float? value)
        {
            if (generatedColors == null || generatedColors.Length == 0)
                return;

            EnsureHandleOverrides(generatedColors.Length);
            if (index < 0 || index >= handleOverrides.Count)
                return;

            var data = handleOverrides[index];
            var changed = false;
            if (saturation.HasValue)
            {
                var newS = Mathf.Clamp01(saturation.Value);
                if (!data.OverrideSaturation || !Mathf.Approximately(data.Saturation, newS))
                {
                    data.OverrideSaturation = true;
                    data.Saturation = newS;
                    changed = true;
                }
            }

            if (value.HasValue)
            {
                var newV = Mathf.Clamp01(value.Value);
                if (!data.OverrideValue || !Mathf.Approximately(data.Value, newV))
                {
                    data.OverrideValue = true;
                    data.Value = newV;
                    changed = true;
                }
            }

            if (!changed)
                return;

            handleOverrides[index] = data;
            ApplyHandleOverrides();
            RegenerateTexture();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void ClearHandleOverride(int index, bool saturation, bool value)
        {
            if (handleOverrides == null || index < 0 || index >= handleOverrides.Count)
                return;

            var data = handleOverrides[index];
            var changed = false;
            if (saturation)
            {
                if (data.OverrideSaturation)
                {
                    data.OverrideSaturation = false;
                    changed = true;
                }
            }

            if (value)
            {
                if (data.OverrideValue)
                {
                    data.OverrideValue = false;
                    changed = true;
                }
            }

            if (!changed)
                return;

            handleOverrides[index] = data;
            ApplyHandleOverrides();
            RegenerateTexture();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void ClearAllOverrides(bool saturation, bool value, bool rebuild = true)
        {
            if (handleOverrides == null || handleOverrides.Count == 0)
                return;

            var changed = false;
            for (var i = 0; i < handleOverrides.Count; i++)
            {
                var data = handleOverrides[i];
                if (saturation && data.OverrideSaturation)
                {
                    data.OverrideSaturation = false;
                    changed = true;
                }

                if (value && data.OverrideValue)
                {
                    data.OverrideValue = false;
                    changed = true;
                }

                handleOverrides[i] = data;
            }

            if (!changed)
                return;

            if (rebuild)
            {
                ApplyHandleOverrides();
                RegenerateTexture();

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        private void ApplyHandleOverrides()
        {
            if (generatedColors == null)
                return;

            EnsureHandleOverrides(generatedColors.Length);
            for (var i = 0; i < generatedColors.Length; i++)
            {
                var overrideData = handleOverrides[i];
                if (!overrideData.OverrideSaturation && !overrideData.OverrideValue)
                    continue;

                var hsv = ColorConversionUtility.RGBToHSV(generatedColors[i]);
                if (overrideData.OverrideSaturation)
                {
                    hsv = new ColorHSV(hsv.H, Mathf.Clamp01(overrideData.Saturation), hsv.V, hsv.A);
                }

                if (overrideData.OverrideValue)
                {
                    hsv = new ColorHSV(hsv.H, hsv.S, Mathf.Clamp01(overrideData.Value), hsv.A);
                }

                generatedColors[i] = ColorConversionUtility.HSVToRGB(hsv);
            }
        }

        private void EnsureHandleOverrides(int count)
        {
            if (handleOverrides == null)
            {
                handleOverrides = new List<PaletteHandleOverride>(count);
            }

            while (handleOverrides.Count < count)
            {
                handleOverrides.Add(new PaletteHandleOverride());
            }

            if (handleOverrides.Count > count)
            {
                handleOverrides.RemoveRange(count, handleOverrides.Count - count);
            }
        }
        
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                GeneratePalette();
            }
            else
            {
                RegenerateTexture();
                UnityEditor.EditorApplication.delayCall -= HandleValidation;
                var instance = this;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (instance != null)
                    {
                        instance.GeneratePalette();
                    }
                };
            }
#endif
        }
        
#if UNITY_EDITOR
        private void HandleValidation()
        {
            if (this != null)
            {
                GeneratePalette();
            }
        }
#endif

        private void OnDisable()
        {
            RegenerateTexture();
        }

        private void OnDestroy()
        {
            RegenerateTexture();
        }
    }
    
    public enum PaletteType
    {
        [Tooltip("Two colors opposite on the color wheel")]
        Complementary,

        [Tooltip("Three colors evenly spaced on the color wheel")]
        Triadic,

        [Tooltip("Four colors evenly spaced on the color wheel")]
        Tetradic,

        [Tooltip("Colors adjacent on the color wheel")]
        Analogous,

        [Tooltip("Base color plus two adjacent to its complement")]
        SplitComplementary,

        [Tooltip("Variations of a single hue")]
        Monochromatic,

        [Tooltip("Smooth gradient between colors")]
        Gradient,

        [Tooltip("Custom defined colors")]
        Custom
    }
    
    [Serializable]
    public class PaletteSettings
    {
        [Range(2, 64)] public int ColorCount = 5;
        [Range(0f, 1f)] public float SaturationVariation = 0.2f;
        [Range(0f, 1f)] public float LightnessVariation = 0.3f;
        [Range(0f, 360f)] public float HueShift;
        
        public TextureLayout Layout = TextureLayout.Horizontal;
        public bool SmoothTransitions = true;
        public bool AutoSize = true;
        [Range(1, 2048)] public int TextureWidth = 256;
        [Range(1, 2048)] public int TextureHeight = 256;
        [Tooltip("Rotation angle in 90-degree increments (0, 90, 180, 270)")]
        [Range(0, 270)] public int RotationAngle = 0;
    }

    [Serializable]
    public class PaletteHandleOverride
    {
        [Range(0f, 1f)] public float Saturation = 1f;
        [Range(0f, 1f)] public float Value = 1f;
        public bool OverrideSaturation;
        public bool OverrideValue;
    }

    public enum TextureLayout
    {
        Horizontal,
        Vertical, 
        Grid,
        Radial,
        Smooth
    }
}