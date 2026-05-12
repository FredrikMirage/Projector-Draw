using UnityEngine;
using UnityEngine.UI;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.Utilities;

namespace XDPaint.ChromaPalette.Internal.Rendering
{
    /// <summary>
    /// Service responsible for managing shaders and materials for the color picker.
    /// Handles material creation, updates, and lifecycle management.
    /// </summary>
    public class PaletteShaderBridge
    {
        private Material paletteMaterial;
        private Material hueSliderMaterial;
        private Material valueSliderMaterial;
        private Material alphaSliderMaterial;
        
        private static readonly int ShaderMode = Shader.PropertyToID("_Mode");
        private static readonly int ShaderHueProperty = Shader.PropertyToID("_Hue");
        private static readonly int ShaderValueProperty = Shader.PropertyToID("_Value");
        private static readonly int ShaderUseBicubicSampling = Shader.PropertyToID("_UseBicubicSampling");
        private static readonly int ShaderPaletteTexture = Shader.PropertyToID("_PaletteTexture");
        private static readonly int ShaderCurrentValue = Shader.PropertyToID("_CurrentValue");
        private static readonly int ShaderCurrentColor = Shader.PropertyToID("_CurrentColor");
        private static readonly int Type = Shader.PropertyToID("_Type");
        private static readonly int CheckerSize = Shader.PropertyToID("_CheckerSize");
        private static readonly int SliderDimensions = Shader.PropertyToID("_SliderDimensions");

        private const string PALETTE_SHADER_NAME = "XD Paint/Chroma Palette/Palette";
        private const string SLIDER_SHADER_NAME = "XD Paint/Chroma Palette/Slider";
        private const float CHECKER_SIZE = 16f;
        
        private RawImage paletteImage;
        private RawImage hueSliderBackground;
        private RawImage valueSliderBackground;
        private RawImage alphaSliderBackground;
        private PaletteMaterialModifier paletteModifier;
        
        private bool isInitialized;

        // Cached values to avoid redundant UI dirties under Mask
        private float lastHue = float.NaN;
        private float lastValue = float.NaN;
        private bool? lastUseBicubic;
        private float lastAlpha = float.NaN;
        private Color lastAlphaColor;
        private float? lastModeValue;
        private Texture lastPaletteTexture;
        private float lastValueSlider = float.NaN;
        
        public void Initialize(RawImage paletteImg, RawImage hueSliderBg, 
                             RawImage valueSliderBg, RawImage alphaSliderBg)
        {
            paletteImage = paletteImg;
            hueSliderBackground = hueSliderBg;
            valueSliderBackground = valueSliderBg;
            alphaSliderBackground = alphaSliderBg;
            if (paletteImage != null)
            {
                paletteModifier = paletteImage.GetComponent<PaletteMaterialModifier>();
                if (paletteModifier == null)
                {
                    paletteModifier = paletteImage.gameObject.AddComponent<PaletteMaterialModifier>();
                }
            }
            CreateMaterials();
            ApplyMaterials();
            isInitialized = true;
        }
        
        private void CreateMaterials()
        {
            var paletteShader = Shader.Find(PALETTE_SHADER_NAME);
            if (paletteShader == null)
            {
                Debug.LogError($"{PALETTE_SHADER_NAME} shader not found!");
                return;
            }
            
            paletteMaterial = new Material(paletteShader);
            
            var sliderShader = Shader.Find(SLIDER_SHADER_NAME);
            if (sliderShader == null)
            {
                Debug.LogError($"{SLIDER_SHADER_NAME} shader not found!");
                return;
            }
            
            hueSliderMaterial = CreateSliderMaterial(sliderShader, 0);
            valueSliderMaterial = CreateSliderMaterial(sliderShader, 1);
            alphaSliderMaterial = CreateSliderMaterial(sliderShader, 2);
            alphaSliderMaterial.SetFloat(CheckerSize, CHECKER_SIZE);
            UpdateAlphaSliderDimensions();
        }
        
        private Material CreateSliderMaterial(Shader shader, float type)
        {
            var material = new Material(shader);
            material.SetFloat(Type, type);
            return material;
        }
        
        private void ApplyMaterials()
        {
            ApplyMaterialToImage(paletteImage, paletteMaterial);
            ApplyMaterialToImage(hueSliderBackground, hueSliderMaterial);
            ApplyMaterialToImage(valueSliderBackground, valueSliderMaterial);
            ApplyMaterialToImage(alphaSliderBackground, alphaSliderMaterial);
            InitializeDefaultProperties();
            if (paletteModifier != null)
            {
                paletteModifier.SetMode(paletteMaterial != null ? paletteMaterial.GetFloat(ShaderMode) : 0f);
                paletteModifier.SetHueValue(
                    paletteMaterial != null ? paletteMaterial.GetFloat(ShaderHueProperty) : 0f,
                    paletteMaterial != null ? paletteMaterial.GetFloat(ShaderValueProperty) : 1f);
                paletteModifier.SetUseBicubic(paletteMaterial != null && paletteMaterial.GetFloat(ShaderUseBicubicSampling) > 0.5f);
                if (paletteMaterial != null)
                {
                    paletteModifier.SetPaletteTexture(paletteMaterial.GetTexture(ShaderPaletteTexture));
                }
                
                paletteModifier.ForceApplyNow();
            }
        }
        
        private void InitializeDefaultProperties()
        {
            if (paletteMaterial != null)
            {
                paletteMaterial.SetFloat(ShaderMode, 0f);
                paletteMaterial.SetFloat(ShaderHueProperty, 0f);
                paletteMaterial.SetFloat(ShaderValueProperty, 1f);
                paletteMaterial.SetFloat(ShaderUseBicubicSampling, 0f);
            }
            
            if (alphaSliderMaterial != null)
            {
                alphaSliderMaterial.SetColor(ShaderCurrentColor, Color.white);
            }
        }
        
        private void ApplyMaterialToImage(RawImage image, Material material)
        {
            if (image != null && material != null)
            {
                image.material = material;
                image.texture = Texture2D.whiteTexture;
                image.SetMaterialDirty();
            }
        }
        
        public void UpdatePaletteMode(ColorPickerMode mode)
        {
            if (paletteMaterial != null)
            {
                float modeValue;
                if (mode is ColorPickerMode.Texture or ColorPickerMode.Palette)
                {
                    modeValue = 5f;
                }
                else
                {
                    modeValue = (int)mode;
                }
                
                if (!lastModeValue.HasValue || !Mathf.Approximately(lastModeValue.Value, modeValue))
                {
                    paletteMaterial.SetFloat(ShaderMode, modeValue);
                    lastModeValue = modeValue;
                    MarkImageMaterialDirty(paletteImage);
                }
                paletteModifier?.SetMode(modeValue);
                paletteModifier?.ForceApplyNow();
            }
            else if (isInitialized)
            {
                ForceReapplyMaterials();
                if (paletteMaterial != null)
                {
                    UpdatePaletteMode(mode);
                }
            }
        }
        
        public void UpdatePaletteTexture(Texture2D texture)
        {
            if (paletteMaterial != null)
            {
                if (lastPaletteTexture != texture)
                {
                    paletteMaterial.SetTexture(ShaderPaletteTexture, texture);
                    lastPaletteTexture = texture;
                    MarkImageMaterialDirty(paletteImage);
                }
                paletteModifier?.SetPaletteTexture(texture);
                paletteModifier?.ForceApplyNow();
            }
        }

        public void UpdateBicubicSampling(bool useBicubic)
        {
            if (paletteMaterial != null)
            {
                if (!lastUseBicubic.HasValue || lastUseBicubic.Value != useBicubic)
                {
                    paletteMaterial.SetFloat(ShaderUseBicubicSampling, useBicubic ? 1f : 0f);
                    lastUseBicubic = useBicubic;
                    MarkImageMaterialDirty(paletteImage);
                }
                paletteModifier?.SetUseBicubic(useBicubic);
                paletteModifier?.ForceApplyNow();
            }
        }
        
        public void UpdatePaletteProperties(float hue, float value)
        {
            if (paletteMaterial != null)
            {
                bool changed = !Mathf.Approximately(lastHue, hue) || !Mathf.Approximately(lastValue, value);
                if (changed)
                {
                    paletteMaterial.SetFloat(ShaderHueProperty, hue);
                    paletteMaterial.SetFloat(ShaderValueProperty, value);
                    lastHue = hue;
                    lastValue = value;
                    MarkImageMaterialDirty(paletteImage);
                }
                paletteModifier?.SetHueValue(hue, value);
                paletteModifier?.ForceApplyNow();
            }
            else if (isInitialized)
            {
                ForceReapplyMaterials();
            }
        }
        
        public void UpdateValueSlider(float currentValue)
        {
            if (valueSliderMaterial != null)
            {
                if (!Mathf.Approximately(lastValueSlider, currentValue))
                {
                    valueSliderMaterial.SetFloat(ShaderCurrentValue, currentValue);
                    lastValueSlider = currentValue;
                    MarkImageMaterialDirty(valueSliderBackground);
                }
            }
        }
        
        public void UpdateAlphaSlider(float alpha, Color currentColor)
        {
            if (alphaSliderMaterial != null)
            {
                bool colorChanged = !ColorsApproximatelyEqual(lastAlphaColor, currentColor);
                bool alphaChanged = !Mathf.Approximately(lastAlpha, alpha);
                if (colorChanged || alphaChanged)
                {
                    alphaSliderMaterial.SetFloat(ShaderCurrentValue, alpha);
                    alphaSliderMaterial.SetColor(ShaderCurrentColor, currentColor);
                    UpdateAlphaSliderDimensions();
                    lastAlpha = alpha;
                    lastAlphaColor = currentColor;
                    MarkImageMaterialDirty(alphaSliderBackground);
                }
            }
        }
        
        private void UpdateAlphaSliderDimensions()
        {
            if (alphaSliderMaterial != null && alphaSliderBackground != null)
            {
                var rect = alphaSliderBackground.rectTransform.rect;
                var dimensions = new Vector4(rect.width, rect.height, 0, 0);
                alphaSliderMaterial.SetVector(SliderDimensions, dimensions);
            }
        }
        
        public void ForceReapplyMaterials()
        {
            if (paletteMaterial == null || hueSliderMaterial == null || 
                valueSliderMaterial == null || alphaSliderMaterial == null)
            {
                CreateMaterials();
            }
            
            ApplyMaterials();
            MarkImageMaterialDirty(paletteImage);
            MarkImageMaterialDirty(hueSliderBackground);
            MarkImageMaterialDirty(valueSliderBackground);
            MarkImageMaterialDirty(alphaSliderBackground);
            lastHue = float.NaN;
            lastValue = float.NaN;
            lastUseBicubic = null;
            lastAlpha = float.NaN;
            lastModeValue = null;
            lastPaletteTexture = null;
            lastValueSlider = float.NaN;
        }
        
        public bool NeedsMaterialRecreation()
        {
            return paletteMaterial == null || 
                   hueSliderMaterial == null ||
                   valueSliderMaterial == null ||
                   alphaSliderMaterial == null ||
                   (paletteImage != null && paletteImage.material == null) ||
                   (hueSliderBackground != null && hueSliderBackground.material == null) ||
                   (valueSliderBackground != null && valueSliderBackground.material == null) ||
                   (alphaSliderBackground != null && alphaSliderBackground.material == null);
        }
        
        public void UpdateAllShaders(float hue, float value, float alpha, Color currentColor)
        {
            UpdatePaletteProperties(hue, value);
            UpdateValueSlider(value);
            UpdateAlphaSlider(alpha, currentColor);
        }
        
        public void Cleanup()
        {
            DestroyMaterial(paletteMaterial);
            DestroyMaterial(hueSliderMaterial);
            DestroyMaterial(valueSliderMaterial);
            DestroyMaterial(alphaSliderMaterial);
            
            paletteMaterial = null;
            hueSliderMaterial = null;
            valueSliderMaterial = null;
            alphaSliderMaterial = null;
            
            isInitialized = false;
        }
        
        private void DestroyMaterial(Material material)
        {
            if (material != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(material);
                }
                else
#endif
                {
                    Object.Destroy(material);
                }
            }
        }

        private static void MarkImageMaterialDirty(RawImage image)
        {
            if (image == null)
                return;
            
            image.SetMaterialDirty();
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }
    }
}