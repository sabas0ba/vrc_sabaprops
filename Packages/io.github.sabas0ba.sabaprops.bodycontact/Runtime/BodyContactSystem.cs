using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.BodyContact
{
    /// <summary>
    /// 他のプレイヤーの体へ自分の体が侵入したときに、自分の位置を動かして侵入を解消します。
    /// <para>
    /// World から動かせるのはローカルプレイヤーの位置だけです。そこで全員が自分のクライアントで
    /// 同じ処理を行い、結果は VRChat 標準のプレイヤー同期で伝わります。このコンポーネントは
    /// 同期変数もネットワークイベントも持ちません。
    /// </para>
    /// <para>
    /// このファイルは VRChat から値を取り出し、プレイヤーへ書き戻す側だけを持ちます。
    /// 判定規則と幾何計算は BodyContactSolver.cs にあり、Unity 無しで実行して検査できます。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Body Contact System")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class BodyContactSystem : UdonSharpBehaviour
    {
        /// <summary>TeleportTo で少しずつ位置を動かします。</summary>
        public const int MoveByTeleport = 0;

        /// <summary>SetVelocity で速度を加えます。比較検証用です。</summary>
        public const int MoveByVelocity = 1;

        /// <summary>範囲外、または未使用。</summary>
        public const int BodyIdle = 0;

        /// <summary>範囲内だが、通り抜けを許可している。</summary>
        public const int BodyPassThrough = 1;

        /// <summary>範囲内で接触判定が有効。</summary>
        public const int BodyArmed = 2;

        /// <summary>同時に扱う相手の上限。ダミーとリモートプレイヤーの合計です。</summary>
        public const int MaxBodies = 16;

        [Header("動作")]
        [Tooltip("オフにすると判定も移動も行いません。")]
        public bool contactEnabled = true;

        [Tooltip("VR で手と足の侵入も判定します。オフの場合と Desktop では頭と体幹だけを判定します。")]
        public bool limbProbesInVR = true;

        [Tooltip("VRの手足から相手の腕・脚への深い接触を補正します。")]
        public bool limbContactsEnabled = true;
        public float limbTolerance = 0.05f;
        public BodyContactPull pull;

        [Tooltip("位置を動かす手段。0 は TeleportTo、1 は SetVelocity。1 は比較検証用です。")]
        public int moveMode = MoveByTeleport;

        [Header("形状")]
        [Tooltip("部位とプローブの半径に掛ける倍率。")]
        public float radiusScale = 1f;

        [Header("応答")]
        [Tooltip("許容する侵入量 (m)。これを超えた分だけ押し戻します。")]
        public float tolerance = 0.03f;

        [Tooltip("押し戻しの時定数 (秒)。小さいほど硬くなります。")]
        public float responseSeconds = 0.03f;

        [Tooltip("押し戻しの最大速度 (m/s)。")]
        public float maxSpeed = 6f;

        [Tooltip("体幹同士の接触がこの秒数続くと、離れるまでその相手との通り抜けを許可します。0 以下で無効。")]
        public float passThroughSeconds = 2.5f;

        [Tooltip("双方の目線の高さの合計にこの値を掛けた距離より遠い相手は判定しません。")]
        public float broadPhaseFactor = 0.8f;

        [Tooltip("押し戻された先に壁があるかを調べるレイヤー。Player と PlayerLocal は除外します。")]
        public LayerMask worldCollisionMask = ~(1 << 9 | 1 << 10);

        [Header("検証用")]
        [Tooltip("標準体型の固定ダミーを置く位置。ネットワークの遅延なしで応答を確認できます。")]
        public Transform[] dummies = new Transform[0];

        [Tooltip("ダミーの目線の高さ (m)。Transform の Y スケールが掛かります。")]
        public float dummyEyeHeight = 1.6f;

        [Tooltip("形状と接触を描画するデバッグ表示。未設定でも動作します。")]
        public BodyContactDebugView debugView;

        // デバッグ表示が読む値。Inspector には出しません。
        [HideInInspector] public int bodyCount;
        [HideInInspector] public int[] bodyStates = new int[MaxBodies];
        [HideInInspector] public Vector3[] partA = new Vector3[MaxBodies * PartCount];
        [HideInInspector] public Vector3[] partB = new Vector3[MaxBodies * PartCount];
        [HideInInspector] public float[] partRadii = new float[MaxBodies * PartCount];
        [HideInInspector] public int activeProbeCount;
        [HideInInspector] public Vector3[] probePositions = new Vector3[ProbeCount];
        [HideInInspector] public float[] probeRadii = new float[ProbeCount];
        [HideInInspector] public float[] probeDepths = new float[ProbeCount];
        [HideInInspector] public Vector3[] probeContacts = new Vector3[ProbeCount];
        [HideInInspector] public Vector3 separation;
        [HideInInspector] public Vector3 appliedStep;
        [HideInInspector] public bool blockedByWorld;

        /// <summary>直近 1 秒間に、押し戻しの向きが反転した回数。振動の指標です。</summary>
        [HideInInspector] public int reversalsPerSecond;

        private VRCPlayerApi[] _players = new VRCPlayerApi[0];
        private bool _playersDirty = true;
        private bool[] _armed = new bool[MaxBodies];
        private float[] _touchSeconds = new float[MaxBodies];
        private Vector3[] _joints = new Vector3[JointCount];
        private bool[] _jointValid = new bool[JointCount];
        private bool _suspended;
        private int _stationCount;
        private Vector3 _lastStep;
        private Vector3 _lastPushVelocity;
        private int _reversals;
        private float _reversalWindowEnd;

        // ------------------------------------------------------------------
        // 外部から呼ぶ操作
        // ------------------------------------------------------------------

        /// <summary>判定を一時停止します。Station に座っている間などに使います。</summary>
        public void _Suspend()
        {
            _suspended = true;
            if (pull != null) pull._ReleasePull();
        }

        public void _StationEntered()
        {
            _stationCount++;
            if (pull != null) pull._ReleasePull();
        }

        public void _StationExited()
        {
            _stationCount = Mathf.Max(0, _stationCount - 1);
            DisarmAll();
        }

        /// <summary>一時停止を解除します。重なっている相手は、離れるまで通り抜けを許可します。</summary>
        public void _Resume()
        {
            _suspended = false;
            DisarmAll();
        }

        public void _ToggleEnabled()
        {
            contactEnabled = !contactEnabled;
            if (!contactEnabled && pull != null) pull._ReleasePull();
            DisarmAll();
        }

        public bool IsSuspended()
        {
            return _suspended || _stationCount > 0;
        }

        // ------------------------------------------------------------------
        // イベント
        // ------------------------------------------------------------------

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            _playersDirty = true;
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            _playersDirty = true;
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                _stationCount = 0;
                if (pull != null) pull._ReleasePull();
                DisarmAll();
            }
        }

        // ボーンは IK の後で読む必要があるため、Update ではなく PostLateUpdate で処理します。
        public override void PostLateUpdate()
        {
            _Step();
        }

        /// <summary>
        /// 1 フレーム分の判定と移動。PostLateUpdate から呼ばれます。ClientSim は PostLateUpdate を
        /// 発火しないため、テストからはこのイベントを直接呼びます。
        /// </summary>
        public void _Step()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            ClearFrameOutputs();
            if (pull != null) pull._Tick();
            if (!Utilities.IsValid(local) || !contactEnabled || IsSuspended())
            {
                if (Utilities.IsValid(local)) ReleasePushVelocity(local);
                bodyCount = 0;
                RefreshDebugView();
                return;
            }

            if (_playersDirty)
            {
                RefreshPlayers();
            }

            float deltaTime = Time.deltaTime;
            float ownEyeHeight = ClampEyeHeight(local.GetAvatarEyeHeightAsMeters());
            Vector3 ownRoot = local.GetPosition();
            float ownSpeed = HorizontalSpeed(local.GetVelocity());

            ReadPlayerJoints(local, ownEyeHeight);
            FillProbes(_joints, _jointValid, ownEyeHeight, radiusScale, probePositions, probeRadii);
            activeProbeCount = limbProbesInVR && local.IsUserInVR() ? ProbeCount : CoreProbeCount;

            Vector3 combined = Vector3.zero;
            int body = 0;

            int dummyCount = dummies == null ? 0 : dummies.Length;
            for (int i = 0; i < dummyCount && body < MaxBodies; i++)
            {
                Transform dummy = dummies[i];
                if (dummy == null)
                {
                    continue;
                }

                float eyeHeight = ClampEyeHeight(dummyEyeHeight * dummy.lossyScale.y);
                Vector3 root = dummy.position;
                if (InRange(ownRoot, ownEyeHeight, root, eyeHeight))
                {
                    FillStandardJoints(root, dummy.rotation, eyeHeight, _joints, _jointValid);
                    FillParts(_joints, _jointValid, eyeHeight, radiusScale, partA, partB, partRadii, body * PartCount);
                    combined = SolveGated(body, true, 1f, ownRoot - root, combined, deltaTime, -1);
                }
                else
                {
                    ResetBody(body);
                }

                body++;
            }

            for (int i = 0; i < _players.Length && body < MaxBodies; i++)
            {
                VRCPlayerApi player = _players[i];
                if (!Utilities.IsValid(player) || player.isLocal)
                {
                    continue;
                }

                float eyeHeight = ClampEyeHeight(player.GetAvatarEyeHeightAsMeters());
                Vector3 root = player.GetPosition();
                if (InRange(ownRoot, ownEyeHeight, root, eyeHeight))
                {
                    ReadPlayerJoints(player, eyeHeight);
                    FillParts(_joints, _jointValid, eyeHeight, radiusScale, partA, partB, partRadii, body * PartCount);
                    float share = CoreYieldShare(ownSpeed, HorizontalSpeed(player.GetVelocity()));
                    combined = SolveGated(body, false, share, ownRoot - root, combined, deltaTime, player.playerId);
                }
                else
                {
                    ResetBody(body);
                }

                body++;
            }

            bodyCount = body;
            separation = combined;

            Vector3 step = StepTowards(
                combined, ResponseFactor(responseSeconds, deltaTime), Mathf.Max(0f, maxSpeed) * deltaTime);
            if (pull != null)
            {
                step = pull.CombineSteps(step, pull.pullStep);
                step = Vector3.ClampMagnitude(step, Mathf.Max(0f, maxSpeed) * deltaTime);
            }
            ApplyStep(local, step, ownEyeHeight, deltaTime);
            if (blockedByWorld && pull != null) pull._BlockedByWorld();
            CountReversals(appliedStep);
            RefreshDebugView();
        }

        // ------------------------------------------------------------------
        // 相手ごとの処理
        // ------------------------------------------------------------------

        private Vector3 SolveGated(
            int body, bool isStatic, float coreYieldShare, Vector3 fallbackDirection, Vector3 combined, float deltaTime, int otherPlayerId)
        {
            int offset = body * PartCount;
            bool touching = CoreContactDepth(probePositions, probeRadii, partA, partB, partRadii, offset) > 0f;
            _touchSeconds[body] = touching ? _touchSeconds[body] + deltaTime : 0f;
            _armed[body] = NextArmed(_armed[body], touching, _touchSeconds[body], passThroughSeconds);

            if (!_armed[body])
            {
                bodyStates[body] = BodyPassThrough;
                return combined;
            }

            bodyStates[body] = BodyArmed;
            int ignoredHand = pull != null && otherPlayerId >= 0 ? pull.IgnoredHandProbe(otherPlayerId) : -1;
            return SolveBodyWithLimbs(
                probePositions, probeRadii, activeProbeCount,
                partA, partB, partRadii, offset,
                isStatic, coreYieldShare, tolerance, fallbackDirection,
                combined, probeDepths, probeContacts, limbContactsEnabled, limbTolerance, ignoredHand);
        }

        private bool InRange(Vector3 ownRoot, float ownEyeHeight, Vector3 root, float eyeHeight)
        {
            float range = (ownEyeHeight + eyeHeight) * broadPhaseFactor;
            return (ownRoot - root).sqrMagnitude <= range * range;
        }

        // 範囲外の相手は接触していないので、次に近づいたときは判定を有効にします。
        private void ResetBody(int body)
        {
            bodyStates[body] = BodyIdle;
            _armed[body] = true;
            _touchSeconds[body] = 0f;
        }

        private void DisarmAll()
        {
            for (int i = 0; i < MaxBodies; i++)
            {
                _armed[i] = false;
                _touchSeconds[i] = 0f;
            }
        }

        private void RefreshPlayers()
        {
            _players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(_players);
            _playersDirty = false;

            // 相手の番号が詰め直されるため、状態を引き継げません。重なっている相手に
            // 弾かれないよう、全員を通り抜け許可から始めます。
            DisarmAll();
        }

        // ------------------------------------------------------------------
        // 形状の収集
        // ------------------------------------------------------------------

        private void ReadPlayerJoints(VRCPlayerApi player, float eyeHeight)
        {
            Vector3 root = player.GetPosition();
            Quaternion rootRotation = player.GetRotation();
            for (int joint = 0; joint < JointCount; joint++)
            {
                Vector3 position = player.GetBonePosition(JointBone(joint));
                bool valid = !IsMissing(position);

                if (joint == JointHead)
                {
                    if (valid)
                    {
                        // Head ボーンは首の付け根側にあるため、頭の球の中心まで頭頂側へずらします。
                        position += player.GetBoneRotation(HumanBodyBones.Head) * Vector3.up * (0.045f * eyeHeight);
                    }
                    else
                    {
                        position = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                        valid = !IsMissing(position);
                    }
                }

                // Neck は Humanoid でも省略できます。体幹の上端が無くならないよう Head ボーンで代替します。
                if (!valid && joint == JointNeck)
                {
                    position = player.GetBonePosition(HumanBodyBones.Head);
                    valid = !IsMissing(position);
                }

                // 非 Humanoid のアバターでも頭と体幹だけは判定できるよう、標準体型で代替します。
                if (!valid && JointIsCore(joint))
                {
                    position = root + rootRotation * (StandardJoint(joint) * eyeHeight);
                    valid = true;
                }

                _joints[joint] = position;
                _jointValid[joint] = valid;
            }
        }

        private HumanBodyBones JointBone(int joint)
        {
            if (joint == JointHips) return HumanBodyBones.Hips;
            if (joint == JointNeck) return HumanBodyBones.Neck;
            if (joint == JointHead) return HumanBodyBones.Head;
            if (joint == JointLeftShoulder) return HumanBodyBones.LeftUpperArm;
            if (joint == JointLeftElbow) return HumanBodyBones.LeftLowerArm;
            if (joint == JointLeftWrist) return HumanBodyBones.LeftHand;
            if (joint == JointRightShoulder) return HumanBodyBones.RightUpperArm;
            if (joint == JointRightElbow) return HumanBodyBones.RightLowerArm;
            if (joint == JointRightWrist) return HumanBodyBones.RightHand;
            if (joint == JointLeftHip) return HumanBodyBones.LeftUpperLeg;
            if (joint == JointLeftKnee) return HumanBodyBones.LeftLowerLeg;
            if (joint == JointLeftAnkle) return HumanBodyBones.LeftFoot;
            if (joint == JointRightHip) return HumanBodyBones.RightUpperLeg;
            if (joint == JointRightKnee) return HumanBodyBones.RightLowerLeg;
            return HumanBodyBones.RightFoot;
        }

        // ------------------------------------------------------------------
        // 移動
        // ------------------------------------------------------------------

        private void ApplyStep(VRCPlayerApi local, Vector3 step, float eyeHeight, float deltaTime)
        {
            appliedStep = Vector3.zero;
            blockedByWorld = false;

            // 微小な移動でも毎フレーム TeleportTo を呼ぶと、静止しているだけで通信が発生します。
            if (step.sqrMagnitude < 2.5e-7f)
            {
                ReleasePushVelocity(local);
                return;
            }

            if (HitsWorld(local.GetPosition(), step, eyeHeight))
            {
                blockedByWorld = true;
                ReleasePushVelocity(local);
                return;
            }

            if (moveMode == MoveByVelocity)
            {
                if (deltaTime <= 0f)
                {
                    return;
                }

                // 前フレームで足した分を差し引いてから足し直し、押し戻しの速度が積み上がらないようにします。
                Vector3 push = step * (1f / deltaTime);
                local.SetVelocity(local.GetVelocity() - _lastPushVelocity + push);
                _lastPushVelocity = push;
            }
            else
            {
                // lerpOnRemote を有効にすると通常の移動として扱われ、リモートで滑らかに見え、
                // アバターのアニメーションと IK もリセットされません。
                local.TeleportTo(
                    local.GetPosition() + step,
                    local.GetRotation(),
                    VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                    true);
            }

            appliedStep = step;
        }

        private void ReleasePushVelocity(VRCPlayerApi local)
        {
            if (_lastPushVelocity.sqrMagnitude > 0f)
            {
                local.SetVelocity(local.GetVelocity() - _lastPushVelocity);
                _lastPushVelocity = Vector3.zero;
            }
        }

        // 押し戻しでプレイヤーが壁の向こうへ出ないよう、膝と胸の高さで進行方向を調べます。
        public bool HitsWorld(Vector3 root, Vector3 step, float eyeHeight)
        {
            if (step.sqrMagnitude < 1e-10f) return false;
            float distance = step.magnitude + 0.14f * eyeHeight;
            Vector3 direction = step.normalized;
            if (pull != null && pull.pullingLocal)
            {
                // 引かれている間は中心線だけでなく体幅も調べ、壁の端を横切る移動も止めます。
                float radius = Mathf.Max(0.05f, eyeHeight * 0.1f);
                if (Physics.CapsuleCast(root + Vector3.up * (radius + 0.02f),
                    root + Vector3.up * Mathf.Max(radius + 0.02f, eyeHeight * 0.9f), radius,
                    direction, step.magnitude + 0.02f, worldCollisionMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return Physics.Raycast(
                       root + Vector3.up * (0.25f * eyeHeight), direction, distance,
                       worldCollisionMask, QueryTriggerInteraction.Ignore)
                   || Physics.Raycast(
                       root + Vector3.up * (0.75f * eyeHeight), direction, distance,
                       worldCollisionMask, QueryTriggerInteraction.Ignore);
        }

        private float HorizontalSpeed(Vector3 velocity)
        {
            return Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
        }

        // ------------------------------------------------------------------
        // デバッグ出力
        // ------------------------------------------------------------------

        private void ClearFrameOutputs()
        {
            activeProbeCount = 0;
            for (int i = 0; i < ProbeCount; i++)
            {
                probeDepths[i] = 0f;
            }

            separation = Vector3.zero;
            appliedStep = Vector3.zero;
            blockedByWorld = false;
        }

        private void CountReversals(Vector3 step)
        {
            if (step.sqrMagnitude > 0f)
            {
                if (Vector3.Dot(step, _lastStep) < 0f)
                {
                    _reversals++;
                }

                _lastStep = step;
            }

            if (Time.time >= _reversalWindowEnd)
            {
                reversalsPerSecond = _reversals;
                _reversals = 0;
                _reversalWindowEnd = Time.time + 1f;
            }
        }

        private void RefreshDebugView()
        {
            if (debugView != null)
            {
                debugView._Refresh();
            }
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        // 編集中にダミーの形状を確認するための表示。実行中の形状と接触は BodyContactDebugView が描画します。
        private void OnDrawGizmosSelected()
        {
            if (dummies == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            for (int i = 0; i < dummies.Length; i++)
            {
                Transform dummy = dummies[i];
                if (dummy == null)
                {
                    continue;
                }

                float eyeHeight = ClampEyeHeight(dummyEyeHeight * dummy.lossyScale.y);
                for (int part = 0; part < PartCount; part++)
                {
                    Vector3 a = dummy.position + dummy.rotation * (StandardJoint(PartJointA(part)) * eyeHeight);
                    Vector3 b = dummy.position + dummy.rotation * (StandardJoint(PartJointB(part)) * eyeHeight);
                    float radius = PartUnitRadius(part) * eyeHeight * radiusScale;
                    Gizmos.DrawWireSphere(a, radius);
                    Gizmos.DrawWireSphere(b, radius);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
#endif
    }
}
