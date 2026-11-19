Shader "Aloysius/Level1/PalaceSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Exposure("Exposure", Range(0, 1)) = .68
        _SceneVisibility("Visibility", Range(0, 1)) = 1
        _Cutoff("Alpha cutoff", Range(0, 1)) = .01
        _Highlight("Contact outline", Range(0, 1)) = 0
        _WorldClip("World clipping bounds", Vector) = (-1000,1000,-1000,1000)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_TexelSize, _WorldClip;
            float _Exposure, _SceneVisibility, _Cutoff, _Highlight;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; half4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o; float3 w=TransformObjectToWorld(v.positionOS);
                o.positionCS=TransformWorldToHClip(w); o.world=w.xy; o.uv=v.uv; o.color=v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                clip(i.world.x-_WorldClip.x); clip(_WorldClip.y-i.world.x);
                clip(i.world.y-_WorldClip.z); clip(_WorldClip.w-i.world.y);
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                float a=smoothstep(_Cutoff,min(1,_Cutoff+.06),c.a);
                float2 d=_MainTex_TexelSize.xy*2;
                float neighbor=min(min(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(d.x,0)).a,
                    SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(d.x,0)).a),
                    min(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(0,d.y)).a,
                    SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv-float2(0,d.y)).a));
                float edge=a*(1-smoothstep(_Cutoff,min(1,_Cutoff+.06),neighbor))*_Highlight;
                c.rgb=lerp(c.rgb*i.color.rgb*_Exposure,float3(.9,.76,.48),edge);
                c.a=a*i.color.a*_SceneVisibility; return c;
            }
            ENDHLSL
        }
    }
}
