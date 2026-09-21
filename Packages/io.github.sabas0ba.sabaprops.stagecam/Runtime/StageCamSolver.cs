using UnityEngine;

namespace SabaProps.StageCam
{
    /// <summary>
    /// StageCamRig の幾何計算。ここにあるものはすべて引数だけから結果を決める関数で、
    /// コンポーネント、シーン、VRChat SDK のいずれにも触れません。
    /// <para>
    /// UdonSharp は UdonSharpBehaviour を継承したクラスしかコンパイルしないため、
    /// ソルバを static なヘルパークラスへ切り出すことができません。代わりに部分クラスに
    /// しています。この宣言には基底型も VRC の using も意図的に書いていないので、
    /// このファイルだけを単独でコンパイルできます。それを利用して
    /// .github/verify/offline が Unity 無しで追従計算そのものを実行します。
    /// </para>
    /// <para>
    /// その前提として、このファイルのメソッドは StageCamRig のフィールドを参照しては
    /// なりません。必要な値はすべて引数で受け取ります。ボーンを読んで Transform を
    /// 動かす側は StageCamRig.cs にあります。
    /// </para>
    /// </summary>
    public partial class StageCamRig
    {
        /// <summary>ワールド座標で方位角を測る軌道。被写体の向きに関係なくアングルが固定されます。</summary>
        public const int FollowWorldOrbit = 0;

        /// <summary>
        /// 被写体の体の向きを基準に方位角を測る軌道。被写体が向きを変えても常に正面や
        /// 真横を維持します。基準に頭ではなく体を使うのは、頭の振りにカメラが追随すると
        /// 見ていられないためです。
        /// </summary>
        public const int FollowBodyOrbit = 1;

        /// <summary>
        /// ボーンが取得できなかったと判定する閾値の 2 乗。VRChat は存在しないボーンに対して
        /// 厳密に Vector3.zero を返すため、原点付近だけを弾けば足ります。被写体がワールド
        /// 原点にちょうど立っている場合は誤検出しますが、その場合もフォールバック先が
        /// 同じ地点を指すので破綻はしません。
        /// </summary>
        private const float SampleEpsilonSquared = 1e-8f;

        // ------------------------------------------------------------------
        // 平滑化
        // ------------------------------------------------------------------

        /// <summary>
        /// フレームレート非依存の指数平滑係数。timeConstant 秒で残差が 1/e になります。
        /// <para>
        /// 残差は exp(-dt / tau) なので、dt を n 分割して n 回適用した結果と、n*dt で
        /// 1 回適用した結果が厳密に一致します。フレームレートによって追従の速さが
        /// 変わらないのはこの性質によるものです。
        /// </para>
        /// </summary>
        private float SmoothingFactor(float timeConstant, float deltaTime)
        {
            if (timeConstant <= 0f)
            {
                return 1f;
            }

            if (deltaTime <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Exp(-deltaTime / timeConstant);
        }

        private Vector3 SmoothTowards(Vector3 current, Vector3 target, float factor)
        {
            return current + (target - current) * factor;
        }

        /// <summary>角度の平滑化。360 度の折り返しを跨いでも最短方向へ寄ります。</summary>
        private float SmoothAngleTowards(float currentDegrees, float targetDegrees, float factor)
        {
            return currentDegrees + Mathf.DeltaAngle(currentDegrees, targetDegrees) * factor;
        }

        /// <summary>
        /// 半径 radius 以内の差を無視します。リモートプレイヤーのボーンはネットワーク補間の
        /// 分だけ細かく震えており、これを入れないとカメラが常時動きます。
        /// <para>
        /// 閾値を跨いだ瞬間に飛ばないよう、超過分だけを残す形にしています。出力と入力の
        /// 距離は常に max(0, |target - current| - radius) です。
        /// </para>
        /// </summary>
        private Vector3 ApplyDeadzone(Vector3 current, Vector3 target, float radius)
        {
            if (radius <= 0f)
            {
                return target;
            }

            Vector3 delta = target - current;
            float distance = delta.magnitude;
            if (distance <= radius)
            {
                return current;
            }

            return current + delta * ((distance - radius) / distance);
        }

        // ------------------------------------------------------------------
        // ボーンのフォールバック
        // ------------------------------------------------------------------

        /// <summary>ボーンが実際に取得できたか。取得できなかった場合は原点が返ります。</summary>
        private bool IsSampledPoint(Vector3 point)
        {
            return point.sqrMagnitude > SampleEpsilonSquared;
        }

        /// <summary>
        /// 最初に取得できた候補を選びます。首や胸は Humanoid でも省略可能で、非 Humanoid
        /// アバターではボーンが 1 つも取れません。fallback にはボーンに依存しない値
        /// （頭のトラッキングデータやプレイヤー原点）を渡してください。
        /// </summary>
        private Vector3 FirstSampledPoint(Vector3 primary, Vector3 secondary, Vector3 tertiary, Vector3 fallback)
        {
            if (IsSampledPoint(primary))
            {
                return primary;
            }

            if (IsSampledPoint(secondary))
            {
                return secondary;
            }

            if (IsSampledPoint(tertiary))
            {
                return tertiary;
            }

            return fallback;
        }

        // ------------------------------------------------------------------
        // 軌道
        // ------------------------------------------------------------------

        /// <summary>
        /// 回転から水平方位角（度）を取り出します。pitch と roll は捨てます。被写体が
        /// 上を向いてもカメラが天地に振られないための処理です。
        /// </summary>
        private float HorizontalYaw(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            if (forward.x * forward.x + forward.z * forward.z < SampleEpsilonSquared)
            {
                // 真上か真下を向いている。方位は forward からは決まらないので up から拾う。
                forward = rotation * Vector3.up;
                if (forward.x * forward.x + forward.z * forward.z < SampleEpsilonSquared)
                {
                    return 0f;
                }
            }

            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>軌道パラメータからカメラ位置を求めます。</summary>
        private Vector3 OrbitPosition(Vector3 targetPoint, float yawDegrees, float pitchDegrees, float distance)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitch);
            Vector3 direction = new Vector3(
                Mathf.Sin(yaw) * horizontal,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * horizontal);
            return targetPoint + direction * distance;
        }

        /// <summary>
        /// 被写体を画面中央に置く回転。
        /// <para>
        /// 軌道上のカメラから見た被写体の方向は、必ず軌道方向の逆になります。そのため
        /// 注視方向は方位を 180 度回し、仰角をそのまま使うだけで求まります。
        /// Quaternion.Euler(pitch, yaw, 0) と同じ合成順で組んでおり、roll は構造的に
        /// ゼロです。LookRotation を使っていないのはこのためで、視線とワールド上方向が
        /// 平行になっても破綻しません。
        /// </para>
        /// トリムは Pickup で構図を直したときに生じる、注視方向からのずれです。
        /// </summary>
        private Quaternion AimRotation(float yawDegrees, float pitchDegrees, float yawTrimDegrees, float pitchTrimDegrees)
        {
            return Quaternion.AngleAxis(yawDegrees + 180f + yawTrimDegrees, Vector3.up)
                 * Quaternion.AngleAxis(pitchDegrees + pitchTrimDegrees, Vector3.right);
        }

        // ------------------------------------------------------------------
        // Pickup 補正（world 姿勢から軌道パラメータへの再ベイク）
        // ------------------------------------------------------------------

        private float BakeDistance(Vector3 targetPoint, Vector3 cameraPosition, float fallbackDistance)
        {
            float distance = (cameraPosition - targetPoint).magnitude;
            return distance > 1e-4f ? distance : fallbackDistance;
        }

        private float BakeYaw(Vector3 targetPoint, Vector3 cameraPosition, float fallbackYawDegrees)
        {
            Vector3 delta = cameraPosition - targetPoint;
            if (delta.x * delta.x + delta.z * delta.z < SampleEpsilonSquared)
            {
                // 被写体の真上か真下。方位は決まらないので直前の値を保ちます。
                return fallbackYawDegrees;
            }

            return Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        }

        private float BakePitch(Vector3 targetPoint, Vector3 cameraPosition, float fallbackPitchDegrees)
        {
            Vector3 delta = cameraPosition - targetPoint;
            float distance = delta.magnitude;
            if (distance <= 1e-4f)
            {
                return fallbackPitchDegrees;
            }

            return Mathf.Asin(Mathf.Clamp(delta.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>掴んだ状態でのカメラの向きと、純粋な注視方向との方位差。</summary>
        private float BakeYawTrim(Quaternion cameraRotation, float orbitYawDegrees)
        {
            return Mathf.DeltaAngle(orbitYawDegrees + 180f, HorizontalYaw(cameraRotation));
        }

        /// <summary>掴んだ状態でのカメラの向きと、純粋な注視方向との仰角差。</summary>
        private float BakePitchTrim(Quaternion cameraRotation, float orbitPitchDegrees)
        {
            Vector3 forward = cameraRotation * Vector3.forward;
            float cameraPitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(orbitPitchDegrees, cameraPitch);
        }

        // ------------------------------------------------------------------
        // 自動フレーミング
        // ------------------------------------------------------------------

        /// <summary>
        /// 被写体の見かけの高さが画面の一定割合を占める距離。0 を返した場合は解が
        /// 決まらなかったということで、呼び出し側は現在の距離を保ちます。
        /// <para>
        /// VRChat はアバターの身長差が大きく、しかも実行中に変わります。距離を固定すると
        /// 同じ設定でも被写体によって画面占有率が変わるため、身長を測って距離側を動かします。
        /// </para>
        /// </summary>
        private float FramingDistance(float subjectHeight, float screenFraction, float verticalFovDegrees)
        {
            if (subjectHeight <= 0f || verticalFovDegrees <= 0f)
            {
                return 0f;
            }

            float halfAngle = Mathf.Clamp(screenFraction, 0.05f, 0.95f) * verticalFovDegrees * 0.5f * Mathf.Deg2Rad;
            return subjectHeight * 0.5f / Mathf.Tan(halfAngle);
        }

        /// <summary>
        /// FramingDistance の逆関数。ある距離のときの画面占有率を返します。
        /// <para>
        /// 自動フレーミング中に Pickup で構図を直したとき、距離をそのまま覚えても次の
        /// フレームで上書きされてしまいます。掴んだ結果は「その被写体をこのくらいの
        /// 大きさで写す」という意図なので、距離ではなく占有率へ焼き戻します。
        /// </para>
        /// </summary>
        private float FramingFraction(float subjectHeight, float distance, float verticalFovDegrees)
        {
            if (subjectHeight <= 0f || distance <= 1e-4f || verticalFovDegrees <= 0f)
            {
                return 0f;
            }

            float halfAngle = Mathf.Atan2(subjectHeight * 0.5f, distance);
            return Mathf.Clamp(halfAngle * Mathf.Rad2Deg * 2f / verticalFovDegrees, 0.05f, 0.95f);
        }

        // ------------------------------------------------------------------
        // 自動カメラワーク（クレーン）
        // ------------------------------------------------------------------

        /// <summary>位相を進めます。戻り値は常に [0, 1) に収まります。</summary>
        private float AdvancePhase(float phase, float periodSeconds, float deltaTime)
        {
            if (periodSeconds <= 0f)
            {
                return phase;
            }

            float next = phase + deltaTime / periodSeconds;
            return next - Mathf.Floor(next);
        }

        /// <summary>
        /// 往復イージング。位相 0 で 0、0.5 で 1、1 で 0 に戻ります。
        /// <para>
        /// 三角波に smoothstep を掛けたもので、折り返し点と周期の境目の両方で微分が
        /// ゼロになります。カメラワークが端で急に切り返さないのはこのためです。
        /// </para>
        /// </summary>
        private float PingPongEase(float phase)
        {
            float wrapped = phase - Mathf.Floor(phase);
            float triangle = wrapped < 0.5f ? wrapped * 2f : (1f - wrapped) * 2f;
            return triangle * triangle * (3f - 2f * triangle);
        }

        /// <summary>
        /// クレーン移動の方位。正面から右手へ回り込む、といった動きをこれで作ります。
        /// 位相はカメラワークの 3 要素で共有するため、振り込みと同時に迫り上がります。
        /// </summary>
        private float CameraWorkYaw(float phase, float baseYawDegrees, float sweepDegrees)
        {
            return baseYawDegrees + sweepDegrees * PingPongEase(phase);
        }

        private float CameraWorkPitch(float phase, float basePitchDegrees, float riseDegrees)
        {
            return basePitchDegrees + riseDegrees * PingPongEase(phase);
        }

        private float CameraWorkDistance(float phase, float baseDistance, float dollyMeters)
        {
            return baseDistance + dollyMeters * PingPongEase(phase);
        }
    }
}
