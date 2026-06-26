Shader "BattlePass/WaveScroll"
{
    // ─────────────────────────────────────────────────────────────────────
    // Wave + Scroll shader — two features in one:
    //
    // 1) WAVE DEFORMATION: Vertices are displaced along a configurable
    //    direction using a sine wave. Wave direction, amplitude, frequency,
    //    and speed are all adjustable.
    //
    // 2) SCROLLING TEXTURE: A texture scrolls continuously along a
    //    configurable direction. Blended additively over the surface.
    //
    // Designed for 3D meshes (not UI). Uses URP HLSL, additive blend,
    // SRP-Batcher compatible.
    //
    // Performance: single-pass additive, clip() for fill-rate culling.
    //
    // Fake bloom: _GlowPower softens alpha falloff, _GlowBoost adds a
    //   radial halo from mesh centre — simulates bloom without
    //   post-processing. Set both to defaults to disable.
    // ─────────────────────────────────────────────────────────────────────

    Properties
    {
        _MainTex       ("Texture", 2D)             = "white" {}
        [HDR] _TintColor    ("Tint Color", Color)        = (1, 1, 1, 1)

        // ── Wave deformation ──
        _WaveAmplitude ("Wave Amplitude", Float)   = 0.1
        _WaveFreqX    ("Wave Frequency X", Float)  = 2.0
        _WaveFreqZ    ("Wave Frequency Z", Float)  = 2.0
        _WaveSpeed     ("Wave Speed", Float)       = 1.0
        _WaveDirection ("Wave Direction (deg)", Range(0,360)) = 0

        // ── Noise modulation (procedural, no texture needed) ──
        _NoiseAmplitude("Noise Amplitude", Float) = 0.05
        _NoiseScale    ("Noise Scale", Float)      = 3.0
        _NoiseSpeed    ("Noise Speed", Float)      = 0.5
        _NoiseInfluence("Noise Influence", Range(0,1)) = 0.5

        // ── Texture scroll ──
        _ScrollSpeed   ("Scroll Speed", Float)     = 0.3
        _ScrollAngle   ("Scroll Angle (deg)", Range(0,360)) = 45
        _ScrollScale   ("Pattern Scale", Float)    = 1.0
        _TextureRotation("Texture Rotation (deg)", Range(0,360)) = 0
        _FadeIn       ("Fade In Width", Range(0,0.5)) = 0.15
        _FadeOut      ("Fade Out Width", Range(0,0.5)) = 0.15

        // ── Fake bloom (procedural glow, no post-processing needed) ──
        _GlowPower    ("Glow Power (fake bloom)", Range(0.25,4.0)) = 1.0
        _GlowBoost    ("Glow Boost (fake bloom)", Range(0,3.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "WaveScroll"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS  : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 fadeUV       : TEXCOORD1;
                float4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                float  _WaveAmplitude;
                float  _WaveFreqX;
                float  _WaveFreqZ;
                float  _WaveSpeed;
                float  _WaveDirection;
                float  _NoiseAmplitude;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseInfluence;
                float  _ScrollSpeed;
                float  _ScrollAngle;
                float  _ScrollScale;
                float  _TextureRotation;
                float  _FadeIn;
                float  _FadeOut;
                float  _GlowPower;
                float  _GlowBoost;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ── Procedural 2D value noise (no texture needed) ──────────
            // Hash-based value noise with smooth interpolation.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // Smoothstep interpolation (quintic Perlin-style)
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // ── World-space position ────────────────────────────────
                // Use world-space coordinates so wave and noise are
                // consistent across multiple meshes sharing this material.
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float2 worldXZ = worldPos.xz;

                // ── Wave deformation (world-space) ─────────────────────
                // Apply independent sine waves on X and Z world axes, then
                // blend them by the wave direction so the result moves
                // along _WaveDirection while keeping per-axis frequency
                // control.
                float waveRad = radians(_WaveDirection);
                float2 waveDir = float2(cos(waveRad), sin(waveRad));

                // Per-axis sine waves (world-space XZ)
                float waveX = sin(worldXZ.x * _WaveFreqX + _Time.y * _WaveSpeed);
                float waveZ = sin(worldXZ.y * _WaveFreqZ + _Time.y * _WaveSpeed);

                // Blend by wave direction: waveDir.x weights X, waveDir.y weights Z
                float wave = waveX * abs(waveDir.x) + waveZ * abs(waveDir.y);

                // ── Noise modulation (world-space, procedural) ─────────
                // Generate scrolling value noise from world position and
                // remap it to -1..1, then blend into the wave.
                float2 noiseUV = worldXZ * _NoiseScale;
                noiseUV.x += _Time.y * _NoiseSpeed;
                noiseUV.y += _Time.y * _NoiseSpeed * 0.7;
                float noiseVal = noise2D(noiseUV);
                float noise = (noiseVal * 2.0 - 1.0) * _NoiseInfluence;

                // Combine wave + noise, displace along face normal
                float waveFinal = wave + noise * _NoiseAmplitude;
                float3 displacedPos = IN.positionOS.xyz;
                displacedPos += IN.normalOS * waveFinal * _WaveAmplitude;

                OUT.positionHCS = TransformObjectToHClip(displacedPos);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fadeUV      = IN.uv;  // raw 0..1 UV for edge fade
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // ── Scrolling UV ─────────────────────────────────────────
                // 1) Rotate the UV around centre (0.5, 0.5) by
                //    _TextureRotation degrees.
                // 2) Scale for tiling density.
                // 3) Offset along _ScrollAngle over time for diagonal scroll.

                // Step 1: rotate around centre
                float rotRad = radians(_TextureRotation);
                float rotCos = cos(rotRad);
                float rotSin = sin(rotRad);
                float2 uv = IN.uv - 0.5;
                float2 rotatedUV;
                rotatedUV.x = uv.x * rotCos - uv.y * rotSin;
                rotatedUV.y = uv.x * rotSin + uv.y * rotCos;
                rotatedUV += 0.5;

                // Step 2: scale
                float2 scrollUV = rotatedUV * _ScrollScale;

                // Step 3: diagonal scroll offset
                float scrollRad = radians(_ScrollAngle);
                scrollUV.x += _Time.y * _ScrollSpeed * cos(scrollRad);
                scrollUV.y += _Time.y * _ScrollSpeed * sin(scrollRad);

                // Sample the texture
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV);

                // ── Edge fade (ease-in & ease-out at mesh edges) ────────
                // Fade-in from one X edge (UV.x=0), fade-out from the
                // opposite X edge (UV.x=1). Uses raw unmodified UV so it
                // always maps to the 0..1 mesh boundary.
                float fin  = max(_FadeIn,  0.0001);
                float fout = max(_FadeOut, 0.0001);
                half edgeMask = smoothstep(0.0, fin, IN.fadeUV.x) * smoothstep(0.0, fout, 1.0 - IN.fadeUV.x);

                // ── Additive output ──────────────────────────────────────
                half3 rgb = tex.rgb * _TintColor.rgb * IN.color.rgb;
                half  a   = tex.a * _TintColor.a * IN.color.a * edgeMask;

                // ── Fake bloom: power curve + radial halo ──────────────
                // _GlowPower < 1.0 softens alpha falloff (wider, gentler
                // glow). _GlowBoost > 0 adds a radial halo from the mesh
                // centre (fadeUV 0.5, 0.5), simulating bloom spread.
                a = pow(a, _GlowPower);

                float2 toCenter = IN.fadeUV - 0.5;
                float radial = 1.0 - saturate(length(toCenter) * 2.0);
                half halo = pow(radial, 3.0) * _GlowBoost;
                a += halo;
                rgb += _TintColor.rgb * IN.color.rgb * halo;

                clip(a - 0.005);
                return half4(rgb * a, a);
            }
            ENDHLSL
        }
    }

    FallBack "Unlit/Transparent"
}