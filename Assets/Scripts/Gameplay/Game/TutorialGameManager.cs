using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts 
{
    /// <summary>
    /// Lightweight game manager for tutorials
    /// Handles core game functionality without unnecessary complexity
    /// </summary>
    public class TutorialGameManager : MonoBehaviour
    {
        // Singleton instance
        public static TutorialGameManager Instance { get; private set; }
        
        [Header("Base Stations")]
        [Tooltip("Positions for bases: [0]=Enemy, [1]=Player")]
        public Vector3[] basePositions;
        
        [Header("Characters")]
        [Tooltip("Player character prefab")]
        public GameObject playerPrefab;
        [Tooltip("Enemy character prefab")]
        public GameObject enemyPrefab;
        
        [Header("Gameplay Settings")]
        [Tooltip("Time delay before player respawn")]
        public float respawnDelay = 3f;
        [Tooltip("Optional visual effect for respawn")]
        public GameObject respawnEffectPrefab;
        
        // References to base stations
        private Unit playerBase;
        private Unit enemyBase;
        
        // Track all active units
        private List<Unit> activeUnits = new List<Unit>();
        
        // Game state
        private bool isGameOver = false;
        private bool isRespawning = false;
        
        // References to player components
        private Player playerComponent;
        
        void Awake()
        {
            // Setup singleton
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            Debug.Log("Tutorial Game Manager initialized");
        }
        
        void Start()
        {
            // Spawn bases
            SpawnPlayerBase();
            SpawnEnemyBase();
        }
        
        #region Spawning
        
        /// <summary>
        /// Spawns the player base at the designated position
        /// </summary>
        private void SpawnPlayerBase()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("Player prefab not assigned in TutorialGameManager!");
                return;
            }
            
            // Spawn at player base position
            GameObject playerObj = Instantiate(playerPrefab, basePositions[1], Quaternion.identity);
            
            // Get components
            playerBase = playerObj.GetComponent<Unit>();
            playerComponent = playerObj.GetComponent<Player>();
            
            if (playerBase == null || playerComponent == null)
            {
                Debug.LogError("Player prefab is missing Unit or Player component!");
                return;
            }
            
            // Setup player unit
            playerBase.MyFaction = Faction.Player;
            playerBase.IsDeath = false;
            
            // Ensure minimum health
            if (playerBase.HitPoints <= 0)
            {
                playerBase.SetMaxHitPoints(100);
                playerBase.HitPoints = 100;
            }
            
            // Register unit
            RegisterUnit(playerBase);
            
            // Subscribe to death event
            playerBase.OnUnitDeath += HandlePlayerDeath;
            
            Debug.Log("Player base spawned successfully");
        }
        
        /// <summary>
        /// Spawns the enemy base at the designated position
        /// </summary>
        private void SpawnEnemyBase()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("Enemy prefab not assigned in TutorialGameManager!");
                return;
            }
            
            // Spawn at enemy base position
            GameObject enemyObj = Instantiate(enemyPrefab, basePositions[0], Quaternion.identity);
            
            // Get Unit component
            enemyBase = enemyObj.GetComponent<Unit>();
            
            if (enemyBase == null)
            {
                Debug.LogError("Enemy prefab is missing Unit component!");
                return;
            }
            
            // Setup enemy unit
            enemyBase.MyFaction = Faction.Enemy;
            enemyBase.IsDeath = false;
            
            // Ensure minimum health
            if (enemyBase.HitPoints <= 0)
            {
                enemyBase.SetMaxHitPoints(100);
                enemyBase.HitPoints = 100;
            }
            
            // Register unit
            RegisterUnit(enemyBase);
            
            Debug.Log("Enemy base spawned successfully");
        }
        
        /// <summary>
        /// Creates a new unit at the specified position
        /// </summary>
        public Unit CreateUnit(GameObject unitPrefab, Vector3 position, Faction faction)
        {
            if (unitPrefab == null)
            {
                Debug.LogError("CreateUnit called with null prefab!");
                return null;
            }
            
            // Instantiate the unit
            GameObject unitObj = Instantiate(unitPrefab, position, Quaternion.identity);
            Unit unit = unitObj.GetComponent<Unit>();
            
            if (unit == null)
            {
                Debug.LogError("Prefab does not have a Unit component!");
                Destroy(unitObj);
                return null;
            }
            
            // Setup unit
            unit.MyFaction = faction;
            unit.IsDeath = false;
            
            // Set health/shield
            unit.HitPoints = unit.GetMaxHitPoints();
            unit.Shield = unit.GetMaxShield();
            
            // Register unit
            RegisterUnit(unit);
            
            return unit;
        }
        
        #endregion
        
        #region Unit Management
        
        /// <summary>
        /// Adds a unit to the active units list
        /// </summary>
        public void RegisterUnit(Unit unit)
        {
            if (unit != null && !activeUnits.Contains(unit))
            {
                activeUnits.Add(unit);
            }
        }
        
        /// <summary>
        /// Removes and destroys a unit
        /// </summary>
        public void RemoveUnit(Unit unit)
        {
            if (unit != null && activeUnits.Contains(unit))
            {
                activeUnits.Remove(unit);
                Destroy(unit.gameObject);
            }
        }
        
        /// <summary>
        /// Triggers a unit's death sequence
        /// </summary>
        public void KillUnit(Unit unit)
        {
            if (unit != null && !unit.IsDeath)
            {
                unit.Die();
            }
        }
        
        /// <summary>
        /// Gets all units of a specific faction
        /// </summary>
        public List<Unit> GetUnitsByFaction(Faction faction)
        {
            List<Unit> result = new List<Unit>();
            
            foreach (Unit unit in activeUnits)
            {
                if (unit != null && unit.MyFaction == faction)
                {
                    result.Add(unit);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets a safe copy of all active units
        /// </summary>
        public List<Unit> GetAllUnits()
        {
            return new List<Unit>(activeUnits);
        }
        
        /// <summary>
        /// Counts units of a specific faction
        /// </summary>
        public int CountUnits(Faction faction)
        {
            int count = 0;
            
            foreach (Unit unit in activeUnits)
            {
                if (unit != null && unit.MyFaction == faction)
                {
                    count++;
                }
            }
            
            return count;
        }
        
        #endregion
        
        #region Targeting
        
        /// <summary>
        /// Gets a target position for a unit's movement
        /// </summary>
        public Vector3 GetTargetPosition(Faction unitFaction)
        {
            // Return the opponent's base position
            if (unitFaction == Faction.Player)
            {
                // Player units target enemy base
                return enemyBase != null ? enemyBase.transform.position : basePositions[0];
            }
            else
            {
                // Enemy units target player base
                return playerBase != null ? playerBase.transform.position : basePositions[1];
            }
        }
        
        /// <summary>
        /// Gets a target transform for a unit's attacks
        /// </summary>
        public Transform GetTargetTransform(Faction unitFaction)
        {
            if (isGameOver) return transform;
            
            if (unitFaction == Faction.Player)
            {
                // Player units target enemy base
                return enemyBase != null ? enemyBase.transform : transform;
            }
            else
            {
                // Enemy units target player base
                return playerBase != null ? playerBase.transform : transform;
            }
        }
        
        #endregion
        
        #region Death & Respawn
        
        /// <summary>
        /// Handles player death and respawn
        /// </summary>
        private void HandlePlayerDeath(Unit player)
        {
            if (isRespawning || isGameOver) return;
            
            Debug.Log("Player died - initiating respawn sequence");
            
            // Start respawn process
            StartCoroutine(RespawnPlayer());
        }
        
        /// <summary>
        /// Respawns the player after a delay
        /// </summary>
        private IEnumerator RespawnPlayer()
        {
            isRespawning = true;
            
            // Wait for respawn delay
            yield return new WaitForSeconds(respawnDelay);
            
            // Reset player position
            playerBase.transform.position = basePositions[1];
            playerBase.transform.rotation = Quaternion.identity;
            
            // Spawn visual effect if available
            if (respawnEffectPrefab != null)
            {
                GameObject effect = Instantiate(respawnEffectPrefab, basePositions[1], Quaternion.identity);
                Destroy(effect, 2f);
            }
            
            // Reset player health/shield
            playerBase.IsDeath = false;
            playerBase.HitPoints = playerBase.GetMaxHitPoints();
            playerBase.Shield = playerBase.GetMaxShield();
            
            // Reset shooter component if present
            Shooter shooter = playerBase.GetComponent<Shooter>();
            if (shooter != null)
            {
                shooter.CanAttack = true;
                shooter.ResetShooter();
            }
            
            // Reset animator if present
            Animator anim = playerBase.GetAnimator();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
                anim.ResetTrigger("Die");
                anim.SetBool("Idle", true);
            }
            
            // Make player visible again
            if (playerBase.Mesh != null)
            {
                playerBase.Mesh.SetActive(true);
            }
            
            // Reactivate player
            playerBase.gameObject.SetActive(true);
            
            Debug.Log("Player respawned successfully");
            
            isRespawning = false;
        }
        
        #endregion
        
        #region Game State
        
        /// <summary>
        /// Called when the game is over
        /// </summary>
        public void EndGame(Faction winner)
        {
            if (isGameOver) return;
            
            isGameOver = true;
            Debug.Log($"Game Over! {winner} faction wins!");
            
            // Here you would trigger UI, show results, etc.
        }
        
        /// <summary>
        /// Checks if all bases still exist
        /// </summary>
        public bool BasesExist()
        {
            return playerBase != null && !playerBase.IsDeath && enemyBase != null && !enemyBase.IsDeath;
        }
        
        /// <summary>
        /// Gets whether the game is over
        /// </summary>
        public bool IsGameOver()
        {
            return isGameOver;
        }
        
        #endregion
        
        #region Helpers
        
        /// <summary>
        /// Gets the color for a unit based on faction
        /// </summary>
        public Color GetUnitColor(Faction faction)
        {
            return faction == Faction.Player ? Color.blue : Color.red;
        }
        
        /// <summary>
        /// Gets the player unit
        /// </summary>
        public Unit GetPlayerUnit()
        {
            return playerBase;
        }
        
        /// <summary>
        /// Gets the enemy base unit
        /// </summary>
        public Unit GetEnemyUnit()
        {
            return enemyBase;
        }
        
        #endregion
    }
} 