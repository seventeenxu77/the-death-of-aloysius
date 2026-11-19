Shader "Hidden/SunCourt/PaperFrescoPost"
{
    Properties
    {
        _EffectStrength ("Effect strength", Range(0,1)) = 1
        _InkStrength ("Contour strength", Range(0,1)) = .3
        _LineWidth ("Contour width in pixels", Range(.5,3)) = 1
        _DepthThreshold ("Depth threshold", Float) = .08
        _InkColor ("Sepia ink", Color) = (.33,.20,.14,1)
        _PaperStrength ("Paper grain", Range(0,.15)) = .035
        _PaperScale ("Paper grain scale", Float) = 1
        _PaletteCompression ("Printed value compression", Range(0,1)) = .18
        _Warmth ("Paper warmth", Range(0,1)) = .10
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PaperAndInk"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _EffectStrength;
                float _InkStrength;
                float _LineWidth;
                float _DepthThreshold;
                float4 _InkColor;
                float _PaperStrength;
                float _PaperScale;
                float _PaletteCompression;
                float _Warmth;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float PaperNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1,0));
                float c = Hash21(cell + float2(0,1));
                float d = Hash21(cell + 1);
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            float EyeDepthAt(float2 uv)
            {
                float rawDepth = SampleSceneDepth(saturate(uv));
                // The court uses an orthographic camera. Perspective-only depth
                // conversion would turn its contours into broad dark gradients.
                return unity_OrthoParams.w > .5
                    ? LinearDepthToEyeDepth(rawDepth)
                    : LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 pixelSize = rcp(_ScaledScreenParams.xy);
                float2 offset = pixelSize * _LineWidth;
                float center = EyeDepthAt(uv);
                float left = EyeDepthAt(uv - float2(offset.x,0));
                float right = EyeDepthAt(uv + float2(offset.x,0));
                float down = EyeDepthAt(uv - float2(0,offset.y));
                float up = EyeDepthAt(uv + float2(0,offset.y));

                // Second differences leave a flat or sloping plane clear while
                // finding silhouettes, arch edges, and discontinuities at steps.
                float discontinuity = max(abs(left + right - 2.0 * center),
                                          abs(down + up - 2.0 * center));
                float threshold = max(_DepthThreshold, center * .0003);
                float ink = smoothstep(threshold, threshold * 2.5, discontinuity);

                float3 printed = max(source.rgb, 0);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    printed = LinearToSRGB(printed);
                #endif

                // Compress luminance, retaining pigment hues and mural details.
                // Surface light bands are authored by the fresco material shader.
                float value = dot(printed, float3(.2126,.7152,.0722));
                float pressedValue = .04 + .92 * value;
                float valueDelta = (pressedValue - value) * _PaletteCompression;
                printed += valueDelta;
                printed *= lerp(float3(1,1,1), float3(1.025,1.0,.955), _Warmth);
                printed = lerp(printed, _InkColor.rgb, saturate(ink * _InkStrength));

                // No time input: the grain stays still like the paper beneath an
                // illustration, rather than flickering like video noise.
                float2 paperUV = (uv * _ScaledScreenParams.xy) / max(.25, _PaperScale);
                float fine = Hash21(floor(paperUV)) - .5;
                float wash = PaperNoise(paperUV / 62.0) - .5;
                float fibres = PaperNoise(paperUV / float2(13.0,1.7)) - .5;
                float grain = fine * .62 + wash * .55 + fibres * .28;
                printed *= 1.0 + grain * _PaperStrength * 2.0;

                #if !defined(UNITY_COLORSPACE_GAMMA)
                    printed = SRGBToLinear(max(printed, 0));
                #endif
                return half4(lerp(source.rgb, printed, _EffectStrength), source.a);
            }
            ENDHLSL
        }
    }
}
