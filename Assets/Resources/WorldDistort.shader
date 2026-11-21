// Draws the world texture with a heat-haze wobble wherever things are falling.
// The texture's alpha channel holds the "just fell" amount (0..1) per cell, written by PowderGame.Render.
// It lives in Resources so it gets included in builds (PowderGame loads it with Resources.Load).
Shader "PowderMan/WorldDistort"
{
    Properties
    {
        [PerRendererData] _MainTex ("World", 2D) = "white" {}
        _Strength ("Strength (cells)", Float) = 1.2
        _Speed ("Wobble speed", Float) = 9
        _Glow ("Brighten falling things", Float) = 0.12
        _GridSize ("Grid size (w, h, 1/w, 1/h)", Vector) = (320, 180, 0.003125, 0.005556)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Strength, _Speed, _Glow;
            float4 _GridSize;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // The fall amount, softened over neighbouring cells so the haze has no hard edges.
            float Motion(float2 uv)
            {
                float2 t = _GridSize.zw * 1.5;
                float m = tex2D(_MainTex, uv).a * 0.4;
                m += tex2D(_MainTex, uv + float2(t.x, 0)).a * 0.15;
                m += tex2D(_MainTex, uv - float2(t.x, 0)).a * 0.15;
                m += tex2D(_MainTex, uv + float2(0, t.y)).a * 0.15;
                m += tex2D(_MainTex, uv - float2(0, t.y)).a * 0.15;
                return m;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float m = Motion(i.uv);
                float2 cell = i.uv * _GridSize.xy;
                float time = _Time.y * _Speed;
                float2 wobble = float2(sin(time + cell.y * 0.9), cos(time * 1.3 + cell.x * 0.7));
                float2 uv = i.uv + wobble * (m * _Strength) * _GridSize.zw;

                fixed4 c = tex2D(_MainTex, uv);
                c.rgb *= 1 + m * _Glow;
                c.a = 1;
                return c * i.color;
            }
            ENDCG
        }
    }
}
