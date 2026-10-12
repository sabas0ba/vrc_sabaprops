Shader "SabaProps/Body Contact/Metric Floor"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input { float3 worldPos; };

        float Grid(float2 position, float spacing, float width)
        {
            float2 distanceToLine = abs(frac(position / spacing + 0.5) - 0.5) * spacing;
            float2 pixelWidth = max(fwidth(position), 0.0001);
            float2 coverage = 1 - smoothstep(width, width + pixelWidth, distanceToLine);
            // 遠方では細かい線を消し、VRでのちらつきを抑えます。
            float fade = 1 - smoothstep(spacing * 0.15, spacing * 0.5, max(pixelWidth.x, pixelWidth.y));
            return max(coverage.x, coverage.y) * fade;
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float2 p = input.worldPos.xz;
            float checker = frac((floor(p.x) + floor(p.y)) * 0.5) * 2;
            float3 color = lerp(float3(0.16, 0.20, 0.24), float3(0.22, 0.27, 0.31), checker);
            color = lerp(color, float3(0.34, 0.40, 0.45), Grid(p, 0.1, 0.0015));
            color = lerp(color, float3(0.65, 0.72, 0.77), Grid(p, 1, 0.008));
            output.Albedo = color;
            output.Smoothness = 0;
            output.Metallic = 0;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
