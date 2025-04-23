using UnityEngine;
using SensorToolkit; // Add missing namespace for SteeringRig
using System.Collections.Generic;
using Cosmicrafts; // Add for UnitsDataBase access

namespace Cosmicrafts.Units // Match the namespace for easier access from Unit.cs
{
    [RequireComponent(typeof(Unit))] // Ensure this script is attached to a GameObject with a Unit component
    public class CompanionController : MonoBehaviour
    {
        [Header("Orbit Settings")]
        [SerializeField] public float orbitDistance = 3.0f; // How far to orbit
        [SerializeField] public float orbitSpeed = 90.0f; // Degrees per second
        [SerializeField] private float followLerpSpeed = 5.0f; // How quickly to move to the target orbit position (Used if Ship component missing)

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
        private Shooter myShooter; // Reference to shooter component if available
        private Renderer[] renderers; // For appearance changes like tint color
        private float currentOrbitAngle = 0.0f;
        private bool isAbandoning = false;
        private float abandonTimer = 0f;
        private SteeringRig mySteeringRig; // Cache the steering rig if ship uses one
        
        // Timer for periodic enemy detection refresh
        private float detectionRefreshTimer = 0f;
        private const float DETECTION_REFRESH_INTERVAL = 2.0f; // Refresh every 2 seconds

        void Awake()
        {
            myUnit = GetComponent<Unit>();
            myShip = GetComponent<Ship>(); // Try to get Ship component
            myShooter = GetComponent<Shooter>(); // Get shooter component if exists
            renderers = GetComponentsInChildren<Renderer>();
            
            // If we have a ship component, try to get its steering rig
            if (myShip != null)
            {
                mySteeringRig = myShip.MySt; 
                // Configure ship for companion role
                ConfigureShipForCompanion();
            }
        }

        /// <summary>
        /// Sets the parent unit this companion should follow.
        /// </summary>
        public void SetParent(Unit parent)
        {
            parentUnit = parent;
            
            // Set shooter to only target enemies, not parent or allies
            ConfigureShooterTargeting();
            
            // Randomize starting angle slightly to avoid perfect stacking
            currentOrbitAngle = Random.Range(0f, 360f); 
            
            // Ensure ship is configured correctly
            ConfigureShipForCompanion();
            
            // Force detection immediately to find enemies at spawn time
            // This ensures companions don't need to wait until enemies enter their trigger
            if (myShooter != null)
            {
                ForceEnemyDetection();
            }
        }
        
        /// <summary>
        /// Configures the Ship/SteeringRig component for companion behavior 
        /// (prevents default AI, ensures responsiveness).
        /// </summary>
        private void ConfigureShipForCompanion()
        {
            if (myShip != null && mySteeringRig != null)
            {
                // Stop the Ship from seeking its default target (like the enemy base)
                // We achieve this by constantly setting the destination in Update
                // No need to set IsSeeking (it's read-only)
                mySteeringRig.Destination = transform.position; // Set initial destination to current pos
                
                // Keep rotation enabled if the shooter needs it
                // Shooter component handles its own rotation via `RotateToEnemy` flag
                mySteeringRig.RotateTowardsTarget = false; // We handle rotation or let Shooter handle it
                
                // Adjust ship parameters for responsiveness
                myShip.StoppingDistance = 0.1f; 
                myShip.AvoidanceRange = 1f; // Small avoidance range for nearby obstacles
                myShip.AlignRotationWithMovement = false; // Let this script or Shooter handle rotation

                // Ensure ship can move
                myShip.CanMove = true;
                
               // Debug.Log($"Configured Ship/SteeringRig for companion {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Configure shooter to only target enemies, not the parent or allies
        /// </summary>
        private void ConfigureShooterTargeting()
        {
            if (myShooter != null && myUnit != null)
            {
                 // Get existing enemy detector trigger
                 if (myShooter.EnemyDetector != null)
                 {
                     // Add/get the target filter component
                     CompanionTargetFilter filter = myShooter.EnemyDetector.gameObject.GetComponent<CompanionTargetFilter>();
                     if (filter == null)
                     {
                         filter = myShooter.EnemyDetector.gameObject.AddComponent<CompanionTargetFilter>();
                     }
                     // Initialize filter with references to this companion and its unit
                     filter.Initialize(this, myUnit, myShooter);
                     
                     // IMPORTANT: Force immediate detection by temporarily toggling the collider
                     bool wasEnabled = myShooter.EnemyDetector.enabled;
                     myShooter.EnemyDetector.enabled = false;
                     myShooter.EnemyDetector.enabled = wasEnabled;
                     
                     // Force radius to match shooter settings with world scale
                     myShooter.EnemyDetector.radius = myShooter.GetWorldDetectionRange();
                     
                     // CRITICAL: Manually find nearby enemies - use GameMng to get all units
                     if (GameMng.GM != null)
                     {
                         List<Unit> allUnits = GameMng.GM.GetUnitsListClone();
                         foreach (Unit unit in allUnits)
                         {
                             if (unit != null && !unit.GetIsDeath() && myUnit.IsEnemy(unit) && !IsFriendly(unit))
                             {
                                 float distance = Vector3.Distance(transform.position, unit.transform.position);
                                 if (distance <= myShooter.GetWorldDetectionRange())
                                 {
                                     myShooter.AddEnemy(unit);
                                 }
                             }
                         }
                     }
                 }
                 
                 // Immediately stop any attack to clear potential friendly target
                 myShooter.StopAttack();
                 
                 // Re-enable shooting
                 myShooter.CanAttack = true;
                    
                 // Debug.Log($"Configured shooter targeting for companion {gameObject.name} to only target enemies");
            }
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
            
            // Apply stats from SO data if available
            if (config.unitData != null && myUnit != null)
            {
                // Create an NFT unit from the SO data to apply stats
                NFTsUnit unitData = config.unitData.ToNFTCard();
                
                // Apply the data to the unit
                myUnit.SetNfts(unitData);
                
                // Configure shooter component with data from the SO
                if (myShooter != null)
                {
                    myShooter.BulletDamage = unitData.Damage;
                    myShooter.CoolDown = 1.0f; // Default value if not specified in SO
                    
                    // Let the shooter handle its own range settings via InitStatsFromNFT
                    myShooter.InitStatsFromNFT(unitData);
                }
                
                // Configure ship component with data from the SO
                if (myShip != null)
                {
                    myShip.MaxSpeed = unitData.Speed;
                }
            }
            
            // Re-configure ship settings after applying config
            ConfigureShipForCompanion();
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
            
            // If parent is disabled and we should match parent state, prevent movement update
            if (matchParentState && parentUnit.GetIsDisabled() && !parentUnit.GetIsDeath())
            {
                 if(myShip != null) myShip.SetDestination(transform.position, 0.1f); // Tell ship to stop
                 return;
            }
            
            // Periodically refresh enemy detection to ensure we detect new enemies
            detectionRefreshTimer -= Time.deltaTime;
            if (detectionRefreshTimer <= 0f)
            {
                if (myShooter != null)
                {
                    // Only force detection if shooter doesn't have any targets
                    if (myShooter.GetIdTarget() == 0)
                    {
                        ForceEnemyDetection();
                    }
                }
                detectionRefreshTimer = DETECTION_REFRESH_INTERVAL;
            }
            
            // Calculate the desired orbit position based on selected style
            Vector3 targetPosition = CalculateTargetOrbitPosition();

            // Move the companion towards the calculated orbit position
            MoveToTargetPosition(targetPosition);
            
            // Set companion rotation based on look direction, unless Shooter is rotating
            SetLookDirection(targetPosition);
        }
        
        Vector3 CalculateTargetOrbitPosition()
        {
            if (parentUnit == null) return transform.position;

            switch (orbitStyle)
            {
                case OrbitStyle.Circle:
                    currentOrbitAngle = (currentOrbitAngle + orbitSpeed * Time.deltaTime) % 360f;
                    Vector3 offset = new Vector3(Mathf.Sin(currentOrbitAngle * Mathf.Deg2Rad), verticalOffset, Mathf.Cos(currentOrbitAngle * Mathf.Deg2Rad)) * orbitDistance;
                    return parentUnit.transform.position + offset;
                    
                case OrbitStyle.Ellipse:
                    currentOrbitAngle = (currentOrbitAngle + orbitSpeed * Time.deltaTime) % 360f;
                    float a = orbitDistance; // Semi-major axis
                    float b = orbitDistance * (1f - orbitEccentricity); // Semi-minor axis
                    Vector3 ellipseOffset = new Vector3(a * Mathf.Sin(currentOrbitAngle * Mathf.Deg2Rad), verticalOffset, b * Mathf.Cos(currentOrbitAngle * Mathf.Deg2Rad));
                    return parentUnit.transform.position + ellipseOffset;
                    
                case OrbitStyle.Fixed:
                    return parentUnit.transform.position + fixedOffset;
                    
                case OrbitStyle.Formation:
                    return parentUnit.transform.position + 
                           parentUnit.transform.right * fixedOffset.x +
                           parentUnit.transform.up * fixedOffset.y +
                           parentUnit.transform.forward * fixedOffset.z;
                    
                default:
                    return parentUnit.transform.position;
            }
        }

        private void SetLookDirection(Vector3 targetPosition)
        {
            // If the Shooter is actively rotating towards an enemy, let it control rotation
            if (myShooter != null && myShooter.RotateToEnemy && myShooter.GetIdTarget() != 0)
            {
                return; // Shooter handles rotation
            }

            switch (lookDirection)
            {
                case LookDirection.TowardParent:
                    if (parentUnit != null)
                        transform.LookAt(parentUnit.transform.position);
                    break;
                    
                case LookDirection.MovementDirection:
                     // Look towards the calculated target position if moving significantly
                     if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
                     {    
                         Vector3 direction = (targetPosition - transform.position).normalized;
                         if (direction.sqrMagnitude > 0.01f) // Avoid zero direction
                         {
                            transform.rotation = Quaternion.LookRotation(direction);
                         }
                     }
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
            if (!myUnit.InControl()) return; // Don't move if disabled or casting
            
            if (myShip != null) 
            {
                // Use the ship's movement system by setting its destination
                myShip.SetDestination(targetPosition, 0.1f); 
            }
            else // Fallback to simple Lerp movement if no Ship component
            {
                 transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followLerpSpeed);
            }
        }
        
        private void HandleAbandonment()
        {
            abandonTimer -= Time.deltaTime;
            if (abandonTimer <= 0f)
            {
                HandleParentLost();
            }
        }
        
        private void HandleParentLost()
        {
             if (!stayWithDeadParent)
             {
                 // Debug.Log($"Parent of {gameObject.name} lost. Destroying companion.");
                 if (this != null && gameObject != null) Destroy(gameObject);
             }
             else
             {
                 // Debug.Log($"Parent of {gameObject.name} lost. Companion is now independent.");
                 // Become independent - stop orbiting
                 parentUnit = null;
                 if (myShip != null) myShip.SetDestination(transform.position, 0.1f); // Stop moving
                 // Could add logic here for independent behavior (e.g., patrol, return to base)
             }
             parentUnit = null; // Ensure parent is null
        }
        
        // Check if a unit is friendly to this companion
        public bool IsFriendly(Unit other)
        {
            // Units are friendly if:
            // 1. They are the parent unit
            // 2. They are on the same team
            if (other == null || myUnit == null) return false;
            
            return (other == parentUnit) || (other.MyFaction == myUnit.MyFaction);
        }
        
        /// <summary>
        /// Forces the companion to scan for enemies in range. 
        /// Can be called externally to refresh targeting.
        /// </summary>
        public void ForceEnemyDetection()
        {
            if (myShooter == null || myUnit == null || myShooter.EnemyDetector == null)
                return;
                
            // Force enemy detector to refresh by toggling
            bool wasEnabled = myShooter.EnemyDetector.enabled;
            myShooter.EnemyDetector.enabled = false;
            myShooter.EnemyDetector.enabled = wasEnabled;
            
            // Force radius update
            myShooter.EnemyDetector.radius = myShooter.GetWorldDetectionRange();
            
            // Manually check for nearby enemies
            if (GameMng.GM != null)
            {
                List<Unit> allUnits = GameMng.GM.GetUnitsListClone();
                int enemiesFound = 0;
                
                foreach (Unit unit in allUnits)
                {
                    if (unit != null && !unit.GetIsDeath() && myUnit.IsEnemy(unit) && !IsFriendly(unit))
                    {
                        float distance = Vector3.Distance(transform.position, unit.transform.position);
                        if (distance <= myShooter.GetWorldDetectionRange())
                        {
                            myShooter.AddEnemy(unit);
                            enemiesFound++;
                        }
                    }
                }
                
                // Debug.Log($"Companion {gameObject.name} found {enemiesFound} enemies in detection range");
            }
            
            // Make sure shooting is enabled
            myShooter.CanAttack = true;
        }
    }
    
    /// <summary>
    /// Helper component added to the Shooter's EnemyDetector trigger 
    /// to filter targets for companions.
    /// </summary>
    [RequireComponent(typeof(Collider))] // Ensure there's a collider to trigger events
    public class CompanionTargetFilter : MonoBehaviour
    {
        private CompanionController companionController;
        private Unit companionUnit;
        private Shooter shooter;
        
        public void Initialize(CompanionController controller, Unit selfUnit, Shooter shooterComponent)
        {
            companionController = controller;
            companionUnit = selfUnit;
            shooter = shooterComponent;
            
            // Ensure the collider is a trigger
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
        
        void OnTriggerEnter(Collider other)
        {
            // Skip filtering if setup is incomplete or components are missing
            if (companionController == null || companionUnit == null || shooter == null)
                return;
                
            Unit unit = other.GetComponentInParent<Unit>();
            if (unit != null && unit != companionUnit) // Ensure we don't target self
            {
                // Only target enemies (check using the controller's IsFriendly method)
                if (!companionController.IsFriendly(unit) && !unit.GetIsDeath())
                {
                    // Add as valid target
                    shooter.AddEnemy(unit);
                }
            }
        }
        
        void OnTriggerExit(Collider other)
        {
            // Skip if setup is incomplete or components are missing
            if (shooter == null)
                return;
            
            Unit unit = other.GetComponentInParent<Unit>();
            if (unit != null)
            {
                // Remove from potential targets
                shooter.RemoveEnemy(unit);
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
        MatchParent,        // Match parents rotation
        Independent         // Don't adjust rotation automatically
    }
} 