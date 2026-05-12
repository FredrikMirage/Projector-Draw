using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.ScriptableObjects;
using XDPaint.ChromaPalette.Utilities;

namespace XDPaint.ChromaPalette.Editor
{
    public partial class ColorPaletteInspector
    {
        private readonly List<PaletteHandleData> wheelHandles = new();
        private Vector2 wheelCenter;
        private float wheelRadius;
        private Rect wheelRect;
        private HandleSelection activeHandle;
        private HandleSelection selectedHandle;
        private PaletteType activeHandleType;
        private bool wheelHandlesUseBaseColors;
        private PaletteType lastWheelType;
        private float wheelMaxValue = 1f;
        private Vector2 activeHandleDragOffset;
        private bool hasActiveHandleDragOffset;

        private const float WheelPadding = 12f;
        private const float HandleRadius = 8f;

        private struct HandleSelection
        {
            public int HandleIndex;
            public int BaseColorIndex;
            public int ColorIndex;
            public float HueOffset;
            public bool MaintainsHarmony;

            public bool HasHandle => HandleIndex >= 0;
            public bool HasBaseColor => BaseColorIndex >= 0 && ColorIndex >= 0;

            public void Reset()
            {
                HandleIndex = -1;
                BaseColorIndex = -1;
                ColorIndex = -1;
                HueOffset = 0f;
                MaintainsHarmony = false;
            }
        }

        private struct PaletteHandleData
        {
            public int ColorIndex;
            public int BaseColorIndex;
            public float Angle;
            public Color Color;
            public Vector2 Position;
            public Rect Rect;
            public bool Draggable;
            public bool MaintainsHarmony;
            public float HueOffset;
            public float Saturation;
            public float Value;
        }

        private void ResetHandleTracking()
        {
            activeHandle.Reset();
            selectedHandle.Reset();
            activeHandleType = (PaletteType)(-1);
            hasActiveHandleDragOffset = false;
            activeHandleDragOffset = Vector2.zero;
        }

        private void ClearActiveHandle()
        {
            activeHandle.Reset();
            activeHandleType = (PaletteType)(-1);
            hasActiveHandleDragOffset = false;
            activeHandleDragOffset = Vector2.zero;
        }

        private void DeselectHandle(bool repaint = false)
        {
            selectedHandle.Reset();
            if (repaint)
            {
                Repaint();
            }
        }

        private static bool HandleMatchesSelection(in PaletteHandleData data, HandleSelection selection)
        {
            if (!selection.HasBaseColor)
                return false;

            if (data.BaseColorIndex != selection.BaseColorIndex || data.ColorIndex != selection.ColorIndex)
                return false;

            if (data.MaintainsHarmony != selection.MaintainsHarmony)
                return false;

            return Mathf.Abs(Mathf.DeltaAngle(data.HueOffset, selection.HueOffset)) <= 0.1f;
        }

        private void SyncSelection(ref HandleSelection selection)
        {
            if (!selection.HasBaseColor)
            {
                selection.Reset();
                return;
            }

            for (var i = 0; i < wheelHandles.Count; i++)
            {
                var data = wheelHandles[i];
                if (!data.Draggable)
                    continue;
                if (!HandleMatchesSelection(data, selection))
                    continue;

                selection.HandleIndex = i;
                selection.HueOffset = data.HueOffset;
                selection.MaintainsHarmony = data.MaintainsHarmony;
                return;
            }

            selection.Reset();
        }

        private static HandleSelection CreateSelectionState(int handleIndex, in PaletteHandleData data)
        {
            return new HandleSelection
            {
                HandleIndex = handleIndex,
                BaseColorIndex = data.BaseColorIndex,
                ColorIndex = data.ColorIndex,
                HueOffset = data.HueOffset,
                MaintainsHarmony = data.MaintainsHarmony
            };
        }
        
		private void DrawHarmonyWheel()
        {
            var paletteType = (PaletteType)paletteTypeProp.enumValueIndex;
            if (paletteType != lastWheelType)
            {
                ResetHandleTracking();
                lastWheelType = paletteType;
            }
            var colors = GetWheelColors(paletteType);
            if (colors == null || colors.Length == 0)
                return;

            wheelMaxValue = 1f;
            if (IsLinkedPalette(paletteType))
            {
                var baseColors = palette.BaseColors;
                if (baseColors != null && baseColors.Length > 0)
                {
                    var baseHsv = ColorConversionUtility.RGBToHSV(baseColors[0]);
                    wheelMaxValue = Mathf.Clamp01(baseHsv.V);
                }
            }

            showColorHarmony = EditorGUILayout.Foldout(showColorHarmony, "Color Harmony", true);

            if (!showColorHarmony)
                return;

            EditorGUI.indentLevel++;

            var maxSize = Mathf.Clamp(EditorGUIUtility.currentViewWidth - 140f, 160f, 260f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            wheelRect = GUILayoutUtility.GetRect(maxSize, maxSize, GUILayout.Width(maxSize), GUILayout.Height(maxSize));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            wheelCenter = new Vector2(wheelRect.x + wheelRect.width * 0.5f, wheelRect.y + wheelRect.height * 0.5f);
            wheelRadius = Mathf.Min(wheelRect.width, wheelRect.height) * 0.5f - WheelPadding;
            if (wheelRadius <= 0f)
                return;

            BuildWheelHandles(paletteType, colors);
            if (activeHandleType == paletteType)
            {
                SyncSelection(ref activeHandle);
            }
            else
            {
                ClearActiveHandle();
            }

            SyncSelection(ref selectedHandle);
            var paletteChanged = HandleWheelInput(Event.current, paletteType);
            if (paletteChanged)
            {
                colors = GetWheelColors(paletteType);
                BuildWheelHandles(paletteType, colors);
                if (activeHandleType == paletteType)
                {
                    SyncSelection(ref activeHandle);
                }
                else
                {
                    ClearActiveHandle();
                }

                SyncSelection(ref selectedHandle);
            }

            DrawWheelBackground();
            DrawWheelConnections(paletteType);
            DrawWheelHandles();
            DrawValueSlider(paletteType);

            EditorGUI.indentLevel--;
        }

        private Color[] GetWheelColors(PaletteType type)
        {
            if (UseBaseColorsForHandles(type))
            {
                var baseColors = palette.BaseColors;
                if (baseColors is { Length: > 0 })
                    return baseColors;
            }
            
            var generated = palette.GeneratedColors;
            if (generated == null || generated.Length == 0)
            {
                palette.GeneratePalette();
                generated = palette.GeneratedColors;
            }

            return generated;
        }

        private bool UseBaseColorsForHandles(PaletteType type)
        {
            return type == PaletteType.Custom || type == PaletteType.Gradient;
        }

        private void BuildWheelHandles(PaletteType type, Color[] colors)
        {
            wheelHandles.Clear();
            if (colors == null)
                return;

            wheelHandlesUseBaseColors = UseBaseColorsForHandles(type);
            var linkedPalette = IsLinkedPalette(type);
            var baseColors = palette.BaseColors ?? Array.Empty<Color>();
            if (linkedPalette && baseColors.Length == 0)
            {
                linkedPalette = false;
            }
            var baseIndex = wheelHandlesUseBaseColors ? -1 : FindBaseHandleIndex(colors, baseColors);
            var resolvedBaseIndex = Mathf.Clamp(baseIndex, 0, colors.Length > 0 ? colors.Length - 1 : 0);
            var baseAngle = 0f;
            if (!wheelHandlesUseBaseColors && colors.Length > 0)
            {
                var baseColorForAngle = colors[resolvedBaseIndex];
                var baseHsv = ColorConversionUtility.RGBToHSV(baseColorForAngle);
                baseAngle = Mathf.Repeat(baseHsv.H * 360f, 360f);
            }
            var baseColorsCount = baseColors.Length;
            for (var i = 0; i < colors.Length; i++)
            {
                var hsv = ColorConversionUtility.RGBToHSV(colors[i]);

                // Limit minimum lightness, we can always trust the HSV values
                var angle = Mathf.Repeat(hsv.H * 360f, 360f);
                var rad = angle * Mathf.Deg2Rad;
                var radius = wheelRadius * Mathf.Clamp01(hsv.S);
                var position = wheelCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                var rect = new Rect(position.x - HandleRadius, position.y - HandleRadius, HandleRadius * 2f, HandleRadius * 2f);

                var baseColorIndex = 0;
                var maintainsHarmony = !wheelHandlesUseBaseColors;
                var hueOffset = 0f;
                if (wheelHandlesUseBaseColors)
                {
                    baseColorIndex = baseColorsCount > 0 ? Mathf.Clamp(i, 0, baseColorsCount - 1) : 0;
                    maintainsHarmony = false;
                }
                else
                {
                    baseColorIndex = baseColorsCount > 0 ? Mathf.Clamp(0, 0, baseColorsCount - 1) : 0;
                    if (colors.Length > 0)
                    {
                        if (!linkedPalette && i == resolvedBaseIndex)
                        {
                            baseAngle = angle;
                            maintainsHarmony = false;
                        }
                        else
                        {
                            hueOffset = linkedPalette ? Mathf.Repeat(angle - baseAngle + 360f, 360f) : Mathf.Repeat(angle - baseAngle + 360f, 360f);
                            if (Mathf.Approximately(hueOffset, 360f))
                            {
                                hueOffset = 0f;
                            }
                            maintainsHarmony = true;
                        }
                    }
                }

                if (linkedPalette)
                {
                    maintainsHarmony = true;
                }

                wheelHandles.Add(new PaletteHandleData
                {
                    ColorIndex = i,
                    BaseColorIndex = baseColorIndex,
                    Angle = angle,
                    Color = colors[i],
                    Position = position,
                    Rect = rect,
                    Draggable = true,
                    MaintainsHarmony = maintainsHarmony,
                    HueOffset = hueOffset,
                    Saturation = Mathf.Clamp01(hsv.S),
                    Value = Mathf.Clamp01(hsv.V)
                });
            }
        }

        private int FindBaseHandleIndex(IReadOnlyList<Color> colors, IReadOnlyList<Color> baseColors)
        {
            if (colors == null || colors.Count == 0 || baseColors == null || baseColors.Count == 0)
                return 0;
            
            var baseColor = baseColors[0];
            var bestIndex = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < colors.Count; i++)
            {
                var distance = ColorDistance(colors[i], baseColor);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private float ColorDistance(Color a, Color b)
        {
            var dr = a.r - b.r;
            var dg = a.g - b.g;
            var db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        private bool HandleWheelInput(Event evt, PaletteType type)
        {
            var changed = false;
            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0:
                {
                    var handleClicked = false;
                    for (var i = 0; i < wheelHandles.Count; i++)
                    {
                        var handle = wheelHandles[i];
                        if (!handle.Draggable)
                            continue;
                        if (!handle.Rect.Contains(evt.mousePosition))
                            continue;

                        activeHandle = CreateSelectionState(i, handle);
                        activeHandleType = type;
                        selectedHandle = activeHandle;
                        activeHandleDragOffset = evt.mousePosition - handle.Position;
                        hasActiveHandleDragOffset = true;
                        handleClicked = true;
                        GUI.FocusControl(string.Empty);
                        evt.Use();
                        Repaint();
                        break;
                    }

                    if (!handleClicked && wheelRect.Contains(evt.mousePosition))
                    {
                        DeselectHandle(true);
                        hasActiveHandleDragOffset = false;
                        activeHandleDragOffset = Vector2.zero;
                        evt.Use();
                    }
                    break;
                }
                case EventType.MouseDrag when evt.button == 0:
                {
                    if (activeHandleType != type || !activeHandle.HasBaseColor)
                        break;

                    SyncSelection(ref activeHandle);
                    if (!activeHandle.HasHandle)
                        break;

                    var handle = wheelHandles[activeHandle.HandleIndex];
                    var pointerPosition = evt.mousePosition;
                    if (hasActiveHandleDragOffset)
                    {
                        pointerPosition -= activeHandleDragOffset;
                    }
                    var delta = pointerPosition - wheelCenter;
                    if (delta.sqrMagnitude < 0.001f)
                        break;

                    var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    if (angle < 0f)
                        angle += 360f;

                    var distance = Mathf.Clamp(delta.magnitude, 0f, wheelRadius);
                    var ratio = wheelRadius > 0f ? Mathf.Clamp01(distance / wheelRadius) : 0f;

                    changed = ApplyHandleAngle(handle, angle) || changed;
                    if (evt.shift)
                    {
                        changed = ApplyHandleValue(handle, ratio) || changed;
                    }
                    else
                    {
                        changed = ApplyHandleSaturation(handle, ratio) || changed;
                    }
                    if (changed)
                    {
                        evt.Use();
                        Repaint();
                    }
                    break;
                }
                case EventType.MouseUp when evt.button == 0:
                case EventType.MouseLeaveWindow:
                {
                    if (activeHandle.HasHandle)
                    {
                        ClearActiveHandle();
                        evt.Use();
                        Repaint();
                    }
                    break;
                }
            }

            return changed;
        }

        private bool ApplyHandleAngle(PaletteHandleData handle, float angle)
        {
            if (!handle.Draggable)
                return false;

            var hue = Mathf.Repeat(angle / 360f, 1f);
            if (!handle.MaintainsHarmony)
            {
                if (IsLinkedPalette(activeHandleType))
                {
                    Undo.RecordObject(palette, "Adjust Palette Color");
                    palette.ClearAllOverrides(true, false, false);
                }
                else
                {
                    Undo.RecordObject(palette, "Adjust Palette Color");
                    palette.ClearHandleOverride(handle.ColorIndex, true, false);
                }
                var changed = UpdateBaseColorHue(handle.BaseColorIndex, hue);
                if (changed)
                {
                    activeHandle.BaseColorIndex = handle.BaseColorIndex;
                    activeHandle.HueOffset = handle.HueOffset;
                }
                return changed;
            }

            var baseAngle = Mathf.Repeat(angle - handle.HueOffset + 360f, 360f);
            var baseHue = baseAngle / 360f;
            if (IsLinkedPalette(activeHandleType))
            {
                Undo.RecordObject(palette, "Adjust Palette Color");
                palette.ClearAllOverrides(true, false, false);
            }

            var changedHarmony = UpdateBaseColorHue(handle.BaseColorIndex, baseHue);
            if (changedHarmony)
            {
                activeHandle.BaseColorIndex = handle.BaseColorIndex;
                activeHandle.HueOffset = handle.HueOffset;
            }

            return changedHarmony;
        }

        private bool ApplyHandleSaturation(PaletteHandleData handle, float saturation)
        {
            saturation = Mathf.Clamp01(saturation);

            if (Mathf.Approximately(handle.Saturation, saturation))
                return false;

            var linked = IsLinkedPalette(activeHandleType);
            if (!handle.MaintainsHarmony || linked)
            {
                if (linked)
                {
                    Undo.RecordObject(palette, "Adjust Palette Color");
                    palette.ClearAllOverrides(true, false, false);
                }
                else
                {
                    Undo.RecordObject(palette, "Adjust Palette Color");
                    palette.ClearHandleOverride(handle.ColorIndex, true, false);
                }
                var changed = UpdateBaseColorSaturation(handle.BaseColorIndex, saturation);
                if (linked && changed)
                {
                    serializedObject.Update();
                }
                if (changed && activeHandle.HasHandle && activeHandle.HandleIndex < wheelHandles.Count)
                {
                    var data = wheelHandles[activeHandle.HandleIndex];
                    data.Saturation = saturation;
                    wheelHandles[activeHandle.HandleIndex] = data;
                }
                return changed;
            }

            Undo.RecordObject(palette, "Adjust Palette Color");
            palette.SetHandleOverride(handle.ColorIndex, saturation, null);
            serializedObject.Update();
            if (activeHandle.HasHandle && activeHandle.HandleIndex < wheelHandles.Count)
            {
                var data = wheelHandles[activeHandle.HandleIndex];
                data.Saturation = saturation;
                wheelHandles[activeHandle.HandleIndex] = data;
            }
            return true;
        }

        private bool ApplyHandleValue(PaletteHandleData handle, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(handle.Value, value))
                return false;

            var linked = IsLinkedPalette(activeHandleType);
            if (!handle.MaintainsHarmony || linked)
            {
                if (linked)
                {
                    Undo.RecordObject(palette, "Adjust Palette Color");
                    palette.ClearAllOverrides(false, true, false);
                }
                else
                {
                    Undo.RecordObject(palette, "Adjust Palette Color");
                    palette.ClearHandleOverride(handle.ColorIndex, false, true);
                }
                var changed = UpdateBaseColorValue(handle.BaseColorIndex, value);
                if (linked && changed)
                {
                    serializedObject.Update();
                }
                if (changed && activeHandle.HasHandle && activeHandle.HandleIndex < wheelHandles.Count)
                {
                    var data = wheelHandles[activeHandle.HandleIndex];
                    data.Value = value;
                    wheelHandles[activeHandle.HandleIndex] = data;
                }
                return changed;
            }

            Undo.RecordObject(palette, "Adjust Palette Color");
            palette.SetHandleOverride(handle.ColorIndex, null, value);
            serializedObject.Update();
            if (activeHandle.HasHandle && activeHandle.HandleIndex < wheelHandles.Count)
            {
                var data = wheelHandles[activeHandle.HandleIndex];
                data.Value = value;
                wheelHandles[activeHandle.HandleIndex] = data;
            }
            return true;
        }

        private bool UpdateBaseColorHue(int baseColorIndex, float hue)
        {
            var baseColors = palette.BaseColors;
            if (baseColors == null || baseColors.Length == 0)
                return false;

            baseColorIndex = Mathf.Clamp(baseColorIndex, 0, baseColors.Length - 1);
            var hsv = ColorConversionUtility.RGBToHSV(baseColors[baseColorIndex]);
            if (Mathf.Approximately(hsv.H, hue))
                return false;

            hsv = new ColorHSV(hue, hsv.S, hsv.V, hsv.A);
            var updated = (Color[])baseColors.Clone();
            updated[baseColorIndex] = ColorConversionUtility.HSVToRGB(hsv);

            Undo.RecordObject(palette, "Adjust Palette Color");
            palette.SetBaseColors(updated);
            serializedObject.Update();

            return true;
        }

        private bool UpdateBaseColorSaturation(int baseColorIndex, float saturation)
        {
            var baseColors = palette.BaseColors;
            if (baseColors == null || baseColors.Length == 0)
                return false;

            baseColorIndex = Mathf.Clamp(baseColorIndex, 0, baseColors.Length - 1);
            var hsv = ColorConversionUtility.RGBToHSV(baseColors[baseColorIndex]);
            if (Mathf.Approximately(hsv.S, saturation))
                return false;

            hsv = new ColorHSV(hsv.H, saturation, hsv.V, hsv.A);
            var updated = (Color[])baseColors.Clone();
            updated[baseColorIndex] = ColorConversionUtility.HSVToRGB(hsv);

            Undo.RecordObject(palette, "Adjust Palette Color");
            palette.SetBaseColors(updated);
            serializedObject.Update();

            return true;
        }

        private bool UpdateBaseColorValue(int baseColorIndex, float value)
        {
            var baseColors = palette.BaseColors;
            if (baseColors == null || baseColors.Length == 0)
                return false;

            baseColorIndex = Mathf.Clamp(baseColorIndex, 0, baseColors.Length - 1);
            var hsv = ColorConversionUtility.RGBToHSV(baseColors[baseColorIndex]);
            if (Mathf.Approximately(hsv.V, value))
                return false;

            hsv = new ColorHSV(hsv.H, hsv.S, value, hsv.A);

            var updated = (Color[])baseColors.Clone();
            updated[baseColorIndex] = ColorConversionUtility.HSVToRGB(hsv);

            Undo.RecordObject(palette, "Adjust Palette Color");
            palette.SetBaseColors(updated);
            serializedObject.Update();

            return true;
        }

        private void DrawValueSlider(PaletteType paletteType)
        {
            var linked = IsLinkedPalette(paletteType);
            var hasHandles = wheelHandles.Count > 0;
            var hasSelection = hasHandles && selectedHandle.HasHandle && selectedHandle.HandleIndex < wheelHandles.Count;
            var label = new GUIContent("Lightness", linked
                ? "Adjust value/brightness for linked harmony"
                : "Adjust value/brightness for the selected harmony handle");

            var sliderValue = 0.5f;
            if (linked)
            {
                var baseColors = palette.BaseColors;
                if (baseColors != null && baseColors.Length > 0)
                {
                    var hsv = ColorConversionUtility.RGBToHSV(baseColors[0]);
                    sliderValue = Mathf.Clamp01(hsv.V);
                }
            }
            else if (hasSelection)
            {
                sliderValue = Mathf.Clamp01(wheelHandles[selectedHandle.HandleIndex].Value);
            }

            var sliderEnabled = linked || hasSelection;
            EditorGUI.BeginDisabledGroup(!sliderEnabled);
            EditorGUI.BeginChangeCheck();
            
            // Limit minimum to 0.01 to prevent losing hue/saturation information
            var newValue = EditorGUILayout.Slider(label, sliderValue, 0.01f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                if (linked)
                {
                    ApplyLinkedSliderValue(newValue);
                }
                else if (hasSelection)
                {
                    ApplyHandleSliderValue(newValue);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ApplyLinkedSliderValue(float newValue)
        {
            var baseColors = palette.BaseColors;
            if (baseColors == null || baseColors.Length == 0)
                return;

            Undo.RecordObject(palette, "Adjust Palette Lightness");
            palette.ClearAllOverrides(false, true, false);
            var updated = (Color[])baseColors.Clone();
            for (var i = 0; i < updated.Length; i++)
            {
                var hsv = ColorConversionUtility.RGBToHSV(updated[i]);
                hsv = new ColorHSV(hsv.H, hsv.S, newValue, hsv.A);
                updated[i] = ColorConversionUtility.HSVToRGB(hsv);
            }

            palette.SetBaseColors(updated);
            serializedObject.Update();

            if (selectedHandle.HasHandle && selectedHandle.HandleIndex < wheelHandles.Count)
            {
                var handle = wheelHandles[selectedHandle.HandleIndex];
                handle.Value = newValue;
                wheelHandles[selectedHandle.HandleIndex] = handle;
            }
        }

        private void ApplyHandleSliderValue(float newValue)
        {
            if (!selectedHandle.HasHandle || selectedHandle.HandleIndex < 0 || selectedHandle.HandleIndex >= wheelHandles.Count)
                return;

            var handle = wheelHandles[selectedHandle.HandleIndex];

            if (!handle.MaintainsHarmony)
            {
                Undo.RecordObject(palette, "Adjust Palette Color");
                palette.ClearHandleOverride(handle.ColorIndex, false, true);
                var changed = UpdateBaseColorValue(handle.BaseColorIndex, newValue);
                if (changed)
                {
                    handle.Value = newValue;
                    wheelHandles[selectedHandle.HandleIndex] = handle;
                }
                return;
            }

            Undo.RecordObject(palette, "Adjust Palette Color");
            palette.SetHandleOverride(handle.ColorIndex, null, newValue);
            serializedObject.Update();
            handle.Value = newValue;
            wheelHandles[selectedHandle.HandleIndex] = handle;
        }

        private bool IsLinkedPalette(PaletteType type)
        {
            return type == PaletteType.Complementary || type == PaletteType.Triadic || type == PaletteType.Analogous ||
                   type == PaletteType.Tetradic || type == PaletteType.Monochromatic;
        }

        private static Material wheelMaterial;

        private void DrawWheelBackground()
        {
            if (wheelMaterial == null)
            {
                var shader = Shader.Find("XD Paint/Chroma Palette/Color Wheel");
                if (shader == null)
                {
                    Debug.LogError("ColorWheel shader not found!");
                    return;
                }
                wheelMaterial = new Material(shader);
            }

            wheelMaterial.SetFloat("_MaxValue", wheelMaxValue);

            var rect = new Rect(
                wheelCenter.x - wheelRadius,
                wheelCenter.y - wheelRadius,
                wheelRadius * 2f,
                wheelRadius * 2f
            );

            EditorGUI.DrawPreviewTexture(rect, Texture2D.whiteTexture, wheelMaterial);

            Handles.BeginGUI();
            Handles.color = new Color(0f, 0f, 0f, 0.25f);
            Handles.DrawWireDisc(wheelCenter, Vector3.forward, wheelRadius);
            Handles.EndGUI();
        }

        private void DrawWheelHandles()
        {
            if (wheelHandles.Count == 0)
                return;

            Handles.BeginGUI();
            for (var i = 0; i < wheelHandles.Count; i++)
            {
                var handle = wheelHandles[i];
                var position = new Vector3(handle.Position.x, handle.Position.y, 0f);
                var isActive = activeHandle.HasHandle && i == activeHandle.HandleIndex;
                var isSelected = selectedHandle.HasHandle && i == selectedHandle.HandleIndex;
                var ring = handle.Draggable ? Color.white : new Color(1f, 1f, 1f, 0.7f);
                if (isSelected)
                {
                    var glowColor = isActive ? new Color(1f, 0.75f, 0f, 0.45f) : new Color(1f, 0.85f, 0.1f, 0.35f);
                    Handles.color = new Color(0f, 0f, 0f, 0.5f);
                    Handles.DrawSolidDisc(position, Vector3.forward, HandleRadius + 4f);
                    Handles.color = glowColor;
                    Handles.DrawWireDisc(position, Vector3.forward, HandleRadius + 4f);
                    Handles.DrawWireDisc(position, Vector3.forward, HandleRadius + 3f);

                    var highlightColor = isActive ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.92f, 0.35f);
                    ring = Color.Lerp(ring, highlightColor, isActive ? 0.6f : 0.45f);
                }

                Handles.color = new Color(0f, 0f, 0f, 0.35f);
                Handles.DrawSolidDisc(position, Vector3.forward, HandleRadius + 2f);
                Handles.color = ring;
                Handles.DrawSolidDisc(position, Vector3.forward, HandleRadius);
                Handles.color = handle.Color;
                Handles.DrawSolidDisc(position, Vector3.forward, HandleRadius - 2f);
                Handles.color = new Color(0f, 0f, 0f, 0.5f);
                Handles.DrawWireDisc(position, Vector3.forward, HandleRadius);
            }
            Handles.EndGUI();
        }

        private void DrawWheelConnections(PaletteType type)
        {
            if (wheelHandles.Count < 2)
                return;

            var pairs = GetHandleConnections(type, wheelHandles.Count);
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.3f);
            foreach (var (a, b) in pairs)
            {
                if (a < 0 || a >= wheelHandles.Count || b < 0 || b >= wheelHandles.Count)
                    continue;
                var start = new Vector3(wheelHandles[a].Position.x, wheelHandles[a].Position.y, 0f);
                var end = new Vector3(wheelHandles[b].Position.x, wheelHandles[b].Position.y, 0f);
                Handles.DrawAAPolyLine(2f, start, end);
            }
            Handles.EndGUI();
        }

        private IEnumerable<(int, int)> GetHandleConnections(PaletteType type, int count)
        {
            if (count < 2)
                yield break;

            switch (type)
            {
                case PaletteType.Complementary:
                    yield return (0, Mathf.Min(1, count - 1));
                    break;
                case PaletteType.Triadic:
                    if (count >= 3)
                    {
                        yield return (0, 1);
                        yield return (1, 2);
                        yield return (2, 0);
                    }
                    break;
                case PaletteType.Tetradic:
                    for (var i = 0; i < Mathf.Min(4, count); i++)
                    {
                        var next = (i + 1) % Mathf.Min(4, count);
                        yield return (i, next);
                    }
                    break;
                case PaletteType.SplitComplementary:
                    if (count >= 3)
                    {
                        yield return (0, 1);
                        yield return (0, 2);
                        yield return (1, 2);
                    }
                    break;
                case PaletteType.Analogous:
                case PaletteType.Monochromatic:
                case PaletteType.Gradient:
                    for (var i = 0; i < count - 1; i++)
                    {
                        yield return (i, i + 1);
                    }
                    break;
                case PaletteType.Custom:
                    break;
            }
        }
    }
}