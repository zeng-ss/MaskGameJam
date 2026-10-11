// 渐变球形边界
Shader "Custom/SphericalBoundary_Fade"
{
    Properties
    {
        _Color ("Color", Color) = (0,1,1,0.8)     // 霓虹色
        _Radius ("Radius", Float) = 10
        _Thickness ("Thickness", Float) = 0.5
        _FadePower ("Fade Power", Range(1,5)) = 2 // 渐变强度
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            
            float4 _Color;
            float _Radius;
            float _Thickness;
            float _FadePower;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float dist = length(i.worldPos);
                float alpha = 0;
                
                // 平滑渐变
                alpha = saturate(1 - pow(abs(dist - _Radius) / (_Thickness * 0.5), _FadePower));
                
                // 内亮外暗效果
                float glow = 1 - saturate(abs(dist - _Radius) / (_Thickness * 0.5));
                glow = pow(glow, 3);
                
                return fixed4(_Color.rgb, _Color.a * alpha * glow);
            }
            ENDCG
        }
    }
}