Shader "UI/HoloScreen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Hologram Background)]
        _BgColor ("Background Color", Color) = (0.015, 0.05, 0.11, 0.28)
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.12
        _ScanlineFreq ("Scanline Frequency", Float) = 220.0
        _ScanlineSpeed ("Scanline Speed", Float) = 1.2

        [Header(Illuminated Border)]
        _BorderColor ("Border Color", Color) = (0.0, 0.88, 1.0, 0.95)
        _BorderWidth ("Border Width", Range(0.001, 0.03)) = 0.004
        _AspectRatio ("Aspect Ratio (W / H)", Float) = 1.33333

        [Header(Edge Glow Bloom)]
        _GlowColor ("Glow Color", Color) = (0.0, 0.65, 1.0, 0.45)
        _GlowWidth ("Glow Width", Range(0.005, 0.15)) = 0.035
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1.2

        [Header(Corner Accents)]
        _CornerColor ("Corner Accent Color", Color) = (0.35, 0.95, 1.0, 1.0)
        _CornerSize ("Corner Size", Range(0.01, 0.2)) = 0.06
        _CornerBoost ("Corner Glow Boost", Range(0, 3)) = 1.0

        [Header(Holographic Pulse)]
        _PulseSpeed ("Pulse Speed", Float) = 1.0
        _PulseAmount ("Pulse Amount", Range(0, 0.3)) = 0.05

        [Header(Depth and Stencil)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode ("ZTest Mode (0=Off, 4=LEqual, 8=Always)", Float) = 0
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
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [_ZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "HoloScreenPass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #pragma multi_compile_instancing

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _BgColor;
            float _ScanlineIntensity;
            float _ScanlineFreq;
            float _ScanlineSpeed;

            fixed4 _BorderColor;
            float _BorderWidth;
            float _AspectRatio;

            fixed4 _GlowColor;
            float _GlowWidth;
            float _GlowIntensity;

            fixed4 _CornerColor;
            float _CornerSize;
            float _CornerBoost;

            float _PulseSpeed;
            float _PulseAmount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float distLeftRight = min(IN.texcoord.x, 1.0 - IN.texcoord.x) * _AspectRatio;
                float distBottomTop = min(IN.texcoord.y, 1.0 - IN.texcoord.y);
                float edgeDist = min(distLeftRight, distBottomTop);

                float halfAA = 0.0015;
                float borderFactor = 1.0 - smoothstep(_BorderWidth - halfAA, _BorderWidth + halfAA, edgeDist);

                float glowFalloff = exp(-edgeDist / max(_GlowWidth, 0.001));
                float glowFactor = saturate(glowFalloff * _GlowIntensity) * (1.0 - borderFactor);

                float cornerDistX = min(IN.texcoord.x, 1.0 - IN.texcoord.x);
                float cornerDistY = min(IN.texcoord.y, 1.0 - IN.texcoord.y);
                float isCornerZone = step(cornerDistX, _CornerSize) * step(cornerDistY, _CornerSize * _AspectRatio);

                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 3.14159);

                float scanline = sin(IN.texcoord.y * _ScanlineFreq - _Time.y * _ScanlineSpeed);
                scanline = scanline * 0.5 + 0.5;

                half4 col = _BgColor;
                col.rgb += _BgColor.rgb * (scanline - 0.5) * _ScanlineIntensity;

                col.rgb += _GlowColor.rgb * (glowFactor * _GlowColor.a * pulse);
                col.a = max(col.a, glowFactor * _GlowColor.a);

                half3 borderRgb = _BorderColor.rgb + _CornerColor.rgb * (isCornerZone * _CornerBoost);
                col.rgb = lerp(col.rgb, borderRgb * pulse, borderFactor * _BorderColor.a);
                col.a = max(col.a, borderFactor * _BorderColor.a);

                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
