using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.ScreenFx.Samples
{
    /// <summary>
    /// Screen FX Volume の Weight を実行時に操作します。状態は利用者ごとで、同期しません。
    /// 公開イベント、Trigger への出入り、移動速度のいずれかで効果を出し入れします。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ScreenFxDriver : UdonSharpBehaviour
    {
        [Tooltip("Screen FX Volume の Renderer。")]
        public Renderer target;

        [Tooltip("Volume を利用者の頭へ追従させ、場所によらず効果を適用します。")]
        public bool followLocalPlayer = true;

        [Tooltip("開始時の Weight の目標値。")]
        [Range(0f, 1f)] public float targetWeight;

        [Tooltip("Weight が 0 から 1 へ変化するのに掛ける秒数。")]
        public float fadeSeconds = 0.5f;

        [Tooltip("この GameObject の Trigger Collider へ利用者が入っている間だけ効果を出します。")]
        public bool activateOnTrigger;

        [Tooltip("移動速度に応じて Weight を変えます。Speed プリセット向けです。")]
        public bool speedLinked;

        [Tooltip("効果が出始める速度 (m/s)。")]
        public float speedMinimum = 4f;

        [Tooltip("効果が最大になる速度 (m/s)。")]
        public float speedMaximum = 12f;

        [Tooltip("切替用の Material。空の場合は Target に設定済みの Material を使います。")]
        public Material[] presets;
        public Material[] litePresets;
        public string[] presetNames;
        public int presetIndex;
        public bool useLite;

        [Tooltip("オンから自動停止までの秒数。0 は無制限。デモでは暗転からの復帰用に設定します。")]
        public float autoOffSeconds;

        private Material _material;
        private Material _originalMaterial;
        private VRCPlayerApi _localPlayer;
        private float _weight = -1f;
        private float _offAt;

        private void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            EnsureMaterial();
            if (targetWeight > 0f) ArmTimer();
        }

        public void _FadeIn() { targetWeight = 1f; ArmTimer(); }

        public void _FadeOut() { targetWeight = 0f; }

        public void _Toggle() { if (targetWeight > 0f) _FadeOut(); else _FadeIn(); }

        public void _StopImmediately()
        {
            targetWeight = 0f;
            _weight = 0f;
            _offAt = 0f;
            if (_material != null) _material.SetFloat("_Weight", 0f);
            if (target != null) target.enabled = false;
        }

        public void SetWeight(float value)
        {
            bool wasOff = targetWeight <= 0f;
            targetWeight = Mathf.Clamp01(value);
            if (wasOff && targetWeight > 0f) ArmTimer();
        }

        // 他の UdonSharpBehaviour から直接呼べる、Material を共有しない操作口です。
        public void SetFloat(string propertyName, float value)
        {
            if (propertyName == "_Weight") { SetWeight(value); return; }
            if (EnsureMaterial() && _material.HasProperty(propertyName)) _material.SetFloat(propertyName, value);
        }

        public float GetFloat(string propertyName)
        {
            if (propertyName == "_Weight") return targetWeight;
            return EnsureMaterial() && _material.HasProperty(propertyName) ? _material.GetFloat(propertyName) : 0f;
        }

        public string GetPresetName()
        {
            return presetNames != null && presetIndex >= 0 && presetIndex < presetNames.Length
                ? presetNames[presetIndex] : "Custom";
        }

        public void _NextPreset() { SelectPreset(presetIndex + 1); }
        public void _PreviousPreset() { SelectPreset(presetIndex - 1); }
        public void _ResetPreset() { SelectPreset(presetIndex); }
        public void _ToggleLite() { useLite = !useLite; SelectPreset(presetIndex); }

        public void SelectPreset(int index)
        {
            Material[] choices = useLite ? litePresets : presets;
            if (choices == null || choices.Length == 0 || target == null) return;
            int selected = ((index % choices.Length) + choices.Length) % choices.Length;
            if (choices[selected] == null) return;
            presetIndex = selected;
            if (_originalMaterial == null) _originalMaterial = target.sharedMaterial;
            Material previous = _material;
            // Udon では Material のコンストラクターが公開されていないため、
            // Renderer.material によるインスタンス生成を利用します。
            target.sharedMaterial = choices[selected];
            _material = target.material;
            _material.SetFloat("_Weight", Mathf.Max(_weight, 0f));
            target.sharedMaterial = _material;
            if (previous != null) Destroy(previous);
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;
            if (target == null || target.sharedMaterial == null) return false;
            _originalMaterial = target.sharedMaterial;
            SelectPreset(presetIndex);
            if (_material == null) _material = target.material;
            _weight = Mathf.Clamp01(targetWeight);
            _material.SetFloat("_Weight", _weight);
            target.enabled = _weight > 0.001f;
            return true;
        }

        private void ArmTimer() { _offAt = autoOffSeconds > 0f ? Time.time + autoOffSeconds : 0f; }

        private void OnDisable() { _StopImmediately(); }

        private void OnDestroy()
        {
            if (_material == null) return;
            if (target != null) target.sharedMaterial = _originalMaterial;
            Destroy(_material);
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (player != null && player.isLocal) _StopImmediately();
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (activateOnTrigger && player != null && player.isLocal) _FadeIn();
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (activateOnTrigger && player != null && player.isLocal) targetWeight = 0f;
        }

        private void Update()
        {
            if (!EnsureMaterial()) return;
            if (_offAt > 0f && Time.time >= _offAt) _StopImmediately();
            if (_localPlayer == null || !_localPlayer.IsValid()) _localPlayer = Networking.LocalPlayer;

            float goal = Mathf.Clamp01(targetWeight);
            bool hasPlayer = _localPlayer != null && _localPlayer.IsValid();
            if (speedLinked && hasPlayer)
            {
                goal *= Mathf.InverseLerp(speedMinimum, speedMaximum, _localPlayer.GetVelocity().magnitude);
            }

            float next = Mathf.MoveTowards(
                Mathf.Max(_weight, 0f), goal, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            if (next != _weight)
            {
                _weight = next;
                _material.SetFloat("_Weight", _weight);
                // 表示中の Renderer は Weight が 0 でも GrabPass を実行するため、無効化して負荷を除きます。
                target.enabled = _weight > 0.001f;
            }

        }

        public override void PostLateUpdate()
        {
            if (target != null && followLocalPlayer && _localPlayer != null && _localPlayer.IsValid())
            {
                target.transform.position =
                    _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            }
        }
    }
}
