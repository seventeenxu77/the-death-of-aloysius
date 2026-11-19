Shader "Aloysius/Level1/OutdoorSilhouette"
{
    Properties
    {
        _BaseMap ("Paper or sky", 2D) = "white" {}
        _BaseColor ("Ink colour", Color) = (0.1,0.11,0.12,1)
        _Grain ("Paper grain", Range(0,0.25)) = 0.04
        _FaceShade ("Flat face separation", Range(0,1)) = 0.25
        _SceneVisibility ("Pipe cutaway visibility", Range(0,1)) = 1
        _StartX ("World left boundary", Float) = -100000
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Grain, _FaceShade, _SceneVisibility, _StartX;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 local : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float worldX : TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.local = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.worldX = TransformObjectToWorld(input.positionOS.xyz).x;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.worldX - _StartX);
                float noise = frac(sin(dot(floor(input.local * 460), float3(12.9898,78.233,37.719))) * 43758.5453);
                float face = lerp(1, .65 + .35 * saturate(dot(normalize(input.normalWS), normalize(float3(-.3,.7,-.8)))), _FaceShade);
                half3 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                colour *= face * (1 + (noise - .5) * _Grain);
                return half4(colour * _SceneVisibility, 1);
            }
            ENDHLSL
        }
    }
}
