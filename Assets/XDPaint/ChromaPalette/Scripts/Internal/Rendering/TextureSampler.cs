using UnityEngine;
using XDPaint.ChromaPalette.Internal.UI;
using XDPaint.ChromaPalette.Internal.Modes;

namespace XDPaint.ChromaPalette.Internal.Rendering
{
    /// <summary>
    /// Service responsible for texture sampling and related operations.
    /// Manages texture updates, sampling settings, and palette texture configuration.
    /// </summary>
    public class TextureSampler
    {
        private PaletteShaderBridge shaderBridge;
        private PaletteInteraction paletteInteraction;
        private PaletteModeCoordinator modeCoordinator;
        
        public void Initialize(
            PaletteShaderBridge shaderMgr,
            PaletteInteraction interactionHandler,
            PaletteModeCoordinator modeMgr)
        {
            shaderBridge = shaderMgr;
            paletteInteraction = interactionHandler;
            modeCoordinator = modeMgr;
        }
        
        public void UpdateTextureDependentComponents()
        {
            if (modeCoordinator == null)
                return;
            
            var textureToUse = modeCoordinator.GetActiveTexture();
            UpdatePaletteTexture(textureToUse);
            paletteInteraction?.SetCurrentTexture(textureToUse);
        }
        
        public void UpdatePaletteTexture(Texture2D texture)
        {
            shaderBridge?.UpdatePaletteTexture(texture);
        }

        public void UpdateSamplingSettings()
        {
            if (modeCoordinator == null) return;
            
            var bicubicSampling = modeCoordinator.GetBicubicSampling();
            UpdateBicubicSampling(bicubicSampling);
        }
        
        public void UpdateBicubicSampling(bool useBicubic)
        {
            shaderBridge?.UpdateBicubicSampling(useBicubic);
        }
        
        public void SetPaletteTexture(Texture2D texture)
        {
            modeCoordinator?.SetPaletteTexture(texture);
            UpdateTextureDependentComponents();
        }
        
        public void UpdateTextureFromPalette()
        {
            UpdateTextureDependentComponents();
        }
    }
}