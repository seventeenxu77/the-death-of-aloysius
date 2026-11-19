Shader "Aloysius/Serpent Skin"
{
 Properties { _MainTex("Awake",2D)="white"{} _SleepTex("Sleeping",2D)="white"{} _Phase("Head Turn",Range(0,1))=1 _Color("Tint",Color)=(1,1,1,1) }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_SleepTex);SAMPLER(sampler_SleepTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _Color;float _Phase;float4 _MainTex_ST;
   CBUFFER_END
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv*_MainTex_ST.xy+_MainTex_ST.zw;return o;}
   half4 frag(V i):SV_Target{half4 a=SAMPLE_TEXTURE2D(_SleepTex,sampler_SleepTex,i.uv);half4 b=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);a.rgb*=a.a;b.rgb*=b.a;half4 c=lerp(a,b,_Phase);c.rgb/=max(c.a,.00001);return c*_Color;}
   ENDHLSL
  }
 }
}
