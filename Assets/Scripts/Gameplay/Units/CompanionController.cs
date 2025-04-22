using UnityEngine;

namespace Cosmicrafts.Units // Match the namespace for easier access from Unit.cs
{
    [RequireComponent(typeof(Unit))] // Ensure this script is attached to a GameObject with a Unit component
    public class CompanionController : MonoBehaviour
    {
        [Header("Orbit Settings")]
        [SerializeField] public float orbitDistance = 3.0f; // How far to orbit
        [SerializeField] public float orbitSpeed = 90.0f; // Degrees per second
        [SerializeField] private float followLerpSpeed = 5.0f; // How quickly to move to the target orbit position

        [Header("Orbit Style")]
        [SerializeField] private OrbitStyle orbitStyle = OrbitStyle.Circle;
        [SerializeField] private float orbitEccentricity = 0.0f; // For elliptical orbits (0=circle, 1=line)
        [SerializeField] private float verticalOffset = 0.0f; // Height offset from parent unit's position
        [SerializeField] private Vector3 fixedOffset = Vector3.zero; // Fixed offset for non-orbiting companions

        [Header("Look Settings")]
        [SerializeField] private LookDirection lookDirection = LookDirection.MovementDirection;
        
        [Header("Companion Behavior")]
        [SerializeField] private bool matchParentState = true; // Whether companion should match parent's enabled/disabled state
        [SerializeField] private bool stayWithDeadParent = false; // Whether to remain after parent dies
        [SerializeField] private float abandonDelay = 0f; // Time before abandoning a dead parent

        private Unit parentUnit;
        private Unit myUnit;
        private Ship myShip; // Optional: Use Ship component for movement if available
        private Renderer[] renderers; // For appearance changes like tint color
        private float currentOrbitAngle = 0.0f;
        private bool isAbandoning = false;
        private float abandonTimer = 0f;

        void Awake()
        {
            myUnit = GetComponent<Unit>();
            myShip = GetComponent<Ship>(); // Try to get Ship component
            renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// Sets the parent unit this companion should follow.
        /// </summary>
        public void SetParent(Unit parent)
        {
            parentUnit = parent;
            
            // Randomize starting angle slightly to avoid perfect stacking
            currentOrbitAngle = Random.Range(0f, 360f); 
        }
        
        /// <summary>
        /// Apply settings from a companion configuration.
        /// </summary>
        public void ApplyConfiguration(CompanionConfig config)
        {
            if (config == null) return;
            
            // Apply orbit settings
            orbitDistance = config.orbitDistance;
            orbitSpeed = config.orbitSpeed;
            
            // Apply appearance settings
            transform.localScale = Vector3.one * config.scale;
            
            // Apply color tint if we have renderers
            if (renderers != null && renderers.Length > 0)
            {
                foreach (Renderer renderer in renderers)
                {
                    // If the material uses the _Color property
                    if (renderer.material.HasProperty("_Color"))
                    {
                        renderer.material.color = config.tintColor;
                    }
                }
            }
        }

        void Update()
        {
            // Handle parent death or abandonment
            if (isAbandoning)
            {
                HandleAbandonment();
                return;
            }
            
            // Check if parent is valid
            if (parentUnit == null)
            {
                HandleParentLost();
                return;
            }
            
            // Check if parent is dead and we should start abandonment process
            if (parentUnit.GetIsDeath() && !stayWithDeadParent)
            {
                if (abandonDelay > 0)
                {
                    isAbandoning = true;
                    abandonTimer = abandonDelay;
                }
                else
                {
                    HandleParentLost();
                }
                return;
            }
            
            // If parent is disabled and we should match parent state, don't update position
            if (matchParentState && parentUnit.GetIsDisabled() && !parentUnit.GetIsDeath())
            {
                return;
            }
            
            // Calculate the desired orbit position based on selected style
            Vector3 targetPosition;
            
            switch (orbitStyle)
            {
                case OrbitStyle.Circle:
                    // Update orbit angle
                    currentOrbitAngle += orbitSpeed * Time.deltaTime;
                    currentOrbitAngle %= 360f; // Keep angle within bounds
                    
                    // Calculate orbit position
                    Vector3 offset = new Vector3(
                        Mathf.Sin(currentOrbitAngle * Mathf.Deg2Rad), 
                        verticalOffset, 
                        Mathf.Cos(currentOrbitAngle * Mathf.Deg2Rad)
                    ) * orbitDistance;
                    
                    targetPosition = parentUnit.transform.position + offset;
                    break;
                    
                case OrbitStyle.Ellipse:
                    // Update orbit angle
                    currentOrbitAngle += orbitSpeed * Time.deltaTime;
                    currentOrbitAngle %= 360f;
                    
                    // Calculate elliptical orbit
                    float a = orbitDistance; // Semi-major axis
                    float b = orbitDistance * (1f - orbitEccentricity); // Semi-minor axis
                    
                    Vector3 ellipseOffset = new Vector3(
                        a * Mathf.Sin(currentOrbitAngle * Mathf.Deg2Rad),
                        verticalOffset,
                        b * Mathf.Cos(currentOrbitAngle * Mathf.Deg2Rad)
                    );
                    
                    targetPosition = parentUnit.transform.position + ellipseOffset;
                    break;
                    
                case OrbitStyle.Fixed:
                    // Maintain a fixed offset from parent
                    targetPosition = parentUnit.transform.position + fixedOffset;
                    break;
                    
                case OrbitStyle.Formation:
                    // Maintain position relative to parent's forward direction
                    targetPosition = parentUnit.transform.position + 
                                     parentUnit.transform.right * fixedOffset.x +
                                     parentUnit.transform.up * fixedOffset.y +
                                     parentUnit.transform.forward * fixedOffset.z;
                    break;
                    
                default:
                    targetPosition = parentUnit.transform.position;
                    break;
            }

            // Move the companion
            MoveToTargetPosition(targetPosition);
            
            // Set companion rotation based on look direction
            SetLookDirection(targetPosition);
        }

        private void SetLookDirection(Vector3 targetPosition)
        {
            switch (lookDirection)
            {
                case LookDirection.TowardParent:
                    if (parentUnit != null)
                        transform.LookAt(parentUnit.transform.position);
                    break;
                    
                case LookDirection.MovementDirection:
                    if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
                        transform.rotation = Quaternion.LookRotation((targetPosition - transform.position).normalized);
                    break;
                    
                case LookDirection.MatchParent:
                    if (parentUnit != null)
                        transform.rotation = parentUnit.transform.rotation;
                    break;
                    
                case LookDirection.Independent:
                    // Don't adjust rotation
                    break;
            }
        }

        private void MoveToTargetPosition(Vector3 targetPosition)
        {
            if (myShip != null && myUnit.InControl()) // Use Ship component if available and unit can move
            {
                // Set a very small stopping distance to try and reach the exact orbit point
                myShip.SetDestination(targetPosition, 0.1f); 
            }
            else // Fallback to simple Lerp movement if no Ship or unit is disabled
            {
                // Prevent movement if unit cannot control itself (e.g., casting, disabled)
                 if (myUnit.InControl()) 
                 {
                     transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followLerpSpeed);
                 }
            }
        }
        
        private void HandleAbandonment()
        {
            abandonTimer -= Time.deltaTime;
            
            if (abandonTimer <= 0f)
            {
                HandleParentLost();
            }
            else
            {
                // Optional: During abandonment, could do special effects or behaviors
                // E.g., slowly drift away, flash, etc.
            }
        }
        
        private void HandleParentLost()
        {
             // What happens when the parent dies or is lost?
             
             if (!stayWithDeadParent)
             {
                 Debug.Log($"Parent of {gameObject.name} lost. Destroying companion.");
                 Destroy(gameObject);
             }
             else
             {
                 // We've chosen to stay after parent death
                 // Could either keep orbiting the last known position or become independent
                 Debug.Log($"Parent of {gameObject.name} lost. Companion is now independent.");
                 
                 // Make the companion independent
                 // Note: You could implement a new behavior here for independent companions
                 parentUnit = null;
             }
        }
    }
    
    /// <summary>
    /// Defines how the companion orbits around the parent
    /// </summary>
    public enum OrbitStyle
    {
        Circle,     // Simple circular orbit around parent
        Ellipse,    // Elliptical orbit with configurable eccentricity
        Fixed,      // Fixed offset from parent in world space
        Formation   // Fixed offset relative to parent's orientation
    }
    
    /// <summary>
    /// Defines which direction the companion should face
    /// </summary>
    public enum LookDirection
    {
        TowardParent,       // Always face the parent
        MovementDirection,   // Face the direction of movement
        MatchParent,        // Match parent's rotation
        Independent         // Don't adjust rotation automatically
    }
} 