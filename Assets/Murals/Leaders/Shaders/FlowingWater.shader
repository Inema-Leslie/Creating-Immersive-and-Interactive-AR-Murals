// Unlit see-through water for the Leaders river.
// The water texture scrolls along V (_FlowOffset, set by LeadersMural) inside a fixed shape mask.
// _Reveal (0 to 1) uncovers the water from the start of the shape (top of the mask) to the end.
Shader "Leaders/Flowing Water"
{
    Properties
    {
        _BaseMap ("Water Texture", 2D) = "white" {}
        _MaskMap ("Shape Mask", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 0.9)
        _FlowOffset ("Flow Offset", Float) = 0
        _Reveal ("Reveal", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MaskMap_ST;
                half4 _BaseColor;
                float _FlowOffset;
                float _Reveal;
            CBUFFER_END

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 flowUV = input.uv * _BaseMap_ST.xy + float2(0, _FlowOffset);
                half4 water = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowUV);
                half mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).r;

                // Distance along the shape: 0 at the top of the mask, 1 at the bottom.
                float along = 1.0 - input.uv.y;
                half reveal = saturate((_Reveal * 1.1 - along) / 0.1);

                return half4(water.rgb * _BaseColor.rgb, water.a * mask * reveal * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
