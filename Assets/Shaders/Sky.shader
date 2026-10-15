// Gradient sky with sun and drifting procedural clouds.
Shader "Frontline/Sky"
{
    Properties
    {
        _Top ("Zenith", Color) = (0.22, 0.42, 0.72, 1)
        _Horizon ("Horizon", Color) = (0.72, 0.8, 0.88, 1)
        _Ground ("Below Horizon", Color) = (0.42, 0.44, 0.46, 1)
        _SunCol ("Sun", Color) = (1, 0.93, 0.8, 1)
        _CloudCol ("Clouds", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade", Color) = (0.55, 0.6, 0.68, 1)
        _Cover ("Cloud Cover", Range(0, 1)) = 0.5
        _Speed ("Cloud Speed", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Ground, _SunCol, _CloudCol, _CloudShade;
            float _Cover, _Speed;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float fbm (float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += noise(p) * a; p = p * 2.03 + 11.7; a *= 0.5; }
                return s;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = h > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(h), 0.45))
                                   : lerp(_Horizon.rgb, _Ground.rgb, saturate(-h * 6));
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float sd = saturate(dot(d, sunDir));
                col += _SunCol.rgb * (pow(sd, 900) * 30 + pow(sd, 12) * 0.35);

                if (h > 0.0)
                {
                    float2 uv = d.xz / (h + 0.12) * 0.9 + _Time.y * _Speed * float2(1, 0.35);
                    float n = fbm(uv * 1.3);
                    float c = smoothstep(1 - _Cover, 1 - _Cover + 0.3, n) * saturate(h * 6);
                    float n2 = fbm(uv * 1.3 + sunDir.xz * 0.06);
                    float lit = saturate(0.6 + (n - n2) * 4);
                    float3 cc = lerp(_CloudShade.rgb, _CloudCol.rgb, lit) + _SunCol.rgb * pow(sd, 6) * 0.4;
                    col = lerp(col, cc, c * 0.92);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
