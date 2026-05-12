Shader "XD Paint/Chroma Palette/Color Wheel"
{
    Properties
    {
        _MaxValue ("Max Value", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float _MaxValue;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Convert HSV to RGB
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Center the UV coordinates
                float2 center = float2(0.5, 0.5);
                float2 pos = i.uv - center;

                // Calculate polar coordinates
                float distance = length(pos) * 2.0; // Scale to 0-1 for full circle

                // Check if pixel is within the circle
                if (distance > 1.0)
                {
                    return fixed4(0, 0, 0, 0); // Transparent outside the circle
                }

                // Flip horizontally by inverting X
                pos.x = -pos.x;
                float angle = atan2(pos.y, pos.x);

                // Normalize angle to 0-1 range for hue
                float hue = (angle / (2.0 * 3.14159265359) + 0.5);
                hue = frac(hue); // Ensure it's in [0,1]

                // Saturation increases from center to edge
                float saturation = distance;

                // Value interpolates from 1 to _MaxValue based on saturation
                float value = lerp(1.0, _MaxValue, saturation);

                // Create HSV color
                float3 hsv = float3(hue, saturation, value);

                // Convert to RGB
                float3 rgb = hsv2rgb(hsv);

                // Add antialiasing at edge
                float alpha = 1.0 - smoothstep(0.98, 1.0, distance);

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}