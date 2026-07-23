Shader "UI/RoundedCorners"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BorderColor ("Border Color", Color) = (0,0,0,1)
        _BorderWidth ("Border Width", Range(0, 0.15)) = 0.02
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        _Aspect ("Aspect Ratio (W/H)", Float) = 1.33
    }
    
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            fixed4 _Color;
            fixed4 _BorderColor;
            float _BorderWidth;
            float _CornerRadius;
            float _Aspect;
            
            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                // Tint support
                o.color = v.color * _Color;
                return o;
            }
            
            // Signed Distance Field (SDF) for a rounded box
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Physical metric space where 1 unit X = 1 unit Y
                float2 p = (i.texcoord - 0.5) * float2(_Aspect, 1.0);
                float2 b = float2(_Aspect * 0.5, 0.5) - float2(_CornerRadius, _CornerRadius);
                float2 q = abs(p) - b;
                float d = min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - _CornerRadius;
                
                float antialias = 0.005;
                float alpha = 1.0 - smoothstep(0.0 - antialias, 0.0 + antialias, d);
                float borderAlpha = smoothstep(-_BorderWidth - antialias, -_BorderWidth + antialias, d);
                
                fixed4 finalColor = lerp(i.color, _BorderColor, borderAlpha);
                finalColor.a *= alpha;
                
                return finalColor;
            }
            ENDCG
        }
    }
}
