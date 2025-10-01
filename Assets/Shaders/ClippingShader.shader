Shader "Custom/ClippingShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _PlanePoint ("Plane Point (World)", Vector) = (0,0,0,0)
        _PlaneNormal ("Plane Normal (World)", Vector) = (0,0,0,0)
        _ClippingEnabled ("Clipping Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _PlanePoint;
            float4 _PlaneNormal;
            float _ClippingEnabled;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_ClippingEnabled > 0.5)
                {
                    float dist = dot(_PlaneNormal.xyz, i.worldPos - _PlanePoint.xyz);
                    clip(dist);
                }
                
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                float3 normal = normalize(i.worldNormal);
                float diffuse = max(0, dot(normal, _WorldSpaceLightPos0.xyz));
                
                col.rgb *= diffuse + UNITY_LIGHTMODEL_AMBIENT.xyz;
                
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}