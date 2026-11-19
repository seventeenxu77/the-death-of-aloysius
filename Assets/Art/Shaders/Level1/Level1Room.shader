Shader "Aloysius/Level1/Room Shadow"
{
    Properties
    {
        _MainTex("Original room image", 2D) = "white" {}
        _Exposure("Room brightness", Range(0,1)) = .16
        _EdgeShadow("Corner shadow", Range(0,1)) = .6
        _FloorUVRemap("Floor alignment: target UV, image UV, enabled", Vector) = (.1,.15,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Exposure;
            float _EdgeShadow;
            float4 _FloorUVRemap;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 distanceFromCenter = (i.uv - .5) * float2(1.6, 1.8);
                float corners = smoothstep(.25, 1.15, length(distanceFromCenter));
                float2 imageUV=i.uv;
                if(_FloorUVRemap.z>.5)
                {
                    float target=clamp(_FloorUVRemap.x,.001,.999),source=clamp(_FloorUVRemap.y,.001,.999);
                    imageUV.y=i.uv.y<target ? i.uv.y*source/target : source+(i.uv.y-target)*(1-source)/(1-target);
                }
                half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, imageUV).rgb;
                return half4(color * _Exposure * (1 - _EdgeShadow * corners), 1);
            }
            ENDHLSL
        }
    }
}
