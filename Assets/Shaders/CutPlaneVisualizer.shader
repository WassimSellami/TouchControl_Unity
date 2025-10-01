Shader "Custom/CutPlaneVisualizer"
{
    Properties
    {
        _Color ("Tint Color", Color) = (0.5, 0.7, 1.0, 0.5)
        _RimColor ("Rim Highlight Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 100
        
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _RimColor;
            float _RimPower;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                
                fixed4 finalColor = _Color;
                
                float3 normal = i.worldNormal * facing;
                
                float rim = 1.0 - saturate(dot(viewDir, normal));
                float rimResult = pow(rim, _RimPower);

                if (facing > 0)
                {
                    finalColor.rgb += _RimColor.rgb * rimResult;
                }
                
                finalColor.a = _Color.a;

                return finalColor;
            }
            ENDCG
        }
    }
}