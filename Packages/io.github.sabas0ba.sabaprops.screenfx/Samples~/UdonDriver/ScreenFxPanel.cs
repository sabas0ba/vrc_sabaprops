using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace SabaProps.ScreenFx.Samples
{
    /// <summary>コピーしたリグ内の Driver だけを操作する、ローカル UI の接続例です。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ScreenFxPanel : UdonSharpBehaviour
    {
        public ScreenFxDriver driver;
        public Text status;
        private float _nextRefresh;

        public void _On() { if (driver != null) driver._FadeIn(); }
        public void _Off() { if (driver != null) driver._StopImmediately(); }
        public void _Next() { if (driver != null) driver._NextPreset(); }
        public void _Previous() { if (driver != null) driver._PreviousPreset(); }
        public void _Reset() { if (driver != null) driver._ResetPreset(); }
        public void _Lite() { if (driver != null) driver._ToggleLite(); }
        public void _WeightUp() { if (driver != null) driver.SetWeight(driver.targetWeight + 0.1f); }
        public void _WeightDown() { if (driver != null) driver.SetWeight(driver.targetWeight - 0.1f); }
        public void _ExposureUp() { Adjust("_Exposure", 0.25f, -3f, 3f); }
        public void _ExposureDown() { Adjust("_Exposure", -0.25f, -3f, 3f); }
        public void _ParticlesUp() { Adjust("_Particle", 0.1f, 0f, 1f); }
        public void _ParticlesDown() { Adjust("_Particle", -0.1f, 0f, 1f); }

        private void Adjust(string property, float step, float minimum, float maximum)
        {
            if (driver != null) driver.SetFloat(property, Mathf.Clamp(driver.GetFloat(property) + step, minimum, maximum));
        }

        private void Update()
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + 0.2f;
            if (status == null) return;
            if (driver == null) { status.text = "Driver is not assigned"; return; }
            status.text = driver.GetPresetName() + (driver.useLite ? " / Lite" : " / GrabPass")
                + (driver.targetWeight > 0f ? " / ON" : " / OFF")
                + "\nWeight " + driver.targetWeight.ToString("F1")
                + "    Exposure " + driver.GetFloat("_Exposure").ToString("F2")
                + "    Particles " + driver.GetFloat("_Particle").ToString("F1");
        }
    }
}
