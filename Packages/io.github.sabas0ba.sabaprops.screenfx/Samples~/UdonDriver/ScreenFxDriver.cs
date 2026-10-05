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

        private Material _material;
        private VRCPlayerApi _localPlayer;
        private float _weight = -1f;

        private void Start()
        {
            _localPlayer = Networking.LocalPlayer;
            if (target != null)
            {
                // Renderer ごとの複製を操作し、同じ Material を使う他の Volume へ波及させません。
                _material = target.material;
            }
        }

        public void _FadeIn() { targetWeight = 1f; }

        public void _FadeOut() { targetWeight = 0f; }

        public void _Toggle() { targetWeight = targetWeight > 0.5f ? 0f : 1f; }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (activateOnTrigger && player != null && player.isLocal) targetWeight = 1f;
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (activateOnTrigger && player != null && player.isLocal) targetWeight = 0f;
        }

        private void Update()
        {
            if (_material == null) return;

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

            if (followLocalPlayer && hasPlayer)
            {
                target.transform.position =
                    _localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            }
        }
    }
}
