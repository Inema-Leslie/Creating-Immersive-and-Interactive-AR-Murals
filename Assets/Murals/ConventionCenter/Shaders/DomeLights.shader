// Dome bands, tower and cups of the Convention Center mural.
// _Lit blends from the plain day colour to the painted night texture.
// A ribbon of light can spiral around the dome (_RibbonPhase moves it), and _Glow brightens a band.
Shader "ConventionCenter/Dome Lights"
{
    Properties
    {
        _BaseMap ("Night Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _DayColor ("Day Color", Color) = (0.86, 0.88, 0.9, 1)
        _NightTint ("Night Tint", Color) = (1, 1, 1, 1)
        _Lit ("Lit", Range(0, 1)) = 0
        _Glow ("Glow", Float) = 0
        _RibbonColor ("Ribbon Color", Color) = (1, 1, 1, 1)
        _RibbonPhase ("Ribbon Phase", Float) = 0
        _RibbonStrength ("Ribbon Strength", Float) = 0
        _BandIndex ("Band Index", Float) = 0
        _BandCount ("Band Count", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DayColor;
                half4 _NightTint;
                half4 _RibbonColor;
                float _Lit;
                float _Glow;
                float _RibbonPhase;
                float _RibbonStrength;
                float _BandIndex;
                float _BandCount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Fixed soft light from the upper front, so the shape reads without scene lights.
                float3 normal = normalize(input.normalOS);
                half shade = 0.65 + 0.35 * saturate(dot(normal, normalize(float3(-0.3, 0.7, 0.6))));

                half3 day = _DayColor.rgb * shade;
                half3 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half3 night = painted * _NightTint.rgb * (0.85 + 0.15 * shade) * (1.0 + _Glow);
                half3 color = lerp(day, night, _Lit);

                // Height on the whole dome: 0 at the bottom band, 1 at the top. Band 0 is the top cap.
                float height = ((_BandCount - 1.0 - _BandIndex) + input.uv.y) / _BandCount;
                float distanceToRibbon = abs(frac(height + input.uv.x * 0.5 - _RibbonPhase) - 0.5);
                half ribbon = saturate(1.0 - distanceToRibbon / 0.04);
                color += _RibbonColor.rgb * ribbon * _RibbonStrength * _Lit;

                return half4(color * _BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
