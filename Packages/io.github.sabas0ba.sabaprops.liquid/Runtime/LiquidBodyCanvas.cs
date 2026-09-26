using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 1 人のプレイヤーの全身に液体の付着を描く Body Canvas。
    /// <para>
    /// このファイルは VRChat から値を取り出し、Transform とマテリアルへ書き戻す側だけを持ちます。
    /// 幾何計算は LiquidCanvasSolver.cs にあり、そちらは Unity 無しで実行して検査できます。
    /// </para>
    /// <para>
    /// 付着は 3 種類の RenderTexture（顔料、液膜、付着した面の奥行き）に蓄えます。各テクスチャは前回の内容を
    /// 読みながら次を書くため 2 枚ずつ持ち、updateInterval ごとに VRCGraphics.Blit で進めます。
    /// 表示は子の Projector が担い、その投影範囲とマテリアルは Editor で確定させてあります。
    /// Udon からは Projector を操作できないためです。
    /// </para>
    /// <para>
    /// 同期は持ちません。付着の入力（DrawOp と浸漬）はすべてのクライアントで同じように
    /// 与えられる前提で、結果は各クライアントが独立に計算します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Body Canvas")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class LiquidBodyCanvas : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("体に追従させる Transform。Projector はこの子に置きます。未設定ならこの GameObject です。")]
        public Transform canvasRoot;

        [Tooltip("割り当て中だけ有効にする Projector の GameObject。")]
        public GameObject projectorObject;

        [Tooltip("この Canvas 専用の Projector マテリアル。Canvas ごとに別のマテリアルが必要です。")]
        public Material projectorMaterial;

        [Tooltip("Canvas を進める Blit 用マテリアル（Hidden/SabaProps/Liquid/Canvas Update）。全 Canvas で共有できます。")]
        public Material updateMaterial;

        [Header("範囲")]
        [Tooltip("Canvas の箱の半径（m）。x: 左右, y: 上下, z: 前後。Projector の投影範囲と一致させます。")]
        public Vector3 halfExtents = new Vector3(0.9f, 1.1f, 0.55f);

        [Tooltip("腰から箱の中心へのずれ（m、体の座標系）。")]
        public Vector3 centerOffset = new Vector3(0f, 0.05f, 0f);

        [Header("解像度と更新")]
        [Tooltip("アトラスの 1 面あたりの解像度。アトラス全体は 3 倍 x 2 倍になります。")]
        [Range(32, 512)]
        public int faceResolution = 160;

        [Tooltip("Canvas を進める間隔（秒）。")]
        [Range(0.02f, 0.5f)]
        public float updateInterval = 0.1f;

        [Tooltip("タイルの縁に残す余白の割合。")]
        [Range(0f, 0.1f)]
        public float atlasInset = 0.02f;

        [Header("液体の振る舞い")]
        [Tooltip("粘性 0 の液膜が重力方向へ流れる速さ（m/s）。")]
        public float flowSpeed = 0.08f;

        [Tooltip("蒸発率の符号化に使う上限（1/s）。これより速い乾燥は表せません。")]
        public float maxEvaporationRate = 0.2f;

        [Tooltip("液から出た後、粘性 0 の液膜の上端が下がる速さ（正規化高さ/s）。")]
        public float immersionDrainSpeed = 0.05f;

        [Tooltip("浸漬の境界の幅（正規化高さ）。")]
        public float immersionEdge = 0.02f;

        [Header("雪")]
        [Tooltip("雪が止んでから、積雪 1 が溶けきるまでの秒数。")]
        [Min(1f)]
        public float snowMeltSeconds = 40f;

        [Tooltip("雨と溶けた雪で上を向いた面に残る水が、乾ききるまでの秒数。")]
        [Min(1f)]
        public float surfaceWaterDryingSeconds = 90f;

        [Header("マネキン")]
        [Tooltip("プレイヤーの代わりに追従する Transform（マネキンの腰）。設定するとプールを介さず常に有効になります。")]
        public Transform anchor;

        [Tooltip("anchor から足裏までの距離（m）。命中判定のカプセルに使います。")]
        public float anchorFeetBelow = 0.95f;

        [Tooltip("anchor から頭頂までの距離（m）。")]
        public float anchorHeadAbove = 0.8f;

        [Tooltip("命中判定のカプセルの半径（m）。")]
        public float anchorBodyRadius = 0.2f;

        [Tooltip("マネキンの頭の中心。部位の推定に使います。")]
        public Transform headAnchor;

        [Tooltip("マネキンの左手。")]
        public Transform leftHandAnchor;

        [Tooltip("マネキンの右手。")]
        public Transform rightHandAnchor;

        [Tooltip("マネキンの左足。")]
        public Transform leftFootAnchor;

        [Tooltip("マネキンの右足。")]
        public Transform rightFootAnchor;

        [Header("受け手の素材")]
        [Tooltip("上半身の衣服の素材。未設定ならマテリアルの既定値（柔らかい布）です。")]
        public LiquidSurfaceProfile bodySurface;

        [Tooltip("下半身の衣服の素材。未設定ならマテリアルの既定値（硬い布）です。")]
        public LiquidSurfaceProfile lowerSurface;

        [Tooltip("靴の素材。未設定ならマテリアルの既定値（革）です。")]
        public LiquidSurfaceProfile feetSurface;

        [Tooltip("髪の素材。")]
        public LiquidSurfaceProfile hairSurface;

        [Tooltip("肌（顔と手）の素材。")]
        public LiquidSurfaceProfile skinSurface;

        [Tooltip("頭と手の位置から髪と肌の部位を推定するか。無効にすると全身を衣服の素材で扱います。")]
        public bool estimateRegions = true;

        [Tooltip("目の高さ 1.6 m のアバターでの頭の半径（m）。")]
        public float headRadius = 0.12f;

        [Tooltip("目の高さ 1.6 m のアバターでの手の半径（m）。")]
        public float handRadius = 0.07f;

        [Tooltip("目の高さ 1.6 m のアバターでの足（靴）の半径（m）。")]
        public float footRadius = 0.13f;

        [Tooltip("腰から上半身と下半身の境目までの高さ（m、体の上方向）。")]
        public float waistOffset = 0.05f;

        private VRCPlayerApi _player;
        private int _playerId = -1;
        private bool _active;

        private RenderTexture _pigmentA;
        private RenderTexture _pigmentB;
        private RenderTexture _filmA;
        private RenderTexture _filmB;
        private RenderTexture _depthA;
        private RenderTexture _depthB;
        private bool _frontIsA = true;

        private Vector3 _origin;
        private Vector3 _right = Vector3.right;
        private Vector3 _up = Vector3.up;
        private Vector3 _forward = Vector3.forward;
        private bool _frameValid;

        private Vector4[] _stampPos = new Vector4[MaxStampsPerUpdate];
        private Vector4[] _stampNormal = new Vector4[MaxStampsPerUpdate];
        private Vector4[] _stampColor = new Vector4[MaxStampsPerUpdate];
        private Vector4[] _stampFilm = new Vector4[MaxStampsPerUpdate];
        private Vector4[] _stampShape = new Vector4[MaxStampsPerUpdate];
        private int _stampCount;

        private float _lastUpdateTime;
        private float _lastActivityTime;

        // 浸漬の状態。高さは Canvas の正規化 y です。
        private float _immersionFilmLevel = -1f;
        private float _immersionPigmentLevel = -1f;
        private float _immersionFilmAmount;
        private float _immersionPigmentCover;
        private float _immersionSmoothness = 0.9f;
        private float _immersionViscosity;
        private float _immersionDryingSeconds = 90f;
        private Color _immersionColor = Color.black;
        private bool _immersedThisUpdate;

        // 雪と雨。積雪は上を向いた面に積もり、止むと溶けて水になります。
        // 雨と溶けた雪の水は、上を向いた面ほど多く残る一様な濡れとして持ちます。
        private float _snowDepth;
        private float _surfaceWater;
        private float _lastSnowTime = -1000f;

        // この周期に洗う範囲。高さは Canvas の正規化 y で、それより下の顔料を洗います。
        private float _washLevel = -2f;
        private float _washAmount;

        private void Start()
        {
            if (canvasRoot == null)
            {
                canvasRoot = transform;
            }

            if (projectorObject != null)
            {
                projectorObject.SetActive(false);
            }

            if (anchor != null)
            {
                Activate();
                UpdateAnchorFrame();
            }
        }

        /// <summary>マネキンとして常に有効な Canvas かどうか。</summary>
        public bool IsMannequin()
        {
            return anchor != null;
        }

        /// <summary>マネキンの命中判定カプセルの下端（ワールド）。</summary>
        public Vector3 GetBodyBottom()
        {
            return anchor.position - anchor.up * Mathf.Max(anchorFeetBelow - anchorBodyRadius, 0f);
        }

        /// <summary>マネキンの命中判定カプセルの上端（ワールド）。</summary>
        public Vector3 GetBodyTop()
        {
            return anchor.position + anchor.up * Mathf.Max(anchorHeadAbove - anchorBodyRadius, 0f);
        }

        /// <summary>割り当て中のプレイヤーの ID。未割り当てなら -1。</summary>
        public int GetPlayerId()
        {
            return _playerId;
        }

        /// <summary>最後に付着の入力を受けた時刻。プールが再利用する Canvas を選ぶのに使います。</summary>
        public float GetLastActivityTime()
        {
            return _lastActivityTime;
        }

        /// <summary>プレイヤーを割り当て、Canvas を空にして表示を始めます。</summary>
        public void Assign(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || anchor != null)
            {
                return;
            }

            _player = player;
            _playerId = player.playerId;
            Activate();

            // 割り当てた周期のうちに届いた付着を捨てないよう、座標系をここで確定させます。
            UpdateFrame();
        }

        /// <summary>Canvas を空にして表示を始めます。プレイヤーの割り当てとマネキンの開始で共通です。</summary>
        private void Activate()
        {
            EnsureTextures();
            ClearTextures();
            ResetImmersion();

            _active = true;
            _stampCount = 0;
            _lastUpdateTime = Time.time;
            _lastActivityTime = Time.time;

            BindTextures();
            PushImmersion();
            PushSurfaces();

            if (projectorObject != null)
            {
                projectorObject.SetActive(true);
            }
        }

        /// <summary>
        /// 受け手の素材をシェーダへ渡します。素材を実行中に変えたときは、これを呼ぶと反映されます。
        /// 未設定の部位はマテリアルの既定値のままにします。
        /// </summary>
        public void PushSurfaces()
        {
            if (bodySurface != null)
            {
                projectorMaterial.SetVector("_SurfaceBodyA", bodySurface.GetPrimary());
                projectorMaterial.SetVector("_SurfaceBodyB", bodySurface.GetSecondary());
            }

            if (hairSurface != null)
            {
                projectorMaterial.SetVector("_SurfaceHairA", hairSurface.GetPrimary());
                projectorMaterial.SetVector("_SurfaceHairB", hairSurface.GetSecondary());
            }

            if (skinSurface != null)
            {
                projectorMaterial.SetVector("_SurfaceSkinA", skinSurface.GetPrimary());
                projectorMaterial.SetVector("_SurfaceSkinB", skinSurface.GetSecondary());
            }

            if (lowerSurface != null)
            {
                projectorMaterial.SetVector("_SurfaceLowerA", lowerSurface.GetPrimary());
                projectorMaterial.SetVector("_SurfaceLowerB", lowerSurface.GetSecondary());
            }

            if (feetSurface != null)
            {
                projectorMaterial.SetVector("_SurfaceFeetA", feetSurface.GetPrimary());
                projectorMaterial.SetVector("_SurfaceFeetB", feetSurface.GetSecondary());
            }
        }

        /// <summary>
        /// 部位の推定に使う頭と手の位置を渡します。プレイヤーはボーン、マネキンは anchor から取ります。
        /// 半径は体格（目の高さ）に比例させ、位置が取れない部位は半径 0 で無効にします。
        /// </summary>
        private void PushRegions()
        {
            Vector4 head = Vector4.zero;
            Vector4 left = Vector4.zero;
            Vector4 right = Vector4.zero;
            Vector4 hip = Vector4.zero;
            Vector4 leftFoot = Vector4.zero;
            Vector4 rightFoot = Vector4.zero;

            if (estimateRegions)
            {
                float scale = 1f;
                if (anchor != null)
                {
                    head = RegionSphere(headAnchor, headRadius);
                    left = RegionSphere(leftHandAnchor, handRadius);
                    right = RegionSphere(rightHandAnchor, handRadius);
                    leftFoot = RegionSphere(leftFootAnchor, footRadius);
                    rightFoot = RegionSphere(rightFootAnchor, footRadius);
                }
                else if (Utilities.IsValid(_player))
                {
                    scale = Mathf.Max(_player.GetAvatarEyeHeightAsMeters(), 0.2f) / 1.6f;
                    head = RegionBone(_player.GetBonePosition(HumanBodyBones.Head), headRadius * scale);
                    left = RegionBone(_player.GetBonePosition(HumanBodyBones.LeftHand), handRadius * scale);
                    right = RegionBone(_player.GetBonePosition(HumanBodyBones.RightHand), handRadius * scale);
                    leftFoot = RegionBone(_player.GetBonePosition(HumanBodyBones.LeftFoot), footRadius * scale);
                    rightFoot = RegionBone(_player.GetBonePosition(HumanBodyBones.RightFoot), footRadius * scale);
                }

                // 上半身と下半身の境目は、腰から体の上方向に少し上がった面です。
                Vector3 waist = _origin - _up * centerOffset.y + _up * (waistOffset * scale);
                hip = new Vector4(waist.x, waist.y, waist.z, 0.03f * scale);
            }

            projectorMaterial.SetVector("_RegionHead", head);
            projectorMaterial.SetVector("_RegionHandL", left);
            projectorMaterial.SetVector("_RegionHandR", right);
            projectorMaterial.SetVector("_RegionHip", hip);
            projectorMaterial.SetVector("_RegionFootL", leftFoot);
            projectorMaterial.SetVector("_RegionFootR", rightFoot);
        }

        private Vector4 RegionSphere(Transform centre, float radius)
        {
            if (centre == null)
            {
                return Vector4.zero;
            }

            Vector3 p = centre.position;
            return new Vector4(p.x, p.y, p.z, radius);
        }

        private Vector4 RegionBone(Vector3 position, float radius)
        {
            if (IsMissingBone(position))
            {
                return Vector4.zero;
            }

            return new Vector4(position.x, position.y, position.z, radius);
        }

        /// <summary>割り当てを外し、表示を止めます。RenderTexture は再利用のため保持します。</summary>
        public void Release()
        {
            _player = null;
            _playerId = -1;
            _active = false;
            _stampCount = 0;
            _frameValid = false;

            if (projectorObject != null)
            {
                projectorObject.SetActive(false);
            }
        }

        /// <summary>
        /// 付着を 1 つ積みます。次の更新でまとめて Canvas へ書き込まれます。
        /// <para>
        /// 積める数は MaxStampsPerUpdate までで、溢れる場合はその場で更新を 1 回進めます。
        /// seed は付着の輪郭の乱数で、全クライアントで同じ値を与えると同じ形になります。
        /// </para>
        /// </summary>
        public void QueueStamp(Vector3 worldPoint, Vector3 worldNormal, float radius, LiquidProfile profile, float amount, float seed)
        {
            if (!_active || !_frameValid || profile == null)
            {
                return;
            }

            if (_stampCount >= MaxStampsPerUpdate)
            {
                AdvanceCanvas(Time.time - _lastUpdateTime);
            }

            Vector3 local = ToCanvasLocalPoint(worldPoint, _origin, _right, _up, _forward);
            Vector3 normal = ToCanvasLocalDirection(worldNormal, _right, _up, _forward).normalized;
            float scale = Mathf.Max(0f, amount);
            // Profile colours are authored in sRGB like any colour field. Arrays reach the shader
            // unconverted, and VRChat worlds render in linear space, so convert here.
            Color color = profile.pigmentColor.linear;

            int i = _stampCount;
            _stampPos[i] = new Vector4(local.x, local.y, local.z, Mathf.Max(radius, 1e-3f));
            _stampNormal[i] = new Vector4(normal.x, normal.y, normal.z, profile.pigmentAmount * scale);
            _stampColor[i] = new Vector4(color.r, color.g, color.b, profile.filmAmount * scale);
            _stampFilm[i] = new Vector4(
                profile.washStrength * scale,
                profile.smoothness,
                profile.viscosity,
                EncodeEvaporation(profile.dryingSeconds, maxEvaporationRate));
            _stampShape[i] = new Vector4(seed, profile.edgeIrregularity, 0f, 0f);
            _stampCount = i + 1;
            _lastActivityTime = Time.time;
        }

        /// <summary>
        /// 液面に浸かっていることを伝えます。浸漬 Source が評価周期ごとに呼びます。
        /// <para>
        /// 液膜の上端は浸かった最高点を保ち、液から出ると粘性に応じて下がります。
        /// 顔料の上端は下がらず、洗浄でのみ薄くなります。
        /// </para>
        /// </summary>
        public void ApplyImmersion(float surfaceWorldY, LiquidProfile profile, float deltaSeconds)
        {
            if (!_active || !_frameValid || profile == null)
            {
                return;
            }

            float level = ImmersionLevel(surfaceWorldY, _origin, _up, halfExtents.y);
            if (level <= -1f)
            {
                return;
            }

            _immersedThisUpdate = true;
            _lastActivityTime = Time.time;

            if (profile.filmAmount > 0f)
            {
                _immersionFilmLevel = Mathf.Max(_immersionFilmLevel, level);
                _immersionFilmAmount = Mathf.Max(_immersionFilmAmount, profile.filmAmount);
                _immersionSmoothness = profile.smoothness;
                _immersionViscosity = profile.viscosity;
                _immersionDryingSeconds = profile.dryingSeconds;
            }

            if (profile.pigmentAmount > 0f)
            {
                _immersionPigmentLevel = Mathf.Max(_immersionPigmentLevel, level);
                _immersionPigmentCover = Mathf.Max(_immersionPigmentCover, profile.pigmentAmount);
                _immersionColor = profile.pigmentColor.linear;
            }
            else if (profile.washStrength > 0f)
            {
                AccumulateWash(level, profile.washStrength * Mathf.Max(0f, deltaSeconds));
            }
        }

        /// <summary>
        /// 雪が降り積もることを伝えます。降雪の Source が評価周期ごとに呼びます。
        /// <para>
        /// 積雪は Canvas に焼かず、深さだけを持ちます。上を向いた面に積もる様子と、溶けた水が
        /// 上を向いた面を濡らす様子は、Projector のシェーダが受け手の向きから描きます。
        /// 降らなかった周期には溶け、溶けた分は水になって乾いていきます。
        /// </para>
        /// </summary>
        public void ApplySnow(float amount)
        {
            if (!_active)
            {
                return;
            }

            _snowDepth = Mathf.Min(1f, _snowDepth + Mathf.Max(0f, amount));
            _lastSnowTime = Time.time;
            _lastActivityTime = Time.time;
        }

        /// <summary>
        /// 雨に濡れることを伝えます。降雨の Source が評価周期ごとに呼びます。
        /// <para>
        /// 雨粒の命中（QueueStamp）とは別に、体の上を向いた面が全体に濡れていく様子を
        /// 一様な水の量として持ちます。描き方は溶けた雪の水と同じで、乾燥時間で乾きます。
        /// </para>
        /// </summary>
        public void ApplyRain(float amount)
        {
            if (!_active)
            {
                return;
            }

            _surfaceWater = Mathf.Min(1f, _surfaceWater + Mathf.Max(0f, amount));
            _lastActivityTime = Time.time;
        }

        /// <summary>
        /// 指定した高さより下を洗います。水が着いた点から体を伝って流れ落ちる Source
        /// （シャワー、水道）が、着水点の高さを渡して呼びます。
        /// </summary>
        public void WashBelow(float worldY, float amount)
        {
            if (!_active || !_frameValid)
            {
                return;
            }

            float level = ImmersionLevel(worldY, _origin, _up, halfExtents.y);
            if (level <= -1f)
            {
                return;
            }

            AccumulateWash(level, Mathf.Max(0f, amount));
            _lastActivityTime = Time.time;
        }

        private void AccumulateWash(float level, float amount)
        {
            _washLevel = Mathf.Max(_washLevel, level);
            _washAmount = Mathf.Min(1f, _washAmount + amount);

            // 浸漬で付いた顔料は、その上端まで洗う液が届いていれば落ちる。
            if (level >= _immersionPigmentLevel)
            {
                _immersionPigmentCover = Mathf.Max(0f, _immersionPigmentCover - amount);
            }
        }

        /// <summary>浸漬で付いた顔料を洗います。シャワーなど、体の外から洗う Source が呼びます。</summary>
        public void WashImmersion(float amount)
        {
            if (!_active)
            {
                return;
            }

            _immersionPigmentCover = Mathf.Max(0f, _immersionPigmentCover - Mathf.Max(0f, amount));
            _lastActivityTime = Time.time;
        }

        public override void PostLateUpdate()
        {
            if (!_active)
            {
                return;
            }

            if (anchor != null)
            {
                UpdateAnchorFrame();
            }
            else if (!Utilities.IsValid(_player))
            {
                Release();
                return;
            }
            else
            {
                UpdateFrame();
            }

            canvasRoot.SetPositionAndRotation(_origin, Quaternion.LookRotation(_forward, _up));
            projectorMaterial.SetVector("_CanvasRowX", CanvasRow(_right, _origin, halfExtents.x));
            projectorMaterial.SetVector("_CanvasRowY", CanvasRow(_up, _origin, halfExtents.y));
            projectorMaterial.SetVector("_CanvasRowZ", CanvasRow(_forward, _origin, halfExtents.z));
            PushRegions();

            float elapsed = Time.time - _lastUpdateTime;
            if (elapsed >= updateInterval)
            {
                AdvanceCanvas(elapsed);
            }
        }

        /// <summary>
        /// マネキンの座標系。anchor の軸をそのまま使います。マネキンはボーンを持たず、
        /// 体の向きは anchor の回転で決まるためです。
        /// </summary>
        private void UpdateAnchorFrame()
        {
            _right = anchor.right;
            _up = anchor.up;
            _forward = anchor.forward;
            _origin = anchor.position + _right * centerOffset.x + _up * centerOffset.y + _forward * centerOffset.z;
            _frameValid = true;
        }

        private void UpdateFrame()
        {
            Vector3 hips = _player.GetBonePosition(HumanBodyBones.Hips);
            Vector3 chest = _player.GetBonePosition(HumanBodyBones.Chest);
            if (IsMissingBone(chest))
            {
                chest = _player.GetBonePosition(HumanBodyBones.Spine);
            }

            Vector3 leftLeg = _player.GetBonePosition(HumanBodyBones.LeftUpperLeg);
            Vector3 rightLeg = _player.GetBonePosition(HumanBodyBones.RightUpperLeg);

            Quaternion playerRotation = _player.GetRotation();
            if (IsMissingBone(hips))
            {
                // Humanoid でないアバター。プレイヤーの位置から腰の高さを仮定します。
                hips = _player.GetPosition() + Vector3.up * halfExtents.y;
            }

            _up = SolveFrameUp(hips, chest, playerRotation * Vector3.up);
            _right = SolveFrameRight(_up, leftLeg, rightLeg, playerRotation * Vector3.forward);
            _forward = SolveFrameForward(_right, _up);
            _origin = hips + _right * centerOffset.x + _up * centerOffset.y + _forward * centerOffset.z;
            _frameValid = true;
        }

        private void AdvanceCanvas(float deltaSeconds)
        {
            float dt = Mathf.Clamp(deltaSeconds, 0f, 1f);
            _lastUpdateTime = Time.time;

            Vector3 gravity = ToCanvasLocalDirection(Vector3.down, _right, _up, _forward);

            updateMaterial.SetFloat("_StampCount", _stampCount);
            updateMaterial.SetVectorArray("_StampPos", _stampPos);
            updateMaterial.SetVectorArray("_StampNormal", _stampNormal);
            updateMaterial.SetVectorArray("_StampColor", _stampColor);
            updateMaterial.SetVectorArray("_StampFilm", _stampFilm);
            updateMaterial.SetVectorArray("_StampShape", _stampShape);
            updateMaterial.SetVector("_HalfExtents", new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0f));
            updateMaterial.SetVector("_GravityCanvas", new Vector4(gravity.x, gravity.y, gravity.z, 0f));
            updateMaterial.SetFloat("_Inset", atlasInset);
            updateMaterial.SetFloat("_DeltaTime", dt);
            updateMaterial.SetFloat("_FlowSpeed", flowSpeed);
            updateMaterial.SetFloat("_MaxEvaporationRate", maxEvaporationRate);
            updateMaterial.SetFloat("_Friction", bodySurface != null ? bodySurface.friction : 0.5f);
            updateMaterial.SetVector("_Wash", new Vector4(_washLevel, _washAmount, immersionEdge, 0f));

            RenderTexture pigmentSource = _frontIsA ? _pigmentA : _pigmentB;
            RenderTexture pigmentTarget = _frontIsA ? _pigmentB : _pigmentA;
            RenderTexture filmSource = _frontIsA ? _filmA : _filmB;
            RenderTexture filmTarget = _frontIsA ? _filmB : _filmA;
            RenderTexture depthSource = _frontIsA ? _depthA : _depthB;
            RenderTexture depthTarget = _frontIsA ? _depthB : _depthA;

            // 顔料と奥行きは流下の判定に更新前の液膜を読みます。液膜より先に進めます。
            updateMaterial.SetTexture("_FilmTex", filmSource);
            VRCGraphics.Blit(pigmentSource, pigmentTarget, updateMaterial, 0);
            VRCGraphics.Blit(depthSource, depthTarget, updateMaterial, 3);
            VRCGraphics.Blit(filmSource, filmTarget, updateMaterial, 1);
            _frontIsA = !_frontIsA;
            _stampCount = 0;
            _washLevel = -2f;
            _washAmount = 0f;

            AdvanceImmersion(dt);
            AdvanceSnow(dt);
            BindTextures();
            PushImmersion();
        }

        private void AdvanceImmersion(float dt)
        {
            if (!_immersedThisUpdate)
            {
                _immersionFilmLevel = DrainLevel(_immersionFilmLevel, _immersionViscosity, immersionDrainSpeed, dt);
                _immersionFilmAmount = EvaporateAmount(_immersionFilmAmount, _immersionDryingSeconds, dt);
            }

            _immersedThisUpdate = false;
        }

        private void AdvanceSnow(float dt)
        {
            // 降雪の評価が途切れてから少し経ったら、止んだとみなして溶かし始めます。
            if (Time.time - _lastSnowTime > 0.5f)
            {
                float melted = MeltSnow(_snowDepth, snowMeltSeconds, dt);
                _snowDepth -= melted;
                _surfaceWater = Mathf.Min(1f, _surfaceWater + melted * 2f);
            }

            _surfaceWater = EvaporateAmount(_surfaceWater, surfaceWaterDryingSeconds, dt);

            bool visible = _snowDepth > 0f || _surfaceWater > 0f;
            projectorMaterial.SetVector("_Snow", new Vector4(_snowDepth, _surfaceWater, 0f, visible ? 1f : 0f));
        }

        private void ResetImmersion()
        {
            _snowDepth = 0f;
            _surfaceWater = 0f;
            _lastSnowTime = -1000f;
            if (projectorMaterial != null)
            {
                projectorMaterial.SetVector("_Snow", Vector4.zero);
            }

            _immersionFilmLevel = -1f;
            _immersionPigmentLevel = -1f;
            _immersionFilmAmount = 0f;
            _immersionPigmentCover = 0f;
            _immersedThisUpdate = false;
        }

        private void PushImmersion()
        {
            bool enabled = _immersionFilmAmount > 0f || _immersionPigmentCover > 0f;
            projectorMaterial.SetVector("_ImmersionLevels", new Vector4(
                _immersionFilmLevel, _immersionPigmentLevel, immersionEdge, enabled ? 1f : 0f));
            projectorMaterial.SetVector("_ImmersionAmounts", new Vector4(
                _immersionFilmAmount, _immersionPigmentCover, _immersionSmoothness, 0f));
            // Already linear. SetVector, so it is not converted a second time.
            projectorMaterial.SetVector("_ImmersionColor", new Vector4(_immersionColor.r, _immersionColor.g, _immersionColor.b, 1f));
        }

        private void BindTextures()
        {
            projectorMaterial.SetTexture("_PigmentTex", _frontIsA ? _pigmentA : _pigmentB);
            projectorMaterial.SetTexture("_FilmTex", _frontIsA ? _filmA : _filmB);
            projectorMaterial.SetTexture("_DepthTex", _frontIsA ? _depthA : _depthB);
            projectorMaterial.SetFloat("_CanvasInset", atlasInset);
        }

        private void EnsureTextures()
        {
            if (_pigmentA != null)
            {
                return;
            }

            int width = faceResolution * AtlasColumns;
            int height = faceResolution * AtlasRows;

            // 顔料は 8 bit で足ります。液膜は蒸発で毎周期わずかに減るため、
            // 8 bit では 1 段階（1/255）未満の減少が失われます。半精度にします。
            _pigmentA = CreateCanvasTexture(width, height, RenderTextureFormat.ARGB32);
            _pigmentB = CreateCanvasTexture(width, height, RenderTextureFormat.ARGB32);
            _filmA = CreateCanvasTexture(width, height, RenderTextureFormat.ARGBHalf);
            _filmB = CreateCanvasTexture(width, height, RenderTextureFormat.ARGBHalf);

            // 付着した面の奥行き（m、符号付き）と記録の確かさ。
            _depthA = CreateCanvasTexture(width, height, RenderTextureFormat.RGHalf);
            _depthB = CreateCanvasTexture(width, height, RenderTextureFormat.RGHalf);
        }

        private RenderTexture CreateCanvasTexture(int width, int height, RenderTextureFormat format)
        {
            RenderTexture texture = new RenderTexture(width, height, 0, format);
            texture.useMipMap = false;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Create();
            return texture;
        }

        private void ClearTextures()
        {
            // 消去のパスは入力を読みませんが、入力と出力に同じテクスチャを渡すのは
            // 未定義動作なので、別のテクスチャを入力に使います。
            VRCGraphics.Blit(_pigmentB, _pigmentA, updateMaterial, 2);
            VRCGraphics.Blit(_pigmentA, _pigmentB, updateMaterial, 2);
            VRCGraphics.Blit(_filmB, _filmA, updateMaterial, 2);
            VRCGraphics.Blit(_filmA, _filmB, updateMaterial, 2);
            VRCGraphics.Blit(_depthB, _depthA, updateMaterial, 2);
            VRCGraphics.Blit(_depthA, _depthB, updateMaterial, 2);
            _frontIsA = true;
        }
    }
}
