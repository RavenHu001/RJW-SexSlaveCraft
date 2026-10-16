Shader "Sexslavecraft/SSC_FullGelBody"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.72, 0.38, 0.95, 0.82)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed3 jellyRgb = lerp(baseCol.rgb, baseCol.rgb * _Color.rgb, 0.55);
                jellyRgb = lerp(jellyRgb, _Color.rgb, 0.25 * baseCol.a);
                fixed alpha = saturate(baseCol.a * _Color.a);
                return fixed4(jellyRgb, alpha);
            }
            ENDCG
        }
    }
}
