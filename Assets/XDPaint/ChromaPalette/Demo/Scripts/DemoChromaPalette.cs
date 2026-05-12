using UnityEngine;
using UnityEngine.UI;
using XDPaint.ChromaPalette.Core;

namespace XDPaint.ChromaPalette.Demo
{
    public class DemoChromaPalette : MonoBehaviour
    {
        [SerializeField] private ColorPickerManager colorPickerManager;
        [SerializeField] private Toggle colorPickerCircleMode;
        [SerializeField] private Toggle colorPickerCircleFullMode;
        [SerializeField] private Toggle colorPickerCircles;
        [SerializeField] private Toggle colorPickerCircleTriangleMode;
        [SerializeField] private Toggle colorPickerRectMode;
        [SerializeField] private Toggle colorPickerPaletteMode;

        private void Start()
        {
            colorPickerRectMode.onValueChanged.AddListener(OnColorPickerRectangleMode);
            colorPickerCircleMode.onValueChanged.AddListener(OnColorPickerCircleMode);
            colorPickerCircleFullMode.onValueChanged.AddListener(OnColorPickerCircleFillMode);
            colorPickerCircles.onValueChanged.AddListener(OnColorPickerCirclesMode);
            colorPickerCircleTriangleMode.onValueChanged.AddListener(OnColorPickerCircleTriangleMode);
            colorPickerPaletteMode.onValueChanged.AddListener(OnColorPickerPaletteMode);
        }
        
        private void OnColorPickerRectangleMode(bool isOs)
        {
            if (!isOs)
                return;

            colorPickerManager.SetMode(ColorPickerMode.Rectangle);
        }
        
        private void OnColorPickerCircleMode(bool isOs)
        {
            if (!isOs)
                return;

            colorPickerManager.SetMode(ColorPickerMode.Circle);
        }
        
        private void OnColorPickerCircleFillMode(bool isOs)
        {
            if (!isOs)
                return;

            colorPickerManager.SetMode(ColorPickerMode.CircleFull);
        }

        private void OnColorPickerCirclesMode(bool isOs)
        {
            if (!isOs)
                return;

            colorPickerManager.SetMode(ColorPickerMode.CircleCircle);
        }
        
        private void OnColorPickerCircleTriangleMode(bool isOs)
        {
            if (!isOs)
                return;

            colorPickerManager.SetMode(ColorPickerMode.CircleTriangle);
        }
        
        private void OnColorPickerPaletteMode(bool isOs)
        {
            if (!isOs)
                return;

            colorPickerManager.SetMode(ColorPickerMode.Palette);
        }
    }
}