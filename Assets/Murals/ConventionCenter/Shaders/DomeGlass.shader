// See-through glass shell over the dome bands, like the real convention centre dome.
// AR has no sky to reflect, so the glass look is drawn by hand: a bright rim where the surface
// turns away from the viewer, a soft highlight from above, and the painted lattice as faint lines.
// The bands underneath stay visible, so their night lights glow through.
Shader "ConventionCenter/Dome Glass"
{
    Properties
    {
        _BaseMap ("Lattice Texture", 2D) = "white" {}
        _GlassColor ("Glass Color", Color) = (0.86, 0.93, 1, 1)
        _BodyAlpha ("Body Alpha", Range(0, 1)) = 0.04
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.5
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _LatticeAlpha ("Lattice Alpha", Range(0, 1)) = 0.1
        _HighlightAlpha ("Highlight Alpha", Range(0, 1)) = 0.22
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _GlassColor;
                half _BodyAlpha;
                half _RimAlpha;
                half _RimPower;
                half _LatticeAlpha;
                half _HighlightAlpha;
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
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(input.viewWS);
                half facing = abs(dot(normal, view));

                // Rim: edges of the dome catch the light, like curved glass.
                half rim = pow(1.0 - facing, _RimPower);

                // Soft highlight where the surface faces up and toward the viewer.
                float3 reflected = reflect(-view, normal);
                half highlight = smoothstep(0.82, 0.99, reflected.y);

                // The painted chevron pattern, used only for its light and dark lines.
                half3 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half lattice = 1.0 - saturate(dot(painted, half3(0.3, 0.59, 0.11)) * 1.4);

                half alpha = _BodyAlpha + rim * _RimAlpha + highlight * _HighlightAlpha + lattice * _LatticeAlpha;
                half3 color = _GlassColor.rgb + highlight * 0.3;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
