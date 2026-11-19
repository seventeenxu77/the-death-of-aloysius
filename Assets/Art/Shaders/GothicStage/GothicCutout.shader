Shader "Aloysius/Gothic Cutout Unlit"
{
    Properties { _MainTex("Texture", 2D) = "white" {} _Tint("Tint", Color) = (1,1,1,1) _ClothFloor("Cloth floor contact", Vector) = (0,0,0,0) _Turn("Turn presentation", Vector) = (1,1,1,0) }
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
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Tint;
            float4 _Turn;
            float4 _ClothFloor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
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
                o.positionCS = TransformWorldToHClip(world); o.uv = v.uv; return o;
            }
            half4 frag(Varyings i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Tint; }
            ENDHLSL
        }
    }
}
