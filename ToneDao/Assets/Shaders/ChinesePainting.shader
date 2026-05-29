// Шейдер постобработки "Китайская тушевая живопись" (水墨画)
// Подключается как ScriptableRendererFeature в URP Renderer.
// Автор эффекта: ToneDAO project, 2026.
//
// Эффекты:
//   1. Sobel edge detection  → чернильные контуры
//   2. Ink wash colour map   → тона тушь + рисовая бумага
//   3. Рисовая бумага        → волокна + зернистость (процедурно)
//   4. Виньетка              → потемнение по краям как на старой бумаге

// Shader "Custom/ChinesePainting"
// {
//     SubShader
//     {
//         Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
//         ZWrite Off
//         ZTest Always
//         Cull Off
//         Blend Off

//         Pass
//         {
//             Name "ChinesePainting"

//             HLSLPROGRAM
//             #pragma vertex   Vert
//             #pragma fragment Frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

//             // ─── Uniforms (устанавливаются из C#) ──────────────────────────

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _InkColor;           // цвет туши
//                 float4 _PaperColor;         // цвет бумаги
//                 float  _OutlineThickness;   // толщина контура в пикселях
//                 float  _OutlineStrength;    // сила контура [0-1]
//                 float  _DesaturationAmount; // обесцвечивание [0-1]
//                 float  _PaperGrain;         // зернистость бумаги [0-1]
//                 float  _InkWashBlend;       // сила ink wash поверх цвета [0-1]
//             CBUFFER_END

//             // ─── Утилиты ───────────────────────────────────────────────────

//             float Lum(half3 c)
//             {
//                 return dot(c, half3(0.299h, 0.587h, 0.114h));
//             }

//             // Быстрый хэш без тригонометрии
//             float Hash(float2 p)
//             {
//                 p = frac(p * float2(127.34, 311.72));
//                 p += dot(p, p + 45.32);
//                 return frac(p.x * p.y);
//             }

//             // Билинейный value-noise для волокон бумаги
//             float ValueNoise(float2 p)
//             {
//                 float2 i = floor(p);
//                 float2 f = frac(p);
//                 f = f * f * (3.0 - 2.0 * f);
//                 float a = Hash(i);
//                 float b = Hash(i + float2(1, 0));
//                 float c = Hash(i + float2(0, 1));
//                 float d = Hash(i + float2(1, 1));
//                 return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
//             }

//             // Текстура рисовой бумаги: горизонтальные волокна + шум
//             float PaperTexture(float2 uv)
//             {
//                 // Волокна разного масштаба
//                 float fibers  = ValueNoise(uv * float2(1.0,  60.0)) * 0.50;
//                       fibers += ValueNoise(uv * float2(1.0, 120.0)) * 0.30;
//                       fibers += ValueNoise(uv * float2(1.0, 240.0)) * 0.20;
//                 // Зерно
//                 float grain   = Hash(uv * 1024.0) * 2.0 - 1.0;
//                 return fibers * 0.6 + grain * 0.4;
//             }

//             // ─── Фрагментный шейдер ────────────────────────────────────────

//             half4 Frag(Varyings input) : SV_Target
//             {
//                 float2 uv  = input.texcoord;

//                 // texelSize уже доступен из Blit.hlsl как _BlitTexture_TexelSize
//                 float2 ts  = _BlitTexture_TexelSize.xy * _OutlineThickness;

//                 // ── Выборки соседей (для Собеля 3×3) ──────────────────────
//                 half4 tl = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-ts.x,  ts.y));
//                 half4 tm = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0,     ts.y));
//                 half4 tr = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( ts.x,  ts.y));
//                 half4 ml = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-ts.x,  0   ));
//                 half4 col= SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
//                 half4 mr = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( ts.x,  0   ));
//                 half4 bl = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-ts.x, -ts.y));
//                 half4 bm = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0,    -ts.y));
//                 half4 br = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( ts.x, -ts.y));

//                 // ── Sobel edge detection ───────────────────────────────────
//                 float ltl = Lum(tl.rgb), ltm = Lum(tm.rgb), ltr = Lum(tr.rgb);
//                 float lml = Lum(ml.rgb),                     lmr = Lum(mr.rgb);
//                 float lbl = Lum(bl.rgb), lbm = Lum(bm.rgb), lbr = Lum(br.rgb);

//                 float gx = -ltl - 2*lml - lbl + ltr + 2*lmr + lbr;
//                 float gy = -ltl - 2*ltm - ltr + lbl + 2*lbm + lbr;
//                 float edge = saturate(sqrt(gx*gx + gy*gy) * 4.0);

//                 // ── Ink Wash цветовое преобразование ──────────────────────
//                 float lum = Lum(col.rgb);

//                 // Обесцвечивание
//                 half3 desatCol = lerp(col.rgb, half3(lum, lum, lum), _DesaturationAmount);

//                 // Маппинг яркости в диапазон тушь→бумага.
//                 // Shadow lift: тени никогда не уходят в чистую чёрную тушь —
//                 // минимальный t=0.18 даёт мягкий серо-синий вместо абсолютного чёрного.
//                 float t = pow(saturate(lum * 1.05), 0.9);
//                 t = t * 0.82 + 0.18;              // [0..1] → [0.18..1.0]
//                 half3 inkWash = lerp(_InkColor.rgb, _PaperColor.rgb, t);

//                 // Смешиваем обесцвеченный оригинал с ink wash
//                 half3 paintCol = lerp(desatCol * _PaperColor.rgb, inkWash, _InkWashBlend);

//                 // ── Текстура рисовой бумаги ───────────────────────────────
//                 // UV масштабируем к ~512 условных единицам
//                 float2 paperUV = uv / _BlitTexture_TexelSize.xy / 512.0;
//                 float  paper   = PaperTexture(paperUV);
//                 paintCol += paper * _PaperGrain * 0.06;

//                 // ── Контурные линии (тушь поверх всего) ───────────────────
//                 // smoothstep даёт плавный переход без пиксельного шума на слабых краях.
//                 // Диапазон [0.08, 0.55]: тихие переходы не рисуются, чёткие — полная тушь.
//                 float inkLine = smoothstep(0.08, 0.55, edge) * _OutlineStrength;
//                 paintCol = lerp(paintCol, _InkColor.rgb, inkLine);

//                 // ── Виньетка: края бумаги темнее ──────────────────────────
//                 float2 vigUV = uv * 2.0 - 1.0;
//                 float  vig   = 1.0 - saturate(dot(vigUV * 0.5, vigUV * 0.5));
//                 vig = pow(vig, 0.4);   // мягкая виньетка
//                 paintCol *= lerp(0.75, 1.0, vig);

//                 return half4(paintCol, 1.0);
//             }
//             ENDHLSL
//         }
//     }
// }

Shader "Custom/ChineseInkPainting"
{
    Properties
    {
        _InkColor ("Ink Color", Color) = (0.05,0.05,0.05,1)
        _PaperColor ("Paper Color", Color) = (0.95,0.92,0.85,1)

        _OutlineThickness ("Outline Thickness", Float) = 1
        _OutlineStrength ("Outline Strength", Float) = 1

        _PosterLevels ("Poster Levels", Float) = 5

        _DesaturationAmount ("Desaturation", Range(0,1)) = 0.8

        _PaperGrain ("Paper Grain", Range(0,1)) = 0.5

        _InkWashBlend ("Ink Wash Blend", Range(0,1)) = 0.85

        _BleedStrength ("Ink Bleed", Range(0,1)) = 0.35

        [Header(Glow Preservation)]
        _GlowSatThreshold ("Glow Sat Threshold", Range(0,1)) = 0.35
        _GlowLumThreshold ("Glow Lum Threshold", Range(0,1)) = 0.55
        _GlowPreserve     ("Glow Preserve Strength", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ChineseInk"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)

            float4 _InkColor;
            float4 _PaperColor;

            float _OutlineThickness;
            float _OutlineStrength;

            float _PosterLevels;

            float _DesaturationAmount;

            float _PaperGrain;

            float _InkWashBlend;

            float _BleedStrength;

            float _GlowSatThreshold;
            float _GlowLumThreshold;
            float _GlowPreserve;

            CBUFFER_END

            float Lum(float3 c)
            {
                return dot(c, float3(0.299,0.587,0.114));
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34,456.21));
                p += dot(p,p+34.45);
                return frac(p.x*p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f*f*(3-2*f);

                float a = Hash(i);
                float b = Hash(i+float2(1,0));
                float c = Hash(i+float2(0,1));
                float d = Hash(i+float2(1,1));

                return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
            }

            float PaperTexture(float2 uv)
            {
                float fibers =
                    ValueNoise(uv*float2(1,60))*0.5 +
                    ValueNoise(uv*float2(1,120))*0.3 +
                    ValueNoise(uv*float2(1,240))*0.2;

                float grain = Hash(uv*2048)*2-1;

                return fibers*0.7 + grain*0.3;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float2 ts = _BlitTexture_TexelSize.xy * _OutlineThickness;

                half4 tl = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(-ts.x,ts.y));
                half4 tm = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(0,ts.y));
                half4 tr = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(ts.x,ts.y));

                half4 ml = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(-ts.x,0));
                half4 col= SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 mr = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(ts.x,0));

                half4 bl = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(-ts.x,-ts.y));
                half4 bm = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(0,-ts.y));
                half4 br = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv+float2(ts.x,-ts.y));

                // Сохраняем чистый оригинал ДО размытия — нужен для glow-маски
                float3 colUnblurred = col.rgb;

                float3 blur =
                      tl.rgb*0.0625
                    + tm.rgb*0.125
                    + tr.rgb*0.0625
                    + ml.rgb*0.125
                    + col.rgb*0.25
                    + mr.rgb*0.125
                    + bl.rgb*0.0625
                    + bm.rgb*0.125
                    + br.rgb*0.0625;

                col.rgb = lerp(col.rgb, blur, _BleedStrength);

                // ── Blurred Sobel: Sobel по средним строк/столбцов ─────────
                // Усредняем строки и столбцы ДО вычисления градиента.
                // Одиночные пиксельные переходы внутри модели (от постеризации)
                // усредняются в ~0. Силуэт (весь столбец/строка разные) остаётся.
                float3 rowTop = (tl.rgb + tm.rgb + tr.rgb) * 0.3333;
                float3 rowBot = (bl.rgb + bm.rgb + br.rgb) * 0.3333;
                float3 colLef = (tl.rgb + ml.rgb + bl.rgb) * 0.3333;
                float3 colRig = (tr.rgb + mr.rgb + br.rgb) * 0.3333;

                float gx = Lum(colRig - colLef);
                float gy = Lum(rowTop - rowBot);

                float edge = length(float2(gx, gy));
                edge = saturate(edge * 2.5);

                float lum = Lum(col.rgb);

                lum = floor(lum*_PosterLevels)/_PosterLevels;

                float3 desatCol = lerp(col.rgb,float3(lum,lum,lum),_DesaturationAmount);

                float t = smoothstep(0.05,0.9,lum);
                t = pow(t,1.2);

                float3 inkWash = lerp(_InkColor.rgb,_PaperColor.rgb,t);

                float3 paintCol = lerp(desatCol*_PaperColor.rgb,inkWash,_InkWashBlend);

                float2 paperUV = uv/_BlitTexture_TexelSize.xy/512;

                float paper = PaperTexture(paperUV);

                paintCol *= 1.0 + paper*_PaperGrain*0.12;

                // Высокий порог: только сильные силуэтные края (≥ 0.3 после * 2.5)
                float inkLine = smoothstep(0.28, 0.65, edge) * _OutlineStrength;

                // Живой мазок — только там, где контур уже есть (inkLine > 0)
                inkLine *= lerp(0.85, 1.15, Hash(uv * 600));

                paintCol = lerp(paintCol, _InkColor.rgb, inkLine);

                float2 vigUV = uv*2-1;

                float vig = 1-saturate(dot(vigUV*0.5,vigUV*0.5));

                vig = pow(vig,0.4);

                paintCol *= lerp(0.75,1,vig);

                // ── Glow preservation: яркие насыщенные пиксели (частицы) ────
                // Используем colUnblurred — оригинал ДО размытия,
                // чтобы маленькие частицы не усреднялись с тёмным фоном.
                float maxC = max(colUnblurred.r, max(colUnblurred.g, colUnblurred.b));
                float minC = min(colUnblurred.r, min(colUnblurred.g, colUnblurred.b));
                float sat  = (maxC > 0.001) ? (maxC - minC) / maxC : 0;
                float rawLum = Lum(colUnblurred);

                float glowMask = saturate((sat    - _GlowSatThreshold) / 0.15)
                               * saturate((rawLum - _GlowLumThreshold) / 0.2);
                glowMask = smoothstep(0, 1, glowMask) * _GlowPreserve;

                // Восстанавливаем оригинальный яркий цвет (не размытый)
                paintCol = lerp(paintCol, colUnblurred, glowMask);

                return float4(paintCol,1);
            }

            ENDHLSL
        }
    }
}