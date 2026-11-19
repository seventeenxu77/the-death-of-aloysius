Shader "Aloysius/Palace Left Spike"
{
 Properties { _ClipEnabled("Clip behind outlet",Float)=0 _ClipRight("Outlet world X",Float)=10000 }
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
   struct A{float4 positionOS:POSITION;half4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;half4 color:COLOR;float worldX:TEXCOORD0;};
   CBUFFER_START(UnityPerMaterial)
   float _ClipEnabled,_ClipRight;
   CBUFFER_END
   V vert(A a){V o;float3 w=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(w);o.worldX=w.x;o.color=a.color;return o;}
   half4 frag(V i):SV_Target{if(_ClipEnabled>.5)clip(_ClipRight-i.worldX);return half4(i.color.rgb*i.color.a,i.color.a);}
   ENDHLSL
  }
 }
}
