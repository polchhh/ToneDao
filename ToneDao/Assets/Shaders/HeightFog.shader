Shader "Custom/HeightFog"
{
    Properties
    {
        _FogColor      ("Fog Color",       Color)        = (0.92, 0.89, 0.84, 1)
        _FogStartY     ("Fog Start Y",     Float)        = 2.0
        _FogEndY       ("Fog End Y",       Float)        = -4.0
        _FogDensity    ("Fog Density",     Range(0,1))   = 0.85
        _FogSoftness   ("Fog Softness",    Range(0,1))   = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "HeightFog"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float  _FogStartY;
                float  _FogEndY;
                float  _FogDensity;
                float  _FogSoftness;
            CBUFFER_END

            // Восстанавливаем мировую позицию из глубины
            float3 ReconstructWorldPos(float2 uv, float rawDepth)
            {
                float4 ndc = float4(uv * 2.0 - 1.0, rawDepth, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    ndc.y = -ndc.y;
                #endif
                float4 worldPos = mul(UNITY_MATRIX_I_VP, ndc);
                return worldPos.xyz / worldPos.w;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float  rawDepth = SampleSceneDepth(uv);
                float3 worldPos = ReconstructWorldPos(uv, rawDepth);

                // Скип скайбокса (глубина = 1 = бесконечность)
                if (rawDepth >= 0.9999)
                {
                    // Для скайбокса тоже добавим лёгкий туман у горизонта
                    float4 orig = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                    return orig;
                }

                // Высотная маска: 1 = полный туман (низко), 0 = нет тумана (высоко)
                float fogT = saturate(
                    (worldPos.y - _FogStartY) / (_FogEndY - _FogStartY)
                );

                // Сглаживание через smoothstep
                fogT = smoothstep(0.0, 1.0 - _FogSoftness, fogT);
                fogT *= _FogDensity;

                float4 orig = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 result = lerp(orig.rgb, _FogColor.rgb, fogT);

                return float4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
