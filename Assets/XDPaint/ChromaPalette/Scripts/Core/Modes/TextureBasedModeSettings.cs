using System;
using UnityEngine;

namespace XDPaint.ChromaPalette.Core.Modes
{
    /// <summary>
    /// Base class for texture-based mode settings that share common sampling and snapping functionality.
    /// </summary>
    [Serializable]
    public abstract class TextureBasedModeSettings
    {
        [Header("Sampling Options")]
        public bool UseBicubicSampling = true;
        public bool SnapCursor = true;
        
        [Header("Selection Filtering")]
        public bool IgnoreTransparent = true;
        [Range(0f, 1f)] public float TransparentAlphaThreshold = 0.01f;
        [Tooltip("Optional list of colors that cannot be selected.")] public Color[] IgnoredColors = Array.Empty<Color>();
        [Range(0f, 1f)] public float IgnoreColorTolerance = 0.02f;
        
        public abstract Texture2D GetTexture();
    }
}
