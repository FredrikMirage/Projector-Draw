using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.Utilities;
using XDPaint.ChromaPalette.Internal.State;

namespace XDPaint.ChromaPalette.Internal.UI
{
    /// <summary>
    /// Service responsible for handling all palette interaction logic.
    /// Manages pointer events, mode-specific palette updates, cursor positioning, and texture sampling.
    /// </summary>
    public class PaletteInteraction
    {
        private const float HueRingThicknessFactor = 0.085f;
        private const float InnerRegionRadiusScale = 0.92f;
        private const float DirectionThreshold = 1e-6f;

        private ColorStateStore colorState;
        private RectTransform paletteRect;
        private Image paletteCursor;
        private Image paletteCursorSecondary;
        
        private ColorPickerMode currentMode;
        private Texture2D currentTexture;
        private bool isDragging;

        private DragRegion activeDragRegion = DragRegion.None;
        private CursorContrastMode cursorContrastMode = CursorContrastMode.InvertColor;
        private Color defaultCursorColor = Color.white;
        private Color defaultCursorSecondaryColor = Color.white;
        private bool hasDefaultCursorColor;
        private bool hasDefaultCursorSecondaryColor;
        
        private Func<bool> getBicubicSampling;
        private Func<bool> getCursorSnapping;
        private Func<bool> getIgnoreTransparent;
        private Func<float> getTransparentAlphaThreshold;
        private Func<Color[]> getIgnoredColors;
        private Func<float> getIgnoreColorTolerance;

        private enum DragRegion
        {
            None,
            Inner,
            Outer
        }

        public event Action OnPaletteUpdated;
        
        public bool IsDragging => isDragging;
        
        public void Initialize(
            ColorStateStore colorStateManager,
            RectTransform paletteRectTransform,
            Image paletteCursorImage,
            Image paletteCursorSecondaryImage,
            Func<bool> bicubicSamplingGetter,
            Func<bool> cursorSnappingGetter,
            Func<bool> ignoreTransparentGetter,
            Func<float> transparentAlphaThresholdGetter,
            Func<Color[]> ignoredColorsGetter,
            Func<float> ignoreColorToleranceGetter)
        {
            colorState = colorStateManager;
            paletteRect = paletteRectTransform;
            paletteCursor = paletteCursorImage;
            if (paletteCursor != null && !hasDefaultCursorColor)
            {
                defaultCursorColor = paletteCursor.color;
                hasDefaultCursorColor = true;
            }

            paletteCursorSecondary = paletteCursorSecondaryImage;
            if (paletteCursorSecondary != null && !hasDefaultCursorSecondaryColor)
            {
                defaultCursorSecondaryColor = paletteCursorSecondary.color;
                hasDefaultCursorSecondaryColor = true;
            }

            getBicubicSampling = bicubicSamplingGetter;
            getCursorSnapping = cursorSnappingGetter;
            getIgnoreTransparent = ignoreTransparentGetter;
            getTransparentAlphaThreshold = transparentAlphaThresholdGetter;
            getIgnoredColors = ignoredColorsGetter;
            getIgnoreColorTolerance = ignoreColorToleranceGetter;

            RefreshCursorColors();
        }

        public void SetMode(ColorPickerMode mode)
        {
            currentMode = mode;
            activeDragRegion = DragRegion.None;
            if (paletteCursorSecondary != null)
            {
                var enableSecondary = currentMode == ColorPickerMode.CircleTriangle || currentMode == ColorPickerMode.CircleCircle;
                var go = paletteCursorSecondary.gameObject;
                if (go.activeSelf != enableSecondary)
                    go.SetActive(enableSecondary);
            }

            ApplyCursorColors();
        }

        public void SetCursorContrastMode(CursorContrastMode mode)
        {
            cursorContrastMode = mode;
            RefreshCursorColors();
        }

        public void RefreshCursorColors()
        {
            ApplyCursorColors();
        }
        
        public void SetCurrentTexture(Texture2D texture)
        {
            currentTexture = texture;
        }
        
        public bool HandlePointerDown(PointerEventData eventData)
        {
            if (paletteRect == null)
                return false;
            
            if (RectTransformUtility.RectangleContainsScreenPoint(paletteRect, eventData.position, eventData.pressEventCamera))
            {
                activeDragRegion = DragRegion.None;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(paletteRect, eventData.position, eventData.pressEventCamera, out var localPoint))
                {
                    activeDragRegion = DetermineDragRegion(localPoint);
                }
                isDragging = true;
                UpdatePalette(eventData);
                return true;
            }
            
            return false;
        }
        
        public void HandleDrag(PointerEventData eventData)
        {
            if (isDragging)
            {
                UpdatePalette(eventData);
            }
        }
        
        public void HandlePointerUp()
        {
            isDragging = false;
            activeDragRegion = DragRegion.None;
            ApplyCursorColors();
        }
        
        private void UpdatePalette(PointerEventData eventData)
        {
            if (paletteRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(paletteRect, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;
            
            switch (currentMode)
            {
                case ColorPickerMode.Rectangle:
                    UpdateRectanglePalette(localPoint);
                    break;
                case ColorPickerMode.Circle:
                    UpdateCirclePalette(localPoint);
                    break;
                case ColorPickerMode.CircleFull:
                    UpdateCircleFullPalette(localPoint);
                    break;
                case ColorPickerMode.CircleTriangle:
                    UpdateCircleTrianglePalette(localPoint);
                    break;
                case ColorPickerMode.CircleCircle:
                    UpdateCircleCirclePalette(localPoint);
                    break;
                case ColorPickerMode.Texture:
                case ColorPickerMode.Palette:
                    UpdateTexturePalette(localPoint);
                    break;
            }
        }
        
        private void UpdateRectanglePalette(Vector2 localPoint)
        {
            if (paletteRect == null)
                return;
            
            var rect = paletteRect.rect;
            var x = Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax);
            var y = Mathf.Clamp(localPoint.y, rect.yMin, rect.yMax);
            var saturation = Mathf.InverseLerp(rect.xMin, rect.xMax, x);
            var value = Mathf.InverseLerp(rect.yMin, rect.yMax, y);
            
            UpdateCursorPosition(new Vector2(x, y));
            
            if (colorState != null)
            {
                var hsv = colorState.ColorHSV;
                hsv.S = saturation;
                hsv.V = value;
                colorState.SetColorFromHSV(hsv, true);
                ApplyCursorColors();
            }

            OnPaletteUpdated?.Invoke();
        }
        
        private void UpdateCirclePalette(Vector2 localPoint)
        {
            if (paletteRect == null)
                return;
            
            var rect = paletteRect.rect;
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var offset = localPoint - center;
            var distance = offset.magnitude;
            var cursorPos = localPoint;
            if (distance > radius)
            {
                offset = offset.normalized * radius;
                distance = radius;
                cursorPos = center + offset;
            }
            
            var angle = Mathf.Atan2(offset.y, offset.x);
            var hue = (angle + Mathf.PI) / (2f * Mathf.PI);
            var saturation = distance / radius;
            
            UpdateCursorPosition(cursorPos);
            
            if (colorState != null)
            {
                var hsv = colorState.ColorHSV;
                hsv.H = hue;
                hsv.S = saturation;
                colorState.SetColorFromHSV(hsv, true);
                ApplyCursorColors();
            }

            OnPaletteUpdated?.Invoke();
        }

        private void UpdateCircleFullPalette(Vector2 localPoint)
        {
            if (paletteRect == null)
                return;

            var rect = paletteRect.rect;
            var center = rect.center;
            var maxRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var offset = localPoint - center;
            var distance = offset.magnitude;
            var clampedOffset = offset;
            var clampedDistance = distance;
            if (clampedDistance > maxRadius)
            {
                clampedOffset = offset.normalized * maxRadius;
                clampedDistance = maxRadius;
            }

            var cursorPos = center + clampedOffset;
            var angle = Mathf.Atan2(clampedOffset.y, clampedOffset.x);
            var hue = Mathf.Repeat(angle / (2f * Mathf.PI) + 0.5f, 1f);
            var rNorm = Mathf.Clamp01(clampedDistance / maxRadius);

            float saturation;
            float value;
            if (rNorm <= 0.5f)
            {
                saturation = rNorm * 2f;
                value = 1f;
            }
            else
            {
                var falloff = Mathf.Max(0f, 2f * (1f - rNorm));
                saturation = falloff;
                value = falloff;
            }

            UpdateCursorPosition(cursorPos);
            if (colorState != null)
            {
                var hsv = colorState.ColorHSV;
                hsv.H = hue;
                hsv.S = saturation;
                hsv.V = value;
                colorState.SetColorFromHSV(hsv, true);
                ApplyCursorColors();
            }

            OnPaletteUpdated?.Invoke();
        }

        private void UpdateCircleTrianglePalette(Vector2 localPoint)
        {
            if (paletteRect == null)
                return;

            var rect = paletteRect.rect;
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var triR = ri * InnerRegionRadiusScale;
            var offset = localPoint - center;
            var r = offset.magnitude;
            if (r > ro)
            {
                offset = offset.normalized * ro;
                r = ro;
                localPoint = center + offset;
            }

            var isRing = r >= ri;
            if (activeDragRegion == DragRegion.Inner)
            {
                isRing = false;
            }
            else if (activeDragRegion == DragRegion.Outer)
            {
                isRing = true;
            }

            if (isRing)
            {
                var direction = offset.sqrMagnitude < DirectionThreshold ? GetHueDirection() : offset.normalized;
                if (direction.sqrMagnitude < DirectionThreshold)
                {
                    direction = GetHueDirection();
                }

                var angle = Mathf.Atan2(direction.y, direction.x);
                var hue = Mathf.Repeat((angle + Mathf.PI) / (2f * Mathf.PI), 1f);
                UpdateCursorPositionSecondary(center + direction * ((ri + ro) * 0.5f));
                if (colorState != null)
                {
                    colorState.SetHue(hue, true);
                    ApplyCursorColors();
                }

                OnPaletteUpdated?.Invoke();
                return;
            }

            var hueRad = colorState != null ? colorState.Hue * 2f * Mathf.PI - Mathf.PI : 0f;
            var dir = new Vector2(Mathf.Cos(hueRad), Mathf.Sin(hueRad));
            var pH = center + dir * triR;
            var pW = center + Rotate(dir * triR, 2f * Mathf.PI / 3f);
            var pB = center + Rotate(dir * triR, -2f * Mathf.PI / 3f);
            var targetPoint = localPoint;
            if (!TryBarycentric(localPoint, pW, pB, pH, out var wW, out var wB, out var wH))
            {
                targetPoint = ClosestPointOnTriangle(localPoint, pW, pB, pH);
                TryBarycentric(targetPoint, pW, pB, pH, out wW, out wB, out wH);
            }

            var V = Mathf.Clamp01(wW + wH);
            var S = V > 1e-5f ? Mathf.Clamp01(wH / V) : 0f;

            UpdateCursorPosition(targetPoint);
            if (colorState != null)
            {
                var hsv = colorState.ColorHSV;
                hsv.S = S;
                hsv.V = V;
                colorState.SetColorFromHSV(hsv, true);
                ApplyCursorColors();
            }

            OnPaletteUpdated?.Invoke();
        }

        private void UpdateCircleCirclePalette(Vector2 localPoint)
        {
            if (paletteRect == null)
                return;

            var rect = paletteRect.rect;
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var circleRadius = ri * InnerRegionRadiusScale;
            var offset = localPoint - center;
            var distance = offset.magnitude;

            if (distance > ro)
            {
                offset = offset.normalized * ro;
                distance = ro;
                localPoint = center + offset;
            }

            var isRing = distance >= ri;
            if (activeDragRegion == DragRegion.Inner)
            {
                isRing = false;
            }
            else if (activeDragRegion == DragRegion.Outer)
            {
                isRing = true;
            }

            if (isRing)
            {
                var direction = offset.sqrMagnitude < DirectionThreshold ? GetHueDirection() : offset.normalized;
                if (direction.sqrMagnitude < DirectionThreshold)
                {
                    direction = GetHueDirection();
                }

                var angle = Mathf.Atan2(direction.y, direction.x);
                var hue = Mathf.Repeat((angle + Mathf.PI) / (2f * Mathf.PI), 1f);
                UpdateCursorPositionSecondary(center + direction * ((ri + ro) * 0.5f));
                if (colorState != null)
                {
                    colorState.SetHue(hue, true);
                    ApplyCursorColors();
                }

                OnPaletteUpdated?.Invoke();
                return;
            }

            if (distance > circleRadius)
            {
                offset = offset.normalized * circleRadius;
                localPoint = center + offset;
                distance = circleRadius;
            }

            var disk = offset / circleRadius;
            var square = DiskToSquare(disk);
            var saturation = Mathf.Clamp01(square.x);
            var value = Mathf.Clamp01(square.y);

            UpdateCursorPosition(center + offset);

            if (colorState != null)
            {
                var hsv = colorState.ColorHSV;
                hsv.S = saturation;
                hsv.V = value;
                colorState.SetColorFromHSV(hsv, true);
                ApplyCursorColors();
            }

            OnPaletteUpdated?.Invoke();
        }

        private Vector2 GetHueDirection()
        {
            var hueAngle = colorState != null ? colorState.Hue * 2f * Mathf.PI - Mathf.PI : 0f;
            return new Vector2(Mathf.Cos(hueAngle), Mathf.Sin(hueAngle));
        }

        private DragRegion DetermineDragRegion(Vector2 localPoint)
        {
            if (paletteRect == null)
            {
                return DragRegion.None;
            }

            var rect = paletteRect.rect;
            switch (currentMode)
            {
                case ColorPickerMode.CircleTriangle:
                    return GetCircleTriangleRegion(localPoint, rect);
                case ColorPickerMode.CircleCircle:
                    return GetCircleCircleRegion(localPoint, rect);
                default:
                    return DragRegion.Inner;
            }
        }

        private DragRegion GetCircleTriangleRegion(Vector2 localPoint, Rect rect)
        {
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var offset = localPoint - center;
            var distance = offset.magnitude;
            if (distance > ro)
            {
                distance = ro;
            }

            return distance >= ri ? DragRegion.Outer : DragRegion.Inner;
        }

        private DragRegion GetCircleCircleRegion(Vector2 localPoint, Rect rect)
        {
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var offset = localPoint - center;
            var distance = offset.magnitude;
            if (distance > ro)
            {
                distance = ro;
            }

            return distance >= ri ? DragRegion.Outer : DragRegion.Inner;
        }

        private static Vector2 SquareToDisk(Vector2 square)
        {
            const float epsilon = 1e-6f;
            var a = Mathf.Clamp(2f * square.x - 1f, -1f, 1f);
            var b = Mathf.Clamp(2f * square.y - 1f, -1f, 1f);

            if (Mathf.Abs(a) <= epsilon && Mathf.Abs(b) <= epsilon)
            {
                return Vector2.zero;
            }

            float r;
            float theta;
            if (Mathf.Abs(a) > Mathf.Abs(b))
            {
                var signA = Mathf.Sign(a);
                if (Mathf.Approximately(signA, 0f))
                {
                    signA = 1f;
                }
                var divisor = Mathf.Abs(a) > epsilon ? a : signA * epsilon;
                r = a;
                theta = Mathf.PI * 0.25f * (b / divisor);
            }
            else
            {
                var signB = Mathf.Sign(b);
                if (Mathf.Approximately(signB, 0f))
                {
                    signB = 1f;
                }
                var divisor = Mathf.Abs(b) > epsilon ? b : signB * epsilon;
                r = b;
                theta = Mathf.PI * 0.5f - Mathf.PI * 0.25f * (a / divisor);
            }

            var x = Mathf.Cos(theta) * r;
            var y = Mathf.Sin(theta) * r;
            return new Vector2(x, y);
        }

        private static Vector2 DiskToSquare(Vector2 disk)
        {
            const float epsilon = 1e-6f;
            var x = Mathf.Clamp(disk.x, -1f, 1f);
            var y = Mathf.Clamp(disk.y, -1f, 1f);
            var absX = Mathf.Abs(x);
            var absY = Mathf.Abs(y);
            if (absX <= epsilon && absY <= epsilon)
            {
                return new Vector2(0.5f, 0.5f);
            }

            var radius = Mathf.Sqrt(x * x + y * y);
            float a;
            float b;

            if (absX > absY)
            {
                var signX = Mathf.Sign(x);
                if (Mathf.Approximately(signX, 0f))
                {
                    signX = 1f;
                }

                var theta = Mathf.Atan(y / x);
                var signedRadius = signX * radius;
                a = signedRadius;
                b = signedRadius * (4f * theta / Mathf.PI);
            }
            else
            {
                var signY = Mathf.Sign(y);
                if (Mathf.Approximately(signY, 0f))
                {
                    signY = 1f;
                }

                var signedRadius = signY * radius;
                var divisor = Mathf.Abs(signedRadius) > epsilon ? signedRadius : signY;
                var cosArgument = Mathf.Clamp(x / divisor, -1f, 1f);
                var theta = Mathf.Acos(cosArgument);
                a = signedRadius * (2f - 4f * theta / Mathf.PI);
                b = signedRadius;
            }

            var u = Mathf.Clamp01(0.5f * (a + 1f));
            var v = Mathf.Clamp01(0.5f * (b + 1f));
            return new Vector2(u, v);
        }

        private static Vector2 Rotate(Vector2 v, float angle)
        {
            var s = Mathf.Sin(angle); var c = Mathf.Cos(angle);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        private static bool TryBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out float w0, out float w1, out float w2)
        {
            var v0 = b - a;
            var v1 = c - a;
            var v2 = p - a;
            var d00 = Vector2.Dot(v0, v0);
            var d01 = Vector2.Dot(v0, v1);
            var d11 = Vector2.Dot(v1, v1);
            var d20 = Vector2.Dot(v2, v0);
            var d21 = Vector2.Dot(v2, v1);
            var denom = d00 * d11 - d01 * d01;
            if (Mathf.Approximately(denom, 0f))
            {
                w0 = w1 = w2 = 0f; return false;
            }
            
            var inv = 1f / denom;
            var v = (d11 * d20 - d01 * d21) * inv;
            var w = (d00 * d21 - d01 * d20) * inv;
            var u = 1f - v - w;
            w0 = u; w1 = v; w2 = w;
            return u >= 0f && v >= 0f && w >= 0f;
        }
        
        private void UpdateTexturePalette(Vector2 localPoint)
        {
            if (currentTexture == null || paletteRect == null)
                return;
            
            var rect = paletteRect.rect;
            
            // Clamp the position to palette bounds
            var x = Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax);
            var y = Mathf.Clamp(localPoint.y, rect.yMin, rect.yMax);
            
            var u = Mathf.InverseLerp(rect.xMin, rect.xMax, x);
            var v = Mathf.InverseLerp(rect.yMin, rect.yMax, y);
            var originalUV = new Vector2(u, v);
            
            var samplingUV = originalUV;
            var cursorPos = new Vector2(x, y);
            
            if (getCursorSnapping != null && getCursorSnapping())
            {
                var snappedUV = SnapToPixelCenter(originalUV);
                var snappedX = Mathf.Lerp(rect.xMin, rect.xMax, snappedUV.x);
                var snappedY = Mathf.Lerp(rect.yMin, rect.yMax, snappedUV.y);
                cursorPos = new Vector2(snappedX, snappedY);
                samplingUV = snappedUV;
            }

            var sampledColor = SampleTextureAtUV(samplingUV);
            UpdateCursorPosition(cursorPos);
            if (IsColorIgnored(sampledColor))
                return;

            if (colorState != null)
            {
                colorState.SetColorFromRGB(sampledColor, false);
                ApplyCursorColors();
            }
            OnPaletteUpdated?.Invoke();
        }
        
        private Vector2 SnapToPixelCenter(Vector2 uv)
        {
            if (currentTexture == null)
                return uv;
            
            var pixelX = uv.x * currentTexture.width;
            var pixelY = uv.y * currentTexture.height;
            
            var intX = Mathf.RoundToInt(pixelX - 0.5f);
            var intY = Mathf.RoundToInt(pixelY - 0.5f);
            
            intX = Mathf.Clamp(intX, 0, currentTexture.width - 1);
            intY = Mathf.Clamp(intY, 0, currentTexture.height - 1);
            
            var centerU = (intX + 0.5f) / currentTexture.width;
            var centerV = (intY + 0.5f) / currentTexture.height;
            
            return new Vector2(centerU, centerV);
        }
        
        private Color SampleTextureAtUV(Vector2 uv)
        {
            if (currentTexture == null)
                return Color.white;
            
            if (getBicubicSampling != null && getBicubicSampling())
                return currentTexture.GetPixelBilinear(uv.x, uv.y);

            var pixelX = uv.x * currentTexture.width - 0.5f;
            var pixelY = uv.y * currentTexture.height - 0.5f;
                
            var x = Mathf.RoundToInt(pixelX);
            var y = Mathf.RoundToInt(pixelY);
                
            x = Mathf.Clamp(x, 0, currentTexture.width - 1);
            y = Mathf.Clamp(y, 0, currentTexture.height - 1);
                
            return currentTexture.GetPixel(x, y);
        }

        private bool IsColorIgnored(Color c)
        {
            if (currentMode != ColorPickerMode.Texture && currentMode != ColorPickerMode.Palette)
                return false;

            if (getIgnoreTransparent != null && getIgnoreTransparent())
            {
                var threshold = getTransparentAlphaThreshold != null ? Mathf.Clamp01(getTransparentAlphaThreshold()) : 0.01f;
                if (c.a <= threshold)
                    return true;
            }

            var ignored = getIgnoredColors?.Invoke();
            if (ignored != null && ignored.Length > 0)
            {
                var tol = getIgnoreColorTolerance != null ? Mathf.Clamp01(getIgnoreColorTolerance()) : 0.02f;
                for (var i = 0; i < ignored.Length; i++)
                {
                    var ic = ignored[i];
                    var dist = Mathf.Abs(c.r - ic.r) + Mathf.Abs(c.g - ic.g) + Mathf.Abs(c.b - ic.b) + Mathf.Abs(c.a - ic.a);
                    if (dist <= tol * 4f)
                        return true;
                }
            }
            
            return false;
        }
        
        public bool UpdateCursorFromColor(ColorHSV hsv)
        {
            if (paletteRect == null || paletteCursor == null)
                return false;

            var cursorUpdated = false;

            switch (currentMode)
            {
                case ColorPickerMode.Rectangle:
                {
                    var position = CalculateRectangleCursorPosition(hsv.S, hsv.V);
                    UpdateCursorPosition(position);
                    cursorUpdated = true;
                    break;
                }
                case ColorPickerMode.Circle:
                {
                    var position = CalculateCircleCursorPosition(hsv.H, hsv.S);
                    UpdateCursorPosition(position);
                    cursorUpdated = true;
                    break;
                }
                case ColorPickerMode.CircleFull:
                {
                    var position = CalculateCircleFullCursorPosition(hsv);
                    UpdateCursorPosition(position);
                    cursorUpdated = true;
                    break;
                }
                case ColorPickerMode.CircleTriangle:
                {
                    var position = CalculateCircleTriangleCursorPosition(hsv);
                    UpdateCursorPosition(position);
                    var ringPos = CalculateCircleTriangleRingCursorPosition(hsv.H);
                    UpdateCursorPositionSecondary(ringPos);
                    cursorUpdated = true;
                    break;
                }
                case ColorPickerMode.CircleCircle:
                {
                    var position = CalculateCircleCircleCursorPosition(hsv);
                    UpdateCursorPosition(position);
                    var circleRingPos = CalculateCircleTriangleRingCursorPosition(hsv.H);
                    UpdateCursorPositionSecondary(circleRingPos);
                    cursorUpdated = true;
                    break;
                }
                case ColorPickerMode.Texture:
                case ColorPickerMode.Palette:
                {
                    var targetColor = ColorConversionUtility.HSVToRGB(hsv);
                    if (TryFindColorInTexture(targetColor, out var uvPosition))
                    {
                        var rect = paletteRect.rect;
                        var x = Mathf.Lerp(rect.xMin, rect.xMax, uvPosition.x);
                        var y = Mathf.Lerp(rect.yMin, rect.yMax, uvPosition.y);
                        UpdateCursorPosition(new Vector2(x, y));
                        cursorUpdated = true;
                    }
                    break;
                }
            }

            if (cursorUpdated)
            {
                ApplyCursorColors();
                return true;
            }

            return false;
        }
        
        private Vector2 CalculateRectangleCursorPosition(float saturation, float value)
        {
            if (paletteRect == null)
                return Vector2.zero;
            
            var rect = paletteRect.rect;
            var x = Mathf.Lerp(rect.xMin, rect.xMax, saturation);
            var y = Mathf.Lerp(rect.yMin, rect.yMax, value);
            return new Vector2(x, y);
        }
        
        private Vector2 CalculateCircleCursorPosition(float hue, float saturation)
        {
            if (paletteRect == null)
                return Vector2.zero;
            
            var rect = paletteRect.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f * saturation;
            var angle = hue * 2f * Mathf.PI - Mathf.PI;
            
            var x = rect.center.x + Mathf.Cos(angle) * radius;
            var y = rect.center.y + Mathf.Sin(angle) * radius;
            return new Vector2(x, y);
        }

        private Vector2 CalculateCircleFullCursorPosition(ColorHSV hsv)
        {
            if (paletteRect == null)
                return Vector2.zero;

            var rect = paletteRect.rect;
            var maxRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var angle = hsv.H * 2f * Mathf.PI - Mathf.PI;

            float rNorm;
            const float eps = 1e-3f;
            if (hsv.V >= 1f - eps)
            {
                rNorm = Mathf.Clamp01(hsv.S) * 0.5f;
            }
            else
            {
                var colTarget = ColorConversionUtility.HSVToRGB(hsv);

                var topS = Mathf.Clamp01(hsv.S);
                var rNormTop = topS * 0.5f;
                var colTop = ColorConversionUtility.HSVToRGB(new ColorHSV(hsv.H, topS, 1f, hsv.A));
                var distTop = Mathf.Abs(colTarget.r - colTop.r) + Mathf.Abs(colTarget.g - colTop.g) + Mathf.Abs(colTarget.b - colTop.b);

                var falloff = Mathf.Clamp01(Mathf.Min(hsv.S, hsv.V));
                var rNormBottom = 1f - falloff * 0.5f;
                var colBottom = ColorConversionUtility.HSVToRGB(new ColorHSV(hsv.H, falloff, falloff, hsv.A));
                var distBottom = Mathf.Abs(colTarget.r - colBottom.r) + Mathf.Abs(colTarget.g - colBottom.g) + Mathf.Abs(colTarget.b - colBottom.b);

                rNorm = distTop <= distBottom ? rNormTop : rNormBottom;
            }

            var r = Mathf.Clamp01(rNorm) * maxRadius;
            var x = rect.center.x + Mathf.Cos(angle) * r;
            var y = rect.center.y + Mathf.Sin(angle) * r;
            return new Vector2(x, y);
        }

        private Vector2 CalculateCircleTriangleCursorPosition(ColorHSV hsv)
        {
            if (paletteRect == null)
                return Vector2.zero;

            var rect = paletteRect.rect;
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var triR = ri * InnerRegionRadiusScale;

            var angle = hsv.H * 2f * Mathf.PI - Mathf.PI;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var pH = center + dir * triR;
            var pW = center + Rotate(dir * triR, 2f * Mathf.PI / 3f);
            var pB = center + Rotate(dir * triR, -2f * Mathf.PI / 3f);
            var wW = Mathf.Clamp01(hsv.V * (1f - hsv.S));
            var wH = Mathf.Clamp01(hsv.V * hsv.S);
            var wB = Mathf.Clamp01(1f - hsv.V);
            var sum = wW + wH + wB;
            if (sum > 1e-5f)
            {
                wW /= sum; wH /= sum; wB /= sum;
            }

            var pos = pW * wW + pB * wB + pH * wH;
            return pos;
        }

        private Vector2 CalculateCircleCircleCursorPosition(ColorHSV hsv)
        {
            if (paletteRect == null)
                return Vector2.zero;

            var rect = paletteRect.rect;
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var circleRadius = ri * InnerRegionRadiusScale;

            var square = new Vector2(Mathf.Clamp01(hsv.S), Mathf.Clamp01(hsv.V));
            var disk = SquareToDisk(square);
            disk = Vector2.ClampMagnitude(disk, 1f);
            var offset = disk * circleRadius;
            return center + offset;
        }

        private Vector2 CalculateCircleTriangleRingCursorPosition(float hue)
        {
            if (paletteRect == null)
                return Vector2.zero;

            var rect = paletteRect.rect;
            var center = rect.center;
            var ro = Mathf.Min(rect.width, rect.height) * 0.5f;
            var ringThickness = HueRingThicknessFactor * 2f * ro;
            var ri = ro - ringThickness;
            var angle = hue * 2f * Mathf.PI - Mathf.PI;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var rMid = (ri + ro) * 0.5f;
            return center + dir * rMid;
        }
        
        private void UpdateCursorPosition(Vector2 localPosition)
        {
            var cursorTransform = paletteCursor?.rectTransform;
            if (cursorTransform == null || paletteRect == null)
                return;
            
            if (cursorTransform.parent == paletteRect)
            {
                cursorTransform.localPosition = localPosition;
            }
            else
            {
                var worldPos = paletteRect.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0));
                cursorTransform.position = worldPos;
            }
        }

        private void UpdateCursorPositionSecondary(Vector2 localPosition)
        {
            var cursorTransform = paletteCursorSecondary?.rectTransform;
            if (cursorTransform == null || paletteRect == null) return;

            if (cursorTransform.parent == paletteRect)
            {
                cursorTransform.localPosition = localPosition;
            }
            else
            {
                var worldPos = paletteRect.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0));
                cursorTransform.position = worldPos;
            }
        }

        private void CacheDefaultCursorColors()
        {
            if (!hasDefaultCursorColor && paletteCursor != null)
            {
                defaultCursorColor = paletteCursor.color;
                hasDefaultCursorColor = true;
            }

            if (!hasDefaultCursorSecondaryColor && paletteCursorSecondary != null)
            {
                defaultCursorSecondaryColor = paletteCursorSecondary.color;
                hasDefaultCursorSecondaryColor = true;
            }
        }

        private void ApplyCursorColors()
        {
            CacheDefaultCursorColors();

            var currentColor = colorState?.CurrentColor ?? Color.white;

            if (paletteCursor != null && hasDefaultCursorColor)
            {
                paletteCursor.color = GetCursorDisplayColor(currentColor, defaultCursorColor);
            }

            if (paletteCursorSecondary != null && hasDefaultCursorSecondaryColor)
            {
                paletteCursorSecondary.color = GetCursorDisplayColor(currentColor, defaultCursorSecondaryColor);
            }
        }

        private Color GetCursorDisplayColor(Color baseColor, Color originalColor)
        {
            switch (cursorContrastMode)
            {
                case CursorContrastMode.InvertColor:
                    return GetInvertedColor(baseColor, originalColor);
                case CursorContrastMode.InvertLightness:
                    return GetInvertedLightnessColor(baseColor, originalColor);
                case CursorContrastMode.None:
                default:
                    return originalColor;
            }
        }

        private static Color GetInvertedColor(Color baseColor, Color originalColor)
        {
            return new Color(1f - baseColor.r, 1f - baseColor.g, 1f - baseColor.b, originalColor.a);
        }

        private static Color GetInvertedLightnessColor(Color baseColor, Color originalColor)
        {
            var brightness = GetPerceivedBrightness(baseColor);
            var invertedBrightness = brightness >= 0.5f ? 0f : 1f;
            return new Color(invertedBrightness, invertedBrightness, invertedBrightness, originalColor.a);
        }

        private static float GetPerceivedBrightness(Color color)
        {
            return Mathf.Clamp01(0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b);
        }

        private static Vector2 ClosestPointOnTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var q1 = ClosestPointOnSegment(a, b, p);
            var q2 = ClosestPointOnSegment(b, c, p);
            var q3 = ClosestPointOnSegment(c, a, p);
            var d1 = (p - q1).sqrMagnitude;
            var d2 = (p - q2).sqrMagnitude;
            var d3 = (p - q3).sqrMagnitude;
            if (d1 <= d2 && d1 <= d3) return q1;
            if (d2 <= d1 && d2 <= d3) return q2;
            return q3;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            var t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-6f);
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }
        
        public bool TryFindColorInTexture(Color targetColor, out Vector2 uvPosition)
        {
            uvPosition = Vector2.zero;
            if (currentTexture == null)
                return false;
            
            const float colorTolerance = 0.02f;
            var samplingStep = 1;
            var pixelCount = currentTexture.width * currentTexture.height;
            if (pixelCount > 65536)
            {
                samplingStep = 2;
            }
            else if (pixelCount > 262144)
            {
                samplingStep = 4;
            }
            
            var minDistance = float.MaxValue;
            var bestMatch = Vector2.zero;
            var foundExactMatch = false;
            
            for (var y = 0; y < currentTexture.height; y += samplingStep)
            {
                for (var x = 0; x < currentTexture.width; x += samplingStep)
                {
                    var pixelColor = currentTexture.GetPixel(x, y);
                    if (IsColorIgnored(pixelColor))
                        continue;
                    
                    var distance = Mathf.Abs(pixelColor.r - targetColor.r) +
                                   Mathf.Abs(pixelColor.g - targetColor.g) +
                                   Mathf.Abs(pixelColor.b - targetColor.b);
                    
                    if (distance < colorTolerance * 3f)
                    {
                        uvPosition = new Vector2(
                            (x + 0.5f) / currentTexture.width,
                            (y + 0.5f) / currentTexture.height
                        );
                        foundExactMatch = true;
                        break;
                    }
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestMatch = new Vector2(
                            (x + 0.5f) / currentTexture.width,
                            (y + 0.5f) / currentTexture.height
                        );
                    }
                }
                
                if (foundExactMatch) break;
            }
            
            if (!foundExactMatch && minDistance < colorTolerance * 6f)
            {
                uvPosition = bestMatch;
                return true;
            }
            
            return foundExactMatch;
        }
    }
}