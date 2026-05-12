using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XDPaint.ChromaPalette.Editor
{
    public partial class ColorPaletteInspector
    {
        private GUIStyle title;
        private GUIStyle subtitle;
        private readonly List<Color> swatches = new(12);

        private new void DrawHeader()
        {
            EnsureStyles();
            var height = 56f;
            var rect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            rect.xMin += 2;
            rect.xMax -= 2;
            DrawGlassCard(rect);
            var colors = GetPaletteColors(serializedObject);
            var accent = GetAccent(colors, new Color(0.36f, 0.62f, 1f));
            var content = new Rect(rect.x + 14, rect.y + 10, rect.width - 28, rect.height - 20);
            var titleRect = new Rect(content.x + 24, content.y, content.width - 38f - 24f, 26);
            EditorGUI.LabelField(titleRect, "COLOR PALETTE", title);
            var subtitleRect = new Rect(content.x, content.y + 22, content.width - 38f, 18);
            EditorGUI.LabelField(subtitleRect, "<b>Create and harmonize palettes</b> · Generate · Customize · Preview", subtitle);
            DrawPaletteIcon(new Rect(content.x, content.y + 2, 24, 24), accent);
            GUI.color = Color.white;
            DrawWheel(rect);
        }
        
        private void EnsureStyles()
        {
            title ??= new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };

            subtitle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.85f) },
                richText = true,
                clipping = TextClipping.Clip
            };

        }

        private void DrawGlassCard(Rect r)
        {
            var texture = EditorGUIUtility.whiteTexture;

            GUI.color = new Color(1, 1, 1, 0.38f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), texture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), texture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), texture);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), texture);

            var shadow = new Rect(r.x, r.y + 2, r.width, r.height);
            GUI.color = new Color(0, 0, 0, 0.15f);
            GUI.DrawTexture(shadow, texture);
            GUI.color = Color.white;
        }

        private IList<Color> GetPaletteColors(SerializedObject so)
        {
            swatches.Clear();
            var prop = so.FindProperty("myPaletteColors");
            if (prop is { isArray: true, arraySize: > 0 })
            {
                var n = Mathf.Min(prop.arraySize, 12);
                for (var i = 0; i < n; i++)
                {
                    swatches.Add(prop.GetArrayElementAtIndex(i).colorValue);
                }
                
                return swatches;
            }

            swatches.Add(new Color(0.99f, 0.27f, 0.42f));
            swatches.Add(new Color(1.00f, 0.58f, 0.28f));
            swatches.Add(new Color(0.99f, 0.87f, 0.33f));
            swatches.Add(new Color(0.33f, 0.85f, 0.58f));
            swatches.Add(new Color(0.27f, 0.67f, 0.99f));
            swatches.Add(new Color(0.58f, 0.42f, 0.99f));
            return swatches;
        }

        private Color GetAccent(IList<Color> list, Color fallback)
        {
            return list is { Count: > 0 } ? list[0] : fallback;
        }

        private void DrawPaletteIcon(Rect r, Color accent)
        {
            Handles.BeginGUI();
            Handles.color = new Color(accent.r, accent.g, accent.b, 0.2f);
            Handles.DrawSolidDisc(new Vector3(r.center.x, r.center.y, 0), Vector3.forward, r.width * 0.5f);
            var center = r.center;
            var radius = r.width * 0.35f;
            var segments = 6;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * (360f / segments) * Mathf.Deg2Rad;
                var nextAngle = (i + 1) * (360f / segments) * Mathf.Deg2Rad;
                var h = i / (float)segments;
                Handles.color = Color.HSVToRGB(h, 0.7f, 0.9f);
                var p1 = center;
                var p2 = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var p3 = center + new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)) * radius;
                Handles.DrawAAConvexPolygon(p1, p2, p3);
            }
    
            Handles.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0), Vector3.forward, 2f);
            Handles.EndGUI();
        }
        
        private void DrawWheel(Rect area)
        {
            var center = new Vector2(area.x + area.width - 28f, area.y + 28f);
            var radius = 18f;
            var inner = 0f;
            Handles.BeginGUI();
            var segments = 24;
            for (var i = 0; i < segments; i++)
            {
                var a0 = i / (float)segments * 360f;
                var a1 = (i + 1) / (float)segments * 360f;
                var h = i / (float)segments;
                var col = Color.HSVToRGB(h, 0.9f, 1f);
                col.a = 0.95f;
                Handles.color = col;
                Handles.DrawSolidArc(center, Vector3.forward, AngleToDir(a0), a1 - a0, radius);
            }

            Handles.color = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.11f, 1f) : new Color(0.92f, 0.92f, 0.95f, 1f);
            Handles.DrawSolidDisc(center, Vector3.forward, inner);
            Handles.color = new Color(0, 0, 0, 0.2f);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.EndGUI();
        }

        private Vector3 AngleToDir(float deg)
        {
            var rad = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        }
    }
}