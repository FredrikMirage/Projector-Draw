using UnityEngine;
using XDPaint.Core;
using XDPaint.Core.PaintObject.Base;

namespace XDPaint.Tools.Image.Base
{
    public abstract class BasePatternPaintToolSettings : BasePaintToolSettings
    {
        // ReSharper disable once InconsistentNaming
        internal bool usePattern;
        [PaintToolSettings] public bool UsePattern
        {
            get => usePattern;
            set
            {
                usePattern = value;
                if (usePattern)
                {
                    Data.PaintMaterial.EnableKeyword(Constants.PaintShader.TileKeyword);
                }
                else
                {
                    Data.PaintMaterial.DisableKeyword(Constants.PaintShader.TileKeyword);
                }
                
                OnPropertyChanged();
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once Unity.RedundantSerializeFieldAttribute
        // ReSharper disable once InconsistentNaming
        [PaintToolSettings(Group = 1), PaintToolConditional("UsePattern"), SerializeField] internal Texture patternTexture;
        [PaintToolSettings(Group = 1), PaintToolConditional("UsePattern")] public Texture PatternTexture
        {
            get => patternTexture;
            set
            {
                patternTexture = value;
                if (patternTexture != null && patternTexture.wrapMode != TextureWrapMode.Repeat)
                {
                    Debug.LogWarning("Pattern texture does not have Wrap Mode = Repeat");
                }
                PatternScale = patternScale;
                Data.PaintMaterial.SetTexture(Constants.PaintShader.PatternTexture, patternTexture);
            }
        }
        
        [SerializeField] private bool preserveAspect;
        [PaintToolSettings(Group = 1), PaintToolConditional("UsePattern")]
        public bool PreserveAspect
        {
            get => preserveAspect;
            set
            {
                if (SetField(ref preserveAspect, value))
                {
                    if (preserveAspect)
                    {
                        var maxScale = Mathf.Max(patternScale.x, patternScale.y);
                        PatternScale = new Vector2(maxScale, maxScale);
                    }
                    else
                    {
                        PatternScale = patternScale;
                    }
                }
            }
        }
        
        private Vector2 patternScale = Vector2.one;
        private Vector2 lastPatternScale = Vector2.one;
        // ReSharper disable once MemberCanBePrivate.Global
        [PaintToolSettings, PaintToolConditional("UsePattern")] public Vector2 PatternScale
        {
            get => patternScale;
            set
            {
                if (preserveAspect)
                {
                    var xChange = Mathf.Abs(value.x - lastPatternScale.x);
                    var yChange = Mathf.Abs(value.y - lastPatternScale.y);
                    if (xChange > yChange && lastPatternScale.x > float.Epsilon)
                    {
                        if (value.x < float.Epsilon)
                        {
                            value.y = value.x;
                        }
                        else
                        {
                            var ratio = value.x / lastPatternScale.x;
                            value.y = lastPatternScale.y * ratio;
                        }
                    }
                    else if (yChange > xChange && lastPatternScale.y > float.Epsilon)
                    {
                        if (value.y < float.Epsilon)
                        {
                            value.x = value.y;
                        }
                        else
                        {
                            var ratio = value.y / lastPatternScale.y;
                            value.x = lastPatternScale.x * ratio;
                        }
                    }
                    else if (lastPatternScale.x < float.Epsilon || lastPatternScale.y < float.Epsilon)
                    {
                        var maxValue = Mathf.Max(value.x, value.y);
                        value.x = maxValue;
                        value.y = maxValue;
                    }
                }
                
                patternScale = value;
                lastPatternScale = value;
                var layerTexture = Data.LayersController.ActiveLayer.RenderTexture;
                var scale = Vector2.one;
                if (patternTexture != null && layerTexture != null)
                {
                    scale = new Vector2(layerTexture.width / (float)patternTexture.width, layerTexture.height / (float)patternTexture.height);
                    var aspectFix = (float)layerTexture.height / layerTexture.width;
                    if (preserveAspect)
                    {
                        scale.x /= aspectFix;
                    }
                }
                
                var result = Vector2.one / (patternScale == Vector2.zero ? Vector2.one : patternScale) * scale;
                Data.PaintMaterial.SetVector(Constants.PaintShader.PatternScale, result);
            }
        }

        private float patternAngle;
        [PaintToolSettings, PaintToolConditional("UsePattern")] public float PatternAngle
        {
            get => patternAngle;
            set
            {
                patternAngle = value;
                Data.PaintMaterial.SetFloat(Constants.PaintShader.PatternAngle, patternAngle);
            }
        }
        
        private Vector2 patternOffset = Vector2.zero;
        [PaintToolSettings, PaintToolConditional("UsePattern")] public Vector2 PatternOffset
        {
            get => patternOffset;
            set
            {
                patternOffset = value;
                Data.PaintMaterial.SetVector(Constants.PaintShader.PatternOffset, patternOffset);
            }
        }
        
        protected BasePatternPaintToolSettings(IPaintData paintData) : base(paintData)
        {
        }
    }
}