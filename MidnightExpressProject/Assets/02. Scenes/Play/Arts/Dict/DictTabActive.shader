Shader "MidnightExpress/UI/Collection Active Tab"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _ShadowColor("Gold Shadow", Color) = (0.20, 0.08, 0.015, 1)
        _MidColor("Gold Midtone", Color) = (0.52, 0.27, 0.045, 1)
        _HighlightColor("Gold Highlight", Color) = (1.0, 0.76, 0.25, 1)
        [HideInInspector] _Color("Tint", Color) = (1, 1, 1, 1)

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
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
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _ShadowColor;
            fixed4 _MidColor;
            fixed4 _HighlightColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.worldPosition = input.positionOS;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, input.uv) + _TextureSampleAdd;
                fixed luminance = dot(sample.rgb, fixed3(0.299, 0.587, 0.114));
                fixed midBlend = smoothstep(0.07, 0.16, luminance);
                fixed highlightBlend = smoothstep(0.18, 0.45, luminance);
                fixed3 gold = lerp(_ShadowColor.rgb, _MidColor.rgb, midBlend);
                gold = lerp(gold, _HighlightColor.rgb, highlightBlend);
                fixed alpha = sample.a * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(gold * input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
