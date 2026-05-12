using System;
using UnityEngine;
using XDPaint.ChromaPalette.ScriptableObjects;

namespace XDPaint.ChromaPalette.Core.Modes
{
    [Serializable]
    public class PaletteModeSettings : TextureBasedModeSettings
    {
        [Header("Palette Source")]
        public ColorPalette ColorPalette;
        
        public override Texture2D GetTexture()
        {
            if (ColorPalette == null)
                return null;
                
            if (ColorPalette.GeneratedColors == null || ColorPalette.GeneratedColors.Length == 0)
            {
                ColorPalette.GeneratePalette();
            }
            
            return ColorPalette.GetOrCreateTexture();
        }
    }
}