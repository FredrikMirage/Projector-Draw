using UnityEngine;
using UnityEditor;
using XDPaint.ChromaPalette.ScriptableObjects;
using XDPaint.ChromaPalette.Utilities;

namespace XDPaint.ChromaPalette.Editor
{
    /// <summary>
    /// Custom editor for ColorPalette ScriptableObject.
    /// Provides real-time palette preview and generation controls.
    /// </summary>
    [CustomEditor(typeof(ColorPalette))]
    public partial class ColorPaletteInspector : UnityEditor.Editor
    {
        private SerializedProperty paletteNameProp;
        private SerializedProperty paletteTypeProp;
        private SerializedProperty baseColorsProp;
        private SerializedProperty generatedColorsProp;
        private SerializedProperty settingsProp;
        private ColorPalette palette;
        private bool showTextureSettings;
        private bool showGenerationSettings = true;
        private bool showGeneratedColors = true;
        private bool showColorHarmony = true;
        
        private void OnEnable()
        {
            palette = (ColorPalette)target;
            paletteNameProp = serializedObject.FindProperty("paletteName");
            paletteTypeProp = serializedObject.FindProperty("paletteType");
            baseColorsProp = serializedObject.FindProperty("baseColors");
            generatedColorsProp = serializedObject.FindProperty("generatedColors");
            settingsProp = serializedObject.FindProperty("settings");
            ResetHandleTracking();
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            DrawBaseColors();
            DrawHarmonyWheel();
            DrawColorPreview();
            DrawTexturePreview();
            
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawBaseColors()
        {
            EditorGUILayout.PropertyField(paletteNameProp, new GUIContent("Name"));

            // Draw Palette Type with tooltips
            var paletteType = (PaletteType)paletteTypeProp.enumValueIndex;
            EditorGUI.BeginChangeCheck();
            var newType = (PaletteType)EditorGUILayout.EnumPopup(GetPaletteTypeContent(paletteType), paletteType);
            if (EditorGUI.EndChangeCheck())
            {
                paletteTypeProp.enumValueIndex = (int)newType;
            }

            // Only show Base Colors for types that actually use them
            if (ShouldShowBaseColors(paletteType))
            {
                EditorGUILayout.PropertyField(baseColorsProp, new GUIContent("Base Colors", GetBaseColorsTooltip(paletteType)));
            }

            DrawPaletteSettings();
        }

        private bool ShouldShowBaseColors(PaletteType type)
        {
            return type == PaletteType.Custom || type == PaletteType.Gradient ||
                   type == PaletteType.Monochromatic || type == PaletteType.Analogous;
        }

        private string GetBaseColorsTooltip(PaletteType type)
        {
            return type switch
            {
                PaletteType.Custom => "Define your custom colors directly",
                PaletteType.Gradient => "Colors to interpolate between",
                PaletteType.Monochromatic => "Base color for monochromatic variations",
                PaletteType.Analogous => "Starting color for analogous sequence",
                _ => "Base colors for palette generation"
            };
        }

        private GUIContent GetPaletteTypeContent(PaletteType type)
        {
            var tooltip = type switch
            {
                PaletteType.Complementary => "Two colors opposite on the color wheel (180° apart)",
                PaletteType.Triadic => "Three colors evenly spaced on the color wheel (120° apart)",
                PaletteType.Tetradic => "Four colors evenly spaced on the color wheel (90° apart)",
                PaletteType.Analogous => "Colors adjacent on the color wheel (typically 30° apart)",
                PaletteType.SplitComplementary => "Base color plus two colors adjacent to its complement",
                PaletteType.Monochromatic => "Different shades and tints of a single hue",
                PaletteType.Gradient => "Smooth gradient interpolation between colors",
                PaletteType.Custom => "Manually defined custom colors",
                _ => ""
            };
            return new GUIContent("Palette Type", tooltip);
        }

        private void DrawPaletteSettings()
        {
            if (settingsProp == null)
                return;

            var paletteType = (PaletteType)paletteTypeProp.enumValueIndex;

            // Generation Settings section
            showGenerationSettings = EditorGUILayout.Foldout(showGenerationSettings, "Generation Settings", true);
            if (!showGenerationSettings)
                return;
            var hasFixedColorCount = HasFixedColorCount(paletteType);
            var colorCountProp = settingsProp.FindPropertyRelative("ColorCount");
            if (hasFixedColorCount || paletteType == PaletteType.Custom)
            {
                GUI.enabled = false;
                var fixedCount = GetFixedColorCount(paletteType);
                var tooltip = paletteType == PaletteType.Custom ? $"Determined by number of base colors ({fixedCount})" : $"Fixed to {fixedCount} for {paletteType} type";
                EditorGUILayout.IntSlider(new GUIContent("Color Count", tooltip), fixedCount, 2, 64);
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.PropertyField(colorCountProp, new GUIContent("Color Count"));
            }
            
            DrawGenerationParameters(paletteType);

            // Add action buttons
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            var canPromoteGenerated = paletteType != PaletteType.Custom && palette.GeneratedColors is { Length: > 0 };
            using (new EditorGUI.DisabledScope(!canPromoteGenerated))
            {
                if (GUILayout.Button("Use Generated as Custom"))
                {
                    UseGeneratedAsBase();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
        
        private bool HasFixedColorCount(PaletteType type)
        {
            return type switch
            {
                PaletteType.Complementary => true,
                PaletteType.Triadic => true,
                PaletteType.Tetradic => true,
                PaletteType.SplitComplementary => true,
                _ => false
            };
        }
        
        private int GetFixedColorCount(PaletteType type)
        {
            return type switch
            {
                PaletteType.Complementary => 2,
                PaletteType.Triadic => 3,
                PaletteType.Tetradic => 4,
                PaletteType.SplitComplementary => 3,
                PaletteType.Custom => baseColorsProp?.arraySize ?? 1,
                _ => 5
            };
        }
        
        private void DrawGenerationParameters(PaletteType paletteType)
        {
            var saturationProp = settingsProp.FindPropertyRelative("SaturationVariation");
            var lightnessProp = settingsProp.FindPropertyRelative("LightnessVariation");
            var hueShiftProp = settingsProp.FindPropertyRelative("HueShift");
            switch (paletteType)
            {
                case PaletteType.Complementary:
                case PaletteType.Triadic:
                case PaletteType.Tetradic:
                case PaletteType.SplitComplementary:
                    // These types have fixed mathematical relationships - no variations needed
                    break;
                    
                case PaletteType.Monochromatic:
                    // Only saturation and lightness make sense for single-hue palettes
                    EditorGUILayout.PropertyField(saturationProp, new GUIContent("Saturation Range", "Variation in saturation across the palette"));
                    EditorGUILayout.PropertyField(lightnessProp, new GUIContent("Lightness Range", "Variation in lightness/value across the palette"));
                    break;
                    
                case PaletteType.Analogous:
                    // Hue shift controls spacing, saturation/lightness add subtle variations
                    EditorGUILayout.PropertyField(hueShiftProp, new GUIContent("Color Spacing", "Degrees between adjacent colors (default: 30°)"));
                    EditorGUILayout.PropertyField(saturationProp, new GUIContent("Saturation Variation", "Optional variation in saturation"));
                    EditorGUILayout.PropertyField(lightnessProp, new GUIContent("Lightness Variation", "Optional variation in lightness"));
                    break;
                    
                case PaletteType.Gradient:
                    // All parameters can be useful for gradient generation
                    EditorGUILayout.PropertyField(saturationProp, new GUIContent("Saturation Curve", "How saturation changes across the gradient"));
                    EditorGUILayout.PropertyField(lightnessProp, new GUIContent("Lightness Curve", "How lightness changes across the gradient"));
                    EditorGUILayout.PropertyField(hueShiftProp, new GUIContent("Hue Rotation", "Additional hue rotation across the gradient"));
                    break;

                case PaletteType.Custom:
                    // Custom uses exact base colors - no variations
                    break;
            }
        }

        private void DrawColorPreview()
        {
            if (palette.GeneratedColors is { Length: > 0 })
            {
                showGeneratedColors = EditorGUILayout.Foldout(showGeneratedColors, $"Generated Colors ({palette.GeneratedColors.Length})", true);
                if (showGeneratedColors)
                {
                    DrawColorSwatches();
                    EditorGUILayout.Space(5);
                    EditorGUI.indentLevel++;
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(generatedColorsProp, new GUIContent("Color Values"), true);
                    GUI.enabled = true;
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawColorSwatches()
        {
            const int swatchSize = 40;
            const int spacing = 2;
            const int marginForHelpBox = 40;
            
            var colors = palette.GeneratedColors;
            if (colors == null || colors.Length == 0)
                return;
            
            var availableWidth = EditorGUIUtility.currentViewWidth - marginForHelpBox;
            var swatchesPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (swatchSize + spacing)));
            for (var i = 0; i < colors.Length; i += swatchesPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                
                for (var j = 0; j < swatchesPerRow && i + j < colors.Length; j++)
                {
                    var color = colors[i + j];
                    
                    var swatchRect = GUILayoutUtility.GetRect(swatchSize, swatchSize, GUILayout.Width(swatchSize), GUILayout.Height(swatchSize));
                    EditorGUI.DrawRect(swatchRect, Color.black);
                    
                    var colorRect = new Rect(swatchRect.x + 1, swatchRect.y + 1, swatchRect.width - 2, swatchRect.height - 2);
                    EditorGUI.DrawRect(colorRect, color);
                    
                    if (Event.current.type == EventType.MouseDown && swatchRect.Contains(Event.current.mousePosition))
                    {
                        var hsv = ColorConversionUtility.RGBToHSV(color);
                        var cmyk = ColorConversionUtility.RGBToCMYK(color);
                        
                        EditorUtility.DisplayDialog("Color Info", 
                            $"Color #{i + j + 1}\n\n" +
                            $"RGB: ({color.r:F2}, {color.g:F2}, {color.b:F2})\n" +
                            $"HSV: ({hsv.H * 360f:F0}°, {hsv.S * 100f:F0}%, {hsv.V * 100f:F0}%)\n" +
                            $"CMYK: ({cmyk.C * 100f:F0}%, {cmyk.M * 100f:F0}%, {cmyk.Y * 100f:F0}%, {cmyk.K * 100f:F0}%)\n" +
                            $"Hex: #{ColorUtility.ToHtmlStringRGB(color)}", "OK");
                        Event.current.Use();
                    }
                    
                    if (j < swatchesPerRow - 1 && i + j + 1 < colors.Length)
                    {
                        GUILayout.Space(spacing);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
                
        private void DrawTexturePreview()
        {
            if (palette.GeneratedTexture != null)
            {
                showTextureSettings = EditorGUILayout.Foldout(showTextureSettings, "Texture Settings", true);
                if (showTextureSettings)
                {
                    DrawTextureSettings();
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Texture Preview", EditorStyles.boldLabel);
                    var maxPreviewHeight = 200f;
                    var texture = palette.GeneratedTexture;
                    EditorGUILayout.LabelField($"Size: {texture.width}x{texture.height}", EditorStyles.miniLabel);
                    var availableWidth = EditorGUIUtility.currentViewWidth;
                    var textureAspect = (float)texture.width / texture.height;
                    var previewWidth = availableWidth;
                    var previewHeight = previewWidth / textureAspect;
                    if (previewHeight > maxPreviewHeight)
                    {
                        previewHeight = maxPreviewHeight;
                        previewWidth = previewHeight * textureAspect;
                    }
                    
                    var minDisplaySize = 64f;
                    if (previewWidth < minDisplaySize && previewHeight < minDisplaySize)
                    {
                        if (textureAspect >= 1f)
                        {
                            previewWidth = minDisplaySize;
                            previewHeight = minDisplaySize / textureAspect;
                        }
                        else
                        {
                            previewHeight = minDisplaySize;
                            previewWidth = minDisplaySize * textureAspect;
                        }
                    }
                    
                    if (previewWidth > availableWidth)
                    {
                        EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(availableWidth), GUILayout.Height(previewHeight + 20));
                        var textureRect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
                        var oldFilter = texture.filterMode;
                        var layout = palette.Settings.Layout;
                        var previewFilter = layout switch
                        {
                            TextureLayout.Horizontal => palette.Settings.SmoothTransitions ? FilterMode.Bilinear : FilterMode.Point,
                            TextureLayout.Vertical => palette.Settings.SmoothTransitions ? FilterMode.Bilinear : FilterMode.Point,
                            TextureLayout.Grid => FilterMode.Point,
                            TextureLayout.Radial => FilterMode.Bilinear,
                            TextureLayout.Smooth => FilterMode.Bilinear,
                            _ => FilterMode.Point
                        };
                        texture.filterMode = previewFilter;
                        EditorGUI.DrawPreviewTexture(textureRect, texture, null, ScaleMode.StretchToFill);
                        texture.filterMode = oldFilter;
                        
                        EditorGUILayout.EndScrollView();
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        var textureRect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
                        var oldFilter = texture.filterMode;
                        var layout2 = palette.Settings.Layout;
                        var previewFilter2 = layout2 switch
                        {
                            TextureLayout.Horizontal => palette.Settings.SmoothTransitions ? FilterMode.Bilinear : FilterMode.Point,
                            TextureLayout.Vertical => palette.Settings.SmoothTransitions ? FilterMode.Bilinear : FilterMode.Point,
                            TextureLayout.Grid => FilterMode.Point,
                            TextureLayout.Radial => FilterMode.Bilinear,
                            TextureLayout.Smooth => FilterMode.Bilinear,
                            _ => FilterMode.Point
                        };
                        texture.filterMode = previewFilter2;
                        EditorGUI.DrawPreviewTexture(textureRect, texture, null, ScaleMode.StretchToFill);
                        texture.filterMode = oldFilter;
                        
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("Generated Texture", texture, typeof(Texture2D), false);
                    GUI.enabled = true;
                }
            }
        }

        private void DrawTextureSettings()
        {
            var layoutProp = settingsProp.FindPropertyRelative("Layout");
            var smoothProp = settingsProp.FindPropertyRelative("SmoothTransitions");
            var autoSizeProp = settingsProp.FindPropertyRelative("AutoSize");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(layoutProp, new GUIContent("Layout", "How colors are arranged in the texture"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                palette.RegenerateTexture();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(smoothProp, new GUIContent("Smooth Transitions", "Enable smooth interpolation between colors"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                palette.RegenerateTexture();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(autoSizeProp, new GUIContent("Auto Size", "Automatically determine texture dimensions"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                palette.RegenerateTexture();
            }

            if (!autoSizeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                var widthProp = settingsProp.FindPropertyRelative("TextureWidth");
                var heightProp = settingsProp.FindPropertyRelative("TextureHeight");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(widthProp, new GUIContent("Width"));
                EditorGUILayout.PropertyField(heightProp, new GUIContent("Height"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    palette.RegenerateTexture();
                }
                EditorGUI.indentLevel--;
            }

            var rotationProp = settingsProp.FindPropertyRelative("RotationAngle");
            if (rotationProp != null)
            {
                var rotationOptions = new[] { "0°", "90°", "180°", "270°" };
                var rotationValues = new[] { 0, 90, 180, 270 };
                var currentIndex = System.Array.IndexOf(rotationValues, rotationProp.intValue);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                var newIndex = EditorGUILayout.Popup(new GUIContent("Rotation", "Rotate the texture in 90-degree increments"), currentIndex, rotationOptions);
                if (newIndex != currentIndex)
                {
                    rotationProp.intValue = rotationValues[newIndex];
                    serializedObject.ApplyModifiedProperties();
                    palette.RegenerateTexture();
                }
            }
        }

        private void UseGeneratedAsBase()
        {
            if (palette.GeneratedColors == null || palette.GeneratedColors.Length == 0)
                return;

            Undo.RecordObject(palette, "Use Generated Colors as Custom");
            palette.SetBaseColors(palette.GeneratedColors);
            palette.SetPaletteType(PaletteType.Custom);
            serializedObject.Update();
        }
    }
}