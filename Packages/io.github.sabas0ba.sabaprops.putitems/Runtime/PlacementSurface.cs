using UdonSharp;
using UnityEngine;

namespace SabaProps.PutItems
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class PlacementSurface : UdonSharpBehaviour
    {
        [Tooltip("ローカル XZ 平面の大きさ。+Y が物を置く側です。")]
        public Vector2 size = new Vector2(1f, 1f);
        [Tooltip("対応する Prop のビットマスク。共通ビットがある場合に吸着できます。")]
        public int acceptedCategories = 1;
        [Tooltip("移動する面の所有者。指定した面に置かれた Prop はこの親を追跡します。")]
        public ObjectSyncPlacement carrier;
    }
}
