Shader "UI/DottedGrid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Background Tint", Color) = (1,1,1,0)
        _DotColor ("Dot Color", Color) = (0.72, 0.78, 0.85, 0.45)
        _DotGridSize ("Grid Density", Float) = 10.0
        _DotRadius ("Dot Radius", Float) = 0.08
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
            
            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 st = i.texcoord * _DotGridSize;
                float2 grid = frac(st) - 0.5;
                float dist = length(grid);
                
                float antialias = 0.02;
                float dotAlpha = 1.0 - smoothstep(_DotRadius - antialias, _DotRadius + antialias, dist);
                
                fixed4 finalColor = lerp(i.color, _DotColor, dotAlpha * _DotColor.a);
                return finalColor;
            }
            ENDCG
        }
    }
}
