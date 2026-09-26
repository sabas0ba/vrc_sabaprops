// Minimal stand-in for Unity's UnityCG.cginc, for type-checking vertex and
// fragment shaders with glslang. Declares only what the shaders in this
// repository call; the bodies return plausible values of the right type and
// are never meant to be correct.
#ifndef SABA_STUB_UNITYCG_INCLUDED
#define SABA_STUB_UNITYCG_INCLUDED

#define fixed  float
#define fixed2 float2
#define fixed3 float3
#define fixed4 float4
#define half   float
#define half2  float2
#define half3  float3
#define half4  float4

#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0
#define UNITY_VERTEX_INPUT_INSTANCE_ID
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(v)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
#define UNITY_FOG_COORDS(idx) float fogCoord : TEXCOORD##idx;
#define UNITY_TRANSFER_FOG(o, outpos) o.fogCoord = (outpos).z
#define UNITY_APPLY_FOG(coord, col) col.rgb = lerp(col.rgb, float3(0.5, 0.5, 0.5), saturate(coord))

float4   _Time;
float3   _WorldSpaceCameraPos;
float4   _WorldSpaceLightPos0;
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixVP;

float4 UnityObjectToClipPos(float3 position)
{
    return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(position, 1.0)));
}

float3 UnityObjectToWorldNormal(float3 normal)
{
    return normalize(mul(normal, (float3x3)unity_WorldToObject));
}

float3 UnityWorldSpaceLightDir(float3 worldPos)
{
    return _WorldSpaceLightPos0.xyz - worldPos * _WorldSpaceLightPos0.w;
}

float3 ShadeSH9(float4 normal)
{
    return max(normal.xyz * 0.1 + 0.2, 0.0);
}

#endif
