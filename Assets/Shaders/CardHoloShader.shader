Shader "YGO/CardHoloShader"
{
    Properties
    {
        _MainTex         ("Card Art",          2D)              = "white" {}
        _Color           ("Tint",              Color)           = (1,1,1,1)

        [Header(Rareza)]
        // 0 = Common (sin efecto) | 1 = Rare (aurora) | 2 = Epic (aurora + glare) | 3 = Legendary (aurora + glare + chispas)
        _RarityMode      ("Rarity Mode",       Range(0,3))      = 0

        [Header(AuroraFondoMovible)]
        _AuroraStrength   ("Aurora Strength",   Range(0,1))      = 0
        _AuroraSpeed      ("Aurora Speed",      Range(0,2))      = 0.7
        _AuroraScale      ("Aurora Scale",      Range(0.5,6))    = 2.0
        _AuroraOpacity    ("Aurora Opacity",    Range(0,1))      = 1.0
        _AuroraIntensity  ("Aurora Intensity",  Range(0,10))     = 6.0
        // 0 = el efecto se ve transparente/blanco (solo se nota el brillo en movimiento,
        // sin pintar la carta del color de la rareza) | 1 = color de rareza al 100%
        _AuroraTintAmount ("Aurora Tint Amount", Range(0,1))     = 0.18
        _AuroraColorA     ("Aurora Color A",    Color)           = (0.2, 1.0, 0.7, 1)
        _AuroraColorB     ("Aurora Color B",    Color)           = (0.5, 0.3, 1.0, 1)

        [Header(ReflejoEsquinaAEsquina)]
        _GlareStrength   ("Glare Strength",    Range(0,1))      = 0
        _GlareSize       ("Glare Width",        Range(2,40))     = 8
        _GlareIntensity  ("Glare Intensity",   Range(0,6))      = 4.0
        _GlareSweepSpeed ("Glare Sweep Speed", Range(0,2))      = 0.18

        [Header(SparklesSoloLegendary)]
        _SparkleStrength ("Sparkle Strength",  Range(0,1))      = 0
        _SparkleSpeed    ("Sparkle Speed",     Range(0,3))      = 1.0
        _SparkleDensity  ("Sparkle Density",   Range(4,40))     = 14
        _SparkleSize     ("Sparkle Size",      Range(0.01,0.3)) = 0.22
        _SparkleColor    ("Sparkle Color",     Color)           = (1,1,1,1)

        // El tilt queda fijo en 0,0 (heredado de una versión anterior); el reflejo
        // diagonal ya no lo usa, pero se deja por compatibilidad con el componente.
        _TiltX           ("Tilt X",            Range(-1,1))     = 0
        _TiltY           ("Tilt Y",            Range(-1,1))     = 0

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

            float  _RarityMode;

            float  _AuroraStrength;
            float  _AuroraSpeed;
            float  _AuroraScale;
            float  _AuroraOpacity;
            float  _AuroraIntensity;
            float  _AuroraTintAmount;
            fixed4 _AuroraColorA;
            fixed4 _AuroraColorB;

            float  _GlareStrength;
            float  _GlareSize;
            float  _GlareIntensity;
            float  _GlareSweepSpeed;

            float  _SparkleStrength;
            float  _SparkleSpeed;
            float  _SparkleDensity;
            float  _SparkleSize;
            fixed4 _SparkleColor;

            float  _TiltX;
            float  _TiltY;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // AURORA: ondas de color que fluyen como aurora boreal.
            // Varias capas de seno desplazadas en el tiempo crean un
            // movimiento continuo de cortinas de luz, mezclando dos colores.
            float3 Aurora(float2 uv, float time)
            {
                float t = time * _AuroraSpeed;

                float wave1 = sin(uv.x * _AuroraScale * 3.0 + t * 1.3) * 0.5 + 0.5;
                float wave2 = sin(uv.x * _AuroraScale * 5.0 - t * 0.8 + uv.y * 2.0) * 0.5 + 0.5;
                float wave3 = sin(uv.y * _AuroraScale * 2.0 + t * 0.6) * 0.5 + 0.5;

                float n = smoothNoise(uv * _AuroraScale * 1.5 + float2(t * 0.4, t * 0.25));

                float intensity = wave1 * 0.4 + wave2 * 0.35 + wave3 * 0.25;
                intensity = saturate(intensity * 0.9 + n * 0.55);
                intensity = pow(intensity, 0.9); // menos gamma = se ve mas brillante en general
                intensity = saturate(intensity * 1.4); // empuja el contraste hacia arriba

                float colorMix = sin(uv.y * 3.0 + t * 0.9 + n * 2.0) * 0.5 + 0.5;
                float3 rarityTint = lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, colorMix);

                // En vez de pintar con el color de rareza al 100%, lo mezclamos con
                // blanco: a Tint Amount bajo el efecto se ve transparente/cristalino
                // (solo se nota el brillo que se mueve), no un velo de color opaco.
                float3 col = lerp(float3(1.0, 1.0, 1.0), rarityTint, _AuroraTintAmount);

                return col * intensity;
            }

            // REFLEJO ESQUINA A ESQUINA: una banda de luz diagonal que recorre
            // la carta desde una esquina hasta la opuesta y vuelve a empezar,
            // como el brillo que cruza una lámina holográfica.
            float3 CornerGlare(float2 uv, float time)
            {
                // Coordenada diagonal: 0 en la esquina inferior-izquierda, 2 en la superior-derecha.
                float diag = uv.x + uv.y;

                // Recorrido continuo de -0.3 a 2.3 para que la banda entre y salga
                // completamente por ambas esquinas antes de reiniciar el ciclo.
                float sweep = frac(time * _GlareSweepSpeed) * 2.6 - 0.3;

                float dist = diag - sweep;
                float band = exp(-dist * dist * _GlareSize);

                float3 fringe;
                fringe.r = sin(diag * 18.0 + 0.0) * 0.5 + 0.5;
                fringe.g = sin(diag * 18.0 + 2.0) * 0.5 + 0.5;
                fringe.b = sin(diag * 18.0 + 4.0) * 0.5 + 0.5;

                float3 col = lerp(fringe, float3(1, 1, 1), band * 0.8);
                return col * band;
            }

            // SPARKLES: chispas/particulas flotando, cada una parpadea
            // de forma independiente segun su celda en una rejilla.
            float3 Sparkles(float2 uv, float time)
            {
                float2 grid = uv * _SparkleDensity;
                float2 cellId = floor(grid);
                float2 cellUv = frac(grid) - 0.5;

                float rnd1 = hash(cellId);
                float rnd2 = hash(cellId + 17.0);

                // Posicion de la chispa dentro de su celda, ligeramente animada
                float2 jitter = float2(
                    sin(time * 0.3 + rnd1 * 6.28) * 0.2,
                    cos(time * 0.25 + rnd2 * 6.28) * 0.2
                );
                float2 sparklePos = cellUv - jitter;
                float dist = length(sparklePos);

                // Parpadeo independiente por chispa (curva mas suave, encendida mas tiempo)
                float blink = pow(saturate(sin(time * _SparkleSpeed * 2.0 + rnd1 * 30.0) * 0.5 + 0.5), 3.0);

                float core = exp(-dist * dist / (_SparkleSize * _SparkleSize * 0.15));
                float spark = core * blink;

                // pequeno destello en cruz para que se note mas como "chispa" y no solo punto
                float crossH = exp(-dist * dist / (_SparkleSize * _SparkleSize * 0.5)) * 0.3;
                spark = saturate(spark + crossH * blink);

                return _SparkleColor.rgb * spark;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.uv);
                base *= i.color;

                float3 col = base.rgb;
                float  t   = _Time.y;

                float brightness = dot(base.rgb, float3(0.299, 0.587, 0.114));
                // Piso de mascara mas alto: la aurora ahora se nota tambien sobre
                // zonas oscuras del arte de la carta, en vez de casi desaparecer ahi.
                float mask = lerp(0.7, 1.0, smoothstep(0.0, 0.45, brightness));

                int mode = (int)floor(_RarityMode + 0.5);

                if (mode >= 1)
                {
                    // Rare, Epic y Legendary: fondo aurora movible (siempre activo, amplificado)
                    if (_AuroraStrength > 0.01)
                    {
                        float3 aurora = Aurora(i.uv, t);
                        col += aurora * _AuroraStrength * _AuroraOpacity * mask * _AuroraIntensity;
                    }
                }

                if (mode >= 2)
                {
                    // Epic y Legendary: reflejo diagonal que viaja de esquina a esquina
                    if (_GlareStrength > 0.01)
                        col += CornerGlare(i.uv, t) * _GlareStrength * mask * _GlareIntensity;
                }

                if (mode >= 3)
                {
                    // Solo Legendary: chispas flotando (siempre activas)
                    if (_SparkleStrength > 0.01)
                    {
                        float3 sparkles = Sparkles(i.uv, t);
                        col += sparkles * _SparkleStrength * 2.2;
                    }
                }

                return fixed4(col, base.a);
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
