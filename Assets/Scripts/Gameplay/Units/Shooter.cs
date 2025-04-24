using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    /// <summary>
    /// Defines how the shooter's range is visualized.
    /// </summary>
    public enum RangeVisualizationType
    {
        None,   // No visualization
        Circle, // Draw a circle representing the AttackRange
        Line    // Draw a line pointing forward representing the AttackRange
    }

    [RequireComponent(typeof(Unit))]
    public class Shooter : MonoBehaviour
    {
        [Header("Detection & Aggro")]
        [Tooltip("The collider responsible for detecting potential targets.")]
        public SphereCollider EnemyDetector;
        [Tooltip("The radius within which enemies are detected and unit can become aggressive.")]
        [Range(1, 150)] public float DetectionRange = 15f;
        [Tooltip("The radius within which the unit can actually attack.")]
        [Range(1, 150)] public float AttackRange = 10f;
        [Tooltip("How quickly the shooter recalculates its best target (seconds).")]
        [Range(0.1f, 1.0f)] public float TargetUpdateInterval = 0.2f;
        
        [Header("Attack Properties")]
        [HideInInspector] public bool CanAttack = true;
        [Range(0, 99)] public float CoolDown = 1f;
        [Range(1, 99)] public float BulletSpeed = 10f;
        [Range(1, 99)] public int BulletDamage = 1;
        [Range(0f, 1f)] public float criticalStrikeChance = 0f;
        public float criticalStrikeMultiplier = 2.0f;
        
        [Header("Movement & Rotation")]
        public bool RotateToEnemy = true;
        public bool StopToAttack = true;
        [Range(1f, 10f)] public float rotationSpeed = 5f;
        
        [Header("Projectile Settings")]
        public GameObject Bullet;
        public Transform[] Cannons;
        
        [Header("VFX Settings")]
        [Tooltip("Impact effect when projectile hits a shield")]
        public GameObject ShieldImpactEffect;
        [Tooltip("Impact effect when projectile hits armor/health")]
        public GameObject ArmorImpactEffect;

        [Header("Range Visualization")]
        [Tooltip("How to visualize the attack range.")]
        [SerializeField] private RangeVisualizationType rangeVisualizationType = RangeVisualizationType.Circle;
        [SerializeField][Range(12, 72)] private int lineSegments = 36;
        [SerializeField] private Color rangeColor = Color.yellow;
        [SerializeField] private float rangeLineWidth = 0.1f;
        
        // Internal state tracking
        private enum ShooterState 
        {
            Idle,       // Default state, no enemies detected
            Pursuing,   // Enemies detected but out of attack range 
            Attacking   // Enemy in attack range, actively attacking
        }
        
        // Private fields
        private ShooterState _currentState = ShooterState.Idle;
        private LineRenderer _rangeLineRenderer;
        private Vector3 _lastScale;
        private float _targetRecalcTimer = 0f;
        private float _attackCooldownTimer = 0f;
        private Unit _currentTarget = null;
        private Unit _myUnit;
        private Unit _myShip;
        
        // Potential targets list - using HashSet for O(1) contains checks
        private HashSet<Unit> _potentialTargets = new HashSet<Unit>();
        
        // For faster nearest target finding without generating garbage
        private List<Unit> _validTargetsCache = new List<Unit>(10);
        
        // Cache performance for muzzle flash
        private ParticleSystem[] _muzzleFlashEffects;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Cache components
            _myUnit = GetComponent<Unit>();
            _myShip = GetComponent<Unit>();
            
            // Validate detector
            if (EnemyDetector == null)
            {
                EnemyDetector = GetComponentInChildren<SphereCollider>();
                if (EnemyDetector == null)
                {
                    Debug.LogError($"Shooter on {gameObject.name} needs an EnemyDetector SphereCollider!", this);
                    this.enabled = false;
                    return;
                }
            }
            
            // Initialize scale tracking
            _lastScale = transform.lossyScale;
            
            // Set detector radius based on current scale
            EnemyDetector.radius = GetScaledDetectionRange();
            
            // Setup muzzle flash references
            SetupMuzzleFlashEffects();
            
            // Create range visualizer
            SetupRangeVisualizer();
            
            // Register VFX with pool
            RegisterVFXWithPool();
        }
        
        private void Start()
        {
            // Draw attack range visualization
            UpdateRangeVisualizer();
        }
        
        /// <summary>
        /// Register this shooter's VFX with the pool system
        /// </summary>
        private void RegisterVFXWithPool()
        {
            if (VFXPool.Instance == null || _myUnit == null) return;
            
            int unitId = _myUnit.getId();
            if (unitId <= 0) return;
            
            // Register the bullet prefab
            if (Bullet != null)
            {
                VFXPool.Instance.RegisterUnitProjectile(unitId, Bullet);
                
                // If the bullet has impact effect, register that too
                Projectile projectile = Bullet.GetComponent<Projectile>();
                if (projectile != null && projectile.impact != null)
                {
                    VFXPool.Instance.RegisterUnitArmorImpact(unitId, projectile.impact);
                }
            }
            
            // Register impact VFX if provided
            if (ShieldImpactEffect != null)
            {
                VFXPool.Instance.RegisterUnitShieldImpact(unitId, ShieldImpactEffect);
            }
            
            if (ArmorImpactEffect != null)
            {
                VFXPool.Instance.RegisterUnitArmorImpact(unitId, ArmorImpactEffect);
            }
        }
        
        private void Update()
        {
            // Handle scale changes
            if (_lastScale != transform.lossyScale)
            {
                _lastScale = transform.lossyScale;
                EnemyDetector.radius = GetScaledDetectionRange();
                UpdateRangeVisualizer();
            }
            
            // Exit if dead or cannot attack
            if (!IsOperational())
            {
                ResetTargeting();
                return;
            }
            
            // Process targeting and attack flow
            ProcessTargeting();
        }
        
        private void FixedUpdate()
        {
            // Update cooldown timer - keep this in FixedUpdate for consistent timing
            if (_attackCooldownTimer > 0)
            {
                _attackCooldownTimer -= Time.fixedDeltaTime;
            }
        }
        
        #endregion
        
        #region Targeting Logic
        
        private void ProcessTargeting()
        {
            // Clear invalid targets from potential targets (dead, null, etc.)
            CleanupInvalidTargets();
            
            // No targets in detection range? Reset and bail out
            if (_potentialTargets.Count == 0)
            {
                ChangeState(ShooterState.Idle);
                return;
            }
            
            // Update target selection at fixed intervals to avoid doing it every frame
            _targetRecalcTimer -= Time.deltaTime;
            if (_currentTarget == null || _targetRecalcTimer <= 0)
            {
                // Time to find a new best target
                _targetRecalcTimer = TargetUpdateInterval;
                SelectBestTarget();
            }
            
            // If we have a target, handle pursuit and attack
            if (_currentTarget != null)
            {
                // Calculate distance to target
                float distToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
                float scaledAttackRange = GetScaledAttackRange();
                
                if (distToTarget <= scaledAttackRange)
                {
                    // Target is in attack range - attack!
                    ChangeState(ShooterState.Attacking);
                    
                    // Only rotate towards target when actually attacking
                    if (RotateToEnemy)
                    {
                        RotateTowardsTarget();
                    }
                    
                    // Try to attack (cooldown handled internally)
                    TryAttack();
                }
                else if (distToTarget <= GetScaledDetectionRange())
                {
                    // Target is out of attack range but within detection range - pursue
                    ChangeState(ShooterState.Pursuing);
                    
                    // Only rotate towards target when actively pursuing
                    if (RotateToEnemy)
                    {
                        RotateTowardsTarget();
                    }
                    
                    // Move towards target if we can
                    if (_myShip != null && StopToAttack)
                    {
                        _myShip.SetDestination(_currentTarget.transform.position, scaledAttackRange * 0.9f);
                    }
                }
                else
                {
                    // Target is outside detection range - should no longer be valid
                    _currentTarget = null;
                    ChangeState(ShooterState.Idle);
                }
            }
            else
            {
                // No valid target found despite having potentials - go idle
                ChangeState(ShooterState.Idle);
            }
        }
        
        private void SelectBestTarget()
        {
            if (_potentialTargets.Count == 0)
            {
                _currentTarget = null;
                return;
            }
            
            // Fast path for single target
            if (_potentialTargets.Count == 1)
            {
                foreach (var target in _potentialTargets)
                {
                    // Check if target is still within detection range
                    float distToTarget = Vector3.Distance(transform.position, target.transform.position);
                    if (distToTarget <= GetScaledDetectionRange())
                    {
                        _currentTarget = target;
                    }
                    else
                    {
                        _currentTarget = null;
                        _potentialTargets.Remove(target);
                    }
                    return;
                }
            }
            
            // Clear and populate our working list to avoid allocations
            _validTargetsCache.Clear();
            foreach (var target in _potentialTargets)
            {
                if (target != null && !target.GetIsDeath() && _myUnit.IsEnemy(target))
                {
                    // Check if target is still within detection range
                    float distToTarget = Vector3.Distance(transform.position, target.transform.position);
                    if (distToTarget <= GetScaledDetectionRange())
                    {
                        _validTargetsCache.Add(target);
                    }
                }
            }
            
            // No valid targets? Clear current target
            if (_validTargetsCache.Count == 0)
            {
                _currentTarget = null;
                return;
            }
            
            // Find closest valid target
            Unit bestTarget = null;
            float bestDistanceSqr = float.MaxValue;
            Vector3 myPos = transform.position;
            
            foreach (var target in _validTargetsCache)
            {
                float distSqr = (target.transform.position - myPos).sqrMagnitude;
                if (distSqr < bestDistanceSqr)
                {
                    bestTarget = target;
                    bestDistanceSqr = distSqr;
                }
            }
            
            _currentTarget = bestTarget;
        }
        
        private void CleanupInvalidTargets()
        {
            // Temporary list to avoid modifying collection during enumeration
            _validTargetsCache.Clear();
            
            foreach (var target in _potentialTargets)
            {
                if (target == null || target.GetIsDeath() || !_myUnit.IsEnemy(target))
                {
                    _validTargetsCache.Add(target); // Mark for removal
                }
            }
            
            // Remove invalid targets
            foreach (var invalidTarget in _validTargetsCache)
            {
                _potentialTargets.Remove(invalidTarget);
            }
            
            // If current target is now invalid, clear it
            if (_currentTarget != null && 
                (_currentTarget.GetIsDeath() || !_myUnit.IsEnemy(_currentTarget)))
            {
                _currentTarget = null;
            }
        }
        
        private void RotateTowardsTarget()
        {
            if (_currentTarget == null) return;
            
            Vector3 direction = _currentTarget.transform.position - transform.position;
            direction.y = 0; // Keep rotations on horizontal plane
            
            if (direction.sqrMagnitude < 0.001f) return;
            
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                Time.deltaTime * rotationSpeed
            );
        }
        
        private void ChangeState(ShooterState newState)
        {
            // Only process state change if it's different
            if (newState == _currentState) return;
            
            // Handle exit from old state
            switch (_currentState)
            {
                case ShooterState.Pursuing:
                    // If we were pursuing and going to idle, reset ship destination
                    if (newState == ShooterState.Idle && _myShip != null && StopToAttack)
                    {
                        _myShip.ResetDestination();
                    }
                    break;
            }
            
            // Set new state
            _currentState = newState;
            
            // Handle enter to new state
            switch (_currentState)
            {
                case ShooterState.Idle:
                    // If becoming idle, ensure ship resets
                    if (_myShip != null && StopToAttack)
                    {
                        _myShip.ResetDestination();
                    }
                    break;
            }
        }
        
        private void ResetTargeting()
        {
            _currentTarget = null;
            _currentState = ShooterState.Idle;
            
            // Stop moving if following a target
            if (_myShip != null && StopToAttack)
            {
                _myShip.ResetDestination();
            }
        }
        
        #endregion
        
        #region Attack Logic
        
        private void TryAttack()
        {
            // Validate target and ready state
            if (_currentTarget == null || _attackCooldownTimer > 0f) return;
            
            // Double-check attack range
            float distToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
            if (distToTarget > GetScaledAttackRange()) return;
            
            // Fire projectiles
            FireProjectiles();
            
            // Trigger attack animation
            _myUnit?.GetAnimator()?.SetTrigger("Attack");
            
            // Reset cooldown
            _attackCooldownTimer = CoolDown;
        }
        
        private void FireProjectiles()
        {
            if (_currentTarget == null) return;
            
            foreach (var cannon in Cannons)
            {
                if (cannon == null) continue;
                
                // Get bullet from pool or instantiate
                GameObject bulletObj = GetBulletFromPool(cannon);
                if (bulletObj == null) continue;
                
                Projectile bullet = bulletObj.GetComponent<Projectile>();
                if (bullet == null)
                {
                    RecycleBullet(bulletObj);
                    continue;
                }
                
                // Setup bullet properties
                SetupBullet(bullet, cannon);
                
                // Play muzzle flash
                PlayMuzzleFlash(cannon);
            }
        }
        
        private GameObject GetBulletFromPool(Transform cannonTransform)
        {
            GameObject bulletObj = null;
            
            // Get from VFXPool if available and unit has an ID
            if (VFXPool.Instance != null && _myUnit != null && _myUnit.getId() > 0)
            {
                // Try to get unit-specific projectile
                bulletObj = VFXPool.Instance.GetUnitProjectile(_myUnit.getId());
                
                // Fallback to specified bullet if unit-specific wasn't found
                if (bulletObj == null && Bullet != null)
                {
                    bulletObj = VFXPool.Instance.GetProjectile(Bullet);
                }
                
                if (bulletObj != null)
                {
                    bulletObj.transform.position = cannonTransform.position;
                    bulletObj.transform.rotation = cannonTransform.rotation;
                    bulletObj.SetActive(true);
                }
            }
            // Direct instantiation fallback
            else if (Bullet != null)
            {
                bulletObj = Instantiate(Bullet, cannonTransform.position, cannonTransform.rotation);
            }
            
            return bulletObj;
        }
        
        private void SetupBullet(Projectile bullet, Transform cannon)
        {
            // Set faction
            bullet.MyFaction = _myUnit.MyFaction;
            bullet.PrefabReference = Bullet;
            
            // Set impact effects if available
            if (ShieldImpactEffect != null)
            {
                bullet.shieldImpactEffect = ShieldImpactEffect;
            }
            
            if (ArmorImpactEffect != null)
            {
                bullet.armorImpactEffect = ArmorImpactEffect;
            }
            
            // Set target - verify it's still valid
            if (_currentTarget != null && _currentTarget.gameObject != null && !_currentTarget.GetIsDeath())
            {
                bullet.SetTarget(_currentTarget.gameObject);
                
                // Calculate damage (with critical chance)
                bullet.Dmg = Random.value < criticalStrikeChance ? 
                    (int)(BulletDamage * criticalStrikeMultiplier) : BulletDamage;
                    
                bullet.Speed = BulletSpeed;
            }
            else
            {
                // Target became invalid, recycle bullet
                RecycleBullet(bullet.gameObject);
            }
        }
        
        private void RecycleBullet(GameObject bulletObj)
        {
            if (VFXPool.Instance != null)
            {
                VFXPool.Instance.ReturnProjectile(bulletObj, Bullet);
            }
            else
            {
                Destroy(bulletObj);
            }
        }
        
        private void PlayMuzzleFlash(Transform cannon)
        {
            // Find the index of this cannon
            for (int i = 0; i < Cannons.Length; i++)
            {
                if (Cannons[i] == cannon && i < _muzzleFlashEffects.Length && _muzzleFlashEffects[i] != null)
                {
                    _muzzleFlashEffects[i].Play();
                    break;
                }
            }
        }
        
        #endregion
        
        #region Range Visualization
        
        private void SetupRangeVisualizer()
        {
            // Find or create line renderer
            if (_rangeLineRenderer == null)
            {
                _rangeLineRenderer = GetComponentInChildren<LineRenderer>();
                
                if (_rangeLineRenderer == null)
                {
                    // Create new GameObject for visualization
                    GameObject lineObj = new GameObject("RangeVisualizer");
                    lineObj.transform.SetParent(transform);
                    lineObj.transform.localPosition = Vector3.zero;
                    lineObj.transform.localRotation = Quaternion.identity;
                    lineObj.transform.localScale = Vector3.one;
                    _rangeLineRenderer = lineObj.AddComponent<LineRenderer>();
                }
            }
            
            // Configure renderer
            _rangeLineRenderer.useWorldSpace = false;
            
            // Setup material if needed
            if (_rangeLineRenderer.sharedMaterial == null || 
                _rangeLineRenderer.sharedMaterial.shader == null || 
                _rangeLineRenderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                
                if (shader != null)
                {
                    _rangeLineRenderer.material = new Material(shader);
                }
                else
                {
                    Debug.LogError("Could not find a suitable shader for LineRenderer visualization");
                }
            }
            
            _rangeLineRenderer.alignment = LineAlignment.TransformZ;
        }
        
        public void UpdateRangeVisualizer()
        {
            if (_rangeLineRenderer == null)
            {
                SetupRangeVisualizer();
                if (_rangeLineRenderer == null) return;
            }
            
            // Update visibility based on visualization type
            _rangeLineRenderer.enabled = (rangeVisualizationType != RangeVisualizationType.None);
            if (!_rangeLineRenderer.enabled) return;
            
            // Configure appearance
            _rangeLineRenderer.startWidth = rangeLineWidth;
            _rangeLineRenderer.endWidth = rangeLineWidth;
            _rangeLineRenderer.startColor = rangeColor;
            _rangeLineRenderer.endColor = rangeColor;
            
            // Draw appropriate visualization
            switch (rangeVisualizationType)
            {
                case RangeVisualizationType.Circle:
                    DrawRangeCircle();
                    break;
                case RangeVisualizationType.Line:
                    DrawDirectionalLine();
                    break;
            }
        }
        
        private void DrawRangeCircle()
        {
            int segments = lineSegments;
            _rangeLineRenderer.positionCount = segments + 1;
            _rangeLineRenderer.loop = true;
            
            float angleStep = 360f / segments;
            Vector3[] points = new Vector3[segments + 1];
            
            // Create circle in local space
            for (int i = 0; i <= segments; i++)
            {
                float angleRad = i * angleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));
                points[i] = dir * AttackRange;
            }
            
            _rangeLineRenderer.SetPositions(points);
        }
        
        private void DrawDirectionalLine()
        {
            _rangeLineRenderer.positionCount = 2;
            _rangeLineRenderer.loop = false;
            
            Vector3[] points = new Vector3[2];
            points[0] = Vector3.zero;
            points[1] = Vector3.forward * AttackRange;
            
            _rangeLineRenderer.SetPositions(points);
        }
        
        #endregion
        
        #region Utility Methods
        
        // Check if shooter can operate
        private bool IsOperational()
        {
            return !_myUnit.GetIsDeath() && CanAttack && _myUnit.InControl();
        }
        
        // Get detection range accounting for scale
        public float GetScaledDetectionRange()
        {
            float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            return DetectionRange * maxScale;
        }
        
        // Get attack range accounting for scale
        public float GetScaledAttackRange()
        {
            float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            return AttackRange * maxScale;
        }
        
        // Setup muzzle flash references
        private void SetupMuzzleFlashEffects()
        {
            _muzzleFlashEffects = new ParticleSystem[Cannons.Length];
            
            for (int i = 0; i < Cannons.Length; i++)
            {
                Transform cannon = Cannons[i];
                if (cannon != null && cannon.childCount > 0)
                {
                    _muzzleFlashEffects[i] = cannon.GetChild(0).GetComponent<ParticleSystem>();
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        // Add enemy to potential targets
        public void AddEnemy(Unit enemy)
        {
            if (enemy == null || enemy.GetIsDeath() || !_myUnit.IsEnemy(enemy)) return;
            
            _potentialTargets.Add(enemy);
            
            // New enemy added - force target recalculation on next update
            _targetRecalcTimer = 0f;
        }
        
        // Remove enemy from potential targets
        public void RemoveEnemy(Unit enemy)
        {
            if (enemy == null) return;
            
            bool wasRemoved = _potentialTargets.Remove(enemy);
            
            // If we removed our current target, force recalculation
            if (wasRemoved && enemy == _currentTarget)
            {
                _currentTarget = null;
                _targetRecalcTimer = 0f;
            }
        }
        
        // Completely stop attacking
        public void StopAttack()
        {
            CanAttack = false;
            _potentialTargets.Clear();
            _currentTarget = null;
            ChangeState(ShooterState.Idle);
        }
        
        // Get current target ID (for networking/serialization)
        public int GetIdTarget()
        {
            return _currentTarget == null ? 0 : _currentTarget.getId();
        }
        
        // Initialize stats from NFT data
        public virtual void InitStatsFromNFT(NFTsUnit nFTsUnit)
        {
            if (nFTsUnit == null) return;
            
            BulletDamage = nFTsUnit.Damage;
            
            // Store previous values for comparison
            float previousAttackRange = AttackRange;
            float previousDetectionRange = DetectionRange;
            
            // Apply new values
            AttackRange = nFTsUnit.AttackRange;
            DetectionRange = nFTsUnit.DetectionRange;
            
            // Update visualization if ranges changed
            if (Mathf.Abs(previousAttackRange - AttackRange) > 0.01f || 
                Mathf.Abs(previousDetectionRange - DetectionRange) > 0.01f)
            {
                // Update collider
                if (EnemyDetector != null)
                {
                    EnemyDetector.radius = GetScaledDetectionRange();
                }
                
                // Update visualization
                UpdateRangeVisualizer();
            }
        }
        
        // Reset shooter to initial state
        public void ResetShooter()
        {
            // Reset timers
            _attackCooldownTimer = 0f;
            _targetRecalcTimer = 0f;
            
            // Reset state
            CanAttack = true;
            _currentTarget = null;
            _potentialTargets.Clear();
            ChangeState(ShooterState.Idle);
            
            // Reset visualization
            UpdateRangeVisualizer();
            
            // Reset detector
            if (EnemyDetector != null)
            {
                EnemyDetector.radius = GetScaledDetectionRange();
            }
            
            // Make sure VFX are registered with the pool
            RegisterVFXWithPool();
        }
        
        #endregion
        
        #region Backwards Compatibility Methods
        
        // Methods to maintain backward compatibility with existing code
        
        /// <summary>
        /// Legacy method to check if shooter is engaging a target
        /// </summary>
        public bool IsEngagingTarget()
        {
            return _currentState == ShooterState.Pursuing || _currentState == ShooterState.Attacking;
        }
        
        /// <summary>
        /// Legacy method - renamed to GetScaledDetectionRange
        /// </summary>
        public float GetWorldDetectionRange()
        {
            return GetScaledDetectionRange();
        }
        
        /// <summary>
        /// Legacy method - renamed to GetScaledAttackRange
        /// </summary>
        public float GetWorldAttackRange()
        {
            return GetScaledAttackRange();
        }
        
        /// <summary>
        /// Legacy method to manually set target
        /// </summary>
        public void SetTarget(Unit target)
        {
            // Check for null or friendly targets
            if (target == null || _myUnit.IsAlly(target))
            {
                // If trying to set current target to null/friendly, clear it
                if (_currentTarget == target) 
                {
                    _currentTarget = null;
                }
                return;
            }
            
            // Only update if different and valid target
            if (_currentTarget != target && !target.GetIsDeath() && _myUnit.IsEnemy(target))
            {
                _currentTarget = target;
                
                // Force immediate target evaluation
                _targetRecalcTimer = 0f;
                
                // Add to potential targets if not already there
                _potentialTargets.Add(target);
                
                // Update state if needed
                if (_currentState == ShooterState.Idle)
                {
                    // Check range to determine state
                    float distance = Vector3.Distance(transform.position, target.transform.position);
                    
                    if (distance <= GetScaledAttackRange())
                    {
                        ChangeState(ShooterState.Attacking);
                    }
                    else
                    {
                        ChangeState(ShooterState.Pursuing);
                    }
                }
            }
        }
        
        /// <summary>
        /// Returns true if the shooter is actually firing at this moment
        /// (useful for triggering attack animations)
        /// </summary>
        public bool IsShooting()
        {
            return _currentState == ShooterState.Attacking && 
                   _attackCooldownTimer <= 0.05f && 
                   _currentTarget != null &&
                   !_currentTarget.GetIsDeath() && 
                   Vector3.Distance(transform.position, _currentTarget.transform.position) <= GetScaledAttackRange();
        }
        
        /// <summary>
        /// Returns the normalized cooldown progress (0 = just fired, 1 = ready to fire)
        /// Useful for animation timing
        /// </summary>
        public float GetAttackCooldownProgress()
        {
            if (CoolDown <= 0) return 1f; // Avoid division by zero
            return 1f - Mathf.Clamp01(_attackCooldownTimer / CoolDown);
        }
        
        #endregion
    }
}
