Shader "UI/GlassButton"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base Glass Tint", Color) = (0.1, 0.15, 0.22, 0.65)
        _BorderColor ("Border Rim Glow", Color) = (0.5, 0.7, 0.95, 0.9)
        _SheenColor ("Specular Sheen Color", Color) = (1, 1, 1, 0.30)
        _BorderWidth ("Border Width", Range(0, 0.15)) = 0.025
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.45
        _Aspect ("Aspect Ratio (W/H)", Float) = 3.0
        _SheenIntensity ("Sheen Intensity", Range(0, 1)) = 0.40
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
            fixed4 _SheenColor;
            float _BorderWidth;
            float _CornerRadius;
            float _Aspect;
            float _SheenIntensity;
            
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
                // Physical metric space where 1 unit Y = 1 unit X
                float2 p = (i.texcoord - 0.5) * float2(_Aspect, 1.0);
                float2 b = float2(_Aspect * 0.5, 0.5) - float2(_CornerRadius, _CornerRadius);
                float2 q = abs(p) - b;
                float d = min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - _CornerRadius;
                
                float antialias = 0.005;
                float alpha = 1.0 - smoothstep(0.0 - antialias, 0.0 + antialias, d);
                float borderAlpha = smoothstep(-_BorderWidth - antialias, -_BorderWidth + antialias, d);
                
                // Base glass body with vertex tint
                fixed4 baseGlass = i.color;
                
                // Specular top gloss / curved glass reflection
                float topSheen = smoothstep(0.35, 0.95, i.texcoord.y) * _SheenIntensity;
                // Subtle bottom rim bounce
                float bottomSheen = smoothstep(0.12, 0.01, i.texcoord.y) * (_SheenIntensity * 0.25);
                
                baseGlass.rgb += _SheenColor.rgb * (topSheen + bottomSheen);
                baseGlass.a = saturate(baseGlass.a + topSheen * 0.12);
                
                // Blend with luminous refractive border
                fixed4 finalColor = lerp(baseGlass, _BorderColor, borderAlpha);
                finalColor.a *= alpha;
                
                return finalColor;
            }
            ENDCG
        }
    }
}
