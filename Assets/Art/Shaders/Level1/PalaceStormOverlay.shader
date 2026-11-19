Shader "Aloysius/Level1/Palace Storm Overlay"
{
    Properties
    {
        _LightTint("Cold light",Color)=(.6,.74,1,1)
        _Strength("Strength",Float)=0
        _Mode("0 window light, 1 rain, 2 lightning",Float)=0
        _WorldClip("World clip",Vector)=(-1000,1000,-1000,1000)
        _Rain("Speed, slant, clock",Vector)=(3.2,.16,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _LightTint,_WorldClip,_Rain;
            float _Strength,_Mode;
            CBUFFER_END
            struct A { float3 p:POSITION;float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1; };
            V vert(A v){V o;float3 w=TransformObjectToWorld(v.p);o.p=TransformWorldToHClip(w);o.world=w.xy;o.uv=v.uv;return o;}
            float hash(float v){return frac(sin(v*127.1)*43758.5453);}
            half4 frag(V i):SV_Target
            {
                clip(i.world.x-_WorldClip.x);clip(_WorldClip.y-i.world.x);
                float alpha=1;
                if(_Mode<.5)
                {
                    float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
                    float vertical=smoothstep(.018,.032,abs(i.uv.x-.5));
                    float horizontal=smoothstep(.022,.04,abs(i.uv.y-.48));
                    alpha=smoothstep(0,.055,edge)*vertical*horizontal*lerp(.35,1,i.uv.y);
                }
                else if(_Mode<1.5)
                {
                    float lane=(i.world.x-_Rain.y*i.world.y)*27;
                    float seed=hash(floor(lane));
                    float stripe=1-smoothstep(.025,.075,abs(frac(lane)-.5));
                    float fall=frac(i.world.y*1.7+_Rain.z*_Rain.x+seed*7);
                    float streak=smoothstep(.70,.78,fall)*(1-smoothstep(.93,1,fall));
                    alpha=stripe*streak*lerp(.25,1,seed);
                }
                else alpha=1-smoothstep(.25,.5,abs(i.uv.y-.5));
                return half4(_LightTint.rgb,saturate(alpha*_Strength));
            }
            ENDHLSL
        }
    }
}
