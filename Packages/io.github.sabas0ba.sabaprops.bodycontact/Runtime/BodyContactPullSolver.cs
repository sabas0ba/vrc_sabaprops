using UnityEngine;

namespace SabaProps.BodyContact
{
    /// <summary>引っ張りの幾何計算。SDKやコンポーネントの状態を参照しません。</summary>
    public partial class BodyContactPull
    {
        public float SegmentFraction(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 axis = b - a;
            return axis.sqrMagnitude < 1e-8f ? 1f : Mathf.Clamp01(Vector3.Dot(point - a, axis) / axis.sqrMagnitude);
        }

        public bool LeaseIsValid(double now, double heartbeat, float timeout)
        {
            double age = now - heartbeat;
            return age >= -0.5 && age <= Mathf.Max(0.1f, timeout);
        }

        public Vector3 PullVelocity(Vector3 delta, float freeDistance, float response,
            float speedLimit, float acceleration, Vector3 previous, float deltaTime)
        {
            delta.y = 0f;
            float distance = delta.magnitude;
            float excess = distance - Mathf.Max(0f, freeDistance);
            if (excess <= 0f || deltaTime <= 0f) return Vector3.zero;
            Vector3 wanted = delta / distance * Mathf.Min(Mathf.Max(0f, speedLimit), excess / Mathf.Max(0.05f, response));
            previous.y = 0f;
            Vector3 velocity = Vector3.MoveTowards(previous, wanted, Mathf.Max(0f, acceleration) * deltaTime);
            // 遅いフレームでも遊びの範囲を飛び越えません。
            return Vector3.ClampMagnitude(velocity, Mathf.Min(Mathf.Max(0f, speedLimit), excess / deltaTime));
        }

        public Vector3 CombineSteps(Vector3 contactStep, Vector3 pullStep)
        {
            contactStep.y = 0f;
            pullStep.y = 0f;
            if (contactStep.sqrMagnitude > 1e-10f)
            {
                Vector3 normal = contactStep.normalized;
                float opposing = Vector3.Dot(pullStep, normal);
                if (opposing < 0f) pullStep -= normal * opposing;
            }
            return contactStep + pullStep;
        }
    }
}
