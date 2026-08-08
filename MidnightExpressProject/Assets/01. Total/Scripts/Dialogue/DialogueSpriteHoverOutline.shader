Shader "MidnightExpress/Dialogue Sprite Hover Outline"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _OutlineColor("Outline Color", Color) = (1, 0.76, 0.34, 1)
        _OutlineWidth("Outline Width", Range(0.5, 4)) = 1.5
        _OutlineEnabled("Outline Enabled", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment OutlineLitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _OutlineEnabled;
            CBUFFER_END

            float4 _MainTex_TexelSize;

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonLitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 OutlineLitFragment(Varyings input) : SV_Target
            {
                half4 litColor = CommonLitFragment(input, input.color);
                float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                half centerAlpha = SampleAlpha(input.uv) * input.color.a;
                half neighborAlpha = 0;
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv + float2(texel.x, 0)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv - float2(texel.x, 0)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv + float2(0, texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv - float2(0, texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv + texel));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv - texel));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv + float2(texel.x, -texel.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(input.uv + float2(-texel.x, texel.y)));

                half outlineAlpha = saturate(neighborAlpha - centerAlpha)
                    * _OutlineColor.a
                    * saturate(_OutlineEnabled);
                litColor.rgb = lerp(litColor.rgb, _OutlineColor.rgb, outlineAlpha);
                litColor.a = max(litColor.a, outlineAlpha);
                return litColor;
            }
            ENDHLSL
        }
    }
}
