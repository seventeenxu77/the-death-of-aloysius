Shader "Aloysius/Level1/EntranceSilhouette"
{
    Properties {
        [PerRendererData] _MainTex("Alpha silhouette",2D)="white" {}
        _Tint("Frame colour and opacity",Color)=(.38,.31,.2,.22)
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            Tags {"LightMode"="SRPDefaultUnlit"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct A {float3 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR;};
            struct V {float4 position:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR;};
            V vert(A i){V o;o.position=TransformObjectToHClip(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(V i):SV_Target {return half4(_Tint.rgb*i.color.rgb,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*_Tint.a*i.color.a);}
            ENDHLSL
        }
    }
}
