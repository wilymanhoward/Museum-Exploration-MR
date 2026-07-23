Shader "UI/TranslucentDottedCard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Card Color", Color) = (0.43, 0.46, 0.38, 0.85)
        _DotColor ("Dot Color", Color) = (0.28, 0.30, 0.25, 0.50)
        _DotGridSize ("Grid Density", Float) = 14.0
        _DotRadius ("Dot Radius", Float) = 0.08
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.08
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
            fixed4 _DotColor;
            float _DotGridSize;
            float _DotRadius;
            float _CornerRadius;
            float _Aspect;
            
            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }
            
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
                float cardAlpha = 1.0 - smoothstep(0.0 - antialias, 0.0 + antialias, d);
                
                float2 st = i.texcoord * _DotGridSize;
                st.x *= _Aspect;
                float2 grid = frac(st) - 0.5;
                float dist = length(grid);
                float dotAlpha = (_DotRadius > 0.001) ? (1.0 - smoothstep(_DotRadius - antialias, _DotRadius + antialias, dist)) : 0.0;
                
                fixed4 baseCol = i.color;
                fixed4 finalCol = lerp(baseCol, _DotColor, dotAlpha * _DotColor.a);
                finalCol.a *= cardAlpha;
                
                return finalCol;
            }
            ENDCG
        }
    }
}
