Shader "BattlePass/DiagonalScroll"
{
    // ─────────────────────────────────────────────────────────────────────
    // Diagonal scrolling texture — a pattern texture that continuously
    // scrolls across the surface at a diagonal angle, blended over the
    // background colour using overlay blend mode.
    //
    // Designed for UI Images: uses [unity_GUIZTestMode], alpha blend,
    // preserves the sprite's alpha, and is SRP-Batcher compatible.
    //
    // Performance: single-pass, clip() for fill-rate culling.
    // ─────────────────────────────────────────────────────────────────────

    Properties
    {
        [PerRendererData] _MainTex ("Base Texture (Sprite)", 2D) = "white" {}
        _ScrollTex     ("Scroll Pattern", 2D)   = "white" {}
        _ScrollColor   ("Scroll Color", Color)  = (1, 1, 1, 0.5)
        _ScrollSpeed   ("Scroll Speed", Float)  = 0.3
        _ScrollAngle   ("Scroll Angle (deg)", Range(0,360)) = 45
        _ScrollScale   ("Pattern Scale", Float) = 1.0
        _TextureRotation("Texture Rotation (deg)", Range(0,360)) = 0
        _OverlayStrength("Overlay Strength", Range(0,1)) = 0.5

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
            Name "DiagonalScroll"

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
                float4 _ScrollTex_ST;
                half4  _ScrollColor;
                float  _ScrollSpeed;
                float  _ScrollAngle;
                float  _ScrollScale;
                float  _TextureRotation;
                float  _OverlayStrength;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_ScrollTex);
            SAMPLER(sampler_ScrollTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                // Screen-space position for camera-projection-style UVs.
                OUT.screenPos   = OUT.positionHCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── Base sprite colour ───────────────────────────────────
                half3 baseRGB = tex.rgb * IN.color.rgb;
                half  baseA   = tex.a * IN.color.a;

                // ── Diagonal scrolling UV (screen-space projection) ─────
                // Instead of using the sprite UV, project the scroll pattern
                // from screen/camera space so the pattern scale is constant
                // regardless of the UI Image's size or aspect ratio.
                //
                // 1) Convert clip-space position to NDC (0..1) screen UV.
                // 2) Scale by _ScrollScale for tiling density (pixels per
                //    tile). Higher = more repeats across the screen.
                // 3) Rotate the pattern around screen centre by
                //    _TextureRotation degrees.
                // 4) Offset along _ScrollAngle over time for diagonal scroll.

                // Step 1: screen-space UV from clip position
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                screenUV = screenUV * 0.5 + 0.5;

                // Step 2: scale for tiling density
                screenUV *= _ScrollScale;

                // Step 3: rotate around centre
                float rotRad = radians(_TextureRotation);
                float rotCos = cos(rotRad);
                float rotSin = sin(rotRad);
                float2 centered = screenUV - 0.5 * _ScrollScale;
                float2 rotatedUV;
                rotatedUV.x = centered.x * rotCos - centered.y * rotSin;
                rotatedUV.y = centered.x * rotSin + centered.y * rotCos;
                rotatedUV += 0.5 * _ScrollScale;

                // Step 4: diagonal scroll offset
                float scrollRad = radians(_ScrollAngle);
                float2 scrollUV;
                scrollUV.x = rotatedUV.x + _Time.y * _ScrollSpeed * cos(scrollRad);
                scrollUV.y = rotatedUV.y + _Time.y * _ScrollSpeed * sin(scrollRad);

                // Sample the scroll pattern texture
                half4 scrollTex = SAMPLE_TEXTURE2D(_ScrollTex, sampler_ScrollTex, scrollUV);

                // ── Overlay blend: scroll pattern over base colour ───────
                //   base < 0.5 → 2 * base * blend
                //   base ≥ 0.5 → 1 - 2 * (1-base) * (1-blend)
                half3 blendCol = scrollTex.rgb * _ScrollColor.rgb;
                half3 overlay  = baseRGB < 0.5
                    ? 2.0 * baseRGB * blendCol
                    : 1.0 - 2.0 * (1.0 - baseRGB) * (1.0 - blendCol);

                // Lerp between base and overlay by pattern alpha * strength
                half  overlayAmt = scrollTex.a * _OverlayStrength * _ScrollColor.a;
                half3 outRGB     = lerp(baseRGB, overlay, saturate(overlayAmt));

                // Alpha stays at base level so the sprite shape is preserved
                half outA = baseA;

                clip(outA - 0.005);
                return half4(outRGB, outA);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}