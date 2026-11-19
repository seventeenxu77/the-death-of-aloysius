using UnityEngine;

namespace Aloysius.GothicStage
{
    [RequireComponent(typeof(Camera))]
    public sealed class GothicBrideGameView : MonoBehaviour
    {
        public Transform actor;
        float zoom = 4.7f;
        Camera view;
        GUIStyle help;
        void Awake() { view = GetComponent<Camera>(); Application.targetFrameRate = 60; }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) zoom = zoom > 3.2f ? 2.2f : 4.7f;
            zoom = Mathf.Clamp(zoom - Input.mouseScrollDelta.y * .3f, 2.2f, 4.7f);
            if (Input.GetKeyDown(KeyCode.Escape) && !Application.isEditor) Application.Quit();
        }
        void LateUpdate()
        {
            if (!view) view = GetComponent<Camera>();
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, zoom, 1f - Mathf.Exp(-9f * Time.deltaTime));
            float close = Mathf.InverseLerp(4.7f, 2.2f, view.orthographicSize);
            Vector3 center = actor ? new Vector3(actor.position.x, actor.position.y + .8f, -10f) : new Vector3(0, 0, -10);
            transform.position = Vector3.Lerp(new Vector3(0, 0, -10), center, close);
        }
        public void SetReviewZoom(bool close)
        {
            zoom = close ? 2.2f : 4.7f;
            if (!view) view = GetComponent<Camera>();
            view.orthographicSize = zoom;
            transform.position = close && actor ? new Vector3(actor.position.x, actor.position.y + .8f, -10f) : new Vector3(0, 0, -10);
        }
        void OnGUI()
        {
            if (help == null)
            {
                help = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                help.normal.textColor = new Color(.89f, .76f, .53f);
                help.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
            }
            GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 30), "A / D  行走     按住 Shift  疾跑     Tab / 滚轮  近景     R  回到中央     Esc  退出", help);
        }
    }
}
