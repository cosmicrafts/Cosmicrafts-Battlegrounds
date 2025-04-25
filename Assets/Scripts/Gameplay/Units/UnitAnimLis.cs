using UnityEngine;
using System.Collections;

namespace Cosmicrafts 
{
    /*
     * Kinetic Animation Controller for Units
     * 
     * Handles all visual animation states using procedural code-based techniques.
     * Manipulates a designated 'kineticTransform' (usually the visual model).
     * Provides customizable styles for different animation types.
     */
    public class UnitAnimLis : MonoBehaviour
    {
        #region Enums for Styles

        public enum IdleStyle
        {
            GentleHover, // Subtle sine wave hover, minimal wobble
            HeavyFloat,  // Slower, larger hover, slight pitch/roll wobble
            AgitatedBuzz // Faster hover, quick small jitters/wobbles
        }

        public enum MoveStyle
        {
            SmoothFlight,   // Moderate banking/pitch, smooth leans
            AggressiveStrafe,// Sharp banking/pitch, strong lean, quick recovery
            HeavyDrift      // Slow banking/pitch, minimal lean, delayed response
        }

        public enum AttackStyle
        {
            QuickRecoil, // Sharp, short recoil pitch, quick recovery, minimal shake
            HeavyCannon, // Significant recoil pitch, slower recovery, strong shake
            EnergyPulse  // Minimal physical recoil, visual pulse (scale/color - requires extra setup)
        }

        public enum EntryStyle
        {
            FastDrop,     // Quick drop from above, sharp bounce, quick scale punch
            PortalEmerge, // Slow scale-up from center, minimal drop (visual portal effect recommended)
            Materialize   // Fade-in effect (requires shader/material control), gentle settle
        }

        public enum WarpStyle
        {
            QuickBlink, // Very fast duration, strong scale shrink/grow, sharp spin
            PhaseShift, // Medium duration, slight scale/position wobble (visual blur recommended)
            StreakingWarp// Longer duration, scale stretch along movement axis (visual trail recommended)
        }

        public enum PowerUpStyle
        {
            SteadyGlow,    // Simple scale pulse, gentle intensity curve
            EnergySurge,   // Fast, strong pulses, sharp intensity curve (visual effects recommended)
            ShieldOvercharge // Larger scale pulse, focused oscillation (visual shield effect recommended)
        }

        public enum DeathStyle
        {
            QuickExplosion, // Fast spin/tumble, quick shrink/fade (visual explosion recommended)
            DamagedFall,    // Slow spin, heavy tumble based on impact, gradual fall
            EngineFailure   // Erratic spins/tumbles, flickering scale/position, then fall (visual sparks recommended)
        }

        #endregion

        [Header("Core Setup")]
        [Tooltip("Enable debug logs for animation state changes")]
        public bool debugAnimations = false;
        [Tooltip("Transform that receives kinetic animation effects (visual model)")]
        public Transform kineticTransform;
        [Tooltip("Automatically play entry animation when unit spawns")]
        public bool autoPlayEntryAnimation = true;

        [Header("Animation Styles")]
        public IdleStyle idleStyle = IdleStyle.GentleHover;
        public MoveStyle moveStyle = MoveStyle.SmoothFlight;
        public AttackStyle attackStyle = AttackStyle.QuickRecoil;
        public EntryStyle entryStyle = EntryStyle.FastDrop;
        public WarpStyle warpStyle = WarpStyle.QuickBlink;
        public PowerUpStyle powerUpStyle = PowerUpStyle.SteadyGlow;
        public DeathStyle deathStyle = DeathStyle.QuickExplosion;

        // --- Parameters will be derived from Styles, but exposed for potential fine-tuning ---
        // (We'll set these based on the style enums in Start/Update)

        [Header("Derived Idle Settings (Read Only)")]
        [SerializeField][ReadOnly] private float currentHoverAmount;
        [SerializeField][ReadOnly] private float currentHoverSpeed;
        [SerializeField][ReadOnly] private float currentIdleWobbleAmount;

        [Header("Derived Movement Settings (Read Only)")]
        [SerializeField][ReadOnly] private float currentMaxBankAngle;
        [SerializeField][ReadOnly] private float currentBankSpeed;
        [SerializeField][ReadOnly] private float currentMaxPitchAngle;
        [SerializeField][ReadOnly] private float currentPitchSpeed;
        [SerializeField][ReadOnly] private float currentForwardLeanAngle;

        [Header("Derived Attack Settings (Read Only)")]
        [SerializeField][ReadOnly] private float currentAttackRecoilAngle;
        [SerializeField][ReadOnly] private float currentRecoilRecoverySpeed;
        [SerializeField][ReadOnly] private float currentAttackShakeAmount;
        [SerializeField][ReadOnly] private bool currentSyncAttackTiming;

        [Header("Derived Special Anim Settings (Read Only)")]
        [SerializeField][ReadOnly] private float currentEntryDuration;
        [SerializeField][ReadOnly] private float currentWarpDuration;
        [SerializeField][ReadOnly] private float currentPowerUpDuration;
        [SerializeField][ReadOnly] private float currentDeathDuration;
        
        // Core references
        private Unit myUnit;
        private Shooter myShooter;
        private PlayerMovement playerMovement; // For player specific overrides if needed

        // Kinetic animation state
        private Vector3 kineticInitialPosition;
        private Quaternion kineticInitialRotation;
        private Vector3 kineticInitialScale;
        private bool hasPlayedEntryAnimation = false;
        private bool isInitialized = false;

        // Animation state tracking
        private bool isMoving = false;
        private bool wasMoving = false;
        private bool isAttacking = false;
        private bool wasAttacking = false;
        private bool isDead = false;
        private bool isEntryPlaying = false;
        private bool isWarpPlaying = false;
        private bool isPowerUpPlaying = false;

        // Hover animation
        private float hoverTimer = 0f;

        // Movement animation
        private float currentBankAngle = 0f;
        private float currentPitchAngle = 0f;
        private float currentLeanAngle = 0f;
        private Vector3 lastMoveDirection = Vector3.zero;
        private float lastSpeedMagnitude = 0f;

        // Attack animation
        private float currentRecoilAngle = 0f;
        private Vector3 currentShakeOffset = Vector3.zero;
        private float attackAnimTimer = 0f; // Tracks time since last shot for recoil recovery

        // Special animation coroutines
        private Coroutine entryAnimationCoroutine = null;
        private Coroutine warpAnimationCoroutine = null;
        private Coroutine powerUpAnimationCoroutine = null;
        private Coroutine deathAnimationCoroutine = null;

        // Attribute to make fields read-only in the inspector
        public class ReadOnlyAttribute : PropertyAttribute { }

        #if UNITY_EDITOR
        [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
        public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
        {
            public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
            {
                GUI.enabled = false;
                UnityEditor.EditorGUI.PropertyField(position, property, label, true);
                GUI.enabled = true;
            }
        }
        #endif

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            if (isInitialized) return;

            try
            {
                // Get core components
                myUnit = GetComponentInParent<Unit>();
                if (myUnit == null)
                {
                    Debug.LogError($"UnitAnimLis on {gameObject.name} requires a Unit component on its parent!", this);
                    this.enabled = false;
                    return;
                }

                myShooter = GetComponentInParent<Shooter>();
                if (myShooter == null && debugAnimations)
                {
                    Debug.Log($"No Shooter component found on {gameObject.name}'s parent. Attack animations may not work correctly.", this);
                }

                playerMovement = GetComponentInParent<PlayerMovement>();
                if (playerMovement == null && debugAnimations)
                {
                    Debug.Log($"No PlayerMovement component found on {gameObject.name}'s parent. Using standard Unit movement.", this);
                }

                // Validate kinetic transform
                if (kineticTransform == null)
                {
                    kineticTransform = transform; // Use this object's transform as fallback
                    Debug.LogWarning($"Kinetic Transform not assigned on {gameObject.name}. Using self.", this);
                }

                // Store initial transform values
                kineticInitialPosition = kineticTransform.localPosition;
                kineticInitialRotation = kineticTransform.localRotation;
                kineticInitialScale = kineticTransform.localScale;

                // Apply style settings
                ApplyStyles();
                
                isInitialized = true;

                // Play entry animation if enabled
                if (autoPlayEntryAnimation && !hasPlayedEntryAnimation)
                {
                    PlayEntryAnimation();
                }

                if (debugAnimations)
                {
                    Debug.Log($"UnitAnimLis for {gameObject.name} initialized successfully with {idleStyle} idle, {moveStyle} movement, and {attackStyle} attack styles.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize UnitAnimLis on {gameObject.name}: {e.Message}\n{e.StackTrace}", this);
                this.enabled = false;
            }
        }
        
        // Called when styles change in inspector (OnValidate) or at start
        void ApplyStyles()
        {
            // --- Idle Styles ---
            switch (idleStyle)
            {
                case IdleStyle.GentleHover:
                    currentHoverAmount = 0.05f; currentHoverSpeed = 1.0f; currentIdleWobbleAmount = 0.3f; break;
                case IdleStyle.HeavyFloat:
                    currentHoverAmount = 0.15f; currentHoverSpeed = 0.6f; currentIdleWobbleAmount = 0.8f; break;
                case IdleStyle.AgitatedBuzz:
                    currentHoverAmount = 0.03f; currentHoverSpeed = 3.5f; currentIdleWobbleAmount = 1.2f; break;
            }

            // --- Move Styles ---
            switch (moveStyle)
            {
                case MoveStyle.SmoothFlight:
                    currentMaxBankAngle = 15f; currentBankSpeed = 5f; currentMaxPitchAngle = 8f; currentPitchSpeed = 5f; currentForwardLeanAngle = -5f; break;
                case MoveStyle.AggressiveStrafe:
                    currentMaxBankAngle = 35f; currentBankSpeed = 9f; currentMaxPitchAngle = 15f; currentPitchSpeed = 8f; currentForwardLeanAngle = -10f; break;
                case MoveStyle.HeavyDrift:
                    currentMaxBankAngle = 8f;  currentBankSpeed = 2f; currentMaxPitchAngle = 5f;  currentPitchSpeed = 2f; currentForwardLeanAngle = -2f; break;
            }

            // --- Attack Styles ---
            switch (attackStyle)
            {
                case AttackStyle.QuickRecoil:
                    currentAttackRecoilAngle = 4f; currentRecoilRecoverySpeed = 25f; currentAttackShakeAmount = 0.02f; currentSyncAttackTiming = true; break;
                case AttackStyle.HeavyCannon:
                    currentAttackRecoilAngle = 12f; currentRecoilRecoverySpeed = 8f; currentAttackShakeAmount = 0.08f; currentSyncAttackTiming = true; break;
                case AttackStyle.EnergyPulse:
                    currentAttackRecoilAngle = 1f; currentRecoilRecoverySpeed = 15f; currentAttackShakeAmount = 0.01f; currentSyncAttackTiming = true; break; // Suggests visual pulse
            }

            // --- Special Animation Durations (other effects handled in coroutines) ---
             switch (entryStyle)
            {
                case EntryStyle.FastDrop:    currentEntryDuration = 0.7f; break;
                case EntryStyle.PortalEmerge:currentEntryDuration = 1.5f; break;
                case EntryStyle.Materialize: currentEntryDuration = 1.2f; break;
            }
             switch (warpStyle)
            {
                case WarpStyle.QuickBlink:   currentWarpDuration = 0.25f; break;
                case WarpStyle.PhaseShift:   currentWarpDuration = 0.6f; break;
                case WarpStyle.StreakingWarp:currentWarpDuration = 0.8f; break;
            }
            switch (powerUpStyle)
            {
                case PowerUpStyle.SteadyGlow:       currentPowerUpDuration = 1.5f; break;
                case PowerUpStyle.EnergySurge:      currentPowerUpDuration = 0.8f; break;
                case PowerUpStyle.ShieldOvercharge: currentPowerUpDuration = 2.0f; break;
            }
            switch (deathStyle)
            {
                case DeathStyle.QuickExplosion: currentDeathDuration = 0.8f; break;
                case DeathStyle.DamagedFall:    currentDeathDuration = 2.5f; break;
                case DeathStyle.EngineFailure:  currentDeathDuration = 3.0f; break;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (!isInitialized || myUnit == null)
            {
                 Initialize(); // Try to initialize if not done yet
                 if (!isInitialized) return; // Still failed
            }

            // Handle death state first
            if (myUnit.GetIsDeath())
            {
                // Death animation is triggered by Unit.Die() calling PlayDeathAnimation()
                // We don't need to do anything here unless the coroutine finishes
                return; 
            }
            
            // Check if we are currently playing a blocking special animation
            bool isSpecialAnimating = isEntryPlaying || isWarpPlaying || isPowerUpPlaying;

            // Update base states (moving, attacking)
            isMoving = myUnit.IsMoving();
            isAttacking = myShooter != null && myShooter.IsEngagingTarget();

            // Apply kinetic animations ONLY if not playing a special animation
            if (!isSpecialAnimating)
            {
                ApplyKineticAnimations();
            }

             // Check for player warp input (if applicable)
            if (playerMovement != null && playerMovement.IsWarping && !isWarpPlaying && !isSpecialAnimating)
            {
                 PlayWarpAnimation();
            }

            // Remember states for next frame delta calculations
            wasMoving = isMoving;
            wasAttacking = isAttacking;
        }

        // Apply all kinetic animations based on current state
        private void ApplyKineticAnimations()
        {
            if (isDead || kineticTransform == null) return;

            // Start with the base resting state
            Vector3 targetPosition = kineticInitialPosition;
            Quaternion targetRotation = kineticInitialRotation;
            Vector3 targetScale = kineticInitialScale; // Base scale, special anims modify this temporarily

            // --- Apply Idle Animation ---
            if (!isMoving && !isAttacking)
            {
                targetPosition = ApplyIdleHover(targetPosition);
                targetRotation = ApplyIdleWobble(targetRotation);
            }
            else
            {
                // Reset hover timer when not idle
                hoverTimer = 0f;
            }

            // --- Apply Movement Animation ---
            if (isMoving)
            {
                Vector3 currentVelocity = GetUnitVelocity();
                
                if (currentVelocity.sqrMagnitude > 0.01f)
                {
                    targetRotation = ApplyMovementBanking(targetRotation, currentVelocity);
                    targetRotation = ApplyMovementPitch(targetRotation, currentVelocity);
                    targetRotation = ApplyForwardLean(targetRotation);
                    
                     // Store direction for next frame's comparison
                    lastMoveDirection = currentVelocity.normalized;
                }
                else
                {
                    // If moving flag is true but velocity is near zero, smoothly return to neutral
                    SmoothReturnToNeutralRotation(ref targetRotation);
                    lastMoveDirection = Vector3.zero;
                }
                 lastSpeedMagnitude = currentVelocity.magnitude;
            }
            else
            {
                 // Smoothly return to neutral when stopped
                 SmoothReturnToNeutralRotation(ref targetRotation);
                 lastMoveDirection = Vector3.zero;
                 lastSpeedMagnitude = 0f;
            }

            // --- Apply Attack Animation ---
            if (isAttacking && myShooter != null)
            {
                targetRotation = ApplyAttackRecoil(targetRotation);
                targetPosition = ApplyAttackShake(targetPosition);
                attackAnimTimer += Time.deltaTime; // Update timer regardless of sync
            }
            else
            {
                // Smoothly recover recoil and shake when not attacking
                currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0, currentRecoilRecoverySpeed * Time.deltaTime);
                currentShakeOffset = Vector3.Lerp(currentShakeOffset, Vector3.zero, 10f * Time.deltaTime); // Faster shake recovery
                attackAnimTimer = 0f;

                // Apply residual recovery effects
                targetRotation *= Quaternion.Euler(-currentRecoilAngle, 0, 0);
                targetPosition += currentShakeOffset;
            }
            
             // Apply the final calculated transforms
            kineticTransform.localPosition = targetPosition;
            kineticTransform.localRotation = targetRotation;
            // Scale is handled by special animations primarily
            // kineticTransform.localScale = targetScale; 
        }
        
         private Vector3 GetUnitVelocity()
        {
            // Prefer PlayerMovement if available
            if (playerMovement != null)
            {
                try
                {
                    // Handle potential missing GetLastMoveDirection method gracefully
                    if (playerMovement.GetType().GetMethod("GetLastMoveDirection") != null)
                    {
                        return playerMovement.GetLastMoveDirection() * playerMovement.moveSpeed;
                    }
                    else
                    {
                        // Fallback if method doesn't exist
                        return playerMovement.transform.forward * playerMovement.moveSpeed;
                    }
                }
                catch (System.Exception)
                {
                    // If any exception occurs, fall back to transform.forward
                    return playerMovement.transform.forward * playerMovement.moveSpeed;
                }
            }
            
            // Use Unit's basic movement info otherwise
            if (myUnit != null && myUnit.HasMovement)
            {
                // Estimate velocity based on current speed and direction
                // Ensure we have a valid direction
                Vector3 direction = myUnit.transform.forward; // Default if no rig info
                
                // Try to use MovementRig if available, wrapped in try/catch in case it's protected
                try
                {
                    if (myUnit.MovementRig != null && myUnit.MovementRig.IsSeeking)
                    {
                        direction = (myUnit.MovementRig.Destination - myUnit.transform.position).normalized;
                    }
                }
                catch (System.Exception)
                {
                    // Silently fail and use transform.forward as fallback
                }
                
                return direction * myUnit.GetCurrentSpeed();
            }
            return Vector3.zero;
        }

        private void SmoothReturnToNeutralRotation(ref Quaternion targetRotation)
        {
             // Smoothly return banking, pitching, and leaning to zero
             currentBankAngle = Mathf.Lerp(currentBankAngle, 0, currentBankSpeed * Time.deltaTime);
             currentPitchAngle = Mathf.Lerp(currentPitchAngle, 0, currentPitchSpeed * Time.deltaTime);
             currentLeanAngle = Mathf.Lerp(currentLeanAngle, 0, currentPitchSpeed * Time.deltaTime); // Use pitch speed for lean recovery

             // Apply the neutral rotation components
             targetRotation *= Quaternion.Euler(currentPitchAngle + currentLeanAngle, 0, currentBankAngle);
        }


        #region Kinetic Calculation Methods

        private Vector3 ApplyIdleHover(Vector3 position)
        {
            hoverTimer += Time.deltaTime * currentHoverSpeed;
            if (hoverTimer > Mathf.PI * 2) hoverTimer -= Mathf.PI * 2;
            float yOffset = Mathf.Sin(hoverTimer) * currentHoverAmount;
            position.y += yOffset;
            return position;
        }

        private Quaternion ApplyIdleWobble(Quaternion rotation)
        {
            if (currentIdleWobbleAmount <= 0) return rotation;
            float wobbleX = 0, wobbleZ = 0;
             switch (idleStyle)
            {
                 case IdleStyle.GentleHover: // Subtle sin/cos wobble
                     wobbleX = Mathf.Sin(Time.time * 0.6f) * currentIdleWobbleAmount;
                     wobbleZ = Mathf.Cos(Time.time * 0.4f) * currentIdleWobbleAmount;
                     break;
                 case IdleStyle.HeavyFloat: // Slower, more pronounced wobble
                     wobbleX = Mathf.Sin(Time.time * 0.3f) * currentIdleWobbleAmount;
                     wobbleZ = Mathf.Cos(Time.time * 0.2f) * currentIdleWobbleAmount;
                     break;
                case IdleStyle.AgitatedBuzz: // Faster, Perlin noise based jitter
                     float timeFactor = Time.time * 2.5f;
                     wobbleX = (Mathf.PerlinNoise(timeFactor, 0f) - 0.5f) * 2f * currentIdleWobbleAmount;
                     wobbleZ = (Mathf.PerlinNoise(0f, timeFactor) - 0.5f) * 2f * currentIdleWobbleAmount;
                     break;
            }
            return rotation * Quaternion.Euler(wobbleX, 0, wobbleZ);
        }

        private Quaternion ApplyMovementBanking(Quaternion rotation, Vector3 velocity)
        {
            float targetBankAngle = 0f;
            if (lastMoveDirection.sqrMagnitude > 0.001f && velocity.sqrMagnitude > 0.001f)
            {
                float turnAmount = Vector3.SignedAngle(lastMoveDirection, velocity.normalized, Vector3.up);
                 // Scale intensity based on style - Aggressive turns more
                 float intensityMultiplier = (moveStyle == MoveStyle.AggressiveStrafe) ? 1.5f : (moveStyle == MoveStyle.HeavyDrift ? 0.6f : 1.0f);
                targetBankAngle = -turnAmount * (currentMaxBankAngle / 90f) * intensityMultiplier;
                targetBankAngle = Mathf.Clamp(targetBankAngle, -currentMaxBankAngle, currentMaxBankAngle);
            }
            currentBankAngle = Mathf.Lerp(currentBankAngle, targetBankAngle, currentBankSpeed * Time.deltaTime);
            return rotation * Quaternion.Euler(0, 0, currentBankAngle);
        }

        private Quaternion ApplyMovementPitch(Quaternion rotation, Vector3 velocity)
        {
            float currentSpeed = velocity.magnitude;
            float accelerationValue = (currentSpeed - lastSpeedMagnitude) / Time.deltaTime;
            // Make accel detection less sensitive for heavy style
            float sensitivity = (moveStyle == MoveStyle.HeavyDrift) ? 0.5f : 1.0f;
            float targetPitchAngle = accelerationValue * (currentMaxPitchAngle / 10f) * sensitivity; // Pitch down on accel
            targetPitchAngle = Mathf.Clamp(targetPitchAngle, -currentMaxPitchAngle, currentMaxPitchAngle);

            currentPitchAngle = Mathf.Lerp(currentPitchAngle, targetPitchAngle, currentPitchSpeed * Time.deltaTime);
            return rotation * Quaternion.Euler(currentPitchAngle, 0, 0);
        }

        private Quaternion ApplyForwardLean(Quaternion rotation)
        {
            float targetLeanAngle = currentForwardLeanAngle;
             // Heavy style leans less
             if (moveStyle == MoveStyle.HeavyDrift) targetLeanAngle *= 0.5f;

            currentLeanAngle = Mathf.Lerp(currentLeanAngle, targetLeanAngle, currentPitchSpeed * Time.deltaTime); // Use pitch speed for lean
            return rotation * Quaternion.Euler(currentLeanAngle, 0, 0);
        }

        private Quaternion ApplyAttackRecoil(Quaternion rotation)
        {
            float targetRecoil = 0f;
             // Check if we should apply recoil based on timing
             bool applyRecoil = false;
             if (currentSyncAttackTiming && myShooter != null)
             {
                 float cooldownProgress = myShooter.GetAttackCooldownProgress();
                 // Apply recoil just as the cooldown resets (at the moment of firing)
                 applyRecoil = (cooldownProgress < 0.1f && attackAnimTimer < 0.1f); // Only apply once per shot
             }
             else
             {
                 // Fallback: apply recoil at the start of the attack state change (less accurate)
                 applyRecoil = isAttacking && !wasAttacking;
             }

            if (applyRecoil)
            {
                 targetRecoil = currentAttackRecoilAngle;
                 // Heavy style recoils more
                 if (attackStyle == AttackStyle.HeavyCannon) targetRecoil *= 1.5f;
                 currentRecoilAngle = targetRecoil; // Apply instantaneous recoil
            }
            else
            {
                // Recover from recoil smoothly otherwise
                 currentRecoilAngle = Mathf.Lerp(currentRecoilAngle, 0, currentRecoilRecoverySpeed * Time.deltaTime);
            }
            
            // Apply recoil rotation (pitching upwards)
            return rotation * Quaternion.Euler(-currentRecoilAngle, 0, 0);
        }

        private Vector3 ApplyAttackShake(Vector3 position)
        {
            if (currentAttackShakeAmount <= 0) return position;

             float shakeIntensity = 0f;
             // Determine shake based on cooldown progress
             if (currentSyncAttackTiming && myShooter != null)
             {
                 float cooldownProgress = myShooter.GetAttackCooldownProgress();
                 if (cooldownProgress < 0.1f) shakeIntensity = 1.0f; // Max shake right after fire
                 else if (cooldownProgress < 0.3f) shakeIntensity = 0.4f; // Reduced shake during recovery
             }
             else if (isAttacking && !wasAttacking) // Fallback timing
             {
                 shakeIntensity = 1.0f;
             }

            if (shakeIntensity > 0)
            {
                 float effectiveShakeAmount = currentAttackShakeAmount;
                 // Heavy style shakes more
                 if (attackStyle == AttackStyle.HeavyCannon) effectiveShakeAmount *= 2.0f;

                currentShakeOffset = new Vector3(
                    (Random.value - 0.5f) * 2f * effectiveShakeAmount * shakeIntensity,
                    (Random.value - 0.5f) * 2f * effectiveShakeAmount * shakeIntensity,
                    (Random.value - 0.5f) * 2f * effectiveShakeAmount * shakeIntensity
                );
            }
            else
            {
                // Smoothly return to zero when not shaking
                currentShakeOffset = Vector3.Lerp(currentShakeOffset, Vector3.zero, 15f * Time.deltaTime);
            }

            return position + currentShakeOffset;
        }

        #endregion

        #region Special Animation Triggers & Coroutines

        public void PlayEntryAnimation()
        {
            if (!isInitialized || hasPlayedEntryAnimation || isEntryPlaying || isDead) return;
            
            StopAllSpecialAnimations(true); // Stop everything except death
            
            // Check if the gameObject is active before starting a coroutine
            if (gameObject.activeInHierarchy)
            {
                entryAnimationCoroutine = StartCoroutine(PlayEntryAnimationCoroutine());
                hasPlayedEntryAnimation = true;
            }
            else if (debugAnimations)
            {
                Debug.LogWarning($"Cannot play entry animation on inactive object {gameObject.name}.");
            }
        }

        private IEnumerator PlayEntryAnimationCoroutine()
        {
            if (debugAnimations) Debug.Log($"Starting kinetic Entry animation ({entryStyle}) on {gameObject.name}");
            isEntryPlaying = true;
            AE_EntryStart();

            Vector3 originalPosition = kineticInitialPosition;
            Quaternion originalRotation = kineticInitialRotation;
            Vector3 originalScale = kineticInitialScale;
            
            float timer = 0f;
            kineticTransform.localScale = (entryStyle == EntryStyle.PortalEmerge || entryStyle == EntryStyle.Materialize) ? Vector3.zero : originalScale; // Start scaled down for emerge/materialize

            Vector3 startPositionOffset = Vector3.zero;
            if (entryStyle == EntryStyle.FastDrop) startPositionOffset = Vector3.up * 3f; // Example drop height

            while (timer < currentEntryDuration)
            {
                float progress = Mathf.Clamp01(timer / currentEntryDuration);
                float easedProgress = EaseOutCubic(progress); // Use an easing function

                // Position
                Vector3 currentPosition = Vector3.Lerp(originalPosition + startPositionOffset, originalPosition, easedProgress);
                
                // Bounce for FastDrop
                 if (entryStyle == EntryStyle.FastDrop && progress > 0.7f)
                 {
                     float bounceProgress = (progress - 0.7f) / 0.3f;
                     float bounce = Mathf.Sin(bounceProgress * Mathf.PI) * 0.3f * (1f - bounceProgress); // Small bounce
                     currentPosition.y -= bounce;
                 }
                kineticTransform.localPosition = currentPosition;

                // Scale
                Vector3 currentScale = originalScale;
                 if (entryStyle == EntryStyle.FastDrop)
                 {
                     // Scale punch near the end
                     float punchProgress = Mathf.Clamp01((progress - 0.6f) / 0.4f);
                     float punchAmount = Mathf.Sin(punchProgress * Mathf.PI) * 0.2f; // Scale punch magnitude
                     currentScale = originalScale * (1f + punchAmount);
                 }
                 else if (entryStyle == EntryStyle.PortalEmerge || entryStyle == EntryStyle.Materialize)
                 {
                     currentScale = Vector3.Lerp(Vector3.zero, originalScale, easedProgress);
                 }
                kineticTransform.localScale = currentScale;

                // Rotation (optional settle rotation)
                Quaternion currentRotation = Quaternion.Slerp(originalRotation * Quaternion.Euler(Random.Range(-5,5), Random.Range(-10,10), Random.Range(-5,5)), originalRotation, easedProgress);
                kineticTransform.localRotation = currentRotation;

                 // TODO: Add Materialize fade-in logic (requires material access)
                 // if (entryStyle == EntryStyle.Materialize) { ... }

                timer += Time.deltaTime;
                yield return null;
            }

            // Ensure final state
            kineticTransform.localPosition = originalPosition;
            kineticTransform.localRotation = originalRotation;
            kineticTransform.localScale = originalScale;

            AE_EntryEnd();
            isEntryPlaying = false;
            entryAnimationCoroutine = null;
        }


        public void PlayWarpAnimation()
        {
            if (!isInitialized || isWarpPlaying || isDead) return;

            StopAllSpecialAnimations(true);
            
            // Check if the gameObject is active before starting a coroutine
            if (gameObject.activeInHierarchy)
            {
                warpAnimationCoroutine = StartCoroutine(PlayWarpAnimationCoroutine());
            }
            else if (debugAnimations)
            {
                Debug.LogWarning($"Cannot play warp animation on inactive object {gameObject.name}.");
            }
        }

        private IEnumerator PlayWarpAnimationCoroutine()
        {
             if (debugAnimations) Debug.Log($"Starting kinetic Warp animation ({warpStyle}) on {gameObject.name}");
            isWarpPlaying = true;
            AE_WarpStart();

            Vector3 originalScale = kineticInitialScale;
            Quaternion originalRotation = kineticInitialRotation;
            float timer = 0f;

            while (timer < currentWarpDuration)
            {
                float progress = Mathf.Clamp01(timer / currentWarpDuration);
                float easedProgress = EaseInOutQuad(progress); // Ping-pong easing

                // Scale Effects
                Vector3 currentScale = originalScale;
                 switch (warpStyle)
                 {
                     case WarpStyle.QuickBlink:
                         // Shrink then grow back quickly
                         float blinkScale = 1f - Mathf.Sin(progress * Mathf.PI) * 0.8f; // Shrink to 20%
                         currentScale = originalScale * blinkScale;
                         break;
                     case WarpStyle.PhaseShift:
                         // Subtle scale wobble
                          float phaseScale = 1f + Mathf.Sin(progress * Mathf.PI * 4f) * 0.1f; // Fast wobble
                          currentScale = originalScale * phaseScale;
                         break;
                     case WarpStyle.StreakingWarp:
                         // Stretch along forward axis (relative to parent unit's forward)
                          Vector3 direction = myUnit?.transform.forward ?? Vector3.forward;
                          float stretchAmount = Mathf.Sin(progress * Mathf.PI) * 0.5f; // Stretch by 50%
                          currentScale = originalScale;
                          currentScale.z *= (1f + stretchAmount); // Assuming Z is forward for the model
                          currentScale.x *= (1f - stretchAmount * 0.5f); // Squash sideways slightly
                          currentScale.y *= (1f - stretchAmount * 0.5f);
                          // Need to apply rotation based on direction if not aligned
                          break;
                 }
                kineticTransform.localScale = currentScale;

                // Rotation Effects
                Quaternion currentRotation = originalRotation;
                 switch (warpStyle)
                 {
                     case WarpStyle.QuickBlink:
                          float spinAmount = easedProgress * 360f * 2f; // Fast spin
                          currentRotation = originalRotation * Quaternion.Euler(Random.value * 20f, spinAmount, Random.value * 20f);
                          break;
                      // Other styles might not need rotation
                 }
                kineticTransform.localRotation = currentRotation;
                
                 // TODO: Add PhaseShift blur/distortion (shader effect)
                 // TODO: Add StreakingWarp trail (particle/trail renderer effect)

                timer += Time.deltaTime;
                yield return null;
            }

            kineticTransform.localScale = originalScale;
            kineticTransform.localRotation = originalRotation;
            AE_WarpEnd();
            isWarpPlaying = false;
            warpAnimationCoroutine = null;
        }


        public void PlayPowerUpAnimation()
        {
            if (!isInitialized || isPowerUpPlaying || isDead) return;

            StopAllSpecialAnimations(true);
            
            // Check if the gameObject is active before starting a coroutine
            if (gameObject.activeInHierarchy)
            {
                powerUpAnimationCoroutine = StartCoroutine(PlayPowerUpAnimationCoroutine());
            }
            else if (debugAnimations)
            {
                Debug.LogWarning($"Cannot play power-up animation on inactive object {gameObject.name}.");
            }
        }

        private IEnumerator PlayPowerUpAnimationCoroutine()
        {
            if (debugAnimations) Debug.Log($"Starting kinetic PowerUp animation ({powerUpStyle}) on {gameObject.name}");
            isPowerUpPlaying = true;
            AE_PowerUpStart();

            Vector3 originalScale = kineticInitialScale;
            float timer = 0f;

            while (timer < currentPowerUpDuration)
            {
                float progress = Mathf.Clamp01(timer / currentPowerUpDuration);
                
                float pulseScale = 1f;
                float pulseFrequency = 1f;
                float pulseMagnitude = 1f;

                 switch (powerUpStyle)
                 {
                     case PowerUpStyle.SteadyGlow:
                         pulseFrequency = 2f; // Two pulses over duration
                         pulseMagnitude = 0.15f * (1f - progress); // Fade out magnitude
                         pulseScale = 1f + Mathf.Sin(progress * pulseFrequency * Mathf.PI * 2f) * pulseMagnitude;
                         break;
                     case PowerUpStyle.EnergySurge:
                         pulseFrequency = 5f; // Faster pulses
                         pulseMagnitude = 0.25f * Mathf.Pow(1f - progress, 0.5f); // Sharper fade out
                         // Use absolute value for sharper peaks
                         pulseScale = 1f + Mathf.Abs(Mathf.Sin(progress * pulseFrequency * Mathf.PI * 2f)) * pulseMagnitude;
                         break;
                     case PowerUpStyle.ShieldOvercharge:
                         pulseFrequency = 1.5f; // Slower, larger pulses
                         pulseMagnitude = 0.3f * (1f - progress);
                         pulseScale = 1f + Mathf.Sin(progress * pulseFrequency * Mathf.PI * 2f) * pulseMagnitude;
                         // TODO: Could add color tinting or link to shield visual
                         break;
                 }

                kineticTransform.localScale = originalScale * pulseScale;

                timer += Time.deltaTime;
                yield return null;
            }

            kineticTransform.localScale = originalScale;
            AE_PowerUpEnd();
            isPowerUpPlaying = false;
            powerUpAnimationCoroutine = null;
        }


        public void PlayDeathAnimation()
        {
            if (!isInitialized || deathAnimationCoroutine != null) return; // Already dying or dead

            isDead = true; // Set flag immediately
            StopAllSpecialAnimations(false); // Stop everything, including potential warp/entry
            
            // Check if the gameObject is active before starting a coroutine
            if (gameObject.activeInHierarchy)
            {
                deathAnimationCoroutine = StartCoroutine(PlayDeathAnimationCoroutine());
            }
            else
            {
                // GameObject is inactive, so we can't start a coroutine
                // Just trigger the final events directly
                if (debugAnimations)
                {
                    Debug.LogWarning($"Cannot play death animation on inactive object {gameObject.name}. Triggering end events directly.");
                }
                
                // Directly call the end events
                AE_BlowUpUnit();
                AE_EndDeath();
            }
        }

        private IEnumerator PlayDeathAnimationCoroutine()
        {
             if (debugAnimations) Debug.Log($"Starting kinetic Death animation ({deathStyle}) on {gameObject.name}");

            Vector3 originalPosition = kineticTransform.localPosition;
            Quaternion originalRotation = kineticInitialRotation;
            Vector3 originalScale = kineticInitialScale;

            // Get impact direction if possible
            Vector3 impactDir = Vector3.down;
            if (myUnit != null)
            {
                // Instead of accessing protected LastImpact field, use a randomized directional vector
                // that looks natural for the death animation
                Transform unitTransform = myUnit.transform;
                
                // Use a random perpendicular direction relative to unit's forward direction
                Vector3 unitForward = unitTransform.forward;
                Vector3 perpendicular = Vector3.Cross(unitForward, Vector3.up).normalized;
                
                // Mix some randomness into the direction
                impactDir = (perpendicular * 0.7f + Vector3.down * 0.3f + UnityEngine.Random.insideUnitSphere * 0.3f).normalized;
                impactDir.y = Mathf.Clamp(impactDir.y, -0.5f, 0.5f); // Prevent pure up/down tumble
            }

            float timer = 0f;
            float spinSpeed = 0f, tumbleSpeed = 0f, fallSpeed = 0f, scaleFactor = 1f;

            while (timer < currentDeathDuration)
            {
                float progress = Mathf.Clamp01(timer / currentDeathDuration);
                float easedProgress = EaseInCubic(progress); // Accelerate into death anim

                // Determine speeds and effects based on style
                 switch (deathStyle)
                 {
                     case DeathStyle.QuickExplosion:
                         spinSpeed = 720f * easedProgress;
                         tumbleSpeed = 360f * easedProgress;
                         fallSpeed = 5f * progress;
                         scaleFactor = 1f - easedProgress; // Shrink quickly
                         break;
                     case DeathStyle.DamagedFall:
                         spinSpeed = 180f * progress;
                         tumbleSpeed = 90f * progress;
                         fallSpeed = 8f * easedProgress; // Accelerate downwards
                         scaleFactor = 1f; // No scaling
                         break;
                     case DeathStyle.EngineFailure:
                         // Erratic spin and tumble
                         spinSpeed = 400f * progress + Mathf.Sin(timer * 15f) * 200f;
                         tumbleSpeed = 200f * progress + Mathf.Cos(timer * 20f) * 150f;
                         fallSpeed = 6f * easedProgress;
                         // Flickering scale/position handled inside loop
                         break;
                 }

                // Position: Apply falling
                Vector3 currentPosition = originalPosition + Vector3.down * timer * fallSpeed;
                
                 // Engine failure specific flickering position
                 if (deathStyle == DeathStyle.EngineFailure)
                 {
                     currentPosition += Random.insideUnitSphere * 0.1f * progress;
                 }
                kineticTransform.localPosition = currentPosition;

                // Rotation: Apply spin and tumble
                float currentSpin = timer * spinSpeed;
                float currentTumble = timer * tumbleSpeed;
                Quaternion spinRotation = Quaternion.Euler(0, currentSpin, 0);
                Quaternion tumbleRotation = Quaternion.AngleAxis(currentTumble, impactDir); // Tumble around impact axis
                kineticTransform.localRotation = originalRotation * tumbleRotation * spinRotation;

                // Scale: Apply shrinking or flickering
                Vector3 currentScale = originalScale * scaleFactor;
                 if (deathStyle == DeathStyle.EngineFailure)
                 {
                     currentScale *= (1f + Mathf.Sin(timer * 30f) * 0.1f * progress); // Flickering scale
                 }
                kineticTransform.localScale = currentScale;

                timer += Time.deltaTime;
                yield return null;
            }

             // Animation finished - trigger final events/cleanup
             AE_BlowUpUnit(); // Trigger explosion effect now
             AE_EndDeath(); // Signal animation completion (Unit handles actual destruction/pooling)
            
            // Optionally hide the object after animation if not handled by AE_EndDeath/Unit
            // kineticTransform.gameObject.SetActive(false); 

            deathAnimationCoroutine = null;
        }

        // Stop all special animations (except optionally death)
        private void StopAllSpecialAnimations(bool keepDeathRunning)
        {
            if (entryAnimationCoroutine != null) { StopCoroutine(entryAnimationCoroutine); entryAnimationCoroutine = null; isEntryPlaying = false; }
            if (warpAnimationCoroutine != null) { StopCoroutine(warpAnimationCoroutine); warpAnimationCoroutine = null; isWarpPlaying = false; }
            if (powerUpAnimationCoroutine != null) { StopCoroutine(powerUpAnimationCoroutine); powerUpAnimationCoroutine = null; isPowerUpPlaying = false; }
            
            if (!keepDeathRunning && deathAnimationCoroutine != null) { StopCoroutine(deathAnimationCoroutine); deathAnimationCoroutine = null; /* isDead remains true */ }

             // Reset transform if stopped mid-animation (except death)
            if (!isDead && kineticTransform != null)
            {
                 kineticTransform.localPosition = kineticInitialPosition;
                 kineticTransform.localRotation = kineticInitialRotation;
                 kineticTransform.localScale = kineticInitialScale;
            }
        }

        #endregion
        
        #region Easing Functions

        private float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3);
        private float EaseInCubic(float t) => t * t * t;
        private float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2) / 2f;

        #endregion

        #region Animation Events (Called by Coroutines or External Triggers)

        public void AE_EntryStart() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_EntryStart"); /* Add visual effects */ }
        public void AE_EntryEnd() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_EntryEnd"); }
        public void AE_PowerUpStart() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_PowerUpStart"); /* Add visual effects */ }
        public void AE_PowerUpEnd() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_PowerUpEnd"); }
        public void AE_WarpStart() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_WarpStart"); /* Add visual effects */ }
        public void AE_WarpEnd() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_WarpEnd"); }
        
        // Note: AE_AttackStart and AE_FireWeapon are less relevant now as attack anim is continuous recoil/shake
        // They could be triggered based on the shooter state if needed.
        public void AE_AttackStart() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_AttackStart (Kinetic)"); }
        public void AE_FireWeapon() { if (debugAnimations) Debug.Log($"{gameObject.name}: AE_FireWeapon (Kinetic)"); }

        public void AE_EndDeath() 
        { 
            if (debugAnimations) Debug.Log($"{gameObject.name}: AE_EndDeath (Kinetic)"); 
            // Unit class handles respawn/destruction logic based on its OnUnitDeath event subscribers
        }
        
        public void AE_BlowUpUnit() 
        { 
            if (debugAnimations) Debug.Log($"{gameObject.name}: AE_BlowUpUnit (Kinetic)"); 
            myUnit?.BlowUpEffect(); // Trigger explosion effect via Unit class
        }

        #endregion

        #region Public API & Reset

        // Called by Unit.cs to reset state, e.g., on respawn
        public void ResetAnimationState()
        {
            if (!isInitialized || kineticTransform == null) return;

            if (debugAnimations) Debug.Log($"{gameObject.name}: Resetting Kinetic Animation State");

            // Stop all running animations
            StopAllCoroutines();
            entryAnimationCoroutine = null;
            warpAnimationCoroutine = null;
            powerUpAnimationCoroutine = null;
            deathAnimationCoroutine = null;

            // Reset state flags
            isMoving = false; wasMoving = false;
            isAttacking = false; wasAttacking = false;
            isDead = false;
            isEntryPlaying = false;
            isWarpPlaying = false;
            isPowerUpPlaying = false;
            hasPlayedEntryAnimation = false; // Allow entry anim to play again

            // Reset kinetic calculation variables
            currentBankAngle = 0f;
            currentPitchAngle = 0f;
            currentLeanAngle = 0f;
            currentRecoilAngle = 0f;
            currentShakeOffset = Vector3.zero;
            lastMoveDirection = Vector3.zero;
            lastSpeedMagnitude = 0f;
            hoverTimer = 0f;
            attackAnimTimer = 0f;

            // Reset transform immediately
            kineticTransform.localPosition = kineticInitialPosition;
            kineticTransform.localRotation = kineticInitialRotation;
            kineticTransform.localScale = kineticInitialScale;

            // Re-apply styles in case they changed while unit was inactive/dead
            ApplyStyles();
            
            // Play entry animation again if configured
            if (autoPlayEntryAnimation && !hasPlayedEntryAnimation)
            {
                 PlayEntryAnimation();
            }
        }
        
        // Called by Unit.cs when specific animations are requested
        public void TriggerWarpAnimation() => PlayWarpAnimation();
        public void TriggerPowerUpAnimation() => PlayPowerUpAnimation();
        // Add attack trigger for Shooter to use
        public void TriggerAttackAnimation() => AE_AttackStart();
        // Entry and Death are handled by Start/Reset and Unit.Die respectively

         // Allow external setting of styles if needed
        public void SetIdleStyle(IdleStyle style) { idleStyle = style; ApplyStyles(); }
        public void SetMoveStyle(MoveStyle style) { moveStyle = style; ApplyStyles(); }
        // ... Add setters for other styles if dynamic changing is required

        #endregion
        
         // Update styles in editor when changed
        void OnValidate()
        {
             // Apply style changes immediately in the editor
             if (Application.isPlaying && isInitialized)
             {
                 ApplyStyles();
             }
        }
    }
}