namespace Cosmicrafts
{
    using SensorToolkit;
    using UnityEngine;
    using System.Collections;

    public class Ship : Unit
    {
        public SteeringRig MySt;
        float Speed = 0f;

        [Range(0, 99)]
        public float Aceleration = 1f;
        [Range(0, 99)]
        public float MaxSpeed = 10f;
        [Range(0, 99)]
        public float DragSpeed = 1f;
        [Range(0, 99)]
        public float TurnSpeed = 5f;
        [Range(0, 99)]
        public float StopSpeed = 5f;
        [Range(0, 10)]
        public float StoppingDistance = 0.5f;
        [Range(0, 50)]
        public float AvoidanceRange = 3f;
        [Range(0, 1)]
        public float RotationDamping = 0.1f; // Damping factor for smoother rotation
        public bool AlignRotationWithMovement = true; // Flag to toggle rotation alignment

        [Header("Follow Behavior")]
        [Tooltip("Whether the ship should follow the player when not attacking")]
        public bool followPlayerWhenIdle = true;
        [Tooltip("Distance to maintain when following the player")]
        [Range(1f, 20f)]
        public float playerFollowDistance = 5f;
        [Tooltip("Whether to return to spawn point when not following player")]
        public bool returnToSpawnWhenIdle = true;

        // Store original spawn point as a position relative to player
        [HideInInspector]
        public Vector3 originalSpawnPointLocalPosition;
        // Reference to the specific spawn point transform if available
        [HideInInspector]
        public Transform spawnPointTransform;
        // The player transform
        [HideInInspector]
        public Transform playerTransform;
        
        [Header("Regeneration Settings")]
        [Tooltip("Amount of shield points regenerated per second")]
        [Range(0, 50)]
        public float ShieldRegenRate = 1f;
        
        [Tooltip("Determines if shield regenerates when taking damage (true) or needs to wait (false)")]
        public bool RegenShieldWhileDamaged = false;
        
        [Tooltip("Amount of HP points regenerated per second")]
        [Range(0, 50)]
        public float HPRegenRate = 0f;
        
        [Tooltip("Delay in seconds before HP regeneration starts after taking damage")]
        [Range(0, 30)]
        public float HPRegenDelay = 5f;
        
        // Timer for HP regeneration
        private float hpRegenTimer = 0f;
        private bool recentlyDamaged = false;
        private float lastDamageTime = 0f;

        Transform Target;
        public RaySensor[] AvoidanceSensors;
        public GameObject[] Thrusters;

        [HideInInspector]
        public bool CanMove = true;

        Vector3 DeathRot;
        Vector3 moveDirection; // Store the current movement direction
        Quaternion targetRotation; // Store the target rotation
        
        private float playerFollowUpdateTimer = 0f;
        private float playerFollowUpdateInterval = 1.5f; // Update follow position every 1.5 seconds
        
        protected override void Start()
        {
            base.Start();
            Target = GameMng.GM.GetFinalTransformTarget(MyTeam);
            MySt.Destination = Target.position;
            MySt.StoppingDistance = StoppingDistance;
            foreach (RaySensor sensor in AvoidanceSensors)
            {
                sensor.Length = AvoidanceRange;
            }
            
            // Initialize target rotation to current rotation
            targetRotation = transform.rotation;
            
            // Set the SteeringRig to allow for proper turning
            MySt.RotateTowardsTarget = false; // We'll handle rotation ourselves
            
            // Initialize player reference if not set already
            if (playerTransform == null && GameMng.P != null)
            {
                playerTransform = GameMng.P.transform;
            }
        }

        protected override void Update()
        {
            base.Update();
            Move();
            
            // Update recently damaged flag
            if (recentlyDamaged && Time.time - lastDamageTime > ShieldDelay)
            {
                recentlyDamaged = false;
            }
            
            // Handle regeneration
            RegenerateShieldAndHP();
        }
        
        // Public method to check if shield can regenerate
        public bool CanRegenerateShield()
        {
            // Shield can regenerate if:
            // 1. Ship allows shields to regenerate while damaged OR
            // 2. Ship hasn't been damaged recently (ShieldLoad is low)
            return RegenShieldWhileDamaged || !IsRecentlyDamaged();
        }
        
        public void TakeDamage(int damage, TypeDmg damageType = TypeDmg.Normal)
        {
            // Call the standard damage method
            AddDmg(damage, damageType);
            
            // Reset HP regeneration timer
            hpRegenTimer = HPRegenDelay;
            
            // Mark that damage was just taken
            recentlyDamaged = true;
            lastDamageTime = Time.time;
        }
        
        private void RegenerateShieldAndHP()
        {
            if (IsDeath) return;
            
            // Shield regeneration
            if (Shield < GetMaxShield() && CanRegenerateShield() && ShieldRegenRate > 0)
            {
                // Add the regeneration amount, taking into account fractional regeneration
                float shieldToAdd = ShieldRegenRate * Time.deltaTime;
                int wholeAmount = Mathf.FloorToInt(shieldToAdd);
                float fractional = shieldToAdd - wholeAmount;
                
                // Random chance to add an extra point based on the fractional part
                if (UnityEngine.Random.value < fractional)
                    wholeAmount += 1;
                
                if (wholeAmount > 0)
                {
                    Shield += wholeAmount;
                    if (Shield > GetMaxShield())
                        Shield = GetMaxShield();
                    
                    UI.SetShieldBar((float)Shield / (float)GetMaxShield());
                }
            }
            
            // HP regeneration - with delay after taking damage
            int currentMaxHP = GetMaxHitPoints();
            if (HPRegenRate > 0 && HitPoints < currentMaxHP)
            {
                if (hpRegenTimer > 0)
                {
                    hpRegenTimer -= Time.deltaTime;
                }
                else
                {
                    // Same logic as shield - convert to int with chance for fractional parts
                    int hpToAdd = Mathf.FloorToInt(HPRegenRate * Time.deltaTime);
                    float fractional = (HPRegenRate * Time.deltaTime) - hpToAdd;
                    if (Random.value < fractional)
                        hpToAdd += 1;
                    
                    if (hpToAdd > 0)
                    {
                        HitPoints += hpToAdd;
                        if (HitPoints > currentMaxHP)
                            HitPoints = currentMaxHP;
                        
                        UI.SetHPBar((float)HitPoints / (float)currentMaxHP);
                    }
                }
            }
        }
        
        // Helper to check if shield is in recovery mode
        private bool IsRecentlyDamaged()
        {
            // Check only using our local damage tracking variable
            return recentlyDamaged;
        }

        protected override void FixedUpdate()
        {
            // Limit velocity but don't zero it completely
            if (MyRb.linearVelocity.magnitude > MaxSpeed + 1f)
            {
                MyRb.linearVelocity = MyRb.linearVelocity.normalized * (MaxSpeed + 1f);
            }
            
            // Allow some angular velocity for smoother turns, but cap it
            if (MyRb.angularVelocity.magnitude > 1f)
            {
                MyRb.angularVelocity = MyRb.angularVelocity.normalized * 1f;
            }
            
            // Only constrain X and Z rotation to keep the ship level, but allow Y rotation to happen naturally
            Vector3 eulerAngles = transform.rotation.eulerAngles;
            if (Mathf.Abs(eulerAngles.x) > 5f || Mathf.Abs(eulerAngles.z) > 5f)
            {
                transform.rotation = Quaternion.Euler(0f, eulerAngles.y, 0f);
            }
        }

        // Get the position where this ship should be relative to the player
        private Vector3 GetFormationPosition()
        {
            if (playerTransform == null)
                return transform.position;
                
            // The ship will maintain its relative position to the player based on its original spawn
            // This creates a more natural formation that maintains the player's intended deployment
            
            // If we have a spawn point reference, use its position
            if (spawnPointTransform != null && spawnPointTransform.gameObject.activeInHierarchy)
            {
                return spawnPointTransform.position;
            }
            
            // Otherwise calculate based on the stored local position
            return playerTransform.TransformPoint(originalSpawnPointLocalPosition);
        }
        
        // Calculate a position to follow based on player and formation
        private Vector3 CalculateFollowPosition()
        {
            if (playerTransform == null)
                return transform.position;
                
            // Get the formation position (where the ship would be if exactly following its spawn point)
            Vector3 formationPos = GetFormationPosition();
            
            // Get the player forward direction
            Vector3 playerForward = playerTransform.forward;
            
            // Calculate the desired distance between player and ship
            float desiredDistance = playerFollowDistance;
            
            // Current distance between ship's formation position and player
            float currentDistance = Vector3.Distance(formationPos, playerTransform.position);
            
            // If we're already in a good formation position, use it
            if (Mathf.Abs(currentDistance - desiredDistance) < 2f)
            {
                return formationPos;
            }
            
            // Otherwise create a blended position that maintains relative angle but adjusts distance
            Vector3 directionFromPlayer = (formationPos - playerTransform.position).normalized;
            return playerTransform.position + directionFromPlayer * desiredDistance;
        }
        
        void Move()
        {
            if (IsDeath)
            {
                transform.Rotate(DeathRot, 100f * Time.deltaTime, Space.Self);
                return;
            }

            if (InControl())
            {
                if (CanMove)
                {
                    // Accelerate gradually
                    if (Speed < MaxSpeed)
                    {
                        Speed += Aceleration * Time.deltaTime;
                    }
                    else
                    {
                        Speed = MaxSpeed;
                    }

                    // Apply steering parameters
                    MySt.TurnForce = TurnSpeed * 100f;
                    MySt.StrafeForce = DragSpeed * 100f;
                    MySt.MoveForce = Speed * 100f;
                    MySt.StopSpeed = StopSpeed;
                    
                    // Calculate steering direction and remember it
                    if (MySt.IsSeeking)
                    {
                        Vector3 directionToTarget = (MySt.Destination - transform.position).normalized;
                        moveDirection = MySt.GetSteeredDirection(directionToTarget);
                    }
                    
                    // Only update rotation if we want to align with movement
                    if (AlignRotationWithMovement && moveDirection.sqrMagnitude > 0.01f)
                    {
                        // Create a target rotation that points in the movement direction
                        targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                        
                        // Smoothly rotate towards the target direction
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            targetRotation,
                            TurnSpeed * Time.deltaTime * (1f - RotationDamping)
                        );
                    }
                    
                    // Periodically update position if following player
                    if (playerTransform != null)
                    {
                        // Update timer
                        playerFollowUpdateTimer -= Time.deltaTime;
                        
                        // If timer expired and we're not attacking, recalculate positions
                        if (playerFollowUpdateTimer <= 0f)
                        {
                            playerFollowUpdateTimer = playerFollowUpdateInterval;
                            
                            // Update spawn point reference if needed
                            if (spawnPointTransform != null && spawnPointTransform.gameObject.activeInHierarchy)
                            {
                                // Update the local position in case the spawn point has moved
                                originalSpawnPointLocalPosition = playerTransform.InverseTransformPoint(spawnPointTransform.position);
                            }
                            
                            // Check if we have a shooter with no targets
                            Shooter shooter = GetComponent<Shooter>();
                            bool hasNoTarget = shooter == null || shooter.GetCurrentTarget() == null;
                            
                            if (hasNoTarget && followPlayerWhenIdle)
                            {
                                // Calculate new formation position using the more advanced method
                                Vector3 followPos = CalculateFollowPosition();
                                
                                // Update destination
                                MySt.Destination = followPos;
                                MySt.StoppingDistance = playerFollowDistance * 0.2f;
                            }
                        }
                    }
                }
                else
                {
                    MySt.TurnForce = 0f;
                    MySt.MoveForce = 0f;
                    Speed = 0f;
                    moveDirection = Vector3.zero;
                }

                // Manage thrusters
                if (MySt.hasReachedDestination() && ThrustersAreEnable())
                {
                    EnableThrusters(false);
                }
                if (!MySt.hasReachedDestination() && !ThrustersAreEnable())
                {
                    EnableThrusters(true);
                }
            }
            else if (ThrustersAreEnable())
            {
                EnableThrusters(false);
                MySt.TurnForce = 0f;
                MySt.MoveForce = 0f;
                Speed = 0f;
                moveDirection = Vector3.zero;
            }
        }

        public void ResetDestination()
        {
            if (!InControl())
                return;

            // Get the shooter component to check if we have any targets
            Shooter shooter = GetComponent<Shooter>();
            bool hasTarget = shooter != null && shooter.GetCurrentTarget() != null;
            
            // If we have a target, don't reset
            if (hasTarget)
                return;
                
            // Determine where to go when idle
            if (followPlayerWhenIdle && playerTransform != null)
            {
                // Use the new method to calculate an intelligent follow position
                Vector3 followPos = CalculateFollowPosition();
                
                // Set destination to follow the player
                MySt.Destination = followPos;
                MySt.StoppingDistance = playerFollowDistance * 0.2f; // Smaller stopping distance for smoother following
            }
            else if (returnToSpawnWhenIdle && playerTransform != null)
            {
                // Calculate the current world position of the spawn point using GetFormationPosition
                Vector3 currentSpawnPos = GetFormationPosition();
                
                // Set destination to the updated spawn point position
                MySt.Destination = currentSpawnPos;
                MySt.StoppingDistance = StoppingDistance;
            }
            else if (Target != null)
            {
                // Default behavior - go to team target
                MySt.Destination = Target.position;
                MySt.StoppingDistance = StoppingDistance;
            }
        }

        public void SetDestination(Vector3 des, float stopdistance)
        {
            MySt.Destination = des;
            MySt.StoppingDistance = stopdistance;
            
            // Calculate initial direction to target for smoother initial rotation
            if (AlignRotationWithMovement)
            {
                Vector3 directionToTarget = (des - transform.position).normalized;
                moveDirection = MySt.GetSteeredDirection(directionToTarget);
                // Don't set rotation immediately, will be handled in Move()
            }
        }

        // Called to override the automatic rotation (e.g., for abilities/attacks)
        public void SetCustomRotation(Quaternion rotation)
        {
            AlignRotationWithMovement = false;
            targetRotation = rotation;
            StartCoroutine(ReenableRotationAlignment(1.0f)); // Auto-reenable after delay
        }
        
        // Re-enable rotation alignment after a delay
        System.Collections.IEnumerator ReenableRotationAlignment(float delay)
        {
            yield return new WaitForSeconds(delay);
            AlignRotationWithMovement = true;
        }

        protected override void CastComplete()
        {
            base.CastComplete();
        }

        public override void Die()
        {
            base.Die();
            MySt.enabled = false;
            EnableThrusters(false);
            float AngleDeathRot = CMath.AngleBetweenVector2(LastImpact, transform.position);

            float z = Mathf.Sin(AngleDeathRot * Mathf.Deg2Rad);
            float x = Mathf.Cos(AngleDeathRot * Mathf.Deg2Rad);
            DeathRot = new Vector3(x, 0, z);
        }

        public override void DisableUnit()
        {
            base.DisableUnit();
        }

        public override void EnableUnit()
        {
            base.EnableUnit();
        }

        public override void SetNfts(NFTsUnit nFTsUnit)
        {
            base.SetNfts(nFTsUnit);

            if (nFTsUnit == null)
                return;

            MaxSpeed = nFTsUnit.Speed;
        }

        void EnableThrusters(bool enable)
        {
            foreach (GameObject t in Thrusters)
                t.SetActive(enable);
        }

        bool ThrustersAreEnable()
        {
            return Thrusters == null ? false : (Thrusters.Length > 0 ? Thrusters[0].activeSelf : false);
        }

        public void ResetShip()
        {
            // Reset shield and HP regeneration timers
            hpRegenTimer = 0f;
            
            // Reset movement variables
            Speed = 0f;
            DeathRot = Vector3.zero;
            moveDirection = Vector3.zero;
            
            // Reset player follow timer
            playerFollowUpdateTimer = 0f;
            
            // Re-enable SteeringRig and set target
            if (MySt != null)
            {
                MySt.enabled = true;
                
                // Reset destination based on follow behavior
                if (followPlayerWhenIdle && playerTransform != null)
                {
                    // Use the new method to calculate follow position
                    Vector3 followPos = CalculateFollowPosition();
                    
                    MySt.Destination = followPos;
                    MySt.StoppingDistance = playerFollowDistance * 0.2f;
                }
                else if (returnToSpawnWhenIdle && playerTransform != null)
                {
                    // Use the GetFormationPosition method to get current spawn position
                    Vector3 currentSpawnPos = GetFormationPosition();
                    
                    MySt.Destination = currentSpawnPos;
                    MySt.StoppingDistance = StoppingDistance;
                }
                else
                {
                    Target = GameMng.GM.GetFinalTransformTarget(MyTeam);
                    MySt.Destination = Target.position;
                    MySt.StoppingDistance = StoppingDistance;
                }
            }
            
            // Reset thrusters
            EnableThrusters(false);
            
            // Reset rotation alignment
            AlignRotationWithMovement = true;
            targetRotation = transform.rotation;
            
            // Re-enable movement
            CanMove = true;
        }

        // Replace the SetSpawnPoint method to store position relative to player
        public void SetSpawnPoint(Vector3 worldSpawnPoint, Transform spawnPointTransform = null)
        {
            if (playerTransform != null)
            {
                // Store the spawn point as a position relative to the player
                originalSpawnPointLocalPosition = playerTransform.InverseTransformPoint(worldSpawnPoint);
                
                // Store reference to the spawn point transform if provided
                this.spawnPointTransform = spawnPointTransform;
            }
        }
        
        // Method to update spawn point positions during formation changes
        public void UpdateSpawnPointPosition()
        {
            if (spawnPointTransform != null && spawnPointTransform.gameObject.activeInHierarchy)
            {
                // Update the local position relative to player based on current spawn point transform
                originalSpawnPointLocalPosition = playerTransform.InverseTransformPoint(spawnPointTransform.position);
            }
        }
        
        // Public method to set the player transform reference
        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }
    }
}
