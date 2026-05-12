using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Scripting;
using XDPaint.Core;
using XDPaint.Core.PaintModes;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public sealed class BrushTool : BasePaintTool<BrushToolSettings>
    {
        [Preserve]
        public BrushTool(IPaintData paintData) : base(paintData)
        {
            Settings = new BrushToolSettings(paintData);
        }

        public override PaintTool Type => PaintTool.Brush;
        public override bool RequiredCombinedTempTexture => Settings.UsePattern;

        public override void Enter()
        {
            base.Enter();
            if (Settings.PatternTexture == null)
            {
                Settings.PatternTexture = Tools.Settings.Instance.DefaultPatternTexture;
            }
            
            ToolSettingsOnPropertyChanged(this, new PropertyChangedEventArgs(nameof(Settings.UsePattern)));
            Settings.PropertyChanged += ToolSettingsOnPropertyChanged;
        }
        
        public override void Exit()
        {
            Settings.PropertyChanged -= ToolSettingsOnPropertyChanged;
            base.Exit();
            Data.PaintMaterial.DisableKeyword(Constants.PaintShader.TileKeyword);
        }

        public override void SetPaintMode(IPaintMode mode)
        {
            base.SetPaintMode(mode);
            ToolSettingsOnPropertyChanged(this, new PropertyChangedEventArgs(nameof(Settings.UsePattern)));
        }
        
        private void ToolSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.UsePattern))
            {
                if (!Data.PaintMode.UsePaintInput && Settings.UsePattern)
                {
                    Debug.LogWarning("PaintMode Default does not supports pattern painting, please, switch to PaintMode Additive.");
                    Settings.usePattern = false;
                    Data.PaintMaterial.DisableKeyword(Constants.PaintShader.TileKeyword);
                }
                else
                {
                    if (Settings.UsePattern)
                    {
                        Data.PaintMaterial.EnableKeyword(Constants.PaintShader.TileKeyword);
                    }
                    else
                    {
                        Data.PaintMaterial.DisableKeyword(Constants.PaintShader.TileKeyword);
                    }
                }
                
                UpdateCombinedTempTexture(false);
            }
        }
        
        protected override void RenderPreviewWorld(UnityEngine.Rendering.RenderTargetIdentifier combined)
        {
            if (Settings.UsePattern && Settings.PatternTexture != null)
            {
                // For world space with patterns, reuse CombinedTemp to avoid allocating new RT
                // Step 1: Clear CombinedTemp to transparent black for brush rendering
                Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.CombinedTemp)).ClearRenderTarget(Color.clear).Execute();
                
                // Step 2: Render world space brush to CombinedTemp (just the brush, no background)
                var previousMainTexture = Data.PaintWorldMaterial.GetTexture(Constants.PaintWorldShader.MainTexture);
                Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, Texture2D.blackTexture);
                Data.PaintWorldMaterial.color = Data.Brush.Color;
                OnPaintWorldRender.Invoke(GetTarget(RenderTarget.CombinedTemp));
                Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, previousMainTexture);
                
                // Step 3: Set up for pattern composite - use the combined (background) as paint and brush as input with pattern
                var previousPaintTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.PaintTexture);
                var previousInputTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.InputTexture);
                
                // Combined has the background, CombinedTemp has the brush shape
                Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.Combined));
                Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.CombinedTemp));
                
                // Use Blend pass to apply pattern to the brush
                Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(combined).DrawMesh(Data.QuadMesh, Data.PaintMaterial, PaintPass.Blend).Execute();
                
                // Restore previous textures
                Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, previousPaintTexture);
                Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, previousInputTexture);
            }
            else
            {
                base.RenderPreviewWorld(combined);
            }
        }
    }
}