Shader "Custom/Leaf"
{
    Properties
    {
        [Header(Textures)]
        _BaseMap    ("Diffuse",     2D) = "white" {}
        _AlphaMap   ("Alpha Mask",  2D) = "white" {}
        _Cutoff     ("Alpha Cutoff", Range(0,1)) = 0.5
        _Color      ("Tint",        Color) = (1,1,1,1)

        [Header(Wind)]
        _WindStrength   ("Wind Strength",    Range(0, 0.5))  = 0.08
        _WindSpeed      ("Wind Speed",       Range(0, 5))    = 1.2
        _WindFrequency  ("Wind Frequency",   Range(0, 10))   = 2.0
        _WindDirection  ("Wind Direction",   Vector)         = (1, 0, 0.3, 0)
        _FlutterStrength("Flutter Strength", Range(0, 0.3))  = 0.04
        _FlutterSpeed   ("Flutter Speed",    Range(0, 20))   = 8.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
        }

        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AlphaMap_ST;
                float4 _Color;
                float  _Cutoff;

                float  _WindStrength;
                float  _WindSpeed;
                float  _WindFrequency;
                float4 _WindDirection;
                float  _FlutterStrength;
                float  _FlutterSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // vertex color — красный канал = маска ветра
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            // ── Анимация ветра ────────────────────────────────────────────────
            float3 ApplyWind(float3 posOS, float3 posWS, float windMask)
            {
                float t = _Time.y;

                // Фаза зависит от мировой позиции — разные ветки качаются не синхронно
                float phase = dot(posWS.xz, float2(1.7, 2.3));

                // Основное покачивание (медленное, большая амплитуда)
                float mainWave = sin(t * _WindSpeed + phase * _WindFrequency) * _WindStrength;

                // Дрожание листьев (быстрое, малая амплитуда)
                float flutter  = sin(t * _FlutterSpeed + phase * 5.1) * _FlutterStrength;
                flutter       += sin(t * _FlutterSpeed * 0.73 + phase * 3.7) * _FlutterStrength * 0.5;

                float3 windDir = normalize(_WindDirection.xyz);
                float3 offset  = windDir * (mainWave + flutter);

                // Вертикальное покачивание (листья немного поднимаются/опускаются)
                offset.y += sin(t * _WindSpeed * 0.8 + phase * 1.5) * _WindStrength * 0.3;

                return posOS + offset * windMask;
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                // vertex color.r — маска ветра (1 = двигается, 0 = закреплено)
                // Если vertex colors не настроены — используем высоту как маску (y > 0)
                float windMask = IN.color.r;

                float3 animatedPosOS = ApplyWind(IN.positionOS.xyz, posWS, windMask);

                OUT.positionWS = TransformObjectToWorld(animatedPosOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 diffuse = SAMPLE_TEXTURE2D(_BaseMap,  sampler_BaseMap,  IN.uv) * _Color;
                half  alpha   = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).r;
                clip(alpha - _Cutoff);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float NdotL   = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half3 lighting = mainLight.color * (NdotL * 0.8 + 0.2);

                half3 color = diffuse.rgb * lighting;
                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster с alpha clip ─────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AlphaMap_ST;
                float4 _Color;
                float  _Cutoff;
                float  _WindStrength;
                float  _WindSpeed;
                float  _WindFrequency;
                float4 _WindDirection;
                float  _FlutterStrength;
                float  _FlutterSpeed;
            CBUFFER_END

            float3 ApplyWindShadow(float3 posOS, float3 posWS, float windMask)
            {
                float t     = _Time.y;
                float phase = dot(posWS.xz, float2(1.7, 2.3));
                float wave  = sin(t * _WindSpeed + phase * _WindFrequency) * _WindStrength;
                float3 dir  = normalize(_WindDirection.xyz);
                return posOS + dir * wave * windMask;
            }

            struct ShadowIn  { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 norm : NORMAL; float4 col : COLOR; };
            struct ShadowOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            ShadowOut ShadowVert(ShadowIn IN)
            {
                ShadowOut OUT;
                float3 posWS  = TransformObjectToWorld(IN.pos.xyz);
                float3 posAnim = ApplyWindShadow(IN.pos.xyz, posWS, IN.col.r);
                float3 wPos   = TransformObjectToWorld(posAnim);
                float3 wNorm  = TransformObjectToWorldNormal(IN.norm);
                wPos = ApplyShadowBias(wPos, wNorm, normalize(_MainLightPosition.xyz));
                OUT.pos = TransformWorldToHClip(wPos);
                OUT.uv  = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(ShadowOut IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).r;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
