Shader "Aloysius/Level1/Silhouette Shadow"
{
    Properties { _Exposure("Scene brightness", Range(0,1)) = 0.128  _MainTex("Texture", 2D) = "white" {} _Tint("Tint", Color) = (1,1,1,1) _ClothFloor("Cloth floor contact", Vector) = (0,0,0,0) _Turn("Turn presentation", Vector) = (1,1,1,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_PipeMask); SAMPLER(sampler_PipeMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Tint;
            float4 _Turn;
            float4 _ClothFloor;
            float _Exposure;
            float _PipeFocus;
            float4x4 _PipeWorldToLocal;
            float4 _PipeLocalToUV, _PipeUVRect;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                p.x = _Turn.w * _Turn.x + (p.x - _Turn.w) * _Turn.z;
                p.y *= _Turn.y;
                float3 world = TransformObjectToWorld(p);
                // Fold the soft lower edge onto its support plane after skinning.
                // The exponential joins with slope one and keeps lace above ground.
                if (_ClothFloor.y > 0.5 && world.y < _ClothFloor.x + 0.06)
                    world.y = _ClothFloor.x + 0.06 * exp((world.y - _ClothFloor.x - 0.06) / 0.06);
                o.positionCS = TransformWorldToHClip(world); o.world=world; o.uv = v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Tint;
                color.rgb *= _Exposure;
                if (_PipeFocus > 0)
                {
                    float2 local = mul(_PipeWorldToLocal,float4(i.world,1)).xy;
                    float2 uv = local*_PipeLocalToUV.xy+_PipeLocalToUV.zw;
                    float inRect = step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                    half mask = SAMPLE_TEXTURE2D(_PipeMask,sampler_PipeMask,uv*_PipeUVRect.zw+_PipeUVRect.xy).a * inRect;
                    color.a *= lerp(1,mask,_PipeFocus);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
