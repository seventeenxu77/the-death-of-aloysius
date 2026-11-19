using UnityEngine;

namespace Aloysius.Level1
{
    /// <summary>Authoring references only. Exit traversal and guard behaviour are a later task.</summary>
    public sealed class Level1OutdoorArea : MonoBehaviour
    {
        [Header("出口与落地位置（场景中可直接移动）")]
        [SerializeField] Transform suspendedPipeExit;
        [SerializeField] Transform landingPoint;
        [SerializeField] BoxCollider2D concreteGround;
        [Header("独立遮挡物")]
        [SerializeField] Transform[] crates;
        [Header("分层场景")]
        [SerializeField] Level1OutdoorParallax[] depthLayers;
        public Transform SuspendedPipeExit => suspendedPipeExit;
        public Transform LandingPoint => landingPoint;
        public BoxCollider2D ConcreteGround => concreteGround;

        public void Configure(Transform exit, Transform landing, BoxCollider2D ground,
            Transform[] cover, Level1OutdoorParallax[] layers)
        { suspendedPipeExit = exit; landingPoint = landing; concreteGround = ground; crates = cover; depthLayers = layers; }

        void OnDrawGizmosSelected()
        {
            if (!suspendedPipeExit || !landingPoint) return;
            Gizmos.color = new Color(.8f, .65f, .3f);
            Gizmos.DrawWireSphere(suspendedPipeExit.position, .12f);
            Gizmos.DrawLine(suspendedPipeExit.position, landingPoint.position);
            Gizmos.DrawWireCube(landingPoint.position + Vector3.up * .03f, new Vector3(1.4f, .06f, .2f));
        }
    }
}
