using System.Collections.Generic;
using UnityEngine;

namespace CosmicraftsSP
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
        public GameObject Bullet;
        public Transform[] Cannons;

        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange;  // ✅ Use HashSet for O(1) lookups
        private Unit Target;

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
                ShootTarget();
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
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 3f);
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
