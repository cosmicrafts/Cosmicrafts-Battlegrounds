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

        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange;  // ✅ Use HashSet for O(1) lookups
        private Unit Target;

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

            // Ensure collections and references are ready before any trigger events
            if (InRange == null)
            {
                InRange = new HashSet<Unit>();
            }

            // Cache common components early (they won't change)
            if (MyUnit == null) MyUnit = GetComponent<Unit>();
            if (MyShip == null) MyShip = GetComponent<Ship>();

            // Sync detector radius at startup
            if (EnemyDetector != null) EnemyDetector.radius = RangeDetector;

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

        void Start()
        {
            // ✅ HashSet is more efficient for Contains()
            // ✅ HashSet is more efficient for Contains()
        }

        void Update()
        {
            if (!MyUnit.GetIsDeath() && CanAttack && MyUnit.InControl())
            {
                // Quick validation: if target died, immediately clear and choose another
                if (Target == null || Target.GetIsDeath())
                {
                    Target = null;
                    FindNewTarget();
                }
                
                // Only try to shoot if we have a valid target
                if (Target != null)
                {
                    ShootTarget();
                }
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

                // After shooting, see if someone else became closer (enemies might cluster)
                FindNewTarget();
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
            if (enemy == null || enemy.GetIsDeath()) return;

            if (InRange.Add(enemy))
            {
                // If we have no target OR this enemy is closer than current target, switch.
                if (Target == null)
                {
                    SetTarget(enemy);
                }
                else
                {
                    float currentDist = Vector3.Distance(transform.position, Target.transform.position);
                    float newDist     = Vector3.Distance(transform.position, enemy.transform.position);
                    if (newDist < currentDist)
                    {
                        SetTarget(enemy);
                    }
                }
            }
        }

        public void RemoveEnemy(Unit enemy)
        {
            if (InRange.Remove(enemy))
            {
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
            
            // Reset detection range in case it was modified
            EnemyDetector.radius = RangeDetector;
        }
    }
}
