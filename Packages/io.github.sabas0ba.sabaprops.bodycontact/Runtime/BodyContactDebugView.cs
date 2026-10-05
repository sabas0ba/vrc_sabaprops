using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace SabaProps.BodyContact
{
    /// <summary>
    /// BodyContactSystem の形状と接触を、World 内に線で描画します。Unity の Gizmos は
    /// VRChat クライアントでは表示されないため、その代わりに使います。
    /// <para>
    /// 線は 1 つのメッシュにまとめ、頂点をワールド座標で書き込みます。そのため MeshFilter を
    /// 持つ Transform は、原点・無回転・等倍に置く必要があります。描画のたびに位置と回転は
    /// 原点へ戻しますが、親のスケールは変更しません。
    /// </para>
    /// <para>
    /// 描画は BodyContactSystem が 1 フレームの処理を終えた直後に呼び出します。非表示の間は
    /// 何も計算しません。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Body Contact Debug View")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class BodyContactDebugView : UdonSharpBehaviour
    {
        /// <summary>1 フレームに描画できる線分の上限。超えた分は描画しません。</summary>
        public const int MaxLines = 2048;

        [Header("構成")]
        [Tooltip("描画する対象。")]
        public BodyContactSystem source;

        [Tooltip("線のメッシュを持つ MeshFilter。原点・無回転・等倍に置きます。")]
        public MeshFilter lineFilter;

        [Tooltip("線を描画する MeshRenderer。表示の切り替えに使います。")]
        public MeshRenderer lineRenderer;

        [Tooltip("数値を表示する Text。未設定なら数値は表示しません。")]
        public Text statusLabel;

        [Tooltip("数値表示を視界の前に保つための親 Transform。未設定なら動かしません。")]
        public Transform hudRoot;

        [Header("表示")]
        [Tooltip("起動時に表示します。")]
        public bool visible = true;

        [Tooltip("数値表示を更新する間隔 (秒)。")]
        public float labelInterval = 0.2f;

        [Tooltip("視点から見た数値表示の位置 (m)。")]
        public Vector3 hudOffset = new Vector3(-0.22f, -0.16f, 0.6f);

        [Header("色")]
        public Color armedColor = new Color(0.2f, 0.9f, 0.4f, 1f);
        public Color passThroughColor = new Color(1f, 0.8f, 0.1f, 1f);
        public Color contactColor = new Color(1f, 0.2f, 0.2f, 1f);
        public Color probeColor = new Color(0.2f, 0.8f, 1f, 1f);
        public Color separationColor = new Color(1f, 0.3f, 1f, 1f);

        private Mesh _mesh;
        private Vector3[] _vertices = new Vector3[MaxLines * 2];
        private Color[] _colors = new Color[MaxLines * 2];
        private int _lineCount;
        private int _previousLineCount;
        private float _nextLabelTime;
        private bool _initialized;

        private void Start()
        {
            Initialize();
            ApplyVisibility();
        }

        public void _Show()
        {
            visible = true;
            ApplyVisibility();
        }

        public void _Hide()
        {
            visible = false;
            ApplyVisibility();
        }

        public void _ToggleVisible()
        {
            visible = !visible;
            ApplyVisibility();
        }

        /// <summary>BodyContactSystem が 1 フレームの処理を終えた直後に呼びます。</summary>
        public void _Refresh()
        {
            if (!visible || source == null)
            {
                return;
            }

            Initialize();
            if (_mesh == null)
            {
                return;
            }

            _lineCount = 0;
            DrawBodies();
            DrawProbes();
            DrawSeparation();
            Upload();
            RefreshHud();
        }

        // ------------------------------------------------------------------
        // 描画内容
        // ------------------------------------------------------------------

        private void DrawBodies()
        {
            int partCount = BodyContactSystem.PartCount;
            for (int body = 0; body < source.bodyCount; body++)
            {
                int state = source.bodyStates[body];
                if (state == BodyContactSystem.BodyIdle)
                {
                    continue;
                }

                Color color = state == BodyContactSystem.BodyArmed ? armedColor : passThroughColor;
                int offset = body * partCount;
                for (int part = 0; part < partCount; part++)
                {
                    float radius = source.partRadii[offset + part];
                    if (radius > 0f)
                    {
                        AddCapsule(source.partA[offset + part], source.partB[offset + part], radius, color);
                    }
                }
            }
        }

        private void DrawProbes()
        {
            for (int probe = 0; probe < source.activeProbeCount; probe++)
            {
                float radius = source.probeRadii[probe];
                if (radius <= 0f)
                {
                    continue;
                }

                Vector3 center = source.probePositions[probe];
                bool touching = source.probeDepths[probe] > 0f;
                AddSphere(center, radius, touching ? contactColor : probeColor);
                if (touching)
                {
                    AddLine(source.probeContacts[probe], center, contactColor);
                }
            }
        }

        // 分離ベクトルは数 cm しかないため、向きが読めるよう 10 倍に伸ばして腰の高さに描画します。
        private void DrawSeparation()
        {
            if (source.separation.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector3 origin = source.probePositions[BodyContactSystem.ProbeHips];
            AddLine(origin, origin + source.separation * 10f, separationColor);
        }

        // ------------------------------------------------------------------
        // 線の生成
        // ------------------------------------------------------------------

        private void AddLine(Vector3 from, Vector3 to, Color color)
        {
            if (_lineCount >= MaxLines)
            {
                return;
            }

            int index = _lineCount * 2;
            _vertices[index] = from;
            _vertices[index + 1] = to;
            _colors[index] = color;
            _colors[index + 1] = color;
            _lineCount++;
        }

        private void AddCircle(Vector3 center, Vector3 u, Vector3 v, float radius, Color color)
        {
            Vector3 previous = CirclePoint(center, u, v, radius, 0);
            for (int i = 1; i <= CircleSegments; i++)
            {
                Vector3 next = CirclePoint(center, u, v, radius, i);
                AddLine(previous, next, color);
                previous = next;
            }
        }

        private void AddSphere(Vector3 center, float radius, Color color)
        {
            AddCircle(center, Vector3.right, Vector3.forward, radius, color);
            AddCircle(center, Vector3.right, Vector3.up, radius, color);
            AddCircle(center, Vector3.forward, Vector3.up, radius, color);
        }

        // 両端の円と、それを結ぶ 4 本の線。端の半球は省略します。
        private void AddCapsule(Vector3 a, Vector3 b, float radius, Color color)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f)
            {
                AddSphere(a, radius, color);
                return;
            }

            Vector3 u = Perpendicular(axis);
            Vector3 v = Vector3.Cross(axis.normalized, u);
            AddCircle(a, u, v, radius, color);
            AddCircle(b, u, v, radius, color);
            AddLine(a + u * radius, b + u * radius, color);
            AddLine(a - u * radius, b - u * radius, color);
            AddLine(a + v * radius, b + v * radius, color);
            AddLine(a - v * radius, b - v * radius, color);
        }

        // ------------------------------------------------------------------
        // メッシュ
        // ------------------------------------------------------------------

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (lineFilter == null)
            {
                return;
            }

            // 索引は固定です。使わない線分は両端を同じ点に置き、長さ 0 にして描画されないようにします。
            int[] indices = new int[MaxLines * 2];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            _mesh = lineFilter.mesh;
            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }

        private void Upload()
        {
            for (int i = _lineCount * 2; i < _previousLineCount * 2; i++)
            {
                _vertices[i] = Vector3.zero;
            }

            _previousLineCount = _lineCount;

            lineFilter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;

            // 頂点はワールド全域に散らばるため、視錐台カリングで消えない大きさの範囲を与えます。
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(20000f, 20000f, 20000f));
        }

        private void ApplyVisibility()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }

            if (hudRoot != null)
            {
                hudRoot.gameObject.SetActive(visible);
            }
        }

        // ------------------------------------------------------------------
        // 数値表示
        // ------------------------------------------------------------------

        private void RefreshHud()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (hudRoot != null && Utilities.IsValid(local))
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                hudRoot.SetPositionAndRotation(head.position + head.rotation * hudOffset, head.rotation);
            }

            if (statusLabel == null || Time.time < _nextLabelTime)
            {
                return;
            }

            _nextLabelTime = Time.time + Mathf.Max(0.05f, labelInterval);

            int inRange = 0;
            int passThrough = 0;
            for (int body = 0; body < source.bodyCount; body++)
            {
                int state = source.bodyStates[body];
                if (state != BodyContactSystem.BodyIdle) inRange++;
                if (state == BodyContactSystem.BodyPassThrough) passThrough++;
            }

            int contacts = 0;
            float deepest = 0f;
            for (int probe = 0; probe < source.activeProbeCount; probe++)
            {
                float depth = source.probeDepths[probe];
                if (depth > 0f) contacts++;
                if (depth > deepest) deepest = depth;
            }

            float deltaTime = Time.deltaTime;
            float speed = deltaTime > 0f ? source.appliedStep.magnitude / deltaTime : 0f;

            statusLabel.text =
                "bodies " + source.bodyCount + "  in range " + inRange + "  pass-through " + passThrough
                + "\ncontacts " + contacts + "  depth " + (deepest * 1000f).ToString("F0") + " mm"
                + "\nseparation " + (source.separation.magnitude * 1000f).ToString("F0") + " mm"
                + "  speed " + speed.ToString("F2") + " m/s"
                + "\nreversals " + source.reversalsPerSecond + " /s"
                + (source.blockedByWorld ? "  blocked by world" : "")
                + "\nmode " + (source.moveMode == BodyContactSystem.MoveByVelocity ? "velocity" : "teleport")
                + (source.IsSuspended() ? "  suspended" : "");
        }
    }
}
