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
        [Tooltip("Transform for power-up effects like lasers. If not set, will use first cannon position.")]
        public Transform powerUpOrigin;
        
        [Header("Targeting Settings")]
        [Tooltip("How often to check if a target is still valid (seconds)")]
        public float targetValidationRate = 0.5f;
        [Tooltip("Extra buffer distance beyond detection range before losing target")]
        public float targetLoseTolerance = 1.5f;
        [Tooltip("Targeting priority mode")]
        public TargetPriorityMode priorityMode = TargetPriorityMode.Closest;
        [Tooltip("Buffer to avoid rapidly switching targets")]
        public float targetSwitchThreshold = 2.0f;

        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange = new HashSet<Unit>();
        private Unit Target;
        private float lastTargetCheckTime = 0f;
        private bool wasTargetNull = true;
        private Vector3 lastKnownTargetPosition = Vector3.zero;

        // For target selection timing
        private float targetSelectionCooldown = 0f;
        private float targetSelectionDelay = 0.2f; // Delay between target changes, prevents jitter

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
                    enabled = false;
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
        }

        void Update()
        {
            // Gracefully handle if MyUnit is destroyed or becomes null
            if (MyUnit == null)
            {
                return;
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
                }
                
                // Only try to shoot if we have a target
                if (Target != null && Target.gameObject != null && Target.gameObject.activeInHierarchy && !Target.GetIsDeath())
                {
                    ShootTarget();
                    wasTargetNull = false;
                    lastKnownTargetPosition = Target.transform.position;
                }
                else
                {
                    if (Target != null && (Target.gameObject == null || !Target.gameObject.activeInHierarchy || Target.GetIsDeath()))
                    {
                        Target = null;
                    }
                    
                    if (!wasTargetNull)
                    {
                        if (MyShip != null && StopToAttack)
                        {
                            // Only reset destination if we don't have any valid targets
                            if (InRange.Count == 0)
                            {
                                MyShip.ResetDestination();
                            }
                            else
                            {
                                // Try to find a new target instead of resetting
                                FindNewTarget();
                            }
                        }
                        wasTargetNull = true;
                    }
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
            // If target is null, clear current target
            if (target == null)
            {
                Target = null;
                if (MyShip != null)
                {
                    MyShip.ResetDestination();
                }
                return;
            }
            
            // If target is already set, don't change unless it's a taunt unit
            if (Target != null && Target != target)
            {
                // Only switch if new target is a taunt unit or current target is not a taunt unit
                if (!target.HasTaunt && Target.HasTaunt)
                {
                    return;
                }
            }
            
            // Set new target
            Target = target;
            
            // Update ship destination
            if (MyShip != null)
            {
                MyShip.SetDestination(Target.transform.position, RangeDetector * 0.5f);
            }
            
            // Reset target selection cooldown
            targetSelectionCooldown = 0f;
            
            // Debug.Log($"[{MyUnit.MyTeam}] Unit {gameObject.name} targeting {target.name} (HasTaunt: {target.HasTaunt})");
        }

        /// <summary>
        /// Handle taunt effect by forcing target selection
        /// </summary>
        public void HandleTauntEffect()
        {
            // Force immediate target re-evaluation regardless of cooldown
            targetSelectionCooldown = 0f;
            
            // Clear current target to force re-evaluation
            SetTarget(null);
            
            // Find best target from current list
            Unit newTarget = FindBestTargetFromList();
            if (newTarget != null)
            {
                Debug.Log($"Taunt effect: Selected new target {newTarget.name}");
                SetTarget(newTarget);
                
                // Force ship to move towards taunt target
                if (MyShip != null)
                {
                    MyShip.SetDestination(newTarget.transform.position, RangeDetector * 0.5f);
                }
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
                
                // Reset the destination to either follow player or return to spawn based on configuration
                Ship shipComponent = GetComponent<Ship>();
                if (shipComponent != null)
                {
                    shipComponent.ResetDestination();
                }
                
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
            float closestDistance = float.MaxValue;
            
            // First pass: Look for taunt units with absolute priority
            foreach (Unit enemy in InRange)
            {
                if (enemy == null || enemy.GetIsDeath()) continue;
                
                // If this is a taunt unit, it gets absolute priority
                if (enemy.HasTaunt)
                {
                    float distance = Vector3.Distance(transform.position, enemy.transform.position);
                    // Only consider taunt units within detection range
                    if (distance <= RangeDetector)
                    {
                        if (distance < closestDistance)
                        {
                            bestTarget = enemy;
                            closestDistance = distance;
                        }
                    }
                }
            }
            
            // If we found a taunt unit, return it immediately and force target switch
            if (bestTarget != null)
            {
                // Debug.Log($"Found taunt unit {bestTarget.name} at distance {closestDistance}");
                targetSelectionCooldown = 0f; // Override any cooldown
                return bestTarget;
            }
            
            // Second pass: Look for regular units based on priority mode
            switch (priorityMode)
            {
                case TargetPriorityMode.Closest:
                    // Find closest enemy
                    closestDistance = float.MaxValue;
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
