Shader "UI/RayCursor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Radius ("Circle Radius", Range(0.1, 0.5)) = 0.46
        _Feather ("Edge Feather", Range(0.001, 0.05)) = 0.015
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+500"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Radius;
            float _Feather;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Distance from center (0.5, 0.5) in normalized UV coordinates
                float2 offset = i.uv - float2(0.5, 0.5);
                float dist = length(offset);

                // Subpixel anti-aliasing: smoothstep over edge feather
                float edge = max(_Feather, fwidth(dist));
                float alpha = 1.0 - smoothstep(_Radius - edge, _Radius + edge, dist);

                // Discard pixels outside circle:
                // Guarantees 100% transparent background with zero color write and zero depth write
                if (alpha <= 0.001)
                    discard;

                fixed4 c = i.color;
                c.a *= alpha;
                return c;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
