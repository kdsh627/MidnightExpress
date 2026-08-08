Shader "MidnightExpress/Loading/UI Image Structure Lines"
{
    Properties
    {
        [PerRendererData] _MainTex("Image Texture", 2D) = "white" {}
        [HDR] _ReplacementColor("Line Color", Color) = (1, 1, 1, 1)
        _PixelRows("Pixel Detail Resolution", Range(32, 256)) = 112
        _EdgeThickness("Line Thickness (Pixels)", Range(1, 3)) = 1
        _EdgeThreshold("Structure Threshold", Range(0.01, 2)) = 0.31
        _SourceAlphaCutoff("Source Alpha Cutoff", Range(0, 1)) = 0.35
        _BuildingScrollOffset("Building Scroll Offset", Float) = 0
        _BuildingRegionStartY("Building Region Start Y", Range(0, 1)) = 1
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
            Name "UIStructureLines"

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _ReplacementColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _PixelRows;
            float _EdgeThickness;
            float _EdgeThreshold;
            float _SourceAlphaCutoff;
            float _BuildingScrollOffset;
            float _BuildingRegionStartY;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.positionOS;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 SampleImage(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                float buildingRegion = step(_BuildingRegionStartY, input.uv.y);
                float2 sampledUV = input.uv;
                sampledUV.x = lerp(
                    sampledUV.x,
                    frac(sampledUV.x + _BuildingScrollOffset),
                    buildingRegion);

                // Build a square-pixel grid from a vertical target resolution.
                // Horizontal resolution follows the source aspect ratio, so a long
                // train keeps its proportions while retaining a restrained pixel-art edge.
                float sourceAspect = _MainTex_TexelSize.z / max(1.0, _MainTex_TexelSize.w);
                float2 pixelGrid = float2(max(1.0, round(_PixelRows * sourceAspect)), _PixelRows);
                float2 pixelUV = (floor(sampledUV * pixelGrid) + 0.5) / pixelGrid;
                float2 texel = (1.0 / pixelGrid) * _EdgeThickness;

                fixed4 topLeft = SampleImage(pixelUV + float2(-texel.x, texel.y));
                fixed4 top = SampleImage(pixelUV + float2(0, texel.y));
                fixed4 topRight = SampleImage(pixelUV + texel);
                fixed4 left = SampleImage(pixelUV + float2(-texel.x, 0));
                fixed4 center = SampleImage(pixelUV);
                fixed4 right = SampleImage(pixelUV + float2(texel.x, 0));
                fixed4 bottomLeft = SampleImage(pixelUV - texel);
                fixed4 bottom = SampleImage(pixelUV + float2(0, -texel.y));
                fixed4 bottomRight = SampleImage(pixelUV + float2(texel.x, -texel.y));

                // Premultiplying RGB by source alpha prevents transparent-pixel RGB
                // from producing false lines around imported sprites.
                topLeft.rgb *= topLeft.a;
                top.rgb *= top.a;
                topRight.rgb *= topRight.a;
                left.rgb *= left.a;
                center.rgb *= center.a;
                right.rgb *= right.a;
                bottomLeft.rgb *= bottomLeft.a;
                bottom.rgb *= bottom.a;
                bottomRight.rgb *= bottomRight.a;

                float4 gradientX =
                    -topLeft - 2 * left - bottomLeft
                    + topRight + 2 * right + bottomRight;
                float4 gradientY =
                    topLeft + 2 * top + topRight
                    - bottomLeft - 2 * bottom - bottomRight;

                const float3 luminanceWeights = float3(0.2126, 0.7152, 0.0722);
                float luminanceX = dot(gradientX.rgb, luminanceWeights);
                float luminanceY = dot(gradientY.rgb, luminanceWeights);
                float colorEdge = sqrt(luminanceX * luminanceX + luminanceY * luminanceY);
                float alphaEdge = sqrt(
                    gradientX.a * gradientX.a
                    + gradientY.a * gradientY.a);
                float structure = max(colorEdge, alphaEdge);
                // Binary output: a detected structure pixel is fully colored;
                // every other pixel is completely transparent.
                float lineMask = step(_EdgeThreshold, structure);

                float sourceCoverage = max(center.a, max(
                    max(max(topLeft.a, top.a), max(topRight.a, left.a)),
                    max(max(right.a, bottomLeft.a), max(bottom.a, bottomRight.a))));
                sourceCoverage = step(_SourceAlphaCutoff, sourceCoverage);
                fixed alpha = lineMask
                    * sourceCoverage
                    * input.color.a
                    * _ReplacementColor.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(_ReplacementColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
