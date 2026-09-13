// Standalone HLSL harness for SabaProps/Soft Surface.
// Unity's surface shader compiler normally injects these declarations.

#define fixed  float
#define fixed2 float2
#define fixed3 float3
#define fixed4 float4
#define half   float
#define half2  float2
#define half3  float3
#define half4  float4

#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0

struct sampler2D_stub { int unused; };
#define sampler2D sampler2D_stub
float4 tex2D(sampler2D_stub s, float2 uv) { return float4(uv, 0.0, 1.0); }

struct appdata_full
{
    float4 vertex    : POSITION;
    float4 tangent   : TANGENT;
    float3 normal    : NORMAL;
    float4 texcoord  : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    float4 texcoord3 : TEXCOORD3;
    float4 color     : COLOR;
};

struct SurfaceOutputStandard
{
    float3 Albedo;
    float3 Normal;
    float3 Emission;
    float  Metallic;
    float  Smoothness;
    float  Occlusion;
    float  Alpha;
};

#include "soft_shader_body.hlsl"

float4 main(appdata_full v : POSITION) : SV_POSITION
{
    Input i;
    vert(v, i);

    SurfaceOutputStandard o;
    UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandard, o);
    surf(i, o);

    return v.vertex + float4(o.Albedo + o.Emission, o.Alpha)
        + float4(o.Metallic, o.Smoothness, o.Occlusion, i.color.r);
}
