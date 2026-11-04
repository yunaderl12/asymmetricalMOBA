// Explosion の瞬間に1〜2コマだけ出す、黒地に白い集中線のインパクト画面。UI の RawImage で全画面に貼る。
Shader "Game/VFX/ImpactBurst"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.5
        _Aspect ("Aspect", Float) = 1.78
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One Zero
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Center;
            float _Radius;
            float _Aspect;
            float _Seed;

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash(float x)
            {
                return frac(sin(x * 127.1 + _Seed) * 43758.5453);
            }

            // 角度ごとにばらつく、とがった長さ。
            float Spikes(float angle, float count)
            {
                float u = (angle / 6.28318 + 0.5) * count;
                float i = floor(u);
                float f = frac(u);
                float tri = 1.0 - abs(f * 2.0 - 1.0);
                return tri * lerp(0.25, 1.0, Hash(i));
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - _Center.xy;
                p.x *= _Aspect;
                float r = length(p);
                float angle = atan2(p.y, p.x);

                float outer = _Radius * (0.62 + 0.55 * Spikes(angle, 90.0) + 0.3 * Spikes(angle, 31.0));
                float inner = _Radius * (0.5 - 0.42 * Spikes(angle + 1.3, 70.0));
                float burst = step(inner, r) * step(r, outer);
                float core = step(r, _Radius * 0.07);
                float beam = step(0.93, Spikes(angle + 0.4, 9.0)) * step(r, _Radius * 2.4) * step(_Radius * 0.1, r);
                return float4((burst + core + beam).xxx, 1.0);
            }
            ENDCG
        }
    }
}
