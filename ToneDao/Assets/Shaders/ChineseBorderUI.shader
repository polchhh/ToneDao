Shader "Custom/ChineseBorderUI"
{
    Properties
    {
        [Header(Colors)]
        _BorderColor   ("Border Color",      Color)        = (0.8, 0.6, 0.1, 1)
        _FillColor     ("Fill Color",        Color)        = (0.0, 0.0, 0.0, 0.35)
        _GlowColor     ("Glow Color",        Color)        = (1.0, 0.85, 0.3, 0.4)

        [Header(Border)]
        _BorderWidth   ("Border Width",      Range(0.005, 0.1)) = 0.025
        _CornerSize    ("Corner Ornament",   Range(0.05,  0.4)) = 0.18
        _GlowWidth     ("Glow Width",        Range(0.0,   0.1)) = 0.02

        [Header(Animation)]
        _PulseSpeed    ("Pulse Speed",       Range(0, 5))   = 1.5
        _PulseAmount   ("Pulse Amount",      Range(0, 0.5)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Overlay"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BorderColor;
                float4 _FillColor;
                float4 _GlowColor;
                float  _BorderWidth;
                float  _CornerSize;
                float  _GlowWidth;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.pos = TransformObjectToHClip(IN.pos.xyz);
                OUT.uv  = IN.uv;
                return OUT;
            }

            // Расстояние до прямоугольной рамки
            float RectBorder(float2 uv, float width)
            {
                float2 d = abs(uv - 0.5) - (0.5 - width);
                return max(d.x, d.y);
            }

            // Угловой орнамент — Г-образные линии в каждом углу
            float CornerOrnament(float2 uv, float size, float width)
            {
                // Зеркалим UV в первый квадрант
                float2 c = abs(uv - 0.5) * 2.0; // 0..1 от центра

                float inCorner = step(1.0 - size, c.x) * step(1.0 - size, c.y);
                if (inCorner < 0.5) return 0.0;

                // Локальные координаты внутри угла
                float2 lc = (c - (1.0 - size)) / size;

                // Горизонтальная черта угла
                float hLine = step(1.0 - width * 3.0 / size, lc.y)
                            * step(lc.x, 1.0);

                // Вертикальная черта угла
                float vLine = step(1.0 - width * 3.0 / size, lc.x)
                            * step(lc.y, 1.0);

                // Маленький квадрат на самом кончике угла
                float dot = step(0.75, lc.x) * step(0.75, lc.y);

                return saturate(hLine + vLine + dot);
            }

            // Средняя засечка на каждой стороне (декоративный элемент)
            float MidOrnament(float2 uv, float bw)
            {
                float2 c  = abs(uv - 0.5) * 2.0;
                float  mw = bw * 4.0;

                // Горизонтальные середины (верх/низ)
                float hMid = step(1.0 - bw * 5.0, c.y)
                           * step(c.x, mw)
                           * step(1.0 - bw * 8.0, c.y);

                // Вертикальные середины (лево/право)
                float vMid = step(1.0 - bw * 5.0, c.x)
                           * step(c.y, mw)
                           * step(1.0 - bw * 8.0, c.x);

                return saturate(hMid + vMid);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float  t  = _Time.y;

                // Пульсация яркости
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;

                // ── Основная рамка ─────────────────────────────────────────
                float border = RectBorder(uv, _BorderWidth);
                float isBorder = step(0.0, border) * step(border, _BorderWidth * 0.5);
                // Сглаживаем края
                float borderMask = smoothstep(_BorderWidth * 0.5,  0.0, border)
                                 * smoothstep(-_BorderWidth * 0.5, 0.0, -border + _BorderWidth * 0.001);
                borderMask = saturate(borderMask);

                // ── Угловые орнаменты ──────────────────────────────────────
                float corners = CornerOrnament(uv, _CornerSize, _BorderWidth);

                // ── Средние засечки ────────────────────────────────────────
                float mids = MidOrnament(uv, _BorderWidth);

                // ── Свечение за рамкой ─────────────────────────────────────
                float2 d2 = abs(uv - 0.5) - 0.5;
                float  dist = max(d2.x, d2.y);
                float  glow = smoothstep(0.0, _GlowWidth, -dist)
                            * smoothstep(-_GlowWidth * 2.0, 0.0, dist);

                // ── Внутренняя область ─────────────────────────────────────
                float inside = step(max(abs(uv.x - 0.5), abs(uv.y - 0.5)), 0.5 - _BorderWidth);

                // ── Сборка ─────────────────────────────────────────────────
                float  decorMask = saturate(borderMask + corners + mids);
                float4 col       = float4(0, 0, 0, 0);

                // Внутренняя заливка
                col = lerp(col, _FillColor, inside * _FillColor.a);

                // Свечение
                col.rgb = lerp(col.rgb, _GlowColor.rgb, glow * _GlowColor.a * pulse);
                col.a   = max(col.a, glow * _GlowColor.a * 0.5);

                // Рамка и орнаменты
                col.rgb = lerp(col.rgb, _BorderColor.rgb * pulse, decorMask);
                col.a   = max(col.a, decorMask * _BorderColor.a);

                return col;
            }
            ENDHLSL
        }
    }
}
