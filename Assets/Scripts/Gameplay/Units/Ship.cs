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
        [Tooltip("How close the ship should be to its spawn point before stopping")]
        [Range(0.5f, 5f)]
        public float spawnPointStoppingDistance = 1f;
        [Tooltip("Priority for returning to spawn (0=low, 1=high)")]
        [Range(0f, 1f)]
        public float returnToSpawnPriority = 0.7f;

        // Store original spawn point as a position relative to player
        [HideInInspector]
        public Vector3 originalSpawnPointLocalPosition;
        // Store absolute world position of spawn point
        [HideInInspector]
        public Vector3 originalSpawnPointWorldPosition;
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
        private float playerFollowUpdateInterval = .25f; // Update follow position every 1.5 seconds
        
        [Header("Formation Settings")]
        [Tooltip("Whether spawn points should move with the player")]
        public bool moveSpawnPointsWithPlayer = true;
        [Tooltip("How frequently to update spawn point positions (seconds)")]
        [Range(0.1f, 2f)]
        public float spawnPointUpdateInterval = 0.5f;
        private float spawnPointUpdateTimer = 0f;
        
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
                SetPlayerTransform(GameMng.P.transform);
            }
            
            // Debug log to check if spawn point is set
            if (spawnPointTransform != null)
            {
                Debug.Log($"Ship {name} has spawn point set: {spawnPointTransform.name}");
                
                // Immediately update our position to match spawn point
                if (returnToSpawnWhenIdle)
                {
                    Vector3 spawnPos = GetFormationPosition();
                    MySt.Destination = spawnPos;
                    MySt.StoppingDistance = spawnPointStoppingDistance;
                    Debug.Log($"Ship {name} immediately moving to spawn position {spawnPos} on start");
                }
            }
            else if (originalSpawnPointWorldPosition != Vector3.zero)
            {
                Debug.Log($"Ship {name} has spawn position: {originalSpawnPointWorldPosition}");
            }
            else
            {
                Debug.LogWarning($"Ship {name} has NO spawn point assigned! Will use default behaviors");
                
                // Create a default spawn position if none exists
                if (playerTransform != null)
                {
                    // Create a random position relative to the player
                    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float distance = UnityEngine.Random.Range(8f, 15f);
                    
                    Vector3 relativePos = new Vector3(
                        Mathf.Sin(angle) * distance,
                        0f,
                        Mathf.Cos(angle) * distance
                    );
                    
                    originalSpawnPointLocalPosition = relativePos;
                    originalSpawnPointWorldPosition = playerTransform.TransformPoint(relativePos);
                    
                    Debug.Log($"Created default spawn position for {name}: {originalSpawnPointWorldPosition}");
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            // Update spawn point position if player is moving
            if (moveSpawnPointsWithPlayer && playerTransform != null && !IsDeath)
            {
                spawnPointUpdateTimer -= Time.deltaTime;
                if (spawnPointUpdateTimer <= 0f)
                {
                    spawnPointUpdateTimer = spawnPointUpdateInterval;
                    UpdateSpawnPointPosition();
                }
            }
            
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
            // First priority: use the spawn point transform if available (it may move with the player)
            if (spawnPointTransform != null && spawnPointTransform.gameObject.activeInHierarchy)
            {
                return spawnPointTransform.position;
            }
            
            // Second priority: if player exists and we're using relative positioning
            if (moveSpawnPointsWithPlayer && playerTransform != null && originalSpawnPointLocalPosition != Vector3.zero)
            {
                // Use the local position relative to the player - this will follow the player
                return playerTransform.TransformPoint(originalSpawnPointLocalPosition);
            }
            
            // Third priority: use the absolute world position if it was saved
            if (originalSpawnPointWorldPosition != Vector3.zero)
            {
                return originalSpawnPointWorldPosition;
            }
            
            // Fallback: use current position
            return transform.position;
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
                    // Make sure we have a player reference
                    if (playerTransform == null && GameMng.P != null)
                    {
                        SetPlayerTransform(GameMng.P.transform);
                    }
                    
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
                            
                            // Check if we have a shooter with no targets
                            Shooter shooter = GetComponent<Shooter>();
                            bool hasNoTarget = shooter == null || shooter.GetCurrentTarget() == null;
                            
                            if (hasNoTarget)
                            {
                                // Always update the spawn point position
                                UpdateSpawnPointPosition();
                                
                                // Determine where to go based on our behaviors
                                Vector3 desiredPosition;
                                
                                // First priority: Return to spawn point if enabled
                                if (returnToSpawnWhenIdle)
                                {
                                    desiredPosition = GetFormationPosition();
                                    float distanceToSpawn = Vector3.Distance(transform.position, desiredPosition);
                                    
                                    // If we're far from our spawn point, move back to it
                                    if (distanceToSpawn > spawnPointStoppingDistance * 1.5f)
                                    {
                                        // The spawn point moves with the player, so this will keep us in formation
                                        MySt.Destination = desiredPosition;
                                        MySt.StoppingDistance = spawnPointStoppingDistance;
                                        
                                        //Debug.Log($"Ship {name} following moving spawn position: {desiredPosition}, distance: {distanceToSpawn}");
                                    }
                                }
                                // Second priority: Follow player directly if enabled
                                else if (followPlayerWhenIdle) 
                                {
                                    desiredPosition = CalculateFollowPosition();
                                    MySt.Destination = desiredPosition;
                                    MySt.StoppingDistance = playerFollowDistance * 0.2f;
                                }
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
            
            // First priority: return to spawn if that behavior is enabled
            if (returnToSpawnWhenIdle)
            {
                Vector3 spawnPosition = GetFormationPosition();
                MySt.Destination = spawnPosition;
                MySt.StoppingDistance = spawnPointStoppingDistance;
                //Debug.Log($"Ship {name} resetting destination to spawn: {spawnPosition}");
            }
            // Second priority: follow player if that behavior is enabled
            else if (followPlayerWhenIdle && playerTransform != null)
            {
                Vector3 followPos = CalculateFollowPosition();
                MySt.Destination = followPos;
                MySt.StoppingDistance = playerFollowDistance * 0.2f;
            }
            // Fallback: go to team target
            else if (Target != null)
            {
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
            
            // Update player transform reference if needed
            if (playerTransform == null && GameMng.P != null)
            {
                SetPlayerTransform(GameMng.P.transform);
                Debug.Log($"Ship {name} updated player reference during reset");
            }
            
            // Re-enable SteeringRig and set target
            if (MySt != null)
            {
                MySt.enabled = true;
                
                // Determine destination based on behavior settings and spawn point
                Vector3 returnPosition;
                
                // First priority - if we have a valid spawn point, use it
                if (returnToSpawnWhenIdle)
                {
                    // Use GetFormationPosition to get the best spawn position
                    returnPosition = GetFormationPosition();
                    MySt.Destination = returnPosition;
                    MySt.StoppingDistance = spawnPointStoppingDistance;
                    
                    Debug.Log($"Reset ship {name} - returning to spawn position: {returnPosition}");
                }
                // Second priority - follow player
                else if (followPlayerWhenIdle && playerTransform != null)
                {
                    // Calculate a formation position to follow
                    returnPosition = CalculateFollowPosition();
                    MySt.Destination = returnPosition;
                    MySt.StoppingDistance = playerFollowDistance * 0.2f;
                }
                // Fallback - go to team target
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
            // Debug any existing spawn point assignment
            if (this.spawnPointTransform != null && this.spawnPointTransform != spawnPointTransform)
            {
                Debug.Log($"Ship {name} changing spawn point from {this.spawnPointTransform.name} to {spawnPointTransform?.name ?? "null"}");
            }
            
            // Always store the absolute world position
            originalSpawnPointWorldPosition = worldSpawnPoint;
            
            // Store reference to the spawn point transform if provided
            this.spawnPointTransform = spawnPointTransform;
            
            // Calculate relative position to player - CRITICAL for proper following
            if (playerTransform != null)
            {
                // Store the spawn point as a position relative to the player
                originalSpawnPointLocalPosition = playerTransform.InverseTransformPoint(worldSpawnPoint);
                
                // Debug verification - if this is zero something is wrong with the calculation
                if (originalSpawnPointLocalPosition.magnitude < 0.01f)
                {
                    Debug.LogWarning($"Ship {name} has zero relative position to player. World={worldSpawnPoint}, Player={playerTransform.position}");
                    
                    // Force a reasonable relative position
                    float randomAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    originalSpawnPointLocalPosition = new Vector3(
                        Mathf.Sin(randomAngle) * 10f,
                        0f,
                        Mathf.Cos(randomAngle) * 10f
                    );
                }
            }
            else
            {
                // No player reference - create a default relative position
                Debug.LogWarning($"Ship {name} has no player reference when setting spawn point");
                // Set a default relative position (this will be transformed when player ref is set)
                float randomAngle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                originalSpawnPointLocalPosition = new Vector3(
                    Mathf.Sin(randomAngle) * 10f,
                    0f,
                    Mathf.Cos(randomAngle) * 10f
                );
            }
            
            // Claim this spawn point explicitly - mark it as in use by us
            if (spawnPointTransform != null)
            {
                // This logging will help see which spawn point is assigned to which ship
                Debug.Log($"Ship {name} CLAIMED spawn point {spawnPointTransform.name} at position {worldSpawnPoint}");
            }
            
            // Debug log that spawn point was set
            Debug.Log($"Ship {name} spawn point set: world={worldSpawnPoint}, " +
                      $"spawnTransform={spawnPointTransform?.name ?? "none"}, " +
                      $"relative={originalSpawnPointLocalPosition}, " +
                      $"playerTransform={(playerTransform != null ? "valid" : "null")}");
        }
        
        // Method to update spawn point positions during formation changes
        public void UpdateSpawnPointPosition()
        {
            // If we have a spawn point transform, use that for positioning
            if (spawnPointTransform != null && spawnPointTransform.gameObject.activeInHierarchy)
            {
                // Update the local position relative to player based on current spawn point transform
                if (playerTransform != null)
                {
                    originalSpawnPointLocalPosition = playerTransform.InverseTransformPoint(spawnPointTransform.position);
                    originalSpawnPointWorldPosition = spawnPointTransform.position;
                }
            }
            // Otherwise update the world position based on the relative position to player
            else if (moveSpawnPointsWithPlayer && playerTransform != null && originalSpawnPointLocalPosition != Vector3.zero)
            {
                // Calculate the new world position based on relative position to player
                originalSpawnPointWorldPosition = playerTransform.TransformPoint(originalSpawnPointLocalPosition);
            }
        }
        
        // Public method to set the player transform reference
        public void SetPlayerTransform(Transform player)
        {
            bool hadNoPlayerBefore = playerTransform == null;
            playerTransform = player;
            
            // If we just got a player reference, recalculate the relative position
            if (hadNoPlayerBefore && player != null)
            {
                // When we get a player reference, update relative position based on world position
                if (originalSpawnPointWorldPosition != Vector3.zero)
                {
                    originalSpawnPointLocalPosition = player.InverseTransformPoint(originalSpawnPointWorldPosition);
                    Debug.Log($"Ship {name} updated relative position to {originalSpawnPointLocalPosition} after receiving player reference");
                }
                
                // Also update the formation behavior to follow by default
                followPlayerWhenIdle = true;
                returnToSpawnWhenIdle = true;
            }
        }

        private void OnEnable()
        {
            // This is called when the ship is enabled, including when returned from object pool
            if (playerTransform == null && GameMng.P != null)
            {
                SetPlayerTransform(GameMng.P.transform);
                Debug.Log($"Ship {name} set player reference in OnEnable");
            }
            
            // Reset our follow timer to update destination immediately
            playerFollowUpdateTimer = 0f;
            spawnPointUpdateTimer = 0f;
            
            // Force a position update
            UpdateSpawnPointPosition();
            
            // Reset destination if we need to move
            if (MySt != null)
            {
                Vector3 spawnPos = GetFormationPosition();
                MySt.Destination = spawnPos;
                Debug.Log($"Ship {name} immediately moving to spawn position {spawnPos} on enable");
            }
        }
    }
}
