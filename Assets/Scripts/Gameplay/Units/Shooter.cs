using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    public class Shooter : MonoBehaviour
    {
        public SphereCollider EnemyDetector;
        [HideInInspector] public bool CanAttack = true;
        [Range(1, 99)] public float RangeDetector = 1f;
        [Range(0, 99)] public float CoolDown = 1f;
        [Range(1, 99)] public float BulletSpeed = 10f;
        [Range(1, 99)] public int BulletDamage = 1;
        [Range(0f, 1f)] public float criticalStrikeChance = 0f;
        public float criticalStrikeMultiplier = 2.0f;
        public bool RotateToEnemy = true;
        public bool StopToAttack = true;
        [Range(1f, 10f)] public float rotationSpeed = 5f;
        public GameObject Bullet;
        public Transform[] Cannons;
        
        [Header("Targeting Settings")]
        [Tooltip("How often to check if a target is still valid (seconds)")]
        public float targetValidationRate = 0.5f;
        [Tooltip("Extra buffer distance beyond detection range before losing target")]
        public float targetLoseTolerance = 1.5f;
        [Tooltip("Targeting priority mode")]
        public TargetPriorityMode priorityMode = TargetPriorityMode.Closest;
        [Tooltip("Buffer to avoid rapidly switching targets")]
        public float targetSwitchThreshold = 2.0f;
        [Tooltip("Whether to enable debug visualization")]
        public bool showDebugVisuals = false;

        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange = new HashSet<Unit>();
        private Unit Target;
        private float lastTargetCheckTime = 0f;
        private bool wasTargetNull = true;
        private Vector3 lastKnownTargetPosition = Vector3.zero;
        
        // Debug variables
        private bool debugLogging = false;
        private float lastShootAttempt = 0f;
        private string lastFailReason = "none";

        // For target selection timing
        private float targetSelectionCooldown = 0f;
        private float targetSelectionDelay = 0.2f; // Delay between target changes, prevents jitter

        // Debug visualizations
        private LineRenderer debugTargetLine;
        private GameObject debugRangeVisualizer;

        public enum TargetPriorityMode
        {
            Closest,        // Closest target first
            LowestHealth,   // Target with lowest health first
            HighestHealth,  // Target with highest health first
            HighestThreat   // Target with highest damage output first (future implementation)
        }

        void Start()
        {
            InRange = new HashSet<Unit>();
            EnemyDetector.radius = RangeDetector;
            MyShip = GetComponent<Ship>();

            // Ensure MyUnit is assigned
            if (MyUnit == null)
            {
                MyUnit = GetComponent<Unit>();
                if (MyUnit == null)
                {
                    Debug.LogError($"Shooter on {gameObject.name} is missing MyUnit reference and couldn't find one on the GameObject. Disabling Shooter.");
                    enabled = false; // Disable the shooter if no Unit component is found
                    return;
                }
            }

            MuzzleFlash = new ParticleSystem[Cannons.Length];

            for (int i = 0; i < Cannons.Length; i++)
            {
                if (Cannons[i].childCount > 0)
                {
                    MuzzleFlash[i] = Cannons[i].GetChild(0).GetComponent<ParticleSystem>();
                }
            }
            if (CoolDown <= 0f) CoolDown = 0.1f;
            
            InitializeDebugVisuals();
        }

        void Update()
        {
            // Gracefully handle if MyUnit is destroyed or becomes null
            if (MyUnit == null)
            {
                return; // Exit Update if MyUnit is not valid
            }

            if (!MyUnit.GetIsDeath() && CanAttack && MyUnit.InControl())
            {
                // Decrement timers
                if (targetSelectionCooldown > 0)
                    targetSelectionCooldown -= Time.deltaTime;
                
                // Check for targets more frequently
                if (Time.time - lastTargetCheckTime > targetValidationRate)
                {
                    ValidateTarget();
                    PerformDistanceCheck();
                    lastTargetCheckTime = Time.time;
                    
                    // If we don't have a target, try to find one
                    if (Target == null && InRange.Count > 0)
                    {
                        FindNewTarget();
                    }
                    
                    // Always ensure detector radius matches range
                    if (EnemyDetector != null && !Mathf.Approximately(EnemyDetector.radius, RangeDetector))
                    {
                        EnemyDetector.radius = RangeDetector;
                    }
                    
                    // Update debug visuals
                    if (showDebugVisuals)
                        UpdateDebugVisuals();
                }
                
                // Only try to shoot if we have a target
                if (Target != null && Target.gameObject != null && Target.gameObject.activeInHierarchy && !Target.GetIsDeath())
                {
                    if (debugLogging && Time.time - lastShootAttempt > 1f)
                    {
                        Debug.Log($"Attempting to shoot at target: {Target.name}");
                        lastShootAttempt = Time.time;
                    }
                    
                    ShootTarget();
                    // Track that we had a target last frame and update last known position
                    wasTargetNull = false;
                    lastKnownTargetPosition = Target.transform.position;
                }
                else
                {
                    // Target is null or invalid, make sure it's actually null to trigger proper handling
                    if (Target != null && (Target.gameObject == null || !Target.gameObject.activeInHierarchy || Target.GetIsDeath()))
                    {
                        Target = null;
                    }
                    
                    if (!wasTargetNull)
                    {
                        // We just lost our target this frame
                        if (MyShip != null && StopToAttack)
                        {
                            MyShip.ResetDestination();
                        }
                        wasTargetNull = true;
                        
                        if (debugLogging)
                        {
                            Debug.Log($"Lost target. Last reason: {lastFailReason}");
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up debug visualization
            if (debugTargetLine != null)
                Destroy(debugTargetLine.gameObject);
            if (debugRangeVisualizer != null)
                Destroy(debugRangeVisualizer);
        }

        /// <summary>
        /// Initialize debug visualization objects
        /// </summary>
        private void InitializeDebugVisuals()
        {
            if (showDebugVisuals)
            {
                // Create line renderer for target visualization
                GameObject lineObj = new GameObject("TargetLine");
                lineObj.transform.SetParent(transform);
                debugTargetLine = lineObj.AddComponent<LineRenderer>();
                debugTargetLine.startWidth = 0.1f;
                debugTargetLine.endWidth = 0.1f;
                debugTargetLine.material = new Material(Shader.Find("Sprites/Default"));
                debugTargetLine.startColor = Color.yellow;
                debugTargetLine.endColor = Color.red;
                debugTargetLine.positionCount = 2;
                
                // Create range visualizer
                debugRangeVisualizer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                debugRangeVisualizer.transform.SetParent(transform);
                debugRangeVisualizer.transform.localPosition = Vector3.zero;
                debugRangeVisualizer.transform.localScale = new Vector3(RangeDetector * 2, RangeDetector * 2, RangeDetector * 2);
                Renderer renderer = debugRangeVisualizer.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
                renderer.material.color = new Color(1, 1, 0, 0.1f);
                Collider collider = debugRangeVisualizer.GetComponent<Collider>();
                Destroy(collider);
            }
        }
        
        /// <summary>
        /// Update debug visualization objects
        /// </summary>
        private void UpdateDebugVisuals()
        {
            if (!showDebugVisuals) return;
            
            // Update target line
            if (debugTargetLine != null)
            {
                if (Target != null)
                {
                    debugTargetLine.enabled = true;
                    debugTargetLine.SetPosition(0, transform.position + Vector3.up * 0.5f);
                    debugTargetLine.SetPosition(1, Target.transform.position + Vector3.up * 0.5f);
                }
                else
                {
                    debugTargetLine.enabled = false;
                }
            }
            
            // Update range visualizer
            if (debugRangeVisualizer != null)
            {
                // Update size if range changed
                float diameter = RangeDetector * 2;
                if (debugRangeVisualizer.transform.localScale.x != diameter)
                {
                    debugRangeVisualizer.transform.localScale = new Vector3(diameter, diameter, diameter);
                }
            }
        }

        /// <summary>
        /// Validate if the current target is still valid
        /// </summary>
        private void ValidateTarget()
        {
            // Skip if already null
            if (Target == null) return;
            
            string reason = "valid";
            
            // Check if target is valid
            if (Target.gameObject == null || !Target.gameObject.activeInHierarchy)
            {
                reason = "target gameObject is null or inactive";
                Target = null;
            }
            else if (Target.GetIsDeath())
            {
                reason = "target is dead";
                Target = null;
            }
            else if (!InRange.Contains(Target))
            {
                // Check if it's truly out of range or just a collection issue
                float distance = Vector3.Distance(transform.position, Target.transform.position);
                if (distance <= RangeDetector * targetLoseTolerance)
                {
                    // Target is actually in range, fix collection issue
                    if (!InRange.Contains(Target))
                    {
                        InRange.Add(Target);
                    }
                }
                else
                {
                    reason = $"target out of range ({distance} > {RangeDetector})";
                    Target = null;
                }
            }
            
            if (Target == null)
            {
                lastFailReason = reason;
                FindNewTarget();
            }
        }

        /// <summary>
        /// Check if the target is too far away - they might have moved but not triggered the exit event
        /// </summary>
        private void PerformDistanceCheck()
        {
            // Cleanup null references from InRange
            CleanupInRangeList();
            
            // Skip the distance check if Target is null
            if (Target == null) return;
            
            float distanceToTarget = Vector3.Distance(transform.position, Target.transform.position);
            // Use tolerance to avoid flickering at the edge of range
            if (distanceToTarget > RangeDetector * targetLoseTolerance)
            {
                RemoveEnemy(Target);
                // Target is now null, so return early
                return;
            }
            
            // Check if a better target has appeared within cooldown threshold
            if (targetSelectionCooldown <= 0 && Target != null && InRange.Count > 1)
            {
                Unit bestTarget = FindBestTargetFromList();
                if (bestTarget != null && bestTarget != Target)
                {
                    float distToCurrent = Vector3.Distance(transform.position, Target.transform.position);
                    float distToBest = Vector3.Distance(transform.position, bestTarget.transform.position);
                    
                    // Only switch if the new target is significantly better
                    if (distToBest < distToCurrent - targetSwitchThreshold)
                    {
                        SetTarget(bestTarget);
                        targetSelectionCooldown = targetSelectionDelay;
                    }
                }
            }
        }

        /// <summary>
        /// Remove null or dead units from the InRange list
        /// </summary>
        private void CleanupInRangeList()
        {
            // Use temp list to avoid collection modification issues
            List<Unit> unitsToRemove = new List<Unit>();
            
            foreach (Unit unit in InRange)
            {
                if (unit == null || unit.GetIsDeath())
                {
                    unitsToRemove.Add(unit);
                }
            }
            
            foreach (Unit unit in unitsToRemove)
            {
                InRange.Remove(unit);
            }
        }

        /// <summary>
        /// Handle shooting at the target
        /// </summary>
        public void ShootTarget()
        {
            // Make sure target is still valid
            if (Target == null || Target.gameObject == null || !Target.gameObject.activeInHierarchy)
            {
                Target = null;
                return;
            }

            // Quick validity check before proceeding
            if (Target.GetIsDeath())
            {
                Target = null;
                lastFailReason = "target died before shooting";
                return;
            }

            // Rotate towards target with improved accuracy
            if (RotateToEnemy)
            {
                Vector3 targetPos = Target.transform.position;
                
                // Apply some prediction for moving targets
                Rigidbody targetRb = Target.GetComponent<Rigidbody>();
                if (targetRb != null && targetRb.linearVelocity.magnitude > 0.1f)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, targetPos);
                    float timeToReach = distanceToTarget / BulletSpeed;
                    targetPos += targetRb.linearVelocity * timeToReach * 0.5f; // 50% prediction
                }
                
                Vector3 direction = (targetPos - transform.position).normalized;
                direction.y = 0;
                direction = direction.normalized;
                
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    Quaternion.LookRotation(direction), 
                    Time.deltaTime * rotationSpeed
                );
                
                // Check if we're facing the target enough to shoot
                float dot = Vector3.Dot(transform.forward, direction);
                if (dot < 0.7f) // Reduced from 0.8 to 0.7 (about 45 degrees) - more lenient
                {
                    // Not facing target enough, don't shoot yet
                    if (DelayShoot <= 0f)
                    {
                        DelayShoot = CoolDown * 0.25f; // Shorter delay when turning
                    }
                    
                    if (debugLogging && Time.time - lastShootAttempt > 1f)
                    {
                        Debug.Log($"Not shooting: Not facing target enough (dot={dot:F2})");
                        lastShootAttempt = Time.time;
                        lastFailReason = "angle";
                    }
                    return;
                }
            }

            // Fire if cooldown is ready
            if (DelayShoot <= 0f)
            {
                FireProjectiles();
                DelayShoot = CoolDown;
                MyUnit.GetAnimator().SetTrigger("Attack");
            }
            else
            {
                DelayShoot -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Fire projectiles at the target
        /// </summary>
        private void FireProjectiles()
        {
            // Final validation check before firing
            if (Target == null || Target.gameObject == null || !Target.gameObject.activeInHierarchy || Target.GetIsDeath())
            {
                // Target is no longer valid, don't fire
                Target = null;
                return;
            }
            
            foreach (Transform cannon in Cannons)
            {
                GameObject bulletPrefab = Instantiate(Bullet, cannon.position, cannon.rotation);
                Projectile bullet = bulletPrefab.GetComponent<Projectile>();
                if (bullet != null)
                {
                    bullet.MyTeam = MyUnit.MyTeam;
                    bullet.SetTarget(Target.gameObject);
                    bullet.Speed = BulletSpeed;
                    
                    // Calculate damage including critical strike chance
                    bool isCritical = Random.value < criticalStrikeChance;
                    int damage = isCritical ? 
                        Mathf.RoundToInt(BulletDamage * criticalStrikeMultiplier) : 
                        BulletDamage;
                    
                    bullet.Dmg = damage;
                    
                    // Add visual effect for critical hits
                    if (isCritical)
                    {
                        bullet.transform.localScale *= 1.2f;
                    }
                }

                // Play muzzle flash
                ParticleSystem flash = cannon.childCount > 0 ? cannon.GetChild(0).GetComponent<ParticleSystem>() : null;
                flash?.Play();
            }
        }

        /// <summary>
        /// Set the target and update ship destination if needed
        /// </summary>
        public void SetTarget(Unit target)
        {
            Target = target;
            
            // Update ship behavior
            if (MyShip == null || !StopToAttack) return;
            
            if (Target == null)
            {
                MyShip.ResetDestination();
            }
            else
            {
                // Position slightly inside the range to ensure we're in attack range
                float optimalDistance = RangeDetector * 0.8f;
                MyShip.SetDestination(Target.transform.position, optimalDistance);
            }
        }

        /// <summary>
        /// Add an enemy to the in-range list
        /// </summary>
        public void AddEnemy(Unit enemy)
        {
            if (enemy == null || enemy.GetIsDeath())
                return;
                
            if (InRange.Add(enemy))
            {
                // Find a new target if we don't have one, or the new enemy is closer
                if (Target == null)
                {
                    FindNewTarget();
                }
                else if (targetSelectionCooldown <= 0)
                {
                    float distanceToCurrentTarget = Vector3.Distance(transform.position, Target.transform.position);
                    float distanceToNewEnemy = Vector3.Distance(transform.position, enemy.transform.position);
                    
                    // Switch to the new enemy if it's significantly closer
                    if (distanceToNewEnemy < distanceToCurrentTarget - targetSwitchThreshold)
                    {
                        SetTarget(enemy);
                        targetSelectionCooldown = targetSelectionDelay;
                    }
                }
            }
        }

        /// <summary>
        /// Remove an enemy from the in-range list
        /// </summary>
        public void RemoveEnemy(Unit enemy)
        {
            if (InRange.Remove(enemy) && Target == enemy)
            {
                Target = null;
                FindNewTarget();
            }
        }

        /// <summary>
        /// Find a new target based on priority mode
        /// </summary>
        private void FindNewTarget()
        {
            if (InRange.Count == 0)
                return;
                
            Unit newTarget = FindBestTargetFromList();
            SetTarget(newTarget);
        }
        
        /// <summary>
        /// Find the best target from the InRange list based on priority mode
        /// </summary>
        private Unit FindBestTargetFromList()
        {
            if (InRange.Count == 0)
                return null;
                
            CleanupInRangeList();
            
            if (InRange.Count == 0)
                return null;
                
            Unit bestTarget = null;
            
            switch (priorityMode)
            {
                case TargetPriorityMode.Closest:
                    // Find closest enemy
                    float closestDistance = float.MaxValue;
                    foreach (Unit enemy in InRange)
                    {
                        if (enemy == null || enemy.GetIsDeath()) continue;
                        
                        float distance = Vector3.Distance(transform.position, enemy.transform.position);
                        if (distance < closestDistance)
                        {
                            bestTarget = enemy;
                            closestDistance = distance;
                        }
                    }
                    break;
                    
                case TargetPriorityMode.LowestHealth:
                    // Find enemy with lowest health
                    int lowestHP = int.MaxValue;
                    foreach (Unit enemy in InRange)
                    {
                        if (enemy == null || enemy.GetIsDeath()) continue;
                        
                        if (enemy.HitPoints < lowestHP)
                        {
                            bestTarget = enemy;
                            lowestHP = enemy.HitPoints;
                        }
                    }
                    break;
                    
                case TargetPriorityMode.HighestHealth:
                    // Find enemy with highest health
                    int highestHP = 0;
                    foreach (Unit enemy in InRange)
                    {
                        if (enemy == null || enemy.GetIsDeath()) continue;
                        
                        if (enemy.HitPoints > highestHP)
                        {
                            bestTarget = enemy;
                            highestHP = enemy.HitPoints;
                        }
                    }
                    break;
                    
                // Future implementation for HighestThreat
                // case TargetPriorityMode.HighestThreat:
                //     break;
            }
            
            return bestTarget;
        }

        /// <summary>
        /// Stop attacking completely
        /// </summary>
        public void StopAttack()
        {
            CanAttack = false;
            InRange.Clear();
            SetTarget(null);
        }

        /// <summary>
        /// Get the target's unique ID or 0 if no target
        /// </summary>
        public int GetIdTarget() => Target == null ? 0 : Target.getId();
        
        /// <summary>
        /// Get the current target reference - for use by spells and other systems
        /// </summary>
        public Unit GetCurrentTarget() => Target;

        /// <summary>
        /// Initialize shooter stats from NFT data
        /// </summary>
        public virtual void InitStatsFromNFT(NFTsUnit nFTsUnit)
        {
            if (nFTsUnit != null) BulletDamage = nFTsUnit.Damage;
        }

        /// <summary>
        /// Reset the shooter component to initial state
        /// </summary>
        public void ResetShooter()
        {
            // Reset attack parameters
            DelayShoot = 0f;
            CanAttack = true;
            
            // Clear current targets and enemies
            Target = null;
            InRange.Clear();
            
            // Reset timers
            lastTargetCheckTime = 0f;
            wasTargetNull = true;
            targetSelectionCooldown = 0f;
            
            // Reset detection range in case it was modified
            EnemyDetector.radius = RangeDetector;
            
            // Update debug visuals
            if (showDebugVisuals)
                UpdateDebugVisuals();
        }
        
        /// <summary>
        /// Static utility method to find the nearest enemy from a point - can be used by spells
        /// </summary>
        public static Unit FindNearestEnemyFromPoint(Vector3 fromPosition, Team myTeam, float maxDistance = 100f)
        {
            Unit nearestEnemy = null;
            float minDistance = maxDistance;
            
            // Get all units in the scene
            Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
            
            foreach (Unit unit in allUnits)
            {
                // Skip if null, dead, or on the same team
                if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(myTeam))
                    continue;
                    
                float distance = Vector3.Distance(fromPosition, unit.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestEnemy = unit;
                }
            }
            
            return nearestEnemy;
        }
    }
}
