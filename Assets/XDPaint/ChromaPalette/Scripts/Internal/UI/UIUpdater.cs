using UnityEngine;
using UnityEngine.UI;
using XDPaint.ChromaPalette.Internal.State;
using XDPaint.ChromaPalette.Internal.Rendering;

namespace XDPaint.ChromaPalette.Internal.UI
{
    /// <summary>
    /// Service responsible for coordinating all UI updates.
    /// Acts as a facade to simplify UI update operations.
    /// </summary>
    public class UIUpdater
    {
        private ColorStateStore colorState;
        private PaletteShaderBridge shaderManager;
        private InputFieldBindings inputFieldManager;
        private PaletteInteraction paletteInteractionHandler;
        
        private Slider hueSlider;
        private Slider valueSlider;
        private Slider alphaSlider;
        private Image colorImage;
        private Image oldColorImage;
        private Color lastCommittedColor = Color.white;
        
        private bool isUpdatingSliders;
        
        public void Initialize(
            ColorStateStore colorStateManager,
            PaletteShaderBridge shaderMgr,
            InputFieldBindings inputFieldMgr,
            PaletteInteraction paletteHandler,
            Slider hue, Slider value, Slider alpha,
            Image colorImg, Image oldColorImg)
        {
            colorState = colorStateManager;
            shaderManager = shaderMgr;
            inputFieldManager = inputFieldMgr;
            paletteInteractionHandler = paletteHandler;
            
            hueSlider = hue;
            valueSlider = value;
            alphaSlider = alpha;
            colorImage = colorImg;
            oldColorImage = oldColorImg;

            if (colorState != null)
            {
                lastCommittedColor = colorState.CurrentColor;
                colorState.OnColorChanged += HandleColorCommitted;
            }
        }
        
        public void UpdateAllUI()
        {
            if (colorState == null) return;
            
            UpdateSliders();
            UpdatePreview();
            UpdateShaders();
            UpdateInputFields();
            UpdateCursor();
        }
        
        public void UpdateUI(bool sliders = true, bool preview = true, 
                           bool shaders = true, bool inputFields = true, bool cursor = true)
        {
            if (colorState == null) return;
            
            if (sliders) UpdateSliders();
            if (preview) UpdatePreview();
            if (shaders) UpdateShaders();
            if (inputFields) UpdateInputFields();
            if (cursor) UpdateCursor();
        }
        
        public void UpdateSliders()
        {
            if (isUpdatingSliders || colorState == null) return;
            
            isUpdatingSliders = true;
            
            if (hueSlider != null)
                hueSlider.value = 1f - colorState.Hue;
            
            if (valueSlider != null)
                valueSlider.value = 1f - colorState.Value;
            
            if (alphaSlider != null)
                alphaSlider.value = colorState.Alpha;
            
            isUpdatingSliders = false;
        }
        
        public void UpdatePreview()
        {
            if (colorImage != null && colorState != null)
            {
                colorImage.color = colorState.CurrentColor;
            }
            
            if (oldColorImage != null)
            {
                oldColorImage.color = lastCommittedColor;
            }
        }

        private void HandleColorCommitted(UnityEngine.Color committed)
        {
            lastCommittedColor = committed;
            UpdatePreview();
        }
        
        public void UpdateShaders()
        {
            if (shaderManager == null || colorState == null)
                return;
            
            shaderManager.UpdateAllShaders(
                colorState.Hue, 
                colorState.Value, 
                colorState.Alpha, 
                colorState.CurrentColor);
        }
        
        public void UpdateInputFields()
        {
            inputFieldManager?.UpdateAllFields();
        }
        
        public void UpdateCursor()
        {
            if (paletteInteractionHandler == null || colorState == null)
                return;
            
            if (paletteInteractionHandler.IsDragging)
                return;
            
            paletteInteractionHandler.UpdateCursorFromColor(colorState.ColorHSV);
        }

        public void Cleanup()
        {
            if (colorState != null)
            {
                colorState.OnColorChanged -= HandleColorCommitted;
            }
        }
    }
}
