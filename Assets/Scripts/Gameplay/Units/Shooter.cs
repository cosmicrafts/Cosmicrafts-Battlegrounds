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
        [Tooltip("How often to check if a target is still valid (seconds)")]
        public float targetValidationRate = 1.0f;
        [Header("Aggro Settings")]
        [Tooltip("How far the unit can detect enemies to start chasing (must be >= RangeDetector)")]
        [Range(1, 200)] public float AggroRange = 5f;
        [Tooltip("How often to scan for aggro targets (seconds)")]
        public float aggroScanRate = 0.5f;
        [Header("Debugging")]
        public bool EnableDebugLogs = false;

        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange;  // ✅ Use HashSet for O(1) lookups
        private Unit Target;
        private float lastTargetCheckTime = 0f;
        private bool wasTargetNull = true;
        private float lastAggroScanTime = 0f;

        private void Awake()
        {
            // Auto‑assign the detector if the reference was lost in the prefab/inspector
            if (EnemyDetector == null)
            {
                EnemyDetector = GetComponent<SphereCollider>();
                if (EnemyDetector == null)
                {
                    EnemyDetector = GetComponentInChildren<SphereCollider>();
                }
            }
            if (EnableDebugLogs) Debug.Log($"[Shooter] Awake on {gameObject.name}. Detector set to {EnemyDetector}.");
        }

        void Start()
        {
            InRange = new HashSet<Unit>();  // ✅ HashSet is more efficient for Contains()
            EnemyDetector.radius = RangeDetector;
            MyShip = GetComponent<Ship>();
            MyUnit = GetComponent<Unit>();
            MuzzleFlash = new ParticleSystem[Cannons.Length];

            for (int i = 0; i < Cannons.Length; i++)
            {
                if (Cannons[i].childCount > 0)
                {
                    MuzzleFlash[i] = Cannons[i].GetChild(0).GetComponent<ParticleSystem>();
                }
            }
            if (CoolDown <= 0f) CoolDown = 0.1f;
            if (EnableDebugLogs) Debug.Log($"[Shooter] Start on {gameObject.name}. Range {RangeDetector} / Aggro {AggroRange}.");
        }

        void Update()
        {
            // Ensure the detector radius always matches the configured range (this may be
            // modified at runtime by skills, NFTs, etc.)
            if (EnemyDetector != null && Mathf.Abs(EnemyDetector.radius - RangeDetector) > 0.01f)
            {
                EnemyDetector.radius = RangeDetector;
            }

            if (!MyUnit.GetIsDeath() && CanAttack && MyUnit.InControl())
            {
                ValidateTarget();
                
                // Only try to shoot if we have a target
                if (Target != null)
                {
                    ShootTarget();
                    // Track that we had a target last frame
                    wasTargetNull = false;
                }
                else if (!wasTargetNull)
                {
                    // We just lost our target this frame
                    if (MyShip != null && StopToAttack)
                    {
                        MyShip.ResetDestination();
                    }
                    wasTargetNull = true;
                }
                
                // Periodically check for targets that might have moved too far away
                if (Time.time - lastTargetCheckTime > targetValidationRate)
                {
                    PerformDistanceCheck();
                    lastTargetCheckTime = Time.time;
                }

                // Actively look for new enemies within aggro range if we have no target
                if (Target == null && Time.time - lastAggroScanTime > aggroScanRate)
                {
                    SearchForAggroTargets();
                    lastAggroScanTime = Time.time;
                }
            }

            if (EnableDebugLogs && Target != null)
            {
                Debug.Log($"[Shooter] Target in sight: {Target.gameObject.name} distance {Vector3.Distance(transform.position, Target.transform.position):F2}");
            }
        }

        /// <summary>
        /// Check if the target is too far away - they might have moved but not triggered the exit event
        /// </summary>
        private void PerformDistanceCheck()
        {
            if (Target != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, Target.transform.position);
                if (distanceToTarget > RangeDetector * 1.2f) // Add a small buffer
                {
                    RemoveEnemy(Target);
                }
            }
        }

        /// ✅ **Only runs when needed, instead of every frame**
        private void ValidateTarget()
        {
            if (Target == null || Target.GetIsDeath())
            {
                Target = null;
                FindNewTarget();
                return;
            }

            // Drop the target if it moved too far beyond aggro range
            float distToTarget = Vector3.Distance(transform.position, Target.transform.position);
            if (distToTarget > AggroRange * 1.2f)
            {
                RemoveEnemy(Target);
            }
        }

        public void ShootTarget()
        {
            if (Target == null) return;

            // ✅ Rotate towards target only if needed
            if (RotateToEnemy)
            {
                Vector3 direction = (Target.transform.position - transform.position).normalized;
                direction.y = 0;
                direction = direction.normalized;
                
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    Quaternion.LookRotation(direction), 
                    Time.deltaTime * rotationSpeed
                );
            }

            // ✅ Fire if cooldown is ready
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

        private void FireProjectiles()
        {
            if (EnableDebugLogs) Debug.Log($"[Shooter] {gameObject.name} firing {Cannons.Length} bullets at {Target?.gameObject?.name}");
            foreach (Transform cannon in Cannons)
            {
                GameObject bulletPrefab = Instantiate(Bullet, cannon.position, cannon.rotation);
                Projectile bullet = bulletPrefab.GetComponent<Projectile>();
                bullet.MyTeam = MyUnit.MyTeam;
                bullet.SetTarget(Target.gameObject);
                bullet.Speed = BulletSpeed;
                bullet.Dmg = Random.value < criticalStrikeChance ? (int)(BulletDamage * criticalStrikeMultiplier) : BulletDamage;

                // ✅ Play muzzle flash
                ParticleSystem flash = cannon.childCount > 0 ? cannon.GetChild(0).GetComponent<ParticleSystem>() : null;
                flash?.Play();
            }
        }

        public void SetTarget(Unit target)
        {
            Target = target;
            if (MyShip == null || !StopToAttack) return;
            if (Target == null) MyShip.ResetDestination();
            else MyShip.SetDestination(Target.transform.position, RangeDetector);
        }

        public void AddEnemy(Unit enemy)
        {
            if (InRange.Add(enemy))
            {
                if (EnableDebugLogs) Debug.Log($"[Shooter] {gameObject.name} detected enemy {enemy.gameObject.name} (total {InRange.Count})");
                if (Target == null)
                {
                    FindNewTarget(); // ✅ Only find a target when a new enemy is added
                }
            }
        }

        public void RemoveEnemy(Unit enemy)
        {
            if (InRange.Remove(enemy))
            {
                if (EnableDebugLogs) Debug.Log($"[Shooter] {gameObject.name} lost enemy {enemy?.gameObject?.name}. Remaining {InRange.Count}");
                if (Target == enemy)
                {
                    Target = null;
                    FindNewTarget();
                }
            }
        }

        /// ✅ **More efficient target selection without sorting**
        private void FindNewTarget()
        {
            Unit closest = null;
            float closestDistance = float.MaxValue;

            foreach (Unit enemy in InRange)
            {
                if (enemy == null || enemy.GetIsDeath()) continue;
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closest = enemy;
                    closestDistance = distance;
                }
            }
            SetTarget(closest);
        }

        public void StopAttack()
        {
            CanAttack = false;
            InRange.Clear();
            SetTarget(null);
        }

        public int GetIdTarget() => Target == null ? 0 : Target.getId();

        public virtual void InitStatsFromNFT(NFTsUnit nFTsUnit)
        {
            if (nFTsUnit != null) BulletDamage = nFTsUnit.Damage;
        }

        public void ResetShooter()
        {
            // Reset attack parameters
            DelayShoot = 0f;
            CanAttack = true;
            
            // Clear current targets and enemies
            Target = null;
            InRange.Clear();
            
            // Reset validation timer
            lastTargetCheckTime = 0f;
            wasTargetNull = true;
            
            // Reset detection range in case it was modified
            EnemyDetector.radius = RangeDetector;
        }

        /// <summary>
        /// Scan for the closest enemy within AggroRange and set it as the current target.
        /// This is used when we don't have any attack-range targets but still want to chase enemies.
        /// </summary>
        private void SearchForAggroTargets()
        {
            if (EnableDebugLogs) Debug.Log($"[Shooter] {gameObject.name} scanning for aggro targets");
            Collider[] hits = Physics.OverlapSphere(transform.position, AggroRange);
            Unit closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Unit")) continue;
                Unit candidate = hit.GetComponent<Unit>();
                if (candidate == null || candidate.GetIsDeath() || candidate.IsMyTeam(MyUnit.MyTeam)) continue;

                float d = Vector3.Distance(transform.position, candidate.transform.position);
                if (d < closestDist)
                {
                    closest = candidate;
                    closestDist = d;
                }
            }

            if (closest != null)
            {
                // Ensure this enemy is tracked and pursue it
                AddEnemy(closest);
                SetTarget(closest);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, RangeDetector);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, AggroRange);
        }
    }
}
