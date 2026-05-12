Shader "XD Paint/Chroma Palette/Slider"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}   // required for Unity UI
        _Type ("Type", Float) = 0 // 0 = Hue, 1 = Value, 2 = Alpha
        _CurrentValue ("Current Value", Range(0, 1)) = 1
        _CheckerSize ("Checker Size", Float) = 8
        _CurrentColor ("Current Color", Color) = (1, 1, 1, 1)
        _SliderDimensions ("Slider Dimensions", Vector) = (1, 1, 0, 0)
        
        // UI Mask support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        LOD 100
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            #include "ChromaPalette.cginc"
            
            float _Type;
            float _CurrentValue;
            float _CheckerSize;
            fixed4 _CurrentColor;
            float4 _ClipRect;
            float4 _SliderDimensions;
            
            ColorV2f vert (ColorAppdata v)
            {
                return ColorVert(v);
            }
            
            fixed4 frag (ColorV2f i) : SV_Target
            {
                fixed4 col = fixed4(1, 1, 1, 1);
                
                if (_Type < 0.5) // Hue slider
                {
                    float h = 1.0f - i.uv.y;
                    float3 rgbGamma = HSVtoRGB_srgb(float3(h, 1.0, 1.0));
                    col.rgb = SRGB_to_Target(rgbGamma);
                }
                else if (_Type < 1.5) // Value slider
                {
                    float v = saturate(i.uv.y);
                    float3 grayGamma = v.xxx;
                    col.rgb = SRGB_to_Target(grayGamma);
                }
                else // Alpha slider
                {
                    float2 pixelPos = i.uv * _SliderDimensions.xy;
                    float2 checker = floor(pixelPos / _CheckerSize);
                    float checkerPattern = fmod(checker.x + checker.y, 2.0);
                    float3 checkerColor = lerp(float3(0.6, 0.6, 0.6), float3(0.8, 0.8, 0.8), checkerPattern);
                    col.rgb = lerp(checkerColor, _CurrentColor.rgb, _CurrentValue);
                    col.a = 1.0;
                }
                
                col *= i.color;
                col.a *= ColorGetAlphaClip(i, _ClipRect);
                
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
}