using UnityEngine;
using System.Collections;

namespace Cosmicrafts
{
    /// <summary>
    /// Diagnostic tool for troubleshooting UnitAnimLis animation issues.
    /// Attach this to any GameObject with a UnitAnimLis component to diagnose problems.
    /// </summary>
    [AddComponentMenu("Cosmicrafts/Debug/Animation Debugger")]
    public class AnimationDebugger : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The UnitAnimLis to debug. Leave empty to automatically find on this GameObject.")]
        public UnitAnimLis targetAnimator;
        
        [Header("Debug Options")]
        [Tooltip("Enable to log detailed information about the animation state")]
        public bool enableDetailedLogging = true;
        
        [Tooltip("Enable to visually highlight the kineticTransform with debug visuals")]
        public bool showVisualDebug = true;
        
        [Tooltip("Enable to force extreme animation values to make issues more obvious")]
        public bool useExaggeratedValues = true;
        
        [Tooltip("Run a complete animation test sequence on start")]
        public bool runTestSequence = true;
        
        [Header("Test Controls")]
        [Tooltip("Test the entry animation")]
        public bool testEntryAnimation;
        
        [Tooltip("Test the warp animation")]
        public bool testWarpAnimation;
        
        [Tooltip("Test the power-up animation")]
        public bool testPowerUpAnimation;
        
        // Runtime tracking
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale;
        private Transform kineticTransform;
        private bool hasReportedSetup = false;
        
        // Visual debug elements
        private GameObject debugVisualContainer;
        private LineRenderer axisX, axisY, axisZ;
        
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
                        Debug.LogError("[AnimDebugger] No UnitAnimLis found on this GameObject or its children!", this);
                        this.enabled = false;
                        return;
                    }
                }
            }
            
            // Set up debug visualizers if enabled
            if (showVisualDebug)
            {
                SetupVisualDebuggers();
            }
            
            // Log initial setup analysis
            StartCoroutine(AnalyzeSetup());
            
            // Modify animation parameters if using exaggerated values
            if (useExaggeratedValues)
            {
                ApplyExaggeratedValues();
            }
            
            // Run test sequence if requested
            if (runTestSequence)
            {
                StartCoroutine(RunTestSequence());
            }
        }
        
        void Update()
        {
            // Run interactive tests when buttons are pressed in inspector
            if (testEntryAnimation)
            {
                testEntryAnimation = false;
                targetAnimator.PlayEntryAnimation();
                Debug.Log("[AnimDebugger] Triggered Entry Animation", this);
            }
            
            if (testWarpAnimation)
            {
                testWarpAnimation = false;
                targetAnimator.PlayWarpAnimation();
                Debug.Log("[AnimDebugger] Triggered Warp Animation", this);
            }
            
            if (testPowerUpAnimation)
            {
                testPowerUpAnimation = false;
                targetAnimator.PlayPowerUpAnimation();
                Debug.Log("[AnimDebugger] Triggered PowerUp Animation", this);
            }
            
            // Update visual debuggers
            if (showVisualDebug && kineticTransform != null)
            {
                UpdateDebugVisuals();
            }
            
            // Monitor for kinetic transform changes
            if (enableDetailedLogging && kineticTransform != null)
            {
                MonitorTransformChanges();
            }
        }
        
        private void MonitorTransformChanges()
        {
            // Only report significant changes to avoid spam
            if (Vector3.Distance(kineticTransform.localPosition, initialPosition) > 0.01f ||
                Quaternion.Angle(kineticTransform.localRotation, initialRotation) > 1f ||
                Vector3.Distance(kineticTransform.localScale, initialScale) > 0.01f)
            {
                // Track how values are changing
                Vector3 positionDelta = kineticTransform.localPosition - initialPosition;
                Vector3 rotationDelta = kineticTransform.localRotation.eulerAngles - initialRotation.eulerAngles;
                Vector3 scaleDelta = kineticTransform.localScale - initialScale;
                
                // Only log if movement is significant
                if (positionDelta.magnitude > 0.05f || rotationDelta.magnitude > 2f || scaleDelta.magnitude > 0.05f)
                {
                    Debug.Log($"[AnimDebugger] Kinetic movement detected!\n" +
                        $"Pos Δ: {positionDelta.ToString("F3")}\n" +
                        $"Rot Δ: {rotationDelta.ToString("F1")}\n" +
                        $"Scale Δ: {scaleDelta.ToString("F3")}", this);
                    
                    // Update baselines to prevent spam
                    initialPosition = kineticTransform.localPosition;
                    initialRotation = kineticTransform.localRotation;
                    initialScale = kineticTransform.localScale;
                }
            }
        }
        
        private IEnumerator AnalyzeSetup()
        {
            // Wait a frame to ensure proper initialization
            yield return null;
            
            if (hasReportedSetup) yield break;
            hasReportedSetup = true;
            
            Debug.Log($"[AnimDebugger] Starting UnitAnimLis Analysis", this);
            
            // Check 1: Is the kineticTransform assigned?
            kineticTransform = targetAnimator.kineticTransform;
            if (kineticTransform == null)
            {
                Debug.LogError("[AnimDebugger] kineticTransform is not assigned! This is required for animations to work.", targetAnimator);
                yield break;
            }
            
            // Store initial transform values
            initialPosition = kineticTransform.localPosition;
            initialRotation = kineticTransform.localRotation;
            initialScale = kineticTransform.localScale;
            
            Debug.Log($"[AnimDebugger] KineticTransform Found: {kineticTransform.name}\n" +
                $"Position: {initialPosition.ToString("F3")}\n" +
                $"Rotation: {initialRotation.eulerAngles.ToString("F1")}\n" +
                $"Scale: {initialScale.ToString("F3")}", kineticTransform);
            
            // Check 2: Is the Unit component available and configured correctly?
            Unit unit = targetAnimator.gameObject.GetComponentInParent<Unit>();
            if (unit == null)
            {
                Debug.LogError("[AnimDebugger] No Unit component found in parent hierarchy! UnitAnimLis requires a Unit component to function.", targetAnimator);
            }
            else
            {
                Debug.Log($"[AnimDebugger] Unit component found: {unit.name}\n" +
                    $"IsDeath: {unit.IsDeath}\n" +
                    $"Has Movement: {unit.HasMovement}", unit);
            }
            
            // Check 3: Check if kineticTransform has other components that might interfere
            if (kineticTransform.GetComponent<Animator>() != null)
            {
                Debug.LogWarning("[AnimDebugger] kineticTransform has an Animator component which may override UnitAnimLis controls!", kineticTransform);
            }
            
            if (kineticTransform.GetComponent<Animation>() != null)
            {
                Debug.LogWarning("[AnimDebugger] kineticTransform has a legacy Animation component which may override UnitAnimLis controls!", kineticTransform);
            }
            
            // Check 4: Check if initialization might have failed
            if (!targetAnimator.enabled)
            {
                Debug.LogError("[AnimDebugger] UnitAnimLis is disabled! This will prevent animations from running.", targetAnimator);
            }

            // Check 5: Check animation style values
            CheckAnimationValues();
        }
        
        private void CheckAnimationValues()
        {
            // Use reflection to access private fields for debugging purposes
            System.Type type = typeof(UnitAnimLis);
            var hoverAmount = type.GetField("currentHoverAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var wobbleAmount = type.GetField("currentIdleWobbleAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var bankAngle = type.GetField("currentMaxBankAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pitchAngle = type.GetField("currentMaxPitchAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            float hover = 0f, wobble = 0f, bank = 0f, pitch = 0f;
            
            if (hoverAmount != null) hover = (float)hoverAmount.GetValue(targetAnimator);
            if (wobbleAmount != null) wobble = (float)wobbleAmount.GetValue(targetAnimator);
            if (bankAngle != null) bank = (float)bankAngle.GetValue(targetAnimator);
            if (pitchAngle != null) pitch = (float)pitchAngle.GetValue(targetAnimator);
            
            // Check if values are too small to be visible
            if (hover < 0.05f && wobble < 0.5f && bank < 5f && pitch < 5f)
            {
                Debug.LogWarning("[AnimDebugger] Animation values may be too small to be visible!\n" +
                    $"Hover: {hover} (recommended: 0.1-0.3)\n" +
                    $"Wobble: {wobble} (recommended: 1.0-5.0)\n" +
                    $"Bank: {bank} (recommended: 10-30)\n" +
                    $"Pitch: {pitch} (recommended: 10-20)", targetAnimator);
            }
            else
            {
                Debug.Log("[AnimDebugger] Animation values look reasonable:\n" +
                    $"Hover: {hover}\n" +
                    $"Wobble: {wobble}\n" +
                    $"Bank: {bank}\n" +
                    $"Pitch: {pitch}", targetAnimator);
            }
        }
        
        private void ApplyExaggeratedValues()
        {
            // Set styles with extreme values to make animations more visible
            targetAnimator.idleStyle = UnitAnimLis.IdleStyle.HeavyFloat;
            targetAnimator.moveStyle = UnitAnimLis.MoveStyle.AggressiveStrafe;
            targetAnimator.attackStyle = UnitAnimLis.AttackStyle.HeavyCannon;
            targetAnimator.entryStyle = UnitAnimLis.EntryStyle.FastDrop;
            targetAnimator.warpStyle = UnitAnimLis.WarpStyle.QuickBlink;
            
            // Use reflection to modify private fields for testing
            System.Type type = typeof(UnitAnimLis);
            var method = type.GetMethod("ApplyStyles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(targetAnimator, null);
                Debug.Log("[AnimDebugger] Applied exaggerated animation values for better visibility", targetAnimator);
            }
            else
            {
                Debug.LogError("[AnimDebugger] Failed to apply exaggerated values - couldn't access ApplyStyles method", targetAnimator);
            }
        }
        
        private IEnumerator RunTestSequence()
        {
            Debug.Log("[AnimDebugger] Starting animation test sequence...", this);
            
            // Wait a bit before starting tests
            yield return new WaitForSeconds(1f);
            
            // Test 1: Entry Animation
            Debug.Log("[AnimDebugger] Testing Entry Animation", this);
            targetAnimator.PlayEntryAnimation();
            yield return new WaitForSeconds(2f);
            
            // Test 2: Warp Animation
            Debug.Log("[AnimDebugger] Testing Warp Animation", this);
            targetAnimator.PlayWarpAnimation();
            yield return new WaitForSeconds(2f);
            
            // Test 3: PowerUp Animation
            Debug.Log("[AnimDebugger] Testing PowerUp Animation", this);
            targetAnimator.PlayPowerUpAnimation();
            yield return new WaitForSeconds(2f);
            
            Debug.Log("[AnimDebugger] Test sequence complete. If no animations were visible, check console for errors.", this);
        }
        
        private void SetupVisualDebuggers()
        {
            // Create debug container
            debugVisualContainer = new GameObject("AnimationDebugVisuals");
            debugVisualContainer.transform.parent = transform;
            
            // Create axis visualizers
            axisX = CreateAxisLine(Color.red);   // X axis
            axisY = CreateAxisLine(Color.green); // Y axis
            axisZ = CreateAxisLine(Color.blue);  // Z axis
        }
        
        private LineRenderer CreateAxisLine(Color color)
        {
            GameObject axisObj = new GameObject("AxisLine");
            axisObj.transform.parent = debugVisualContainer.transform;
            
            LineRenderer line = axisObj.AddComponent<LineRenderer>();
            line.startWidth = 0.03f;
            line.endWidth = 0.01f;
            line.positionCount = 2;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            
            return line;
        }
        
        private void UpdateDebugVisuals()
        {
            if (kineticTransform == null) return;
            
            // Position debug container at the kineticTransform
            debugVisualContainer.transform.position = kineticTransform.position;
            debugVisualContainer.transform.rotation = kineticTransform.rotation;
            
            // Scale factor for axis visualization
            float axisLength = 0.5f;
            
            // Update axis lines
            axisX.SetPosition(0, Vector3.zero);
            axisX.SetPosition(1, Vector3.right * axisLength);
            
            axisY.SetPosition(0, Vector3.zero);
            axisY.SetPosition(1, Vector3.up * axisLength);
            
            axisZ.SetPosition(0, Vector3.zero);
            axisZ.SetPosition(1, Vector3.forward * axisLength);
        }
        
        void OnDestroy()
        {
            // Clean up visual debuggers
            if (debugVisualContainer != null)
            {
                Destroy(debugVisualContainer);
            }
        }
    }
} 