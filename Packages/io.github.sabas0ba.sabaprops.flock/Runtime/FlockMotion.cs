using UnityEngine;

namespace SabaProps.Flock
{
    /// <summary>
    /// Constants of one individual, as the vertex shader receives them from the
    /// mesh. See <see cref="FlockShaderContract"/> for the channel layout.
    /// </summary>
    public struct FlockMotionInput
    {
        public FlockPattern Pattern;
        /// <summary>Travel speed in metres per second.</summary>
        public float Speed;
        /// <summary>Seconds added to the clock, so that swarms sharing a material do not move in step.</summary>
        public float TimeOffset;
        /// <summary>Radius the group occupies around its centre, in metres.</summary>
        public float ClusterRadius;
        /// <summary>Half extents of the box the swarm stays inside, in object space.</summary>
        public Vector3 Area;
        /// <summary>Body length in metres. Kept as a margin from the area walls.</summary>
        public float BodyLength;
        /// <summary>Number of individuals in the swarm.</summary>
        public float Count;
        /// <summary>Zero-based index of the individual.</summary>
        public float Index;
        /// <summary>Three uniform random values in [0, 1) fixed per individual.</summary>
        public Vector3 Random;
        /// <summary>Largest vertical component of the heading. Keeps fish from pointing straight up.</summary>
        public float MaxPitch;
        /// <summary>Bank angle in radians per metre per second squared of sideways acceleration.</summary>
        public float BankGain;
        /// <summary>Conservative animated body radius on each axis, baked in UV6.</summary>
        public Vector3 BodyMargin;
        public float AnimationFrequency;
    }

    /// <summary>
    /// Reference implementation of the group motion that
    /// <c>SabaFlockMotion.cginc</c> evaluates per vertex.
    /// <para>
    /// The shader is a line-by-line port of this class. Keeping a C# copy lets
    /// the editor size the mesh bounds and draw gizmos, and lets the offline
    /// checks prove that every pattern stays inside its area, stays finite and
    /// turns smoothly. A change here must be mirrored in the shader include.
    /// </para>
    /// <para>
    /// The original group patterns use a group centre
    /// <c>C(t)</c> plus a per-individual offset <c>O(t)</c> with
    /// <c>|O| &lt;= ClusterRadius</c>. The centre stays within
    /// <c>Area - ClusterRadius - BodyLength</c> on each axis, so the individual
    /// stays within <c>Area - BodyLength</c> as long as the cluster radius fits;
    /// <see cref="ClampClusterRadius"/> enforces that.
    /// </para>
    /// </summary>
    public static class FlockMotion
    {
        /// <summary>Time step of the finite differences that give the heading and the bank.</summary>
        public const float DerivativeStep = 0.05f;

        public const float TwoPi = 6.2831853f;

        /// <summary>Largest bank angle in radians.</summary>
        public const float MaxBank = 0.9f;

        /// <summary>Largest angular speed of an orbit in radians per second.</summary>
        public const float MaxOrbitRate = 2.5f;

        /// <summary>Position of the individual at time <paramref name="time"/> in object space.</summary>
        public static Vector3 Position(in FlockMotionInput input, float time)
        {
            float t = time + input.TimeOffset;
            Vector3 area = Max(input.Area, 0.001f);
            float radius = Mathf.Max(input.ClusterRadius, 0f);
            float length = Mathf.Max(input.BodyLength, 0f);
            Vector3 r = input.Random;

            if (input.Pattern == FlockPattern.Anchored)
            {
                return input.Count < 1.5f ? Vector3.zero
                    : Scale(r * 2f - Vector3.one, Max(area - Vector3.one * length, 0f));
            }

            if (input.Pattern == FlockPattern.Wander)
            {
                return Wander(Max(area - Margin(input), 0f), input.Speed, r, t);
            }
            if (input.Pattern == FlockPattern.FreeFlight)
                return Wander(Max(area - Margin(input), 0f), input.Speed, r, t);
            if (input.Pattern == FlockPattern.Jet)
                return Wander(Max(area - Margin(input), 0f), input.Speed, r, JetClock(t, input.AnimationFrequency, r));
            if (input.Pattern == FlockPattern.Float)
                return FloatDrift(Max(area - Margin(input), 0f), length, input.Speed, r, t);
            if (input.Pattern == FlockPattern.OctopusDrift)
                return FloatDrift(Max(area - Margin(input), 0f), length, Mathf.Min(input.Speed * 10f, length * 5f), r,
                    OctopusClock(t, input.AnimationFrequency, r));
            if (input.Pattern == FlockPattern.FloorGlide)
                return FloorGlide(Max(area - Margin(input), 0f), length, input.Speed, r, t);

            Vector3 centreAmplitude = Max(area - Vector3.one * (radius + length), 0f);
            float angularSpeed = PathAngularSpeed(centreAmplitude, input.Speed);
            float theta = t * angularSpeed;

            Vector3 centre;
            Vector3 offset;
            Vector3 k = UnitBallPoint(r);
            Vector3 wobble = Wobble(r, t, input.Speed, radius);

            switch (input.Pattern)
            {
                case FlockPattern.Murmuration:
                {
                    centre = PathPoint(centreAmplitude, theta);
                    // Both rates are capped relative to the travel speed, so the
                    // folding never outruns the group and reverses a heading.
                    float relative = input.Speed / Mathf.Max(radius, 0.01f);
                    float slow = t * Mathf.Min(0.35f, 0.5f * relative);
                    Vector3 shape = new Vector3(
                        0.55f + 0.45f * Mathf.Sin(slow * 0.61f),
                        0.35f + 0.25f * Mathf.Sin(slow * 0.47f + 2.1f),
                        0.55f + 0.45f * Mathf.Sin(slow * 0.37f + 4.2f));
                    float wave = Mathf.Sin((k.x * 3f + k.y + k.z * 2f) * 1.5f - t * Mathf.Min(1.3f, 1.2f * relative));
                    offset = Scale(k, shape) * (0.75f * radius)
                        + new Vector3(0.7071f, 0.3536f, 0.6124f) * (0.25f * radius * wave);
                    break;
                }

                case FlockPattern.VFormation:
                {
                    centre = PathPoint(centreAmplitude, theta);
                    PathFrame(centreAmplitude, theta, angularSpeed, out Vector3 forward, out Vector3 right, out Vector3 up);
                    float ranks = Mathf.Max(Mathf.Floor(input.Count * 0.5f), 1f);
                    float spacing = radius / (ranks + 0.2f);
                    float rank = Mathf.Floor((input.Index + 1f) * 0.5f);
                    float side = Frac(input.Index * 0.5f) > 0.25f ? -1f : 1f;
                    offset = forward * (-0.8192f * rank * spacing)
                        + right * (0.5736f * side * rank * spacing)
                        + up * (0.05f * rank * spacing)
                        + wobble * (0.06f * spacing);
                    break;
                }

                case FlockPattern.Thermal:
                {
                    centre = PathPoint(centreAmplitude, t * PathAngularSpeed(centreAmplitude, DriftSpeed(input.Speed, radius)));
                    float orbit = radius * (0.3f + 0.5f * r.x);
                    float angle = t * OrbitRate(input.Speed, orbit) + TwoPi * r.y;
                    float climb = 0.6f * radius * Mathf.Sin(t * 0.05f * input.Speed / Mathf.Max(radius, 1f) + TwoPi * r.z);
                    offset = new Vector3(orbit * Mathf.Cos(angle), climb, orbit * Mathf.Sin(angle));
                    break;
                }

                case FlockPattern.Stream:
                {
                    centre = PathPoint(centreAmplitude, theta);
                    PathFrame(centreAmplitude, theta, angularSpeed, out Vector3 forward, out Vector3 right, out Vector3 up);
                    offset = forward * ((2f * r.x - 1f) * 0.85f * radius)
                        + right * ((2f * r.y - 1f) * 0.3f * radius)
                        + up * ((2f * r.z - 1f) * 0.2f * radius)
                        + wobble * (0.03f * radius);
                    break;
                }

                case FlockPattern.BaitBall:
                {
                    centre = PathPoint(centreAmplitude, t * PathAngularSpeed(centreAmplitude, DriftSpeed(input.Speed, radius)));
                    float orbit = radius * (0.35f + 0.6f * Mathf.Pow(Mathf.Max(r.z, 1e-6f), 0.3333f));
                    float tilt = (2f * r.x - 1f) * 0.6f;
                    float azimuth = TwoPi * r.y;
                    Vector3 axis = new Vector3(
                        Mathf.Sin(tilt) * Mathf.Cos(azimuth),
                        Mathf.Cos(tilt),
                        Mathf.Sin(tilt) * Mathf.Sin(azimuth));
                    Vector3 e1 = Vector3.Cross(axis, Vector3.right).normalized;
                    Vector3 e2 = Vector3.Cross(axis, e1);
                    float angle = t * OrbitRate(input.Speed, orbit)
                        + TwoPi * Frac(r.x * 7.31f + r.y * 3.17f);
                    offset = (e1 * Mathf.Cos(angle) + e2 * Mathf.Sin(angle)) * orbit;
                    break;
                }

                case FlockPattern.Tornado:
                {
                    centre = PathPoint(centreAmplitude, t * PathAngularSpeed(centreAmplitude, DriftSpeed(input.Speed, radius)));
                    float height = (2f * r.x - 1f) * 0.8f * radius;
                    float baseOrbit = radius * (0.2f + 0.22f * r.y);
                    // The rate comes from the unmodulated orbit: a rate that
                    // varied with time would be multiplied by the clock and
                    // swing the phase back and forth.
                    float angle = t * OrbitRate(input.Speed, baseOrbit) + TwoPi * r.z;
                    float orbit = baseOrbit * (1f + 0.2f * Mathf.Sin(height / Mathf.Max(radius, 0.01f) * 3f + t * 0.5f));
                    offset = new Vector3(
                        orbit * Mathf.Cos(angle),
                        height + 0.05f * radius * Mathf.Sin(t * 0.7f + TwoPi * r.y),
                        orbit * Mathf.Sin(angle));
                    break;
                }

                default:
                {
                    centre = PathPoint(centreAmplitude, theta);
                    offset = k * (0.8f * radius) + wobble * (0.1155f * radius);
                    offset.y *= 0.5f;
                    break;
                }
            }

            return centre + offset;
        }

        /// <summary>Reference displacement for walking, tentacles and bell pulsation.</summary>
        public static Vector3 AppendageOffset(Vector3 p, Vector4 body, float mode,
            float frequency, float amplitude, float t, float phase, float bodyLength)
        {
            float beat = Mathf.Sin(TwoPi * frequency * t + phase);
            if (mode > 7.5f)
            {
                float wave = TwoPi * frequency * t + phase + body.y * 3f;
                float stroke = OctopusStroke(t, frequency, phase);
                return new Vector3(-0.08f * p.x, 0.12f * p.y, -0.08f * p.z) * stroke
                    + body.y * new Vector3(-0.42f * p.x, -0.18f * bodyLength, -0.42f * p.z) * stroke
                    + amplitude * bodyLength * body.y * (1f - stroke) * new Vector3(Mathf.Sin(wave),
                        0.35f * Mathf.Sin(wave * 0.7f), 0.5f * Mathf.Cos(wave));
            }
            if (mode > 6.5f)
            {
                float contraction = Mathf.Cos(TwoPi * frequency * t + phase);
                return new Vector3(0.08f * p.x, 0.08f * p.y, -0.04f * p.z) * contraction
                    + new Vector3(amplitude * bodyLength * body.y * beat, 0f, 0f);
            }
            if (mode < 3.5f)
            {
                if (Mathf.Abs(body.z - 4f) < 0.5f)
                {
                    float step = Mathf.Sin(TwoPi * frequency * t + phase + (p.x < 0f ? 3.1415927f : 0f));
                    return new Vector3(0f, Mathf.Max(step, 0f) * 0.05f * bodyLength * body.y, step * amplitude * bodyLength * body.y);
                }
                return new Vector3(0f, Mathf.Abs(beat) * 0.015f * bodyLength, 0f);
            }
            if (mode < 4.5f)
            {
                float wave = TwoPi * frequency * t + phase + body.y * 3f;
                return new Vector3(amplitude * bodyLength * body.y * Mathf.Sin(wave), 0f,
                    0.5f * amplitude * bodyLength * body.y * Mathf.Cos(wave));
            }
            if (mode < 5.5f)
            {
                float scale = amplitude * body.y * beat;
                return new Vector3(p.x * scale, -0.03f * bodyLength * body.y * beat, p.z * scale);
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Position and orientation at <paramref name="time"/>. The heading and
        /// the bank come from central differences of <see cref="Position"/>, as
        /// in the shader.
        /// </summary>
        public static void Pose(
            in FlockMotionInput input,
            float time,
            out Vector3 position,
            out Vector3 forward,
            out Vector3 up,
            out Vector3 right)
        {
            float h = DerivativeStep;
            Vector3 previous = Position(input, time - h);
            position = Position(input, time);
            Vector3 next = Position(input, time + h);

            if (input.Pattern == FlockPattern.OctopusDrift)
            {
                // The mantle apex is local +Y. Align it with propulsion, including dives.
                Vector3 travel = next - previous;
                up = travel.sqrMagnitude > 1e-10f ? travel.normalized : Vector3.up;
                right = Vector3.Cross(Vector3.up, up);
                if (right.sqrMagnitude < 1e-8f) right = Vector3.Cross(Vector3.forward, up);
                right = right.normalized;
                forward = Vector3.Cross(right, up);
                return;
            }
            if (input.Pattern == FlockPattern.Float)
            {
                float yaw = TwoPi * input.Random.x + 0.15f * Mathf.Sin((time + input.TimeOffset)
                    * Mathf.Min(input.Speed / Mathf.Max(input.BodyLength, 0.01f) * 0.015f, 0.6f) + TwoPi * input.Random.z);
                forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                up = Vector3.up;
                right = Vector3.Cross(up, forward);
                return;
            }

            Vector3 velocity = next - previous;
            Vector3 direction = velocity.sqrMagnitude > 1e-10f ? velocity.normalized : Vector3.forward;

            // Keep the compass heading and limit the climb: the vertical
            // component becomes the sine of the pitch.
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            horizontal = horizontal.sqrMagnitude > 1e-8f ? horizontal.normalized : Vector3.forward;
            float pitch = Mathf.Clamp(direction.y, -input.MaxPitch, input.MaxPitch);
            forward = horizontal * Mathf.Sqrt(1f - pitch * pitch) + Vector3.up * pitch;
            right = Vector3.Cross(Vector3.up, forward).normalized;
            up = Vector3.Cross(forward, right);

            Vector3 acceleration = (next - position * 2f + previous) / (h * h);
            float bank = Mathf.Clamp(Vector3.Dot(acceleration, right) * input.BankGain, -MaxBank, MaxBank);
            Vector3 bankedUp = up * Mathf.Cos(bank) + right * Mathf.Sin(bank);
            right = Vector3.Cross(bankedUp, forward);
            up = bankedUp;
        }

        /// <summary>
        /// Cluster radius that gives <paramref name="count"/> individuals of
        /// size <paramref name="spacing"/> roughly one spacing of room each.
        /// </summary>
        public static float AutoClusterRadius(FlockPattern pattern, int count, float spacing)
        {
            float n = Mathf.Max(count, 1);
            switch (pattern)
            {
                case FlockPattern.Murmuration:
                    return 1.2f * spacing * 0.62f * Mathf.Pow(n, 0.3333f);
                case FlockPattern.VFormation:
                    return (Mathf.Floor(n * 0.5f) + 0.2f) * 1.2f * spacing;
                case FlockPattern.Thermal:
                    return Mathf.Max(6f * spacing * 0.62f * Mathf.Pow(n, 0.3333f), 3f * spacing);
                case FlockPattern.Stream:
                    return 1.3f * spacing * Mathf.Pow(n / 0.408f, 0.3333f);
                case FlockPattern.BaitBall:
                    return 1.0f * spacing * 0.62f * Mathf.Pow(n, 0.3333f) * 1.4f;
                case FlockPattern.Tornado:
                    return 1.2f * spacing * Mathf.Pow(n / 0.887f, 0.3333f);
                case FlockPattern.Wander:
                    return 0f;
                default:
                    return 2.5f * spacing * 0.62f * Mathf.Pow(n, 0.3333f);
            }
        }

        /// <summary>
        /// Largest cluster radius for which every individual stays inside the
        /// area with one body length to spare.
        /// </summary>
        public static float ClampClusterRadius(float radius, Vector3 area, float bodyLength)
        {
            // Vertically the cluster may fill the area. Horizontally it keeps
            // half the room for the centre's path: a path squeezed to zero on
            // one axis degenerates into a line, and the group would reverse on
            // the spot at each end.
            float vertical = area.y - bodyLength;
            float horizontal = 0.5f * (Mathf.Min(area.x, area.z) - bodyLength);
            return Mathf.Clamp(radius, 0f, Mathf.Max(Mathf.Min(vertical, horizontal), 0f));
        }

        // ------------------------------------------------------------------
        // Shared pieces. Each has a same-named function in SabaFlockMotion.cginc.
        // ------------------------------------------------------------------

        /// <summary>Angular speed along the figure-eight so the centre moves at about <paramref name="speed"/>.</summary>
        public static float PathAngularSpeed(Vector3 amplitude, float speed)
        {
            float pathScale = Mathf.Max(0.7f * (amplitude.x + 2f * amplitude.z), 0.25f);
            return speed / pathScale;
        }

        /// <summary>Figure-eight in the horizontal plane with a slow vertical drift.</summary>
        public static Vector3 PathPoint(Vector3 amplitude, float theta)
        {
            return new Vector3(
                amplitude.x * Mathf.Sin(theta),
                amplitude.y * Mathf.Sin(theta * 0.73f + 1.3f),
                amplitude.z * Mathf.Sin(theta * 2f));
        }

        /// <summary>Orthonormal frame that follows the tangent of <see cref="PathPoint"/>.</summary>
        public static void PathFrame(
            Vector3 amplitude, float theta, float angularSpeed,
            out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            Vector3 tangent = new Vector3(
                amplitude.x * Mathf.Cos(theta),
                0.73f * amplitude.y * Mathf.Cos(theta * 0.73f + 1.3f),
                2f * amplitude.z * Mathf.Cos(theta * 2f)) * (angularSpeed < 0f ? -1f : 1f);
            Vector3 horizontal = new Vector3(tangent.x, 0f, tangent.z);
            forward = horizontal.sqrMagnitude > 1e-10f ? horizontal.normalized : Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward).normalized;
            up = Vector3.up;
        }

        /// <summary>Point in the unit ball, uniform in volume, from three uniform randoms.</summary>
        public static Vector3 UnitBallPoint(Vector3 r)
        {
            float z = 2f * r.x - 1f;
            float phi = TwoPi * r.y;
            float ring = Mathf.Sqrt(Mathf.Max(1f - z * z, 0f));
            float scale = Mathf.Pow(Mathf.Max(r.z, 1e-6f), 0.3333f);
            return new Vector3(ring * Mathf.Cos(phi), z, ring * Mathf.Sin(phi)) * scale;
        }

        /// <summary>Slow per-individual oscillation. Each component is in [-1, 1].</summary>
        public static Vector3 Wobble(Vector3 r, float t, float speed, float radius)
        {
            float rate = Mathf.Clamp(speed / Mathf.Max(radius, 0.1f) * 0.3f, 0.2f, 2f);
            return new Vector3(
                Mathf.Sin(t * rate * (0.5f + 0.6f * r.y) + TwoPi * r.x),
                Mathf.Sin(t * rate * (0.4f + 0.5f * r.z) + TwoPi * r.y),
                Mathf.Sin(t * rate * (0.6f + 0.4f * r.x) + TwoPi * r.z));
        }

        /// <summary>
        /// Independent path of one individual inside the area, as a fish in a
        /// tank: the animated body margin is subtracted once from its physical half extents.
        /// </summary>
        public static Vector3 Margin(in FlockMotionInput input)
        {
            return input.BodyMargin.sqrMagnitude > 0f ? input.BodyMargin : Vector3.one * input.BodyLength;
        }

        /// <summary>Wide individual paths with slowly varying radii, centre and speed.</summary>
        public static Vector3 Wander(Vector3 room, float speed, Vector3 r, float t)
        {
            float a = room.x * (0.68f + 0.14f * r.x);
            float b = room.z * (0.68f + 0.14f * r.z);
            float direction = r.y > 0.5f ? 1f : -1f;
            float aspect = Mathf.Min(a, b) / Mathf.Max(Mathf.Max(a, b), 0.01f);
            float orbitRate = Mathf.Min(speed / Mathf.Max(0.5f * (a + b), 0.01f), 1.2f * aspect);
            float clock = orbitRate * t;
            float angle = direction * (clock + 0.12f * Mathf.Sin(clock * 0.37f + TwoPi * r.x)) + TwoPi * r.y;
            float slowest = orbitRate * Mathf.Min(a, b);
            float drift = Mathf.Max(Mathf.Max(room.x - a, room.z - b), 0.01f);
            float driftRate = 0.08f * slowest / drift;
            float radiusX = a * (1f + 0.08f * Mathf.Sin(clock * 0.13f + TwoPi * r.z));
            float radiusZ = b * (1f + 0.08f * Mathf.Sin(clock * 0.17f + TwoPi * r.x));
            return new Vector3(
                (room.x - 1.08f * a) * Mathf.Sin(t * driftRate + TwoPi * r.z) + radiusX * Mathf.Cos(angle),
                room.y * (0.7f * Mathf.Sin(clock * 0.19f + TwoPi * r.z) + 0.3f * Mathf.Sin(clock * 0.071f + TwoPi * r.x)),
                (room.z - 1.08f * b) * Mathf.Sin(t * driftRate * 0.7f + TwoPi * r.x) + radiusZ * Mathf.Sin(angle));
        }

        public static float JetClock(float t, float frequency, Vector3 r)
        {
            float rate = TwoPi * Mathf.Max(frequency, 0.05f);
            return t - 0.65f * Mathf.Sin(rate * t + TwoPi * Frac(r.y * 5.13f + r.z * 2.71f)) / rate;
        }

        /// <summary>Smooth arm flexion pulse, shared with the propulsion clock.</summary>
        public static float OctopusStroke(float t, float frequency, float phase)
        {
            float stroke = 0.5f + 0.5f * Mathf.Cos(TwoPi * frequency * t + phase);
            return stroke * stroke * stroke * stroke;
        }

        /// <summary>Integrated response of one propulsion harmonic to drag with a 0.18-cycle time constant.</summary>
        public static float CoastTerm(float harmonic, float amplitude, float phase, float rate)
        {
            float drag = TwoPi * 0.18f * harmonic;
            float angle = harmonic * phase;
            return amplitude * (Mathf.Sin(angle) - drag * Mathf.Cos(angle))
                / (harmonic * rate * (1f + drag * drag));
        }

        /// <summary>Analytic stroke and coast clock: positive velocity, impulse followed by drag.</summary>
        public static float OctopusClock(float t, float frequency, Vector3 r)
        {
            if (frequency <= 0f) return t;
            float rate = TwoPi * frequency;
            float phase = rate * t + TwoPi * Frac(r.y * 5.13f + r.z * 2.71f);
            return t + CoastTerm(1f, 1.28f, phase, rate) + CoastTerm(2f, 0.64f, phase, rate)
                + CoastTerm(3f, 0.18285714f, phase, rate) + CoastTerm(4f, 0.02285714f, phase, rate);
        }

        public static Vector3 FloorGlide(Vector3 room, float bodyLength, float speed, Vector3 r, float t)
        {
            Vector3 position = Wander(room, speed, r, t);
            position.y = 0.6f * position.y - 0.3f * room.y;
            return position;
        }

        public static Vector3 DriftPoint(float index, Vector3 r)
        {
            Vector3 p = new Vector3(Frac(index * 0.1031f + r.x), Frac(index * 0.11369f + r.y), Frac(index * 0.13787f + r.z));
            return new Vector3(Frac(p.x * (p.y + 19.19f)), Frac(p.y * (p.z + 23.23f)), Frac(p.z * (p.x + 29.29f))) * 2f - Vector3.one;
        }

        /// <summary>Cubic B-spline of seeded waypoints; all weights are nonnegative.</summary>
        public static Vector3 FloatDrift(Vector3 room, float bodyLength, float speed, Vector3 r, float t)
        {
            float clock = t * speed / Mathf.Max(bodyLength, 0.01f) / (60f + 90f * r.y);
            float index = Mathf.Floor(clock);
            float u = Frac(clock), v = 1f - u;
            Vector3 point = DriftPoint(index - 1f, r) * (v * v * v)
                + DriftPoint(index, r) * (3f * u * u * u - 6f * u * u + 4f)
                + DriftPoint(index + 1f, r) * (-3f * u * u * u + 3f * u * u + 3f * u + 1f)
                + DriftPoint(index + 2f, r) * (u * u * u);
            return Scale(point / 6f, room);
        }

        /// <summary>
        /// Angular speed of a circular orbit, capped at
        /// <see cref="MaxOrbitRate"/> so a small orbit does not spin the body
        /// faster than the eye can follow.
        /// </summary>
        public static float OrbitRate(float speed, float orbit)
        {
            return Mathf.Min(speed / Mathf.Max(orbit, 0.01f), MaxOrbitRate);
        }

        /// <summary>
        /// Speed at which the centre of an orbiting group drifts. Held below a
        /// third of the slowest orbit's tangential speed (the smallest orbit is
        /// 0.16 of the cluster radius), so the drift never cancels an
        /// individual's own motion and flips its heading.
        /// </summary>
        public static float DriftSpeed(float speed, float radius)
        {
            return 0.3f * Mathf.Min(speed, MaxOrbitRate * 0.16f * radius);
        }

        public static float Frac(float v) => v - Mathf.Floor(v);

        private static Vector3 Max(Vector3 v, float floor) =>
            new Vector3(Mathf.Max(v.x, floor), Mathf.Max(v.y, floor), Mathf.Max(v.z, floor));

        private static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }
}
