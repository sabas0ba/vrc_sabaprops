using UdonSharp;
using UnityEngine;

namespace SabaProps.Tablet
{
    /// <summary>Animator で PostProcessVolume の weight を調整します。状態は利用者ごとです。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletPostEffects : UdonSharpBehaviour
    {
        public Animator animator;
        public Behaviour[] volumes;
        public TabletButton[] buttons;
        public bool isOn = true;
        public float tabletValue;
        private void Start() { ApplyEnabled(); }
        public void _Toggle() { isOn = !isOn; ApplyEnabled(); }
        public void _SetBrightness() { SetParameter("Brightness", Mathf.Clamp(tabletValue, -2f, 2f)); }
        public void _SetHue() { SetParameter("Hue", Mathf.Clamp(tabletValue, -180f, 180f)); }
        public void _SetGlow() { SetParameter("Glow", Mathf.Clamp(tabletValue, 0f, 1f)); }

        private void SetParameter(string parameter, float value)
        {
            if (animator == null) return;
            animator.SetFloat(parameter, value);
            // 入力イベント内で定数ポーズを評価し、次の描画までに Volume へ反映します。
            animator.Update(0f);
        }

        private void ApplyEnabled()
        {
            if (volumes != null)
                for (int i = 0; i < volumes.Length; i++) if (volumes[i] != null) volumes[i].enabled = isOn;
            if (buttons != null)
                for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].SetLit(isOn);
        }
    }
}
