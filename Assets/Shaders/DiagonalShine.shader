Shader "BattlePass/DiagonalShine"
{
    // ─────────────────────────────────────────────────────────────────────
    // Diagonal shine band — a ribbon-shaped highlight that sweeps across
    // the surface at an angle, looping every _SweepPeriod seconds.
    //
    // Designed for UI Images: uses [unity_GUIZTestMode], additive blend,
    // preserves the sprite's alpha, and is SRP-Batcher compatible.
    //
    // Screen-space mode (_ScreenSpace = 1): shine band is projected from
    // screen coordinates so the sweep is continuous across all UI Images
    // sharing this material — identical to DiagonalScroll's approach.
    // UV mode (_ScreenSpace = 0): each Image gets its own independent sweep.
    //
    // Performance: single-pass additive, clip() for fill-rate culling.
    // ─────────────────────────────────────────────────────────────────────

    Properties
    {
        [PerRendererData] _MainTex ("Base Texture (Sprite)", 2D) = "white" {}
        _ShineColor    ("Shine Color", Color)     = (1, 1, 1, 0.8)
        _ShineWidth    ("Shine Width", Range(0.01,5)) = 0.12
        _ShineAngle    ("Shine Angle (deg)", Range(0,360)) = 30
        _SweepPeriod   ("Sweep Period (sec)", Float) = 1.0
        _PauseDuration ("Pause Between Sweeps (sec)", Float) = 0.0
        _ShineDistance ("Shine Travel Distance", Range(-10,10)) = 0.2
        _ShineIntensity("Shine Intensity", Float) = 1.0
        _AutoPlay      ("Auto Play (0/1)", Float) = 1
        _SweepOffset   ("Manual Sweep Offset", Range(-10,10)) = 0
        _ScreenSpace   ("Screen Space Shine (0/1)", Float) = 0

        // ── Stencil (required for ScrollRect / Mask / RectMask2D) ──
        _StencilComp   ("Stencil Comparison", Float) = 8
        _Stencil       ("Stencil ID", Float) = 0
        _StencilOp     ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask     ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DiagonalShine"

            Stencil
            {
                Ref      [_Stencil]
                Comp     [_StencilComp]
                Pass     [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask[_StencilWriteMask]
            }
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float4 screenPos   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _ShineColor;
                half   _ShineWidth;
                float  _ShineAngle;
                float  _SweepPeriod;
                float  _PauseDuration;
                float  _ShineDistance;
                float  _ShineIntensity;
                float  _AutoPlay;
                float  _SweepOffset;
                float  _ScreenSpace;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                OUT.screenPos   = OUT.positionHCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── Base sprite colour ───────────────────────────────────
                half3 baseRGB = tex.rgb * IN.color.rgb;
                half  baseA   = tex.a * IN.color.a;

                // ── Shine band ───────────────────────────────────────────
                // Project UV onto the sweep axis (rotated by _ShineAngle).
                // In screen-space mode, use screen coordinates instead of
                // sprite UV so the sweep is continuous across all Images
                // sharing this material.
                float2 shineUV;
                if (_ScreenSpace > 0.5)
                {
                    // Screen-space UV from clip position (0..1)
                    shineUV = IN.screenPos.xy / IN.screenPos.w;
                    shineUV = shineUV * 0.5 + 0.5;
                }
                else
                {
                    shineUV = IN.uv;
                }

                float rad  = radians(_ShineAngle);
                float axis = shineUV.x * cos(rad) + shineUV.y * sin(rad);

                // Sweep position: auto (time-driven, looping every
                // _SweepPeriod seconds) or manual (_SweepOffset from C#).
                // After each sweep there is a _PauseDuration second pause
                // where the band is fully off-screen (invisible).
                float cycle = max(_SweepPeriod + _PauseDuration, 0.0001);
                float t     = _Time.y % cycle;
                float sweep = _AutoPlay > 0.5
                    ? saturate(t / max(_SweepPeriod, 0.0001))
                    : _SweepOffset;

                // Remap sweep from 0..1 to -_ShineDistance..1+_ShineDistance
                // so the band travels beyond the UV edges before/after the
                // visible area. Higher _ShineDistance = longer travel path.
                float pos = lerp(-_ShineDistance, 1.0 + _ShineDistance, sweep);

                // Soft ribbon band: two smoothsteps create a gradient strip
                // of width _ShineWidth centred on `pos`.
                float halfW = _ShineWidth * 0.5;
                float band  = smoothstep(pos - halfW, pos,        axis)
                            - smoothstep(pos,        pos + halfW, axis);

                // Overlay blend: preserves luminance, boosts contrast.
                //   base < 0.5 → 2 * base * blend
                //   base ≥ 0.5 → 1 - 2 * (1-base) * (1-blend)
                half3 blendCol = _ShineColor.rgb;
                half3 overlay  = baseRGB < 0.5
                    ? 2.0 * baseRGB * blendCol
                    : 1.0 - 2.0 * (1.0 - baseRGB) * (1.0 - blendCol);

                // Lerp between base and overlay by band * intensity
                half  shineAmt = band * _ShineIntensity;
                half3 outRGB   = lerp(baseRGB, overlay, saturate(shineAmt));

                // Alpha stays at base level so the sprite shape is preserved;
                // shine only brightens existing pixels.
                half outA = baseA;

                clip(outA - 0.005);
                return half4(outRGB, outA);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}