using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using System.Linq;
using System.Collections;
using System;

namespace Cosmicrafts
{
    public enum Team
    {
        Blue,
        Red
    }

    public enum TypeDmg
    {
        Normal,
        Direct,
        Shield
    }

    public class Unit : MonoBehaviour
    {
        public event Action<Unit> OnDeath;
        public event Action<Unit> OnUnitDeath;
        protected int Id;
        protected NFTsUnit NFTs;
        public bool IsDeath;
        public int PlayerId = 1;
        public Team MyTeam;

        [Range(1, 999999)]
        public int HitPoints = 10;
        int MaxHp = 10;
        [Range(1, 999999)]
        public int Shield = 0;
        int MaxShield = 0;
        [Range(0, 10)]
        public float ShieldDelay = 3f;
        [Range(0.1f, 10)]
        public float Size = 1f;
        [Range(0, 30)]
        public float SpawnAreaSize = 0f;
        [Range(0, 1)]
        public float DodgeChance = 0f;
        [Range(1, 999)]
        public int Level = 1;

        [HideInInspector]
        public bool IsInmortal = false;
        [HideInInspector]
        public bool flagShield = false;
        [HideInInspector]
        protected bool Disabled = false;
        [HideInInspector]
        protected float Casting = 1f;

        float ShieldLoad = 0f;
        float ShieldCharge = 0f;
        float ShieldSpeed = 1f;

        protected SphereCollider TrigerBase;
        protected SphereCollider SolidBase;

        public GameObject Mesh;
        public GameObject SA;
        public GameObject Explosion;
        public GameObject Portal;
        public GameObject ShieldGameObject;
        public UIUnit UI;
        protected OutlineController MyOutline;
        [SerializeField]
        protected Animator MyAnim;
        protected AnimationClip[] MyClips;
        protected Vector3 LastImpact;

        protected Rigidbody MyRb;

        private float shieldVisualTimer = 0f;

        private void Awake()
        {
            //MyClips = MyAnim.runtimeAnimatorController.animationClips;
        }

        protected virtual void Start()
        {
            // IMMEDIATE HEALTH CHECK - absolutely critical to prevent instant death
            // Do this before ANYTHING else happens
            if (HitPoints <= 0)
            {
                Debug.LogError($"CRITICAL: Unit {gameObject.name} started with {HitPoints} HP! Forcing to 10 HP to prevent death.");
                HitPoints = 10;
                SetMaxHitPoints(10);
            }
            
            // Ensure the unit starts alive
            IsDeath = false;
            LastImpact = Vector3.zero;
            MaxShield = Shield;
            MaxHp = HitPoints;
            Level = Mathf.Clamp(Level, 1, 999);
            MyRb = GetComponent<Rigidbody>();
            MyOutline = Mesh.GetComponent<OutlineController>();
            if (MyOutline == null)
            {
                MyOutline = Mesh.AddComponent<OutlineController>();
            }
            TrigerBase = GetComponent<SphereCollider>();
            SolidBase = Mesh.GetComponent<SphereCollider>();

            // Debug UI initialization
            if (UI == null)
            {
                Debug.LogError($"UI component is null on {gameObject.name}!");
            }
            else
            {
                Debug.Log($"Initializing UI for {gameObject.name}, HP: {HitPoints}, MaxHP: {MaxHp}, Shield: {Shield}, MaxShield: {MaxShield}");
                
                // Make sure Canvas is active
                if (UI.Canvas != null && !UI.Canvas.activeSelf)
                {
                    Debug.LogWarning($"UI Canvas was inactive on {gameObject.name}, activating it");
                    UI.Canvas.SetActive(true);
                }
                
                // Explicit UI initialization with current values
                UI.Init(MaxHp, MaxShield);
                UI.SetHPBar((float)HitPoints / (float)MaxHp);
                UI.SetShieldBar((float)Shield / (float)MaxShield);
            
                // Add null check for GameMng.P
                if (GameMng.P != null) {
                    UI.SetColorBars(!IsMyTeam(GameMng.P.MyTeam));
                } else {
                    // Default behavior when player is not yet initialized
                    UI.SetColorBars(MyTeam != Team.Blue);
                }
            }
            
            if (MyOutline != null)
            {
                MyOutline.SetColor(GameMng.GM.GetColorUnit(MyTeam, PlayerId));
                MyOutline.SetThickness(Size * 0.0002f);
            }
            TrigerBase.radius = SolidBase.radius;
            transform.localScale = new Vector3(Size, Size, Size);
            MyAnim = Mesh.GetComponent<Animator>();
            
            // Make sure the animator is reset and not playing death animation
            if (MyAnim != null)
            {
                MyAnim.ResetTrigger("Die");
                MyAnim.SetBool("Idle", true);
            }
            
            // FIXED: Don't modify the Portal prefab reference directly
            if (Portal != null)
            {
                // Create a portal effect at spawn time
                GameObject portalInstance = Instantiate(Portal, transform.position, Quaternion.identity);
                Destroy(portalInstance, 3f);
            }
            
            transform.LookAt(CMath.LookToY(transform.position, GameMng.GM.GetDefaultTargetPosition(MyTeam)));
            
            // Add null check for GameMng.P
            if (GameMng.P != null) {
                SA.SetActive(IsMyTeam(GameMng.P.MyTeam) && SpawnAreaSize > 0f);
            } else {
                // Default behavior when player is not yet initialized
                SA.SetActive(MyTeam == Team.Blue && SpawnAreaSize > 0f);
            }
            
            GameMng.GM.AddUnit(this);
        }

        protected virtual void Update()
        {
            if (Casting > 0f)
            {
                Casting -= Time.deltaTime;
                if (Casting <= 0f)
                {
                    CastComplete();
                }
            }

            if (ShieldLoad > 0.1f)
            {
                ShieldLoad -= Time.deltaTime;
            }
            else if (Shield < MaxShield)
            {
                if (ShieldCharge < ShieldSpeed)
                {
                    ShieldCharge += Time.deltaTime;
                }
                else
                {
                    ShieldCharge = 12.5f;
                    Shield++;
                    UI.SetShieldBar((float)Shield / (float)MaxShield);
                }
            }
            
            // Handle shield visual timer without coroutines
            if (shieldVisualTimer > 0)
            {
                shieldVisualTimer -= Time.deltaTime;
                if (shieldVisualTimer <= 0 && ShieldGameObject != null)
                {
                    ShieldGameObject.SetActive(false);
                }
            }
        }

        protected virtual void FixedUpdate()
        {
            if (MyRb.linearVelocity.magnitude > 0f)
            {
                MyRb.linearVelocity = Vector3.zero;
            }
            if (MyRb.angularVelocity.magnitude > 0.5f)
            {
                MyRb.angularVelocity = Vector3.zero;
            }
            if (transform.rotation.x != 0f || transform.rotation.y != 0f)
            {
                transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            }
        }

        protected virtual void CastComplete()
        {
            MyAnim.SetBool("Idle", true);
            MyAnim.speed = 1;
            if (SpawnAreaSize > 0f && MyTeam == GameMng.P.MyTeam)
            {
                SA.SetActive(true);
                SA.transform.localScale = new Vector3(SpawnAreaSize, SpawnAreaSize);
            }
        }

        public void AddDmg(int dmg, TypeDmg typeDmg)
        {
            if (IsDeath || !InControl() || dmg <= 0)
                return;

            ShieldLoad = ShieldDelay;
            ShieldCharge = 0f;

            int DirectDmg = 0;

            if (Shield > 0 && typeDmg != TypeDmg.Direct)
            {
                Shield -= dmg;
                if (Shield < 0)
                {
                    HitPoints += Shield;
                    DirectDmg += Mathf.Abs(Shield);
                    Shield = 0;
                }
                UI.SetShieldBar((float)Shield / (float)MaxShield);
            }
            else if (typeDmg != TypeDmg.Shield)
            {
                HitPoints -= dmg;
                DirectDmg += dmg;
            }

            if (!IsMyTeam(GameMng.P.MyTeam))
            {
                GameMng.MT.AddDamage(DirectDmg);
            }

            if (HitPoints <= 0 && !IsInmortal)
            {
                HitPoints = 0;
                Die();
            }

            UI.SetHPBar((float)HitPoints / (float)MaxHp);
        }

        public void AddDmg(int dmg)
        {
            AddDmg(dmg, TypeDmg.Normal);
        }

        public virtual void Die()
        {
            // Prevent multiple deaths or death on spawn
            if (IsDeath)
            {
                Debug.LogWarning($"Die() called on already dead unit: {gameObject.name}");
                return;
            }
            
            HitPoints = 0;
            IsDeath = true;

            // Broadcast the death event
            OnDeath?.Invoke(this);
            OnUnitDeath?.Invoke(this);

            // Special handling for player's controllable character
            // Check if this unit belongs to the player instance referenced by GameMng.P
            bool isPlayerCharacter = (GameMng.P != null && GameMng.P.GetComponent<Unit>() == this);

            if (isPlayerCharacter)
            {
                // Don't hide UI or disable SolidBase for player's character
                // Respawn logic in GameMng will handle its state.
                Debug.Log($"Player character '{name}' died, but keeping visuals active for respawn.");
                // Note: OnUnitDeath event is still invoked, which triggers GameMng.HandlePlayerBaseStationDeath
            }
            else
            {
                // Regular death handling for other units (including enemy bases)
                UI.HideUI();
                SA.SetActive(false);
                if (MyAnim != null) MyAnim.SetTrigger("Die");
                if (SolidBase != null) SolidBase.enabled = false;

                // Destroy non-player units after a delay
                Destroy(gameObject, 2f); // Give time for death animation
            }
        }

        public virtual void DisableUnit()
        {
            Disabled = true;

            Ship ship = GetComponent<Ship>();

            if (ship != null)
                ship.CanMove = false;

            Shooter shooter = GetComponent<Shooter>();

            if (shooter != null)
                shooter.CanAttack = false;
        }

        public virtual void EnableUnit()
        {
            Disabled = false;

            Ship ship = GetComponent<Ship>();

            if (ship != null)
                ship.CanMove = true;

            Shooter shooter = GetComponent<Shooter>();

            if (shooter != null)
                shooter.CanAttack = true;
        }

        public bool IsMyTeam(Team other)
        {
            return other == MyTeam;
        }

        public bool GetIsDeath()
        {
            return IsDeath;
        }

        public bool GetIsDisabled()
        {
            return Disabled;
        }

        public bool GetIsCasting()
        {
            return Casting > 0f;
        }

        public virtual void DestroyUnit()
        {
            // If it's the player's controllable character, let GameMng handle it instead
            // This prevents actual destruction of the player's character
            bool isPlayerUnit = (GameMng.P != null && GameMng.P.GetComponent<Unit>() == this);
            if (isPlayerUnit)
            {
                // GameMng.HandlePlayerBaseStationDeath will be called through the OnUnitDeath event
                // which will handle respawning without destroying the base station
                return;
            }

            GameMng.GM.DeleteUnit(this);

            // For non-player units, still track kills
            if (GameMng.P != null && !IsMyTeam(GameMng.P.MyTeam))
            {
                GameMng.MT.AddKills(1);
            }

            Destroy(gameObject);
        }

        public void BlowUpEffect()
        {
            // Prefer using the pooled VFX system if available
            float scaleMultiplier = transform.localScale.x * 1.8f;

            if (VFXPool.Instance != null)
            {
                // This will play a random explosion effect from the pool's configured "Explosions" category
                VFXPool.Instance.PlayExplosion(transform.position, scaleMultiplier);
                return;
            }

            // Fallback to the old behaviour if VFXPool is missing (e.g., in editor tests)
            if (Explosion == null)
            {
                Debug.LogWarning($"Cannot create explosion effect for {gameObject.name} - Explosion prefab reference is missing and VFXPool not found.");
                return;
            }

            GameObject explosion = Instantiate(Explosion, transform.position, Quaternion.identity);
            explosion.transform.localScale = Vector3.one * scaleMultiplier;
            Destroy(explosion, 4f);
        }

        public void SetImpactPosition(Vector3 position)
        {
            LastImpact = position;
        }

        public void setId(int id)
        {
            Id = id;
        }

        public int getId()
        {
            return Id;
        }

        public string getKey()
        {
            return NFTs == null ? string.Empty : NFTs.KeyId;
        }

        public int GetPlayerId()
        {
            return PlayerId;
        }

        public Animator GetAnimator()
        {
            return MyAnim;
        }

        public AnimationClip GetAnimationClip(string name)
        {
            return MyClips == null ? null : MyClips.FirstOrDefault(f => f.name == name);
        }

        public int GetMaxShield()
        {
            return MaxShield;
        }

        public void SetMaxShield(int maxshield)
        {
            MaxShield = maxshield;
        }

        public void SetMaxHitPoints(int maxhp)
        {
            MaxHp = maxhp;
            UI.SetHPBar((float)HitPoints / (float)MaxHp);
        }

        public int GetMaxHitPoints()
        {
            return MaxHp;
        }

        public virtual void SetNfts(NFTsUnit nFTsUnit)
        {
            NFTs = nFTsUnit;

            if (nFTsUnit == null)
                return;

            HitPoints = nFTsUnit.HitPoints;
            MaxHp = HitPoints;
            Shield = nFTsUnit.Shield;
            MaxShield = Shield;
            Level = nFTsUnit.Level;

            GetComponent<Shooter>()?.InitStatsFromNFT(nFTsUnit);
        }

        public bool InControl()
        {
            return (!Disabled && Casting <= 0f);
        }

        public void OnImpactShield(int dmg)
        {
            // Early return if object is inactive or destroyed
            if (this == null || !gameObject || !gameObject.activeInHierarchy || IsDeath)
            {
                return;
            }
            
            // Activate shield visual if available
            if (ShieldGameObject != null)
            {
                ShieldGameObject.SetActive(true);
                // Set timer instead of using coroutine
                shieldVisualTimer = 1f;
            }
            
            // Apply damage
            AddDmg(dmg);
        }

        public int GetLevel()
        {
            return Level;
        }

        public void SetLevel(int newLevel)
        {
            Level = Mathf.Clamp(newLevel, 1, 99);
        }

        public virtual void MoveTo(Vector3 position)
        {
            Ship ship = GetComponent<Ship>();
            if (ship != null)
            {
                ship.SetDestination(position, ship.StoppingDistance);
            }
            else
            {
                Debug.LogWarning($"MoveTo called on {name}, but no Ship component was found.");
            }
        }

        public virtual void ResetUnit()
        {
           // Debug.Log($"[Unit.ResetUnit] Resetting unit {gameObject.name} (ID: {Id})");
            
            // Reset the unit to its initial state for reuse in object pooling
            IsDeath = false;
            Disabled = false;
            Casting = 0f;
            
            // Make sure colliders are enabled
            if (SolidBase != null) 
                SolidBase.enabled = true;
            
            // Re-enable GameObject if it was disabled
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            
            // Reset health and shield to their maximum values
            HitPoints = MaxHp;
            Shield = MaxShield;
            
            // Reset UI elements
            if (UI != null) {
                UI.SetHPBar(1f);
                UI.SetShieldBar(MaxShield > 0 ? (float)Shield / (float)GetMaxShield() : 0f);
                // Show UI
                if (UI.Canvas != null)
                    UI.Canvas.SetActive(true);
            }
            
            // Reset animation state if needed
            if (MyAnim != null) {
                // Ensure animator controller is valid before resetting triggers
                if (MyAnim.runtimeAnimatorController != null) {
                    // Force a complete reset of the animator state
                    MyAnim.Rebind();
                    MyAnim.Update(0f);
                    
                    // Clear death trigger and set idle
                    MyAnim.ResetTrigger("Die");
                    MyAnim.SetBool("Idle", true);
                    
                    Debug.Log($"Completely reset animator for {gameObject.name}");
                } else {
                    //Debug.LogWarning($"Animator on {gameObject.name} has no controller.");
                }
            }
            
            // Reset any ship-specific components
            Ship ship = GetComponent<Ship>();
            if (ship != null)
                ship.ResetShip();
            
            Shooter shooter = GetComponent<Shooter>();
            if (shooter != null)
                shooter.ResetShooter();
            
            // Reactivate spawn area if applicable
            if (SA != null && SpawnAreaSize > 0f && IsMyTeam(GameMng.P.MyTeam))
                SA.SetActive(true);
            
            // Create portal effect for respawn if Portal prefab exists
            if (Portal != null) {
                GameObject portal = Instantiate(Portal, transform.position, Quaternion.identity);
                Destroy(portal, 3f);
            }
            
            //Debug.Log($"[Unit.ResetUnit] Unit reset complete at position {transform.position}");
        }
    }
}
