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

    [RequireComponent(typeof(Unit))] // Ensure Shooter is on a Unit
    public class Shooter : MonoBehaviour
    {
        [Header("Detection & Attack")]
        [Tooltip("The collider responsible for detecting potential targets.")]
        public SphereCollider EnemyDetector;
        [Tooltip("The radius within which enemies are detected.")]
        [Range(1, 150)] public float DetectionRange = 15f; // Renamed and set default
        [Tooltip("The radius within which the unit can actually attack.")]
        [Range(1, 150)] public float AttackRange = 10f; // Previously RangeDetector
        
        [HideInInspector] public bool CanAttack = true;
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

        [Header("Range Visualization (Based on AttackRange)")]
        [Tooltip("How to visualize the attack range.")]
        [SerializeField] private RangeVisualizationType rangeVisualizationType = RangeVisualizationType.Circle;
        [SerializeField][Range(12, 72)] private int lineSegments = 36; // Number of segments for the circle
        [SerializeField] private Color rangeColor = Color.yellow;
        [SerializeField] private float rangeLineWidth = 0.1f;

        private LineRenderer rangeLineRenderer;
        private ParticleSystem[] MuzzleFlash;
        private float DelayShoot = 0f;
        private Ship MyShip;
        private Unit MyUnit;
        private HashSet<Unit> InRange;  // Use HashSet for O(1) lookups
        private Unit Target;

        private void Awake()
        {
            // Auto‑assign the detector if the reference was lost in the prefab/inspector
            if (EnemyDetector == null)
            {
                EnemyDetector = GetComponentInChildren<SphereCollider>();
                if (EnemyDetector == null)
                {
                    Debug.LogError($"Shooter on {gameObject.name} needs an EnemyDetector SphereCollider!", this);
                    this.enabled = false; // Disable shooter if no detector
                    return;
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
            EnemyDetector.radius = DetectionRange;

            MuzzleFlash = new ParticleSystem[Cannons.Length];

            for (int i = 0; i < Cannons.Length; i++)
            {
                if (Cannons[i] != null && Cannons[i].childCount > 0)
                {
                    MuzzleFlash[i] = Cannons[i].GetChild(0).GetComponent<ParticleSystem>();
                }
            }
            if (CoolDown <= 0f) CoolDown = 0.1f;
            
            // Setup LineRenderer based on selected type
            SetupRangeVisualizer();
        }

        void Start()
        {
            // Draw the initial range visualization
            UpdateRangeVisualizer();
        }

        void Update()
        {
            if (MyUnit.GetIsDeath() || !CanAttack || !MyUnit.InControl())
            {
                // If we cannot attack, ensure ship isn't trying to close distance to a target
                if (Target != null && MyShip != null && StopToAttack)
                {
                    MyShip.ResetDestination(); // Go back to default behavior
                }
                Target = null; // Clear target if we can't attack
                return; // Exit if dead, disabled, or casting
            }

            // 1. Validate Current Target
            bool targetStillValid = Target != null && !Target.GetIsDeath();
            bool targetInAttackRange = targetStillValid && (Vector3.Distance(transform.position, Target.transform.position) <= AttackRange);

            // 2. If current target is invalid or out of attack range, find a new one
            if (!targetStillValid || !targetInAttackRange) 
            {   
                // If the target became invalid/out of range, clear it before finding new
                if(Target != null) SetTarget(null);
                
                FindNewTarget(); // Find the *best* candidate currently detected
                
                // Re-evaluate attack range for the newly found target (if any)
                targetStillValid = Target != null && !Target.GetIsDeath();
                targetInAttackRange = targetStillValid && (Vector3.Distance(transform.position, Target.transform.position) <= AttackRange);
            }

            // 3. Handle Ship Movement (if applicable)
            if (MyShip != null && StopToAttack)
            {
                if (targetStillValid) // We have a valid target (might be out of range still)
                {   
                    // Tell ship to move towards target and stop just inside attack range
                    MyShip.SetDestination(Target.transform.position, AttackRange * 0.9f); 
                }
                else // No valid target currently
                {
                    // Resume default movement
                    MyShip.ResetDestination();
                }
            }
            
            // 4. Attempt to Shoot if target is valid AND in attack range
            if (targetInAttackRange) // Check the re-evaluated range status
            {
                ShootTarget(); // This checks cooldown internally
            }
        }

        // Public method to update the visualizer if range or type changes
        public void UpdateRangeVisualizer()
        {
            // Ensure detector radius always matches the range value used for visualization
            if (EnemyDetector != null)
            {
                EnemyDetector.radius = DetectionRange;
            }
            else { 
                Debug.LogError($"EnemyDetector is missing on {gameObject.name}! Cannot sync range.", this);
                return;
            }
            
            if (rangeLineRenderer == null) 
            {
                SetupRangeVisualizer(); 
                if (rangeLineRenderer == null) return; 
            }
            
            rangeLineRenderer.enabled = (rangeVisualizationType != RangeVisualizationType.None);
            if (!rangeLineRenderer.enabled) return;
            
            rangeLineRenderer.startWidth = rangeLineWidth;
            rangeLineRenderer.endWidth = rangeLineWidth;
            rangeLineRenderer.startColor = rangeColor;
            rangeLineRenderer.endColor = rangeColor;

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
        
        private void SetupRangeVisualizer()
        {
            if (rangeLineRenderer == null)
            {
                rangeLineRenderer = GetComponentInChildren<LineRenderer>();
                if (rangeLineRenderer == null)
                {
                    GameObject lineObj = new GameObject("RangeVisualizer");
                    lineObj.transform.SetParent(transform);
                    lineObj.transform.localPosition = Vector3.zero;
                    lineObj.transform.localRotation = Quaternion.identity; 
                    lineObj.transform.localScale = Vector3.one;
                    rangeLineRenderer = lineObj.AddComponent<LineRenderer>();
                }
            }
            rangeLineRenderer.useWorldSpace = false; 
            if (rangeLineRenderer.sharedMaterial == null || rangeLineRenderer.sharedMaterial.shader == null || rangeLineRenderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null) rangeLineRenderer.material = new Material(shader);
                else Debug.LogError("Could not find a suitable default shader for LineRenderer.");
            }
            rangeLineRenderer.alignment = LineAlignment.TransformZ; 
            UpdateRangeVisualizer(); 
        }

        private void DrawRangeCircle()
        {
            // Build circle in world space, then convert to local so scale has no effect
            int segments = lineSegments;
            rangeLineRenderer.positionCount = segments + 1;
            rangeLineRenderer.loop = true;
            rangeLineRenderer.transform.localRotation = Quaternion.identity; // No extra rotation needed

            float angleStep = 360f / segments;
            Vector3[] points = new Vector3[segments + 1];
            Vector3 center = transform.position;

            for (int i = 0; i <= segments; i++)
            {
                float angleRad = i * angleStep * Mathf.Deg2Rad;
                // Compute offset on XZ plane using world units (y stays the same)
                Vector3 worldOffset = new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * AttackRange;
                Vector3 worldPoint = center + worldOffset;

                // Convert the world point to local space for the LineRenderer (which uses local space)
                points[i] = transform.InverseTransformPoint(worldPoint);
            }

            rangeLineRenderer.SetPositions(points);
        }
        
        private void DrawDirectionalLine()
        {
            rangeLineRenderer.positionCount = 2;
            rangeLineRenderer.loop = false;
            rangeLineRenderer.transform.localRotation = Quaternion.identity;

            // Start point is always the origin of the LineRenderer (local zero)
            Vector3 localStart = Vector3.zero;

            // Compute end point in world space, then convert to local
            Vector3 endWorld = transform.position + transform.forward * AttackRange;
            Vector3 localEnd = transform.InverseTransformPoint(endWorld);

            rangeLineRenderer.SetPositions(new Vector3[] { localStart, localEnd });
        }

        public void ShootTarget()
        {
            // Target validity and range is checked in Update before calling this
            if (Target == null) return; // Still good to have a basic null check
            
            // Rotate if needed
            if (RotateToEnemy)
            {
                Vector3 direction = (Target.transform.position - transform.position).normalized;
                direction.y = 0; 
                if (direction.sqrMagnitude > 0.01f) 
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                }
            }

            // Fire if cooldown is ready
            if (DelayShoot <= 0f)
            {
                FireProjectiles();
                DelayShoot = CoolDown; // Reset cooldown *after* firing
                MyUnit?.GetAnimator()?.SetTrigger("Attack");
            }
        }

        private void FireProjectiles()
        {
             if (Bullet == null) 
             { 
                 Debug.LogWarning($"Shooter on {gameObject.name} has no Bullet prefab assigned!", this);
                 return; 
             }
             
            foreach (Transform cannon in Cannons)
            {
                if (cannon == null) continue; // Skip if cannon transform is missing
                
                GameObject bulletPrefab = Instantiate(Bullet, cannon.position, cannon.rotation);
                Projectile bullet = bulletPrefab.GetComponent<Projectile>();
                
                if (bullet != null)
                {
                    bullet.MyTeam = MyUnit.MyTeam;
                    // Double check target validity just before firing
                    if (Target != null && Target.gameObject != null && !Target.GetIsDeath()) 
                    {
                        bullet.SetTarget(Target.gameObject);
                    }
                    else
                    {
                        Destroy(bulletPrefab); // Invalid target at fire time
                        continue; // Skip this bullet
                    }
                    
                    bullet.Speed = BulletSpeed;
                    bullet.Dmg = Random.value < criticalStrikeChance ? (int)(BulletDamage * criticalStrikeMultiplier) : BulletDamage;

                    // ✅ Play muzzle flash
                    // Find ParticleSystem safely
                    ParticleSystem flash = null;
                    if (cannon.childCount > 0) flash = cannon.GetChild(0).GetComponent<ParticleSystem>();
                    flash?.Play();
                }
                else
                {
                    Debug.LogWarning($"Instantiated bullet from {gameObject.name} is missing Projectile component!", bulletPrefab);
                    Destroy(bulletPrefab); // Clean up invalid bullet
                }
            }
        }

        // Tick cooldown timer separately
        private void HandleCooldown()
        {
             if (DelayShoot > 0f)
             {
                 DelayShoot -= Time.deltaTime;
             }
        }

        void FixedUpdate() 
        {
            HandleCooldown();
        }

        // SetTarget now only updates the Target variable. Movement is handled in Update.
        public void SetTarget(Unit target)
        {
            if (target != null && (target == MyUnit || target.MyTeam == MyUnit.MyTeam)) 
            {   
                if (Target == target) Target = null; // Clear if setting self/friendly
                return; 
            }
            
            // Only update if the target is actually different
            if (Target != target) 
            { 
                Target = target;
                // No ship movement logic here anymore - handled in Update
            }
        }

        // AddEnemy just adds to the list. Update loop handles choosing target.
        public void AddEnemy(Unit enemy)
        {
            if (enemy == null || enemy.GetIsDeath() || enemy == MyUnit || enemy.MyTeam == MyUnit.MyTeam) return;
            
            // Add valid enemy to potential targets list
            InRange.Add(enemy);
            
            // No need to immediately FindNewTarget here. Update loop will handle it.
        }

        // RemoveEnemy just removes from the list. Update loop handles target loss.
        public void RemoveEnemy(Unit enemy)
        {
            if (enemy == null) return;
            
            InRange.Remove(enemy);
            
            // If the removed enemy was our current target, 
            // the Update loop will detect it as invalid/out of range and find a new one.
            // No need for specific logic here anymore.
            // if (Target == enemy)
            // {
            //     Target = null;
            //     FindNewTarget(); // This call is removed
            // }
        }

        // FindNewTarget now finds the closest valid enemy detected by the collider,
        // regardless of whether it's currently in exact attack range.
        private void FindNewTarget()
        {
            Unit closestEnemy = null;
            float closestDistanceSqr = float.MaxValue;
            
            // Use a temporary list to iterate while potentially modifying InRange
            List<Unit> currentInRange = new List<Unit>(InRange);
            
            foreach (Unit potentialTarget in currentInRange)
            {
                // Check basic validity: Not null, not dead, not friendly
                if (potentialTarget == null || potentialTarget.GetIsDeath() || potentialTarget.MyTeam == MyUnit.MyTeam)
                { 
                    InRange.Remove(potentialTarget); // Clean up invalid entries from main set
                    continue; 
                }
                
                // Find the closest valid enemy based on distance
                float distanceSqr = (transform.position - potentialTarget.transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestEnemy = potentialTarget;
                    closestDistanceSqr = distanceSqr;
                }
            }
            
            // Update the target if the closest enemy found is different from the current one
            if(Target != closestEnemy) 
            { 
                SetTarget(closestEnemy); 
            }
            // If no valid enemy found, SetTarget(null) will be called implicitly if Target was previously set
        }

        public void StopAttack()
        {
            CanAttack = false;
            InRange.Clear();
            SetTarget(null);
            // Stop Ship if it was moving towards a target
            if (MyShip != null && StopToAttack)
            {
                MyShip.ResetDestination();
            }
        }

        public int GetIdTarget() => Target == null ? 0 : Target.getId();

        public virtual void InitStatsFromNFT(NFTsUnit nFTsUnit)
        {
            if (nFTsUnit != null) 
            {
                BulletDamage = nFTsUnit.Damage; // Set damage from NFT
                float previousAttackRange = AttackRange; // Remember previous value for comparison
                float previousDetectionRange = DetectionRange; // Remember previous value for comparison
                
                // Use NFT's range values
                AttackRange = nFTsUnit.AttackRange; // Attack range (was RangeDetector)
                DetectionRange = nFTsUnit.DetectionRange; // Detection range for enemy detection
                
                if (Mathf.Abs(previousAttackRange - AttackRange) > 0.01f || 
                    Mathf.Abs(previousDetectionRange - DetectionRange) > 0.01f) 
                {
                    UpdateRangeVisualizer();
                }
            }
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
            if (EnemyDetector != null) EnemyDetector.radius = DetectionRange;
            
            // Update the visualizer
            UpdateRangeVisualizer();
        }
    }
}
