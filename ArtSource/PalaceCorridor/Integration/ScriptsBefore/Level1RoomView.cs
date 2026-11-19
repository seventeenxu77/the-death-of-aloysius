using UnityEngine;

namespace Aloysius.Level1
{
    [RequireComponent(typeof(Camera))]
    public sealed class Level1RoomView : MonoBehaviour
    {
        [SerializeField] Vector2 roomSize = new Vector2(10f, 5.62799f);
        Camera roomCamera;
        public Vector2 RoomSize => roomSize;
        public void Configure(Vector2 size) { roomSize = size; Fit(); }
        void Awake()
        {
            Application.targetFrameRate = 60;
            Fit();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !Application.isEditor) Application.Quit();
        }
        void LateUpdate() => Fit();
        public void Fit()
        {
            if (!roomCamera) roomCamera = GetComponent<Camera>();
            float aspect = roomCamera.targetTexture ? (float)roomCamera.targetTexture.width / roomCamera.targetTexture.height
                : (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            roomCamera.orthographic = true;
            roomCamera.orthographicSize = Mathf.Max(roomSize.y * .5f, roomSize.x * .5f / aspect);
            roomCamera.rect = new Rect(0f, 0f, 1f, 1f);
            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = Color.black;
            transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        }
    }
}
