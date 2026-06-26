Shader "BattlePass/ParticleAdditiveHDR"
{
    // ─────────────────────────────────────────────────────────────────────
    // Unlit additive particle shader with HDR tint and stencil support.
    //
    // Optimised for particle systems:
    //   • Single pass, no lighting, no shadow, no GBuffer writes.
    //   • Additive blending — zero overdraw cost on opaque content behind.
    //   • HDR tint colour for bloom / glow effects.
    //   • Vertex colour modulates tint (ParticleSystem startColor works).
    //   • Soft particles via depth-based edge fade (optional).
    //   • Stencil-enabled for Mask / RectMask2D / ScrollRect clipping.
    //   • SRP Batcher compatible (UnityPerMaterial CBUFFER).
    //   • clip() discard for fill-rate culling on near-transparent pixels.
    //   • Suitable for mobile (target 2.0, minimal ALU).
    //   • Fake bloom: _GlowPower softens alpha falloff, _GlowBoost adds
    //     a radial halo from particle centre — simulates bloom without
    //     post-processing. Set both to defaults to disable.
    //
    // Usage:
    //   Assign to a Particle System Renderer's Material slot.
    //   Set _TintColor intensity > 1 in HDR picker for bloom glow.
    //   For fake bloom without post-processing: set _GlowPower < 1.0
    //   (e.g. 0.5) and _GlowBoost > 0 (e.g. 0.5).
    // ─────────────────────────────────────────────────────────────────────

    Properties
    {
        _MainTex       ("Particle Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color", Color)  = (1, 1, 1, 1)
        _ColorStrength ("Color Strength", Float) = 1.0
        _AlphaStrength ("Alpha Strength", Range(0,5)) = 1.0

        // ── Fake bloom (procedural glow, no post-processing needed) ──
        _GlowPower ("Glow Power (fake bloom)", Range(0.25,4.0)) = 1.0
        _GlowBoost ("Glow Boost (fake bloom)", Range(0,3.0)) = 0.0

        // ── Soft particles (depth-based edge fade) ──
        _SoftParticlesNear ("Soft Particles Near", Float) = 0.0
        _SoftParticlesFar  ("Soft Particles Far",  Float) = 0.0

        // ── Stencil (required for Mask / RectMask2D / ScrollRect) ──
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
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "ParticleAdditiveHDR"

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
            #pragma multi_compile_instancing

            // Soft particles need depth texture — only compile when enabled
            #pragma multi_compile_local _ _SOFTPARTICLES_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            #if defined(_SOFTPARTICLES_ON)
                float4 screenPos   : TEXCOORD1;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                float  _ColorStrength;
                float  _AlphaStrength;
                float  _GlowPower;
                float  _GlowBoost;
                float  _SoftParticlesNear;
                float  _SoftParticlesFar;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Depth texture for soft particles
            #if defined(_SOFTPARTICLES_ON)
                TEXTURE2D(_CameraDepthTexture);
                SAMPLER(sampler_CameraDepthTexture);
            #endif

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
            #if defined(_SOFTPARTICLES_ON)
                OUT.screenPos   = ComputeScreenPos(OUT.positionHCS);
            #endif
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ── Soft particles: fade alpha near scene geometry ───────
                half softFade = 1.0;
            #if defined(_SOFTPARTICLES_ON)
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float sceneDepth = LinearEyeDepth(
                    SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r,
                    _ZBufferParams);
                float thisDepth = LinearEyeDepth(IN.positionHCS.z, _ZBufferParams);
                float depthDiff = sceneDepth - thisDepth;
                float fadeRange = max(_SoftParticlesFar - _SoftParticlesNear, 0.0001);
                softFade = saturate((depthDiff - _SoftParticlesNear) / fadeRange);
            #endif

                // ── Combine: HDR tint × vertex colour × texture ─────────
                half3 rgb = tex.rgb * _TintColor.rgb * IN.color.rgb * _ColorStrength;
                half  a   = tex.a * _TintColor.a * IN.color.a * _AlphaStrength * softFade;

                // ── Fake bloom: power curve + radial halo ──────────────
                // _GlowPower < 1.0 softens alpha falloff (wider, gentler
                // glow). _GlowBoost > 0 adds a radial halo from the
                // particle centre (UV 0.5, 0.5), simulating bloom spread.
                a = pow(a, _GlowPower);

                float2 toCenter = IN.uv - 0.5;
                float radial = 1.0 - saturate(length(toCenter) * 2.0);
                half halo = pow(radial, 3.0) * _GlowBoost;
                a += halo;
                rgb += _TintColor.rgb * IN.color.rgb * halo;

                // Fill-rate culling: discard near-invisible pixels
                clip(a - 0.005);
                return half4(rgb * a, a);
            }
            ENDHLSL
        }
    }

    FallBack "Unlit/Transparent"
}