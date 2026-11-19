using UnityEngine;

namespace Aloysius.Level1
{
    /// <summary>Separate authored geometry layers for an orthographic side-scroller.</summary>
    [DefaultExecutionOrder(800)]
    public sealed class Level1OutdoorParallax : MonoBehaviour
    {
        [SerializeField] Camera sceneCamera;
        [SerializeField] Level1Lighting lighting;
        [SerializeField, Tooltip("1 = 与地面同速；小于 1 = 较远；大于 1 = 前景。"), Range(0, 1.5f)] float horizontalMotion = 1;
        [SerializeField, Tooltip("纵向视差比例。镜头缩放仍使用相机真实投影。"), Range(0, 1.5f)] float verticalMotion = 1;
        Vector3 authoredPosition, cameraOrigin;
        Renderer[] pieces;
        MaterialPropertyBlock properties;
        bool captured;

        public void Configure(Camera camera, Level1Lighting sceneLighting, float horizontal, float vertical)
        { sceneCamera = camera; lighting = sceneLighting; horizontalMotion = horizontal; verticalMotion = vertical; }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!sceneCamera) sceneCamera = Camera.main;
            authoredPosition = transform.localPosition;
            cameraOrigin = sceneCamera ? sceneCamera.transform.position : Vector3.zero;
            pieces = GetComponentsInChildren<Renderer>(true);
            properties = new MaterialPropertyBlock();
            captured = true;
        }

        void LateUpdate()
        {
            if (!captured || !sceneCamera) return;
            Vector3 delta = sceneCamera.transform.position - cameraOrigin;
            Vector3 shift = new Vector3(delta.x * (1 - horizontalMotion), delta.y * (1 - verticalMotion), 0);
            if (transform.parent) shift = transform.parent.InverseTransformVector(shift);
            transform.localPosition = authoredPosition + shift;
            // Keep the existing pipe cutaway: exterior scenery also goes black inside.
            float visibility = lighting ? 1 - lighting.PipeVisionBlend : 1;
            foreach (Renderer piece in pieces)
            {
                if (!piece) continue;
                piece.GetPropertyBlock(properties);
                properties.SetFloat("_SceneVisibility", visibility);
                piece.SetPropertyBlock(properties);
            }
        }

        void OnDisable()
        {
            if (!captured) return;
            transform.localPosition = authoredPosition;
            foreach (Renderer piece in pieces)
            {
                if (!piece) continue;
                piece.GetPropertyBlock(properties); properties.SetFloat("_SceneVisibility", 1);
                piece.SetPropertyBlock(properties);
            }
            captured = false;
        }
    }
}
