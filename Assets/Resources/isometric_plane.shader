Shader "Custom/IsometricPlane"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _IsometricSkew ("Isometric Skew", Range(0, 1)) = 0.5
        _VerticalScale ("Vertical Scale", Range(1, 4)) = 2.0
        _HorizontalScale ("Horizontal Scale", Range(-24, 24)) = 1.0
        [Toggle] _PreserveAspect("Preserve Aspect Ratio", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _IsometricSkew;
            float _VerticalScale;
            float _HorizontalScale;
            float _PreserveAspect;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // Calculate dynamic scaling based on skew
                float skewScale = 1.0 + (_IsometricSkew * _VerticalScale);
                
                // Scale vertices to compensate for UV skew
                float3 scaledPos = IN.positionOS.xyz;
                scaledPos.y *= skewScale;
                
                // If preserving aspect ratio, scale horizontally too
                if (_PreserveAspect > 0.5)
                {
                    scaledPos.x *= (1.0 + (_IsometricSkew * _HorizontalScale * 0.5));
                }
                
                OUT.positionCS = TransformObjectToHClip(scaledPos);
                
                // Apply UV transformation
                OUT.uv = IN.uv;
                float skewAmount = _IsometricSkew;
                OUT.uv.y = lerp(IN.uv.y, 1.0 - IN.uv.y, skewAmount);
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return tex2D(_MainTex, IN.uv);
            }
            ENDHLSL
        }
    }
}