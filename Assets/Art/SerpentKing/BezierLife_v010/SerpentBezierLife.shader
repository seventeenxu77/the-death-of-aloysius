Shader "Aloysius/Serpent Bezier Life"
{
 Properties
 {
  _MainTex("Open",2D)="white"{} _ClosedTex("Closed",2D)="white"{}
  _ClosedEye("Lid amount",Float)=0 _Eye0("Eye 0",Vector)=(0,0,0,0) _Eye1("Eye 1",Vector)=(0,0,0,0)
  _IdleBlend("Wake handoff",Float)=1 _Fang("Fang",Float)=0 _Details("Details",Float)=1
  _Exposure("Exposure",Float)=1 _RoofClipEnabled("Roof clipping",Float)=0 _RoofY("Roof world Y",Float)=1000
  _Solid("Vertex colour tongue",Float)=0
 }
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
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float worldY:TEXCOORD1;float imageY:TEXCOORD2;half4 color:COLOR;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_ClosedTex);SAMPLER(sampler_ClosedTex);
   CBUFFER_START(UnityPerMaterial)
   float _ClosedEye,_IdleBlend,_Fang,_Details,_Exposure,_RoofClipEnabled,_RoofY,_Solid;float4 _Eye0,_Eye1;
   CBUFFER_END
   V vert(A a){V o;float3 w=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(w);o.worldY=w.y;o.imageY=-a.positionOS.y*100;o.uv=a.uv;o.color=a.color;return o;}
   float Lid(float4 e,float2 p)
   {
    if(e.z<1||_ClosedEye<.001)return 0;
    float2 d=(p-e.xy)/e.zw;
    float mask=1-smoothstep(.80,1.,length(d)),limit=(1-_ClosedEye)*1.15;
    return mask*smoothstep(limit-.075,limit+.075,abs(d.y)+.1*_ClosedEye);
   }
   half4 frag(V i):SV_Target
   {
    if(_RoofClipEnabled>.5)clip(_RoofY-i.worldY);
    if(_Solid>.5)return half4(i.color.rgb*_Exposure*i.color.a,i.color.a);
    float2 p=float2(i.uv.x,1-i.uv.y)*float2(1536,1024);
    half4 a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv),b=SAMPLE_TEXTURE2D(_ClosedTex,sampler_ClosedTex,i.uv);
    half4 c=half4(lerp(a.rgb,b.rgb,max(Lid(_Eye0,p),Lid(_Eye1,p))),a.a);
    c.a*=1-lerp(smoothstep(845.,865.,p.y),smoothstep(785.,805.,i.imageY),_IdleBlend);
    c.a*=1-_Fang*_Details*_IdleBlend*smoothstep(1138.,1146.,p.x)*smoothstep(442.,448.,p.y);
    c.rgb*=_Exposure*c.a;return c;
   }
   ENDHLSL
  }
 }
}
