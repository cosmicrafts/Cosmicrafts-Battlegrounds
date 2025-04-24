using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace Cosmicrafts
{
    [AddComponentMenu("Cosmicrafts/PlayerMovement")]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 8f;
        public float rotationSpeed = 12f;
        public float acceleration = 5f;
        public float deceleration = 8f;
        
        [Header("Combat Settings")]
        public float returnToMovementRotationDelay = 0.5f; // Delay before returning to movement-based rotation
        
        [Header("Spaceship Kinetic Rotation")]
        [Tooltip("Transform that should receive kinetic rotation effects (visual model)")]
        public Transform kineticTransform;
        
        [Header("Banking (Z-axis / Roll)")]
        [Tooltip("Maximum roll angle when turning (degrees)")]
        [Range(0f, 45f)] 
        public float maxBankAngle = 20f;
        [Tooltip("How quickly the banking tilt is applied")]
        [Range(1f, 20f)]
        public float bankSpeed = 6f;
        [Tooltip("How quickly the banking returns to neutral")]
        [Range(1f, 20f)]
        public float bankRecoverySpeed = 4f;
        [Tooltip("Multiplier for turning effect (higher = more dramatic banking)")]
        [Range(0.1f, 10f)]
        public float bankIntensity = 2.5f;
        
        [Header("Pitching (X-axis)")]
        [Tooltip("Maximum pitch angle during acceleration/deceleration (degrees)")]
        [Range(0f, 45f)]
        public float maxPitchAngle = 10f;
        [Tooltip("How quickly the pitch tilt is applied")]
        [Range(1f, 20f)]
        public float pitchSpeed = 7f;
        [Tooltip("How quickly the pitch returns to neutral")]
        [Range(1f, 20f)]
        public float pitchRecoverySpeed = 5f;
        [Tooltip("Multiplier for acceleration effect (negative = nose down when accelerating)")]
        [Range(-10f, 10f)]
        public float pitchIntensity = -3f;
        
        [Header("Forward Lean (X-axis)")]
        [Tooltip("Constant forward lean angle applied during movement (degrees)")]
        [Range(-45f, 45f)]
        public float forwardLeanAngle = -2f;
        [Tooltip("How quickly the forward lean is applied")]
        [Range(1f, 20f)]
        public float leanSpeed = 5f;
        
        [Header("Hover Effect (Y-axis)")]
        [Tooltip("Enable subtle bobbing motion")]
        public bool enableHoverEffect = true;
        [Tooltip("Hover bobbing amount in local Y axis")]
        [Range(0f, 0.5f)]
        public float hoverAmount = 0.1f;
        [Tooltip("Hover bobbing speed")]
        [Range(0.1f, 5f)]
        public float hoverSpeed = 1f;
        
        [Header("Debug")]
        [Tooltip("Show debug info in console")]
        public bool showKineticDebug = false;
        
        // Private kinetic rotation variables
        private float currentBankAngle = 0f; // Current Z-axis rotation
        private float currentPitchAngle = 0f; // Current X-axis rotation from acceleration
        private float currentLeanAngle = 0f; // Current X-axis rotation from constant lean
        private Vector3 lastMoveDirection = Vector3.zero;
        private float lastSpeedMagnitude = 0f;
        private Vector3 kineticInitialPosition; // Store initial local position for hover effect
        private float hoverTimer = 0f;
        
        [Header("Dash Settings")]
        public float dashDistance = 15f;
        public float dashDuration = 0.1f;
        public float dashCooldown = 1.5f;
        public bool useBlink = false;
        public bool leavesTrail = true;
        public KeyCode dashKey = KeyCode.Space;
        public GameObject dashEffectPrefab;
        
        [Header("Dash Animation")]
        [Tooltip("Controls how the dash accelerates and decelerates")]
        public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0f, 2f)]
        [Tooltip("Higher values make the dash 'overshoot' the target before settling")]
        public float overshootAmount = 0.15f;
        [Range(0f, 1f)]
        [Tooltip("Controls how much the character slows down at the end of the dash")]
        public float endSlowdownFactor = 0.2f;
        [Range(0.1f, 2f)]
        [Tooltip("Controls the size of the trail elements")]
        public float trailScale = 0.3f;
        
        [Header("Debug Options")]
        public bool bypassEnergyCheck = false;
        
        private Vector3 currentVelocity;
        private Transform mainCameraTransform;
        private Shooter shooter;
        private float targetingTimer;
        private bool wasTargeting;
        
        // Dash variables
        private bool isDashing = false;
        private float dashTimer = 0f;
        private float dashCooldownTimer = 0f;
        private Vector3 dashStartPosition;
        private Vector3 dashEndPosition;
        private Vector3 dashDirection;
        
        private Collider playerCollider;
        private bool wasCollisionEnabled = true;
        
        // Create a simple pool manager for your markers
        private Queue<GameObject> markerPool = new Queue<GameObject>();
        private int poolSize = 50;
        
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
            
            // Get player collider
            playerCollider = GetComponent<Collider>();
            
            // Initialize dash cooldown as ready
            dashCooldownTimer = 0f;
            
            // Force dash key to Space for better gameplay
            dashKey = KeyCode.Space;
            
            // Pre-create your markers
            for (int i = 0; i < poolSize; i++) {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                marker.SetActive(false);
                markerPool.Enqueue(marker);
            }
            
            // Initialize kinetic transform
            if (kineticTransform != null) {
                // Store initial position for hover effect
                kineticInitialPosition = kineticTransform.localPosition;
            }
        }

        void Update()
        {
            // Handle dash input and cooldown
            UpdateDashState();
            
            // If we're dashing, handle that separately
            if (isDashing)
            {
                ExecuteDash();
                return;
            }
            
            HandleMovementInput();
            ApplyMovement();
            
            // Apply hover effect (separate from rotation)
            ApplyHoverEffect();
        }
        
        // Apply subtle hover/bob effect to the kinetic transform
        void ApplyHoverEffect() 
        {
            if (!enableHoverEffect || kineticTransform == null) return;
            
            hoverTimer += Time.deltaTime * hoverSpeed;
            
            if (hoverTimer > 2 * Mathf.PI) hoverTimer -= 2 * Mathf.PI;
            
            // Calculate bobbing vertical position
            float yOffset = Mathf.Sin(hoverTimer) * hoverAmount;
            
            // Apply to local position (preserving x and z)
            Vector3 newPosition = kineticInitialPosition;
            newPosition.y += yOffset;
            kineticTransform.localPosition = newPosition;
        }
        
        void UpdateDashState()
        {
            // Update dash cooldown timer
            if (dashCooldownTimer > 0)
            {
                dashCooldownTimer -= Time.deltaTime;
            }
            
            // Check for dash input if not dashing and cooldown is ready
            if (Input.GetKeyDown(dashKey))
            {
                if (isDashing)
                {
                    return;
                }
                
                if (dashCooldownTimer > 0)
                {
                    return;
                }
                
                // Check if player has enough energy - use GameMng.P instead of playerComponent
                if (!bypassEnergyCheck && GameMng.P != null && !HasEnoughEnergy())
                {
                    return;
                }
                
                // USE INPUT DIRECTION INSTEAD OF TRANSFORM.FORWARD
                // Get current movement input
                Vector3 input = new Vector3(
                    Input.GetAxisRaw("Horizontal"),
                    0,
                    Input.GetAxisRaw("Vertical")
                ).normalized;
                
                // If we have input, use it for dash direction
                if (input.sqrMagnitude > 0.01f)
                {
                    // Convert input to camera-relative direction
                    dashDirection = GetCameraRelativeDirection(input);
                }
                else
                {
                    // Fall back to current facing direction if no input
                    dashDirection = transform.forward;
                }
                
                // Calculate start and end positions
                dashStartPosition = transform.position;
                dashEndPosition = dashStartPosition + (dashDirection * dashDistance);
                
                // Start the dash
                isDashing = true;
                dashTimer = 0f;
                
                // Temporarily disable collisions during the dash to prevent getting stuck
                if (playerCollider != null)
                {
                    wasCollisionEnabled = playerCollider.enabled;
                    playerCollider.enabled = false;
                }
                
                // Trigger warp animation on unit
                Unit playerUnit = GetComponent<Unit>();
                if (playerUnit != null)
                {
                    playerUnit.PlayWarpAnimation();
                }
                
                // Consume energy using GameMng.P
                if (!bypassEnergyCheck && GameMng.P != null)
                {
                    ConsumeEnergy();
                }
                
                // If it's a blink, just teleport
                if (useBlink)
                {
                    // Perform raycast to ensure we don't blink through walls
                    RaycastHit hit;
                    if (Physics.Raycast(dashStartPosition, dashDirection, out hit, dashDistance))
                    {
                        // If we hit something, blink to just before the hit point
                        dashEndPosition = hit.point - (dashDirection * 0.5f);
                    }
                    
                    // Teleport to end position
                    transform.position = dashEndPosition;
                    
                    // Create effect if prefab is assigned
                    if (dashEffectPrefab != null)
                    {
                        Instantiate(dashEffectPrefab, dashStartPosition, Quaternion.identity);
                        Instantiate(dashEffectPrefab, dashEndPosition, Quaternion.identity);
                    }
                    
                    // End dash immediately
                    isDashing = false;
                    dashCooldownTimer = dashCooldown;
                }
            }
        }
        
        bool HasEnoughEnergy()
        {
            // Default energy cost if not configured in Player
            float cost = 2f;
            
            // Try to get energy cost from Player if available
            if (GameMng.P.dashEnergyCost > 0)
            {
                cost = GameMng.P.dashEnergyCost;
            }
            
            // Check if player has enough energy
            return GameMng.P.CurrentEnergy >= cost;
        }
        
        void ConsumeEnergy()
        {
            // Default energy cost if not configured in Player
            float cost = 2f;
            
            // Try to get energy cost from Player if available
            if (GameMng.P.dashEnergyCost > 0)
            {
                cost = GameMng.P.dashEnergyCost;
            }
            
            // Consume energy
            GameMng.P.RestEnergy(cost);
        }
        
        void ExecuteDash()
        {
            // Increment timer
            dashTimer += Time.deltaTime;
            
            // Calculate progress (0 to 1)
            float progress = Mathf.Clamp01(dashTimer / dashDuration);
            
            // Store previous position for debugging
            Vector3 oldPosition = transform.position;
            
            // Apply animation curve for Pixar-like motion
            float curvedProgress = dashCurve.Evaluate(progress);
            
            // Apply overshoot if configured (makes dash feel more dynamic)
            float animatedProgress = curvedProgress;
            if (overshootAmount > 0 && progress > 0.5f && progress < 0.9f) {
                // Create a subtle overshoot effect in the middle of the dash
                animatedProgress += Mathf.Sin((progress - 0.5f) * 3f * Mathf.PI) * overshootAmount * (1f - progress);
            }
            
            // Apply slight slowdown at the end for more graceful finish
            if (progress > 0.7f) {
                float slowdownFactor = Mathf.Lerp(1f, 1f - endSlowdownFactor, (progress - 0.7f) / 0.3f);
                animatedProgress = Mathf.Lerp(curvedProgress, 1f, slowdownFactor);
            }
            
            // Calculate the current position along the dash path
            Vector3 targetPosition = Vector3.Lerp(dashStartPosition, dashEndPosition, animatedProgress);
            
            // Apply a subtle arc to the dash path for more visual interest
            if (progress > 0.1f && progress < 0.9f) {
                float arcHeight = dashDistance * 0.05f * Mathf.Sin(progress * Mathf.PI);
                targetPosition += Vector3.up * arcHeight;
            }
            
            // Move along the dash path
            transform.position = targetPosition;
            
            // Visual trail effects
            if (leavesTrail) 
            {
                // Calculate trail density based on speed
                float distanceMoved = Vector3.Distance(oldPosition, transform.position);
                int trailCount = Mathf.Clamp(Mathf.CeilToInt(distanceMoved * 2f), 1, 3);
                
                for (int i = 0; i < trailCount; i++) {
                    float lerpFactor = i / (float)trailCount;
                    Vector3 trailPos = Vector3.Lerp(oldPosition, transform.position, lerpFactor);
                    
                    // Get a marker from pool
                    GameObject marker = GetMarker();
                    
                    // Scale marker based on speed and progress
                    float dynamicScale = trailScale * (1f - 0.5f * progress);
                    marker.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);
                    marker.transform.position = trailPos;
                    
                    // Start with yellow, end with red
                    if (marker.GetComponent<Renderer>())
                    {
                        // Use more interesting color progression
                        Color startColor = new Color(1f, 0.8f, 0.2f);  // Vibrant yellow
                        Color endColor = new Color(1f, 0.2f, 0.1f);    // Deep red
                        marker.GetComponent<Renderer>().material.color = Color.Lerp(startColor, endColor, progress);
                        
                        // Add slight opacity change
                        Color currentColor = marker.GetComponent<Renderer>().material.color;
                        currentColor.a = Mathf.Lerp(0.8f, 0.4f, progress);
                        marker.GetComponent<Renderer>().material.color = currentColor;
                    }
                    
                    // Schedule marker to return to pool
                    StartCoroutine(ReturnMarkerAfterDelay(marker, 0.7f * (1f - progress * 0.5f)));
                }
                
                // Create a bigger flash at the start position on first frame
                if (dashTimer < Time.deltaTime)
                {
                    GameObject startFlash = GetMarker();
                    startFlash.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    startFlash.transform.position = dashStartPosition;
                    if (startFlash.GetComponent<Renderer>())
                        startFlash.GetComponent<Renderer>().material.color = new Color(1f, 0.9f, 0.2f, 0.9f);
                    StartCoroutine(ReturnMarkerAfterDelay(startFlash, 1.0f));
                }
            }
            
            // Create an effect at destination if we're almost done
            if (progress > 0.8f && progress < 0.85f && dashEffectPrefab != null)
            {
                Instantiate(dashEffectPrefab, dashEndPosition, Quaternion.identity);
            }
            
            // Check if dash is complete
            if (progress >= 1.0f)
            {
                isDashing = false;
                dashCooldownTimer = dashCooldown;
                
                // Restore collision
                if (playerCollider != null)
                {
                    playerCollider.enabled = wasCollisionEnabled;
                }
                
                // Add end effect
                if (leavesTrail) 
                {
                    GameObject endFlash = GetMarker();
                    endFlash.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    endFlash.transform.position = dashEndPosition;
                    if (endFlash.GetComponent<Renderer>())
                        endFlash.GetComponent<Renderer>().material.color = new Color(1f, 0.3f, 0.1f, 0.9f);
                    StartCoroutine(ReturnMarkerAfterDelay(endFlash, 1.0f));
                }
            }
        }
        
        // Helper method to return markers to pool after a delay
        System.Collections.IEnumerator ReturnMarkerAfterDelay(GameObject marker, float delay) {
            yield return new WaitForSeconds(delay);
            ReturnMarker(marker);
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
            // Move character
            if (currentVelocity.magnitude > 0.01f)
            {
                transform.position += currentVelocity * Time.deltaTime;
                
                // Handle rotation based on targeting state
                HandleRotation();
            }
            else
            {
                // If we're not moving, still handle rotation for recovery
                HandleRotation();
            }
        }
        
        void HandleRotation()
        {
            // Check if shooter is actively engaging a target (in combat mode)
            bool isCurrentlyTargeting = shooter != null && shooter.IsEngagingTarget();
            
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
            
            // Get the transform to apply kinetic rotation to (use our transform if not specified)
            Transform targetKineticTransform = kineticTransform != null ? kineticTransform : transform;
            
            // Current velocity values
            Vector3 normalizedVelocity = currentVelocity.normalized;
            float currentSpeedMagnitude = currentVelocity.magnitude;
            
            // Target rotation angles (will be calculated based on movement)
            float targetBankAngle = 0f;
            float targetPitchAngle = 0f;
            float targetLeanAngle = 0f;
            
            // Only apply full movement-based rotation if:
            // 1. Not currently targeting an enemy AND
            // 2. The return-to-movement delay has expired
            if (!isCurrentlyTargeting && targetingTimer <= 0)
            {
                // Double-check that normalized velocity is valid for look rotation
                if (normalizedVelocity.sqrMagnitude > 0.001f)
                {
                    // -- 1. Calculate Banking (Z-axis) based on turning --
                    if (lastMoveDirection.sqrMagnitude > 0.001f)
                    {
                        // Calculate turn amount using SignedAngle for better results
                        float turnAmount = Vector3.SignedAngle(lastMoveDirection, normalizedVelocity, Vector3.up);
                        
                        // Calculate target bank angle - negative because we want to bank INTO the turn
                        // Scale based on max angle and clamp to limits
                        targetBankAngle = -turnAmount * bankIntensity * (maxBankAngle / 90f);
                        targetBankAngle = Mathf.Clamp(targetBankAngle, -maxBankAngle, maxBankAngle);
                    }
                    
                    // -- 2. Calculate Pitch (X-axis) based on acceleration/deceleration --
                    // Calculate acceleration (change in speed over time)
                    float accelerationValue = (currentSpeedMagnitude - lastSpeedMagnitude) / Time.deltaTime;
                    
                    // Translate acceleration to pitch angle
                    // Negative pitchIntensity = nose down when accelerating forward (realistic feeling)
                    targetPitchAngle = accelerationValue * pitchIntensity * (maxPitchAngle / 10f);
                    targetPitchAngle = Mathf.Clamp(targetPitchAngle, -maxPitchAngle, maxPitchAngle);
                    
                    // -- 3. Apply constant forward lean when moving --
                    targetLeanAngle = forwardLeanAngle * Mathf.Clamp01(currentSpeedMagnitude / moveSpeed);
                    
                    // --- Apply Forward Direction to main transform ---
                    // The primary transform always rotates to face movement direction
                    Quaternion targetForwardRotation = Quaternion.LookRotation(normalizedVelocity);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetForwardRotation,
                        rotationSpeed * Time.deltaTime
                    );
                    
                    if (showKineticDebug)
                    {
                        Debug.Log($"Kinetic - Speed: {currentSpeedMagnitude:F2}, Accel: {accelerationValue:F2}, Turn: {targetBankAngle:F2}° | " +
                                  $"Bank(Z): {currentBankAngle:F2}° → {targetBankAngle:F2}° | " +
                                  $"Pitch(X): {currentPitchAngle:F2}° → {targetPitchAngle:F2}° | " +
                                  $"Lean(X): {currentLeanAngle:F2}° → {targetLeanAngle:F2}°");
                    }
                    
                    // Store direction for next frame's comparison
                    lastMoveDirection = normalizedVelocity;
                }
                else
                {
                    // When velocity is zero or near-zero, still handle recovery to neutral position
                    targetBankAngle = 0f;
                    targetPitchAngle = 0f;
                    targetLeanAngle = 0f;
                    lastMoveDirection = Vector3.zero;
                }
            }
            else
            {
                // When targeting enemies, gradually return to neutral position
                targetBankAngle = 0f;
                targetPitchAngle = 0f;
                targetLeanAngle = 0f;
            }
            
            // --- Smoothly interpolate current rotation angles toward targets ---
            currentBankAngle = Mathf.Lerp(currentBankAngle, targetBankAngle, bankSpeed * Time.deltaTime);
            currentPitchAngle = Mathf.Lerp(currentPitchAngle, targetPitchAngle, pitchSpeed * Time.deltaTime);
            currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLeanAngle, leanSpeed * Time.deltaTime);
            
            // --- Apply combined kinetic rotation to the visual transform ---
            // Only apply if we have a separate transform for kinetic effects
            if (targetKineticTransform != transform)
            {
                // Combine all rotational effects (X = pitch+lean, Z = bank)
                targetKineticTransform.localRotation = Quaternion.Euler(
                    currentPitchAngle + currentLeanAngle, 
                    0, 
                    currentBankAngle
                );
            }
            else
            {
                // If we're applying to the main transform, blend with current rotation
                // This is trickier since we don't want to disrupt the forward direction
                // For simplicity, only apply the bank effect in this case
                transform.rotation *= Quaternion.Euler(0, 0, currentBankAngle);
            }
            
            // Update last speed for next frame's acceleration calculation
            lastSpeedMagnitude = currentSpeedMagnitude;
            
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
        
        // Public accessor for dash cooldown status (for UI)
        public float GetDashCooldownPercent()
        {
            if (dashCooldownTimer <= 0)
                return 1.0f; // Ready
            
            return 1.0f - (dashCooldownTimer / dashCooldown);
        }
        
        // Public accessor for dash status
        public bool IsDashing()
        {
            return isDashing;
        }
        
        // Public property for animation system to detect warping
        public bool IsWarping
        {
            get { return isDashing; }
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

        // Get a marker from pool
        GameObject GetMarker() {
            if (markerPool.Count > 0) {
                GameObject marker = markerPool.Dequeue();
                marker.SetActive(true);
                return marker;
            }
            return GameObject.CreatePrimitive(PrimitiveType.Sphere);
        }

        // Return marker to pool
        void ReturnMarker(GameObject marker) {
            marker.SetActive(false);
            markerPool.Enqueue(marker);
        }
    }
} 