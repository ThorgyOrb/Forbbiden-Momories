Shader "YGO/CardHoloShader"
{
    // ═══ Estilo "Neo-Kemet": Egipto futurista + cyberpunk ═══
    // La rareza enciende capas:
    //   0 = Common     → arte plano, sin efectos.
    //   1 = Rare       → aurora (turquesa/azul profundo) + parallax sutil.
    //   2 = Epic       → + foil de interferencia, barrido dorado, luces
    //                     metálicas en el arte y scanlines holográficas.
    //   3 = Legendary  → + relieve de circuito-jeroglífico (normal map procedural
    //                     u opcionalmente _BumpMap), rayos de Ra, ráfagas de
    //                     glitch y chispas doradas.
    // Funciona sobre UI (Canvas) sin luces reales: la "luz" es virtual y se
    // desplaza con la dirección de vista, por eso el foil y el grabado cambian
    // al mover la cámara sobre la mesa 3D.
    Properties
    {
        _MainTex         ("Card Art",          2D)              = "white" {}
        _Color           ("Tint",              Color)           = (1,1,1,1)

        [Header(Rareza)]
        // 0 = Common | 1 = Rare (aurora) | 2 = Epic (+foil) | 3 = Legendary (+relieve y chispas)
        _RarityMode      ("Rarity Mode",       Range(0,3))      = 0

        [Header(AuroraFondoMovible)]
        _AuroraStrength   ("Aurora Strength",   Range(0,1))      = 0
        _AuroraSpeed      ("Aurora Speed",      Range(0,2))      = 0.7
        _AuroraScale      ("Aurora Scale",      Range(0.5,6))    = 2.0
        _AuroraOpacity    ("Aurora Opacity",    Range(0,1))      = 1.0
        _AuroraIntensity  ("Aurora Intensity",  Range(0,10))     = 4.5
        _AuroraTintAmount ("Aurora Tint Amount", Range(0,1))     = 0.35
        _AuroraColorA     ("Aurora Color A",    Color)           = (0.10, 0.72, 0.65, 1)
        _AuroraColorB     ("Aurora Color B",    Color)           = (0.12, 0.37, 0.85, 1)

        [Header(BarridoDorado)]
        _GlareStrength   ("Glare Strength",    Range(0,1))      = 0
        _GlareSize       ("Glare Width",        Range(2,40))     = 8
        _GlareIntensity  ("Glare Intensity",   Range(0,6))      = 2.6
        _GlareSweepSpeed ("Glare Sweep Speed", Range(0,2))      = 0.18
        _GlareTint       ("Glare Tint",        Color)           = (1.0, 0.86, 0.52, 1)

        [Header(FoilInterferencia)]
        _FoilStrength    ("Foil Strength",     Range(0,1))      = 0
        _FoilIntensity   ("Foil Intensity",    Range(0,4))      = 1.4
        _FoilStripeScale ("Foil Stripe Scale", Range(2,40))     = 14
        _FoilHueSpeed    ("Foil Hue Speed",    Range(0,2))      = 0.35
        _FoilDuoA        ("Foil Duo Gold",     Color)           = (1.00, 0.80, 0.42, 1)
        _FoilDuoB        ("Foil Duo Cyan",     Color)           = (0.22, 0.95, 0.86, 1)

        [Header(RelieveCircuitoJeroglifico)]
        _ReliefStrength  ("Relief Strength",   Range(0,1))      = 0
        _ReliefIntensity ("Relief Intensity",  Range(0,4))      = 1.6
        _ReliefScale     ("Relief Scale",      Range(4,40))     = 15
        // Normal map opcional (grabado de autor). Si se deja el "bump" gris,
        // el patrón procedural de circuito-jeroglífico hace de relieve.
        [Normal] _BumpMap ("Normal Map (opcional)", 2D)         = "bump" {}
        _BumpInfluence   ("Normal Map Influence", Range(0,2))   = 0

        [Header(ArteVivo)]
        // Efectos centrados en la ILUSTRACIÓN (no en el marco):
        //   Parallax  — el arte flota detrás del marco al mover la cámara (Rare+).
        //   Metal     — las luces del arte brillan como lámina metálica (Epic+).
        //   Scanlines — barrido holográfico turquesa muy sutil (Epic+).
        //   God Rays  — rayos de Ra cayendo desde arriba del arte (Legendary).
        //   Glitch    — ráfaga breve de distorsión + separación RGB (Legendary).
        _ParallaxStrength ("Parallax Strength",  Range(0,1))      = 0
        _ParallaxDepth    ("Parallax Depth",     Range(0,0.08))   = 0.035
        _MetalStrength    ("Metal Highlights",   Range(0,1))      = 0
        _MetalIntensity   ("Metal Intensity",    Range(0,4))      = 1.1
        _ScanStrength     ("Scanline Strength",  Range(0,1))      = 0
        _RayStrength      ("God Rays Strength",  Range(0,1))      = 0
        _RayIntensity     ("God Rays Intensity", Range(0,4))      = 1.2
        _GlitchStrength   ("Glitch Strength",    Range(0,1))      = 0

        [Header(SparklesSoloLegendary)]
        _SparkleStrength ("Sparkle Strength",  Range(0,1))      = 0
        _SparkleSpeed    ("Sparkle Speed",     Range(0,3))      = 1.0
        _SparkleDensity  ("Sparkle Density",   Range(4,40))     = 14
        _SparkleSize     ("Sparkle Size",      Range(0.01,0.3)) = 0.22
        _SparkleColor    ("Sparkle Color",     Color)           = (1.0, 0.92, 0.70, 1)

        // El tilt queda fijo en 0,0 (heredado de una versión anterior); se
        // conserva por compatibilidad con el componente.
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
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 color    : COLOR;
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
            fixed4 _GlareTint;

            float  _FoilStrength;
            float  _FoilIntensity;
            float  _FoilStripeScale;
            float  _FoilHueSpeed;
            fixed4 _FoilDuoA;
            fixed4 _FoilDuoB;

            float  _ReliefStrength;
            float  _ReliefIntensity;
            float  _ReliefScale;
            sampler2D _BumpMap;
            float4    _BumpMap_ST;
            float  _BumpInfluence;

            float  _ParallaxStrength;
            float  _ParallaxDepth;
            float  _MetalStrength;
            float  _MetalIntensity;
            float  _ScanStrength;
            float  _RayStrength;
            float  _RayIntensity;
            float  _GlitchStrength;

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
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.uv       = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color    = v.color * _Color;
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

            // Desplazamiento de fase según el ángulo de cámara, proyectado al
            // plano de la carta (espacio de objeto). Es lo que hace que el foil
            // y el grabado "se muevan" al orbitar la cámara sobre la mesa.
            float2 ViewShift(float3 worldPos)
            {
                float3 viewWorld = _WorldSpaceCameraPos - worldPos;
                float3 viewObj   = mul((float3x3)unity_WorldToObject, viewWorld);
                float  len       = max(length(viewObj), 1e-4);
                return viewObj.xy / len;
            }

            // AURORA: cortinas de luz fluidas (idéntica base a la v1).
            float3 Aurora(float2 uv, float time)
            {
                float t = time * _AuroraSpeed;

                float wave1 = sin(uv.x * _AuroraScale * 3.0 + t * 1.3) * 0.5 + 0.5;
                float wave2 = sin(uv.x * _AuroraScale * 5.0 - t * 0.8 + uv.y * 2.0) * 0.5 + 0.5;
                float wave3 = sin(uv.y * _AuroraScale * 2.0 + t * 0.6) * 0.5 + 0.5;

                float n = smoothNoise(uv * _AuroraScale * 1.5 + float2(t * 0.4, t * 0.25));

                float intensity = wave1 * 0.4 + wave2 * 0.35 + wave3 * 0.25;
                intensity = saturate(intensity * 0.9 + n * 0.55);
                intensity = pow(intensity, 0.9);
                intensity = saturate(intensity * 1.4);

                float colorMix = sin(uv.y * 3.0 + t * 0.9 + n * 2.0) * 0.5 + 0.5;
                float3 rarityTint = lerp(_AuroraColorA.rgb, _AuroraColorB.rgb, colorMix);

                float3 col = lerp(float3(1.0, 1.0, 1.0), rarityTint, _AuroraTintAmount);

                return col * intensity;
            }

            // BARRIDO DORADO: banda de luz diagonal esquina a esquina. Antes el
            // fleco era arcoíris pleno; ahora se tiñe con _GlareTint (oro) y solo
            // conserva una irisación sutil en los bordes de la banda.
            float3 GoldSweep(float2 uv, float2 vShift, float time)
            {
                float diag = uv.x + uv.y + (vShift.x + vShift.y) * 0.15;

                float sweep = frac(time * _GlareSweepSpeed) * 2.6 - 0.3;

                float dist = diag - sweep;
                float band = exp(-dist * dist * _GlareSize);

                float3 fringe;
                fringe.r = sin(diag * 14.0 + 0.0) * 0.5 + 0.5;
                fringe.g = sin(diag * 14.0 + 2.0) * 0.5 + 0.5;
                fringe.b = sin(diag * 14.0 + 4.0) * 0.5 + 0.5;

                float3 col = lerp(_GlareTint.rgb, _GlareTint.rgb * (0.6 + 0.6 * fringe), 0.35);
                col = lerp(col, float3(1, 1, 1), band * 0.65);
                return col * band;
            }

            // FOIL DE INTERFERENCIA: franjas prismáticas finas cuya fase depende
            // del ángulo de vista, sesgadas hacia el dúo oro/turquesa del estilo.
            // La microtextura cruzada (etch) rompe la luz como el holograbado de
            // una lámina real.
            float3 InterferenceFoil(float2 uv, float2 vShift, float time)
            {
                float diag  = uv.x + uv.y * 0.62;
                float phase = diag * _FoilStripeScale
                            + vShift.x * 2.4 + vShift.y * 1.6
                            + time * _FoilHueSpeed;

                float3 iri;
                iri.r = sin(phase)         * 0.5 + 0.5;
                iri.g = sin(phase + 2.094) * 0.5 + 0.5;
                iri.b = sin(phase + 4.188) * 0.5 + 0.5;

                float  duoMix = sin(phase * 0.5) * 0.5 + 0.5;
                float3 duo    = lerp(_FoilDuoA.rgb, _FoilDuoB.rgb, duoMix);

                float3 col = lerp(iri, duo, 0.55);

                // Etch: rejilla diagonal muy fina que modula la intensidad.
                float etchA = sin((uv.x + uv.y) * 160.0) * 0.5 + 0.5;
                float etchB = sin((uv.x - uv.y) * 160.0 + vShift.x * 8.0) * 0.5 + 0.5;
                float etch  = lerp(0.55, 1.0, etchA * etchB);

                // Solo brilla por vetas: bandas anchas y suaves que respiran.
                float veins = smoothNoise(uv * 3.0 + vShift * 1.2 + time * 0.15);
                veins = smoothstep(0.35, 0.85, veins);

                return col * etch * veins;
            }

            // RELIEVE CIRCUITO-JEROGLÍFICO: campo de altura procedural — pistas
            // de circuito con pads y celdas "glifo" — convertido a normales por
            // diferencias finitas e iluminado con una luz virtual que orbita y
            // sigue la dirección de vista.
            float CircuitHeight(float2 uv)
            {
                float2 g  = uv * _ReliefScale;
                float2 id = floor(g);
                float2 f  = frac(g);
                float  r  = hash(id);

                float h = 0.0;
                float lw = 0.10;

                // Pista horizontal o vertical según la celda.
                if (r < 0.45)
                    h = smoothstep(lw, lw * 0.35, abs(f.y - 0.5));
                else if (r < 0.9)
                    h = smoothstep(lw, lw * 0.35, abs(f.x - 0.5));
                else
                {
                    // Celda "glifo": anillo (ojo/cartucho) en vez de pista.
                    float d = length(f - 0.5);
                    h = smoothstep(0.06, 0.02, abs(d - 0.26));
                }

                // Pad/nodo en algunas intersecciones.
                float pad = smoothstep(0.16, 0.10, length(f - 0.5))
                          * step(0.62, hash(id + 3.1));

                return max(h, pad);
            }

            float3 CircuitRelief(float2 uv, float2 vShift, float time)
            {
                float2 e = 1.0 / max(_ReliefScale * 24.0, 64.0);

                float h  = CircuitHeight(uv);
                float hx = CircuitHeight(uv + float2(e.x, 0));
                float hy = CircuitHeight(uv + float2(0, e.y));

                float3 n = normalize(float3(h - hx, h - hy, 0.35));

                // Normal map de autor (opcional) perturba el relieve procedural.
                if (_BumpInfluence > 0.001)
                {
                    float3 bump = UnpackNormal(tex2D(_BumpMap, uv * _BumpMap_ST.xy + _BumpMap_ST.zw));
                    n = normalize(n + bump * _BumpInfluence);
                }

                // Luz virtual: orbita despacio y se desplaza con la cámara.
                float2 lxy = vShift * 1.6 + float2(sin(time * 0.5), cos(time * 0.4)) * 0.45;
                float3 L   = normalize(float3(lxy, 0.9));

                float diff = saturate(dot(n, L));
                float spec = pow(diff, 14.0);

                // El brillo vive sobre los surcos del grabado (h), no en el fondo.
                float3 gold = _FoilDuoA.rgb;
                float3 cyan = _FoilDuoB.rgb;
                float3 tint = lerp(gold, cyan, saturate(vShift.x * 0.5 + 0.5) * 0.45);

                return tint * (spec * 1.4 + diff * 0.15) * h;
            }

            // RAYOS DE RA: haces de luz dorada que caen desde arriba del arte,
            // girando muy despacio, como sol atravesando polvo de tumba.
            float3 GodRays(float2 uv, float time)
            {
                float2 src = float2(0.5, 1.25); // foco virtual sobre el arte
                float2 d   = uv - src;

                float ang  = atan2(d.y, d.x);
                float rays = sin(ang * 16.0 + time * 0.5) * 0.5 + 0.5;
                rays = pow(rays, 4.0);

                float fall = saturate(1.0 - length(d) * 0.75);
                fall *= fall;

                return _GlareTint.rgb * rays * fall;
            }

            // SPARKLES: chispas doradas parpadeando (idéntica base a la v1).
            float3 Sparkles(float2 uv, float time)
            {
                float2 grid = uv * _SparkleDensity;
                float2 cellId = floor(grid);
                float2 cellUv = frac(grid) - 0.5;

                float rnd1 = hash(cellId);
                float rnd2 = hash(cellId + 17.0);

                float2 jitter = float2(
                    sin(time * 0.3 + rnd1 * 6.28) * 0.2,
                    cos(time * 0.25 + rnd2 * 6.28) * 0.2
                );
                float2 sparklePos = cellUv - jitter;
                float dist = length(sparklePos);

                float blink = pow(saturate(sin(time * _SparkleSpeed * 2.0 + rnd1 * 30.0) * 0.5 + 0.5), 3.0);

                float core = exp(-dist * dist / (_SparkleSize * _SparkleSize * 0.15));
                float spark = core * blink;

                float crossH = exp(-dist * dist / (_SparkleSize * _SparkleSize * 0.5)) * 0.3;
                spark = saturate(spark + crossH * blink);

                return _SparkleColor.rgb * spark;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float  t      = _Time.y;
                float2 vShift = ViewShift(i.worldPos);

                // ── Parallax: el arte flota detrás del marco. Se encoge un poco
                // el UV (margen) y se desplaza con la vista; así nunca se lee
                // fuera del sprite.
                float2 uvArt = i.uv;
                if (_ParallaxStrength > 0.001)
                {
                    float zoom = _ParallaxDepth * _ParallaxStrength * 1.2;
                    uvArt = (uvArt - 0.5) * (1.0 - zoom * 2.0) + 0.5;
                    uvArt += vShift * _ParallaxDepth * _ParallaxStrength;
                }

                // ── Glitch holográfico: en algunos ciclos de 4 s, una ráfaga de
                // ~0.2 s desplaza filas del arte y separa los canales RGB.
                float glitchAmt = 0.0;
                if (_GlitchStrength > 0.001)
                {
                    float cycle = floor(t / 4.0);
                    float trig  = step(0.6, hash(float2(cycle, 7.7)));
                    float phase = frac(t / 4.0);
                    glitchAmt = trig * step(phase, 0.055) * _GlitchStrength;

                    if (glitchAmt > 0.001)
                    {
                        float row  = floor(uvArt.y * 22.0);
                        float rsel = step(0.55, hash(float2(row, cycle)));
                        uvArt.x += (hash(float2(row, cycle + 13.0)) - 0.5) * 0.08 * glitchAmt * rsel;
                    }
                }

                uvArt = clamp(uvArt, 0.0, 1.0);

                fixed4 base = tex2D(_MainTex, uvArt);
                if (glitchAmt > 0.001)
                {
                    float2 split = float2(0.007, 0) * glitchAmt;
                    base.r = tex2D(_MainTex, clamp(uvArt + split, 0.0, 1.0)).r;
                    base.b = tex2D(_MainTex, clamp(uvArt - split, 0.0, 1.0)).b;
                }
                base *= i.color;

                float3 col = base.rgb;

                float brightness = dot(base.rgb, float3(0.299, 0.587, 0.114));
                float mask = lerp(0.7, 1.0, smoothstep(0.0, 0.45, brightness));

                int mode = (int)floor(_RarityMode + 0.5);

                if (mode >= 1)
                {
                    // Rare+: aurora de fondo.
                    if (_AuroraStrength > 0.01)
                    {
                        float3 aurora = Aurora(i.uv, t);
                        col += aurora * _AuroraStrength * _AuroraOpacity * mask * _AuroraIntensity;
                    }
                }

                if (mode >= 2)
                {
                    // Epic+: foil de interferencia + barrido dorado.
                    if (_FoilStrength > 0.01)
                        col += InterferenceFoil(i.uv, vShift, t) * _FoilStrength * mask * _FoilIntensity;

                    if (_GlareStrength > 0.01)
                        col += GoldSweep(i.uv, vShift, t) * _GlareStrength * mask * _GlareIntensity;

                    // Luces metálicas: las zonas claras del ARTE brillan como
                    // lámina, con una banda que barre según la vista.
                    if (_MetalStrength > 0.01)
                    {
                        float band = sin((i.uv.x - i.uv.y) * 9.0 + vShift.x * 3.5 + t * 0.5) * 0.5 + 0.5;
                        float spec = pow(brightness, 3.0) * (0.35 + 0.65 * band);
                        float3 metalTint = lerp(_FoilDuoA.rgb, float3(1.0, 1.0, 1.0), 0.45);
                        col += metalTint * spec * _MetalStrength * _MetalIntensity;
                    }

                    // Scanlines holográficas turquesa, muy sutiles.
                    if (_ScanStrength > 0.01)
                    {
                        float scan = sin(i.uv.y * 240.0 - t * 3.0) * 0.5 + 0.5;
                        col += _FoilDuoB.rgb * scan * scan * 0.05 * _ScanStrength;
                    }
                }

                if (mode >= 3)
                {
                    // Legendary: rayos de Ra sobre el arte.
                    if (_RayStrength > 0.01)
                        col += GodRays(i.uv, t) * _RayStrength * _RayIntensity * mask;

                    // Relieve grabado + chispas doradas.
                    if (_ReliefStrength > 0.01)
                        col += CircuitRelief(i.uv, vShift, t) * _ReliefStrength * _ReliefIntensity;

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
