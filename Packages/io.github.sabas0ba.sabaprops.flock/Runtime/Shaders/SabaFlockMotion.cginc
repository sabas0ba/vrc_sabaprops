// Group motion and body animation of SabaProps Flock, evaluated per vertex.
//
// This file is a port of Runtime/FlockMotion.cs. The C# copy is the reference:
// the editor sizes the mesh bounds with it and the offline checks prove that it
// stays inside the area, stays finite and turns smoothly. Any change here must
// be made there too, function by function; the names match.
#ifndef SABA_FLOCK_MOTION_INCLUDED
#define SABA_FLOCK_MOTION_INCLUDED

#define FLOCK_TWO_PI 6.2831853
#define FLOCK_DERIVATIVE_STEP 0.05
#define FLOCK_MAX_BANK 0.9
#define FLOCK_MAX_ORBIT_RATE 2.5

#define FLOCK_PATTERN_CRUISE 0
#define FLOCK_PATTERN_MURMURATION 1
#define FLOCK_PATTERN_VFORMATION 2
#define FLOCK_PATTERN_THERMAL 3
#define FLOCK_PATTERN_STREAM 4
#define FLOCK_PATTERN_BAITBALL 5
#define FLOCK_PATTERN_TORNADO 6
#define FLOCK_PATTERN_WANDER 7
#define FLOCK_PATTERN_ANCHORED 8

#define FLOCK_PART_WING 1

struct FlockMotionInput
{
    float pattern;
    float speed;
    float timeOffset;
    float clusterRadius;
    float3 area;
    float bodyLength;
    float count;
    float index;
    float3 random;
    float maxPitch;
    float bankGain;
};

float FlockFrac(float v)
{
    return v - floor(v);
}

float FlockPathAngularSpeed(float3 amplitude, float speed)
{
    float pathScale = max(0.7 * (amplitude.x + 2.0 * amplitude.z), 0.25);
    return speed / pathScale;
}

float3 FlockPathPoint(float3 amplitude, float theta)
{
    return float3(
        amplitude.x * sin(theta),
        amplitude.y * sin(theta * 0.73 + 1.3),
        amplitude.z * sin(theta * 2.0));
}

void FlockPathFrame(float3 amplitude, float theta, float angularSpeed,
    out float3 forward, out float3 right, out float3 up)
{
    float3 tangent = float3(
        amplitude.x * cos(theta),
        0.73 * amplitude.y * cos(theta * 0.73 + 1.3),
        2.0 * amplitude.z * cos(theta * 2.0)) * (angularSpeed < 0.0 ? -1.0 : 1.0);
    float3 horizontal = float3(tangent.x, 0.0, tangent.z);
    forward = dot(horizontal, horizontal) > 1e-10 ? normalize(horizontal) : float3(0.0, 0.0, 1.0);
    right = normalize(cross(float3(0.0, 1.0, 0.0), forward));
    up = float3(0.0, 1.0, 0.0);
}

float3 FlockUnitBallPoint(float3 r)
{
    float z = 2.0 * r.x - 1.0;
    float phi = FLOCK_TWO_PI * r.y;
    float ring = sqrt(max(1.0 - z * z, 0.0));
    float scale = pow(max(r.z, 1e-6), 0.3333);
    return float3(ring * cos(phi), z, ring * sin(phi)) * scale;
}

float3 FlockWobble(float3 r, float t, float speed, float radius)
{
    float rate = clamp(speed / max(radius, 0.1) * 0.3, 0.2, 2.0);
    return float3(
        sin(t * rate * (0.5 + 0.6 * r.y) + FLOCK_TWO_PI * r.x),
        sin(t * rate * (0.4 + 0.5 * r.z) + FLOCK_TWO_PI * r.y),
        sin(t * rate * (0.6 + 0.4 * r.x) + FLOCK_TWO_PI * r.z));
}

float FlockOrbitRate(float speed, float orbit)
{
    return min(speed / max(orbit, 0.01), FLOCK_MAX_ORBIT_RATE);
}

float3 FlockWander(float3 area, float bodyLength, float speed, float3 r, float t)
{
    float3 room = max(area - bodyLength, 0.0);
    float a = min(room.x, 2.5 * room.z) * (0.35 + 0.4 * r.x);
    float b = room.z * (0.4 + 0.5 * r.z);
    float direction = r.y > 0.5 ? 1.0 : -1.0;
    float orbitRate = min(speed / max(0.5 * (a + b), 0.01), FLOCK_MAX_ORBIT_RATE);
    float angle = direction * orbitRate * t + FLOCK_TWO_PI * r.y;

    float slowest = orbitRate * min(a, b);
    float drift = max(max(room.x - a, room.z - b), 0.01);
    float driftRate = 0.3 * slowest / drift;
    float vertical = min(speed / max(room.y, 0.01) * (0.12 + 0.1 * r.y), 0.3 * orbitRate);

    return float3(
        (room.x - a) * sin(t * driftRate + FLOCK_TWO_PI * r.z) + a * cos(angle),
        room.y * sin(t * vertical + FLOCK_TWO_PI * r.z),
        (room.z - b) * sin(t * driftRate * 0.7 + FLOCK_TWO_PI * r.x) + b * sin(angle));
}

float FlockDriftSpeed(float speed, float radius)
{
    return 0.3 * min(speed, FLOCK_MAX_ORBIT_RATE * 0.16 * radius);
}

float3 FlockPosition(FlockMotionInput input, float time)
{
    float t = time + input.timeOffset;
    float3 area = max(input.area, 0.001);
    float radius = max(input.clusterRadius, 0.0);
    float bodyLength = max(input.bodyLength, 0.0);
    float3 r = input.random;

    if (input.pattern == FLOCK_PATTERN_ANCHORED)
    {
        return input.count < 1.5 ? float3(0.0, 0.0, 0.0)
            : (r * 2.0 - 1.0) * max(area - bodyLength, 0.0);
    }
    int pattern = (int)round(input.pattern);

    if (pattern == FLOCK_PATTERN_WANDER)
    {
        return FlockWander(area, bodyLength, input.speed, r, t);
    }

    float3 centreAmplitude = max(area - (radius + bodyLength), 0.0);
    float angularSpeed = FlockPathAngularSpeed(centreAmplitude, input.speed);
    float theta = t * angularSpeed;
    float driftTheta = t * FlockPathAngularSpeed(centreAmplitude, FlockDriftSpeed(input.speed, radius));

    float3 k = FlockUnitBallPoint(r);
    float3 wobble = FlockWobble(r, t, input.speed, radius);
    float3 centre;
    float3 offset;
    float3 forward;
    float3 right;
    float3 up;

    if (pattern == FLOCK_PATTERN_MURMURATION)
    {
        centre = FlockPathPoint(centreAmplitude, theta);
        float relative = input.speed / max(radius, 0.01);
        float slow = t * min(0.35, 0.5 * relative);
        float3 shape = float3(
            0.55 + 0.45 * sin(slow * 0.61),
            0.35 + 0.25 * sin(slow * 0.47 + 2.1),
            0.55 + 0.45 * sin(slow * 0.37 + 4.2));
        float wave = sin((k.x * 3.0 + k.y + k.z * 2.0) * 1.5 - t * min(1.3, 1.2 * relative));
        offset = k * shape * (0.75 * radius)
            + float3(0.7071, 0.3536, 0.6124) * (0.25 * radius * wave);
    }
    else if (pattern == FLOCK_PATTERN_VFORMATION)
    {
        centre = FlockPathPoint(centreAmplitude, theta);
        FlockPathFrame(centreAmplitude, theta, angularSpeed, forward, right, up);
        float ranks = max(floor(input.count * 0.5), 1.0);
        float spacing = radius / (ranks + 0.2);
        float rank = floor((input.index + 1.0) * 0.5);
        float side = FlockFrac(input.index * 0.5) > 0.25 ? -1.0 : 1.0;
        offset = forward * (-0.8192 * rank * spacing)
            + right * (0.5736 * side * rank * spacing)
            + up * (0.05 * rank * spacing)
            + wobble * (0.06 * spacing);
    }
    else if (pattern == FLOCK_PATTERN_THERMAL)
    {
        centre = FlockPathPoint(centreAmplitude, driftTheta);
        float orbit = radius * (0.3 + 0.5 * r.x);
        float angle = t * FlockOrbitRate(input.speed, orbit) + FLOCK_TWO_PI * r.y;
        float climb = 0.6 * radius * sin(t * 0.05 * input.speed / max(radius, 1.0) + FLOCK_TWO_PI * r.z);
        offset = float3(orbit * cos(angle), climb, orbit * sin(angle));
    }
    else if (pattern == FLOCK_PATTERN_STREAM)
    {
        centre = FlockPathPoint(centreAmplitude, theta);
        FlockPathFrame(centreAmplitude, theta, angularSpeed, forward, right, up);
        offset = forward * ((2.0 * r.x - 1.0) * 0.85 * radius)
            + right * ((2.0 * r.y - 1.0) * 0.3 * radius)
            + up * ((2.0 * r.z - 1.0) * 0.2 * radius)
            + wobble * (0.03 * radius);
    }
    else if (pattern == FLOCK_PATTERN_BAITBALL)
    {
        centre = FlockPathPoint(centreAmplitude, driftTheta);
        float orbit = radius * (0.35 + 0.6 * pow(max(r.z, 1e-6), 0.3333));
        float tilt = (2.0 * r.x - 1.0) * 0.6;
        float azimuth = FLOCK_TWO_PI * r.y;
        float3 axis = float3(sin(tilt) * cos(azimuth), cos(tilt), sin(tilt) * sin(azimuth));
        float3 e1 = normalize(cross(axis, float3(1.0, 0.0, 0.0)));
        float3 e2 = cross(axis, e1);
        float angle = t * FlockOrbitRate(input.speed, orbit)
            + FLOCK_TWO_PI * FlockFrac(r.x * 7.31 + r.y * 3.17);
        offset = (e1 * cos(angle) + e2 * sin(angle)) * orbit;
    }
    else if (pattern == FLOCK_PATTERN_TORNADO)
    {
        centre = FlockPathPoint(centreAmplitude, driftTheta);
        float height = (2.0 * r.x - 1.0) * 0.8 * radius;
        float baseOrbit = radius * (0.2 + 0.22 * r.y);
        float angle = t * FlockOrbitRate(input.speed, baseOrbit) + FLOCK_TWO_PI * r.z;
        float orbit = baseOrbit * (1.0 + 0.2 * sin(height / max(radius, 0.01) * 3.0 + t * 0.5));
        offset = float3(
            orbit * cos(angle),
            height + 0.05 * radius * sin(t * 0.7 + FLOCK_TWO_PI * r.y),
            orbit * sin(angle));
    }
    else
    {
        centre = FlockPathPoint(centreAmplitude, theta);
        offset = k * (0.8 * radius) + wobble * (0.1155 * radius);
        offset.y *= 0.5;
    }

    return centre + offset;
}

void FlockPose(FlockMotionInput input, float time,
    out float3 position, out float3 forward, out float3 up, out float3 right)
{
    float h = FLOCK_DERIVATIVE_STEP;
    float3 previous = FlockPosition(input, time - h);
    position = FlockPosition(input, time);
    float3 next = FlockPosition(input, time + h);

    float3 velocity = next - previous;
    float3 direction = dot(velocity, velocity) > 1e-10 ? normalize(velocity) : float3(0.0, 0.0, 1.0);

    float3 horizontal = float3(direction.x, 0.0, direction.z);
    horizontal = dot(horizontal, horizontal) > 1e-8 ? normalize(horizontal) : float3(0.0, 0.0, 1.0);
    float pitch = clamp(direction.y, -input.maxPitch, input.maxPitch);
    forward = horizontal * sqrt(1.0 - pitch * pitch) + float3(0.0, pitch, 0.0);
    right = normalize(cross(float3(0.0, 1.0, 0.0), forward));
    up = cross(forward, right);

    float3 acceleration = (next - position * 2.0 + previous) / (h * h);
    float bank = clamp(dot(acceleration, right) * input.bankGain, -FLOCK_MAX_BANK, FLOCK_MAX_BANK);
    float3 bankedUp = up * cos(bank) + right * sin(bank);
    right = cross(bankedUp, forward);
    up = bankedUp;
}

float3 FlockAppendageOffset(float3 p, float4 body, float mode,
    float frequency, float amplitude, float t, float phase, float bodyLength)
{
    float beat = sin(FLOCK_TWO_PI * frequency * t + phase);
    if (mode < 3.5)
    {
        if (abs(body.z - 4.0) < 0.5)
        {
            float step = sin(FLOCK_TWO_PI * frequency * t + phase + (p.x < 0.0 ? 3.1415927 : 0.0));
            return float3(0.0, max(step, 0.0) * 0.05 * bodyLength * body.y, step * amplitude * bodyLength * body.y);
        }
        return float3(0.0, abs(beat) * 0.015 * bodyLength, 0.0);
    }
    if (mode < 4.5)
    {
        float wave = FLOCK_TWO_PI * frequency * t + phase + body.y * 3.0;
        return float3(amplitude * bodyLength * body.y * sin(wave), 0.0,
            0.5 * amplitude * bodyLength * body.y * cos(wave));
    }
    if (mode < 5.5)
    {
        float scale = amplitude * body.y * beat;
        return float3(p.x * scale, -0.03 * bodyLength * body.y * beat, p.z * scale);
    }
    return float3(0.0, 0.0, 0.0);
}

// Body animation in body-local space. body = UV0 (span, axial, part,
// shoulder), animation = UV4 (mode, frequency, amplitude, glide).
void FlockAnimate(inout float3 p, inout float3 n, float4 body, float4 animation,
    float3 random, float t, float bodyLength)
{
    float mode = animation.x;
    float frequency = animation.y;
    float amplitude = animation.z;
    float glide = animation.w;
    float phase = FLOCK_TWO_PI * FlockFrac(random.y * 5.13 + random.z * 2.71);
    bool wing = abs(body.z - FLOCK_PART_WING) < 0.5;

    if (mode >= 2.5)
    {
        p += FlockAppendageOffset(p, body, mode, frequency, amplitude, t, phase, bodyLength);
        return;
    }

    if (mode < 0.5)
    {
        float beat = sin(FLOCK_TWO_PI * frequency * t + phase);
        float flapping = 1.0;
        if (glide > 0.0)
        {
            // Gliding while a slow cycle sits above a threshold, so about
            // `glide` of the time is spent with the wings held still.
            float cycle = sin(FLOCK_TWO_PI * 0.11 * t + FLOCK_TWO_PI * random.x);
            float edge = 1.0 - 2.0 * glide;
            flapping = 1.0 - smoothstep(edge - 0.25, edge + 0.25, cycle);
        }

        if (wing)
        {
            float side = body.x >= 0.0 ? 1.0 : -1.0;
            float shoulder = body.w;
            // Wings held in a shallow dihedral while gliding.
            float angle = amplitude * (beat * flapping + (1.0 - flapping) * 0.15)
                * (0.75 + 0.5 * abs(body.x));
            float c = cos(angle);
            float s = sin(angle);
            float along = abs(p.x) - shoulder;
            float2 rotated = float2(along * c - p.y * s, along * s + p.y * c);
            p.x = side * (shoulder + rotated.x);
            p.y = rotated.y;

            float2 normal = float2(n.x * side, n.y);
            n.x = side * (normal.x * c - normal.y * s);
            n.y = normal.x * s + normal.y * c;
        }

        p.y -= 0.04 * bodyLength * beat * flapping;
    }
    else if (mode < 1.5)
    {
        float axial = body.y;
        float weight = 0.1 + 0.9 * axial * axial;
        p.x += amplitude * bodyLength * weight
            * sin(FLOCK_TWO_PI * (axial * 0.9 - frequency * t) + phase);
    }
    else
    {
        float axial = body.y;
        if (wing)
        {
            p.y += amplitude * bodyLength * abs(body.x)
                * sin(FLOCK_TWO_PI * (axial * 0.7 - frequency * t) + phase);
        }
        else if (axial > 1.0)
        {
            p.x += amplitude * bodyLength * 0.3 * (axial - 1.0)
                * sin(FLOCK_TWO_PI * (axial * 0.9 - frequency * t) + phase);
        }
    }
}

// Per-individual scale in [1 - variance, 1 + variance].
float FlockIndividualScale(float3 random, float variance)
{
    return 1.0 + variance * (2.0 * FlockFrac(random.x * 13.37 + random.y * 7.13) - 1.0);
}

#endif
