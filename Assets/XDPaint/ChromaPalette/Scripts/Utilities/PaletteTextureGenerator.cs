using UnityEngine;
using XDPaint.ChromaPalette.ScriptableObjects;
using Object = UnityEngine.Object;

namespace XDPaint.ChromaPalette.Utilities
{
    /// <summary>
    /// Utility class for generating textures from color palette data.
    /// Creates various texture layouts for use with the texture-based color picker.
    /// </summary>
    public static class PaletteTextureGenerator
    {
        /// <summary>
        /// Create a texture representation of the color palette.
        /// </summary>
        public static Texture2D CreatePaletteTexture(Color[] colors, PaletteSettings settings)
        {
            if (colors == null || colors.Length == 0)
                return FinalizeCreatedTexture(CreateSolidTexture(Color.white, 256, 256), settings);
                
            var (width, height) = CalculateOptimalSize(colors, settings);
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            
            var texture = settings.Layout switch
            {
                TextureLayout.Horizontal => CreateHorizontalTexture(colors, width, height, settings.SmoothTransitions),
                TextureLayout.Vertical => CreateVerticalTexture(colors, width, height, settings.SmoothTransitions),
                TextureLayout.Grid => CreateGridTexture(colors, width, height),
                TextureLayout.Radial => CreateRadialTexture(colors, width, height),
                TextureLayout.Smooth => CreateSmoothTexture(colors, width, height),
                _ => CreateHorizontalTexture(colors, width, height, settings.SmoothTransitions)
            };
            
            if (settings.RotationAngle > 0)
            {
                texture = RotateTexture(texture, settings.RotationAngle);
            }

            texture = FinalizeCreatedTexture(texture, settings);
            return texture;
        }

        /// <summary>
        /// Apply common safety settings to reduce memory usage and make the texture
        /// behave like a generated runtime asset (not saved to disk, no CPU copy).
        /// </summary>
        private static Texture2D FinalizeCreatedTexture(Texture2D texture, PaletteSettings settings)
        {
            if (texture == null)
                return null;

            // Default sampling: respect SmoothTransitions for 1D layouts
            if (settings.Layout is TextureLayout.Horizontal or TextureLayout.Vertical)
            {
                texture.filterMode = settings.SmoothTransitions ? FilterMode.Bilinear : FilterMode.Point;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 0;
            texture.hideFlags = HideFlags.HideAndDontSave;
            if (texture.isReadable)
            {
                texture.Apply(false, false);
            }

            return texture;
        }
        
        /// <summary>
        /// Calculate optimal texture size based on palette data.
        /// </summary>
        private static (int width, int height) CalculateOptimalSize(Color[] colors, PaletteSettings settings)
        {
            if (!settings.AutoSize)
                return (settings.TextureWidth, settings.TextureHeight);
            
            var minRes = 64;
            var perColor = 8;
            var maxRes = 2048;
            switch (settings.Layout)
            {
                case TextureLayout.Horizontal:
                    if (settings.SmoothTransitions)
                    {
                        var w = Mathf.Clamp(colors.Length * perColor, minRes, maxRes);
                        return (w, 1);
                    }
                    return (colors.Length, 1);
                case TextureLayout.Vertical:
                    if (settings.SmoothTransitions)
                    {
                        var h = Mathf.Clamp(colors.Length * perColor, minRes, maxRes);
                        return (1, h);
                    }
                    return (1, colors.Length);
                case TextureLayout.Grid:
                    return GetGridSize(colors.Length);
                case TextureLayout.Radial:
                    return (256, 256);
                case TextureLayout.Smooth:
                    return (256, 256);
                default:
                    return (colors.Length, 1);
            }
        }
        
        /// <summary>
        /// Calculate optimal grid size for color array.
        /// </summary>
        private static (int width, int height) GetGridSize(int colorCount)
        {
            if (colorCount <= 1)
                return (1, 1);
            
            var sqrtCount = Mathf.Sqrt(colorCount);
            var initialWidth = Mathf.CeilToInt(sqrtCount);
            var initialHeight = Mathf.CeilToInt((float)colorCount / initialWidth);
            
            var bestWidth = initialWidth;
            var bestHeight = initialHeight;
            var bestWaste = initialWidth * initialHeight - colorCount;
            var bestAspectDiff = Mathf.Abs(initialWidth - initialHeight);
            
            for (var delta = 0; delta <= colorCount; delta++)
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var w = Mathf.RoundToInt(sqrtCount) + (sign * delta);
                    if (w < 1 || w > colorCount)
                        continue;
                    
                    var h = Mathf.CeilToInt((float)colorCount / w);
                    var waste = w * h - colorCount;
                    var aspectDiff = Mathf.Abs(w - h);
                    var isBetter = false;
                    if (waste < bestWaste)
                    {
                        isBetter = true;
                    }
                    else if (waste == bestWaste && aspectDiff < bestAspectDiff)
                    {
                        isBetter = true;
                    }
                    
                    if (isBetter)
                    {
                        bestWidth = w;
                        bestHeight = h;
                        bestWaste = waste;
                        bestAspectDiff = aspectDiff;
                        
                        if (waste == 0 && aspectDiff == 0)
                            return (bestWidth, bestHeight);
                    }
                }
                
                if (bestWaste == 0 && bestAspectDiff <= 3)
                    break;
            }
            
            return (bestWidth, bestHeight);
        }
        
        /// <summary>
        /// Create horizontal stripe texture.
        /// </summary>
        private static Texture2D CreateHorizontalTexture(Color[] colors, int width, int height, bool smoothTransitions)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Color color;
                    if (smoothTransitions)
                    {
                        var t = (float)x / (width - 1);
                        color = InterpolateColors(colors, t);
                    }
                    else
                    {
                        var colorIndex = width == colors.Length ? x : Mathf.FloorToInt((float)x / width * colors.Length);
                        colorIndex = Mathf.Clamp(colorIndex, 0, colors.Length - 1);
                        color = colors[colorIndex];
                    }
                    
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.filterMode = smoothTransitions ? FilterMode.Bilinear : FilterMode.Point;
            return texture;
        }
        
        /// <summary>
        /// Create vertical stripe texture.
        /// </summary>
        private static Texture2D CreateVerticalTexture(Color[] colors, int width, int height, bool smoothTransitions)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Color color;
                    if (smoothTransitions)
                    {
                        var t = (float)y / (height - 1);
                        color = InterpolateColors(colors, t);
                    }
                    else
                    {
                        var colorIndex = height == colors.Length ? y : Mathf.FloorToInt((float)y / height * colors.Length);
                        colorIndex = Mathf.Clamp(colorIndex, 0, colors.Length - 1);
                        color = colors[colorIndex];
                    }
                    
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.filterMode = smoothTransitions ? FilterMode.Bilinear : FilterMode.Point;
            return texture;
        }
        
        /// <summary>
        /// Create grid-based texture with color tiles.
        /// </summary>
        private static Texture2D CreateGridTexture(Color[] colors, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var maxColors = width * height;
            var colorsToPlace = Mathf.Min(colors.Length, maxColors);
            var gridCols = width;
            var gridRows = height;
            if (colorsToPlace < maxColors)
            {
                var optimalGrid = GetGridSize(colorsToPlace);
                if (optimalGrid.width <= width && optimalGrid.height <= height)
                {
                    gridCols = optimalGrid.width;
                    gridRows = optimalGrid.height;
                }
            }
            
            var tileWidth = Mathf.Max(1, width / gridCols);
            var tileHeight = Mathf.Max(1, height / gridRows);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var gridX = Mathf.Min(x / tileWidth, gridCols - 1);
                    var gridY = Mathf.Min(y / tileHeight, gridRows - 1);
                    var invertedGridY = gridRows - 1 - gridY;
                    var colorIndex = invertedGridY * gridCols + gridX;
                    var color = colorIndex < colors.Length ? colors[colorIndex] : Color.clear;
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return texture;
        }
        
        /// <summary>
        /// Create radial/circular texture with colors radiating from center.
        /// </summary>
        private static Texture2D CreateRadialTexture(Color[] colors, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var center = new Vector2(width * 0.5f, height * 0.5f);
            var maxDistance = Mathf.Min(width, height) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pos = new Vector2(x, y);
                    var distance = Vector2.Distance(pos, center);
                    var t = Mathf.Clamp01(distance / maxDistance);
                    var color = InterpolateColors(colors, t);
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        
        /// <summary>
        /// Create smooth 2D gradient texture mixing all colors.
        /// </summary>
        private static Texture2D CreateSmoothTexture(Color[] colors, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = (float)x / (width - 1);
                    var v = (float)y / (height - 1);
                    var horizontalColor = InterpolateColors(colors, u);
                    var verticalColor = InterpolateColors(colors, v);
                    var finalColor = Color.Lerp(horizontalColor, verticalColor, 0.5f);
                    texture.SetPixel(x, y, finalColor);
                }
            }
            
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        
        /// <summary>
        /// Create a solid color texture.
        /// </summary>
        private static Texture2D CreateSolidTexture(Color color, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return texture;
        }
        
        /// <summary>
        /// Interpolate between colors in an array.
        /// </summary>
        private static Color InterpolateColors(Color[] colors, float t)
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
            
            return Color.Lerp(colors[segment], colors[segment + 1], localT);
        }
        
        /// <summary>
        /// Rotate a texture by the specified angle (90, 180, or 270 degrees).
        /// </summary>
        private static Texture2D RotateTexture(Texture2D original, int angle)
        {
            if (angle == 0)
                return original;
            
            angle = angle % 360 / 90 * 90;
            if (angle < 0)
                angle += 360;
            
            int newWidth;
            int newHeight;
            if (angle is 90 or 270)
            {
                newWidth = original.height;
                newHeight = original.width;
            }
            else
            {
                newWidth = original.width;
                newHeight = original.height;
            }
            
            var rotated = new Texture2D(newWidth, newHeight, original.format, false);
            var originalPixels = original.GetPixels();
            var rotatedPixels = new Color[newWidth * newHeight];
            for (var y = 0; y < original.height; y++)
            {
                for (var x = 0; x < original.width; x++)
                {
                    var sourceIndex = y * original.width + x;
                    var color = originalPixels[sourceIndex];
                    
                    int newX, newY;
                    
                    switch (angle)
                    {
                        case 90:
                            newX = original.height - 1 - y;
                            newY = x;
                            break;
                        case 180:
                            newX = original.width - 1 - x;
                            newY = original.height - 1 - y;
                            break;
                        case 270:
                            newX = y;
                            newY = original.width - 1 - x;
                            break;
                        default:
                            newX = x;
                            newY = y;
                            break;
                    }
                    
                    var targetIndex = newY * newWidth + newX;
                    rotatedPixels[targetIndex] = color;
                }
            }
            
            rotated.SetPixels(rotatedPixels);
            rotated.Apply();
            rotated.filterMode = original.filterMode;
            
            if (Application.isPlaying)
            {
                Object.Destroy(original);
            }
            else
            {
                Object.DestroyImmediate(original);
            }
            
            return rotated;
        }
    }
}