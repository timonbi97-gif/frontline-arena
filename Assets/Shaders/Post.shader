// Bloom + ACES tonemapping + grading + vignette + grain.
Shader "Hidden/Frontline/Post"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _Bloom ("", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _Bloom;
    float4 _MainTex_TexelSize;
    float _Threshold, _Intensity, _Exposure, _Saturation, _Contrast, _Vignette, _Grain, _Desat;
    float4 _Tint;

    half3 Sample4 (float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half4 fragPrefilter (v2f_img i) : SV_Target
    {
        half3 c = Sample4(i.uv, 1);
        half br = max(c.r, max(c.g, c.b));
        half k = max(0, br - _Threshold) / max(br, 1e-4);
        return half4(min(c * k, 20), 1);
    }

    half4 fragDown (v2f_img i) : SV_Target { return half4(Sample4(i.uv, 1), 1); }
    half4 fragUp (v2f_img i) : SV_Target { return half4(Sample4(i.uv, 0.5), 1); }

    half3 ACES (half3 x)
    {
        const half a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    float rand (float2 co) { return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453); }

    half4 fragFinal (v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_Bloom, i.uv).rgb * _Intensity;
        c *= _Exposure * _Tint.rgb;
        c = ACES(c);
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(l.xxx, c, _Saturation * (1 - _Desat));
        half3 s = sqrt(c);
        s = saturate((s - 0.5) * _Contrast + 0.5);
        c = s * s;
        float2 d = i.uv - 0.5;
        c *= saturate(1 - dot(d, d) * _Vignette);
        c += (rand(i.uv * 1.7 + frac(_Time.y * 7.3)) - 0.5) * _Grain;
        return half4(max(c, 0), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragFinal
            ENDCG }
    }
}
