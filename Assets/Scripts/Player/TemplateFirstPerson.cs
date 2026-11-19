using UnityEngine;

namespace Aloysius.TemplateWhitebox
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TemplateFirstPerson : MonoBehaviour
    {
        public Camera eyes;
        [Min(.1f)] public float movementSpeed = 3.5f;
        [Min(.1f)] public float mouseSensitivity = 1.8f;

        CharacterController controller;
        float pitch;
        bool inputSuspended;

        public bool InputSuspended => inputSuspended;
        public CharacterController Controller =>
            controller ? controller : GetComponent<CharacterController>();

        void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (!eyes)
                eyes = GetComponentInChildren<Camera>();

            RefreshPitchFromCamera();
        }

        void OnEnable()
        {
            if (!inputSuspended)
                CaptureCursor(true);
        }

        void OnDisable()
        {
            CaptureCursor(false);
        }

        public void CaptureCursor(bool capture)
        {
            Cursor.lockState = capture
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = !capture;
        }

        public void SetInputSuspended(
            bool suspended,
            bool captureWhenResuming = true)
        {
            inputSuspended = suspended;
            CaptureCursor(!suspended && captureWhenResuming);
        }

        public void RefreshPitchFromCamera()
        {
            if (eyes)
                pitch = Mathf.DeltaAngle(
                    0f,
                    eyes.transform.localEulerAngles.x);
        }

        void Update()
        {
            if (inputSuspended || !controller || !controller.enabled)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
                CaptureCursor(false);

            if (Input.GetMouseButtonDown(0))
                CaptureCursor(true);

            if (Cursor.lockState != CursorLockMode.Locked ||
                !Application.isFocused)
                return;

            // 鼠标控制视角。
            transform.Rotate(
                0f,
                Input.GetAxisRaw("Mouse X") * mouseSensitivity,
                0f);

            pitch = Mathf.Clamp(
                pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity,
                -85f,
                85f);

            if (eyes)
                eyes.transform.localRotation =
                    Quaternion.Euler(pitch, 0f, 0f);

            // WASD 沿摄像机朝向移动。
            Transform view = eyes ? eyes.transform : transform;

            Vector3 direction =
                view.forward * Input.GetAxisRaw("Vertical") +
                view.right * Input.GetAxisRaw("Horizontal");

            // E 上升，Q 下降，使用世界竖直方向。
            if (Input.GetKey(KeyCode.E))
                direction += Vector3.up;

            if (Input.GetKey(KeyCode.Q))
                direction += Vector3.down;

            // 限制斜向移动速度；没有重力，因此松手后悬浮。
            direction = Vector3.ClampMagnitude(direction, 1f);

            controller.Move(
                direction * movementSpeed * Time.deltaTime);
        }
    }
}