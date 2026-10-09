// The painted plant, cut out of the mural photo.
// _GrowthMap stores how far each pixel is from the base of the stem (0 near, 1 far),
// so raising _Grow from 0 to 1 makes the plant grow along its own branches.
Shader "ConventionCenter/Growing Plant"
{
    Properties
    {
        _BaseMap ("Plant", 2D) = "white" {}
        _GrowthMap ("Growth Map", 2D) = "black" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Grow ("Grow", Range(0, 1)) = 1
        _Glow ("Glow", Float) = 0
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
            TEXTURE2D(_GrowthMap);
            SAMPLER(sampler_GrowthMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _GrowthMap_ST;
                half4 _BaseColor;
                float _Grow;
                float _Glow;
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
                half4 plant = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half distanceFromStem = SAMPLE_TEXTURE2D(_GrowthMap, sampler_GrowthMap, input.uv).r;
                half grown = saturate((_Grow * 1.05 - distanceFromStem) / 0.05);
                return half4(plant.rgb * (1.0 + _Glow) * _BaseColor.rgb, plant.a * grown * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
