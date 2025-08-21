Shader "Indicators/ArrowTextureNineSlice"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Tint    ("Tint Color", Color) = (1,1,1,1)

        // Gradiente opcional ao longo do comprimento (U)
        _UseGradient ("Use Gradient", Float) = 0
        _GradA ("Gradient Start", Color) = (1,1,1,1)
        _GradB ("Gradient End",   Color) = (0.6,0.9,1,1)
        _GradPower ("Gradient Power", Range(0.1,3)) = 1

        // 9-slice no eixo X: pixels preservados na cauda e na cabeça
        _TailPixels ("Tail (px)", Float) = 8
        _HeadPixels ("Head (px)", Float) = 24
        _TexWidthPx ("Texture Width (px)", Float) = 128

        // Fator de estiramento (escala X do objeto relativa à base)
        _StretchX ("Stretch X", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _UseGradient;
                float4 _GradA;
                float4 _GradB;
                float  _GradPower;

                float  _TailPixels;
                float  _HeadPixels;
                float  _TexWidthPx;
                float  _StretchX;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // Remapeia UV.x para preservar extremidades (9-slice horizontal)
            float remapNineSliceU(float u, float stretch, float tailPx, float headPx, float texW)
            {
                // frações normalizadas das bordas
                float tailN = (texW > 0.0) ? tailPx / texW : 0.0;
                float headN = (texW > 0.0) ? headPx / texW : 0.0;
                tailN = saturate(tailN);
                headN = saturate(headN);
                float coreN = max(1e-5, 1.0 - tailN - headN);

                // Para stretch <= 1, deixamos passar (encolhimento pequeno ainda fica decente)
                if (stretch <= 1.0001)
                    return u;

                // Zonas: [0..tailN] | [tailN..1-headN] | [1-headN..1]
                if (u < tailN)
                    return u;

                if (u > 1.0 - headN)
                    return u;

                // Região central: comprimimos a amostragem em 1/stretch para compensar a escala do objeto
                float uLocal = (u - tailN) / coreN;      // 0..1 no miolo
                float uCompressed = uLocal / stretch;    // encolhe no UV
                // Reinsere no espaço 0..1 com o miolo comprimido e bordas originais
                float coreNCompressed = coreN / stretch;
                float uPrime = tailN + uCompressed * coreNCompressed;

                // Mantém as bordas do UV intactas, só o miolo é comprimido
                return uPrime;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Remapeia apenas o eixo X:
                float u = remapNineSliceU(IN.uv.x, _StretchX, _TailPixels, _HeadPixels, _TexWidthPx);
                float2 uvRemap = float2(u, IN.uv.y);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvRemap);

                // Tint multiplicativo
                half4 col = tex * _Tint * IN.color;

                // Gradiente opcional ao longo do comprimento original (usa IN.uv.x para "do início ao fim")
                if (_UseGradient > 0.5)
                {
                    float t = saturate(pow(IN.uv.x, _GradPower));
                    half3 g = lerp(_GradA.rgb, _GradB.rgb, t);
                    col.rgb *= g;
                }

                // Premultiplica por alpha da textura para recorte limpo
                col.rgb *= col.a;
                return col;
            }
            ENDHLSL
        }
    }
}
