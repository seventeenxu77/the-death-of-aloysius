Shader "Aloysius/Level1/Prop Contact Outline"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _StyleTex("Optional painted surface", 2D) = "white" {}
        _StyleBlend("Painted surface blend (preserves original alpha)", Range(0,1)) = 0
        _Exposure("Scene brightness", Range(0,1)) = .16
        _Highlight("Contact highlight", Range(0,1)) = 0
        _OutlineColor("Outline color", Color) = (1,.8,.45,1)
        _OutlineWidth("Outline pixels", Range(1,5)) = 2.5
        _SceneVisibility("Scene visibility", Range(0,1)) = 1
        _PipeFocus("Pipe view blend", Range(0,1)) = 0
        _PipeInterior("Pipe interior layer", Float) = 0
        _PipeInteriorBrightness("Interior brightness", Float) = .32
        _PipeShellOpacity("Shell opacity", Float) = .035
        _PipeOutlineOpacity("Shell outline opacity", Float) = .45
        _PipeOutlineWidth("Shell outline pixels", Float) = 4
        _PipeTint("Interior tint", Color) = (.65,.48,.24,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_StyleTex); SAMPLER(sampler_StyleTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _OutlineColor;
            float _Exposure;
            float _StyleBlend;
            float _Highlight;
            float _OutlineWidth;
            float _SceneVisibility, _PipeFocus, _PipeInterior, _PipeInteriorBrightness;
            float _PipeShellOpacity, _PipeOutlineOpacity, _PipeOutlineWidth;
            float4 _PipeTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.color = v.color; return o;
            }
            half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }
            half4 frag(Varyings i) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                source.rgb=lerp(source.rgb,SAMPLE_TEXTURE2D(_StyleTex,sampler_StyleTex,i.uv).rgb*i.color.rgb,_StyleBlend);
                if (_PipeInterior > .5)
                {
                    // Light the hollow volume without retaining the opaque scale texture.
                    half shade = lerp(.72,1.0,saturate(1-abs(i.uv.y-.5)*1.5));
                    return half4(_PipeTint.rgb * _PipeInteriorBrightness * shade,source.a * _PipeFocus);
                }
                float2 d = _MainTex_TexelSize.xy * _OutlineWidth;
                half expanded = source.a;
                expanded = max(expanded, Alpha(i.uv + float2(d.x,0)));
                expanded = max(expanded, Alpha(i.uv - float2(d.x,0)));
                expanded = max(expanded, Alpha(i.uv + float2(0,d.y)));
                expanded = max(expanded, Alpha(i.uv - float2(0,d.y)));
                expanded = max(expanded, Alpha(i.uv + d * .7071));
                expanded = max(expanded, Alpha(i.uv - d * .7071));
                expanded = max(expanded, Alpha(i.uv + float2(d.x,-d.y) * .7071));
                expanded = max(expanded, Alpha(i.uv + float2(-d.x,d.y) * .7071));
                half edge = saturate(expanded - source.a) * _Highlight * _OutlineColor.a;
                half alpha = source.a + edge * (1 - source.a);
                // The outline is legible in the dim room, but also disappears at full blackout.
                half3 glow = _OutlineColor.rgb * saturate(_Exposure * 6);
                half3 rgb = (source.rgb * _Exposure * source.a + glow * edge * (1 - source.a)) / max(alpha, .00001);
                if (_PipeFocus > 0)
                {
                    float2 rimD = _MainTex_TexelSize.xy * _PipeOutlineWidth;
                    half eroded = min(Alpha(i.uv+float2(rimD.x,0)),Alpha(i.uv-float2(rimD.x,0)));
                    eroded = min(eroded,min(Alpha(i.uv+float2(0,rimD.y)),Alpha(i.uv-float2(0,rimD.y))));
                    eroded = min(eroded,min(Alpha(i.uv+rimD*.7071),Alpha(i.uv-rimD*.7071)));
                    eroded = min(eroded,min(Alpha(i.uv+float2(rimD.x,-rimD.y)*.7071),Alpha(i.uv+float2(-rimD.x,rimD.y)*.7071)));
                    half rim = saturate(source.a-eroded);
                    half shellAlpha = max(source.a*_PipeShellOpacity,rim*_PipeOutlineOpacity);
                    // Blend premultiplied colors before converting to straight alpha.
                    half finalAlpha = lerp(alpha,shellAlpha,_PipeFocus);
                    rgb = lerp(rgb*alpha,_PipeTint.rgb*shellAlpha,_PipeFocus)/max(finalAlpha,.00001);
                    alpha = finalAlpha;
                }
                return half4(rgb, alpha * _SceneVisibility);
            }
            ENDHLSL
        }
    }
}
