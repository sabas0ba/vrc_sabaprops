// Standalone HLSL harness for SabaProps/Foliage.
// Provides the Unity declarations that the surface shader compiler normally
// injects, so the shader's own code can be type-checked by glslang.

#define fixed  float
#define fixed2 float2
#define fixed3 float3
#define fixed4 float4
#define half   float
#define half2  float2
#define half3  float3
#define half4  float4

#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0

float4   _Time;
float3   _WorldSpaceCameraPos;
float4   _LightColor0;
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;

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

struct SurfaceOutput
{
    float3 Albedo;
    float3 Normal;
    float3 Emission;
    float  Specular;
    float  Gloss;
    float  Alpha;
};

#include "shader_body.hlsl"

// Exercise every entry point the surface compiler would generate a call to.
float4 main(appdata_full v : POSITION) : SV_POSITION
{
    Input i;
    vert(v, i);

    SurfaceOutput o;
    UNITY_INITIALIZE_OUTPUT(SurfaceOutput, o);
    surf(i, o);

    float4 lit = LightingSabaFoliage(o, float3(0, 1, 0), float3(0, 0, 1), 1.0);
    return v.vertex + lit + float4(i.color.rgb, i.uv_MainTex.x);
}
