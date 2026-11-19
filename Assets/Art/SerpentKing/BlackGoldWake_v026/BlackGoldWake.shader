Shader "Aloysius/Black Gold Wake"
{
 Properties { _MainTex("Open",2D)="white"{} _ClosedTex("Closed",2D)="white"{} _ClosedEye("Closed eye",Float)=0 _Roots("Root fade",Float)=0 _Front("Front coil slice",Float)=0 _Eye0("Eye 0",Vector)=(0,0,0,0) _Eye1("Eye 1",Vector)=(0,0,0,0) _Exposure("Exposure",Float)=1 _RoofClipEnabled("Roof clipping",Float)=0 _RoofY("Roof world Y",Float)=1000 }
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
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float worldY:TEXCOORD1;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_ClosedTex);SAMPLER(sampler_ClosedTex);
   CBUFFER_START(UnityPerMaterial)
   float _ClosedEye,_Roots,_Front,_Exposure,_RoofClipEnabled,_RoofY;float4 _Eye0,_Eye1;
   CBUFFER_END
   V vert(A a){V o;float3 w=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(w);o.worldY=w.y;o.uv=a.uv;return o;}
   float Eye(float4 e,float2 p){if(e.z<1)return 0;return 1-smoothstep(.80,1.,length((p-e.xy)/e.zw));}
   half4 frag(V i):SV_Target
   {
    if(_RoofClipEnabled>.5)clip(_RoofY-i.worldY);
    float2 p=float2(i.uv.x,1-i.uv.y)*float2(1536,1024);
    half4 a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv),b=SAMPLE_TEXTURE2D(_ClosedTex,sampler_ClosedTex,i.uv);
    float mask=max(Eye(_Eye0,p),Eye(_Eye1,p))*_ClosedEye;
    half4 c=half4(lerp(a.rgb,b.rgb,mask),a.a);c.a*=1-_Roots*smoothstep(845.,865.,p.y);
    float edge=785.-15.*smoothstep(850.,1150.,p.x)+65.*(1.-smoothstep(240.,400.,p.x));
    if(_Front>.5 && p.y<edge)discard;
    c.rgb*=_Exposure*c.a;return c;
   }
   ENDHLSL
  }
 }
}
