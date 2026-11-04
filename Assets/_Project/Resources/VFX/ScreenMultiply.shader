// 画面に色を掛ける。色は Graphic.color で渡す。白で変化なし。
Shader "Game/VFX/ScreenMultiply"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "IgnoreProjector" = "True" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
