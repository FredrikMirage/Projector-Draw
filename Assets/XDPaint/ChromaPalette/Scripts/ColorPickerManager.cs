using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using XDPaint.ChromaPalette.Core;
using XDPaint.ChromaPalette.Core.Modes;
using XDPaint.ChromaPalette.Internal.State;
using XDPaint.ChromaPalette.Internal.UI;
using XDPaint.ChromaPalette.Internal.Rendering;
using XDPaint.ChromaPalette.Internal.Modes;
using XDPaint.ChromaPalette.Utilities;

namespace XDPaint.ChromaPalette
{
    /// <summary>
    /// Main color picker component that acts as a facade, coordinating all subsystems.
    /// </summary>
    [ExecuteAlways]
    public class ColorPickerManager : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private ColorPickerMode mode = ColorPickerMode.Rectangle;
        [SerializeField] private TextureModeSettings textureSettings = new();
        [SerializeField] private PaletteModeSettings paletteSettings = new();
        
        [SerializeField] private RawImage paletteImage;
        [SerializeField] private RectTransform paletteRect;
        [SerializeField] private Image paletteCursor;
        [SerializeField] private Image paletteCursorExternal;
        [SerializeField] private CursorContrastMode cursorContrastMode = CursorContrastMode.InvertColor;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Slider valueSlider;
        [SerializeField] private Slider alphaSlider;
        [SerializeField] private RawImage hueSliderBackground;
        [SerializeField] private RawImage valueSliderBackground;
        [SerializeField] private RawImage alphaSliderBackground;
        [SerializeField] private Image colorImage;
        [SerializeField] private Image previousColorImage;

        [SerializeField] private TMP_InputField inputH;
        [SerializeField] private TMP_InputField inputS;
        [SerializeField] private TMP_InputField inputV;
        [SerializeField] private TMP_InputField inputR;
        [SerializeField] private TMP_InputField inputG;
        [SerializeField] private TMP_InputField inputB;
        [SerializeField] private TMP_InputField inputC;
        [SerializeField] private TMP_InputField inputM;
        [SerializeField] private TMP_InputField inputY;
        [SerializeField] private TMP_InputField inputK;
        [SerializeField] private TMP_InputField inputHex;
        [SerializeField] private TMP_InputField inputAlpha;

        [SerializeField] private UnityEvent<Color> onColorChanging = new();
        [SerializeField] private UnityEvent<Color> onColorChanged = new();

        private ColorStateStore colorState;
        private PaletteShaderBridge shaderBridge;
        private InputFieldBindings inputBindings;
        private PaletteInteraction paletteInteraction;
        private UIUpdater uiUpdater;
        private PaletteModeCoordinator modeCoordinator;
        private TextureSampler textureSampler;

        public Color CurrentColor => colorState?.CurrentColor ?? Color.white;
        public bool IsDragging => paletteInteraction?.IsDragging ?? false;
        public UnityEvent<Color> OnColorChanging => onColorChanging;
        public UnityEvent<Color> OnColorChanged => onColorChanged;
        public CursorContrastMode CursorContrast
        {
            get => cursorContrastMode;
            set
            {
                if (cursorContrastMode == value)
                    return;
                cursorContrastMode = value;
                if (paletteInteraction != null)
                {
                    paletteInteraction.SetCursorContrastMode(cursorContrastMode);
                    paletteInteraction.RefreshCursorColors();
                }
            }
        }

        private void Awake()
        {
            InitializeColorPicker();
        }

        private void InitializeColorPicker()
        {
            CreateServices();
            
            colorState.OnColorChanging += color => onColorChanging?.Invoke(color);
            colorState.OnColorChanged += color => onColorChanged?.Invoke(color);
            
            InitializeShaderComponents();
            SetupComponents();
            SetupSliders();
            
            modeCoordinator?.UpdateMode();
            textureSampler?.UpdateTextureFromPalette();
            if (colorState?.CurrentColor == default(Color))
            {
                SetColor(Color.white, false);
            }
            
            uiUpdater?.UpdateAllUI();
            
            HandleEditorModeInitialization();
        }
        
        private void EnsureServicesInitialized()
        {
            if (colorState == null || shaderBridge == null)
            {
                InitializeColorPicker();
            }
        }

        private void Start()
        {
            HandlePlayModeInitialization();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && colorState == null && paletteImage != null)
                EnsureServicesInitialized();
#endif
            
            if (shaderBridge?.NeedsMaterialRecreation() ?? false)
            {
                shaderBridge.ForceReapplyMaterials();
                shaderBridge.UpdatePaletteMode(mode);
                textureSampler?.UpdateSamplingSettings();
            }

            if (mode == ColorPickerMode.Palette && paletteSettings?.ColorPalette != null)
            {
                textureSampler?.UpdateTextureFromPalette();
            }
            
            if (colorState != null)
            {
                shaderBridge?.UpdatePaletteProperties(colorState.Hue, colorState.Value);
                shaderBridge?.UpdateValueSlider(colorState.Value);
                shaderBridge?.UpdateAlphaSlider(colorState.Alpha, CurrentColor);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (paletteImage != null)
            {
                if (Application.isPlaying)
                {
                    EnsureServicesInitialized();
                    modeCoordinator?.SetMode(mode, true);
                    textureSampler?.UpdateTextureFromPalette();
                    textureSampler?.UpdateSamplingSettings();
                    paletteInteraction?.SetCursorContrastMode(cursorContrastMode);
                    uiUpdater?.UpdateAllUI();
                    paletteInteraction?.RefreshCursorColors();
                }
                else
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this != null)
                        {
                            EnsureServicesInitialized();
                            shaderBridge?.UpdatePaletteMode(mode);
                            modeCoordinator?.SetMode(mode, true);
                            paletteInteraction?.SetMode(mode);
                            paletteInteraction?.SetCursorContrastMode(cursorContrastMode);
                            textureSampler?.UpdateTextureFromPalette();
                            textureSampler?.UpdateSamplingSettings();
                            uiUpdater?.UpdateAllUI();
                            paletteInteraction?.RefreshCursorColors();
                        }
                    };
                }
            }
        }
#endif

        private void OnDestroy()
        {
            inputBindings?.RemoveListeners();
            shaderBridge?.Cleanup();
            uiUpdater?.Cleanup();
        }

        public void SetColor(Color color, bool notify = true)
        {
            colorState?.SetColorFromRGB(color, notify);
            uiUpdater?.UpdateAllUI();
        }

        public void SetMode(ColorPickerMode newMode)
        {
            if (mode != newMode)
            {
                var previousColor = CurrentColor;
                mode = newMode;
                modeCoordinator?.SetMode(newMode);

                if (newMode == ColorPickerMode.Palette || newMode == ColorPickerMode.Texture)
                {
                    var availableColors = modeCoordinator?.GetAvailableColors();
                    if (availableColors != null && availableColors.Length > 0)
                    {
                        var nearestColor = ColorConversionUtility.FindNearestColor(previousColor, availableColors);
                        if (!nearestColor.Equals(previousColor))
                        {
                            SetColor(nearestColor, false);
                        }
                    }
                }

                textureSampler?.UpdateTextureDependentComponents();
                textureSampler?.UpdateSamplingSettings();
                paletteInteraction?.SetCursorContrastMode(cursorContrastMode);
                paletteInteraction?.RefreshCursorColors();
                if (Application.isPlaying)
                    uiUpdater?.UpdateAllUI();
            }
        }

        public void SetPaletteTexture(Texture2D texture)
        {
            textureSettings ??= new TextureModeSettings();
            textureSettings.Texture = texture;
            mode = ColorPickerMode.Texture;
            modeCoordinator?.SetPaletteTexture(texture);
        }

        public void OnPointerDown(PointerEventData eventData) => paletteInteraction?.HandlePointerDown(eventData);

        public void OnDrag(PointerEventData eventData) => paletteInteraction?.HandleDrag(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            paletteInteraction?.HandlePointerUp();
            colorState?.NotifyColorChanged();
        }

        private void SetupSliders()
        {
            ConfigureSlider(hueSlider, 0f, 1f, Slider.Direction.BottomToTop);
            ConfigureSlider(valueSlider, 0f, 1f, Slider.Direction.TopToBottom);
            ConfigureSlider(alphaSlider, 0f, 1f, Slider.Direction.LeftToRight);
            
            hueSlider?.onValueChanged.AddListener(value =>
            {
                colorState?.SetHue(1f - value, true);
                uiUpdater?.UpdateAllUI();
            });

            valueSlider?.onValueChanged.AddListener(value =>
            {
                colorState?.SetValue(1f - value, true);
                uiUpdater?.UpdateAllUI();
            });

            alphaSlider?.onValueChanged.AddListener(value =>
            {
                colorState?.SetAlpha(value, true);
                uiUpdater?.UpdateAllUI();
            });

            AddSliderPointerUpHandler(hueSlider);
            AddSliderPointerUpHandler(valueSlider);
            AddSliderPointerUpHandler(alphaSlider);
        }

        private void ConfigureSlider(Slider slider, float min, float max, Slider.Direction direction)
        {
            if (slider != null)
            {
                slider.minValue = min;
                slider.maxValue = max;
                slider.direction = direction;
            }
        }

        private void AddSliderPointerUpHandler(Slider slider)
        {
            if (slider != null)
            {
                var trigger = slider.gameObject.GetComponent<EventTrigger>() ?? slider.gameObject.AddComponent<EventTrigger>();
                var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                pointerUp.callback.AddListener(_ => colorState?.NotifyColorChanged());
                trigger.triggers.Add(pointerUp);
            }
        }

        public void UpdatePaletteMode(ColorPickerMode mode) => shaderBridge?.UpdatePaletteMode(mode);
        public void ForceReapplyMaterials() => shaderBridge?.ForceReapplyMaterials();
        public void UpdateAllUI() => uiUpdater?.UpdateAllUI();
        public void UpdateCursor() => uiUpdater?.UpdateCursor();
        public PaletteModeSettings GetPaletteModeSettings() => paletteSettings;
        public void SetCursorContrastMode(CursorContrastMode mode) => CursorContrast = mode;

        #region Initialization Methods

        private void CreateServices()
        {
            colorState = new ColorStateStore();
            shaderBridge = new PaletteShaderBridge();
            inputBindings = new InputFieldBindings();
            paletteInteraction = new PaletteInteraction();
            uiUpdater = new UIUpdater();
            modeCoordinator = new PaletteModeCoordinator();
            textureSampler = new TextureSampler();
        }
        
        private void InitializeShaderComponents()
        {
            shaderBridge?.Initialize(paletteImage, hueSliderBackground, 
                valueSliderBackground, alphaSliderBackground);
        }
        
        private void SetupComponents()
        {
            if (modeCoordinator != null)
            {
                modeCoordinator.Initialize(
                    shaderBridge,
                    paletteInteraction,
                    hueSlider?.gameObject,
                    valueSlider?.gameObject
                );
                modeCoordinator.SetModeSettings(textureSettings, paletteSettings);
                modeCoordinator.SetMode(mode, true);
            }
            
            textureSampler?.Initialize(shaderBridge, paletteInteraction, modeCoordinator);
            
            if (uiUpdater != null)
            {
                uiUpdater.Initialize(
                    colorState,
                    shaderBridge,
                    inputBindings,
                    paletteInteraction,
                    hueSlider,
                    valueSlider,
                    alphaSlider,
                    colorImage,
                    previousColorImage
                );
            }
            
            if (inputBindings != null)
            {
                inputBindings.Initialize(
                    colorState,
                    inputH, inputS, inputV,
                    inputR, inputG, inputB,
                    inputC, inputM, inputY, inputK,
                    inputHex, inputAlpha
                );
                inputBindings.SetupListeners();
                inputBindings.OnInputValueChanged += () =>
                {
                    colorState?.NotifyColorChanged();
                    uiUpdater?.UpdateAllUI();
                };
            }
            
            if (paletteInteraction != null)
            {
                paletteInteraction.Initialize(
                    colorState,
                    paletteRect,
                    paletteCursor,
                    paletteCursorExternal,
                    () => modeCoordinator?.GetBicubicSampling() ?? false,
                    () => modeCoordinator?.GetCursorSnapping() ?? false,
                    () => modeCoordinator?.GetIgnoreTransparent() ?? true,
                    () => modeCoordinator?.GetTransparentAlphaThreshold() ?? 0.01f,
                    () => modeCoordinator?.GetIgnoredColors() ?? System.Array.Empty<Color>(),
                    () => modeCoordinator?.GetIgnoreColorTolerance() ?? 0.02f
                );
                paletteInteraction.SetMode(mode);
                paletteInteraction.SetCursorContrastMode(cursorContrastMode);
                paletteInteraction.RefreshCursorColors();
                paletteInteraction.OnPaletteUpdated += () => uiUpdater?.UpdateAllUI();
            }
            
            textureSampler?.UpdateTextureDependentComponents();
            textureSampler?.UpdateSamplingSettings();
        }
        
        private void HandleEditorModeInitialization()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        UpdatePaletteMode(mode);
                        uiUpdater?.UpdateAllUI();
                    }
                };
            }
#endif
        }
        
        private void HandlePlayModeInitialization()
        {
            if (Application.isPlaying && paletteImage != null)
            {
                shaderBridge?.ForceReapplyMaterials();
                shaderBridge?.UpdatePaletteMode(mode);
                
                textureSampler?.UpdateSamplingSettings();
                textureSampler?.UpdateTextureDependentComponents();
                
                uiUpdater?.UpdateAllUI();
                paletteInteraction?.SetCursorContrastMode(cursorContrastMode);
                paletteInteraction?.RefreshCursorColors();
            }
        }

        #endregion
    }
}
