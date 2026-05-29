Shader "Custom/ChineseSkybox"
{
    Properties
    {
        [Header(Sky Gradient)]
        _ZenithColor    ("Zenith Color",    Color) = (0.55, 0.70, 0.85, 1)
        _MidColor       ("Mid Color",       Color) = (0.85, 0.80, 0.75, 1)
        _HorizonColor   ("Horizon Color",   Color) = (0.95, 0.75, 0.55, 1)
        _HorizonWidth   ("Horizon Width",   Range(0.01, 0.5)) = 0.12

        [Header(Sun)]
        _SunColor       ("Sun Color",       Color) = (1.0, 0.90, 0.65, 1)
        _SunGlowColor   ("Sun Glow Color",  Color) = (1.0, 0.60, 0.25, 1)
        _SunDir         ("Sun Direction",   Vector) = (0.3, 0.6, 0.5, 0)
        _SunSize        ("Sun Size",        Range(0.001, 0.1)) = 0.015
        _SunGlowSize    ("Sun Glow Size",   Range(0.01, 0.8))  = 0.35

        [Header(Ink Clouds)]
        _CloudColor     ("Cloud Color",     Color) = (0.92, 0.88, 0.82, 1)
        _CloudDark      ("Cloud Dark",      Color) = (0.60, 0.55, 0.50, 1)
        _CloudScale     ("Cloud Scale",     Range(1, 10)) = 4.0
        _CloudAmount    ("Cloud Amount",    Range(0, 1))  = 0.55
        _CloudSoftness  ("Cloud Softness",  Range(0.01, 0.5)) = 0.18

        [Header(Mist)]
        _MistColor      ("Mist Color",      Color) = (0.90, 0.87, 0.82, 1)
        _MistAmount     ("Mist Amount",     Range(0, 1)) = 0.45
        _MistHeight     ("Mist Height",     Range(0, 0.4)) = 0.08

        [Header(Ground)]
        _GroundColor    ("Ground Color",    Color) = (0.75, 0.70, 0.62, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _MidColor;
                float4 _HorizonColor;
                float  _HorizonWidth;

                float4 _SunColor;
                float4 _SunGlowColor;
                float4 _SunDir;
                float  _SunSize;
                float  _SunGlowSize;

                float4 _CloudColor;
                float4 _CloudDark;
                float  _CloudScale;
                float  _CloudAmount;
                float  _CloudSoftness;

                float4 _MistColor;
                float  _MistAmount;
                float  _MistHeight;

                float4 _GroundColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir        = IN.positionOS.xyz;
                return OUT;
            }

            // ── Утилиты шума ─────────────────────────────────────────────────

            float Hash(float2 p)
            {
                p = frac(p * float2(127.34, 311.72));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(i), Hash(i + float2(1,0)), f.x),
                    lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), f.x),
                    f.y);
            }

            // Многооктавный шум для облаков в стиле туши
            float CloudNoise(float2 uv)
            {
                float v  = ValueNoise(uv)         * 0.500;
                      v += ValueNoise(uv * 2.1)   * 0.250;
                      v += ValueNoise(uv * 4.3)   * 0.125;
                      v += ValueNoise(uv * 8.7)   * 0.063;
                      v += ValueNoise(uv * 17.3)  * 0.031;
                return v;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);
                float  y   = dir.y; // -1 (низ) .. +1 (верх)

                // ── Градиент неба ──────────────────────────────────────────
                // Разбиваем на три зоны: зенит / средина / горизонт
                float tZenith  = saturate(y / 0.6);
                float tHorizon = saturate(1.0 - abs(y) / _HorizonWidth);
                float tGround  = saturate(-y / 0.15);

                float3 skyCol = lerp(_MidColor.rgb, _ZenithColor.rgb,
                                     smoothstep(0, 1, tZenith));
                skyCol = lerp(skyCol, _HorizonColor.rgb,
                              smoothstep(0, 1, tHorizon));
                skyCol = lerp(skyCol, _GroundColor.rgb, tGround);

                // ── Солнце ─────────────────────────────────────────────────
                float3 sunDir = normalize(_SunDir.xyz);
                float  sd     = dot(dir, sunDir);

                // Большой мягкий ореол
                float glow = saturate((sd - (1.0 - _SunGlowSize)) / _SunGlowSize);
                glow = pow(glow, 2.5);
                skyCol = lerp(skyCol, _SunGlowColor.rgb, glow * 0.6);

                // Диск солнца
                float sunMask = saturate((sd - (1.0 - _SunSize * 0.5)) / (_SunSize * 0.5));
                sunMask = smoothstep(0.0, 1.0, sunMask);
                skyCol = lerp(skyCol, _SunColor.rgb, sunMask);

                // ── Облака в стиле тушевой живописи ───────────────────────
                // Проецируем направление на плоскость для UV облаков
                float cloudMask = smoothstep(-0.05, 0.5, y);  // только выше горизонта
                if (cloudMask > 0.01)
                {
                    float2 cloudUV = float2(
                        atan2(dir.x, dir.z) / (2.0 * 3.14159),
                        y * 0.5 + 0.5) * _CloudScale;

                    float cn = CloudNoise(cloudUV);

                    // Постеризуем шум — имитация мазков туши
                    float levels = 4.0;
                    float cnPost = floor(cn * levels) / levels;
                    cn = lerp(cn, cnPost, 0.4);

                    float cloud = smoothstep(
                        1.0 - _CloudAmount,
                        1.0 - _CloudAmount + _CloudSoftness,
                        cn);

                    float3 cloudCol = lerp(_CloudDark.rgb, _CloudColor.rgb,
                                           smoothstep(0, 1, cn));

                    skyCol = lerp(skyCol, cloudCol, cloud * cloudMask * 0.85);
                }

                // ── Туман у горизонта ──────────────────────────────────────
                float mistMask = smoothstep(_MistHeight, 0.0, abs(y)) * _MistAmount;
                skyCol = lerp(skyCol, _MistColor.rgb, mistMask);

                return half4(skyCol, 1.0);
            }
            ENDHLSL
        }
    }
}
