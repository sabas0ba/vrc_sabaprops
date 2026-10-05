using TMPro;
using UdonSharp;
using UnityEngine;

namespace SabaProps.Tablet
{
    /// <summary>
    /// タブレット上の物理ボタン。押されると target の eventName を呼びます。
    /// <para>
    /// VR では TabletController が指先の位置から押下を判定し、Desktop では Interact で押します。
    /// 呼び出し先は任意の UdonSharpBehaviour で、Stage Cam の操作パネルなど既存の UI も操作できます。
    /// </para>
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletButton : UdonSharpBehaviour
    {
        [Header("呼び出し先")]
        public UdonSharpBehaviour target;
        public string eventName;

        [Tooltip("有効にすると、呼び出し前に target の tabletArgument へ argument を書き込みます。")]
        public bool useArgument;
        public int argument;

        [Header("表示")]
        [Tooltip("押下を判定する領域。前面が local -Z 側、ボタン面が +Z 側の面です。")]
        public BoxCollider pressZone;

        [Tooltip("押下時に奥へ沈むキャップ。")]
        public Transform cap;
        public Renderer capRenderer;
        public Material normalMaterial;
        public Material activeMaterial;
        public Material pressedMaterial;
        public TextMeshPro label;

        [Tooltip("押下時にキャップが沈む量 (m)。")]
        public float pressTravel = 0.003f;

        [Tooltip("Interact で押したときに押下表示を保つ時間 (秒)。")]
        public float flashSeconds = 0.12f;

        private bool lit;
        private bool pressed;
        private Vector3 capRest;
        private bool capCaptured;

        private void Start()
        {
            CaptureCap();
            ApplyVisual();
        }

        public override void Interact()
        {
            SetPressed(true);
            SendCustomEventDelayedSeconds("_EndFlash", flashSeconds);
            _Press();
        }

        public void _EndFlash()
        {
            SetPressed(false);
        }

        /// <summary>呼び出し先のイベントを送ります。表示は変えません。</summary>
        public void _Press()
        {
            if (target == null || string.IsNullOrEmpty(eventName))
            {
                return;
            }

            if (useArgument)
            {
                target.SetProgramVariable("tabletArgument", argument);
            }

            target.SendCustomEvent(eventName);
        }

        /// <summary>ON 状態の表示にします。呼び出し先のモジュールが状態の変化に合わせて呼びます。</summary>
        public void SetLit(bool value)
        {
            lit = value;
            ApplyVisual();
        }

        public bool IsLit()
        {
            return lit;
        }

        public void SetPressed(bool value)
        {
            pressed = value;
            ApplyVisual();
        }

        public void SetLabel(string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private void CaptureCap()
        {
            if (cap == null || capCaptured)
            {
                return;
            }

            capRest = cap.localPosition;
            capCaptured = true;
        }

        private void ApplyVisual()
        {
            if (capRenderer != null)
            {
                Material material = pressed && pressedMaterial != null ? pressedMaterial
                    : lit && activeMaterial != null ? activeMaterial
                    : normalMaterial;
                if (material != null)
                {
                    capRenderer.sharedMaterial = material;
                }
            }

            CaptureCap();
            if (cap != null)
            {
                cap.localPosition = capRest + Vector3.forward * (pressed ? pressTravel : 0f);
            }
        }
    }
}
