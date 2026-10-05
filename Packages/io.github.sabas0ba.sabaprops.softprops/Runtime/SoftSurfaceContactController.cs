using UdonSharp;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDKBase;

namespace SabaProps.SoftProps
{
    /// <summary>
    /// World Contactの接触点をsoft surface shaderへ渡すUdon behaviour。
    /// 状態は各clientで同じavatar poseから再構成し、network同期を行わない。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public sealed class SoftSurfaceContactController : UdonSharpBehaviour
    {
        private const int SlotCount = 8;

        [Header("References")]
        [Tooltip("変形shaderを使用するRenderer。未指定時は同じGameObjectから取得します。")]
        public Renderer targetRenderer;

        [Tooltip("接触座標を変換するTransform。未指定時はtargetRendererのTransformを使用します。")]
        public Transform surfaceTransform;

        [Header("Material response")]
        [Range(0f, 1f)]
        [Tooltip("0は柔らかく、1は硬い設定です。")]
        public float hardness = 0.35f;

        [Range(0.005f, 0.25f)]
        [Tooltip("最も強い接触で沈み込む距離です。")]
        public float maximumIndent = 0.09f;

        [Range(0.04f, 0.8f)]
        [Tooltip("1接触点が変形させる半径です。")]
        public float contactRadius = 0.24f;

        [Range(0f, 0.04f)]
        [Tooltip("沈み込み周囲に生じる隆起です。")]
        public float rimLift = 0.008f;

        [Range(0f, 0.03f)]
        [Tooltip("接触周囲の局所的なしわの振幅です。")]
        public float wrinkleStrength = 0.006f;

        [Range(4f, 40f)]
        public float wrinkleFrequency = 18f;

        [Header("Contact footprints")]
        [Range(0.015f, 0.15f)]
        [Tooltip("FingerおよびSoftProbeFingerの接触半径です。")]
        public float fingerRadius = 0.055f;

        [Range(0.05f, 0.5f)]
        [Tooltip("SoftProbeRodの中心線から片側の長さです。")]
        public float rodHalfLength = 0.22f;

        [Range(0.015f, 0.12f)]
        [Tooltip("SoftProbeRodの接触半径です。")]
        public float rodRadius = 0.045f;

        [Range(0.05f, 0.5f)]
        [Tooltip("SoftProbePlateの長辺方向の半寸法です。")]
        public float plateHalfLength = 0.20f;

        [Range(0.03f, 0.35f)]
        [Tooltip("SoftProbePlateの短辺方向の半寸法です。")]
        public float plateHalfWidth = 0.12f;

        [Header("Temporal response")]
        [Range(0.015f, 1.5f)]
        [Tooltip("荷重に追従する時定数です。小さいほど即座に沈みます。")]
        public float responseSeconds = 0.08f;

        [Range(0.05f, 3f)]
        [Tooltip("接触が離れた後に元へ戻る時定数です。")]
        public float recoverySeconds = 0.45f;

        [Range(10f, 90f)]
        [Tooltip("shader parameterを更新する頻度です。")]
        public float updateRate = 30f;

        [Range(0f, 1f)]
        [Tooltip("侵入速度を一時的な追加荷重へ変換する係数です。")]
        public float impactResponse = 0.25f;

        [Header("Contact volume")]
        [Tooltip("変形面のlocal Y座標です。generatorがmodelごとに設定します。")]
        public float surfacePlaneY = 0.05f;

        [Header("World collider probes")]
        public Collider[] probeColliders = new Collider[0];
        [Tooltip("0=指、1=棒、2=板。probeCollidersと同順です。")]
        public int[] probeKinds = new int[0];
        public bool automaticProbe;
        public float cycleSeconds = 5f;
        public float approachHeight = 0.12f;
        public float pressDepth = 0.045f;
        public Vector2 surfaceHalfSize = new Vector2(0.5f, 0.5f);
        public bool playerStandingLoad = true;

        [HideInInspector] public float measuredGap;
        [HideInInspector] public float appliedPressure;
        [HideInInspector] public int playerCollisionEvents;
        [HideInInspector] public int standingSupportSamples;
        private Collider _supportCollider;
        public UnityEngine.UI.Text statusLabel;
        private Vector3[] _probeRestPositions = new Vector3[SlotCount];
        private float[] _probeRestGaps = new float[SlotCount];
        private float _nextStatus;
        private VRCPlayerApi[] _players = new VRCPlayerApi[SlotCount];
        private float[] _playerSeen = new float[SlotCount];
        private Vector3[] _senderOffsets = new Vector3[SlotCount];
        private Vector4[] _lastShapes = new Vector4[SlotCount];

        private ContactSenderProxy[] _senders = new ContactSenderProxy[SlotCount];
        private Vector3[] _positions = new Vector3[SlotCount];
        private float[] _pressures = new float[SlotCount];
        private float[] _weights = new float[SlotCount];
        private float[] _impulses = new float[SlotCount];
        private int[] _shapeKinds = new int[SlotCount];
        private float[] _shapeLengths = new float[SlotCount];
        private float[] _shapeWidths = new float[SlotCount];

        private Material _material;
        private float _nextUpdate;
        private bool _initialized;

        private void Start()
        {
            Initialize();
        }

        public override void OnContactEnter(ContactEnterInfo contactInfo)
        {
            Initialize();

            ContactSenderProxy sender = contactInfo.contactSender;
            if (sender == null || !sender.isValid)
            {
                return;
            }

            // 既知のworld probeはCollider経由で評価し、Contactとの二重加算を防ぐ。
            if (contactInfo.matchingTags != null && probeColliders.Length > 0)
                for (int t = 0; t < contactInfo.matchingTags.Length; t++)
                    if (contactInfo.matchingTags[t].StartsWith("SoftProbe")) return;

            int slot = FindSender(sender);
            if (slot < 0)
            {
                slot = FindAvailableSlot();
            }

            if (slot < 0)
            {
                return;
            }

            AssignSender(slot, sender);
            _senderOffsets[slot] = Quaternion.Inverse(sender.rotation)
                * (contactInfo.contactPoint - sender.position);
            _positions[slot] = ToSurfaceLocal(contactInfo.contactPoint);
            _positions[slot].y = surfacePlaneY;
            _weights[slot] = WeightForTags(contactInfo.matchingTags);
            ConfigureShape(slot, contactInfo.matchingTags);

            Vector3 localVelocity = surfaceTransform.InverseTransformDirection(contactInfo.enterVelocity);
            float downwardSpeed = Mathf.Max(0f, -localVelocity.y);
            _impulses[slot] = Mathf.Clamp01(downwardSpeed * impactResponse);

            // 最初のparameter反映を1 frame待たせない。
            _nextUpdate = 0f;
        }

        public override void OnContactExit(ContactExitInfo contactInfo)
        {
            ContactSenderProxy sender = contactInfo.contactSender;
            int slot = FindSender(sender);
            if (slot >= 0)
            {
                _senders[slot] = null;
            }
        }

        private void Update()
        {
            if (!_initialized || Time.time < _nextUpdate)
            {
                return;
            }

            float interval = 1f / Mathf.Max(updateRate, 1f);
            _nextUpdate = Time.time + interval;
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (playerStandingLoad && _supportCollider != null && localPlayer != null
                && localPlayer.IsValid() && localPlayer.IsPlayerGrounded()
                && Vector3.Dot(surfaceTransform.up, Vector3.up) > 0.7f)
            {
                Vector3 feetWorld = localPlayer.GetPosition();
                Vector3 feetLocal = ToSurfaceLocal(feetWorld);
                if (Mathf.Abs(feetLocal.y - surfacePlaneY) < 0.035f
                    && Mathf.Abs(feetLocal.x) < surfaceHalfSize.x && Mathf.Abs(feetLocal.z) < surfaceHalfSize.y)
                {
                    RaycastHit supportHit;
                    if (Physics.Raycast(feetWorld + Vector3.up * 0.03f, Vector3.down,
                        out supportHit, 0.08f, -1, QueryTriggerInteraction.Ignore)
                        && supportHit.collider == _supportCollider)
                    {
                        standingSupportSamples++;
                        RecordStandingPlayer(localPlayer);
                    }
                }
            }

            if (automaticProbe && probeColliders.Length > 0 && probeColliders[0] != null)
            {
                float phase = Mathf.Repeat(Time.time / Mathf.Max(cycleSeconds, 1f), 1f);
                float travel = phase < 0.2f ? 0f : phase < 0.45f
                    ? Mathf.SmoothStep(0f, 1f, (phase - 0.2f) / 0.25f)
                    : phase < 0.65f ? 1f : phase < 0.85f
                    ? 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.65f) / 0.2f) : 0f;
                float gap = Mathf.Lerp(approachHeight, -pressDepth, travel);
                for (int p = 0; p < Mathf.Min(probeColliders.Length, SlotCount); p++)
                    if (probeColliders[p] != null)
                        probeColliders[p].transform.position = _probeRestPositions[p]
                            + surfaceTransform.up * (gap - _probeRestGaps[p]) * surfaceTransform.lossyScale.y;
            }

            bool anyVisible = false;
            for (int i = 0; i < SlotCount; i++)
            {
                ContactSenderProxy sender = _senders[i];
                bool active = sender != null && sender.isValid;
                float target = 0f;
                float penetrationLimit = 1f;

                if (i < probeColliders.Length && probeColliders[i] != null)
                {
                    Collider probe = probeColliders[i];
                    active = probe.enabled && probe.gameObject.activeInHierarchy;
                    Vector3 center = ToSurfaceLocal(probe.bounds.center);
                    float gap = ColliderGap(probe);
                    if (i == 0) measuredGap = gap;
                    active = active && Mathf.Abs(center.x) < surfaceHalfSize.x
                        && Mathf.Abs(center.z) < surfaceHalfSize.y;
                    _positions[i] = new Vector3(center.x, surfacePlaneY, center.z);
                    int kind = i < probeKinds.Length ? probeKinds[i] : 0;
                    _shapeKinds[i] = kind;
                    _shapeLengths[i] = kind == 1 ? rodHalfLength : kind == 2 ? plateHalfLength : 0f;
                    _shapeWidths[i] = kind == 1 ? rodRadius : kind == 2 ? plateHalfWidth : fingerRadius;
                    Vector3 axis = surfaceTransform.InverseTransformDirection(kind == 1
                        ? probe.transform.up : probe.transform.right);
                    _lastShapes[i] = new Vector4(axis.x, axis.z, _shapeLengths[i],
                        kind == 2 ? -_shapeWidths[i] : _shapeWidths[i]);
                    penetrationLimit = Mathf.Clamp01(-gap / Mathf.Max(EffectiveDepth(), 0.001f));
                    target = active ? penetrationLimit : 0f;
                }
                else if (_players[i] != null && _players[i].IsValid()
                    && Time.time - _playerSeen[i] < 0.2f)
                {
                    Vector3 feet = ToSurfaceLocal(_players[i].GetPosition());
                    active = Mathf.Abs(feet.y - surfacePlaneY) < 0.035f
                        && Mathf.Abs(feet.x) < surfaceHalfSize.x && Mathf.Abs(feet.z) < surfaceHalfSize.y;
                    _positions[i] = new Vector3(feet.x, surfacePlaneY, feet.z);
                    _lastShapes[i] = new Vector4(1f, 0f, 0f, Mathf.Min(contactRadius, 0.22f));
                    target = active ? 0.72f : 0f;
                }
                else if (active)
                {
                    Vector3 local = ToSurfaceLocal(sender.position + sender.rotation * _senderOffsets[i]);
                    float penetration = Mathf.Max(0f, surfacePlaneY - local.y);
                    local.y = surfacePlaneY;
                    _positions[i] = Vector3.Lerp(_positions[i], local, 0.55f);

                    // 初回接触点をSender rootに対するoffsetとして追跡する近似。
                    // Avatar Senderの寸法を取得できないため、Collider経路ほど厳密ではない。
                    penetrationLimit = Mathf.Clamp01(penetration / Mathf.Max(EffectiveDepth(), 0.001f));
                    target = Mathf.Min(_weights[i] + _impulses[i], penetrationLimit);
                    _lastShapes[i] = ShapeForSender(sender, i);
                }
                else if (sender != null)
                {
                    _senders[i] = null;
                }

                float seconds = target > _pressures[i] ? responseSeconds : recoverySeconds;
                float blend = 1f - Mathf.Exp(-interval / Mathf.Max(seconds, 0.001f));
                _pressures[i] = Mathf.Lerp(_pressures[i], target, blend);
                // 接触中は物体下面より深く掘らない。離脱後のみ残留変形を復元する。
                if (active && target > 0f) _pressures[i] = Mathf.Min(_pressures[i], penetrationLimit);
                _impulses[i] = Mathf.MoveTowards(_impulses[i], 0f, interval * 2.5f);

                if (_pressures[i] < 0.001f && !active)
                {
                    _pressures[i] = 0f;
                }
                else
                {
                    anyVisible = true;
                }

                ApplySlot(i, new Vector4(
                    _positions[i].x,
                    _positions[i].y,
                    _positions[i].z,
                    _pressures[i]));
                ApplyShapeSlot(i, _lastShapes[i]);
                if (i == 0) appliedPressure = _pressures[i];
            }

            if (!anyVisible && probeColliders.Length == 0)
            {
                // idle中のUdon実行を30 Hzで継続しない。
                _nextUpdate = Time.time + 0.25f;
            }
            if (statusLabel != null && Time.time >= _nextStatus)
            {
                _nextStatus = Time.time + 0.1f;
                statusLabel.text = "Gap: " + (measuredGap * 1000f).ToString("F0") + " mm"
                    + " / compression: " + (appliedPressure * 100f).ToString("F0") + "%"
                    + "\nPlayer collision events: " + playerCollisionEvents;
            }
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (surfaceTransform == null && targetRenderer != null)
            {
                surfaceTransform = targetRenderer.transform;
            }

            if (targetRenderer == null || surfaceTransform == null)
            {
                return;
            }

            // renderer.materialはinstanceごとのmaterialを作る。複数配置したPrefabが
            // 同じcontact parameterを上書きしないために必要である。
            _material = targetRenderer.material;
            _supportCollider = GetComponent<Collider>();
            _material.SetFloat("_Hardness", hardness);
            _material.SetFloat("_MaximumIndent", maximumIndent);
            _material.SetFloat("_ContactRadius", contactRadius);
            _material.SetFloat("_RimLift", rimLift);
            _material.SetFloat("_WrinkleStrength", wrinkleStrength);
            _material.SetFloat("_WrinkleFrequency", wrinkleFrequency);
            if (automaticProbe && probeColliders.Length > 0 && probeColliders[0] != null)
            {
                for (int p = 0; p < Mathf.Min(probeColliders.Length, SlotCount); p++)
                    if (probeColliders[p] != null)
                    {
                        _probeRestPositions[p] = probeColliders[p].transform.position;
                        _probeRestGaps[p] = ColliderGap(probeColliders[p]);
                    }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                ApplySlot(i, Vector4.zero);
                ApplyShapeSlot(i, Vector4.zero);
            }

            _initialized = true;
        }

        private Vector3 ToSurfaceLocal(Vector3 worldPosition)
        {
            return surfaceTransform.InverseTransformPoint(worldPosition);
        }

        private int FindSender(ContactSenderProxy sender)
        {
            if (sender == null)
            {
                return -1;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (_senders[i] == sender)
                {
                    return i;
                }
            }

            return -1;
        }

        private void AssignSender(int slot, ContactSenderProxy sender)
        {
            _senders[slot] = sender;
            _players[slot] = null;
            _playerSeen[slot] = 0f;
        }

        private int FindAvailableSlot()
        {
            int quietest = -1;
            float quietestPressure = 2f;

            for (int i = Mathf.Min(probeColliders.Length, SlotCount); i < SlotCount; i++)
            {
                if (_players[i] != null && _players[i].IsValid() && Time.time - _playerSeen[i] < 0.2f) continue;
                ContactSenderProxy sender = _senders[i];
                if (sender == null || !sender.isValid)
                {
                    if (_pressures[i] < quietestPressure)
                    {
                        quietest = i;
                        quietestPressure = _pressures[i];
                    }
                }
            }

            return quietest;
        }

        public override void OnPlayerCollisionStay(VRCPlayerApi player)
        {
            if (!playerStandingLoad || player == null || !player.IsValid()) return;
            Initialize();
            if (Vector3.Dot(surfaceTransform.up, Vector3.up) < 0.7f) return;
            playerCollisionEvents++;
            RecordStandingPlayer(player);
        }

        private void RecordStandingPlayer(VRCPlayerApi player)
        {
            int slot = -1;
            for (int i = probeColliders.Length; i < SlotCount; i++)
                if (_players[i] == player && (_senders[i] == null || !_senders[i].isValid)) slot = i;
            if (slot < 0) slot = FindAvailableSlot();
            if (slot < 0) return;
            _players[slot] = player;
            _playerSeen[slot] = Time.time;
            _nextUpdate = Mathf.Min(_nextUpdate, Time.time + 1f / Mathf.Max(updateRate, 1f));
        }

        private float EffectiveDepth()
        {
            return maximumIndent * Mathf.Lerp(1f, 0.28f, hardness);
        }

        public float ColliderGap(Collider probe)
        {
            Transform t = probe.transform;
            Vector3 n = surfaceTransform.up;
            Vector3 scale = t.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 center = probe.bounds.center;
            float support = 0f;
            if (probe.GetType() == typeof(SphereCollider))
            {
                SphereCollider sphere = (SphereCollider)probe;
                center = t.TransformPoint(sphere.center);
                support = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            }
            else if (probe.GetType() == typeof(CapsuleCollider))
            {
                CapsuleCollider capsule = (CapsuleCollider)probe;
                center = t.TransformPoint(capsule.center);
                Vector3 axis = capsule.direction == 0 ? t.right : capsule.direction == 1 ? t.up : t.forward;
                float axialScale = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
                float radialScale = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z)
                    : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
                float radius = capsule.radius * radialScale;
                support = radius + Mathf.Max(0f, capsule.height * axialScale * 0.5f - radius)
                    * Mathf.Abs(Vector3.Dot(n, axis));
            }
            else if (probe.GetType() == typeof(BoxCollider))
            {
                BoxCollider box = (BoxCollider)probe;
                center = t.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size, scale) * 0.5f;
                support = Mathf.Abs(Vector3.Dot(n, t.right)) * half.x
                    + Mathf.Abs(Vector3.Dot(n, t.up)) * half.y
                    + Mathf.Abs(Vector3.Dot(n, t.forward)) * half.z;
            }
            else return 1f;
            return (Vector3.Dot(center - surfaceTransform.TransformPoint(new Vector3(0f, surfacePlaneY, 0f)), n)
                - support) / Mathf.Max(Mathf.Abs(surfaceTransform.lossyScale.y), 0.001f);
        }

        private float WeightForTags(string[] tags)
        {
            float weight = 0.5f;
            if (tags == null)
            {
                return weight;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (tag == "SoftProbePlate")
                {
                    return 0.70f;
                }

                if (tag == "SoftProbeRod")
                {
                    return 0.55f;
                }

                if (tag == "SoftProbeFinger")
                {
                    weight = Mathf.Max(weight, 0.32f);
                }

                if (tag == "Torso")
                {
                    return 1f;
                }

                if (tag == "Head")
                {
                    weight = Mathf.Max(weight, 0.8f);
                }
                else if (tag == "Foot" || tag == "FootL" || tag == "FootR")
                {
                    weight = Mathf.Max(weight, 0.72f);
                }
                else if (tag == "Hand" || tag == "HandL" || tag == "HandR")
                {
                    weight = Mathf.Max(weight, 0.48f);
                }
                else if (tag == "Finger" || tag == "FingerL" || tag == "FingerR")
                {
                    weight = Mathf.Max(weight, 0.28f);
                }
            }

            return weight;
        }

        private void ConfigureShape(int slot, string[] tags)
        {
            // 0: point/circle, 1: capsule/rod, 2: oriented box/plate.
            _shapeKinds[slot] = 0;
            _shapeLengths[slot] = 0f;
            _shapeWidths[slot] = contactRadius;

            if (tags == null)
            {
                return;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (tag == "SoftProbePlate")
                {
                    _shapeKinds[slot] = 2;
                    _shapeLengths[slot] = plateHalfLength;
                    _shapeWidths[slot] = plateHalfWidth;
                    return;
                }

                if (tag == "SoftProbeRod")
                {
                    _shapeKinds[slot] = 1;
                    _shapeLengths[slot] = rodHalfLength;
                    _shapeWidths[slot] = rodRadius;
                    return;
                }

                if (tag == "SoftProbeFinger"
                    || tag == "Finger" || tag == "FingerL" || tag == "FingerR")
                {
                    _shapeWidths[slot] = fingerRadius;
                }
                else if (tag == "Hand" || tag == "HandL" || tag == "HandR")
                {
                    _shapeWidths[slot] = Mathf.Min(contactRadius, 0.13f);
                }
                else if (tag == "Foot" || tag == "FootL" || tag == "FootR")
                {
                    _shapeWidths[slot] = Mathf.Min(contactRadius, 0.18f);
                }
            }
        }

        private Vector4 ShapeForSender(ContactSenderProxy sender, int slot)
        {
            int kind = _shapeKinds[slot];
            Vector3 worldAxis = Vector3.right;
            if (sender != null && sender.isValid)
            {
                worldAxis = kind == 1
                    ? sender.rotation * Vector3.up
                    : sender.rotation * Vector3.right;
            }

            Vector3 localAxis3 = surfaceTransform.InverseTransformDirection(worldAxis);
            Vector2 localAxis = new Vector2(localAxis3.x, localAxis3.z);
            if (localAxis.sqrMagnitude < 0.0001f)
            {
                localAxis = Vector2.right;
            }
            else
            {
                localAxis.Normalize();
            }

            // shape.w < 0をoriented boxの識別子として使用する。
            float packedWidth = kind == 2 ? -_shapeWidths[slot] : _shapeWidths[slot];
            return new Vector4(localAxis.x, localAxis.y, _shapeLengths[slot], packedWidth);
        }

        private void ApplySlot(int index, Vector4 value)
        {
            if (_material == null)
            {
                return;
            }

            switch (index)
            {
                case 0: _material.SetVector("_Contact0", value); break;
                case 1: _material.SetVector("_Contact1", value); break;
                case 2: _material.SetVector("_Contact2", value); break;
                case 3: _material.SetVector("_Contact3", value); break;
                case 4: _material.SetVector("_Contact4", value); break;
                case 5: _material.SetVector("_Contact5", value); break;
                case 6: _material.SetVector("_Contact6", value); break;
                case 7: _material.SetVector("_Contact7", value); break;
            }
        }

        private void ApplyShapeSlot(int index, Vector4 value)
        {
            if (_material == null)
            {
                return;
            }

            switch (index)
            {
                case 0: _material.SetVector("_ContactShape0", value); break;
                case 1: _material.SetVector("_ContactShape1", value); break;
                case 2: _material.SetVector("_ContactShape2", value); break;
                case 3: _material.SetVector("_ContactShape3", value); break;
                case 4: _material.SetVector("_ContactShape4", value); break;
                case 5: _material.SetVector("_ContactShape5", value); break;
                case 6: _material.SetVector("_ContactShape6", value); break;
                case 7: _material.SetVector("_ContactShape7", value); break;
            }
        }
    }
}
