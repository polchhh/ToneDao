// Full-Screen Outline Shader — Depth + Normals
// Реализация по туториалу: обводка только по геометрии сцены.
// Не реагирует на цветовые переходы внутри объектов.
//
// ТРЕБОВАНИЯ:
//   • В PC_RPAsset (URP Asset) включить "Depth Texture"
//   • В PC_Renderer добавить "Full Screen Pass Renderer Feature"
//     → Material = FullScreenOutlineMat (из этого шейдера)
//     → Injection Point = Before Rendering Post Processing
//   • Если используется совместно с ChinesePainting — добавить ПОСЛЕ него.

Shader "Outline/FullScreenOutline"
{
    Properties
    {
        _OutlineColor      ("Outline Color",        Color)        = (0, 0, 0, 1)
        _OutlineThickness  ("Outline Thickness",    Range(0, 10)) = 1
        _DepthSensitivity  ("Depth Sensitivity",    Range(0, 50)) = 10
        _NormalSensitivity ("Normal Sensitivity",   Range(0, 10)) = 1
        _EdgeThreshold     ("Edge Threshold",       Range(0,  1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OutlinePass"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ─── Uniforms ─────────────────────────────────────────────────

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _DepthSensitivity;
                float  _NormalSensitivity;
                float  _EdgeThreshold;
            CBUFFER_END

            // ─── Восстановление мировых координат из глубины ──────────────

            float3 ReconstructWorldPos(float2 uv, float depth)
            {
                float4 clipPos = float4(uv * 2.0 - 1.0, depth, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    clipPos.y = -clipPos.y;
                #endif
                float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
                return worldPos.xyz / worldPos.w;
            }

            // ─── Оценка нормали по 5 соседним значениям глубины ──────────

            float3 ReconstructNormalFromDepth(float2 uv, float2 texelSize)
            {
                float depthC = SampleSceneDepth(uv);
                float depthL = SampleSceneDepth(uv + float2(-texelSize.x,  0));
                float depthR = SampleSceneDepth(uv + float2( texelSize.x,  0));
                float depthU = SampleSceneDepth(uv + float2( 0,  texelSize.y));
                float depthD = SampleSceneDepth(uv + float2( 0, -texelSize.y));

                float3 posL = ReconstructWorldPos(uv + float2(-texelSize.x,  0), depthL);
                float3 posR = ReconstructWorldPos(uv + float2( texelSize.x,  0), depthR);
                float3 posU = ReconstructWorldPos(uv + float2( 0,  texelSize.y), depthU);
                float3 posD = ReconstructWorldPos(uv + float2( 0, -texelSize.y), depthD);

                float3 dx = (posR - posL) * 0.5;
                float3 dy = (posU - posD) * 0.5;
                return normalize(cross(dy, dx));
            }

            // ─── Sobel по линейной глубине (силуэт объектов) ─────────────

            float DetectDepthEdge(float2 uv, float2 texelSize)
            {
                // Ядра Собеля 3×3
                const float kX[9] = { -1, 0, 1,  -2, 0, 2,  -1, 0, 1 };
                const float kY[9] = { -1,-2,-1,   0, 0, 0,   1, 2, 1 };

                float sobelX = 0, sobelY = 0;
                int   idx = 0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 off   = float2(x, y) * texelSize;
                        float  depth = LinearEyeDepth(SampleSceneDepth(uv + off), _ZBufferParams);
                        sobelX += depth * kX[idx];
                        sobelY += depth * kY[idx];
                        idx++;
                    }
                }
                return sqrt(sobelX * sobelX + sobelY * sobelY);
            }

            // ─── Разница нормалей в 4 направлениях (изгибы, складки) ─────

            float DetectNormalEdge(float2 uv, float2 texelSize)
            {
                float3 nL = ReconstructNormalFromDepth(uv + float2(-texelSize.x, 0), texelSize);
                float3 nR = ReconstructNormalFromDepth(uv + float2( texelSize.x, 0), texelSize);
                float3 nU = ReconstructNormalFromDepth(uv + float2(0,  texelSize.y), texelSize);
                float3 nD = ReconstructNormalFromDepth(uv + float2(0, -texelSize.y), texelSize);

                float edgeX = length(nR - nL);
                float edgeY = length(nU - nD);
                return (edgeX + edgeY) * 0.5;
            }

            // ─── Фрагментный шейдер ───────────────────────────────────────

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float4 originalColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 texelSize = _OutlineThickness *
                                   float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);

                // Скип фона/неба (глубина → 1 = бесконечность)
                float centerDepth = SampleSceneDepth(uv);
                if (centerDepth >= 0.99999)
                    return originalColor;

                // Depth + Normal edges
                float depthEdge  = DetectDepthEdge (uv, texelSize) * _DepthSensitivity;
                float normalEdge = DetectNormalEdge(uv, texelSize) * _NormalSensitivity;

                float combinedEdge = max(depthEdge, normalEdge);

                // Плавный порог → нет шума у слабых переходов
                combinedEdge = smoothstep(_EdgeThreshold, _EdgeThreshold + 0.05, combinedEdge);
                combinedEdge = saturate(combinedEdge * 2.0);

                float3 finalColor = lerp(originalColor.rgb, _OutlineColor.rgb, combinedEdge);
                return float4(finalColor, originalColor.a);
            }

            ENDHLSL
        }
    }
}
