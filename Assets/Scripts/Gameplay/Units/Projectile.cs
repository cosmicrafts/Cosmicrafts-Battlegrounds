namespace Cosmicrafts
{
    using UnityEngine;

    public class Projectile : MonoBehaviour
    {
        [HideInInspector]
        public Faction MyFaction = Faction.Player;
        
        // Reference to the original prefab for pool return
        [HideInInspector]
        public GameObject PrefabReference;

        // Target tracking
        private GameObject _target;
        private Vector3 _lastTargetPosition;
        private bool _isReturningToPool = false;

        // Movement parameters
        [HideInInspector]
        public float Speed = 10f;
        [HideInInspector]
        public int Dmg = 1;

        // Visual effects
        public GameObject impact;
        
        // Explosion effect
        [HideInInspector]
        public GameObject explosionEffect;

        // Projectile behavior
        public enum TrajectoryType
        {
            Straight,
            Wavering,
            Zigzag,
            Circular
        }

        // Selected trajectory type
        public TrajectoryType trajectoryType = TrajectoryType.Straight;

        // Fields for Wavering Movement
        public float WaveringAmplitude = 0.5f;
        public float WaveringFrequency = 2f;

        // Fields for Zigzag Movement
        public float ZigzagAmplitude = 0.5f;
        public float ZigzagFrequency = 2f;

        // Fields for Circular Movement
        public float CircularRadius = 1f;
        public float CircularSpeed = 2f;

        // Movement tracking
        private Vector3 _initialPosition;
        private float _timeSinceStart;
        private float _timeAlive = 0f;

        // Area of Effect settings
        public bool IsAoE = false;
        public float AoERadius = 5f;

        // Lifecycle settings
        public float maxLifespan = 2f;

        private void OnEnable()
        {
            // Reset state when enabled from pool
            _timeAlive = 0f;
            _timeSinceStart = 0f;
            _isReturningToPool = false;
            
            // Initialize position reference
            _initialPosition = transform.position;
        }
        
        /// <summary>
        /// Resets the projectile state for reuse from pool
        /// </summary>
        public void ResetProjectile()
        {
            _timeAlive = 0f;
            _timeSinceStart = 0f;
            _isReturningToPool = false;
            _target = null;
            
            // Any other state initialization
        }

        private void FixedUpdate()
        {
            if (_isReturningToPool) return;
            
            _timeAlive += Time.fixedDeltaTime;
            _timeSinceStart += Time.fixedDeltaTime;

            // Check for maximum lifespan
            if (_timeAlive >= maxLifespan)
            {
                HandleImpact(null);
                return;
            }

            // Check if the target is valid
            if (_target == null || (_target != null && _target.GetComponent<Unit>() != null && _target.GetComponent<Unit>().IsDeath))
            {
                _target = null; // Clear invalid target
            }

            // Move based on target state
            if (_target == null)
            {
                MoveToLastPositionOrDestroy();
            }
            else
            {
                MoveProjectile();
            }
        }

        private void MoveProjectile()
        {
            switch (trajectoryType)
            {
                case TrajectoryType.Straight:
                    MoveStraight();
                    break;
                case TrajectoryType.Wavering:
                    MoveWavering();
                    break;
                case TrajectoryType.Zigzag:
                    MoveZigzag();
                    break;
                case TrajectoryType.Circular:
                    MoveCircular();
                    break;
            }
        }

        private void MoveStraight()
        {
            if (_target != null)
            {
                _lastTargetPosition = _target.transform.position;
                RotateTowards(_lastTargetPosition);
            }

            transform.position = Vector3.MoveTowards(transform.position, _lastTargetPosition, Speed * Time.fixedDeltaTime);

            // Check if reached target position
            if (Vector3.Distance(transform.position, _lastTargetPosition) <= 0.1f)
            {
                HandleImpact(null);
            }
        }

        private void MoveWavering()
        {
            if (_target != null)
            {
                _lastTargetPosition = _target.transform.position;
                RotateTowards(_lastTargetPosition);
            }

            Vector3 forwardMove = transform.forward * Speed * Time.fixedDeltaTime;
            Vector3 waveringOffset = transform.right * Mathf.Sin(_timeSinceStart * WaveringFrequency) * WaveringAmplitude;
            transform.position += forwardMove + waveringOffset;

            // Check if reached target position
            if (Vector3.Distance(transform.position, _lastTargetPosition) <= 0.1f)
            {
                HandleImpact(null);
            }
        }

        private void MoveZigzag()
        {
            if (_target != null)
            {
                _lastTargetPosition = _target.transform.position;
                RotateTowards(_lastTargetPosition);
            }

            Vector3 forwardMove = transform.forward * Speed * Time.fixedDeltaTime;
            Vector3 zigzagOffset = transform.right * Mathf.Sign(Mathf.Sin(_timeSinceStart * ZigzagFrequency)) * ZigzagAmplitude;
            transform.position += forwardMove + zigzagOffset;

            // Check if reached target position
            if (Vector3.Distance(transform.position, _lastTargetPosition) <= 0.1f)
            {
                HandleImpact(null);
            }
        }

        private void MoveCircular()
        {
            if (_target != null)
            {
                _lastTargetPosition = _target.transform.position;
                RotateTowards(_lastTargetPosition);
            }

            Vector3 offset = new Vector3(
                Mathf.Sin(_timeSinceStart * CircularSpeed), 
                0f, 
                Mathf.Cos(_timeSinceStart * CircularSpeed)
            ) * CircularRadius;
            
            transform.position = _initialPosition + offset;
            transform.position = Vector3.MoveTowards(transform.position, _lastTargetPosition, Speed * Time.fixedDeltaTime);

            // Check if reached target position
            if (Vector3.Distance(transform.position, _lastTargetPosition) <= 0.1f)
            {
                HandleImpact(null);
            }
        }

        private void MoveToLastPositionOrDestroy()
        {
            // Safety check for destroyed/disabled objects
            if (this == null || !gameObject || !gameObject.activeInHierarchy || _isReturningToPool)
            {
                return;
            }
            
            transform.position = Vector3.MoveTowards(transform.position, _lastTargetPosition, Speed * Time.fixedDeltaTime);

            // Check if reached last known position
            if (Vector3.Distance(transform.position, _lastTargetPosition) <= 0.1f)
            {
                HandleImpact(null);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Prevent handling if already returning to pool
            if (_isReturningToPool) return;

            if (other.gameObject == _target)
            {
                HandleImpact(_target.GetComponent<Unit>());
            }
            else if (other.CompareTag("Unit"))
            {
                Unit target = other.gameObject.GetComponent<Unit>();
                if (target != null && FactionManager.GetRelationship(this.MyFaction, target.MyFaction) == FactionRelationship.Hostile)
                {
                    HandleImpact(target);
                }
            }
            else if (other.CompareTag("Out"))
            {
                HandleImpact(null);
            }
        }

        void HandleImpact(Unit target)
        {
            // Prevent multiple impact handling
            if (_isReturningToPool) return;
            _isReturningToPool = true;

            try
            {
                // Apply AoE damage if enabled
                if (IsAoE)
                {
                    ApplyAoEDamage();
                }

                // Create impact effect using the pool
                PlayImpactEffect();

                // Apply damage to direct target if valid
                if (target != null && target.gameObject != null && target.gameObject.activeInHierarchy && !target.IsDeath)
                {
                    ApplyDirectDamage(target);
                    target.SetImpactPosition(transform.position);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in HandleImpact: {e.Message}");
            }
            finally
            {
                // Return projectile to pool instead of destroying it
                ReturnToPool();
            }
        }

        void ApplyDirectDamage(Unit target)
        {
            // Validate target is still active and alive
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy || target.IsDeath)
            {
                return;
            }

            // Apply damage with dodge chance
            if (Random.value < target.DodgeChance)
            {
                // Dodge - no damage
                return;
            }

            // Apply shield damage if available
            if (target.Shield > 0 && !target.flagShield && target.gameObject.activeInHierarchy)
            {
                target.OnImpactShield(Dmg);
            }
            else if (target.gameObject.activeInHierarchy)
            {
                // Apply direct damage
                target.AddDmg(Dmg);
            }
        }

        void ApplyAoEDamage()
        {
            // Find all colliders in the AoE radius
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, AoERadius);
            
            foreach (Collider hitCollider in hitColliders)
            {
                // Skip null or inactive colliders
                if (hitCollider == null || !hitCollider.gameObject || !hitCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Check if it's a unit and apply damage if it's an enemy
                Unit unit = hitCollider.GetComponent<Unit>();
                if (unit != null && unit.gameObject.activeInHierarchy && !unit.IsDeath && unit.MyFaction != MyFaction)
                {
                    ApplyDirectDamage(unit);
                }
            }
            
            // Create an explosion effect
            if (VFXPool.Instance != null)
            {
                VFXPool.Instance.PlayExplosion(transform.position, transform.localScale.x);
            }
        }

        void PlayImpactEffect()
        {
            // Use the VFXPool to create and manage impact effects
            if (VFXPool.Instance != null && impact != null)
            {
                VFXPool.Instance.PlayImpact(impact, transform.position, transform.rotation, 1f);
            }
        }

        void RotateTowards(Vector3 target)
        {
            Vector3 direction = (target - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        public void SetLastPosition(Vector3 lastPosition)
        {
            _lastTargetPosition = lastPosition;
        }

        public void SetTarget(GameObject target)
        {
            // Unsubscribe from previous target if exists
            if (_target != null)
            {
                Unit previousTarget = _target.GetComponent<Unit>();
                if (previousTarget != null)
                {
                    previousTarget.OnDeath -= HandleTargetDeath;
                }
            }
            
            _target = target;
            
            // If no target, just return
            if (target == null)
            {
                return;
            }
            
            // Track target position and listen for death
            _lastTargetPosition = target.transform.position;
            
            Unit targetUnit = target.GetComponent<Unit>();
            if (targetUnit != null)
            {
                targetUnit.OnDeath += HandleTargetDeath;
            }
        }

        private void HandleTargetDeath(Unit unit)
        {
            // Check if this projectile is still valid
            if (this == null || !gameObject || !gameObject.activeInHierarchy || _isReturningToPool)
            {
                return;
            }
            
            // Store the last position and clear target
            Vector3 lastPosition = unit != null && unit.gameObject != null 
                ? unit.transform.position 
                : _lastTargetPosition;
                
            _target = null;
            _lastTargetPosition = lastPosition;
            
            // Continue moving toward the last position
            MoveToLastPositionOrDestroy();
        }

        private void OnDisable()
        {
            // Ensure we unsubscribe from any death events when disabled
            if (_target != null)
            {
                Unit targetUnit = _target.GetComponent<Unit>();
                if (targetUnit != null)
                {
                    targetUnit.OnDeath -= HandleTargetDeath;
                }
            }
        }

        private void ReturnToPool()
        {
            // Safety check to prevent double returns
            if (this == null || !gameObject || !gameObject.activeInHierarchy)
            {
                return;
            }

            // Clear target references
            if (_target != null)
            {
                Unit targetUnit = _target.GetComponent<Unit>();
                if (targetUnit != null)
                {
                    targetUnit.OnDeath -= HandleTargetDeath;
                }
                _target = null;
            }

            // Return to pool if available
            if (VFXPool.Instance != null && PrefabReference != null)
            {
                VFXPool.Instance.ReturnProjectile(gameObject, PrefabReference);
            }
            else
            {
                // Fallback if pool doesn't exist
                Destroy(gameObject);
            }
        }
    }
}
