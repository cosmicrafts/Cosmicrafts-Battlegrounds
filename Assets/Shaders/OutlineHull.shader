Shader "Custom/OutlineHull"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1,1,0,1)
        _Thickness("Outline Thickness", Range(0.0001, 0.1)) = 0.01
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" }
        
        // First pass - outline
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest Less
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _Thickness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Extrude the position along the normal direction
                float3 posOS = input.positionOS.xyz + input.normalOS * _Thickness;
                
                // Transform position to clip space
                output.positionCS = TransformObjectToHClip(posOS);
                
                // Calculate fog factor
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = _OutlineColor;
                
                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Lit"
} 