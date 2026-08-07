Shader "YGO/RainbowBorderShader"
{
    // Pinta el marco (la malla de _MainTex actúa solo como máscara de alfa)
    // con una "serpiente" arcoíris que gira alrededor del centro de la carta:
    // el matiz cambia según la posición angular sobre el borde y avanza solo
    // con el tiempo, dando la sensación de un color que recorre el contorno.
    Properties
    {
        _MainTex     ("Border Sprite (solo alfa)", 2D)            = "white" {}
        _Color       ("Tint",                      Color)         = (1,1,1,1)

        _Speed       ("Snake Speed",                Range(-3,3))  = 0.6
        _BandCount   ("Repeticiones alrededor",     Range(1,8))   = 2
        _Saturation  ("Saturation",                 Range(0,1))   = 0.9
        _Brightness  ("Brightness",                 Range(0,2))   = 1.0

        [Header(ModoCircuitoNeoKemet)]
        // 0 = serpiente arcoíris clásica | 1 = circuito duotono: aro dorado con
        // pulsos turquesa que recorren el marco como paquetes de datos.
        [Toggle] _Duotone ("Duotone Circuit Mode",  Float)        = 0
        _DuoColorA   ("Circuito (base oro)",        Color)        = (0.91, 0.72, 0.29, 1)
        _DuoColorB   ("Pulso (turquesa)",           Color)        = (0.22, 0.95, 0.86, 1)

        _StencilComp     ("Stencil Comparison",Float) = 8
        _Stencil         ("Stencil ID",        Float) = 0
        _StencilOp       ("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask",Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask       ("Color Mask",        Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;

            float _Speed;
            float _BandCount;
            float _Saturation;
            float _Brightness;

            float  _Duotone;
            fixed4 _DuoColorA;
            fixed4 _DuoColorB;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // Conversion HSV -> RGB sin ramas, para el matiz de la serpiente.
            float3 hsv2rgb(float h, float s, float v)
            {
                h = frac(h);
                float3 rgb = saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return v * lerp(float3(1, 1, 1), rgb, s);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // El sprite del marco solo aporta su canal alfa (la forma del borde).
                fixed4 mask = tex2D(_MainTex, i.uv);
                mask *= i.color;

                float2 d = i.uv - 0.5;
                float angle = atan2(d.y, d.x) / (2.0 * UNITY_PI); // -0.5..0.5
                angle = frac(angle + 0.5);                        // 0..1 alrededor del marco

                if (_Duotone > 0.5)
                {
                    // Circuito Neo-Kemet: el marco es un aro de oro tenue y uno o
                    // más pulsos turquesa lo recorren dejando una cola que se
                    // apaga, como un paquete de datos viajando por la pista.
                    float p = frac(angle * _BandCount - _Time.y * _Speed);
                    float pulse = exp(-p * 6.0);

                    float3 circuit = _DuoColorA.rgb * _Brightness * 0.55;
                    circuit += _DuoColorB.rgb * pulse * _Brightness * 1.25;
                    circuit += pow(pulse, 8.0) * 0.8; // chispa blanca en la cabeza

                    return fixed4(circuit, mask.a);
                }

                // El matiz avanza con el tiempo: la franja de color recorre todo
                // el contorno de forma continua, como una serpiente arcoiris.
                float hue = frac(angle * _BandCount - _Time.y * _Speed);
                float3 rainbow = hsv2rgb(hue, _Saturation, _Brightness);

                return fixed4(rainbow, mask.a);
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
