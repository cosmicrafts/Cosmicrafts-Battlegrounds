using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace Cosmicrafts
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float rotationSpeed = 12f;
        public float acceleration = 5f;
        public float deceleration = 8f;
        
        [Header("Combat Settings")]
        public float returnToMovementRotationDelay = 0.5f; // Delay before returning to movement-based rotation
        
        private Vector3 currentVelocity;
        private Transform mainCameraTransform;
        private Shooter shooter;
        private float targetingTimer;
        private bool wasTargeting;
        
        void Start()
        {
            // Get reference to main camera
            mainCameraTransform = Camera.main ? Camera.main.transform : null;
            if (mainCameraTransform == null)
            {
                Debug.LogWarning("Main camera not found - camera relative movement may not work correctly");
            }
            
            // Get shooter component
            shooter = GetComponent<Shooter>();
            if (shooter == null)
            {
                Debug.Log("No Shooter component found on this object");
            }
        }

        void Update()
        {
            HandleMovementInput();
            ApplyMovement();
        }

        void HandleMovementInput()
        {
            // Get normalized input vector from InputManager
            Vector2 input = InputManager.GetMoveInput();
            
            Vector3 moveInput = new Vector3(input.x, 0, input.y);

            // Convert input to camera-relative direction
            Vector3 moveDirection = GetCameraRelativeDirection(moveInput);

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
            // Move character
            if (currentVelocity.magnitude > 0.01f)
            {
                transform.position += currentVelocity * Time.deltaTime;
                
                // Handle rotation based on targeting state
                HandleRotation();
            }
        }
        
        void HandleRotation()
        {
            bool isCurrentlyTargeting = shooter != null && shooter.GetIdTarget() != 0;
            
            // If we just stopped targeting, start the timer
            if (wasTargeting && !isCurrentlyTargeting)
            {
                targetingTimer = returnToMovementRotationDelay;
            }
            
            // Count down timer if needed
            if (targetingTimer > 0)
            {
                targetingTimer -= Time.deltaTime;
            }
            
            // Only apply movement-based rotation if:
            // 1. Not currently targeting an enemy AND
            // 2. The return-to-movement delay has expired
            if (!isCurrentlyTargeting && targetingTimer <= 0)
            {
                // Before creating rotation, ensure vector is not zero
                Vector3 normalizedVelocity = currentVelocity.normalized;
                
                // Double-check that normalized velocity is valid for look rotation
                if (normalizedVelocity.sqrMagnitude > 0.001f)
                {
                    // Rotate towards movement direction
                    Quaternion targetRotation = Quaternion.LookRotation(normalizedVelocity);
                    transform.rotation = Quaternion.Lerp(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed * Time.deltaTime
                    );
                }
            }
            
            // Remember targeting state for next frame
            wasTargeting = isCurrentlyTargeting;
        }

        Vector3 GetCameraRelativeDirection(Vector3 input)
        {
            // Safe check for camera
            if (mainCameraTransform == null)
            {
                mainCameraTransform = Camera.main?.transform;
                if (mainCameraTransform == null)
                    return Vector3.forward; // Default direction if no camera
            }
            
            // Get camera forward and right vectors (ignoring Y-axis)
            Vector3 cameraForward = mainCameraTransform.forward;
            Vector3 cameraRight = mainCameraTransform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            
            // Safely normalize
            if (cameraForward.sqrMagnitude > 0.001f)
                cameraForward.Normalize();
            else
                cameraForward = Vector3.forward;
                
            if (cameraRight.sqrMagnitude > 0.001f)
                cameraRight.Normalize();
            else
                cameraRight = Vector3.right;

            Vector3 result = (cameraForward * input.z + cameraRight * input.x);
            
            // Safely normalize the result
            if (result.sqrMagnitude > 0.001f)
                return result.normalized;
            else
                return Vector3.zero; // Return zero if no input
        }
        
        // Public accessor for the player's movement direction - used by spells for aiming
        public Vector3 GetLastMoveDirection()
        {
            // If we have significant velocity, use it
            if (currentVelocity.sqrMagnitude > 0.01f)
            {
                return currentVelocity.normalized;
            }
            // Otherwise return the forward direction
            return transform.forward;
        }
    }
}