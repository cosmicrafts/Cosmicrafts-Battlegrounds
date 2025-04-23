using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System;
using Cosmicrafts.Units; // Required for CompanionController

namespace Cosmicrafts
{
    // Expanded team enum with potential for more factions
    public enum Faction
    {
        Player,     // Player's faction (previously Blue team + PlayerId 1)
        Enemy,      // Enemy/Bot faction (previously Red team + PlayerId 2) 
        Neutral     // Optional: For non-hostile entities
    }

    // Relationship between factions
    public enum FactionRelationship
    {
        Friendly,
        Hostile,
        Neutral
    }

    public enum TypeDmg
    {
        Normal,
        Direct,
        Shield
    }

    // Static class to manage faction relationships
    public static class FactionManager
    {
        // Relationship matrix defining how factions interact with each other
        private static FactionRelationship[,] relationshipMatrix = {
            // Player  Enemy  Neutral
            { FactionRelationship.Friendly, FactionRelationship.Hostile, FactionRelationship.Neutral }, // Player's relationships
            { FactionRelationship.Hostile, FactionRelationship.Friendly, FactionRelationship.Neutral }, // Enemy's relationships
            { FactionRelationship.Neutral, FactionRelationship.Neutral, FactionRelationship.Neutral }   // Neutral's relationships
        };

        // Get relationship between two factions
        public static FactionRelationship GetRelationship(Faction a, Faction b)
        {
            return relationshipMatrix[(int)a, (int)b];
        }
        
        // Backwards compatibility for Team -> Faction conversion
        public static Faction ConvertTeamToFaction(Team team)
        {
            return team == Team.Blue ? Faction.Player : Faction.Enemy;
        }
        
        // Backwards compatibility for PlayerId -> Faction conversion
        public static Faction ConvertPlayerIdToFaction(int playerId)
        {
            return playerId == 1 ? Faction.Player : Faction.Enemy;
        }
        
        // Backwards compatibility: Convert Faction to legacy Team
        public static Team ConvertFactionToTeam(Faction faction)
        {
            return faction == Faction.Player ? Team.Blue : Team.Red;
        }
    }

    // Keep old Team enum for backward compatibility
    public enum Team
    {
        Blue,
        Red
    }

    public class Unit : MonoBehaviour
    {
        public event Action<Unit> OnDeath;
        public event Action<Unit> OnUnitDeath;
        protected int Id;
        protected NFTsUnit NFTs;
        public bool IsDeath;
        
        // New unified faction system
        [Header("Faction Identity")]
        public Faction MyFaction = Faction.Player;
        
        // Keep for backwards compatibility but mark obsolete
        [System.Obsolete("Use MyFaction instead")]
        public int PlayerId 
        {
            get { return MyFaction == Faction.Player ? 1 : 2; }
            set { MyFaction = value == 1 ? Faction.Player : Faction.Enemy; }
        }
        
        // Keep for backwards compatibility but mark obsolete
        [System.Obsolete("Use MyFaction instead")]
        public Team MyTeam
        {
            get { return MyFaction == Faction.Player ? Team.Blue : Team.Red; }
            set { MyFaction = value == Team.Blue ? Faction.Player : Faction.Enemy; }
        }

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

        [Header("Companion System")]
        [Tooltip("List of companion configurations")]
        public List<CompanionConfig> companions = new List<CompanionConfig>();
        
        // Track active companion instances
        private List<Unit> activeCompanions = new List<Unit>();

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
              //  Debug.Log($"Initializing UI for {gameObject.name}, HP: {HitPoints}, MaxHP: {MaxHp}, Shield: {Shield}, MaxShield: {MaxShield}");
                
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
                    UI.SetColorBars(MyFaction != GameMng.P.MyFaction);
                } else {
                    // Default behavior when player is not yet initialized
                    UI.SetColorBars(MyFaction == Faction.Enemy);
                }
            }
            
            if (MyOutline != null)
            {
                MyOutline.SetColor(GameMng.GM.GetColorUnit(MyFaction));
                MyOutline.SetThickness(Size * 0.00000420f);
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
            
            transform.LookAt(CMath.LookToY(transform.position, GameMng.GM.GetDefaultTargetPosition(MyFaction)));
            
            // Add null check for GameMng.P
            if (GameMng.P != null) {
                SA.SetActive(MyFaction == GameMng.P.MyFaction && SpawnAreaSize > 0f);
            } else {
                // Default behavior when player is not yet initialized
                SA.SetActive(MyFaction == Faction.Player && SpawnAreaSize > 0f);
            }
            
            GameMng.GM.AddUnit(this);
            
            // Spawn companions
            SpawnCompanions();
            
            // Register VFX with the pool
            RegisterVFXWithPool();
        }

        /// <summary>
        /// Registers this unit's VFX with the VFXPool for efficient reuse
        /// </summary>
        protected virtual void RegisterVFXWithPool()
        {
            // Skip if VFXPool isn't available
            if (VFXPool.Instance == null || Id <= 0) return;
            
            // Register death explosion
            if (Explosion != null)
            {
                VFXPool.Instance.RegisterUnitExplosion(Id, Explosion);
            }
            
            // Register shield impact - if available
            if (ShieldGameObject != null)
            {
                // Try to find child particle system to use as shield impact
                ParticleSystem[] particleSystems = ShieldGameObject.GetComponentsInChildren<ParticleSystem>(true);
                if (particleSystems.Length > 0)
                {
                    GameObject shieldEffect = particleSystems[0].gameObject;
                    VFXPool.Instance.RegisterUnitShieldImpact(Id, shieldEffect);
                }
            }
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
            if (SpawnAreaSize > 0f && MyFaction == GameMng.P.MyFaction)
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
                
                // Play shield impact VFX using pool
                if (VFXPool.Instance != null)
                {
                    VFXPool.Instance.PlayShieldImpact(transform.position, Quaternion.identity, transform.localScale.x, Id);
                }
                else if (ShieldGameObject != null)
                {
                    // Fallback to original behavior
                    ShieldGameObject.SetActive(true);
                    shieldVisualTimer = 1f;
                }
                
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
                // Play armor impact VFX using pool
                if (VFXPool.Instance != null)
                {
                    VFXPool.Instance.PlayArmorImpact(transform.position, Quaternion.identity, transform.localScale.x, Id);
                }
                
                HitPoints -= dmg;
                DirectDmg += dmg;
            }

            if (!IsMyTeam(GameMng.P.MyFaction))
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

            // Make sure shield visual is disabled
            if (ShieldGameObject != null)
            {
                ShieldGameObject.SetActive(false);
                shieldVisualTimer = 0f;
            }

            // Broadcast the death event - this will trigger GameMng.HandlePlayerBaseStationDeath for player
            OnDeath?.Invoke(this);
            OnUnitDeath?.Invoke(this);

            // Standard death handling for all units, including player
            UI.HideUI();
            if (SA != null) SA.SetActive(false);
            if (MyAnim != null) MyAnim.SetTrigger("Die");
            if (SolidBase != null) SolidBase.enabled = false;
            
            // Handle companion death/destruction
            DestroyCompanions();
            
            // Special player death handling
            bool isPlayerCharacter = (GameMng.P != null && GameMng.P.GetComponent<Unit>() == this);
            if (isPlayerCharacter)
            {
                // Dramatic camera effect
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    CameraController cameraController = mainCamera.GetComponent<CameraController>();
                    if (cameraController != null)
                    {
                        cameraController.StartDeathSequence();
                    }
                }
                
                // Create visual soul effect
                StartCoroutine(TransformToSoulState());
                
                Debug.Log($"PLAYER DEATH HANDLED: {name} - starting soul transformation");
            }
            else
            {
                // For non-player units, destroy after animation time
                Destroy(gameObject, 2f); // Give time for death animation
            }
        }

        // Soul state transformation
        private IEnumerator TransformToSoulState()
        {
            // Wait for death animation
            yield return new WaitForSeconds(1.5f);
            
            // Apply ghost/soul effect
            if (Mesh != null)
            {
                // Make the mesh ghostly
                Renderer[] renderers = Mesh.GetComponentsInChildren<Renderer>();
                foreach (Renderer rend in renderers)
                {
                    // Store original materials for restoration
                    Material[] originalMaterials = rend.materials;
                    Material[] soulMaterials = new Material[originalMaterials.Length];
                    
                    // Create soul versions of each material
                    for (int i = 0; i < originalMaterials.Length; i++)
                    {
                        soulMaterials[i] = new Material(originalMaterials[i]);
                        Color soulColor = new Color(0.7f, 0.7f, 1f, 0.7f); // Ethereal blue-white
                        soulMaterials[i].color = soulColor;
                        
                        // Try to make it transparent if shader supports it
                        if (soulMaterials[i].HasProperty("_Mode"))
                        {
                            soulMaterials[i].SetFloat("_Mode", 3); // Transparent mode
                            soulMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            soulMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            soulMaterials[i].SetInt("_ZWrite", 0);
                            soulMaterials[i].DisableKeyword("_ALPHATEST_ON");
                            soulMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                            soulMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            soulMaterials[i].renderQueue = 3000;
                        }
                    }
                    
                    // Apply soul materials
                    rend.materials = soulMaterials;
                }
            }
            
            // Update camera to grayscale effect
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                CameraController cameraController = mainCamera.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    cameraController.StartDeathSequence();
                }
            }
            
            // Add basic explosion effect - safely check for existence
            if (Explosion != null)
            {
                GameObject effect = Instantiate(Explosion, transform.position, Quaternion.identity);
                effect.transform.localScale = transform.localScale * 0.5f;
                Destroy(effect, 2f);
            }
            
            // Show respawn UI with countdown
            ShowRespawnUI();
        }
        
        // Show respawn UI with countdown
        private void ShowRespawnUI()
        {
            if (GameMng.UI != null)
            {
                // Check if UI has a method to show respawn UI
                GameMng.UI.ShowRespawnCountdown(GameMng.GM.respawnDelay);
            }
            else
            {
                Debug.LogWarning("GameMng.UI is null, cannot show respawn UI");
            }
        }
        
        // Method that can be called by a respawn button
        public void TriggerRespawn()
        {
            if (IsDeath && GameMng.P != null && GameMng.P.GetComponent<Unit>() == this)
            {
                // Notify the game manager to respawn player immediately
                if (GameMng.GM != null)
                {
                    GameMng.GM.ForcePlayerRespawn();
                }
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

        public bool IsMyTeam(Team team)
        {
            return FactionManager.ConvertFactionToTeam(MyFaction) == team;
        }

        public bool IsMyTeam(Faction other)
        {
            return MyFaction == other;
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
            if (GameMng.P != null && !IsMyTeam(GameMng.P.MyFaction))
            {
                GameMng.MT.AddKills(1);
            }

            Destroy(gameObject);
        }

        public void BlowUpEffect()
        {
            // Early exit if the unit is already destroyed or inactive
            if (this == null || !gameObject || !gameObject.activeInHierarchy)
            {
                return;
            }

            // Use unit-specific explosion from pool if available
            float scaleMultiplier = transform.localScale.x * 1.8f;

            if (VFXPool.Instance != null)
            {
                VFXPool.Instance.PlayUnitExplosion(transform.position, scaleMultiplier, Id);
                return;
            }

            // Fallback to the old behaviour if VFXPool is missing (e.g., in editor tests)
            if (Explosion == null)
            {
                Debug.LogWarning($"Cannot create explosion effect for {gameObject.name} - Explosion prefab reference is missing and VFXPool not found.");
                return;
            }

            // Create explosion with shorter lifetime (2s instead of 4s)
            GameObject explosion = Instantiate(Explosion, transform.position, Quaternion.identity);
            explosion.transform.localScale = Vector3.one * scaleMultiplier;
            
            // Add a dedicated component to handle cleanup even if the parent is destroyed
            DestroyAfterTime destroyComponent = explosion.AddComponent<DestroyAfterTime>();
            destroyComponent.timeToDestroy = 2f;
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

        public Faction GetFaction()
        {
            return MyFaction;
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
            
            // Use pooled shield impact effect if available
            if (VFXPool.Instance != null)
            {
                VFXPool.Instance.PlayShieldImpact(transform.position, Quaternion.identity, transform.localScale.x, Id);
            }
            // Fallback to old behavior
            else if (ShieldGameObject != null)
            {
                ShieldGameObject.SetActive(true);
                // Set timer instead of using coroutine
                shieldVisualTimer = 1f;
                
                // Ensure shield will be deactivated if the unit is destroyed while shield is active
                if (!ShieldGameObject.TryGetComponent<DestroyAfterTime>(out var destroyComponent))
                {
                    destroyComponent = ShieldGameObject.AddComponent<DestroyAfterTime>();
                    destroyComponent.timeToDestroy = 1.5f; // Slightly longer than visual timer
                }
                else
                {
                    // Reset the existing timer
                    destroyComponent.timeToDestroy = 1.5f;
                }
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
            if (SA != null && SpawnAreaSize > 0f && IsMyTeam(GameMng.P.MyFaction))
                SA.SetActive(true);
            
            // Create portal effect for respawn if Portal prefab exists
            if (Portal != null) {
                GameObject portal = Instantiate(Portal, transform.position, Quaternion.identity);
                Destroy(portal, 3f);
            }
            
            // Respawn companions
            SpawnCompanions();
            
            // Make sure VFX are registered with the pool
            RegisterVFXWithPool();
        }

        /// <summary>
        /// Instantiates and initializes all companion units based on configurations.
        /// </summary>
        protected virtual void SpawnCompanions()
        {
            // Clear any existing companions to avoid duplicates
            DestroyCompanions();
            
            // Skip if no companions are configured
            if (companions == null || companions.Count == 0)
                return;
                
            // Spawn each configured companion
            foreach (CompanionConfig config in companions)
            {
                if (config == null || config.unitData == null)
                    continue;
                    
                // Instantiate the companion prefab
                GameObject companionGO = Instantiate(config.unitData.prefab, transform.position, transform.rotation);
                Unit companionUnit = companionGO.GetComponent<Unit>();
                
                if (companionUnit != null)
                {
                    // IMPORTANT: Set basic properties first before initializing controllers
                    // This ensures the companion's team and ID are set before any behavior logic runs
                    companionUnit.MyFaction = this.MyFaction;
                    
                    // Explicitly enforce friendly status with shooter if present
                    Shooter companionShooter = companionGO.GetComponent<Shooter>();
                    if (companionShooter != null)
                    {
                        // Immediately stop any active attacks to ensure it doesn't target parent
                        companionShooter.StopAttack();
                    }
                    
                    // Get or add CompanionController component (now added dynamically)
                    CompanionController controller = companionGO.GetComponent<CompanionController>();
                    if (controller == null)
                    {
                        // Add the controller component only if not already present
                        controller = companionGO.AddComponent<CompanionController>();
                        Debug.Log($"Added CompanionController to {companionGO.name} dynamically");
                    }
                    
                    // Apply configuration settings
                    controller.SetParent(this);
                    controller.ApplyConfiguration(config);
                    
                    // Add to tracking list
                    activeCompanions.Add(companionUnit);
                    
                    // Apply custom behavior type if specified
                    if (config.behaviorType != CompanionBehaviorType.Default)
                    {
                        SetupCompanionBehavior(companionGO, config.behaviorType);
                    }
                    
                    Debug.Log($"{gameObject.name} spawned companion {companionGO.name} with behavior: {config.behaviorType}");
                }
                else
                {
                    Debug.LogError($"Companion prefab {config.unitData.prefab.name} is missing the Unit component!", config.unitData.prefab);
                    Destroy(companionGO); // Clean up invalid instance
                }
            }
        }
        
        /// <summary>
        /// Sets up specialized companion behavior based on the behavior type.
        /// </summary>
        private void SetupCompanionBehavior(GameObject companionGO, CompanionBehaviorType behaviorType)
        {
            // Make sure the companion has all necessary components for the behavior type
            Shooter shooter = companionGO.GetComponent<Shooter>();
            Ship companionShip = companionGO.GetComponent<Ship>();
            Unit companionUnit = companionGO.GetComponent<Unit>();
            CompanionController controller = companionGO.GetComponent<CompanionController>();
            
            // Controller is essential for all behaviors - should be added by SpawnCompanions but check again
            if (controller == null)
            {
                controller = companionGO.AddComponent<CompanionController>();
                controller.SetParent(this);
            }
            
            switch (behaviorType)
            {
                case CompanionBehaviorType.Attacker:
                    // Make sure the companion has a shooter component
                    if (shooter == null)
                    {
                        shooter = companionGO.AddComponent<Shooter>();
                        // FIXED: Use AttackRange instead of deprecated RangeDetector  
                        shooter.AttackRange = 10f;  // Default range
                        shooter.CoolDown = 0.5f;      // Fast firing rate
                        shooter.BulletDamage = 2;    // Lower damage than typical units
                        shooter.DetectionRange = shooter.AttackRange * 1.2f; // Set detection range larger than attack range
                        
                        // Create EnemyDetector if needed
                        if (shooter.EnemyDetector == null)
                        {
                            GameObject detectorObj = new GameObject("EnemyDetector");
                            detectorObj.transform.parent = companionGO.transform;
                            detectorObj.transform.localPosition = Vector3.zero;
                            
                            SphereCollider detector = detectorObj.AddComponent<SphereCollider>();
                            detector.isTrigger = true;
                            detector.radius = shooter.DetectionRange;
                            
                            shooter.EnemyDetector = detector;
                        }
                        
                        // Try to find a bullet prefab from parent if available
                        Shooter parentShooter = GetComponent<Shooter>();
                        if (parentShooter != null && parentShooter.Bullet != null)
                        {
                            shooter.Bullet = parentShooter.Bullet;
                            shooter.Cannons = new Transform[] { companionGO.transform };
                        }
                        else
                        {
                            // Try to load a default bullet prefab
                            GameObject bulletPrefab = Resources.Load<GameObject>("Prefabs/Bullets/SmallBullet");
                            if (bulletPrefab != null)
                            {
                                shooter.Bullet = bulletPrefab;
                                shooter.Cannons = new Transform[] { companionGO.transform };
                            }
                            else
                            {
                                Debug.LogWarning("No bullet prefab found for attacker companion. Please assign one manually.");
                            }
                        }
                    }
                    
                    // Make sure Cannons is set up with at least one transform
                    if (shooter.Cannons == null || shooter.Cannons.Length == 0)
                    {
                        shooter.Cannons = new Transform[] { companionGO.transform };
                    }
                    
                    // Ensure ranges are consistent
                    if (shooter != null)
                    {
                        shooter.AttackRange = 10f;
                        shooter.DetectionRange = 12f;
                        shooter.UpdateRangeVisualizer();
                        shooter.CanAttack = true;
                        
                        // Force initial enemy detection
                        if (controller != null)
                        {
                            controller.ForceEnemyDetection();
                        }
                    }
                    break;
                    
                case CompanionBehaviorType.Healer:
                    // Make the companion heal the parent unit periodically
                    // Uses the parent's existing HitPoints and UI
                    StartCoroutine(HealParentCoroutine(companionUnit, 1f, 1f)); // 1 HP per second
                    
                    // Set visual parameters for recognition
                    if (controller != null)
                    {
                        // Create a healing effect by making the companion orbit closer
                        controller.orbitDistance = 2f;
                    }
                    break;
                    
                case CompanionBehaviorType.Shield:
                    // Make the companion boost the parent's shield
                    // This can directly modify the parent unit's Shield property
                    int shieldBonus = 5;
                    SetMaxShield(GetMaxShield() + shieldBonus);
                    Shield += shieldBonus;
                    UI.SetShieldBar((float)Shield / (float)GetMaxShield());
                    
                    // Also start a coroutine to regenerate shield faster
                    StartCoroutine(ShieldRegenerateCoroutine(2f)); // 2 shield per second
                    
                    // Set visual parameters for recognition
                    if (controller != null)
                    {
                        // Set the companion to orbit close and slow
                        controller.orbitDistance = 2.5f;
                        controller.orbitSpeed = 45f;
                    }
                    break;
                    
                case CompanionBehaviorType.Scout:
                    // Create Ship component if needed
                    if (companionShip == null)
                    {
                        companionShip = companionGO.AddComponent<Ship>();
                        companionShip.MaxSpeed = 10f;
                        companionShip.TurnSpeed = 5f;
                    }
                    
                    // Increase movement speed and detection range for scouting
                    if (controller != null)
                    {
                        controller.orbitDistance = 8f; // Orbit further out
                        controller.orbitSpeed = 120f; // Move faster
                    }
                    
                    // If it has a shooter, increase detection range
                    if (shooter != null)
                    {
                        // FIXED: Use DetectionRange instead of deprecated RangeDetector
                        shooter.DetectionRange = 15f; // Scouts have excellent detection
                        shooter.AttackRange = Mathf.Min(10f, shooter.DetectionRange); // Keep attack range reasonable
                        shooter.UpdateRangeVisualizer();
                    }
                    
                    // If it has a ship component, increase its speed
                    if (companionShip != null)
                    {
                        companionShip.MaxSpeed *= 1.5f;
                    }
                    break;
            }
        }
        
        // Coroutine to heal the parent unit periodically
        private IEnumerator HealParentCoroutine(Unit companion, float healAmount, float interval)
        {
            while (companion != null && !companion.GetIsDeath() && !this.GetIsDeath())
            {
                yield return new WaitForSeconds(interval);
                
                // Only heal if parent is not at full health
                if (HitPoints < GetMaxHitPoints())
                {
                    HitPoints += Mathf.CeilToInt(healAmount);
                    if (HitPoints > GetMaxHitPoints())
                    {
                        HitPoints = GetMaxHitPoints();
                    }
                    
                    // Update the UI
                    UI.SetHPBar((float)HitPoints / (float)GetMaxHitPoints());
                }
            }
        }
        
        // Coroutine to regenerate shield faster
        private IEnumerator ShieldRegenerateCoroutine(float regenRate)
        {
            Ship shipComponent = GetComponent<Ship>();
            bool hasShipComponent = (shipComponent != null);
            
            while (!this.GetIsDeath())
            {
                yield return new WaitForSeconds(0.5f);
                
                // Only regenerate if the shield isn't full and we can regenerate
                if (Shield < GetMaxShield())
                {
                    // Check if we can regenerate shield based on Ship component rules
                    bool canRegen = true;
                    if (hasShipComponent)
                    {
                        canRegen = shipComponent.CanRegenerateShield();
                    }
                    
                    if (canRegen)
                    {
                        Shield += Mathf.CeilToInt(regenRate * 0.5f); // Adjust for half-second interval
                        if (Shield > GetMaxShield())
                        {
                            Shield = GetMaxShield();
                        }
                        
                        // Update the UI
                        UI.SetShieldBar((float)Shield / (float)GetMaxShield());
                    }
                }
            }
        }

        /// <summary>
        /// Destroys all active companion instances.
        /// </summary>
        protected virtual void DestroyCompanions()
        {
            foreach (Unit companion in activeCompanions.ToList())
            {
                if (companion != null)
                {
                    // Use Destroy instead of DestroyUnit to avoid potential recursion
                    Destroy(companion.gameObject);
                }
            }
            activeCompanions.Clear();
        }

        // --- Team Relationship Methods ---

        /// <summary>
        /// Checks if the provided unit is an ally (same faction, not self).
        /// </summary>
        public bool IsAlly(Unit otherUnit)
        {
            if (otherUnit == null || otherUnit == this) // Null or self is not an ally
                return false;
            
            // Use FactionManager to determine relationship
            return FactionManager.GetRelationship(this.MyFaction, otherUnit.MyFaction) == FactionRelationship.Friendly;
        }

        /// <summary>
        /// Checks if the provided unit is an enemy (hostile faction).
        /// </summary>
        public bool IsEnemy(Unit otherUnit)
        {
            if (otherUnit == null || otherUnit == this) // Null or self is not an enemy
                return false;
            
            // Use FactionManager to determine relationship
            return FactionManager.GetRelationship(this.MyFaction, otherUnit.MyFaction) == FactionRelationship.Hostile;
        }
        
        /// <summary>
        /// Checks if the provided unit is neutral towards this unit.
        /// </summary>
        public bool IsNeutral(Unit otherUnit)
        {
            if (otherUnit == null || otherUnit == this) // Null or self cannot be neutral
                return false;
            
            // Use FactionManager to determine relationship
            return FactionManager.GetRelationship(this.MyFaction, otherUnit.MyFaction) == FactionRelationship.Neutral;
        }

        // Now that GameMng.GetUnitsListClone exists, update to use it
        public Unit GetClosestEnemy(float maxDistance = 999999999f)
        {
            Unit enemyOutput = null;
            float distClosest = maxDistance;

            if (GameMng.GM != null)
            {
                foreach (Unit unit in GameMng.GM.GetUnitsListClone())
                {
                    if (unit != null && !unit.IsDeath && unit.MyFaction != MyFaction)
                    {
                        float distance = Vector3.Distance(transform.position, unit.transform.position);
                        if (distance < distClosest)
                        {
                            distClosest = distance;
                            enemyOutput = unit;
                        }
                    }
                }
            }

            return enemyOutput;
        }
    }
    
    /// <summary>
    /// Defines different companion behavior types
    /// </summary>
    public enum CompanionBehaviorType
    {
        Default,    // Standard orbiting behavior
        Attacker,   // Focuses on attacking enemies
        Healer,     // Provides healing to parent
        Shield,     // Enhances parent's shield
        Scout       // Orbits at greater distance and detects enemies
    }
    
    /// <summary>
    /// Configuration data for a companion
    /// </summary>
    [System.Serializable]
    public class CompanionConfig
    {
        public string name = "Companion";
        [Tooltip("Scriptable object defining the companion unit")]
        public UnitsDataBase unitData;
        
        [Header("Behavior")]
        public CompanionBehaviorType behaviorType = CompanionBehaviorType.Default;
        
        [Header("Orbit Settings")]
        [Range(1f, 10f)]
        public float orbitDistance = 3.0f;
        [Range(10f, 180f)]
        public float orbitSpeed = 90.0f;
        
        [Header("Appearance")]
        public Color tintColor = Color.white;
        [Range(0.5f, 2.0f)]
        public float scale = 1.0f;
    }
}
