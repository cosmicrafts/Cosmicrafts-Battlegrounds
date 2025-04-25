using UnityEngine;
using System.Collections;

namespace Cosmicrafts
{
    /// <summary>
    /// Test tool for attack animations in UnitAnimLis.
    /// Allows triggering manual attack animations with customizable intensity.
    /// </summary>
    [AddComponentMenu("Cosmicrafts/Debug/Attack Animation Tester")]
    public class AttackAnimationTester : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The UnitAnimLis to test. Leave empty to automatically find on this GameObject.")]
        public UnitAnimLis targetAnimator;
        
        [Header("Attack Animation Settings")]
        [Tooltip("How many attacks to fire in sequence")]
        [Range(1, 10)]
        public int attackCount = 3;
        
        [Tooltip("Time between attacks in seconds")]
        [Range(0.1f, 2.0f)]
        public float attackInterval = 0.5f;
        
        [Tooltip("Duration to simulate attack state")]
        [Range(0.1f, 1.0f)]
        public float attackStateDuration = 0.2f;
        
        [Header("Test Controls")]
        [Tooltip("Press to start an attack test sequence")]
        public bool triggerAttackSequence;
        
        [Tooltip("Press to simulate a single attack")]
        public bool triggerSingleAttack;
        
        [Header("Visualization")]
        [Tooltip("Add visual markers during attack (helpful to see timing)")]
        public bool showVisualMarkers = true;
        
        [Tooltip("Color of the attack markers")]
        public Color markerColor = Color.red;
        
        // Private fields for animation state control
        private bool isTestSequenceRunning = false;
        private System.Reflection.FieldInfo attackAnimTimerField;
        private System.Reflection.MethodInfo applyAttackRecoilMethod;
        private System.Reflection.MethodInfo applyAttackShakeMethod;
        private System.Reflection.FieldInfo isAttackingField;
        private System.Reflection.FieldInfo wasAttackingField;
        
        // UI elements for visualization
        private GameObject markersContainer;
        
        void Start()
        {
            // Find or validate target animator
            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<UnitAnimLis>();
                if (targetAnimator == null)
                {
                    targetAnimator = GetComponentInChildren<UnitAnimLis>();
                    if (targetAnimator == null)
                    {
                        Debug.LogError("[AttackTester] No UnitAnimLis found on this GameObject or its children.", this);
                        this.enabled = false;
                        return;
                    }
                }
            }
            
            // Create markers container if needed
            if (showVisualMarkers)
            {
                markersContainer = new GameObject("AttackMarkers");
                markersContainer.transform.parent = transform;
            }
            
            // Get reflection access to private fields and methods
            System.Type type = typeof(UnitAnimLis);
            attackAnimTimerField = type.GetField("attackAnimTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isAttackingField = type.GetField("isAttacking", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            wasAttackingField = type.GetField("wasAttacking", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // These are likely private methods but worth trying to access
            applyAttackRecoilMethod = type.GetMethod("ApplyAttackRecoil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            applyAttackShakeMethod = type.GetMethod("ApplyAttackShake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        void Update()
        {
            HandleTestInputs();
        }
        
        private void HandleTestInputs()
        {
            // Prevent triggering new tests if one is already running
            if (isTestSequenceRunning)
                return;
                
            if (triggerAttackSequence)
            {
                triggerAttackSequence = false;
                StartCoroutine(RunAttackSequence());
            }
            
            if (triggerSingleAttack)
            {
                triggerSingleAttack = false;
                StartCoroutine(SimulateSingleAttack());
            }
        }
        
        private IEnumerator RunAttackSequence()
        {
            isTestSequenceRunning = true;
            Debug.Log($"[AttackTester] Starting attack sequence with {attackCount} attacks at {attackInterval}s intervals", this);
            
            for (int i = 0; i < attackCount; i++)
            {
                // Simulate an attack
                yield return SimulateSingleAttack();
                
                // Wait for the interval before the next attack
                if (i < attackCount - 1)
                    yield return new WaitForSeconds(attackInterval);
            }
            
            isTestSequenceRunning = false;
        }
        
        private IEnumerator SimulateSingleAttack()
        {
            if (targetAnimator == null)
                yield break;
                
            // Reset the attack timer to simulate a new attack
            if (attackAnimTimerField != null)
                attackAnimTimerField.SetValue(targetAnimator, 0f);
                
            // Set attacking state to true
            if (isAttackingField != null)
                isAttackingField.SetValue(targetAnimator, true);
                
            // Create visual marker if enabled
            if (showVisualMarkers && markersContainer != null)
                CreateAttackMarker();
                
            // Log the attack
            Debug.Log("[AttackTester] Simulating attack", this);
            
            // Call the attack trigger method (public interface)
            targetAnimator.TriggerAttackAnimation();
            
            // Keep attack state active for the specified duration
            yield return new WaitForSeconds(attackStateDuration);
            
            // Set attacking state back to false
            if (isAttackingField != null)
                isAttackingField.SetValue(targetAnimator, false);
                
            // Ensure wasAttacking is properly set for next frame
            if (wasAttackingField != null)
                wasAttackingField.SetValue(targetAnimator, true);
                
            // Let the animation recover naturally
            yield return null;
        }
        
        private void CreateAttackMarker()
        {
            // Create a small sphere to mark where an attack happened
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.parent = markersContainer.transform;
            marker.transform.position = targetAnimator.transform.position + Vector3.up * 0.5f;
            marker.transform.localScale = Vector3.one * 0.2f;
            
            // Set the color
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = markerColor;
            }
            
            // Remove collider
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
                
            // Destroy after a few seconds
            StartCoroutine(DestroyAfterDelay(marker, 3f));
        }
        
        private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
                Destroy(obj);
        }
        
        void OnDestroy()
        {
            // Clean up markers
            if (markersContainer != null)
                Destroy(markersContainer);
        }
        
        // Menu commands for editor convenience
        [ContextMenu("Trigger Attack Sequence")]
        public void TriggerAttackSequenceFromMenu()
        {
            if (!isTestSequenceRunning)
                StartCoroutine(RunAttackSequence());
        }
        
        [ContextMenu("Trigger Single Attack")]
        public void TriggerSingleAttackFromMenu()
        {
            if (!isTestSequenceRunning)
                StartCoroutine(SimulateSingleAttack());
        }
    }
} 