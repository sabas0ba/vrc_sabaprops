// オフラインコンパイル用の uGUI 定義。イベント接続と API の正しさは Unity テストで検証します。
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    public class Graphic : MonoBehaviour
    {
        public Color color;
        public bool raycastTarget;
    }
    public class Text : Graphic
    {
        public Font font;
        public int fontSize;
        public string text;
        public TextAnchor alignment;
        public bool supportRichText;
    }
    public class RawImage : Graphic { public Texture texture; }
    public class Image : Graphic { }
    public class GraphicRaycaster : MonoBehaviour { }
    public class CanvasScaler : MonoBehaviour { }
    public class Button : MonoBehaviour
    {
        public Graphic targetGraphic;
        public ButtonClickedEvent onClick = new ButtonClickedEvent();
        public class ButtonClickedEvent : UnityEvent { }
    }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem : MonoBehaviour { }
    public class StandaloneInputModule : MonoBehaviour { }
}
