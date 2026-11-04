// 画面の色を反転させる。UI の RawImage で全画面に貼る。
Shader "Game/VFX/ScreenInvert"
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
            Blend OneMinusDstColor Zero
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            float4 frag() : SV_Target
            {
                return float4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
