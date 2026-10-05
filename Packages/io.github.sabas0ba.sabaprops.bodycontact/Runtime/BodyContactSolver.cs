using UnityEngine;

namespace SabaProps.BodyContact
{
    /// <summary>
    /// BodyContactSystem の幾何計算と判定規則。ここにあるものはすべて引数だけから結果を決める
    /// 関数で、コンポーネント、シーン、VRChat SDK のいずれにも触れません。
    /// <para>
    /// UdonSharp は UdonSharpBehaviour を継承したクラスしかコンパイルしないため、部分クラスに
    /// しています。この宣言には基底型も VRC の using も書いていないので、このファイルだけを
    /// 単独でコンパイルできます。.github/verify/offline/OfflineBodyContactTests.cs が Unity 無しで
    /// 実行します。
    /// </para>
    /// <para>
    /// その前提として、このファイルのメソッドは BodyContactSystem のフィールドを参照しては
    /// なりません。必要な値はすべて引数で受け取ります。
    /// </para>
    /// </summary>
    public partial class BodyContactSystem
    {
        // ------------------------------------------------------------------
        // 関節。プレイヤーではボーンから、ダミーでは標準体型から求めます。
        // ------------------------------------------------------------------

        public const int JointHips = 0;
        public const int JointNeck = 1;

        /// <summary>頭の球の中心。Head ボーンそのものではなく、そこから頭頂側へずらした点です。</summary>
        public const int JointHead = 2;
        public const int JointLeftShoulder = 3;
        public const int JointLeftElbow = 4;
        public const int JointLeftWrist = 5;
        public const int JointRightShoulder = 6;
        public const int JointRightElbow = 7;
        public const int JointRightWrist = 8;
        public const int JointLeftHip = 9;
        public const int JointLeftKnee = 10;
        public const int JointLeftAnkle = 11;
        public const int JointRightHip = 12;
        public const int JointRightKnee = 13;
        public const int JointRightAnkle = 14;
        public const int JointCount = 15;

        // ------------------------------------------------------------------
        // 部位。相手側の形状で、2 つの関節を結ぶカプセルです。
        // ------------------------------------------------------------------

        public const int PartHead = 0;
        public const int PartTorso = 1;
        public const int PartLeftUpperArm = 2;
        public const int PartLeftForearm = 3;
        public const int PartRightUpperArm = 4;
        public const int PartRightForearm = 5;
        public const int PartLeftThigh = 6;
        public const int PartLeftShin = 7;
        public const int PartRightThigh = 8;
        public const int PartRightShin = 9;
        public const int PartCount = 10;

        // ------------------------------------------------------------------
        // プローブ。自分側の形状で、関節に置く球です。
        // ------------------------------------------------------------------

        public const int ProbeHead = 0;
        public const int ProbeChest = 1;
        public const int ProbeHips = 2;
        public const int ProbeLeftHand = 3;
        public const int ProbeRightHand = 4;
        public const int ProbeLeftFoot = 5;
        public const int ProbeRightFoot = 6;
        public const int ProbeCount = 7;

        /// <summary>頭と体幹だけを数えたプローブ数。Desktop ではこの範囲だけを判定します。</summary>
        public const int CoreProbeCount = 3;

        /// <summary>
        /// ボーンが取得できなかったと判定する閾値の 2 乗。VRChat は存在しないボーンに対して
        /// 厳密に Vector3.zero を返します。
        /// </summary>
        private const float MissingEpsilonSquared = 1e-8f;

        /// <summary>
        /// 分離方向の水平成分がこれ未満の接触は扱いません。動かせるのは水平方向だけなので、
        /// ほぼ真上や真下からの接触は水平移動では解消できないためです。
        /// </summary>
        private const float MinimumHorizontalShare = 0.3f;

        /// <summary>体幹同士の接触で、静止時に均等へ寄せるための速度の下駄 (m/s)。</summary>
        private const float SpeedBias = 0.05f;

        private const float MinimumEyeHeight = 0.2f;
        private const float MaximumEyeHeight = 5f;

        // ------------------------------------------------------------------
        // 体型
        // ------------------------------------------------------------------

        /// <summary>寸法の基準にする目線の高さ。極端な値は丸めます。</summary>
        private float ClampEyeHeight(float eyeHeight)
        {
            return Mathf.Clamp(eyeHeight, MinimumEyeHeight, MaximumEyeHeight);
        }

        /// <summary>ボーンが存在しない場合に VRChat が返す値かどうか。</summary>
        private bool IsMissing(Vector3 position)
        {
            return position.sqrMagnitude < MissingEpsilonSquared;
        }

        /// <summary>
        /// 目線の高さを 1 とした標準体型の関節位置。足元が原点、+Z が正面、-X が左です。
        /// ダミーの形状と、ボーンを取得できないアバターの体幹の代替に使います。
        /// </summary>
        public Vector3 StandardJoint(int joint)
        {
            if (joint == JointHips) return new Vector3(0f, 0.59f, 0f);
            if (joint == JointNeck) return new Vector3(0f, 0.89f, 0f);
            if (joint == JointHead) return new Vector3(0f, 0.98f, 0f);
            if (joint == JointLeftShoulder) return new Vector3(-0.11f, 0.86f, 0f);
            if (joint == JointLeftElbow) return new Vector3(-0.20f, 0.71f, 0f);
            if (joint == JointLeftWrist) return new Vector3(-0.26f, 0.56f, 0.03f);
            if (joint == JointRightShoulder) return new Vector3(0.11f, 0.86f, 0f);
            if (joint == JointRightElbow) return new Vector3(0.20f, 0.71f, 0f);
            if (joint == JointRightWrist) return new Vector3(0.26f, 0.56f, 0.03f);
            if (joint == JointLeftHip) return new Vector3(-0.055f, 0.575f, 0f);
            if (joint == JointLeftKnee) return new Vector3(-0.06f, 0.31f, 0.01f);
            if (joint == JointLeftAnkle) return new Vector3(-0.06f, 0.055f, 0f);
            if (joint == JointRightHip) return new Vector3(0.055f, 0.575f, 0f);
            if (joint == JointRightKnee) return new Vector3(0.06f, 0.31f, 0.01f);
            if (joint == JointRightAnkle) return new Vector3(0.06f, 0.055f, 0f);
            return Vector3.zero;
        }

        /// <summary>頭と体幹の関節かどうか。これらはボーンが無くても標準体型で代替します。</summary>
        private bool JointIsCore(int joint)
        {
            return joint <= JointHead;
        }

        /// <summary>部位のカプセルの始点側の関節。</summary>
        public int PartJointA(int part)
        {
            if (part == PartHead) return JointHead;
            if (part == PartTorso) return JointHips;
            if (part == PartLeftUpperArm) return JointLeftShoulder;
            if (part == PartLeftForearm) return JointLeftElbow;
            if (part == PartRightUpperArm) return JointRightShoulder;
            if (part == PartRightForearm) return JointRightElbow;
            if (part == PartLeftThigh) return JointLeftHip;
            if (part == PartLeftShin) return JointLeftKnee;
            if (part == PartRightThigh) return JointRightHip;
            return JointRightKnee;
        }

        /// <summary>部位のカプセルの終点側の関節。頭は球なので始点と同じです。</summary>
        public int PartJointB(int part)
        {
            if (part == PartHead) return JointHead;
            if (part == PartTorso) return JointNeck;
            if (part == PartLeftUpperArm) return JointLeftElbow;
            if (part == PartLeftForearm) return JointLeftWrist;
            if (part == PartRightUpperArm) return JointRightElbow;
            if (part == PartRightForearm) return JointRightWrist;
            if (part == PartLeftThigh) return JointLeftKnee;
            if (part == PartLeftShin) return JointLeftAnkle;
            if (part == PartRightThigh) return JointRightKnee;
            return JointRightAnkle;
        }

        /// <summary>目線の高さを 1 とした部位の半径。</summary>
        public float PartUnitRadius(int part)
        {
            if (part == PartHead) return 0.065f;
            if (part == PartTorso) return 0.095f;
            if (part == PartLeftUpperArm || part == PartRightUpperArm) return 0.032f;
            if (part == PartLeftForearm || part == PartRightForearm) return 0.026f;
            if (part == PartLeftThigh || part == PartRightThigh) return 0.048f;
            return 0.035f;
        }

        /// <summary>目線の高さを 1 としたプローブの半径。</summary>
        private float ProbeUnitRadius(int probe)
        {
            if (probe == ProbeHead) return 0.065f;
            if (probe == ProbeChest || probe == ProbeHips) return 0.09f;
            if (probe == ProbeLeftHand || probe == ProbeRightHand) return 0.03f;
            return 0.035f;
        }

        /// <summary>腕と脚の部位かどうか。</summary>
        private bool PartIsLimb(int part)
        {
            return part >= PartLeftUpperArm;
        }

        /// <summary>手と足のプローブかどうか。</summary>
        private bool ProbeIsLimb(int probe)
        {
            return probe >= CoreProbeCount;
        }

        /// <summary>root に立つ標準体型の関節を書き込みます。すべての関節が有効になります。</summary>
        private void FillStandardJoints(
            Vector3 root, Quaternion rotation, float eyeHeight, Vector3[] joints, bool[] jointValid)
        {
            for (int joint = 0; joint < JointCount; joint++)
            {
                joints[joint] = root + rotation * (StandardJoint(joint) * eyeHeight);
                jointValid[joint] = true;
            }
        }

        /// <summary>
        /// 関節から相手 1 体分の部位を partOffset 以降へ書き込みます。
        /// 両端の関節のどちらかが無い部位は半径を 0 にし、以降の判定から除外します。
        /// </summary>
        private void FillParts(
            Vector3[] joints, bool[] jointValid, float eyeHeight, float radiusScale,
            Vector3[] partA, Vector3[] partB, float[] partRadii, int partOffset)
        {
            for (int part = 0; part < PartCount; part++)
            {
                int a = PartJointA(part);
                int b = PartJointB(part);
                int index = partOffset + part;
                partA[index] = joints[a];
                partB[index] = joints[b];
                partRadii[index] = jointValid[a] && jointValid[b]
                    ? PartUnitRadius(part) * eyeHeight * radiusScale
                    : 0f;
            }
        }

        /// <summary>関節から自分の判定点を書き込みます。関節が無い判定点は半径を 0 にします。</summary>
        private void FillProbes(
            Vector3[] joints, bool[] jointValid, float eyeHeight, float radiusScale,
            Vector3[] probePositions, float[] probeRadii)
        {
            for (int probe = 0; probe < ProbeCount; probe++)
            {
                Vector3 position;
                bool valid;
                if (probe == ProbeChest)
                {
                    // 胸のボーンは省略できるため、腰と首の間の点で代用します。
                    position = Vector3.Lerp(joints[JointHips], joints[JointNeck], 0.65f);
                    valid = jointValid[JointHips] && jointValid[JointNeck];
                }
                else
                {
                    int joint = ProbeJoint(probe);
                    position = joints[joint];
                    valid = jointValid[joint];
                }

                probePositions[probe] = position;
                probeRadii[probe] = valid ? ProbeUnitRadius(probe) * eyeHeight * radiusScale : 0f;
            }
        }

        /// <summary>判定点を置く関節。胸は 2 つの関節の間に置くため、この対応には含みません。</summary>
        private int ProbeJoint(int probe)
        {
            if (probe == ProbeHead) return JointHead;
            if (probe == ProbeHips) return JointHips;
            if (probe == ProbeLeftHand) return JointLeftWrist;
            if (probe == ProbeRightHand) return JointRightWrist;
            if (probe == ProbeLeftFoot) return JointLeftAnkle;
            return JointRightAnkle;
        }

        // ------------------------------------------------------------------
        // 退く側の規則
        // ------------------------------------------------------------------

        /// <summary>
        /// 体幹同士の接触で自分が負担する割合。動いている側が退きます。
        /// <para>
        /// 双方のクライアントが同じ式を相手と自分を入れ替えて評価するため、2 人の負担の合計は
        /// 1 になります。立ち止まっている相手へ歩いて入った場合は、歩いた側がほぼ全量を負担し、
        /// 相手は動きません。双方が静止していれば半分ずつです。
        /// </para>
        /// </summary>
        private float CoreYieldShare(float ownSpeed, float otherSpeed)
        {
            float own = Mathf.Max(0f, ownSpeed) + SpeedBias;
            float other = Mathf.Max(0f, otherSpeed) + SpeedBias;
            return own / (own + other);
        }

        /// <summary>
        /// 自分のプローブが相手の部位へ侵入したときに、自分が退く割合。0 なら反応しません。
        /// <para>
        /// 手足が相手の頭や体幹へ侵入した場合は、手足の持ち主が全量を退きます。逆に相手の手足が
        /// 自分の体幹へ侵入した場合、自分は動きません。他人を押して動かすことを許さないための
        /// 規則です。手足同士は反応させません。
        /// </para>
        /// </summary>
        private float YieldWeight(bool probeIsLimb, bool partIsLimb, bool targetIsStatic, float coreYieldShare)
        {
            if (partIsLimb)
            {
                return 0f;
            }

            if (probeIsLimb || targetIsStatic)
            {
                return 1f;
            }

            return Mathf.Clamp01(coreYieldShare);
        }

        // ------------------------------------------------------------------
        // 侵入の検出
        // ------------------------------------------------------------------

        /// <summary>線分 ab 上で point に最も近い点。a と b が一致していれば a を返します。</summary>
        private Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 axis = b - a;
            float lengthSquared = axis.sqrMagnitude;
            if (lengthSquared < 1e-10f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - a, axis) / lengthSquared);
            return a + axis * t;
        }

        /// <summary>球とカプセルの侵入量。正なら重なっており、負なら離れています。</summary>
        private float PenetrationDepth(Vector3 probeCenter, float probeRadius, Vector3 closestPoint, float partRadius)
        {
            return probeRadius + partRadius - (probeCenter - closestPoint).magnitude;
        }

        /// <summary>
        /// 応答の対象にする侵入量。tolerance までの浅い侵入は許容し、超過分だけを返します。
        /// 撫でる、握手するといった接触で体が押し戻されないようにするためです。
        /// </summary>
        private float EffectiveDepth(float depth, float tolerance)
        {
            return Mathf.Max(0f, depth - Mathf.Max(0f, tolerance));
        }

        /// <summary>
        /// 侵入 1 m あたりの水平移動量。向きはカプセルの軸からプローブ中心へ向かう方向の水平成分です。
        /// <para>
        /// 水平にしか動かせないため、斜めの接触では侵入方向へ進む距離が水平成分の分だけ
        /// 目減りします。それを補うために水平成分の逆数を掛けますが、上限は 2 倍です。
        /// 水平成分が小さすぎる接触は Vector3.zero を返し、応答しません。
        /// </para>
        /// <para>
        /// プローブ中心が軸上にあって方向が定まらない場合は fallbackDirection を使います。
        /// </para>
        /// </summary>
        private Vector3 SeparationPerDepth(Vector3 probeCenter, Vector3 closestPoint, Vector3 fallbackDirection)
        {
            Vector3 normal = probeCenter - closestPoint;
            if (normal.sqrMagnitude < 1e-10f)
            {
                normal = fallbackDirection;
            }

            float length = normal.magnitude;
            if (length < 1e-5f)
            {
                return Vector3.zero;
            }

            float horizontalLength = Mathf.Sqrt(normal.x * normal.x + normal.z * normal.z);
            float share = horizontalLength / length;
            if (share < MinimumHorizontalShare)
            {
                return Vector3.zero;
            }

            float gain = 1f / Mathf.Max(share, 0.5f);
            return new Vector3(normal.x / horizontalLength * gain, 0f, normal.z / horizontalLength * gain);
        }

        /// <summary>
        /// 複数の接触から求めた補正を 1 つの分離ベクトルへまとめます。
        /// <para>
        /// 単純に加算すると、同じ向きの接触が重なった分だけ動きすぎます。そこで、既にまとめた
        /// ベクトルが補正の向きへ進む量を差し引き、不足分だけを足します。同じ補正を何度渡しても
        /// 結果は変わりません。
        /// </para>
        /// </summary>
        private Vector3 AccumulateSeparation(Vector3 combined, Vector3 correction)
        {
            float length = correction.magnitude;
            if (length < 1e-6f)
            {
                return combined;
            }

            Vector3 direction = correction * (1f / length);
            float shortfall = length - Vector3.Dot(combined, direction);
            if (shortfall <= 0f)
            {
                return combined;
            }

            return combined + direction * shortfall;
        }

        /// <summary>
        /// 1 体の相手に対して、自分の頭・体幹プローブが相手の頭・体幹へ侵入している最大量。
        /// 接触していなければ 0 以下です。通り抜けの許可判定に使い、許容幅は差し引きません。
        /// </summary>
        private float CoreContactDepth(
            Vector3[] probePositions, float[] probeRadii,
            Vector3[] partA, Vector3[] partB, float[] partRadii, int partOffset)
        {
            float deepest = -1f;
            for (int probe = 0; probe < CoreProbeCount; probe++)
            {
                float probeRadius = probeRadii[probe];
                if (probeRadius <= 0f)
                {
                    continue;
                }

                for (int part = 0; part < PartCount; part++)
                {
                    if (PartIsLimb(part))
                    {
                        continue;
                    }

                    int index = partOffset + part;
                    float partRadius = partRadii[index];
                    if (partRadius <= 0f)
                    {
                        continue;
                    }

                    Vector3 closest = ClosestPointOnSegment(partA[index], partB[index], probePositions[probe]);
                    float depth = PenetrationDepth(probePositions[probe], probeRadius, closest, partRadius);
                    if (depth > deepest)
                    {
                        deepest = depth;
                    }
                }
            }

            return deepest;
        }

        /// <summary>
        /// 1 体の相手に対する分離ベクトルを combined へ加えて返します。
        /// <para>
        /// probeDepths と probeContacts はデバッグ表示用の出力で、プローブごとに最も深い侵入量と
        /// そのときの相手側の最近点を保持します。呼び出し側がフレームの先頭で 0 に戻します。
        /// </para>
        /// </summary>
        private Vector3 SolveBody(
            Vector3[] probePositions, float[] probeRadii, int probeCount,
            Vector3[] partA, Vector3[] partB, float[] partRadii, int partOffset,
            bool targetIsStatic, float coreYieldShare, float tolerance, Vector3 fallbackDirection,
            Vector3 combined, float[] probeDepths, Vector3[] probeContacts)
        {
            for (int probe = 0; probe < probeCount; probe++)
            {
                float probeRadius = probeRadii[probe];
                if (probeRadius <= 0f)
                {
                    continue;
                }

                bool probeIsLimb = ProbeIsLimb(probe);
                Vector3 center = probePositions[probe];
                for (int part = 0; part < PartCount; part++)
                {
                    int index = partOffset + part;
                    float partRadius = partRadii[index];
                    if (partRadius <= 0f)
                    {
                        continue;
                    }

                    float weight = YieldWeight(probeIsLimb, PartIsLimb(part), targetIsStatic, coreYieldShare);
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    Vector3 closest = ClosestPointOnSegment(partA[index], partB[index], center);
                    float depth = PenetrationDepth(center, probeRadius, closest, partRadius);
                    if (depth <= 0f)
                    {
                        continue;
                    }

                    if (depth > probeDepths[probe])
                    {
                        probeDepths[probe] = depth;
                        probeContacts[probe] = closest;
                    }

                    float effective = EffectiveDepth(depth, tolerance);
                    if (effective <= 0f)
                    {
                        continue;
                    }

                    Vector3 perDepth = SeparationPerDepth(center, closest, fallbackDirection);
                    combined = AccumulateSeparation(combined, perDepth * (effective * weight));
                }
            }

            return combined;
        }

        // ------------------------------------------------------------------
        // 通り抜けの許可
        // ------------------------------------------------------------------

        /// <summary>
        /// 相手との接触を有効にするかどうかの次の状態。
        /// <para>
        /// 無効の間は、体幹同士が離れるまで無効のままです。スポーン直後のように最初から
        /// 重なっている相手に弾き飛ばされないようにするためです。
        /// </para>
        /// <para>
        /// 有効の間に体幹同士の接触が passThroughSeconds 続いた場合は無効にします。狭い通路で
        /// 立ち止まった相手を通り抜けられるようにするためです。0 以下なら無効にしません。
        /// </para>
        /// </summary>
        private bool NextArmed(bool armed, bool coreTouching, float touchSeconds, float passThroughSeconds)
        {
            if (!armed)
            {
                return !coreTouching;
            }

            if (coreTouching && passThroughSeconds > 0f && touchSeconds >= passThroughSeconds)
            {
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        // 応答
        // ------------------------------------------------------------------

        /// <summary>
        /// フレームレート非依存の応答係数。timeConstant 秒で残差が 1/e になります。
        /// 残差が exp(-dt / tau) なので、フレームを分割しても合成が保たれます。
        /// </summary>
        private float ResponseFactor(float timeConstant, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            if (timeConstant <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-deltaTime / timeConstant);
        }

        /// <summary>このフレームで動かす量。分離ベクトルに係数を掛け、長さを maxStep で制限します。</summary>
        private Vector3 StepTowards(Vector3 separation, float factor, float maxStep)
        {
            Vector3 step = separation * Mathf.Clamp01(factor);
            float length = step.magnitude;
            if (length <= maxStep || length < 1e-9f)
            {
                return step;
            }

            return step * (Mathf.Max(0f, maxStep) / length);
        }
    }
}
