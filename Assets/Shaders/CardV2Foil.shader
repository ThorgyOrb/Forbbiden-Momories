Shader "YGO/CardV2Foil"
{
    // Foil de CRISTAL para TODA la carta (rareza ultra): la superficie se fragmenta en
    // esquirlas irregulares (voronoi) con un tinte iridiscente MUY sutil, y un BRILLO
    // DIAGONAL recorre la carta encendiendo las esquirlas a su paso. El alfa se enmascara
    // con el canal alfa del sprite (silueta de la carta). Se anima solo (_Time).
    Properties
    {
        [PerRendererData] _MainTex ("Máscara (alfa = silueta)", 2D) = "white" {}
        _Cells ("Esquirlas (densidad)", Float) = 9
        _Base ("Cristal base (siempre)", Range(0,0.4)) = 0.06
        _Saturation ("Saturación tinte", Range(0,1)) = 0.55
        _SweepIntensity ("Intensidad del brillo", Range(0,1)) = 0.45
        _SweepWidth ("Ancho del brillo", Range(0.02,0.5)) = 0.14
        _SweepSpeed ("Velocidad del brillo", Float) = 0.25
        _Crack ("Grietas entre esquirlas", Range(0,1)) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 texcoord:TEXCOORD0; float4 worldPosition:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cells, _Base, _Saturation, _SweepIntensity, _SweepWidth, _SweepSpeed, _Crack;
            float4 _ClipRect;

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 g = uv * _Cells;
                float2 n = floor(g);
                float2 f = frac(g);

                // Voronoi: esquirla más cercana (md) y segunda (md2) para las grietas.
                float md = 8.0, md2 = 8.0;
                float2 cellId = n;
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 gi = float2(i, j);
                        float2 o = hash2(n + gi);
                        float2 r = gi + o - f;
                        float d = dot(r, r);
                        if (d < md) { md2 = md; md = d; cellId = n + gi; }
                        else if (d < md2) { md2 = d; }
                    }

                float facet = hash2(cellId).x;                 // aleatorio por esquirla
                float crackDist = sqrt(md2) - sqrt(md);        // ~0 en las aristas
                float crack = smoothstep(0.0, 0.06, crackDist); // 0 en la grieta, 1 dentro

                // Tinte iridiscente sutil por esquirla.
                float3 tint = hsv2rgb(float3(frac(facet + _Time.y * 0.03), _Saturation, 1.0));
                float3 baseCol = lerp(float3(1.0, 1.0, 1.0), tint, 0.35);

                // Brillo DIAGONAL que recorre la carta.
                float diag = (uv.x + (1.0 - uv.y)) * 0.5;      // coordenada diagonal 0..1
                float band = frac(_Time.y * _SweepSpeed);
                float dist = abs(frac(diag - band + 0.5) - 0.5);
                float sweep = smoothstep(_SweepWidth, 0.0, dist);
                sweep *= 0.55 + 0.45 * facet;                  // cada esquirla se enciende distinto

                // Color y alfa finales.
                float3 rgb = lerp(baseCol, tint, sweep);       // más iridiscente bajo el brillo
                float a = _Base + sweep * _SweepIntensity;
                a *= lerp(1.0 - _Crack, 1.0, crack);           // las grietas rebajan el alfa

                float mask = tex2D(_MainTex, uv).a;
                a *= mask;

                fixed4 color = fixed4(rgb, saturate(a)) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
