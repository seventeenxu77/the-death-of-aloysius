Shader "Aloysius/Complete Bedroom Floor"
{
 Properties { _MainTex("Entire intact floor",2D)="white"{} _AfterTex("Entire broken floor",2D)="white"{} _Reveal("Broken state",Float)=0 _Exposure("Exposure",Float)=.72 _Bounds("Room left, right, walking Y",Vector)=(0,30,0,1) _LayerMode("0 surface, 1 foreground slab",Float)=0 _NearLipDepth("Front lip below walk line",Float)=.12 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Cull Off ZWrite Off Blend One OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_AfterTex);SAMPLER(sampler_AfterTex);
   CBUFFER_START(UnityPerMaterial)
   float _Reveal,_Exposure,_LayerMode,_NearLipDepth;float4 _Bounds;
   CBUFFER_END
   V vert(A a){V o;float3 w=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(w);o.uv=a.uv;o.world=w.xy;return o;}
   half4 frag(V i):SV_Target
   {
    float sides=min(saturate((i.world.x-_Bounds.x)/6.85),saturate((_Bounds.y-i.world.x)/6.48));
    float back=_Bounds.z+1.25+1.25*sides;clip(back-i.world.y);
    if(_LayerMode>.5)clip(_Bounds.z-_NearLipDepth-i.world.y);
    float2 uv=float2(i.uv.x,1-(683+i.uv.y*341)/1024.);
    half4 a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv),b=SAMPLE_TEXTURE2D(_AfterTex,sampler_AfterTex,uv);
    // Two complete floor states: never place a local rectangular patch over the room.
    half4 c=lerp(a,b,saturate(_Reveal));c.a=1;c.rgb*=_Exposure;return c;
   }
   ENDHLSL
  }
 }
}
