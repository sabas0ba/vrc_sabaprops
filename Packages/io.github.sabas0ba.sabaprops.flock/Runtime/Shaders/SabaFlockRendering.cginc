// Shared animated geometry and lighting for the ForwardBase and ForwardAdd passes.
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
#include "SabaFlockMotion.cginc"

float _Wrap;
float _SheenStrength;
float _SheenPower;
float4 _SilhouetteColor;
float _SilhouetteStart;
float _SilhouetteEnd;
float4 _MediumColor;
float _MediumDensity;
float _TimeScale;

// Channel layout: see FlockShaderContract.cs.
struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 color : COLOR;
    float4 body : TEXCOORD0;
    float4 individual : TEXCOORD1;
    float4 swarm : TEXCOORD2;
    float4 area : TEXCOORD3;
    float4 animation : TEXCOORD4;
    float4 extra : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos : SV_POSITION;
    float4 color : COLOR;
    float3 worldNormal : TEXCOORD0;
    float3 worldPos : TEXCOORD1;
    UNITY_FOG_COORDS(2)
    UNITY_LIGHTING_COORDS(3, 4)
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    FlockMotionInput motion;
    motion.pattern = v.swarm.x;
    motion.speed = v.swarm.y;
    motion.timeOffset = v.swarm.z;
    motion.clusterRadius = v.swarm.w;
    motion.area = v.area.xyz;
    motion.bodyLength = v.area.w;
    motion.count = v.extra.x;
    motion.index = v.individual.x;
    motion.random = v.individual.yzw;
    motion.bankGain = v.extra.y;
    motion.maxPitch = v.extra.z;

    float time = _Time.y * _TimeScale;
    float3 position;
    float3 forward;
    float3 up;
    float3 right;
    FlockPose(motion, time, position, forward, up, right);

    float3 p = v.vertex.xyz;
    float3 n = v.normal;
    FlockAnimate(p, n, v.body, v.animation, motion.random,
        time + motion.timeOffset, motion.bodyLength);
    p *= FlockIndividualScale(motion.random, v.extra.w);

    float3 objectPos = position + right * p.x + up * p.y + forward * p.z;
    float3 objectNormal = right * n.x + up * n.y + forward * n.z;

    o.pos = UnityObjectToClipPos(objectPos);
    o.worldPos = mul(unity_ObjectToWorld, float4(objectPos, 1.0)).xyz;
    o.worldNormal = UnityObjectToWorldNormal(objectNormal);
    o.color = v.color;
    // Unity's light/cookie/shadow macros must use the animated position.
    v.vertex = float4(objectPos, 1.0);
    UNITY_TRANSFER_LIGHTING(o, float2(0.0, 0.0));
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float3 normal = normalize(i.worldNormal);
    // Direction for a directional light, or from this fragment to a
    // point or spot light; zero when the scene has no main light.
    float3 toLight = UnityWorldSpaceLightDir(i.worldPos);
    float3 light = dot(toLight, toLight) > 1e-8 ? normalize(toLight) : float3(0.0, 1.0, 0.0);
    float3 toCamera = _WorldSpaceCameraPos - i.worldPos;
    float cameraDistance = length(toCamera);
    float3 view = toCamera / max(cameraDistance, 1e-4);

    float diffuse = saturate((dot(normal, light) + _Wrap) / (1.0 + _Wrap));
    float3 ambient = max(ShadeSH9(float4(normal, 1.0)), 0.0);
    UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
    float3 direct = _LightColor0.rgb * attenuation;
    float3 halfVector = light + view;
    float3 halfway = halfVector / max(length(halfVector), 1e-4);
    float sheen = pow(saturate(dot(normal, halfway)), _SheenPower)
        * i.color.a * _SheenStrength;

    #if defined(SABA_FLOCK_ADDITIVE_PASS)
        ambient = 0.0;
    #else
        #if defined(VERTEXLIGHT_ON)
        ambient += Shade4PointLights(unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
            unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb,
            unity_LightColor[3].rgb, unity_4LightAtten0, i.worldPos, normal);
        #endif
    #endif
    float3 illumination = ambient + direct * diffuse;
    float3 colour = i.color.rgb * illumination + direct * sheen;

    // Far individuals read as a silhouette against the sky; under
    // water (or in haze) they fade into the medium.
    float silhouette = smoothstep(_SilhouetteStart, max(_SilhouetteEnd, _SilhouetteStart + 1e-3), cameraDistance)
        * _SilhouetteColor.a;
    // Distance colours follow the environment instead of glowing at night.
    colour = lerp(colour, _SilhouetteColor.rgb * illumination, silhouette);
    float medium = 1.0 - exp(-_MediumDensity * cameraDistance);
    #if defined(SABA_FLOCK_ADDITIVE_PASS)
        colour *= 1.0 - medium;
    #else
        colour = lerp(colour, _MediumColor.rgb * ambient, medium);
    #endif

    fixed4 result = fixed4(colour, 1.0);
    #if defined(SABA_FLOCK_ADDITIVE_PASS)
        UNITY_APPLY_FOG_COLOR(i.fogCoord, result, fixed4(0, 0, 0, 0));
    #else
        UNITY_APPLY_FOG(i.fogCoord, result);
    #endif
    return result;
}
