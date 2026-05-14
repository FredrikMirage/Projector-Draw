using UnityEngine;
using UnityEngine.UI;
using XDPaint;
using XDPaint.Core;
using XDPaint.States;

public class DoodleController : MonoBehaviour
{
    [Header("Core References")]
    public PaintManager paintManager;

    [Header("UI Elements")]
    public Slider brushSizeSlider;
    public Button undoButton;
    public Button redoButton;

    [Header("Preview Elements")]
    public RectTransform previewCircle; // Dra in din BrushPreview (UI Image) här
    public float maxPreviewSize = 200f;  // Hur många pixlar stor cirkeln är vid slider-värde 1.0
    public Image backgroundImage;
    public enum EditMode { Brush, Background }
    public EditMode currentMode = EditMode.Brush;

    private Image previewImage; // Intern referens för att byta färg

    void Start()
    {
        /*
        paintManager.OnInitialized += (manager) => {
            var rt = manager.GetPaintTexture();
            Debug.Log($"Ritytans upplösning: {rt.width}x{rt.height}");
        };
        */

        if (previewCircle != null)
        {
            previewImage = previewCircle.GetComponent<Image>();
        }

        if (brushSizeSlider != null)
        {
            brushSizeSlider.value = 0.2f;
            // Uppdatera preview direkt vid start
            UpdatePreview(brushSizeSlider.value);
        }

        if (paintManager != null)
        {
            paintManager.OnInitialized += OnPaintInitialized;
        }

        brushSizeSlider.onValueChanged.AddListener(SetBrushSize);
        undoButton.onClick.AddListener(Undo);
        redoButton.onClick.AddListener(Redo);
    }



    private void OnPaintInitialized(PaintManager manager)
    {
        if (StatesSettings.Instance != null)
        {
            StatesSettings.Instance.UndoRedoEnabled = true;
            StatesSettings.Instance.UndoRedoMaxActionsCount = 20;
            //Debug.Log("XDPaint: Undo-kapacitet satt till 20");
        }

        if (brushSizeSlider != null)
        {
            manager.Brush.Size = brushSizeSlider.value;
            // Säkerställ att preview matchar även när XDPaint vaknar
            UpdatePreview(brushSizeSlider.value);

            // Sätt även preview-färgen till penselns startfärg
            if (previewImage != null)
                previewImage.color = manager.Brush.Color;
        }

        if (manager.StatesController != null)
        {
            manager.StatesController.Enable();
            manager.StatesController.OnUndoStatusChanged += (canUndo) => undoButton.interactable = canUndo;
            manager.StatesController.OnRedoStatusChanged += (canRedo) => redoButton.interactable = canRedo;
            undoButton.interactable = manager.StatesController.CanUndo();
            redoButton.interactable = manager.StatesController.CanRedo();
        }
    }

    // --- PENSELSTORLEK ---
    public void SetBrushSize(float size)
    {
        if (paintManager != null && paintManager.Initialized)
        {
            paintManager.Brush.Size = size;
        }
        UpdatePreview(size);
    }

    private void UpdatePreview(float size)
    {
        if (previewCircle != null)
        {
            float pixelSize = size   * maxPreviewSize;
            previewCircle.sizeDelta = new Vector2(pixelSize, pixelSize);
        }
    }

    // --- FÄRG ---
    public void SetColor(Color newColor)
    {
        if (paintManager != null && paintManager.Initialized)
        {
            if (currentMode == EditMode.Brush)
            {
                // LÄGE: PENSEL
                paintManager.Brush.SetColor(newColor);

                // Uppdatera preview-cirkeln om du har en
                if (previewCircle != null)
                    previewCircle.GetComponent<Image>().color = newColor;

                Debug.Log("Penselfärg ändrad.");
            }
            else
            {
                // LÄGE: BAKGRUND
                if (backgroundImage != null)
                {
                    backgroundImage.color = newColor;
                    Debug.Log("Bakgrundsfärg ändrad.");
                }
            }
        }
    }

    // --- UNDO / REDO ---
    public void Undo()
    {
        if (paintManager != null && paintManager.StatesController != null)
        {
            if (paintManager.StatesController.CanUndo())
            {
                paintManager.StatesController.Undo();
                paintManager.Render();
            }
        }
    }

    public void Redo()
    {
        if (paintManager != null && paintManager.StatesController != null)
        {
            if (paintManager.StatesController.CanRedo())
            {
                paintManager.StatesController.Redo();
                paintManager.Render();
            }
        }
    }

    // --- INPUT BLOCKING ---
    public void BlockPainting()
    {
        if (paintManager != null && paintManager.Initialized)
        {
            paintManager.PaintObject.ProcessInput = false;
            paintManager.PaintObject.FinishPainting();
        }
    }

    public void UnblockPainting()
    {
        if (paintManager != null && paintManager.Initialized)
        {
            paintManager.PaintObject.ProcessInput = true;
        }
    }
    public void ClearCanvas()
    {
        if (paintManager != null && paintManager.Initialized)
        {
            // 1. Hämta det aktiva lagrets textur
            var activeLayer = paintManager.LayersController.ActiveLayer;
            RenderTexture rt = activeLayer.RenderTexture;

            // 2. Rensa texturen manuellt med Unitys grafikmotor
            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear); // Gör allt 100% genomskinligt
            RenderTexture.active = previousRT;

            // 3. Nollställ historiken så man inte kan göra Undo
            if (paintManager.StatesController != null)
            {
                paintManager.StatesController.Enable(); // Säkerställ att den är på
                                                        // Om .Clear() inte finns, testa .Reset() eller .DisposeStates()
                                                        // Men oftast räcker det att bara rendera om ytan
            }

            // 4. Tvinga XDPaint att visa den tomma ytan
            paintManager.Render();

            // Uppdatera knapparna
            undoButton.interactable = false;
            redoButton.interactable = false;

            Debug.Log("Canvas rensad med GL.Clear");
        }
    }

    // Denna metod anropas av din "Brush Toggle"
    public void SetModeToBrush(bool isOn)
    {
        Debug.Log("Brush");
        if (isOn) currentMode = EditMode.Brush;
    }

    // Denna metod anropas av din "Background Toggle"
    public void SetModeToBackground(bool isOn)
    {
        Debug.Log("BG");
        if (isOn) currentMode = EditMode.Background;
    }
}