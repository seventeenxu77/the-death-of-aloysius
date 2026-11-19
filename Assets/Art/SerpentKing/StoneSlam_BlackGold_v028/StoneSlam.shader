Shader "Aloysius/Stone Slam Black Gold"
{
 Properties { _MainTex("Open",2D)="white"{} _ClosedTex("Closed",2D)="white"{} _ClosedEye("Closed Eye",Range(0,1))=0 _Color("Color",Color)=(1,1,1,1) _CutMode("0 whole, 1 neck, 2 detached head",Float)=0 _CutSourceY("Cut line in original art",Float)=640 _CutColor("Cut edge",Color)=(.52,.035,.075,1) }
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
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_ClosedTex);SAMPLER(sampler_ClosedTex);
   CBUFFER_START(UnityPerMaterial)
   float _ClosedEye,_CutMode,_CutSourceY;float4 _Color,_CutColor;
   CBUFFER_END
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target
   {
    float2 p=float2(i.uv.x,1-i.uv.y)*float2(1024,1536);
    float mask=smoothstep(332,340,p.x)*(1-smoothstep(412,420,p.x))*smoothstep(1000,1008,p.y)*(1-smoothstep(1082,1090,p.y));
    half4 a=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv),b=SAMPLE_TEXTURE2D(_ClosedTex,sampler_ClosedTex,i.uv);
    half4 c=half4(lerp(a.rgb,b.rgb,mask*_ClosedEye),a.a)*_Color;
    if(_CutMode>.5)
    {
     float edge=_CutSourceY+sin(p.x*.11)*4+sin(p.x*.037)*3;
     if(_CutMode<1.5)clip(edge-p.y);else clip(p.y-edge);
     float wound=1-smoothstep(2,8,abs(p.y-edge));c.rgb=lerp(c.rgb,_CutColor.rgb,wound);
    }
    c.rgb*=c.a;return c;
   }
   ENDHLSL
  }
 }
}
