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

        Transform Target;
        public RaySensor[] AvoidanceSensors;
        public GameObject[] Thrusters;

        [HideInInspector]
        public bool CanMove = true;

        Vector3 DeathRot;
        Vector3 moveDirection; // Store the current movement direction
        Quaternion targetRotation; // Store the target rotation
        
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
        }

        protected override void Update()
        {
            base.Update();
            Move();
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

            if (Target != null)
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
            // Reset movement variables
            Speed = 0f;
            DeathRot = Vector3.zero;
            moveDirection = Vector3.zero;
            
            // Re-enable SteeringRig and set target
            if (MySt != null)
            {
                MySt.enabled = true;
                Target = GameMng.GM.GetFinalTransformTarget(MyTeam);
                MySt.Destination = Target.position;
                MySt.StoppingDistance = StoppingDistance;
            }
            
            // Reset thrusters
            EnableThrusters(false);
            
            // Reset rotation alignment
            AlignRotationWithMovement = true;
            targetRotation = transform.rotation;
            
            // Re-enable movement
            CanMove = true;
        }
    }
}
