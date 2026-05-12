using System;
using TMPro;
using UnityEngine;
using XDPaint.ChromaPalette.Utilities;
using XDPaint.ChromaPalette.Internal.State;

namespace XDPaint.ChromaPalette.Internal.UI
{
    /// <summary>
    /// Service responsible for managing all input field operations.
    /// Handles input validation, parsing, and field updates while preventing circular updates.
    /// </summary>
    public class InputFieldBindings
    {
        private TMP_InputField inputH;
        private TMP_InputField inputS;
        private TMP_InputField inputV;
        private TMP_InputField inputR;
        private TMP_InputField inputG;
        private TMP_InputField inputB;
        private TMP_InputField inputC;
        private TMP_InputField inputM;
        private TMP_InputField inputY;
        private TMP_InputField inputK;
        private TMP_InputField inputHex;
        private TMP_InputField inputAlpha;
        
        private bool isUpdatingFields;
        
        private ColorStateStore colorState;
        
        public event Action OnInputValueChanged;
        
        public void Initialize(
            ColorStateStore colorStateManager,
            TMP_InputField h, TMP_InputField s, TMP_InputField v,
            TMP_InputField r, TMP_InputField g, TMP_InputField b,
            TMP_InputField c, TMP_InputField m, TMP_InputField y, TMP_InputField k,
            TMP_InputField hex, TMP_InputField alpha)
        {
            colorState = colorStateManager;
            
            inputH = h;
            inputS = s;
            inputV = v;
            inputR = r;
            inputG = g;
            inputB = b;
            inputC = c;
            inputM = m;
            inputY = y;
            inputK = k;
            inputHex = hex;
            inputAlpha = alpha;
        }
        
        public void SetupListeners()
        {
            AddInputListener(inputH, OnHChanged);
            AddInputListener(inputS, OnSChanged);
            AddInputListener(inputV, OnVChanged);
            
            AddInputListener(inputR, OnRChanged);
            AddInputListener(inputG, OnGChanged);
            AddInputListener(inputB, OnBChanged);
            
            AddInputListener(inputC, OnCChanged);
            AddInputListener(inputM, OnMChanged);
            AddInputListener(inputY, OnYChanged);
            AddInputListener(inputK, OnKChanged);
            
            AddInputListener(inputHex, OnHexChanged);
            AddInputListener(inputAlpha, OnAlphaInputChanged);
        }
        
        public void RemoveListeners()
        {
            RemoveInputListener(inputH, OnHChanged);
            RemoveInputListener(inputS, OnSChanged);
            RemoveInputListener(inputV, OnVChanged);
            RemoveInputListener(inputR, OnRChanged);
            RemoveInputListener(inputG, OnGChanged);
            RemoveInputListener(inputB, OnBChanged);
            RemoveInputListener(inputC, OnCChanged);
            RemoveInputListener(inputM, OnMChanged);
            RemoveInputListener(inputY, OnYChanged);
            RemoveInputListener(inputK, OnKChanged);
            RemoveInputListener(inputHex, OnHexChanged);
            RemoveInputListener(inputAlpha, OnAlphaInputChanged);
        }
        
        public void UpdateAllFields()
        {
            if (isUpdatingFields || colorState == null)
                return;
            
            isUpdatingFields = true;
            
            UpdateHSVFields();
            UpdateRGBFields();
            UpdateCMYKFields();
            UpdateOtherFields();
            
            isUpdatingFields = false;
        }
        
        private void UpdateHSVFields()
        {
            var hsv = colorState.ColorHSV;
            SetFieldText(inputH, Mathf.RoundToInt(hsv.H * 360f));
            SetFieldText(inputS, Mathf.RoundToInt(hsv.S * 100f));
            SetFieldText(inputV, Mathf.RoundToInt(hsv.V * 100f));
        }
        
        private void UpdateRGBFields()
        {
            var color = colorState.CurrentColor;
            SetFieldText(inputR, Mathf.RoundToInt(color.r * 255f));
            SetFieldText(inputG, Mathf.RoundToInt(color.g * 255f));
            SetFieldText(inputB, Mathf.RoundToInt(color.b * 255f));
        }
        
        private void UpdateCMYKFields()
        {
            var cmyk = colorState.ColorCMYK;
            SetFieldText(inputC, Mathf.RoundToInt(cmyk.C * 100f));
            SetFieldText(inputM, Mathf.RoundToInt(cmyk.M * 100f));
            SetFieldText(inputY, Mathf.RoundToInt(cmyk.Y * 100f));
            SetFieldText(inputK, Mathf.RoundToInt(cmyk.K * 100f));
        }
        
        private void UpdateOtherFields()
        {
            if (inputHex != null)
            {
                var hex = colorState.GetHexString(colorState.Alpha < 0.999f);
                inputHex.text = hex;
            }
            
            SetFieldText(inputAlpha, Mathf.RoundToInt(colorState.Alpha * 100f));
        }
        
        private void SetFieldText(TMP_InputField field, int value)
        {
            if (field != null)
            {
                field.text = value.ToString();
            }
        }
        
        private void AddInputListener(TMP_InputField field, UnityEngine.Events.UnityAction<string> action)
        {
            if (field != null)
                field.onEndEdit.AddListener(action);
        }
        
        private void RemoveInputListener(TMP_InputField field, UnityEngine.Events.UnityAction<string> action)
        {
            if (field != null)
            {
                field.onEndEdit.RemoveListener(action);
            }
        }
        
        private bool TryParseInt(string text, out int value, int min, int max)
        {
            if (int.TryParse(text, out value))
            {
                value = Mathf.Clamp(value, min, max);
                return true;
            }
            
            return false;
        }
        
        private void OnHChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var h, 0, 360))
            {
                colorState?.SetHue(h / 360f);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnSChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var s, 0, 100))
            {
                colorState?.SetSaturation(s / 100f);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnVChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var v, 0, 100))
            {
                colorState?.SetValue(v / 100f);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnRChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var r, 0, 255))
            {
                colorState?.SetRed(r / 255f);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnGChanged(string text)
        {
            if (isUpdatingFields)
                return;
            
            if (TryParseInt(text, out var g, 0, 255))
            {
                colorState?.SetGreen(g / 255f);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnBChanged(string text)
        {
            if (isUpdatingFields)
                return;
            
            if (TryParseInt(text, out var b, 0, 255))
            {
                colorState?.SetBlue(b / 255f);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnCChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var c, 0, 100))
            {
                var cmyk = colorState.ColorCMYK;
                cmyk.C = c / 100f;
                colorState?.SetColorFromCMYK(cmyk);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnMChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var m, 0, 100))
            {
                var cmyk = colorState.ColorCMYK;
                cmyk.M = m / 100f;
                colorState?.SetColorFromCMYK(cmyk);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnYChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var y, 0, 100))
            {
                var cmyk = colorState.ColorCMYK;
                cmyk.Y = y / 100f;
                colorState?.SetColorFromCMYK(cmyk);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnKChanged(string text)
        {
            if (isUpdatingFields) return;
            
            if (TryParseInt(text, out var k, 0, 100))
            {
                var cmyk = colorState.ColorCMYK;
                cmyk.K = k / 100f;
                colorState?.SetColorFromCMYK(cmyk);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnHexChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (ColorConversionUtility.TryParseHex(text, out var color))
            {
                colorState?.SetColorFromRGB(color, false);
                OnInputValueChanged?.Invoke();
            }
        }
        
        private void OnAlphaInputChanged(string text)
        {
            if (isUpdatingFields)
                return;

            if (TryParseInt(text, out var a, 0, 100))
            {
                colorState?.SetAlpha(a / 100f);
                OnInputValueChanged?.Invoke();
            }
        }
    }
}