namespace Cosmicrafts
{
    using UnityEngine;

    public class Projectile : MonoBehaviour
    {
        [HideInInspector]
        public Faction MyFaction = Faction.Player;
        
        // Keep for backwards compatibility
        [System.Obsolete("Use MyFaction instead")]
        public Team MyTeam
        {
            get { return MyFaction == Faction.Player ? Team.Blue : Team.Red; }
            set { MyFaction = value == Team.Blue ? Faction.Player : Faction.Enemy; }
        }

        GameObject Target;
        [HideInInspector]
        public float Speed;
        [HideInInspector]
        public int Dmg;

        public GameObject canvasDamageRef;
        public GameObject impact;
        Vector3 LastTargetPosition;

        // Enum for different trajectory types
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
        public float WaveringAmplitude = 0.5f; // How far it moves from the center
        public float WaveringFrequency = 2f; // How fast it oscillates

        // Fields for Zigzag Movement
        public float ZigzagAmplitude = 0.5f; // Amplitude of the zigzag
        public float ZigzagFrequency = 2f; // Frequency of the zigzag

        // Fields for Circular Movement
        public float CircularRadius = 1f; // Radius of the circular path
        public float CircularSpeed = 2f; // Speed of circular movement

        private Vector3 initialPosition;
        private float timeSinceStart;

        public bool IsAoE = false;  // Checkmark in Inspector
        public float AoERadius = 5f;  // Radius of AoE damage

        // Make maxLifespan public to customize in Inspector
        public float maxLifespan = 1f; // Maximum life of projectile

        private float timeAlive = 0f;

        // Add these fields at the top of the class after the existing declarations
        private bool isDestroyed = false;
        public bool HitEffectsEnabled = true;

        private void Start()
        {
            initialPosition = transform.position;
        }

        private void FixedUpdate()
        {
            timeAlive += Time.fixedDeltaTime;
            timeSinceStart += Time.fixedDeltaTime;

            // Check if the projectile has exceeded its maximum lifespan
            if (timeAlive >= maxLifespan)
            {
                HandleImpact(null); // Impact at the last known target position if lifespan exceeded
                return;
            }

            // Check if the target is destroyed or null
            if (Target == null || (Target != null && Target.GetComponent<Unit>() != null && Target.GetComponent<Unit>().IsDeath))
            {
                Target = null; // Target destroyed or null, continue to last known position
            }

            if (Target == null)
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
            if (Target != null)
            {
                LastTargetPosition = Target.transform.position;
                RotateTowards(LastTargetPosition);
            }

            transform.position = Vector3.MoveTowards(transform.position, LastTargetPosition, Speed * Time.fixedDeltaTime);

            // Check if the projectile has reached the last known position of the target
            if (Vector3.Distance(transform.position, LastTargetPosition) <= 0.1f)
            {
                HandleImpact(null); // Impact at the last known position
            }
        }

        private void MoveWavering()
        {
            if (Target != null)
            {
                LastTargetPosition = Target.transform.position;
                RotateTowards(LastTargetPosition);
            }

            Vector3 forwardMove = transform.forward * Speed * Time.fixedDeltaTime;
            Vector3 waveringOffset = transform.right * Mathf.Sin(timeSinceStart * WaveringFrequency) * WaveringAmplitude;
            transform.position += forwardMove + waveringOffset;

            // Check if the projectile has reached the last known position of the target
            if (Vector3.Distance(transform.position, LastTargetPosition) <= 0.1f)
            {
                HandleImpact(null); // Impact at the last known position
            }
        }

        private void MoveZigzag()
        {
            if (Target != null)
            {
                LastTargetPosition = Target.transform.position;
                RotateTowards(LastTargetPosition);
            }

            Vector3 forwardMove = transform.forward * Speed * Time.fixedDeltaTime;
            Vector3 zigzagOffset = transform.right * Mathf.Sign(Mathf.Sin(timeSinceStart * ZigzagFrequency)) * ZigzagAmplitude;
            transform.position += forwardMove + zigzagOffset;

            // Check if the projectile has reached the last known position of the target
            if (Vector3.Distance(transform.position, LastTargetPosition) <= 0.1f)
            {
                HandleImpact(null); // Impact at the last known position
            }
        }

        private void MoveCircular()
        {
            if (Target != null)
            {
                LastTargetPosition = Target.transform.position;
                RotateTowards(LastTargetPosition);
            }

            Vector3 offset = new Vector3(Mathf.Sin(timeSinceStart * CircularSpeed), 0f, Mathf.Cos(timeSinceStart * CircularSpeed)) * CircularRadius;
            transform.position = initialPosition + offset;
            transform.position = Vector3.MoveTowards(transform.position, LastTargetPosition, Speed * Time.fixedDeltaTime);

            // Check if the projectile has reached the last known position of the target
            if (Vector3.Distance(transform.position, LastTargetPosition) <= 0.1f)
            {
                HandleImpact(null); // Impact at the last known position
            }
        }


        private void MoveToLastPositionOrDestroy()
        {
            // Check if this projectile is still valid and has not been destroyed
            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            {
                // Projectile has been destroyed or is inactive, don't proceed
                return;
            }
            
            try
            {
                transform.position = Vector3.MoveTowards(transform.position, LastTargetPosition, Speed * Time.fixedDeltaTime);

                if (Vector3.Distance(transform.position, LastTargetPosition) <= 0.1f)
                {
                    HandleImpact(null); // Impact at the last known target position
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in MoveToLastPositionOrDestroy: {e.Message}");
                // If we can't safely move the projectile, destroy it
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == Target)
            {
                HandleImpact(Target.GetComponent<Unit>());
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
                HandleImpact(null); // Apply AoE even if it's out of bounds
            }
        }

        void HandleImpact(Unit target)
        {
            try
            {
                // Apply AoE if applicable, even if there's no target
                if (IsAoE)
                {
                    ApplyAoEDamage();
                }

                // Impact at the last known target position or collision point
                InstantiateImpactEffect();

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
                // Always destroy the projectile
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }

        void ApplyDirectDamage(Unit target)
        {
            // First check if the target exists and is active
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy || target.IsDeath)
            {
                return;
            }

            try
            {
                if (Random.value < target.DodgeChance)
                {
                    Dmg = 0;
                }

                // Additional safety check right before calling OnImpactShield
                if (target.Shield > 0 && !target.flagShield && target.gameObject.activeInHierarchy)
                {
                    // Final safety check via reflection to avoid direct method call that might crash
                    try
                    {
                        target.OnImpactShield(Dmg);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"Failed to call OnImpactShield: {ex.Message}");
                        // Fallback - just apply damage directly
                        if (target != null && target.gameObject != null && target.gameObject.activeInHierarchy)
                        {
                            target.AddDmg(Dmg);
                        }
                    }
                }
                else if (target.gameObject.activeInHierarchy)
                {
                    InstantiateImpactEffect();
                    // Only add damage if target is still active
                    if (target.gameObject.activeInHierarchy)
                    {
                        target.AddDmg(Dmg);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in ApplyDirectDamage: {e.Message}");
            }
        }

        void ApplyAoEDamage()
        {
            try
            {
                Collider[] hitColliders = Physics.OverlapSphere(transform.position, AoERadius);
                foreach (Collider hitCollider in hitColliders)
                {
                    // Skip null or inactive colliders
                    if (hitCollider == null || !hitCollider.gameObject || !hitCollider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Unit unit = hitCollider.GetComponent<Unit>();
                    if (unit != null && unit.gameObject.activeInHierarchy && !unit.IsDeath && unit.MyFaction != MyFaction)
                    {
                        ApplyDirectDamage(unit);
                    }
                }
                InstantiateImpactEffect();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in ApplyAoEDamage: {e.Message}");
            }
        }

        void InstantiateImpactEffect()
        {
            try
            {
                if (impact != null)
                {
                    GameObject impactPrefab = Instantiate(impact, transform.position, Quaternion.identity);
                    Destroy(impactPrefab, 0.25f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in InstantiateImpactEffect: {e.Message}");
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
            LastTargetPosition = lastPosition;
        }

        public void SetTarget(GameObject target)
        {
            // Unsubscribe from previous target if exists
            if (Target != null)
            {
                Unit previousTarget = Target.GetComponent<Unit>();
                if (previousTarget != null)
                {
                    previousTarget.OnDeath -= HandleTargetDeath;
                }
            }
            
            Target = target;
            if (target == null)
            {
                // If no target, destroy this projectile
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
                return;
            }
            
            try
            {
                LastTargetPosition = target.transform.position;
                // Subscribe to the unit's OnDeath event
                Unit targetUnit = target.GetComponent<Unit>();
                if (targetUnit != null)
                {
                    targetUnit.OnDeath += HandleTargetDeath;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in SetTarget: {e.Message}");
                // If we can't safely set the target, destroy the projectile
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void HandleTargetDeath(Unit unit)
        {
            // Check if this projectile is still valid and has not been destroyed
            if (this == null || gameObject == null || !gameObject.activeInHierarchy)
            {
                // Projectile has been destroyed or is inactive, don't proceed
                return;
            }
            
            // Store the last position before clearing the target
            Vector3 lastPosition = unit != null && unit.gameObject != null 
                ? unit.transform.position 
                : LastTargetPosition;
                
            // Handle the target death
            Target = null;
            LastTargetPosition = lastPosition;

            try
            {
                // Ensure that the projectile directly moves towards the last position
                MoveToLastPositionOrDestroy();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error in HandleTargetDeath: {e.Message}");
                // If we can't safely move the projectile, destroy it
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (Target != null)
                {
                    Unit targetUnit = Target.GetComponent<Unit>();
                    if (targetUnit != null)
                    {
                        targetUnit.OnDeath -= HandleTargetDeath;
                    }
                }
            }
            catch (System.Exception e)
            {
                // Just log and continue - we're destroying anyway
                Debug.LogWarning($"Error in OnDestroy cleanup: {e.Message}");
            }
        }

        public void Destroy(bool hitUnit = false, Unit hitUnitRef = null, Vector3 hitPoint = default, Vector3 hitNormal = default)
        {
            if (isDestroyed) return;

            // Spawn appropriate hit effects
            if (hitUnit && hitUnitRef != null && HitEffectsEnabled)
            {
                // Use the FactionManager to check if the unit hit is an enemy
                bool isEnemyHit = hitUnitRef.MyFaction != MyFaction;
            
                // Create VFX appropriate for what was hit
                CreateHitEffects(hitPoint, hitNormal, isEnemyHit, hitUnitRef);
            
                // Add damage if this was an enemy unit
                if (isEnemyHit)
                {
                    ApplyDamageToUnit(hitUnitRef);
                }
            }
            else if (HitEffectsEnabled)
            {
                // Hit something else (terrain, etc.) - create default effects
                CreateEnvironmentHitEffects(hitPoint, hitNormal);
            }

            // Mark as destroyed to prevent duplicate calls
            isDestroyed = true;
            
            // Deactivate the projectile
            DeactivateProjectile();
        }

        // Add these methods needed by the Destroy method
        private void CreateHitEffects(Vector3 hitPoint, Vector3 hitNormal, bool isEnemyHit, Unit hitUnit)
        {
            // Create impact effect at the hit point
            if (impact != null)
            {
                GameObject impactEffect = Instantiate(impact, hitPoint, Quaternion.LookRotation(hitNormal));
                Destroy(impactEffect, 0.5f);
            }
            
            // Optionally create different effects based on if it's an enemy or not
            if (isEnemyHit)
            {
                // Could create additional enemy-hit specific effects here
            }
        }

        private void CreateEnvironmentHitEffects(Vector3 hitPoint, Vector3 hitNormal)
        {
            // Create a generic environment hit effect
            if (impact != null)
            {
                GameObject impactEffect = Instantiate(impact, hitPoint, Quaternion.LookRotation(hitNormal));
                Destroy(impactEffect, 0.5f);
            }
        }

        private void ApplyDamageToUnit(Unit hitUnit)
        {
            if (hitUnit != null && !hitUnit.GetIsDeath())
            {
                // Apply damage based on the projectile's damage value
                hitUnit.AddDmg(Dmg);
            }
        }

        private void DeactivateProjectile()
        {
            // Clean up and destroy this projectile
            Destroy(gameObject);
        }

    }
}
