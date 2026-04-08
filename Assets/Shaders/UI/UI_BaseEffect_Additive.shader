Shader "NewWorld/UI/UI_BaseEffect_Additive"
{
    Properties
    {
        // =====================================================================
        // Unity UI 必需属性（Mask / RectMask2D 支持）
        // =====================================================================
        [PerRendererData] _MainTex ("Sprite Texture (unused, for UI compatibility)", 2D) = "white" {}
        _Color ("Tint (UI)", Color) = (1,1,1,1)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Toggle(ISCUSTOM)] _IsCustom ("启用自定义数据", Int) = 0

        [Header(Base Map)]
        [Space(5)]
        [Toggle(BASE_POLAR)] _BasePolar ("启用极坐标", Int) = 0
        _BasePolarCenter ("极坐标中心", Vector) = (0.5, 0.5, 0, 0)
        _BasePolarRadius ("极坐标半径偏移", Range(-0.5, 0.5)) = 0
        [HDR] _BaseColor ("基础颜色 (HDR)", Color) = (1,1,1,1)
        _BaseMap ("基础贴图", 2D) = "white" {}
        _BaseMapBrightness ("亮度", Range(0.1, 5)) = 1
        _BaseMapContrast ("对比度", Range(0.1, 5)) = 1
        _BaseMapPannerX ("X轴流动速度", Float) = 0
        _BaseMapPannerY ("Y轴流动速度", Float) = 0
        _RemoveBlack ("去黑底强度", Range(0, 1)) = 0

        [Header(Mask Map)]
        [Space(5)]
        [Toggle(MASK_POLAR)] _MaskPolar ("启用极坐标", Int) = 0
        _MaskPolarCenter ("极坐标中心", Vector) = (0.5, 0.5, 0, 0)
        _MaskPolarRadius ("极坐标半径偏移", Range(-0.5, 0.5)) = 0
        _MaskIntensity ("遮罩强度", Range(0, 1)) = 1
        _MaskTex ("遮罩贴图", 2D) = "white" {}
        _MaskPannerX ("X轴流动速度", Float) = 0
        _MaskPannerY ("Y轴流动速度", Float) = 0

        [Header(Intensity Settings)]
        [Space(5)]
        _Intensity ("整体强度", Range(0, 5)) = 1
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

        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI_BaseEffect_Additive"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _ ISCUSTOM
            #pragma shader_feature_local _ BASE_POLAR
            #pragma shader_feature_local _ MASK_POLAR

            // =================================================================
            // Unity UI 内置变量
            // =================================================================
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            // =================================================================
            // 效果参数
            // =================================================================
            float4 _BaseColor;
            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _BasePolarCenter;
            float _BasePolarRadius;
            float _BaseMapBrightness;
            float _BaseMapContrast;
            float _BaseMapPannerX;
            float _BaseMapPannerY;
            float _RemoveBlack;

            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            float4 _MaskPolarCenter;
            float _MaskPolarRadius;
            float _MaskIntensity;
            float _MaskPannerX;
            float _MaskPannerY;

            float _Intensity;

            // _MainTex 用于 UI Image 兼容
            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                float4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                half4  mask          : TEXCOORD2;
                float4 customData    : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 ToPolarUV(float2 uv, float2 center, float radiusOffset)
            {
                float2 delta = uv - center;
                float angle = atan2(delta.y, delta.x);
                float radius = length(delta) + radiusOffset;
                float u = angle / (2.0 * UNITY_PI) + 0.5;
                float v = radius;
                return float2(u, v);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                // UI 裁剪 mask 计算（与 Unity UI Default 一致）
                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = half4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy))
                );

                OUT.uv = v.texcoord0.xy;
                OUT.customData = v.texcoord1;
                OUT.color = v.color * _Color;

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 baseUV = IN.uv;
                float2 maskUV = IN.uv;

                // ---------------------------------------------------------
                // 基础贴图UV
                // ---------------------------------------------------------
                #if defined(BASE_POLAR)
                    baseUV = ToPolarUV(IN.uv, _BasePolarCenter.xy, _BasePolarRadius);
                #endif

                baseUV = TRANSFORM_TEX(baseUV, _BaseMap);
                baseUV += _Time.y * float2(_BaseMapPannerX, _BaseMapPannerY);

                #if defined(ISCUSTOM)
                    baseUV += IN.customData.xy;
                #endif

                // ---------------------------------------------------------
                // 遮罩贴图UV
                // ---------------------------------------------------------
                #if defined(MASK_POLAR)
                    maskUV = ToPolarUV(IN.uv, _MaskPolarCenter.xy, _MaskPolarRadius);
                #endif

                maskUV = TRANSFORM_TEX(maskUV, _MaskTex);
                maskUV += _Time.y * float2(_MaskPannerX, _MaskPannerY);

                // ---------------------------------------------------------
                // 采样
                // ---------------------------------------------------------
                float4 baseSample = tex2D(_BaseMap, baseUV);
                float4 maskSample = tex2D(_MaskTex, maskUV);
                float maskValue = lerp(1.0, maskSample.r * maskSample.a, _MaskIntensity);

                // ---------------------------------------------------------
                // 颜色计算
                // ---------------------------------------------------------
                float3 baseColor = pow(abs(baseSample.rgb * _BaseMapBrightness), _BaseMapContrast);
                float3 finalColor = baseColor * _BaseColor.rgb * IN.color.rgb;

                // ---------------------------------------------------------
                // 去黑底
                // ---------------------------------------------------------
                float luminance = dot(baseSample.rgb, float3(0.299, 0.587, 0.114));
                float removeBlackFactor = lerp(1.0, luminance, _RemoveBlack);

                // ---------------------------------------------------------
                // 强度
                // ---------------------------------------------------------
                float intensityFactor = baseSample.a * _BaseColor.a * IN.color.a * maskValue;
                intensityFactor *= _Intensity * removeBlackFactor;

                // ---------------------------------------------------------
                // UI 裁剪（RectMask2D）
                // 因为是 Additive（Blend One One），alpha 不参与混合，
                // 所以我们直接把裁剪因子乘到颜色上来实现淡出/裁剪
                // ---------------------------------------------------------
                float clipFactor = 1.0;

                #ifdef UNITY_UI_CLIP_RECT
                    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                    clipFactor = m.x * m.y;
                #endif

                // Additive 最终输出
                finalColor *= intensityFactor * clipFactor;

                #ifdef UNITY_UI_ALPHACLIP
                    // 对于 alpha clip，我们用亮度近似判断
                    float outLum = dot(finalColor, float3(0.299, 0.587, 0.114));
                    clip(outLum * clipFactor - 0.001);
                #endif

                // Additive 模式下 alpha 实际不影响输出，写1.0
                return half4(finalColor, 1.0);
            }

            ENDCG
        }
    }

    FallBack "UI/Default"
}