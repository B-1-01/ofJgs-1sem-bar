Shader "Barkeeper/DrinkLiquid"
{
    Properties
    {
        _LiquidColor ("Liquid Color", Color) = (0.95, 0.65, 0.12, 0.85) // Âmbar cerveja
        _FoamColor ("Foam Color", Color) = (0.98, 0.98, 0.92, 1.0)     // Espuma
        _LiquidAmount ("Liquid Fill (0-1)", Range(0, 1)) = 0.0
        _FoamAmount ("Foam Fill (0-1)", Range(0, 1)) = 0.0
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

                // 3. ZONA DA CERVEJA: do fundo ate _LiquidAmount
                return _LiquidColor;
            }
            ENDHLSL
        }
    }
}