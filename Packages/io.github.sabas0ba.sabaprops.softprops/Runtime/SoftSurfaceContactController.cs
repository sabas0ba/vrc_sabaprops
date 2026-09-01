using UdonSharp;
using UnityEngine;
using VRC.Dynamics;

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

            int slot = FindSender(sender);
            if (slot < 0)
            {
                slot = FindAvailableSlot();
            }

            if (slot < 0)
            {
                return;
            }

            _senders[slot] = sender;
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

            bool anyVisible = false;
            for (int i = 0; i < SlotCount; i++)
            {
                ContactSenderProxy sender = _senders[i];
                bool active = sender != null && sender.isValid;
                float target = 0f;

                if (active)
                {
                    Vector3 local = ToSurfaceLocal(sender.position);
                    local.y = surfacePlaneY;
                    _positions[i] = Vector3.Lerp(_positions[i], local, 0.55f);

                    // Receiverを表面近傍の薄い層に限定するため、Enter前には荷重を
                    // 発生させず、Sender中心高から疑似penetrationも生成しない。
                    target = _weights[i];
                    target = Mathf.Clamp01(target + _impulses[i]);
                }
                else if (sender != null)
                {
                    _senders[i] = null;
                }

                float seconds = target > _pressures[i] ? responseSeconds : recoverySeconds;
                float blend = 1f - Mathf.Exp(-interval / Mathf.Max(seconds, 0.001f));
                _pressures[i] = Mathf.Lerp(_pressures[i], target, blend);
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
                ApplyShapeSlot(i, ShapeForSender(sender, i));
            }

            if (!anyVisible)
            {
                // idle中のUdon実行を30 Hzで継続しない。
                _nextUpdate = Time.time + 0.25f;
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
            _material.SetFloat("_Hardness", hardness);
            _material.SetFloat("_MaximumIndent", maximumIndent);
            _material.SetFloat("_ContactRadius", contactRadius);
            _material.SetFloat("_RimLift", rimLift);
            _material.SetFloat("_WrinkleStrength", wrinkleStrength);
            _material.SetFloat("_WrinkleFrequency", wrinkleFrequency);

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

        private int FindAvailableSlot()
        {
            int quietest = -1;
            float quietestPressure = 2f;

            for (int i = 0; i < SlotCount; i++)
            {
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
