using UnityEngine;

namespace Aloysius.Level1
{
    [DefaultExecutionOrder(900)]
    public sealed class Level1PalaceParallaxLayer : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField, Range(0, 1), Tooltip("0 为无限远，1 与走廊同步移动。")]
        float motionRatio = .3f;
        [SerializeField] float referenceCameraX = 5f;
        Vector3 origin;
        public void Configure(Camera camera, float ratio, float referenceX)
        { view = camera; motionRatio = ratio; referenceCameraX = referenceX; }
        void Awake() => origin = transform.position;
        void LateUpdate()
        {
            if (view) transform.position = origin + Vector3.right * ((view.transform.position.x - referenceCameraX) * (1f - motionRatio));
        }
    }
}
