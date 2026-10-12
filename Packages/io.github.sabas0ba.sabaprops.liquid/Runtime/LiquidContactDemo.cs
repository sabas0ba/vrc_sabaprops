using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    /// <summary>A repeatable desktop demonstration of the same transfer used by body contact.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("SabaProps/Liquid/Contact Demo")]
    public class LiquidContactDemo : UdonSharpBehaviour
    {
        public LiquidCanvasPool pool;
        public LiquidBodyCanvas surface;
        public LiquidBodyCanvas padCanvas;
        public Transform pad;
        public Transform sourceStart;
        public Transform sourceEnd;
        public Transform destinationStart;
        public Transform destinationEnd;
        public Transform displayPose;
        public LiquidProfile paint;
        public LiquidProfile fluorescentPaint;
        public GameObject[] phaseLabels;
        [Range(0.03f, 0.3f)] public float radius = 0.16f;
        private bool _running;
        private int _startedAt;
        private int _lastStep = -1;
        private int _phase;
        private int _runs;
        private float _nextRun;
        private Vector3 _previousPoint;

        public int GetPhase() { return _phase; }
        public int GetRunCount() { return _runs; }

        public void Run()
        {
            if (Time.time < _nextRun) return;
            _nextRun = Time.time + 1f;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveRun), Networking.GetServerTimeInMilliseconds());
        }

        [NetworkCallable(1)]
        public void ReceiveRun(int startedAt)
        {
            if (pool == null || surface == null || padCanvas == null || pad == null || paint == null
                || sourceStart == null || sourceEnd == null || destinationStart == null || destinationEnd == null
                || displayPose == null) return;
            _startedAt = startedAt;
            _lastStep = -1;
            _phase = 0;
            _runs++;
            _running = true;
            // This is the explicitly resettable sample fixture, not a global player reset.
            surface.Clear();
            padCanvas.Clear();
            if (pool.paintLog != null) pool.paintLog.Forget();
            Vector3 normal = -sourceStart.forward;
            surface.QueueShape(sourceStart.position, sourceEnd.position, normal, radius * 0.8f,
                paint, LiquidBodyCanvas.ShapeStroke, 0f, 0f);
            if (fluorescentPaint != null)
            {
                Vector3 middle = (sourceStart.position + sourceEnd.position) * 0.5f;
                surface.QueueShape(middle - sourceStart.up * 0.22f, middle + sourceStart.up * 0.22f,
                    normal, radius * 0.6f, fluorescentPaint, LiquidBodyCanvas.ShapeStroke, 0f, 0f);
            }
            pad.SetPositionAndRotation(displayPose.position, displayPose.rotation);
            SetPhase(0);
        }

        private void Update()
        {
            if (!_running) return;
            int step = Mathf.Max(0, (Networking.GetServerTimeInMilliseconds() - _startedAt) / 100);
            if (step == _lastStep) return;
            bool consecutive = step == _lastStep + 1 && step != 10 && step != 55 && step != 80;
            _lastStep = step;
            if (step < 10) return;
            if (step < 35)
            {
                SetPhase(1);
                Touch(sourceStart, sourceEnd, (step - 10) / 24f, false, false, consecutive, step);
            }
            else if (step < 55)
            {
                SetPhase(2);
                pad.SetPositionAndRotation(displayPose.position, displayPose.rotation);
            }
            else if (step < 80)
            {
                SetPhase(3);
                Touch(destinationStart, destinationEnd, (step - 55) / 24f, true, false, consecutive, step);
            }
            else if (step < 105)
            {
                SetPhase(4);
                Touch(sourceStart, sourceEnd, (step - 80) / 24f, false, true, consecutive, step);
            }
            else
            {
                SetPhase(5);
                pad.SetPositionAndRotation(displayPose.position, displayPose.rotation);
                _running = false;
            }
        }

        private void Touch(Transform from, Transform to, float t, bool deposit, bool wipe, bool consecutive, int seed)
        {
            Vector3 point = Vector3.Lerp(from.position, to.position, Mathf.Clamp01(t));
            Vector3 normal = -from.forward;
            pad.SetPositionAndRotation(point + normal * 0.06f, from.rotation);
            Vector3 padPoint = pad.position + pad.forward * 0.03f;
            Vector3 motion = consecutive ? Vector3.ClampMagnitude(point - _previousPoint, radius) : Vector3.zero;
            _previousPoint = point;
            if (!pool.TryBeginContact()) return;
            if (deposit)
                padCanvas.TransferContact(surface, padPoint, point, -normal, Vector3.zero, radius, 0.45f, 0f, seed);
            else
                surface.TransferContact(padCanvas, point, padPoint, normal, motion, radius, wipe ? 0.85f : 0.3f, 0.8f, seed);
            if (wipe)
                surface.QueueShape(point - motion, point, normal, radius, null, LiquidBodyCanvas.ShapeStroke, 0f, 0.85f);
        }

        private void SetPhase(int phase)
        {
            _phase = phase;
            if (phaseLabels == null) return;
            for (int i = 0; i < phaseLabels.Length; i++)
                if (phaseLabels[i] != null) phaseLabels[i].SetActive(i == phase);
        }
    }
}
