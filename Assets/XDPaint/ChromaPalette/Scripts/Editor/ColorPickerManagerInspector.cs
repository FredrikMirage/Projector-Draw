using UnityEditor;
using UnityEngine;
using XDPaint.ChromaPalette.Core;

namespace XDPaint.ChromaPalette.Editor
{
    [CustomEditor(typeof(ColorPickerManager))]
    public class ColorPickerManagerInspector : UnityEditor.Editor
    {
        private SerializedProperty mode;
        private SerializedProperty textureSettings;
        private SerializedProperty paletteSettings;

        // Foldout states
        private bool paletteFoldout = false;
        private bool slidersFoldout = false;
        private bool previewFoldout = false;
        private bool inputFieldsFoldout = false;
        
        // UI References (now direct fields)
        private SerializedProperty paletteImage;
        private SerializedProperty paletteRect;
        private SerializedProperty paletteCursor;
        private SerializedProperty paletteCursor2;
        private SerializedProperty cursorContrastModeProperty;
        private SerializedProperty hueSlider;
        private SerializedProperty hueSliderBackground;
        private SerializedProperty valueSlider;
        private SerializedProperty valueSliderBackground;
        private SerializedProperty alphaSlider;
        private SerializedProperty alphaSliderBackground;
        private SerializedProperty colorImage;
        private SerializedProperty previousColorImage;
        
        // Input Fields (now direct fields)
        private SerializedProperty inputH;
        private SerializedProperty inputS;
        private SerializedProperty inputV;
        private SerializedProperty inputR;
        private SerializedProperty inputG;
        private SerializedProperty inputB;
        private SerializedProperty inputC;
        private SerializedProperty inputM;
        private SerializedProperty inputY;
        private SerializedProperty inputK;
        private SerializedProperty inputHex;
        private SerializedProperty inputAlpha;
        
        private SerializedProperty onColorChanging;
        private SerializedProperty onColorChanged;

        private void OnEnable()
        {
            mode = serializedObject.FindProperty("mode");
            textureSettings = serializedObject.FindProperty("textureSettings");
            paletteSettings = serializedObject.FindProperty("paletteSettings");
            
            // UI References (now direct fields)
            paletteImage = serializedObject.FindProperty("paletteImage");
            paletteRect = serializedObject.FindProperty("paletteRect");
            paletteCursor = serializedObject.FindProperty("paletteCursor");
            paletteCursor2 = serializedObject.FindProperty("paletteCursorExternal");
            cursorContrastModeProperty = serializedObject.FindProperty("cursorContrastMode");
            hueSlider = serializedObject.FindProperty("hueSlider");
            hueSliderBackground = serializedObject.FindProperty("hueSliderBackground");
            valueSlider = serializedObject.FindProperty("valueSlider");
            valueSliderBackground = serializedObject.FindProperty("valueSliderBackground");
            alphaSlider = serializedObject.FindProperty("alphaSlider");
            alphaSliderBackground = serializedObject.FindProperty("alphaSliderBackground");
            colorImage = serializedObject.FindProperty("colorImage");
            previousColorImage = serializedObject.FindProperty("previousColorImage");
            
            // Input Fields (now direct fields)
            inputH = serializedObject.FindProperty("inputH");
            inputS = serializedObject.FindProperty("inputS");
            inputV = serializedObject.FindProperty("inputV");
            inputR = serializedObject.FindProperty("inputR");
            inputG = serializedObject.FindProperty("inputG");
            inputB = serializedObject.FindProperty("inputB");
            inputC = serializedObject.FindProperty("inputC");
            inputM = serializedObject.FindProperty("inputM");
            inputY = serializedObject.FindProperty("inputY");
            inputK = serializedObject.FindProperty("inputK");
            inputHex = serializedObject.FindProperty("inputHex");
            inputAlpha = serializedObject.FindProperty("inputAlpha");
            
            onColorChanging = serializedObject.FindProperty("onColorChanging");
            onColorChanged = serializedObject.FindProperty("onColorChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(mode, new GUIContent("Palette Mode", "How the color palette is rendered and interacted with (Rectangle, Circle, Texture/Palette, CircleFull, CircleTriangle, CircleCircle)."));
            var selectedMode = (ColorPickerMode)mode.enumValueIndex;
            EditorGUI.indentLevel++;
            switch (selectedMode)
            {
                case ColorPickerMode.Rectangle:
                case ColorPickerMode.Circle:
                    break;
                case ColorPickerMode.Texture:
                    DrawTextureSettings();
                    break;
                case ColorPickerMode.Palette:
                    DrawPaletteSettings();
                    break;
                case ColorPickerMode.CircleTriangle:
                case ColorPickerMode.CircleCircle:
                    EditorGUILayout.HelpBox("PaletteCursor2 is used as the hue ring cursor in CircleTriangle and CircleCircle modes (optional).", MessageType.Info);
                    break;
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            DrawUIReferences();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(onColorChanging);
            EditorGUILayout.PropertyField(onColorChanged);

            serializedObject.ApplyModifiedProperties();
        }


        private void DrawTextureSettings()
        {
            DrawTextureBasedSettings(
                textureSettings,
                "Snap to Pixel Center",
                () =>
                {
                    var texture = textureSettings.FindPropertyRelative("Texture");
                    if (texture != null)
                    {
                        EditorGUILayout.PropertyField(texture);
                    }
                },
                "High-quality filtering for smooth color transitions between palette colors"
            );
        }

        private void DrawPaletteSettings()
        {
            DrawTextureBasedSettings(
                paletteSettings,
                "Snap to Pixel Center",
                () =>
                {
                    var colorPalette = paletteSettings.FindPropertyRelative("ColorPalette");
                    if (colorPalette != null)
                    {
                        EditorGUILayout.PropertyField(colorPalette);
                        if (colorPalette.objectReferenceValue != null)
                        {
                            var palette = colorPalette.objectReferenceValue as ScriptableObjects.ColorPalette;
                            if (palette != null && palette.GeneratedColors != null)
                            {
                                EditorGUILayout.LabelField($"Colors: {palette.GeneratedColors.Length}", EditorStyles.miniLabel);
                            }
                        }
                    }
                },
                "High-quality filtering for smooth color transitions between palette colors"
            );
        }

        private void DrawTextureBasedSettings(SerializedProperty settings, string snapLabel, System.Action drawSourceSection, string bicubicTooltip)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            drawSourceSection?.Invoke();

            var useBicubic = settings.FindPropertyRelative("UseBicubicSampling");
            var snapCursor = settings.FindPropertyRelative("SnapCursor");
            var ignoreTransparent = settings.FindPropertyRelative("IgnoreTransparent");
            var transparentAlphaThreshold = settings.FindPropertyRelative("TransparentAlphaThreshold");
            var ignoredColors = settings.FindPropertyRelative("IgnoredColors");
            var ignoreColorTolerance = settings.FindPropertyRelative("IgnoreColorTolerance");
            if (useBicubic != null)
            {
                EditorGUILayout.PropertyField(useBicubic, new GUIContent("Bicubic Sampling", bicubicTooltip));
            }

            if (snapCursor != null)
            {
                EditorGUILayout.PropertyField(snapCursor, new GUIContent(snapLabel));
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Selection Filtering", EditorStyles.boldLabel);
            if (ignoreTransparent != null)
            {
                EditorGUILayout.PropertyField(ignoreTransparent, new GUIContent("Ignore Transparent"));
            }
            
            if (transparentAlphaThreshold != null && (ignoreTransparent?.boolValue ?? false))
            {
                EditorGUILayout.Slider(transparentAlphaThreshold, 0f, 1f, new GUIContent("Alpha Threshold"));
            }
            
            if (ignoredColors != null)
            {
                EditorGUILayout.PropertyField(ignoredColors, new GUIContent("Ignored Colors"), true);
            }
            
            if (ignoreColorTolerance != null && ignoredColors != null && ignoredColors.arraySize > 0)
            {
                EditorGUILayout.Slider(ignoreColorTolerance, 0f, 1f, new GUIContent("Color Tolerance"));
            }

            EditorGUILayout.EndVertical();
        }
        
        private void DrawUIReferences()
        {
            // Palette section
            paletteFoldout = EditorGUILayout.Foldout(paletteFoldout, "Palette UI", true);
            if (paletteFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(paletteImage);
                EditorGUILayout.PropertyField(paletteRect);
                EditorGUILayout.PropertyField(paletteCursor);
                EditorGUILayout.PropertyField(paletteCursor2, new GUIContent("Palette Cursor 2", "Used as hue ring cursor in CircleTriangle mode (optional)"));
                EditorGUILayout.PropertyField(cursorContrastModeProperty, new GUIContent("Cursor Contrast Mode", "Controls how cursor colors are adjusted for readability."));
                EditorGUI.indentLevel--;
            }

            // Sliders section
            slidersFoldout = EditorGUILayout.Foldout(slidersFoldout, "Sliders", true);
            if (slidersFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(hueSlider);
                EditorGUILayout.PropertyField(hueSliderBackground);
                EditorGUILayout.PropertyField(valueSlider);
                EditorGUILayout.PropertyField(valueSliderBackground);
                EditorGUILayout.PropertyField(alphaSlider);
                EditorGUILayout.PropertyField(alphaSliderBackground);
                EditorGUI.indentLevel--;
            }

            // Preview section
            previewFoldout = EditorGUILayout.Foldout(previewFoldout, "Preview", true);
            if (previewFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(colorImage);
                EditorGUILayout.PropertyField(previousColorImage);
                EditorGUI.indentLevel--;
            }

            // Input Fields section
            inputFieldsFoldout = EditorGUILayout.Foldout(inputFieldsFoldout, "Input Fields", true);
            if (inputFieldsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawInputFields();
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawInputFields()
        {
            // HSV fields
            EditorGUILayout.LabelField("HSV Fields", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(inputH);
            EditorGUILayout.PropertyField(inputS);
            EditorGUILayout.PropertyField(inputV);
            
            // RGB fields
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RGB Fields", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(inputR);
            EditorGUILayout.PropertyField(inputG);
            EditorGUILayout.PropertyField(inputB);
            
            // CMYK fields
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("CMYK Fields", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(inputC);
            EditorGUILayout.PropertyField(inputM);
            EditorGUILayout.PropertyField(inputY);
            EditorGUILayout.PropertyField(inputK);
            
            // Other fields
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Other Fields", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(inputHex);
            EditorGUILayout.PropertyField(inputAlpha);
        }
    }
}
