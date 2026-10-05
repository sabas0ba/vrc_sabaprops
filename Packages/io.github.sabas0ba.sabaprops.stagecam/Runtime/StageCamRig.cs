using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.StageCam
{
    /// <summary>
    /// 特定のプレイヤーの部位を追い、一定距離と固定アングルを保つカメラリグ。
    /// <para>
    /// このファイルは VRChat から値を取り出し、Transform へ書き戻す側だけを持ちます。
    /// 幾何計算は StageCamSolver.cs にあり、そちらは Unity 無しで実行して検査できます。
    /// </para>
    /// <para>
    /// 0.1.0 はローカル専用です。同期は持たず、各クライアントが自分の見たい対象を
    /// 自分で追います。状態はすべて float と int なので、全員で同じ画を見る運用が
    /// 要るようになったら、Transform ではなくこのパラメータ群を同期させます。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Stage Cam Rig")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class StageCamRig : UdonSharpBehaviour
    {
        /// <summary>顔。Head ボーン、無ければ頭のトラッキングデータ。</summary>
        public const int SubjectFace = 0;

        /// <summary>首。Humanoid でも省略可能なため、Head と Chest へ落ちます。</summary>
        public const int SubjectNeck = 1;

        /// <summary>胸。UpperChest は省略可能なので Chest と Spine へ落ちます。</summary>
        public const int SubjectChest = 2;

        /// <summary>体。Hips は Humanoid で必須です。</summary>
        public const int SubjectBody = 3;

        /// <summary>左手。ボーンが無ければ左手のトラッキングデータ。</summary>
        public const int SubjectLeftHand = 4;

        /// <summary>右手。</summary>
        public const int SubjectRightHand = 5;

        /// <summary>左足。FBT でなければ推定値です。</summary>
        public const int SubjectLeftFoot = 6;

        /// <summary>右足。</summary>
        public const int SubjectRightFoot = 7;

        [Header("構成")]
        [Tooltip("動かす Transform。未設定ならこの GameObject です。カメラはこの子に置きます。")]
        public Transform rigRoot;

        [Tooltip("自動フレーミングが画角を読むカメラ。未設定なら子から探します。")]
        public Camera framingCamera;

        [Tooltip("手動補正に使う Pickup。未設定ならこの GameObject から探します。無くても動きます。")]
        public VRC_Pickup handle;

        [Header("追従先")]
        [Tooltip("追う部位。SubjectFace / SubjectNeck / SubjectChest / SubjectBody / SubjectLeftHand / SubjectRightHand / SubjectLeftFoot / SubjectRightFoot。")]
        public int subject = SubjectFace;

        [Tooltip("方位角の基準。FollowWorldOrbit はワールド固定、FollowBodyOrbit は被写体の体の向きに追従します。")]
        public int followMode = FollowBodyOrbit;

        [Tooltip("起動時にローカルプレイヤーを追い始めます。オフの場合は Interact か _TargetLocalPlayer で指定します。")]
        public bool targetLocalPlayerOnStart;

        [Header("構図")]
        [Tooltip("基準からの方位角。0 が正面、90 が被写体から見て右手側です。")]
        public float orbitYaw;

        [Tooltip("仰角。正の値でカメラが上、被写体を見下ろします。")]
        public float orbitPitch = 8f;

        [Tooltip("被写体との距離 (m)。自動フレーミングが有効な場合は上書きされます。")]
        public float orbitDistance = 2.5f;

        [Tooltip("注視点を部位から上下にずらす量 (m)。")]
        public float subjectHeightOffset;

        [Header("追従の滑らかさ")]
        [Tooltip("位置が追いつくまでの時定数 (s)。大きいほど緩やかです。")]
        public float positionTimeConstant = 0.25f;

        [Tooltip("向きが追いつくまでの時定数 (s)。")]
        public float rotationTimeConstant = 0.18f;

        [Tooltip("この半径 (m) 以内の被写体の揺れを無視します。リモートプレイヤーの補間ノイズを抑えます。")]
        public float deadzoneRadius = 0.02f;

        [Header("自動フレーミング")]
        [Tooltip("被写体の画面占有率が一定になるよう距離を調整します。アバターの身長差を吸収します。")]
        public bool autoFraming;

        [Tooltip("被写体が縦方向に占める画面の割合。")]
        public float screenFraction = 0.6f;

        [Tooltip("目線の高さに掛けて被写体の大きさとする係数。全身なら 1.1、顔寄りなら 0.2 程度です。")]
        public float subjectHeightScale = 1.1f;

        [Header("自動カメラワーク")]
        [Tooltip("被写体の周囲を自動で回り込みます。クレーンの振り込みに相当します。")]
        public bool cameraWork;

        [Tooltip("往復 1 周にかける時間 (s)。")]
        public float cameraWorkPeriod = 24f;

        [Tooltip("振り込む角度。90 なら正面から真横まで回り込みます。")]
        public float cameraWorkYawSweep = 90f;

        [Tooltip("振り込みと同時に上がる角度。")]
        public float cameraWorkPitchRise = 6f;

        [Tooltip("振り込みと同時に寄る距離 (m)。負で寄り、正で引きます。")]
        public float cameraWorkDolly = -0.4f;

        [Header("手動補正")]
        [Tooltip("Pickup で掴んで動かすと、その構図が新しい定常アングルになります。")]
        public bool allowPickupAdjust = true;

        /// <summary>追従中のプレイヤー。-1 は未指定。</summary>
        private int targetPlayerId = -1;

        /// <summary>初回フレームで構図へスナップさせるためのフラグ。</summary>
        private bool hasPose;

        private Vector3 smoothedSubjectPoint;
        private float currentYaw;
        private float currentPitch;
        private float currentDistance;
        private float yawTrim;
        private float pitchTrim;
        private float workPhase;

        private Rigidbody handleBody;

        private void Start()
        {
            if (rigRoot == null)
            {
                rigRoot = transform;
            }

            if (framingCamera == null)
            {
                framingCamera = GetComponentInChildren<Camera>();
            }

            if (handle == null)
            {
                handle = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            }

            if (handle != null)
            {
                handleBody = handle.GetComponent<Rigidbody>();
                ParkHandleBody();
            }

            currentDistance = orbitDistance;
            currentPitch = orbitPitch;

            if (targetLocalPlayerOnStart)
            {
                _TargetLocalPlayer();
            }
        }

        // ------------------------------------------------------------------
        // 追従先の指定
        // ------------------------------------------------------------------

        /// <summary>UI が選択したプレイヤーを指定します。無効な ID は現在の対象を変更しません。</summary>
        public void SetTargetPlayer(int playerId)
        {
            if (!Utilities.IsValid(VRCPlayerApi.GetPlayerById(playerId)))
            {
                return;
            }

            targetPlayerId = playerId;
            hasPose = false;
        }

        public int GetTargetPlayerId()
        {
            return targetPlayerId;
        }

        /// <summary>触れた人を追い始めます。ゼロ設定で使える最短の指定方法です。</summary>
        public override void Interact()
        {
            _TargetLocalPlayer();
        }

        /// <summary>ローカルプレイヤーを追い始めます。</summary>
        public void _TargetLocalPlayer()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player))
            {
                return;
            }

            targetPlayerId = player.playerId;
            hasPose = false;
        }

        /// <summary>リグにいちばん近いプレイヤーを追い始めます。</summary>
        public void _TargetNearestPlayer()
        {
            int count = VRCPlayerApi.GetPlayerCount();
            if (count <= 0)
            {
                return;
            }

            VRCPlayerApi[] players = new VRCPlayerApi[count];
            VRCPlayerApi.GetPlayers(players);

            Vector3 origin = rigRoot == null ? transform.position : rigRoot.position;
            int nearestId = -1;
            float nearestSquared = 0f;

            for (int i = 0; i < players.Length; i++)
            {
                VRCPlayerApi player = players[i];
                if (!Utilities.IsValid(player))
                {
                    continue;
                }

                float squared = (player.GetPosition() - origin).sqrMagnitude;
                if (nearestId < 0 || squared < nearestSquared)
                {
                    nearestId = player.playerId;
                    nearestSquared = squared;
                }
            }

            if (nearestId >= 0)
            {
                targetPlayerId = nearestId;
                hasPose = false;
            }
        }

        /// <summary>追従を止めます。カメラは最後の姿勢のまま留まります。</summary>
        public void _ClearTarget()
        {
            targetPlayerId = -1;
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.playerId == targetPlayerId)
            {
                targetPlayerId = -1;
            }
        }

        // ------------------------------------------------------------------
        // 毎フレームの処理
        // ------------------------------------------------------------------

        /// <summary>
        /// IK とアニメーションが適用された後のボーン姿勢を読むため PostLateUpdate を使います。
        /// Update で読むと 1 フレーム古い値になり、被写体が動くたびにカメラが揺れます。
        /// </summary>
        public override void PostLateUpdate()
        {
            if (rigRoot == null)
            {
                return;
            }

            VRCPlayerApi target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateSubjectPoint(target, deltaTime);

            if (allowPickupAdjust && handle != null && handle.IsHeld)
            {
                BakeCurrentPose(target);
                return;
            }

            ApplyFollowPose(target, deltaTime);
        }

        private VRCPlayerApi ResolveTarget()
        {
            if (targetPlayerId < 0)
            {
                return null;
            }

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(targetPlayerId);
            if (!Utilities.IsValid(player))
            {
                targetPlayerId = -1;
                return null;
            }

            return player;
        }

        private void UpdateSubjectPoint(VRCPlayerApi target, float deltaTime)
        {
            Vector3 sampled = SampleSubjectPoint(target);
            sampled.y += subjectHeightOffset;

            if (!hasPose)
            {
                smoothedSubjectPoint = sampled;
                return;
            }

            Vector3 desired = ApplyDeadzone(smoothedSubjectPoint, sampled, deadzoneRadius);
            smoothedSubjectPoint = SmoothTowards(
                smoothedSubjectPoint, desired, SmoothingFactor(positionTimeConstant, deltaTime));
        }

        private void ApplyFollowPose(VRCPlayerApi target, float deltaTime)
        {
            float frameYaw = followMode == FollowBodyOrbit ? HorizontalYaw(target.GetRotation()) : 0f;

            float desiredDistance = orbitDistance;
            if (autoFraming)
            {
                float framed = FramingDistance(SubjectHeight(target), screenFraction, VerticalFov());
                if (framed > 0f)
                {
                    desiredDistance = framed;
                }
            }

            float desiredYaw = frameYaw + orbitYaw;
            float desiredPitch = orbitPitch;

            if (cameraWork)
            {
                workPhase = AdvancePhase(workPhase, cameraWorkPeriod, deltaTime);
                desiredYaw = CameraWorkYaw(workPhase, desiredYaw, cameraWorkYawSweep);
                desiredPitch = CameraWorkPitch(workPhase, desiredPitch, cameraWorkPitchRise);
                desiredDistance = CameraWorkDistance(workPhase, desiredDistance, cameraWorkDolly);
            }

            if (!hasPose)
            {
                // 初回は構図へそのまま置きます。直前にどこにあったかは意味を持ちません。
                currentYaw = desiredYaw;
                currentPitch = desiredPitch;
                currentDistance = desiredDistance;
                yawTrim = 0f;
                pitchTrim = 0f;
                hasPose = true;
            }
            else
            {
                float angleFactor = SmoothingFactor(rotationTimeConstant, deltaTime);
                float distanceFactor = SmoothingFactor(positionTimeConstant, deltaTime);
                currentYaw = SmoothAngleTowards(currentYaw, desiredYaw, angleFactor);
                currentPitch = SmoothAngleTowards(currentPitch, desiredPitch, angleFactor);
                currentDistance += (desiredDistance - currentDistance) * distanceFactor;
            }

            rigRoot.SetPositionAndRotation(
                OrbitPosition(smoothedSubjectPoint, currentYaw, currentPitch, currentDistance),
                AimRotation(currentYaw, currentPitch, yawTrim, pitchTrim));

            ParkHandleBody();
        }

        /// <summary>
        /// 掴んでいる間の姿勢を軌道パラメータへ焼き戻します。離した瞬間の値がそのまま
        /// 次の定常アングルになるため、離すための特別な処理は要りません。
        /// </summary>
        private void BakeCurrentPose(VRCPlayerApi target)
        {
            Vector3 cameraPosition = rigRoot.position;
            Quaternion cameraRotation = rigRoot.rotation;

            currentYaw = BakeYaw(smoothedSubjectPoint, cameraPosition, currentYaw);
            currentPitch = BakePitch(smoothedSubjectPoint, cameraPosition, currentPitch);
            currentDistance = BakeDistance(smoothedSubjectPoint, cameraPosition, currentDistance);
            yawTrim = BakeYawTrim(cameraRotation, currentYaw);
            pitchTrim = BakePitchTrim(cameraRotation, currentPitch);

            // カメラワークの寄与を差し引いてから設定値へ戻します。差し引かないと、
            // 振り込みの途中で掴むたびに基準アングルがその分だけずれていきます。
            float ease = cameraWork ? PingPongEase(workPhase) : 0f;
            float frameYaw = followMode == FollowBodyOrbit ? HorizontalYaw(target.GetRotation()) : 0f;

            orbitYaw = Mathf.DeltaAngle(frameYaw, currentYaw - cameraWorkYawSweep * ease);
            orbitPitch = currentPitch - cameraWorkPitchRise * ease;

            float baseDistance = currentDistance - cameraWorkDolly * ease;
            if (autoFraming)
            {
                // 距離を覚えても次のフレームで上書きされるので、意図の方を覚えます。
                float fraction = FramingFraction(SubjectHeight(target), baseDistance, VerticalFov());
                if (fraction > 0f)
                {
                    screenFraction = fraction;
                }
            }
            else
            {
                orbitDistance = baseDistance;
            }

            hasPose = true;
        }

        // ------------------------------------------------------------------
        // VRChat から値を取り出す
        // ------------------------------------------------------------------

        /// <summary>
        /// 部位の位置。存在しないボーンは Vector3.zero で返るため、候補を順に試します。
        /// 非 Humanoid アバターではボーンが 1 つも取れないので、最後はトラッキング
        /// データとプレイヤー原点に落ちます。
        /// </summary>
        private Vector3 SampleSubjectPoint(VRCPlayerApi player)
        {
            Vector3 root = player.GetPosition();
            Vector3 head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

            if (subject == SubjectNeck)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.Neck),
                    player.GetBonePosition(HumanBodyBones.Head),
                    player.GetBonePosition(HumanBodyBones.Chest),
                    head);
            }

            if (subject == SubjectChest)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.Chest),
                    player.GetBonePosition(HumanBodyBones.UpperChest),
                    player.GetBonePosition(HumanBodyBones.Spine),
                    head);
            }

            if (subject == SubjectBody)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.Hips),
                    player.GetBonePosition(HumanBodyBones.Spine),
                    player.GetBonePosition(HumanBodyBones.Chest),
                    root);
            }

            if (subject == SubjectLeftHand)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.LeftHand),
                    player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position,
                    player.GetBonePosition(HumanBodyBones.LeftLowerArm),
                    head);
            }

            if (subject == SubjectRightHand)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.RightHand),
                    player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position,
                    player.GetBonePosition(HumanBodyBones.RightLowerArm),
                    head);
            }

            if (subject == SubjectLeftFoot)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.LeftFoot),
                    player.GetBonePosition(HumanBodyBones.LeftLowerLeg),
                    player.GetBonePosition(HumanBodyBones.Hips),
                    root);
            }

            if (subject == SubjectRightFoot)
            {
                return FirstSampledPoint(
                    player.GetBonePosition(HumanBodyBones.RightFoot),
                    player.GetBonePosition(HumanBodyBones.RightLowerLeg),
                    player.GetBonePosition(HumanBodyBones.Hips),
                    root);
            }

            return FirstSampledPoint(
                player.GetBonePosition(HumanBodyBones.Head),
                head,
                player.GetBonePosition(HumanBodyBones.Neck),
                root);
        }

        /// <summary>
        /// 被写体の大きさ。目線の高さは実行中に変わる（アバタースケーリング）ので、
        /// 毎フレーム読み直します。
        /// </summary>
        private float SubjectHeight(VRCPlayerApi player)
        {
            return player.GetAvatarEyeHeightAsMeters() * subjectHeightScale;
        }

        /// <summary>Unity の Camera.fieldOfView は垂直画角です。</summary>
        private float VerticalFov()
        {
            return framingCamera == null ? 60f : framingCamera.fieldOfView;
        }

        /// <summary>
        /// 掴んでいない間、Rigidbody を kinematic にしておきます。そうしないと物理と
        /// スクリプトが同じ Transform を取り合い、カメラが落下します。
        /// </summary>
        private void ParkHandleBody()
        {
            if (handleBody != null && !handleBody.isKinematic)
            {
                handleBody.isKinematic = true;
            }
        }
    }
}
