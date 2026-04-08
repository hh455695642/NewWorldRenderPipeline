Shader "NewWorld/UI/UI_BaseEffect_TwoMasks"
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

        [Header(Main)]
        [Space(5)]
        _BaseTex ("主贴图", 2D) = "white" {}

        [KeywordEnum(R, G, B, A)] _BaseChannel ("主贴图通道", Float) = 3
        [KeywordEnum(Repeat, Clamp)] _BaseUVMode ("主贴图UV", Float) = 0
        _BaseRotation ("主贴图旋转", Range(-1, 1)) = 0
        _BaseTilingOffset ("主贴图tilling&offset", Vector) = (1, 1, 0, 0)
        _BasePanSpeed ("主贴图流动速度", Vector) = (0, 0, 0, 0)

        [Toggle(CUSTOM1XY_BASE)] _Custom1xyBase ("custom1xy控制主贴图偏移", Int) = 0

        [Header(Mask)]
        [Space(5)]
        _Mask01Tex ("遮罩01", 2D) = "white" {}

        [KeywordEnum(R, G, B, A)] _Mask01Channel ("mask01通道", Float) = 0
        [KeywordEnum(Repeat, Clamp)] _Mask01UVMode ("Mask01UV", Float) = 0
        _Mask01Rotation ("遮罩01旋转", Range(-1, 1)) = 0
        _Mask01TilingOffset ("Mask01_tilling&offset", Vector) = (1, 1, 0, 0)
        _Mask01PanSpeed ("Mask01流动速度", Vector) = (0, 0, 0, 0)

        [Space(10)]
        [Toggle(USE_MASK2)] _UseMask2 ("Use_mask2", Int) = 0
        _Mask02Tex ("遮罩02", 2D) = "white" {}

        [KeywordEnum(R, G, B, A)] _Mask02Channel ("mask02通道", Float) = 0
        [KeywordEnum(Repeat, Clamp)] _Mask02UVMode ("Mask02UV", Float) = 0
        _Mask02Rotation ("遮罩02旋转", Range(-1, 1)) = 0
        _Mask02TilingOffset ("Mask02_tilling&offset", Vector) = (1, 1, 0, 0)
        _Mask02PanSpeed ("Mask02流动速度", Vector) = (0, 0, 0, 0)

        [Toggle(CUSTOM1ZW_MASK01)] _Custom1zwMask01 ("custom1zw控制mask01偏移", Int) = 0
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
            Name "UI_BaseEffect_TwoMasks"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #pragma shader_feature_local _ CUSTOM1XY_BASE
            #pragma shader_feature_local _ USE_MASK2
            #pragma shader_feature_local _ CUSTOM1ZW_MASK01

            // 通道选择: 用 multi_compile 会产生太多变体，改用 float 分支
            // _BaseChannel: 0=R, 1=G, 2=B, 3=A
            // _Mask01Channel: 0=R, 1=G, 2=B, 3=A
            // _Mask02Channel: 0=R, 1=G, 2=B, 3=A

            // =================================================================
            // Unity UI 内置变量
            // =================================================================
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            // =================================================================
            // Main
            // =================================================================
            sampler2D _BaseTex;
            float4 _BaseTex_ST;
            float _BaseChannel;
            float _BaseUVMode;
            float _BaseRotation;
            float4 _BaseTilingOffset;
            float4 _BasePanSpeed;

            // =================================================================
            // Mask 01
            // =================================================================
            sampler2D _Mask01Tex;
            float4 _Mask01Tex_ST;
            float _Mask01Channel;
            float _Mask01UVMode;
            float _Mask01Rotation;
            float4 _Mask01TilingOffset;
            float4 _Mask01PanSpeed;

            // =================================================================
            // Mask 02
            // =================================================================
            sampler2D _Mask02Tex;
            float4 _Mask02Tex_ST;
            float _Mask02Channel;
            float _Mask02UVMode;
            float _Mask02Rotation;
            float4 _Mask02TilingOffset;
            float4 _Mask02PanSpeed;

            // _MainTex for UI compat
            sampler2D _MainTex;
            float4 _MainTex_ST;

            // =================================================================
            // 结构体
            // =================================================================
            struct appdata_t
            {
                float4 vertex    : POSITION;
                float4 color     : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;   // custom data
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

            // =================================================================
            // 工具函数
            // =================================================================

            // UV 旋转（绕 0.5,0.5 中心旋转，rotation 范围 -1~1 映射到 -PI~PI）
            float2 RotateUV(float2 uv, float rotation)
            {
                float angle = rotation * UNITY_PI;
                float s, c;
                sincos(angle, s, c);
                float2 center = float2(0.5, 0.5);
                uv -= center;
                float2 rotated = float2(
                    uv.x * c - uv.y * s,
                    uv.x * s + uv.y * c
                );
                return rotated + center;
            }

            // 应用 tiling & offset（Vector4: xy=tiling, zw=offset）
            float2 ApplyTilingOffset(float2 uv, float4 to)
            {
                return uv * to.xy + to.zw;
            }

            // 从 float4 中按通道索引提取值
            float SampleChannel(float4 texColor, float channelIndex)
            {
                // 0=R, 1=G, 2=B, 3=A
                // 使用 dot 技巧避免分支
                float4 channelMask = float4(
                    step(channelIndex, 0.5),                                          // R: index < 0.5
                    step(0.5, channelIndex) * step(channelIndex, 1.5),               // G: 0.5 <= index < 1.5
                    step(1.5, channelIndex) * step(channelIndex, 2.5),               // B: 1.5 <= index < 2.5
                    step(2.5, channelIndex)                                           // A: index >= 2.5
                );
                return dot(texColor, channelMask);
            }

            // =================================================================
            // 顶点着色器
            // =================================================================
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                // UI mask 计算
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

            // =================================================================
            // 片元着色器
            // =================================================================
            half4 frag(v2f IN) : SV_Target
            {
                // ---------------------------------------------------------
                // 主贴图 UV
                // ---------------------------------------------------------
                float2 baseUV = IN.uv;

                // 旋转
                baseUV = RotateUV(baseUV, _BaseRotation);

                // Tiling & Offset
                baseUV = ApplyTilingOffset(baseUV, _BaseTilingOffset);

                // 流动
                baseUV += _Time.y * _BasePanSpeed.xy;

                // Custom1 xy 偏移
                #if defined(CUSTOM1XY_BASE)
                    baseUV += IN.customData.xy;
                #endif

                // 采样主贴图
                float4 baseSample = tex2D(_BaseTex, baseUV);
                float baseValue = SampleChannel(baseSample, _BaseChannel);

                // ---------------------------------------------------------
                // 遮罩01 UV
                // ---------------------------------------------------------
                float2 mask01UV = IN.uv;

                // 旋转
                mask01UV = RotateUV(mask01UV, _Mask01Rotation);

                // Tiling & Offset
                mask01UV = ApplyTilingOffset(mask01UV, _Mask01TilingOffset);

                // 流动
                mask01UV += _Time.y * _Mask01PanSpeed.xy;

                // Custom1 zw 偏移 mask01
                #if defined(CUSTOM1ZW_MASK01)
                    mask01UV += IN.customData.zw;
                #endif

                // 采样遮罩01
                float4 mask01Sample = tex2D(_Mask01Tex, mask01UV);
                float mask01Value = SampleChannel(mask01Sample, _Mask01Channel);

                // ---------------------------------------------------------
                // 遮罩02 UV
                // ---------------------------------------------------------
                float mask02Value = 1.0;

                #if defined(USE_MASK2)
                    float2 mask02UV = IN.uv;

                    // 旋转
                    mask02UV = RotateUV(mask02UV, _Mask02Rotation);

                    // Tiling & Offset
                    mask02UV = ApplyTilingOffset(mask02UV, _Mask02TilingOffset);

                    // 流动
                    mask02UV += _Time.y * _Mask02PanSpeed.xy;

                    // 采样遮罩02
                    float4 mask02Sample = tex2D(_Mask02Tex, mask02UV);
                    mask02Value = SampleChannel(mask02Sample, _Mask02Channel);
                #endif

                // ---------------------------------------------------------
                // 最终合成
                // ---------------------------------------------------------
                // 主贴图值 × 遮罩01 × 遮罩02 × 顶点色 alpha → 作为强度因子
                float intensity = baseValue * mask01Value * mask02Value * IN.color.a;

                // Additive 输出：颜色 × 强度
                float3 finalColor = IN.color.rgb * intensity;

                // ---------------------------------------------------------
                // UI 裁剪（RectMask2D）
                // ---------------------------------------------------------
                float clipFactor = 1.0;

                #ifdef UNITY_UI_CLIP_RECT
                    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                    clipFactor = m.x * m.y;
                #endif

                finalColor *= clipFactor;

                #ifdef UNITY_UI_ALPHACLIP
                    float outLum = dot(finalColor, float3(0.299, 0.587, 0.114));
                    clip(outLum * clipFactor - 0.001);
                #endif

                return half4(finalColor, 1.0);
            }

            ENDCG
        }
    }

    FallBack "UI/Default"
}