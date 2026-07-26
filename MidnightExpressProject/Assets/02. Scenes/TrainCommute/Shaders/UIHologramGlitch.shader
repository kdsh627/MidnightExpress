Shader "MidnightExpress/UI/Hologram Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Hologram Color)]
        _HologramColor ("Hologram Color", Color) = (0.15,0.85,1,1)
        _TintStrength ("Tint Strength", Range(0,1)) = 0.28
        _Opacity ("Opacity", Range(0,1)) = 0.84

        [Header(Scanlines)]
        _ScanlineDensity ("Scanline Density", Range(10,500)) = 180
        _ScanlineSpeed ("Scanline Speed", Range(-10,10)) = 1.7
        _ScanlineStrength ("Scanline Strength", Range(0,0.8)) = 0.12
        _ScanBarSpeed ("Scan Bar Speed", Range(-3,3)) = 0.22
        _ScanBarWidth ("Scan Bar Width", Range(0.005,0.3)) = 0.07
        _ScanBarStrength ("Scan Bar Strength", Range(0,1)) = 0.18

        [Header(Static Noise)]
        _NoiseScale ("Noise Scale", Range(10,500)) = 180
        _NoiseSpeed ("Noise Speed", Range(0,60)) = 24
        _NoiseStrength ("Noise Strength", Range(0,0.5)) = 0.025

        [Header(Horizontal Glitch)]
        _GlitchRate ("Glitch Rate", Range(0.1,30)) = 9
        _GlitchBands ("Glitch Bands", Range(2,80)) = 28
        _GlitchFrequency ("Glitch Frequency", Range(0,1)) = 0.11
        _GlitchStrength ("Glitch Offset", Range(0,0.08)) = 0.009
        _GlitchDropout ("Glitch Dropout", Range(0,1)) = 0.24
        _ChromaticSplit ("Chromatic Split", Range(0,0.03)) = 0.0018
        _ChromaticStrength ("Chromatic Strength", Range(0,1)) = 0.3

        [Header(Flicker)]
        _FlickerRate ("Flicker Rate", Range(1,60)) = 24
        _FlickerStrength ("Flicker Strength", Range(0,0.6)) = 0.05

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "HologramUI"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _MainTex_ST;
            float4 _ClipRect;
            fixed4 _Color;
            fixed4 _HologramColor;
            float _TintStrength;
            float _Opacity;
            float _ScanlineDensity;
            float _ScanlineSpeed;
            float _ScanlineStrength;
            float _ScanBarSpeed;
            float _ScanBarWidth;
            float _ScanBarStrength;
            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseStrength;
            float _GlitchRate;
            float _GlitchBands;
            float _GlitchFrequency;
            float _GlitchStrength;
            float _GlitchDropout;
            float _ChromaticSplit;
            float _ChromaticStrength;
            float _FlickerRate;
            float _FlickerStrength;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(output.worldPosition);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float time = _Time.y;
                float2 uv = input.texcoord;

                float glitchFrame = floor(time * _GlitchRate);
                float glitchBand = floor(uv.y * _GlitchBands);
                float bandRandom = Hash21(float2(glitchBand, glitchFrame));
                float glitchGate = step(1.0 - _GlitchFrequency, bandRandom);
                float glitchDirection =
                    Hash21(float2(glitchFrame + 17.17, glitchBand + 31.73)) * 2.0 - 1.0;
                float glitchPulse =
                    glitchGate *
                    lerp(0.35, 1.0, Hash21(float2(glitchBand + 8.41, glitchFrame + 4.23)));

                uv.x += glitchDirection * _GlitchStrength * glitchPulse;

                fixed4 center = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float chromaOffset = _ChromaticSplit * (0.35 + glitchPulse);
                fixed4 redSample =
                    tex2D(_MainTex, uv + float2(chromaOffset, 0.0)) + _TextureSampleAdd;
                fixed4 blueSample =
                    tex2D(_MainTex, uv - float2(chromaOffset, 0.0)) + _TextureSampleAdd;

                fixed4 hologram = center;
                hologram.r = lerp(center.r, redSample.r, _ChromaticStrength);
                hologram.b = lerp(center.b, blueSample.b, _ChromaticStrength);

                float luminance = dot(hologram.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 tinted =
                    hologram.rgb * _HologramColor.rgb +
                    _HologramColor.rgb * luminance * 0.16;
                hologram.rgb = lerp(hologram.rgb, tinted, _TintStrength);

                float scanWave =
                    sin((uv.y * _ScanlineDensity - time * _ScanlineSpeed) * UNITY_TWO_PI);
                float scanDarken = (scanWave * 0.5 + 0.5) * _ScanlineStrength;
                hologram.rgb *= 1.0 - scanDarken;
                hologram.rgb +=
                    _HologramColor.rgb *
                    saturate(-scanWave) *
                    (_ScanlineStrength * 0.08);
                hologram.a *= 1.0 - scanDarken * 0.3;

                float scanBarPosition = frac(time * _ScanBarSpeed);
                float scanBarDistance =
                    abs(frac(uv.y - scanBarPosition + 0.5) - 0.5);
                float scanBar =
                    pow(
                        saturate(1.0 - scanBarDistance / max(_ScanBarWidth, 0.001)),
                        3.0);
                hologram.rgb += _HologramColor.rgb * scanBar * _ScanBarStrength;

                float2 noiseCell =
                    floor(uv * float2(_NoiseScale, _NoiseScale * 0.63) +
                          floor(time * _NoiseSpeed));
                float staticNoise = Hash21(noiseCell) - 0.5;
                hologram.rgb +=
                    _HologramColor.rgb *
                    staticNoise *
                    _NoiseStrength *
                    (0.45 + luminance);

                float flicker =
                    1.0 -
                    Hash21(float2(floor(time * _FlickerRate), 73.19)) *
                    _FlickerStrength;
                float dropout =
                    1.0 -
                    glitchPulse *
                    _GlitchDropout *
                    lerp(0.35, 1.0, Hash21(float2(glitchFrame, glitchBand + 91.7)));

                hologram *= input.color;
                hologram.rgb *= flicker;
                hologram.a *= _Opacity * flicker * dropout;

                #ifdef UNITY_UI_CLIP_RECT
                hologram.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(hologram.a - 0.001);
                #endif

                return hologram;
            }
            ENDCG
        }
    }
}
