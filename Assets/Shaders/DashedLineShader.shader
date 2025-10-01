Shader "Custom/DashedLine"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Pattern ("Dash/Gap Pattern (UV Tile)", Float) = 20.0
        _DashLength ("Total Line Length", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            ZTest Always 
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float dashPos : TEXCOORD1;
            };

            fixed4 _Color;
            float _Pattern;
            float _DashLength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.dashPos = v.uv.x * _DashLength; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float dashValue = frac(i.dashPos * _Pattern); 
                clip(0.5 - dashValue); 
                return i.color;
            }
            ENDCG
        }
    }
}   