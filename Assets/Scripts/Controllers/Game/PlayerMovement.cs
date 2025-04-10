using UnityEngine;

namespace CosmicraftsSP
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float rotationSpeed = 12f;
        public float acceleration = 5f;
        public float deceleration = 8f;

        private Vector3 currentVelocity;
        private Transform mainCameraTransform;

        void Start()
        {
            // Get reference to main camera
            mainCameraTransform = Camera.main.transform;
        }

        void Update()
        {
            HandleMovementInput();
            ApplyMovement();
        }

        void HandleMovementInput()
        {
            // Get normalized input vector
            Vector3 input = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                0,
                Input.GetAxisRaw("Vertical")
            ).normalized;

            // Convert input to camera-relative direction
            Vector3 moveDirection = GetCameraRelativeDirection(input);

            // Calculate target velocity
            Vector3 targetVelocity = moveDirection * moveSpeed;

            // Smoothly interpolate velocity
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                (moveDirection.magnitude > 0 ? acceleration : deceleration) * Time.deltaTime
            );
        }

        void ApplyMovement()
        {
            if (currentVelocity.magnitude > 0.01f) // ✅ Prevent LookRotation error
            {
                // Move character
                transform.position += currentVelocity * Time.deltaTime;

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(currentVelocity.normalized);
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        Vector3 GetCameraRelativeDirection(Vector3 input)
        {
            // Get camera forward and right vectors (ignoring Y-axis)
            Vector3 cameraForward = mainCameraTransform.forward;
            Vector3 cameraRight = mainCameraTransform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            return (cameraForward * input.z + cameraRight * input.x).normalized;
        }
    }
}