using UnityEngine;
using UnityEngine.UI;

namespace XDPaint.ChromaPalette.Utilities
{
    // Applies palette shader parameters to the final UI material (after Mask/Stencil).
    // Attach to the same GameObject as the palette RawImage.
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class PaletteMaterialModifier : BaseMeshEffect, IMaterialModifier
    {
        private static readonly int ShaderMode = Shader.PropertyToID("_Mode");
        private static readonly int ShaderHueProperty = Shader.PropertyToID("_Hue");
        private static readonly int ShaderValueProperty = Shader.PropertyToID("_Value");
        private static readonly int ShaderUseBicubicSampling = Shader.PropertyToID("_UseBicubicSampling");
        private static readonly int ShaderPaletteTexture = Shader.PropertyToID("_PaletteTexture");

        [SerializeField] private float modeValue; // 0 Rect, 1 Circle, 2 Texture
        [SerializeField] private float hue = 0f;
        [SerializeField] private float value = 1f;
        [SerializeField] private bool useBicubic = false;
        [SerializeField] private Texture paletteTexture;

        public void SetMode(float mode)
        {
            if (!Mathf.Approximately(modeValue, mode))
            {
                modeValue = mode;
                MarkDirty();
            }
        }

        public void SetHueValue(float h, float v)
        {
            var changed = !Mathf.Approximately(hue, h) || !Mathf.Approximately(value, v);
            if (changed)
            {
                hue = h;
                value = v;
                MarkDirty();
            }
        }

        public void SetUseBicubic(bool use)
        {
            if (useBicubic != use)
            {
                useBicubic = use;
                MarkDirty();
            }
        }

        public void SetPaletteTexture(Texture texture)
        {
            if (paletteTexture != texture)
            {
                paletteTexture = texture;
                MarkDirty();
            }
        }

        public void ForceApplyNow()
        {
            MarkDirty();
        }

        private void MarkDirty()
        {
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
                graphic.SetVerticesDirty();
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (baseMaterial == null)
                return null;

            // Apply parameters directly to the final material used for rendering (after Mask/Stencil)
            if (baseMaterial.HasProperty(ShaderMode))
            {
                baseMaterial.SetFloat(ShaderMode, modeValue);
            }

            if (baseMaterial.HasProperty(ShaderHueProperty))
            {
                baseMaterial.SetFloat(ShaderHueProperty, hue);
            }

            if (baseMaterial.HasProperty(ShaderValueProperty))
            {
                baseMaterial.SetFloat(ShaderValueProperty, value);
            }

            if (baseMaterial.HasProperty(ShaderUseBicubicSampling))
            {
                baseMaterial.SetFloat(ShaderUseBicubicSampling, useBicubic ? 1f : 0f);
            }

            if (paletteTexture != null && baseMaterial.HasProperty(ShaderPaletteTexture))
            {
                baseMaterial.SetTexture(ShaderPaletteTexture, paletteTexture);
            }

            return baseMaterial;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
        }
    }
}
