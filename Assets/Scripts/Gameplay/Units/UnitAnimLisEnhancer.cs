using UnityEngine;
using System.Collections;

namespace Cosmicrafts
{
    /// <summary>
    /// Enhancement component for UnitAnimLis that boosts animation values and fixes common issues.
    /// Attach this alongside UnitAnimLis to make animations more visible and dramatic.
    /// </summary>
    [AddComponentMenu("Cosmicrafts/Animation/UnitAnimLis Enhancer")]
    [RequireComponent(typeof(UnitAnimLis))]
    public class UnitAnimLisEnhancer : MonoBehaviour
    {
        [Header("Enhancement Settings")]
        [Tooltip("Multiplier for all animation magnitudes")]
        [Range(1f, 10f)]
        public float animationIntensityMultiplier = 3f;
        
        [Tooltip("Apply enhancements immediately on Start")]
        public bool applyOnStart = true;
        
        [Tooltip("Force initialization of UnitAnimLis if it failed")]
        public bool forceInitialization = true;
        
        [Header("Animation Boosters")]
        [Tooltip("Boost idle hover/wobble animations")]
        public bool boostIdleAnimations = true;
        [Range(1f, 10f)]
        public float idleBoostFactor = 3f;
        
        [Tooltip("Boost movement banking/pitch animations")]
        public bool boostMovementAnimations = true;
        [Range(1f, 10f)]
        public float movementBoostFactor = 2.5f;
        
        [Tooltip("Boost attack recoil/shake animations")]
        public bool boostAttackAnimations = true;
        [Range(1f, 10f)]
        public float attackBoostFactor = 2f;
        
        [Tooltip("Boost special animations (entry, warp, etc.)")]
        public bool boostSpecialAnimations = true;
        [Range(1f, 5f)]
        public float specialBoostFactor = 1.5f;
        
        [Header("Fix Options")]
        [Tooltip("Force kineticTransform to this object if not set")]
        public bool fixMissingKineticTransform = true;
        
        [Tooltip("Override the transform used for animations")]
        public Transform overrideKineticTransform;
        
        [Tooltip("Enable detailed debug logs")]
        public bool enableDebugLogs = true;
        
        [Header("Attack Animation Enhancements")]
        [Tooltip("Boost recoil angle (how far unit pitches on attack)")]
        [Range(5f, 50f)]
        public float attackRecoilAngle = 25f;
        
        [Tooltip("Recovery speed (lower = longer visible recoil)")]
        [Range(1f, 25f)]
        public float attackRecoverySpeed = 5f;
        
        [Tooltip("Shake magnitude (higher = more visible shake)")]
        [Range(0.05f, 1.0f)]
        public float attackShakeMagnitude = 0.25f;
        
        [Tooltip("Extend attack visibility duration")]
        public bool extendAttackVisibility = true;
        
        // Reference to the UnitAnimLis component
        private UnitAnimLis unitAnimLis;
        
        // Fields for reflection-based access
        private System.Reflection.FieldInfo[] styleFields;
        
        void Start()
        {
            // Get the UnitAnimLis component
            unitAnimLis = GetComponent<UnitAnimLis>();
            
            if (unitAnimLis == null)
            {
                Debug.LogError("[UnitAnimLisEnhancer] No UnitAnimLis component found on this object!", this);
                enabled = false;
                return;
            }
            
            if (applyOnStart)
            {
                // Wait a frame to ensure UnitAnimLis initialization
                StartCoroutine(EnhanceAfterDelay(0.1f));
            }
            
            // Cache field info for reflection
            CacheReflectionFields();
        }
        
        private IEnumerator EnhanceAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ApplyEnhancements();
        }
        
        private void CacheReflectionFields()
        {
            System.Type type = typeof(UnitAnimLis);
            
            // Get private fields using reflection
            styleFields = new System.Reflection.FieldInfo[]
            {
                // Idle animation fields
                type.GetField("currentHoverAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentHoverSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentIdleWobbleAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                
                // Movement animation fields
                type.GetField("currentMaxBankAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentBankSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentMaxPitchAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentPitchSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentForwardLeanAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                
                // Attack animation fields
                type.GetField("currentAttackRecoilAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentAttackShakeAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                
                // Special animation fields
                type.GetField("currentEntryDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentWarpDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("currentPowerUpDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                
                // Initialization fields
                type.GetField("isInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("kineticInitialPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("kineticInitialRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("kineticInitialScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
                type.GetField("hasPlayedEntryAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            };
        }
        
        /// <summary>
        /// Apply enhancements to make animations more visible.
        /// </summary>
        public void ApplyEnhancements()
        {
            if (unitAnimLis == null)
            {
                Debug.LogError("[UnitAnimLisEnhancer] UnitAnimLis reference is null!", this);
                return;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log("[UnitAnimLisEnhancer] Applying animation enhancements to " + gameObject.name, this);
            }
            
            // Fix any initialization issues
            FixInitializationIssues();
            
            // Apply style enhancements
            EnhanceAnimationStyles();
            
            // Force applying the styles
            System.Type type = typeof(UnitAnimLis);
            var applyStylesMethod = type.GetMethod("ApplyStyles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (applyStylesMethod != null)
            {
                applyStylesMethod.Invoke(unitAnimLis, null);
                
                if (enableDebugLogs)
                {
                    Debug.Log("[UnitAnimLisEnhancer] Successfully applied enhanced animation styles!", this);
                }
                
                // Play a test animation to demonstrate the enhancement
                StartCoroutine(PlayDemoAnimations());
            }
            else
            {
                Debug.LogWarning("[UnitAnimLisEnhancer] Couldn't find ApplyStyles method via reflection", this);
            }
        }
        
        private void EnhanceAnimationStyles()
        {
            // Set more dramatic animation styles
            if (boostIdleAnimations)
            {
                // Use a more noticeable idle style
                unitAnimLis.idleStyle = UnitAnimLis.IdleStyle.HeavyFloat;
                
                // Boost idle animation values
                BoostField("currentHoverAmount", idleBoostFactor);
                BoostField("currentIdleWobbleAmount", idleBoostFactor * 1.2f);
            }
            
            if (boostMovementAnimations)
            {
                // Use a more dramatic movement style
                unitAnimLis.moveStyle = UnitAnimLis.MoveStyle.AggressiveStrafe;
                
                // Boost movement animation values
                BoostField("currentMaxBankAngle", movementBoostFactor);
                BoostField("currentMaxPitchAngle", movementBoostFactor);
                BoostField("currentForwardLeanAngle", movementBoostFactor * 1.2f, true); // Multiply negative value
            }
            
            if (boostAttackAnimations)
            {
                // Use a more dramatic attack style
                unitAnimLis.attackStyle = UnitAnimLis.AttackStyle.HeavyCannon;
                
                // Instead of using a percentage boost, directly set the values
                // to make attacks much more visible
                SetField("currentAttackRecoilAngle", attackRecoilAngle);
                SetField("currentRecoilRecoverySpeed", attackRecoverySpeed);
                SetField("currentAttackShakeAmount", attackShakeMagnitude);
                
                // If extending attack visibility, also modify how attack animations are timed
                if (extendAttackVisibility)
                {
                    System.Type type = typeof(UnitAnimLis);
                    
                    // Try to find and modify fields that control attack animation timing
                    var attackAnimTimerField = type.GetField("attackAnimTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var syncAttackTimingField = type.GetField("currentSyncAttackTiming", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (syncAttackTimingField != null)
                    {
                        // Force sync attack timing to be true for better visual feedback
                        syncAttackTimingField.SetValue(unitAnimLis, true);
                        
                        if (enableDebugLogs)
                            Debug.Log("[UnitAnimLisEnhancer] Ensured attack timing sync is enabled for better visibility", this);
                    }
                }
            }
            
            if (boostSpecialAnimations)
            {
                // Set dramatic entry and warp styles
                unitAnimLis.entryStyle = UnitAnimLis.EntryStyle.FastDrop;
                unitAnimLis.warpStyle = UnitAnimLis.WarpStyle.QuickBlink;
                unitAnimLis.powerUpStyle = UnitAnimLis.PowerUpStyle.EnergySurge;
                
                // Adjust durations - don't make too fast or too slow
                ScaleField("currentEntryDuration", 1.0f); // Keep duration normal
                ScaleField("currentWarpDuration", 0.8f); // Make warp slightly faster
                ScaleField("currentPowerUpDuration", 1.2f); // Make power-up slightly longer
            }
        }
        
        private void FixInitializationIssues()
        {
            if (!forceInitialization) return;
            
            System.Type type = typeof(UnitAnimLis);
            
            // Fix missing kineticTransform
            if (fixMissingKineticTransform && (unitAnimLis.kineticTransform == null))
            {
                if (overrideKineticTransform != null)
                {
                    unitAnimLis.kineticTransform = overrideKineticTransform;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log("[UnitAnimLisEnhancer] Set kineticTransform to: " + overrideKineticTransform.name, this);
                    }
                }
                else
                {
                    // Try to find a suitable transform in children
                    Transform potentialTarget = FindSuitableKineticTransform();
                    if (potentialTarget != null)
                    {
                        unitAnimLis.kineticTransform = potentialTarget;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log("[UnitAnimLisEnhancer] Auto-assigned kineticTransform to: " + potentialTarget.name, this);
                        }
                    }
                    else
                    {
                        // Last resort: use this transform
                        unitAnimLis.kineticTransform = transform;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log("[UnitAnimLisEnhancer] Using self as kineticTransform as fallback", this);
                        }
                    }
                }
            }
            
            // Force initialization if it failed
            var isInitializedField = type.GetField("isInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isInitializedField != null)
            {
                bool isInitialized = (bool)isInitializedField.GetValue(unitAnimLis);
                
                if (!isInitialized)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log("[UnitAnimLisEnhancer] Forcing UnitAnimLis initialization", this);
                    }
                    
                    // Call Initialize method
                    var initMethod = type.GetMethod("Initialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (initMethod != null)
                    {
                        initMethod.Invoke(unitAnimLis, null);
                    }
                    
                    // In case that failed, manually set initialization values
                    if (unitAnimLis.kineticTransform != null)
                    {
                        var kineticInitialPositionField = type.GetField("kineticInitialPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var kineticInitialRotationField = type.GetField("kineticInitialRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var kineticInitialScaleField = type.GetField("kineticInitialScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (kineticInitialPositionField != null)
                            kineticInitialPositionField.SetValue(unitAnimLis, unitAnimLis.kineticTransform.localPosition);
                        
                        if (kineticInitialRotationField != null)
                            kineticInitialRotationField.SetValue(unitAnimLis, unitAnimLis.kineticTransform.localRotation);
                        
                        if (kineticInitialScaleField != null)
                            kineticInitialScaleField.SetValue(unitAnimLis, unitAnimLis.kineticTransform.localScale);
                        
                        // Now set isInitialized to true
                        isInitializedField.SetValue(unitAnimLis, true);
                    }
                }
                
                // Reset hasPlayedEntryAnimation to allow entry animation to play again
                var hasPlayedEntryAnimationField = type.GetField("hasPlayedEntryAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hasPlayedEntryAnimationField != null)
                {
                    hasPlayedEntryAnimationField.SetValue(unitAnimLis, false);
                }
            }
        }
        
        private Transform FindSuitableKineticTransform()
        {
            // Look for a child with "model" or "mesh" in the name
            foreach (Transform child in transform)
            {
                string lowerName = child.name.ToLower();
                if (lowerName.Contains("model") || lowerName.Contains("mesh") || lowerName.Contains("visual"))
                {
                    return child;
                }
            }
            
            // Look for a renderer component
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.transform;
            }
            
            // If we have any children at all, use the first one
            if (transform.childCount > 0)
            {
                return transform.GetChild(0);
            }
            
            return null;
        }
        
        private void BoostField(string fieldName, float boostFactor, bool isNegative = false)
        {
            foreach (var field in styleFields)
            {
                if (field != null && field.Name == fieldName)
                {
                    float currentValue = (float)field.GetValue(unitAnimLis);
                    
                    // If value is negative and isNegative is true, make it more negative
                    if (currentValue < 0 && isNegative)
                    {
                        field.SetValue(unitAnimLis, currentValue * boostFactor);
                    }
                    else
                    {
                        field.SetValue(unitAnimLis, Mathf.Abs(currentValue) * boostFactor * (isNegative ? -1 : 1));
                    }
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimLisEnhancer] Boosted {fieldName} from {currentValue} to {field.GetValue(unitAnimLis)}", this);
                    }
                    
                    return;
                }
            }
        }
        
        private void ScaleField(string fieldName, float scaleFactor)
        {
            foreach (var field in styleFields)
            {
                if (field != null && field.Name == fieldName)
                {
                    float currentValue = (float)field.GetValue(unitAnimLis);
                    field.SetValue(unitAnimLis, currentValue * scaleFactor);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimLisEnhancer] Scaled {fieldName} from {currentValue} to {field.GetValue(unitAnimLis)}", this);
                    }
                    
                    return;
                }
            }
        }
        
        private void SetField(string fieldName, float newValue)
        {
            foreach (var field in styleFields)
            {
                if (field != null && field.Name == fieldName)
                {
                    float oldValue = (float)field.GetValue(unitAnimLis);
                    field.SetValue(unitAnimLis, newValue);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[UnitAnimLisEnhancer] Set {fieldName} from {oldValue} to {newValue}", this);
                    }
                    
                    return;
                }
            }
        }
        
        private IEnumerator PlayDemoAnimations()
        {
            yield return new WaitForSeconds(0.5f);
            
            // Play entry animation
            unitAnimLis.PlayEntryAnimation();
            
            yield return new WaitForSeconds(1.5f);
            
            // Play warp animation
            unitAnimLis.PlayWarpAnimation();
            
            yield return new WaitForSeconds(1.0f);
            
            // Play power-up animation
            unitAnimLis.PlayPowerUpAnimation();
        }
        
        // Allow triggering animations from the inspector
        [ContextMenu("Apply Enhanced Animations")]
        public void ApplyEnhancementsFromMenu()
        {
            ApplyEnhancements();
        }
        
        [ContextMenu("Play Entry Animation")]
        public void PlayEntryFromMenu()
        {
            if (unitAnimLis != null)
            {
                unitAnimLis.PlayEntryAnimation();
            }
        }
        
        [ContextMenu("Play Warp Animation")]
        public void PlayWarpFromMenu()
        {
            if (unitAnimLis != null)
            {
                unitAnimLis.PlayWarpAnimation();
            }
        }
        
        [ContextMenu("Play PowerUp Animation")]
        public void PlayPowerUpFromMenu()
        {
            if (unitAnimLis != null)
            {
                unitAnimLis.PlayPowerUpAnimation();
            }
        }
    }
} 