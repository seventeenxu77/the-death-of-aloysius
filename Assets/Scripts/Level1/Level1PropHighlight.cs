using UnityEngine;

namespace Aloysius.Level1
{
    /// <summary>Outline activation uses the actual 2D collider distance, not center proximity.</summary>
    [DefaultExecutionOrder(600)]
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class Level1PropHighlight : MonoBehaviour
    {
        [SerializeField] Collider2D playerCollider;
        [SerializeField] Collider2D objectCollider;
        [SerializeField] Renderer visualRenderer;
        [SerializeField, Min(0f)] float contactTolerance = .002f;
        [SerializeField, Min(.01f)] float fadeSeconds = .08f;
        MaterialPropertyBlock properties;
        static readonly int Highlight = Shader.PropertyToID("_Highlight");
        public bool IsContacting { get; private set; }
        public float Strength { get; private set; }
        public bool Held { get; set; }
        public Collider2D ObjectCollider => objectCollider;
        public Collider2D PlayerCollider => playerCollider;
        public Renderer VisualRenderer => visualRenderer;

        public void Configure(Collider2D player)
        {
            playerCollider = player;
            objectCollider = GetComponent<Collider2D>();
            visualRenderer = GetComponent<Renderer>();
            objectCollider.isTrigger = true;
            SetStrength(0f);
        }
        void Awake()
        {
            if (!objectCollider) objectCollider = GetComponent<Collider2D>();
            if (!visualRenderer) visualRenderer = GetComponent<Renderer>();
            SetStrength(0f);
        }
        void LateUpdate() => Evaluate(Time.deltaTime);
        void OnDisable() { IsContacting = false; SetStrength(0f); }

        static bool Active(Collider2D shape)
        {
            return shape && shape.enabled && shape.gameObject.activeInHierarchy
                && (!shape.attachedRigidbody || shape.attachedRigidbody.simulated);
        }

        public void Evaluate(float dt)
        {
            IsContacting = false;
            if (isActiveAndEnabled && Active(playerCollider) && Active(objectCollider))
            {
                // The existing walker changes Transform in Update. Synchronize before
                // querying, so jumping, turning and R-reset are reflected this frame.
                Physics2D.SyncTransforms();
                ColliderDistance2D distance = objectCollider.Distance(playerCollider);
                // Physics2D includes a small polygon skin in distance queries. Require
                // the visible collider bounds to meet too, preventing early highlights.
                Bounds a = objectCollider.bounds, b = playerCollider.bounds;
                bool boundsMeet = a.min.x <= b.max.x + contactTolerance
                    && b.min.x <= a.max.x + contactTolerance
                    && a.min.y <= b.max.y + contactTolerance
                    && b.min.y <= a.max.y + contactTolerance;
                IsContacting = boundsMeet && distance.isValid
                    && (distance.isOverlapped || distance.distance <= contactTolerance);
            }
            SetStrength(Mathf.MoveTowards(Strength, IsContacting || Held ? 1f : 0f,
                Mathf.Max(0f, dt) / Mathf.Max(.01f, fadeSeconds)));
        }

        void SetStrength(float value)
        {
            Strength = value;
            if (!visualRenderer) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            visualRenderer.GetPropertyBlock(properties);
            properties.SetFloat(Highlight, value);
            visualRenderer.SetPropertyBlock(properties);
        }
    }
}
