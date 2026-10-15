// World-space triplanar PBR surface: lets scaled/batched primitives share tiling textures.
Shader "Frontline/Triplanar"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Side Albedo (A = smoothness)", 2D) = "white" {}
        _BumpMap ("Side Normal", 2D) = "bump" {}
        _TopTex ("Top Albedo (A = smoothness)", 2D) = "white" {}
        _TopBump ("Top Normal", 2D) = "bump" {}
        _MacroTex ("Macro Variation", 2D) = "gray" {}
        _Scale ("Scale (side U, side V, top, macro) in m", Vector) = (2,2,2,60)
        _Offset ("World Offset", Vector) = (0,0,0,0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        _BumpScale ("Bump Scale", Float) = 1
        _MacroStrength ("Macro Strength", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap, _TopTex, _TopBump, _MacroTex;
        float4 _Scale, _Offset;
        half _Glossiness, _Metallic, _BumpScale, _MacroStrength;
        fixed4 _Color;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = WorldNormalVector(IN, float3(0, 0, 1));
            float3 w = pow(abs(wn), 8);
            w /= max(w.x + w.y + w.z, 1e-4);
            float3 p = IN.worldPos + _Offset.xyz;
            float2 uvX = float2(p.z, p.y) / _Scale.xy;
            float2 uvZ = float2(p.x, p.y) / _Scale.xy;
            float2 uvY = p.xz / _Scale.z;
            float top = step(0, wn.y);

            half4 cX = tex2D(_MainTex, uvX);
            half4 cZ = tex2D(_MainTex, uvZ);
            half4 cY = lerp(tex2D(_MainTex, uvY), tex2D(_TopTex, uvY), top);
            half4 c = cX * w.x + cY * w.y + cZ * w.z;

            half macro = tex2D(_MacroTex, p.xz / _Scale.w).r;
            macro = lerp(1, macro * 2, _MacroStrength);

            o.Albedo = c.rgb * _Color.rgb * macro;
            o.Smoothness = c.a * _Glossiness;
            o.Metallic = _Metallic;

            half3 nX = UnpackScaleNormal(tex2D(_BumpMap, uvX), _BumpScale);
            half3 nZ = UnpackScaleNormal(tex2D(_BumpMap, uvZ), _BumpScale);
            half3 nY = UnpackScaleNormal(lerp(tex2D(_BumpMap, uvY), tex2D(_TopBump, uvY), top), _BumpScale);
            float3 worldN = normalize(wn
                + w.x * float3(0, nX.y, nX.x)
                + w.y * float3(nY.x, 0, nY.y)
                + w.z * float3(nZ.x, nZ.y, 0));

            float3 tX = WorldNormalVector(IN, float3(1, 0, 0));
            float3 tY = WorldNormalVector(IN, float3(0, 1, 0));
            float3 tZ = WorldNormalVector(IN, float3(0, 0, 1));
            o.Normal = normalize(float3(dot(worldN, tX), dot(worldN, tY), dot(worldN, tZ)));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
