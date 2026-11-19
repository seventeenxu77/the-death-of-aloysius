Shader "Aloysius/Severed Serpent"
{
 Properties { _MainTex("Approved neck",2D)="white"{} _Exposure("Exposure",Float)=1 _Opacity("Opacity",Range(0,1))=1 _Solid("Blood vertex colour",Float)=0 }
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
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float _Exposure,_Opacity,_Solid;
   CBUFFER_END
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(V i):SV_Target
   {
    half4 c=_Solid>.5?i.color:SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
    if(_Solid<.5)c.a=smoothstep(.86,.965,c.a);
    c.a*=_Opacity;c.rgb*=_Exposure*c.a;return c;
   }
   ENDHLSL
  }
 }
}
