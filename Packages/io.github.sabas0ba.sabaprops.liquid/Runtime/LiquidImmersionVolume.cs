using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 浸漬の Source。プール、浴槽、海、水たまり、泥沼など、体が浸かる液体です。
    /// <para>
    /// 常在の Source で、場の評価を行います。各クライアントが、トリガーの中にいる
    /// 全プレイヤーについて液面の高さを Body Canvas へ渡します。プレイヤーの位置は
    /// VRChat が同期しているため、この Source は何も同期しません。
    /// </para>
    /// <para>
    /// 同じ GameObject にトリガーの Collider が必要です。RequireComponent を付けていないのは、
    /// Collider が抽象型で自動追加できず、指定するとコンポーネントの追加自体が失敗するためです。
    /// </para>
    /// <para>
    /// 液面の高さは surface の Transform、未設定ならトリガーの上端です。波を設定すると
    /// サーバー時刻から液面を上下させます。全クライアントが同じ時刻を使うため、
    /// 波の位相もそろいます。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Immersion Volume")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidImmersionVolume : UdonSharpBehaviour
    {
        [Header("構成")]
        [Tooltip("Canvas を割り当てるプール。")]
        public LiquidCanvasPool pool;

        [Tooltip("この液体の定義。")]
        public LiquidProfile profile;

        [Tooltip("液面の高さを示す Transform。未設定ならトリガーの上端を液面とします。")]
        public Transform surface;

        [Header("波")]
        [Tooltip("液面が上下する振幅（m）。0 で静水です。")]
        [Min(0f)]
        public float waveAmplitude = 0f;

        [Tooltip("波の周期（秒）。")]
        [Min(0.1f)]
        public float wavePeriod = 6f;

        [Header("評価")]
        [Tooltip("浸漬を評価する間隔（秒）。")]
        [Range(0.05f, 1f)]
        public float evaluationInterval = 0.1f;

        private const int MaxInside = 100;

        private int[] _inside = new int[MaxInside];
        private int _insideCount;
        private Collider _trigger;
        private float _lastEvaluation;

        private void Start()
        {
            _trigger = GetComponent<Collider>();
            _lastEvaluation = Time.time;
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player))
            {
                return;
            }

            int id = player.playerId;
            for (int i = 0; i < _insideCount; i++)
            {
                if (_inside[i] == id)
                {
                    return;
                }
            }

            if (_insideCount < MaxInside)
            {
                _inside[_insideCount] = id;
                _insideCount++;
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player))
            {
                RemovePlayer(player.playerId);
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player))
            {
                RemovePlayer(player.playerId);
            }
        }

        /// <summary>現在の液面の高さ（ワールド y）。</summary>
        public float GetSurfaceHeight()
        {
            float height;
            if (surface != null)
            {
                height = surface.position.y;
            }
            else if (_trigger != null)
            {
                height = _trigger.bounds.max.y;
            }
            else
            {
                height = transform.position.y;
            }

            if (waveAmplitude <= 0f)
            {
                return height;
            }

            // double のまま周期で割った余りを取り、float の精度が落ちる前に位相へ直します。
            double time = Networking.GetServerTimeInSeconds();
            float phase = (float)(time % wavePeriod) / wavePeriod;
            return height + waveAmplitude * Mathf.Sin(phase * 2f * Mathf.PI);
        }

        private void Update()
        {
            if (pool == null || profile == null || (_insideCount == 0 && pool.GetMannequinCount() == 0))
            {
                _lastEvaluation = Time.time;
                return;
            }

            float elapsed = Time.time - _lastEvaluation;
            if (elapsed < evaluationInterval)
            {
                return;
            }

            _lastEvaluation = Time.time;
            float level = GetSurfaceHeight();

            for (int i = _insideCount - 1; i >= 0; i--)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(_inside[i]);
                if (!Utilities.IsValid(player))
                {
                    RemovePlayer(_inside[i]);
                    continue;
                }

                LiquidBodyCanvas canvas = pool.AcquireCanvas(player);
                if (canvas != null)
                {
                    canvas.ApplyImmersion(level, profile, elapsed);
                }
            }

            // マネキンはプレイヤーのトリガーイベントを受けないため、足元がトリガーの中にあるかで判定します。
            if (_trigger == null)
            {
                return;
            }

            int mannequins = pool.GetMannequinCount();
            for (int i = 0; i < mannequins; i++)
            {
                LiquidBodyCanvas mannequin = pool.CanvasForTarget(pool.GetMannequinTarget(i));
                if (mannequin != null && _trigger.bounds.Contains(mannequin.GetBodyBottom()))
                {
                    mannequin.ApplyImmersion(level, profile, elapsed);
                }
            }
        }

        private void RemovePlayer(int id)
        {
            for (int i = 0; i < _insideCount; i++)
            {
                if (_inside[i] == id)
                {
                    _insideCount--;
                    _inside[i] = _inside[_insideCount];
                    return;
                }
            }
        }
    }
}
