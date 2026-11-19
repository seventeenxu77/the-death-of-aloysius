Shader "Aloysius/Sun Court/Painted Fresco"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted pigment", 2D) = "white" {}
        [MainColor] _BaseColor("Pigment tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow wash", Color) = (0.90,0.82,0.85,1)
        _ShadowValue("Dark paint band", Range(0.4,1)) = 0.70
        _MiddleValue("Middle paint band", Range(0.5,1)) = 0.87
        _BandSoftness("Paint edge softness", Range(0.005,0.2)) = 0.045
        _CastShadowValue("Cast shadow paint value", Range(0.4,1)) = 0.73
        _PigmentBleed("Irregular pigment edge", Range(0,0.12)) = 0.025
        _PaperLift("Faded paper lift", Range(0,0.2)) = 0.025
        [Toggle] _FlatArtwork("Flat wall painting", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Faces", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowTint;
            half _ShadowValue;
            half _MiddleValue;
            half _BandSoftness;
            half _CastShadowValue;
            half _PigmentBleed;
            half _PaperLift;
            half _FlatArtwork;
            half _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "PaintedForward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PaintVertex
            #pragma fragment PaintFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct PaintAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct PaintVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            PaintVaryings PaintVertex(PaintAttributes input)
            {
                PaintVaryings output = (PaintVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 PaintFragment(PaintVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 pigment = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half3 n = normalize(input.normalWS);
                half incidence = saturate(dot(n, mainLight.direction));
                half pigmentVariation = dot(pigment, half3(.2126,.7152,.0722)) - .45;
                incidence += pigmentVariation * _PigmentBleed;

                // Three restrained pigment values replace continuous PBR shading.
                half mid = smoothstep(.22 - _BandSoftness, .22 + _BandSoftness, incidence);
                half bright = smoothstep(.63 - _BandSoftness, .63 + _BandSoftness, incidence);
                half value = lerp(_ShadowValue, _MiddleValue, mid);
                value = lerp(value, 1.0h, bright);
                half castValue = lerp(_CastShadowValue, 1.0h, smoothstep(.25h,.7h,mainLight.shadowAttenuation));
                value = min(value, castValue);
                value = lerp(value, 1.0h, saturate(_FlatArtwork));
                half washAmount = saturate((1.0h-value) / max(.05h,1.0h-_ShadowValue)) * .30h;
                half3 painted = pigment * value * lerp(half3(1,1,1),_ShadowTint.rgb,washAmount);
                // A small lifted black point resembles pigment printed on warm paper.
                painted = lerp(painted,half3(.78h,.66h,.48h),_PaperLift);
                return half4(painted,1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
