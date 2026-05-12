Shader "XD Paint/Chroma Palette/Palette"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}   // required for Unity UI
        _Mode ("Mode", Float) = 0 // 0 = Rectangle, 1 = Circle(HS + Value slider), 2 = CircleFull (HSV double-cone), 3 = Circle+Circle (Hue ring + S/V disk), 4 = Circle+Triangle (Hue ring + S/V triangle), 5 = Texture
        _Hue ("Hue", Range(0, 1)) = 0
        _Value ("Value", Range(0, 1)) = 1
        _PaletteTexture ("Palette Texture", 2D) = "white" {}
        _UseBicubicSampling ("Use Bicubic Sampling", Float) = 1
        
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
            
            float _Mode;
            float _Hue;
            float _Value;
            sampler2D _PaletteTexture;
            float4 _PaletteTexture_TexelSize;
            float _UseBicubicSampling;
            float4 _ClipRect;
            
            ColorV2f vert (ColorAppdata v)
            {
                return ColorVert(v);
            }

            float2 rotateCCW(float2 v, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(c*v.x - s*v.y, s*v.x + c*v.y);
            }

            void barycentric(float2 p, float2 a, float2 b, float2 c, out float w0, out float w1, out float w2)
            {
                float2 v0 = b - a;
                float2 v1 = c - a;
                float2 v2 = p - a;
                float d00 = dot(v0, v0);
                float d01 = dot(v0, v1);
                float d11 = dot(v1, v1);
                float d20 = dot(v2, v0);
                float d21 = dot(v2, v1);
                float denom = d00 * d11 - d01 * d01;
                float inv = denom != 0 ? 1.0/denom : 0.0;
                float v = (d11 * d20 - d01 * d21) * inv;
                float w = (d00 * d21 - d01 * d20) * inv;
                float u = 1.0 - v - w;
                w0 = u; w1 = v; w2 = w;
            }

            float2 diskToSquare(float2 p)
            {
                const float EPS = 1e-6;
                float x = clamp(p.x, -1.0, 1.0);
                float y = clamp(p.y, -1.0, 1.0);
                float ax = abs(x);
                float ay = abs(y);
                if (ax <= EPS && ay <= EPS)
                {
                    return float2(0.5, 0.5);
                }

                float r = sqrt(x * x + y * y);
                float aVal;
                float bVal;

                if (ax > ay)
                {
                    float signX = x >= 0.0 ? 1.0 : -1.0;
                    float theta = atan(y / x);
                    float signedRadius = signX * r;
                    aVal = signedRadius;
                    bVal = signedRadius * (4.0 * theta / 3.14159265359);
                }
                else
                {
                    float signY = y >= 0.0 ? 1.0 : -1.0;
                    float signedRadius = signY * r;
                    float divisor = (abs(signedRadius) > EPS) ? signedRadius : signY;
                    float cosArg = clamp(x / divisor, -1.0, 1.0);
                    float theta = acos(cosArg);
                    aVal = signedRadius * (2.0 - (4.0 * theta / 3.14159265359));
                    bVal = signedRadius;
                }

                float u = saturate(0.5 * (aVal + 1.0));
                float v = saturate(0.5 * (bVal + 1.0));
                return float2(u, v);
            }

            fixed4 frag (ColorV2f i) : SV_Target
            {
                fixed4 col = fixed4(1,1,1,1);

                if (_Mode < 0.5) // 0 = Rectangle: x = S, y = V
                {
                    float s = saturate(i.uv.x);
                    float v = saturate(i.uv.y);

                    float3 rgbGamma = HSVtoRGB_srgb(float3(_Hue, s, v));
                    col.rgb = SRGB_to_Target(rgbGamma);
                }
                else if (_Mode < 1.5) // 1 = Circle (HS), Value is uniform
                {
                    float2 center = float2(0.5, 0.5);
                    float2 d = i.uv - center;
                    float r = length(d);

                    if (r > 0.5)
                    {
                        col.a = 0.0;
                    }
                    else
                    {
                        const float TWO_PI = 6.28318530718;
                        float ang = atan2(d.y, d.x);
                        float h = frac(ang / TWO_PI + 0.5);
                        float s = saturate(r * 2.0);
                        float3 rgbGamma = HSVtoRGB_srgb(float3(h, s, _Value));
                        col.rgb = SRGB_to_Target(rgbGamma);
                    }
                }
                else if (_Mode < 2.5f) // 2 = CircleFull: angle=hue, radius mixes S and V (double-cone)
                {
                    float2 center = float2(0.5, 0.5);
                    float2 d = i.uv - center;
                    float r = length(d);

                    if (r > 0.5)
                    {
                        col.a = 0.0;
                    }
                    else
                    {
                        const float TWO_PI = 6.28318530718;
                        float ang = atan2(d.y, d.x);
                        float h = frac(ang / TWO_PI + 0.5);
                        float rn = saturate(r * 2.0);
                        float s = saturate(rn <= 0.5 ? rn * 2.0 : 2.0 * (1.0 - rn));
                        float v = saturate(rn <= 0.5 ? 1.0 : 2.0 * (1.0 - rn));
                        float3 rgbGamma = HSVtoRGB_srgb(float3(h, s, v));
                        col.rgb = SRGB_to_Target(rgbGamma);
                    }
                }
                else if (_Mode < 3.5) // 3 = CircleCircle
                {
                    float2 center = float2(0.5, 0.5);
                    float2 d = i.uv - center;
                    float r = length(d);
                    const float TWO_PI = 6.28318530718;
                    float ang = atan2(d.y, d.x);
                    float hRing = frac(ang / TWO_PI + 0.5);

                    float ro = 0.5;
                    float ringThickness = 0.085;
                    float ri = ro - ringThickness;
                    float circleRadius = ri * 0.92;

                    if (r > ro)
                    {
                        col.a = 0.0;
                    }
                    else if (r >= ri)
                    {
                        float3 rgbGamma = HSVtoRGB_srgb(float3(hRing, 1.0, 1.0));
                        col.rgb = SRGB_to_Target(rgbGamma);
                        col.a = 1.0;
                    }
                    else if (r > circleRadius)
                    {
                        col.a = 0.0;
                    }
                    else
                    {
                        float2 disk = d / circleRadius;
                        float2 square = diskToSquare(disk);
                        float S = saturate(square.x);
                        float V = saturate(square.y);
                        float3 rgbGamma = HSVtoRGB_srgb(float3(_Hue, S, V));
                        col.rgb = SRGB_to_Target(rgbGamma);
                        col.a = 1.0;
                    }
                }
                else if (_Mode < 4.5) // 4 = CircleTriangle (Hue ring + S/V triangle)
                {
                    float2 center = float2(0.5, 0.5);
                    float2 d = i.uv - center;
                    float r = length(d);
                    const float TWO_PI = 6.28318530718;
                    float ang = atan2(d.y, d.x);
                    float hRing = frac(ang / TWO_PI + 0.5);

                    // Geometry
                    float ro = 0.5;                // outer radius
                    float ringThickness = 0.085;   // in UV units
                    float ri = ro - ringThickness; // inner radius of ring
                    float triR = ri * 0.92;        // triangle radius inside

                    if (r > ro)
                    {
                        col.a = 0.0;
                    }
                    else if (r >= ri)
                    {
                        float3 rgbGamma = HSVtoRGB_srgb(float3(hRing, 1.0, 1.0));
                        col.rgb = SRGB_to_Target(rgbGamma);
                    }
                    else
                    {
                        float phi = _Hue * TWO_PI - 3.14159265359;
                        float2 dir = float2(cos(phi), sin(phi));
                        float2 pH = center + dir * triR;
                        float2 pW = center + rotateCCW(dir * triR, 2.09439510239);
                        float2 pB = center + rotateCCW(dir * triR, -2.09439510239);

                        float wW, wB, wH;
                        barycentric(i.uv, pW, pB, pH, wW, wB, wH);
                        if (wW >= 0.0 && wB >= 0.0 && wH >= 0.0)
                        {
                            float V = saturate(wW + wH);
                            float S = (V > 1e-5) ? saturate(wH / V) : 0.0;
                            float3 rgbGamma = HSVtoRGB_srgb(float3(_Hue, S, V));
                            col.rgb = SRGB_to_Target(rgbGamma);
                            col.a = 1.0;
                        }
                        else
                        {
                            col.a = 0.0;
                        }
                    }
                }
                else if (_Mode < 5.5) // 5 = Texture mode (Unity handles sRGB->Linear based on import settings)
                {
                    float4 t = (_UseBicubicSampling > 0.5) ? BicubicSample(_PaletteTexture, i.uv, _PaletteTexture_TexelSize)
                                                           : tex2D(_PaletteTexture, i.uv);
                    col = t;
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