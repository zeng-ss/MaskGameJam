Shader "Custom/ExplosionWarningAdvanced"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,0,0,0.8)
        _EdgeColor ("Edge Color", Color) = (1,1,0,1)
        _Radius ("Radius", Float) = 5
        _WarningTime ("Warning Time", Range(0,10)) = 3
        _ScanSpeed ("Scan Speed", Range(0,5)) = 2
        _ScanWidth ("Scan Width", Range(0,1)) = 0.1
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+200" }
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
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };
            
            float4 _MainColor;
            float4 _EdgeColor;
            float _Radius;
            float _WarningTime;
            float _ScanSpeed;
            float _ScanWidth;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float dist = length(i.uv);
                float time = _Time.y;
                
                // 倒计时进度（0到1）
                float progress = frac(time / _WarningTime);
                
                // 基础圆形
                float circle = smoothstep(_Radius - 0.1, _Radius, dist) -
                              smoothstep(_Radius, _Radius + 0.1, dist);
                
                // 扫描环
                float scanPos = progress * (_Radius + 0.2);
                float scan = smoothstep(scanPos - _ScanWidth, scanPos, dist) -
                            smoothstep(scanPos, scanPos + _ScanWidth, dist);
                
                // 辐射状线条
                float angle = atan2(i.uv.y, i.uv.x);
                float radialLines = sin(angle * 8 + time * 3) * 0.5 + 0.5;
                radialLines = smoothstep(0.4, 0.6, radialLines);
                
                // 组合效果
                float edgeAlpha = circle * 0.8 + scan * 0.6 + radialLines * 0.4;
                float innerAlpha = 1 - smoothstep(_Radius * 0.2, _Radius, dist) * 0.3;
                
                // 颜色混合
                float3 finalColor = lerp(_MainColor.rgb, _EdgeColor.rgb, edgeAlpha);
                float alpha = edgeAlpha * 0.8 + innerAlpha * 0.2;
                
                // 最后时刻快速闪烁
                if (progress > 0.8)
                {
                    float fastBlink = sin(time * 20) * 0.5 + 0.5;
                    alpha *= fastBlink;
                }
                
                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
}