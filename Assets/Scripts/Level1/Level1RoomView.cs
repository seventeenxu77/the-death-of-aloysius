using UnityEngine;

namespace Aloysius.Level1
{
    [DefaultExecutionOrder(700)]
    [RequireComponent(typeof(Camera))]
    public sealed class Level1RoomView : MonoBehaviour
    {
        [SerializeField] Vector2 roomSize = new Vector2(10f, 5.62799f);
        [SerializeField] Level1PalaceCorridor palaceCorridor;
        [SerializeField] Level1RoyalBedchamber royalBedchamber;
        Camera roomCamera;
        Boss.SerpentCampaign serpentCampaign;
        public Vector2 RoomSize => roomSize;
        public void Configure(Vector2 size) { roomSize = size; Fit(); }
        void Awake()
        {
            Application.targetFrameRate = 60;
            Fit();
        }
        void Update()
        {
            if(Level1TeleportConsole.BlocksGameplayInput)return;
            if (Input.GetKeyDown(KeyCode.Escape) && !Application.isEditor) Application.Quit();
        }
        void LateUpdate() => Fit();
        public void Fit()
        {
            if (!roomCamera) roomCamera = GetComponent<Camera>();
            float aspect = roomCamera.targetTexture ? (float)roomCamera.targetTexture.width / roomCamera.targetTexture.height
                : (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            roomCamera.orthographic = true;
            if (!Application.isPlaying || !palaceCorridor) roomCamera.orthographicSize = Mathf.Max(roomSize.y * .5f, roomSize.x * .5f / aspect);
            roomCamera.rect = new Rect(0f, 0f, 1f, 1f);
            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = Color.black;
            if(Application.isPlaying&&!serpentCampaign)serpentCampaign=FindObjectOfType<Boss.SerpentCampaign>();
            if(Application.isPlaying&&serpentCampaign&&serpentCampaign.FrameCamera(roomCamera,aspect)) { }
            else if (Application.isPlaying && royalBedchamber && royalBedchamber.FrameCamera(roomCamera, aspect)) { }
            else if (Application.isPlaying && palaceCorridor) palaceCorridor.FrameCamera(roomCamera, roomSize, aspect);
            else transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        }
    }
}
