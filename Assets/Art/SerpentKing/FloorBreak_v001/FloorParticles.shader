Shader "Aloysius/Floor Debris And Dust"
{
 Properties {_Soft("Soft dust",Float)=0}
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
   CBUFFER_START(UnityPerMaterial)
   float _Soft;
   CBUFFER_END
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(V i):SV_Target{half4 c=i.color;if(_Soft>.5)c.a*=1-smoothstep(.3,1,length(i.uv*2-1));c.rgb*=c.a;return c;}
   ENDHLSL
  }
 }
}
