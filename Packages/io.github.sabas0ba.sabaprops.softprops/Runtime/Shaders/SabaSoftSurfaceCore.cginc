#ifndef SABAPROPS_SOFT_SURFACE_CORE_INCLUDED
#define SABAPROPS_SOFT_SURFACE_CORE_INCLUDED

float4 _Contact0;
float4 _Contact1;
float4 _Contact2;
float4 _Contact3;
float4 _Contact4;
float4 _Contact5;
float4 _Contact6;
float4 _Contact7;
float4 _ContactShape0;
float4 _ContactShape1;
float4 _ContactShape2;
float4 _ContactShape3;
float4 _ContactShape4;
float4 _ContactShape5;
float4 _ContactShape6;
float4 _ContactShape7;

float _Hardness;
float _MaximumIndent;
float _ContactRadius;
float _RimLift;
float _WrinkleStrength;
float _WrinkleFrequency;
float _LateralSpread;

// shape.xy: 接触面上の長辺axis、shape.z: capsuleのhalf lengthまたはbox half length。
// shape.w >= 0: capsule radius、shape.w < 0: oriented boxのhalf width。
void SabaSoftFootprint(
    float2 delta,
    float4 shape,
    out float distanceToContact,
    out float footprintRadius,
    out float2 direction)
{
    float2 axis = shape.xy;
    axis = dot(axis, axis) > 0.0001 ? normalize(axis) : float2(1.0, 0.0);
    float2 perpendicular = float2(-axis.y, axis.x);
    float2 oriented = float2(dot(delta, axis), dot(delta, perpendicular));

    if (shape.w < 0.0)
    {
        float2 extents = max(float2(shape.z, -shape.w), 0.001);
        float2 closest = clamp(oriented, -extents, extents);
        float2 offset = oriented - closest;
        distanceToContact = length(offset);
        direction = distanceToContact > 0.0001
            ? (axis * offset.x + perpendicular * offset.y) / distanceToContact
            : float2(0.0, 0.0);
        footprintRadius = max(_ContactRadius * 0.22, 0.012);
        return;
    }

    float closestAlong = clamp(oriented.x, -shape.z, shape.z);
    float2 capsuleOffset = float2(oriented.x - closestAlong, oriented.y);
    distanceToContact = length(capsuleOffset);
    direction = distanceToContact > 0.0001
        ? (axis * capsuleOffset.x + perpendicular * capsuleOffset.y) / distanceToContact
        : float2(0.0, 0.0);
    footprintRadius = max(shape.w, 0.001);
}

// 1 contactあたりの変形。contact.xyzはobject local座標、wは0..1の荷重。
// softnessはmeshのCOLOR.rに格納した縫い目／端部の固定maskである。
void SabaSoftContact(
    float3 positionOS,
    float softness,
    float4 contact,
    float4 shape,
    inout float displacement,
    inout float2 slope,
    inout float2 lateral)
{
    if (contact.w <= 0.0001 || softness <= 0.0001)
    {
        return;
    }

    float hardness = saturate(_Hardness);
    float2 delta = positionOS.xz - contact.xz;
    float distanceToContact;
    float radius;
    float2 direction;
    SabaSoftFootprint(delta, shape, distanceToContact, radius, direction);
    radius *= lerp(1.18, 0.72, hardness);
    float normalizedDistance = distanceToContact / radius;

    if (normalizedDistance >= 1.35)
    {
        return;
    }

    float core = saturate(1.0 - normalizedDistance);
    core = core * core * (3.0 - 2.0 * core);

    float depth = _MaximumIndent * lerp(1.0, 0.28, hardness) * contact.w;
    displacement -= core * depth * softness;

    // d(-depth * smooth kernel)/drの近似をnormal補正に使う。
    float gradient = depth * 5.5 * core * (1.0 - core) / radius;
    slope += direction * gradient * softness;

    // foamが沈み込み周辺へ逃げる量。硬いpresetでは横方向へ広げない。
    float spread = core * (1.0 - core) * depth * _LateralSpread * (1.0 - hardness);
    lateral += direction * spread * softness;

    // core外周の小さな隆起と不規則なしわ。pixel shaderへcontact計算を
    // 持ち込まず、subdivided meshのvertexだけで表現する。
    float rimBand = saturate(1.0 - abs(normalizedDistance - 0.82) * 5.0);
    displacement += rimBand * _RimLift * contact.w * softness * (1.0 - hardness * 0.6);

    // 同心円状の波を避け、面上の2方向へ不規則に走る短いcreaseとして扱う。
    float wrinkleBand = saturate(core * (1.0 - core) * 4.2);
    float creaseA = sin(dot(positionOS.xz, float2(0.82, 0.57))
        * _WrinkleFrequency * 4.1 + contact.x * 13.0);
    float creaseB = sin(dot(positionOS.xz, float2(-0.36, 0.93))
        * _WrinkleFrequency * 3.3 + contact.z * 17.0 + 1.7);
    float wrinkle = creaseA * 0.62 + creaseB * 0.38;
    displacement += wrinkle * wrinkleBand * _WrinkleStrength * contact.w * softness;
}

void SabaSoftDeform(inout float3 positionOS, inout float3 normalOS, float softness)
{
    float displacement = 0.0;
    float2 slope = 0.0;
    float2 lateral = 0.0;

    SabaSoftContact(positionOS, softness, _Contact0, _ContactShape0, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact1, _ContactShape1, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact2, _ContactShape2, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact3, _ContactShape3, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact4, _ContactShape4, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact5, _ContactShape5, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact6, _ContactShape6, displacement, slope, lateral);
    SabaSoftContact(positionOS, softness, _Contact7, _ContactShape7, displacement, slope, lateral);

    positionOS.y += displacement;
    positionOS.xz += lateral;

    // 上面以外はsoftness=0であるため、rounded sideのnormalを変更しない。
    float3 deformedNormal = normalize(float3(-slope.x, 1.0, -slope.y));
    normalOS = normalize(lerp(normalOS, deformedNormal, softness));
}

#endif
