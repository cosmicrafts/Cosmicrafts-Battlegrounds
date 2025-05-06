using UnityEngine;
using System.Collections;

namespace Cosmicrafts 
{
    /*
     * Simple Kinetic Animation Controller for Units
     * 
     * Handles basic visual animations using procedural techniques.
     * Just set the modelTransform and adjust the intensity sliders.
     */
    public class UnitAnimLis : MonoBehaviour
    {
        #region Compatibility Enums
        
        // Keep these for backward compatibility
        public enum IdleStyle
        {
            GentleHover, 
            HeavyFloat,  
            AgitatedBuzz 
        }

        public enum MoveStyle
        {
            SmoothFlight,   
            AggressiveStrafe,
            HeavyDrift      
        }

        public enum AttackStyle
        {
            QuickRecoil, 
            HeavyCannon, 
            EnergyPulse  
        }

        public enum EntryStyle
        {
            FastDrop,     
            PortalEmerge, 
            Materialize   
        }

        public enum WarpStyle
        {
            QuickBlink, 
            PhaseShift, 
            StreakingWarp
        }

        public enum PowerUpStyle
        {
            SteadyGlow,    
            EnergySurge,   
            ShieldOvercharge 
        }

        public enum DeathStyle
        {
            QuickExplosion, 
            DamagedFall,    
            EngineFailure   
        }
        
        #endregion
        
        [Header("Setup")]
        [Tooltip("The visual model to animate")]
        public Transform modelTransform;
        
        // Backwards compatibility property
        public Transform kineticTransform 
        { 
            get { return modelTransform; } 
            set { modelTransform = value; } 
        }
        
        [Tooltip("Automatically play entry animation when unit spawns")]
        public bool autoPlayEntryAnimation = true;
        
        [Header("Animation Intensity")]
        [Range(0f, 10f)]
        public float idleIntensity = 3f;
        [Range(0f, 10f)]
        public float movementIntensity = 3f;
        [Range(0f, 10f)]
        public float attackIntensity = 4f;
        [Range(0f, 10f)]
        public float specialIntensity = 3f;
        
        [Header("Animation Types")]
        public bool useHover = true;
        public bool useRotation = true;
        public bool useAttackRecoil = true;
        public bool useEntryAnimation = true;
        
        // Backwards compatibility style fields
        [Header("Compatibility Styles (use intensity sliders instead)")]
        public IdleStyle idleStyle = IdleStyle.HeavyFloat;
        public MoveStyle moveStyle = MoveStyle.AggressiveStrafe;
        public AttackStyle attackStyle = AttackStyle.HeavyCannon;
        public EntryStyle entryStyle = EntryStyle.FastDrop;
        public WarpStyle warpStyle = WarpStyle.QuickBlink;
        public PowerUpStyle powerUpStyle = PowerUpStyle.EnergySurge;
        public DeathStyle deathStyle = DeathStyle.QuickExplosion;
        
        // Core references
        private Unit myUnit;
        private Shooter myShooter;
        
        // Initial transform values
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale;
        
        // Animation state
        private bool isMoving = false;
        private bool isAttacking = false;
        private bool isDead = false;
        private float hoverTimer = 0f;
        private float attackTimer = 0f;
        
        // Special animations
        private Coroutine currentSpecialAnim = null;
        
        void Start()
        {
            // Auto-find model if not set
            if (modelTransform == null) {
                // Try to find a child with renderer
                Renderer r = GetComponentInChildren<Renderer>();
                if (r != null) {
                    modelTransform = r.transform;
                } else if (transform.childCount > 0) {
                    // Otherwise use first child
                    modelTransform = transform.GetChild(0);
                } else {
                    // Last resort, use self
                    modelTransform = transform;
                }
            }
            
            // Store initial transform values
            initialPosition = modelTransform.localPosition;
            initialRotation = modelTransform.localRotation;
            initialScale = modelTransform.localScale;
            
            // Get components
            myUnit = GetComponentInParent<Unit>();
            myShooter = GetComponentInParent<Shooter>();
            
            // Play entry animation
            if (autoPlayEntryAnimation && useEntryAnimation) {
                PlayEntryAnimation();
            }
        }
        
        void Update()
        {
            // Skip if dead or no model
            if (isDead || modelTransform == null) return;
            
            // Skip if playing special animation
            if (currentSpecialAnim != null) return;
            
            // Update state
            UpdateState();
            
            // Apply animations
            Vector3 targetPos = initialPosition;
            Quaternion targetRot = initialRotation;
            
            // Idle hover
            if (useHover && !isMoving) {
                targetPos = ApplyHover(targetPos);
            }
            
            // Movement tilting
            if (useRotation && isMoving) {
                targetRot = ApplyMovementRotation(targetRot);
            }
            
            // Attack recoil/shake
            if (useAttackRecoil && isAttacking) {
                targetRot = ApplyAttackRecoil(targetRot);
                targetPos = ApplyAttackShake(targetPos);
            }
            
            // Apply final transforms
            modelTransform.localPosition = targetPos;
            modelTransform.localRotation = targetRot;
        }
        
        private void UpdateState()
        {
            // Update movement state
            isMoving = (myUnit != null) && myUnit.IsMoving();
            
            // Update attack state
            bool wasAttacking = isAttacking;
            isAttacking = (myShooter != null) && myShooter.IsEngagingTarget();
            
            // Reset attack timer on new attack
            if (isAttacking && !wasAttacking) {
                attackTimer = 0f;
            }
            
            // Update timers
            hoverTimer += Time.deltaTime;
            if (isAttacking) attackTimer += Time.deltaTime;
        }
        
        private Vector3 ApplyHover(Vector3 position)
        {
            // Simple sine wave hover
            float yOffset = Mathf.Sin(hoverTimer * 2f) * 0.1f * idleIntensity;
            position.y += yOffset;
            
            // Add slight random wobble
            position += new Vector3(
                Mathf.Sin(hoverTimer * 1.5f) * 0.02f * idleIntensity,
                0f,
                Mathf.Cos(hoverTimer * 1.2f) * 0.02f * idleIntensity
            );
            
            return position;
        }
        
        private Quaternion ApplyMovementRotation(Quaternion rotation)
        {
            // Get movement direction
            Vector3 moveDir = GetMovementDirection();
            
            if (moveDir.magnitude > 0.1f) {
                // Calculate banking angle based on horizontal turning
                float bankAngle = Vector3.SignedAngle(
                    Vector3.ProjectOnPlane(transform.forward, Vector3.up),
                    Vector3.ProjectOnPlane(moveDir, Vector3.up),
                    Vector3.up
                );
                
                // Clamp banking and scale by intensity
                bankAngle = Mathf.Clamp(bankAngle * -0.5f, -35f, 35f) * movementIntensity * 0.1f;
                
                // Apply bank (z-axis rotation)
                rotation *= Quaternion.Euler(0f, 0f, bankAngle);
                
                // Apply forward pitch (x-axis rotation)
                rotation *= Quaternion.Euler(-5f * movementIntensity * 0.1f, 0f, 0f);
            }
            
            return rotation;
        }
        
        private Vector3 GetMovementDirection()
        {
            if (myUnit == null) return Vector3.zero;
            
            // Use velocity if available
            Rigidbody rb = myUnit.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude > 0.1f) {
                return rb.linearVelocity.normalized;
            }
            
            // Fallback to transform.forward
            return transform.forward;
        }
        
        private Quaternion ApplyAttackRecoil(Quaternion rotation)
        {
            // Get attack cooldown progress
            float cooldown = 0f;
            if (myShooter != null) {
                cooldown = myShooter.GetAttackCooldownProgress();
            }
            
            // Apply recoil at the start of attack
            float recoilAngle = 0f;
            if (cooldown < 0.1f && attackTimer < 0.1f) {
                // Strong initial recoil
                recoilAngle = 10f * attackIntensity;
            }
            else if (attackTimer < 0.3f) {
                // Gradual recoil recovery
                recoilAngle = Mathf.Lerp(10f * attackIntensity, 0f, attackTimer / 0.3f);
            }
            
            // Apply recoil pitch (x-axis rotation)
            rotation *= Quaternion.Euler(-recoilAngle, 0f, 0f);
            
            return rotation;
        }
        
        private Vector3 ApplyAttackShake(Vector3 position)
        {
            // Apply shake at start of attack
            if (attackTimer < 0.2f) {
                float shakeIntensity = (0.2f - attackTimer) * 5f * attackIntensity * 0.05f;
                position += new Vector3(
                    Random.Range(-shakeIntensity, shakeIntensity),
                    Random.Range(-shakeIntensity, shakeIntensity),
                    Random.Range(-shakeIntensity, shakeIntensity)
                );
            }
            
            return position;
        }
        
        // --- Special Animations ---
        
        public void PlayEntryAnimation()
        {
            if (isDead || !gameObject.activeInHierarchy) return;
            
            if (currentSpecialAnim != null) {
                StopCoroutine(currentSpecialAnim);
            }
            
            currentSpecialAnim = StartCoroutine(EntryAnimCoroutine());
        }
        
        private IEnumerator EntryAnimCoroutine()
        {
            // Starting position and scale
            modelTransform.localPosition = initialPosition + Vector3.up * 2f;
            modelTransform.localScale = initialScale * 0.2f;
            
            float duration = 0.8f;
            float timer = 0f;
            
            while (timer < duration) {
                float progress = timer / duration;
                float easedProgress = 1f - Mathf.Pow(1f - progress, 3f); // Ease out cubic
                
                // Drop from above
                modelTransform.localPosition = Vector3.Lerp(
                    initialPosition + Vector3.up * 2f,
                    initialPosition,
                    easedProgress
                );
                
                // Scale up
                modelTransform.localScale = Vector3.Lerp(
                    initialScale * 0.2f,
                    initialScale,
                    easedProgress
                );
                
                // Add bounce at the end
                if (progress > 0.7f) {
                    float bounceProgress = (progress - 0.7f) / 0.3f;
                    float bounce = Mathf.Sin(bounceProgress * Mathf.PI) * 0.4f * specialIntensity * (1f - bounceProgress);
                    modelTransform.localPosition -= new Vector3(0f, bounce, 0f);
                }
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Ensure final position
            modelTransform.localPosition = initialPosition;
            modelTransform.localScale = initialScale;
            
            currentSpecialAnim = null;
        }
        
        public void PlayWarpAnimation()
        {
            if (isDead || !gameObject.activeInHierarchy) return;
            
            if (currentSpecialAnim != null) {
                StopCoroutine(currentSpecialAnim);
            }
            
            currentSpecialAnim = StartCoroutine(WarpAnimCoroutine());
        }
        
        private IEnumerator WarpAnimCoroutine()
        {
            float duration = 0.3f;
            float timer = 0f;
            
            while (timer < duration) {
                float progress = timer / duration;
                
                // Shrink then grow
                float scale = 1f - Mathf.Sin(progress * Mathf.PI) * 0.7f * specialIntensity * 0.1f;
                modelTransform.localScale = initialScale * scale;
                
                // Add spin
                float spin = progress * 360f * specialIntensity * 0.1f;
                modelTransform.localRotation = initialRotation * Quaternion.Euler(0f, spin, 0f);
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Reset
            modelTransform.localScale = initialScale;
            modelTransform.localRotation = initialRotation;
            
            currentSpecialAnim = null;
        }
        
        public void PlayPowerUpAnimation()
        {
            if (isDead || !gameObject.activeInHierarchy) return;
            
            if (currentSpecialAnim != null) {
                StopCoroutine(currentSpecialAnim);
            }
            
            currentSpecialAnim = StartCoroutine(PowerUpAnimCoroutine());
        }
        
        private IEnumerator PowerUpAnimCoroutine()
        {
            float duration = 1.0f;
            float timer = 0f;
            
            while (timer < duration) {
                float progress = timer / duration;
                
                // Pulse scale
                float pulse = 1f + Mathf.Sin(progress * Mathf.PI * 4f) * 0.2f * specialIntensity * 0.1f;
                modelTransform.localScale = initialScale * pulse;
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Reset
            modelTransform.localScale = initialScale;
            
            currentSpecialAnim = null;
        }
        
        public void PlayDeathAnimation()
        {
            if (isDead || !gameObject.activeInHierarchy) return;
            
            isDead = true;
            
            if (currentSpecialAnim != null) {
                StopCoroutine(currentSpecialAnim);
            }
            
            currentSpecialAnim = StartCoroutine(DeathAnimCoroutine());
        }
        
        private IEnumerator DeathAnimCoroutine()
        {
            float duration = 1.0f;
            float timer = 0f;
            
            while (timer < duration) {
                float progress = timer / duration;
                
                // Spin and tumble
                float spin = progress * 720f;
                float tumble = progress * 360f;
                modelTransform.localRotation = initialRotation * 
                    Quaternion.Euler(tumble, spin, tumble * 0.5f);
                
                // Fall down
                modelTransform.localPosition = initialPosition + Vector3.down * progress * 5f;
                
                // Shrink
                modelTransform.localScale = initialScale * (1f - progress * 0.8f);
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Hide model at end
            modelTransform.gameObject.SetActive(false);
            
            // Trigger unit explosion if available
            if (myUnit != null) {
                myUnit.BlowUpEffect();
            }
            
            currentSpecialAnim = null;
        }
        
        // Reset everything for respawn
        public void ResetAnimationState()
        {
            isDead = false;
            isMoving = false;
            isAttacking = false;
            
            if (currentSpecialAnim != null) {
                StopCoroutine(currentSpecialAnim);
                currentSpecialAnim = null;
            }
            
            if (modelTransform != null) {
                modelTransform.gameObject.SetActive(true);
                modelTransform.localPosition = initialPosition;
                modelTransform.localRotation = initialRotation;
                modelTransform.localScale = initialScale;
            }
            
            // Play entry animation on reset
            if (autoPlayEntryAnimation && useEntryAnimation) {
                PlayEntryAnimation();
            }
        }
        
        // --- Additional backward compatibility methods ---
        
        // Support for old TriggerAttackAnimation method
        public void TriggerAttackAnimation() 
        {
            attackTimer = 0f; // Reset timer to trigger a new attack animation
        }
    }
}