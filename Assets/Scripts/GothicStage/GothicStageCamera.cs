using UnityEngine;

namespace Aloysius.GothicStage
{
    [RequireComponent(typeof(Camera))]
    public sealed class GothicStageCamera : MonoBehaviour
    {
        Camera stageCamera;

        void Awake() => stageCamera = GetComponent<Camera>();

        void OnPreCull()
        {
            float displayAspect = (float)Screen.width / Screen.height;
            const float stageAspect = 16f / 9f;
            if (displayAspect > stageAspect)
            {
                float width = stageAspect / displayAspect;
                stageCamera.rect = new Rect((1f - width) * .5f, 0f, width, 1f);
            }
            else
            {
                float height = displayAspect / stageAspect;
                stageCamera.rect = new Rect(0f, (1f - height) * .5f, 1f, height);
            }
        }
    }
}
