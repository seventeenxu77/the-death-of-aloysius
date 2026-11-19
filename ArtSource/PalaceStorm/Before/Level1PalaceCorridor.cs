using UnityEngine;
using Aloysius.GothicStage;

namespace Aloysius.Level1
{
    [DefaultExecutionOrder(800)]
    public sealed class Level1PalaceCorridor : MonoBehaviour
    {
        [Header("场景引用")]
        [SerializeField] GothicBride2DWalker actor;
        [SerializeField] Level1TraversalController traversal;
        [SerializeField] Level1DragController drag;
        [SerializeField] Level1Lighting lighting;
        [SerializeField] Renderer[] corridorVisuals;
        [SerializeField] Transform startBoundary;
        [SerializeField] Transform endBoundary;
        [SerializeField] Transform ground;
        [Header("镜头")]
        [SerializeField, Min(.3f)] float pipeCameraSize = 1.45f;
        [SerializeField, Min(.3f)] float corridorCameraSize = 3.15f;
        [SerializeField, Min(.01f)] float cameraSmoothTime = .35f;
        [SerializeField] Vector2 pipeCameraOffset = new Vector2(.45f, .28f);
        [SerializeField] Vector2 corridorCameraOffset = new Vector2(.8f, 2.65f);
        [Header("亮度")]
        [SerializeField, Range(0, 1)] float exposure = .68f;
        [SerializeField, Range(0, 1)] float characterExposure = .65f;
        [Header("走廊移动边界")]
        [SerializeField, Min(0)] float boundaryInset = .15f;
        public bool Arrived { get; private set; }
        public float CharacterExposure => characterExposure;
        float originalLeft, originalRight, sizeVelocity;
        Vector3 cameraVelocity;
        MaterialPropertyBlock properties;

        void Awake()
        {
            originalLeft = actor.LeftLimit; originalRight = actor.RightLimit;
            traversal.RightExitCompleted += EnterCorridor;
            if (drag) drag.ResetPerformed += ResetArea;
        }
        void OnDestroy()
        {
            if (traversal) traversal.RightExitCompleted -= EnterCorridor;
            if (drag) drag.ResetPerformed -= ResetArea;
        }
        void EnterCorridor()
        {
            Arrived = true;
            actor.SetMovementBounds(startBoundary.position.x + boundaryInset, endBoundary.position.x - boundaryInset);
        }
        void ResetArea()
        {
            Arrived = false; actor.SetMovementBounds(originalLeft, originalRight);
            cameraVelocity = Vector3.zero; sizeVelocity = 0;
        }
        public void FrameCamera(Camera camera, Vector2 roomSize, float aspect)
        {
            bool pipe = traversal.State == Level1TraversalState.InsidePipe || traversal.State == Level1TraversalState.ExitingPipe;
            Vector3 target = new Vector3(0, 0, -10);
            float size = Mathf.Max(roomSize.y * .5f, roomSize.x * .5f / aspect);
            if (pipe)
            {
                Vector3 focus = traversal.State == Level1TraversalState.ExitingPipe ? actor.TraversalFocusPosition : actor.transform.position;
                target = new Vector3(focus.x + pipeCameraOffset.x, focus.y + pipeCameraOffset.y, -10);
                size = pipeCameraSize;
            }
            else if (Arrived)
            {
                size = corridorCameraSize;
                float half = Mathf.Min(size * aspect, (endBoundary.position.x - startBoundary.position.x) * .5f);
                target = new Vector3(Mathf.Clamp(actor.transform.position.x + corridorCameraOffset.x,
                    startBoundary.position.x + half, endBoundary.position.x - half), ground.position.y + corridorCameraOffset.y, -10);
            }
            camera.orthographicSize = Mathf.SmoothDamp(camera.orthographicSize, size, ref sizeVelocity, cameraSmoothTime);
            camera.transform.position = Vector3.SmoothDamp(camera.transform.position, target, ref cameraVelocity, cameraSmoothTime);
        }
        void LateUpdate()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            float visibility = 1f - (lighting ? lighting.PipeVisionBlend : 0f);
            foreach (var visual in corridorVisuals)
            {
                if (!visual) continue;
                visual.GetPropertyBlock(properties);
                properties.SetFloat("_Exposure", exposure);
                properties.SetFloat("_SceneVisibility", visibility);
                properties.SetVector("_WorldClip", new Vector4(startBoundary.position.x, endBoundary.position.x, -1000, 1000));
                visual.SetPropertyBlock(properties);
            }
        }
    }
}
