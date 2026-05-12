using System;
using UnityEngine;

namespace XDPaint.ChromaPalette.Core.Modes
{
    [Serializable]
    public class TextureModeSettings : TextureBasedModeSettings
    {
        [Header("Texture Source")]
        public Texture2D Texture;
        
        public override Texture2D GetTexture()
        {
            return Texture;
        }
    }
}