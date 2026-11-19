using Aloysius.TemplateWhitebox;
using UnityEngine;

namespace Aloysius.RoyalScene
{
    [DisallowMultipleComponent]
    public sealed class RoyalLayoutDoor : MonoBehaviour
    {
        public Transform leaf;
        public float openAngle = 95f;
        public float speed = 2.5f;
        public float interactionDistance = 4f;
        public bool startsOpen;
        public bool oneWay;
        [TextArea] public string futurePuzzleCondition;
        public Vector3 allowedSideLocal = Vector3.back;

        Quaternion initialLocalRotation;
        float openness;
        bool targetOpen;
        bool initialized;

        public bool IsOpen => initialized ? targetOpen : startsOpen;

        void Awake()
        {
            Initialize();
        }

        bool Initialize()
        {
            if (!leaf) return false;
            if (initialized) return true;

            initialLocalRotation = leaf.localRotation;
            targetOpen = startsOpen;
            openness = startsOpen ? 1f : 0f;
            initialized = true;
            ApplyPose();
            return true;
        }

        void Update()
        {
            if (!Initialize()) return;

            openness = Mathf.MoveTowards(openness, targetOpen ? 1f : 0f,
                Mathf.Max(0f, speed) * Time.deltaTime);
            ApplyPose();

            if (!Application.isFocused || !Input.GetKeyDown(KeyCode.F)) return;
            Camera camera = Camera.main;
            if (!camera) return;
            TemplateFirstPerson player = camera.GetComponentInParent<TemplateFirstPerson>();
            if (player && player.InputSuspended) return;

            Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(0f, interactionDistance),
                ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return;

            // Only the opening attempt is directional. Once open, the doorway
            // remains a normal physical passage and may be closed from either side.
            if (!targetOpen && oneWay && !IsOnAllowedSide(camera.transform.position)) return;
            Toggle();
        }

        bool IsOnAllowedSide(Vector3 worldPosition)
        {
            if (allowedSideLocal.sqrMagnitude < .000001f) return true;
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            return Vector3.Dot(localPosition, allowedSideLocal.normalized) > 0f;
        }

        void ApplyPose()
        {
            if (!leaf) return;
            Quaternion opened = initialLocalRotation * Quaternion.Euler(0f, openAngle, 0f);
            leaf.localRotation = Quaternion.Slerp(initialLocalRotation, opened, openness);
        }

        void SetOpen(bool open)
        {
            if (!Initialize()) return;
            targetOpen = open;
            if (!Application.isPlaying)
            {
                openness = open ? 1f : 0f;
                ApplyPose();
            }
        }

        [ContextMenu("Open")]
        public void Open() { SetOpen(true); }

        [ContextMenu("Close")]
        public void Close() { SetOpen(false); }

        [ContextMenu("Toggle")]
        public void Toggle()
        {
            if (Initialize()) SetOpen(!targetOpen);
        }
    }
}
