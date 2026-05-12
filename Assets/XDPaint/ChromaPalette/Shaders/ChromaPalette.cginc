#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct ColorAppdata
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

struct ColorV2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
    float4 color : COLOR;
    float4 worldPosition : TEXCOORD1;
};

float3 HueToRGB_srgb(float h)
{
    float3 t = abs(frac(h + float3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0;
    float3 rgb = saturate(t);
    rgb = rgb * rgb * (3.0 - 2.0 * rgb);
    return rgb;
}

float3 HSVtoRGB_srgb(float3 hsv)
{
    float3 hue = HueToRGB_srgb(hsv.x);
    float3 rgb = lerp(1.0.xxx, hue, hsv.y) * hsv.z;
    return rgb;
}

float3 SRGB_to_Target(float3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return GammaToLinearSpace(c);
#endif
}

float4 BicubicSample(sampler2D tex, float2 uv, float4 texelSize)
{
    float2 texelSizeXY = texelSize.xy;
    float2 samplePos = uv / texelSizeXY - 0.5;
    float2 f = frac(samplePos);
    samplePos = floor(samplePos) + 0.5;

    float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    float2 w3 = f * f * (-0.5 + 0.5 * f);

    float2 g0 = w0 + w1;
    float2 g1 = w2 + w3;

    float2 h0 = w1 / max(g0, 1e-6) - 1.0;
    float2 h1 = w3 / max(g1, 1e-6) + 1.0;

    float4 p00 = tex2D(tex, (samplePos + float2(h0.x, h0.y)) * texelSizeXY);
    float4 p10 = tex2D(tex, (samplePos + float2(h1.x, h0.y)) * texelSizeXY);
    float4 p01 = tex2D(tex, (samplePos + float2(h0.x, h1.y)) * texelSizeXY);
    float4 p11 = tex2D(tex, (samplePos + float2(h1.x, h1.y)) * texelSizeXY);

    p00 = lerp(p01, p00, g0.y);
    p10 = lerp(p11, p10, g0.y);

    return lerp(p10, p00, g0.x);
}

ColorV2f ColorVert(ColorAppdata v)
{
    ColorV2f o;
    o.worldPosition = v.vertex;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    o.color = v.color;
    return o;
}

float ColorGetAlphaClip(ColorV2f i, float4 clipRect)
{
    return UnityGet2DClipping(i.worldPosition.xy, clipRect);
}