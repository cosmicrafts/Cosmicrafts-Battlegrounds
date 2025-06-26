Shader "Custom/LowHealthWarning"
{
    Properties
    {
        _Color ("Color", Color) = (1,0,0,1)
        _VignettePower ("Vignette Power", Range(0.1, 10)) = 2
        _VignetteSoftness ("Vignette Softness", Range(0, 1)) = 0.5
        _FlashSpeed ("Flash Speed", Range(0.1, 5)) = 1
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 screenPos : TEXCOORD1;
            };

            float4 _Color;
            float _VignettePower;
            float _VignetteSoftness;
            float _FlashSpeed;
            float _FlashIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex).xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate vignette
                float2 center = i.screenPos * 2.0 - 1.0;
                float vignette = dot(center, center) * _VignettePower;
                vignette = smoothstep(0.0, _VignetteSoftness, vignette);
                
                // Calculate flash effect using sine wave
                float flash = (sin(_Time.y * _FlashSpeed) + 1.0) * 0.5;
                float flashEffect = lerp(1.0 - _FlashIntensity, 1.0, flash);
                
                // Combine effects
                float finalAlpha = vignette * flashEffect;
                
                // Apply color
                fixed4 col = _Color;
                col.a *= finalAlpha;
                
                return col;
            }
            ENDCG
        }
    }
} 