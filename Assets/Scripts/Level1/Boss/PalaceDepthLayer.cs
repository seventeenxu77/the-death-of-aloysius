using UnityEngine;
namespace Aloysius.Level1.Boss
{
    [DefaultExecutionOrder(1300)]
    public sealed class PalaceDepthLayer:MonoBehaviour
    {
        [Range(0,1)]public float parallax=.08f;
        public Vector2 cameraOrigin;
        Vector3 rest;
        void Awake(){rest=transform.position;}
        void LateUpdate(){var c=Camera.main;if(c){var d=(Vector2)c.transform.position-cameraOrigin;transform.position=rest+(Vector3)(d*parallax);}}
    }
}
