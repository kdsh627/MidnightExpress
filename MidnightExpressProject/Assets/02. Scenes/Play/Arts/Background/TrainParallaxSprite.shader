Shader "MidnightExpress/Train Parallax Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ScrollOffset ("Horizontal Scroll Offset", Float) = 0
        _SeamBlendWidth ("Seam Blend Width", Range(0.001, 0.1)) = 0.02
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _ScrollOffset;
                float _SeamBlendWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = float2(input.uv.x + _ScrollOffset, input.uv.y);
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float wrappedU = frac(input.uv.x);
                float seamDistance = wrappedU < 0.5 ? wrappedU : wrappedU - 1.0;
                float2 sampleUv = float2(wrappedU, input.uv.y);
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUv);

                if (abs(seamDistance) < _SeamBlendWidth)
                {
                    float blendPosition = saturate((seamDistance + _SeamBlendWidth) / (2.0 * _SeamBlendWidth));
                    float endU = 1.0 - _SeamBlendWidth + blendPosition * _SeamBlendWidth;
                    float startU = blendPosition * _SeamBlendWidth;
                    half4 endColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(endU, input.uv.y));
                    half4 startColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(startU, input.uv.y));
                    sampled = lerp(endColor, startColor, smoothstep(0.0, 1.0, blendPosition));
                }

                half4 color = sampled * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
