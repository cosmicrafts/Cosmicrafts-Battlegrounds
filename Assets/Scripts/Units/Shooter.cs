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

        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange;  // ✅ Use HashSet for O(1) lookups
        private Unit Target;
        private float lastTargetCheckTime = 0f;
        private bool wasTargetNull = true;

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
        }

        void Update()
        {
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
            if (Target == null || Target.GetIsDeath() || !InRange.Contains(Target))
            {
                Target = null;
                FindNewTarget();
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
            if (InRange.Add(enemy) && Target == null)
            {
                FindNewTarget(); // ✅ Only find a target when a new enemy is added
            }
        }

        public void RemoveEnemy(Unit enemy)
        {
            if (InRange.Remove(enemy) && Target == enemy)
            {
                Target = null;
                FindNewTarget();
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
            if (nFTsUnit != null) BulletDamage = nFTsUnit.Dammage;
        }
    }
}
