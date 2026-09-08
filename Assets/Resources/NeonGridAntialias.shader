Shader "NeonReflex/UI/Grid Coverage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
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
            Name "GridCoverage"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 shape : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 localPoint : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            fixed4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                #ifndef UNITY_COLORSPACE_GAMMA
                if (_UIVertexColorAlwaysGammaSpace != 0)
                    output.color.rgb = UIGammaToLinear(output.color.rgb);
                #endif
                output.localPoint = input.uv.xy;
                output.shape = input.shape;
                float2 pixelSize = output.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    .25 / (.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float rectangleCoverage(float2 localPoint, float2 halfSize, float2 footprint)
            {
                float2 coverage = saturate((halfSize - abs(localPoint)) / footprint + .5);
                return coverage.x * coverage.y;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Derivatives follow the final raster transform, including the
                // Canvas scaler, target role size, grid scale, and rotation.
                float2 footprint = max(fwidth(input.localPoint), .00001);
                float2 halfSize = input.shape.xy;
                float coverage = rectangleCoverage(input.localPoint, halfSize, footprint);
                if (input.shape.z >= 0)
                {
                    float2 inner = max(halfSize - input.shape.z, 0);
                    if (min(inner.x, inner.y) > 0)
                        coverage -= rectangleCoverage(input.localPoint, inner, footprint);
                    if (input.shape.w < .5)
                    {
                        float2 cornerStart = halfSize - max(halfSize * 2 * input.shape.w, input.shape.z);
                        float2 corners = saturate((abs(input.localPoint) - cornerStart) / footprint + .5);
                        coverage *= corners.x * corners.y;
                    }
                }
                fixed4 color = input.color;
                color.a *= saturate(coverage);
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color.a *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - .001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
