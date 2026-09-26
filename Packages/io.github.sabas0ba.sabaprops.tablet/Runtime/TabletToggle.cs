using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet
{
    /// <summary>
    /// GameObject、Collider、Behaviour の有効・無効を切り替えるモジュール。
    /// ミラー、コライダー、ポストエフェクトの Volume、負荷の高いエフェクト、動画プレイヤーの画面などに使います。
    /// <para>
    /// global が無効のときはローカルでのみ切り替えます。有効のときは所有権を取って状態を同期し、
    /// 後から入ったプレイヤーにも反映します。
    /// </para>
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TabletToggle : UdonSharpBehaviour
    {
        [Header("対象")]
        [Tooltip("ON のとき有効にする GameObject。")]
        public GameObject[] objects;

        [Tooltip("ON のとき無効にする GameObject。ミラーの代わりに置く板などに使います。")]
        public GameObject[] invertedObjects;

        [Tooltip("ON のとき有効にする Collider。")]
        public Collider[] colliders;

        [Tooltip("ON のとき有効にするコンポーネント (Light、Camera、Post Process Volume など)。")]
        public Behaviour[] behaviours;

        [Header("状態")]
        public bool isOn;

        [Tooltip("有効にすると全員の状態を同期します。")]
        public bool global;

        [Tooltip("これが ON になったときに OFF にする他のトグル。高画質と低画質のミラーなどに使います。")]
        public TabletToggle[] exclusive;

        [Tooltip("状態を表示するボタン。")]
        public TabletButton[] buttons;

        [UdonSynced]
        private bool syncedOn;

        private void Start()
        {
            // 所有者以外は同期値を上書きしません。OnDeserialization が Start より先に届いた場合、
            // isOn は既に同期値になっているため、それをそのまま反映します。
            if (Networking.IsOwner(gameObject))
            {
                syncedOn = isOn;
            }

            Apply();
        }

        public bool IsOn()
        {
            return isOn;
        }

        public void _Toggle()
        {
            SetOn(!isOn);
        }

        public void _TurnOn()
        {
            SetOn(true);
        }

        public void _TurnOff()
        {
            SetOn(false);
        }

        public void SetOn(bool value)
        {
            if (value && exclusive != null)
            {
                for (int i = 0; i < exclusive.Length; i++)
                {
                    TabletToggle other = exclusive[i];
                    if (other != null && other != this && other.IsOn())
                    {
                        other.SetOn(false);
                    }
                }
            }

            isOn = value;
            Apply();

            if (!global)
            {
                return;
            }

            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && !Networking.IsOwner(local, gameObject))
            {
                Networking.SetOwner(local, gameObject);
            }

            syncedOn = value;
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (!global || syncedOn == isOn)
            {
                return;
            }

            isOn = syncedOn;
            Apply();
        }

        /// <summary>現在の isOn を対象と表示へ反映します。</summary>
        public void Apply()
        {
            if (objects != null)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] != null)
                    {
                        objects[i].SetActive(isOn);
                    }
                }
            }

            if (invertedObjects != null)
            {
                for (int i = 0; i < invertedObjects.Length; i++)
                {
                    if (invertedObjects[i] != null)
                    {
                        invertedObjects[i].SetActive(!isOn);
                    }
                }
            }

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = isOn;
                    }
                }
            }

            if (behaviours != null)
            {
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] != null)
                    {
                        behaviours[i].enabled = isOn;
                    }
                }
            }

            if (buttons != null)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null)
                    {
                        buttons[i].SetLit(isOn);
                    }
                }
            }
        }
    }
}
