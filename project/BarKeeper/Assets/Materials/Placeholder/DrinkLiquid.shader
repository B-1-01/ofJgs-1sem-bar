Shader "Barkeeper/DrinkLiquid"
{
    Properties
    {
        _LiquidColor ("Liquid Color (Mixed)", Color) = (0.95, 0.65, 0.12, 0.85)
        _FoamColor ("Foam Color", Color) = (0.98, 0.98, 0.92, 1.0)
        _LiquidAmount ("Liquid Fill (0-1)", Range(0, 1)) = 0.0
        _FoamAmount ("Foam Fill (0-1)", Range(0, 1)) = 0.0
        _MixFactor ("Mix Factor (0=Layered, 1=Mixed)", Range(0, 1)) = 0.0
        _LayerCount ("Layer Count", Float) = 0.0

        _LayerColor0 ("Layer 0 Color", Color) = (0.95, 0.65, 0.12, 0.85)
        _LayerColor1 ("Layer 1 Color", Color) = (0.18, 0.07, 0.03, 0.94)
        _LayerColor2 ("Layer 2 Color", Color) = (0.88, 0.94, 0.98, 0.45)
        _LayerColor3 ("Layer 3 Color", Color) = (0.45, 0.88, 0.18, 0.80)
        _LayerColor4 ("Layer 4 Color", Color) = (0.96, 0.84, 0.42, 0.75)
        _LayerColor5 ("Layer 5 Color", Color) = (0.85, 0.96, 0.92, 0.48)
        _LayerColor6 ("Layer 6 Color", Color) = (0.95, 0.92, 0.80, 0.65)
        _LayerColor7 ("Layer 7 Color", Color) = (0.82, 0.95, 1.00, 0.55)

        _LayerTops0 ("Layer Tops 0-3", Vector) = (0, 0, 0, 0)
        _LayerTops1 ("Layer Tops 4-7", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off // Permite ver o líquido de qualquer ângulo (inclusive por dentro do copo)
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float heightNormalized : TEXCOORD0; // Altura normalizada 0 (fundo) a 1 (topo)
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _LiquidColor;
                half4 _FoamColor;
                float _LiquidAmount;
                float _FoamAmount;
                float _MixFactor;
                float _LayerCount;
                half4 _LayerColor0;
                half4 _LayerColor1;
                half4 _LayerColor2;
                half4 _LayerColor3;
                half4 _LayerColor4;
                half4 _LayerColor5;
                half4 _LayerColor6;
                half4 _LayerColor7;
                float4 _LayerTops0;
                float4 _LayerTops1;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // O cilindro padrão do Unity tem Y de -1 (fundo) a +1 (topo)
                // Convertemos para 0.0 (fundo) até 1.0 (topo)
                output.heightNormalized = (input.positionOS.y + 1.0) * 0.5;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float h = input.heightNormalized;
                float totalFill = _LiquidAmount + _FoamAmount;

                // 1. ZONA DO AR (Vazio): se o copo estiver vazio ou o pixel estiver acima do total, descarta
                if (totalFill <= 0.001 || h > totalFill)
                {
                    discard;
                }

                // 2. ZONA DA ESPUMA: se nao houver liquido ou se o pixel estiver acima do liquido
                if (_LiquidAmount <= 0.001 || h > _LiquidAmount)
                {
                    return _FoamColor;
                }

                // 3. ZONA DO LÍQUIDO: camadas por ingrediente ou cor misturada
                if (_LayerCount <= 1.5 || _MixFactor >= 0.999)
                {
                    return _LiquidColor;
                }

                const float edgeSoftness = 0.008;
                half4 layeredColor = _LayerColor0;

                if (_LayerCount > 1.5)
                    layeredColor = lerp(layeredColor, _LayerColor1, smoothstep(_LayerTops0.x - edgeSoftness, _LayerTops0.x + edgeSoftness, h));
                if (_LayerCount > 2.5)
                    layeredColor = lerp(layeredColor, _LayerColor2, smoothstep(_LayerTops0.y - edgeSoftness, _LayerTops0.y + edgeSoftness, h));
                if (_LayerCount > 3.5)
                    layeredColor = lerp(layeredColor, _LayerColor3, smoothstep(_LayerTops0.z - edgeSoftness, _LayerTops0.z + edgeSoftness, h));
                if (_LayerCount > 4.5)
                    layeredColor = lerp(layeredColor, _LayerColor4, smoothstep(_LayerTops0.w - edgeSoftness, _LayerTops0.w + edgeSoftness, h));
                if (_LayerCount > 5.5)
                    layeredColor = lerp(layeredColor, _LayerColor5, smoothstep(_LayerTops1.x - edgeSoftness, _LayerTops1.x + edgeSoftness, h));
                if (_LayerCount > 6.5)
                    layeredColor = lerp(layeredColor, _LayerColor6, smoothstep(_LayerTops1.y - edgeSoftness, _LayerTops1.y + edgeSoftness, h));
                if (_LayerCount > 7.5)
                    layeredColor = lerp(layeredColor, _LayerColor7, smoothstep(_LayerTops1.z - edgeSoftness, _LayerTops1.z + edgeSoftness, h));

                return lerp(layeredColor, _LiquidColor, saturate(_MixFactor));
            }
            ENDHLSL
        }
    }
}
