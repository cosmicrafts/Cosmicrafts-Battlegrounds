namespace Cosmicrafts
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Linq;
    using System.Collections;

    public class GameMng : MonoBehaviour
    {
        public static GameMng GM;
        public static Player P; // Player component reference
        public static GameMetrics MT;
        public static UIGameMng UI;
        public Vector3[] BS_Positions; // Base stations positions [0] = Enemy (Red), [1] = Player (Blue)

        [Header("Character Settings")]
        public CharacterBaseSO playerCharacterSO; // Player character definition
        public CharacterBaseSO enemyCharacterSO;  // Enemy character definition (should have Unit and Bot components on its prefab)

        [Header("Player Respawn Settings")]
        public int playerLives = 9; // Number of lives before game over (Currently not used due to infinite respawn)
        public float respawnDelay = 5f; // Seconds to wait before respawning
        public GameObject respawnEffectPrefab; // Optional visual effect for respawn

        private int playerLivesRemaining; // Currently not used
        private bool isRespawning = false;

        private List<Unit> units = new List<Unit>();
        private List<Spell> spells = new List<Spell>();
        private int idCounter = 0;
        private Dictionary<string, NFTsCard> allPlayersNfts = new Dictionary<string, NFTsCard>();

        public Unit[] Targets = new Unit[2]; // Array for managing base stations [0] = Enemy, [1] = Player
        bool GameOver = false;

        // Time variables (if needed, otherwise remove)
        private TimeSpan timeOut;
        private DateTime startTime;

        private void Awake()
        {
            Debug.Log("--GAME MANAGER AWAKE--");
            if (GM != null && GM != this)
            {
                Destroy(gameObject);
                return;
            }
            GM = this;
            DontDestroyOnLoad(gameObject); // Optional: Keep GameMng across scenes

            Debug.Log("--GAME VARIABLES INITIALIZING--");
            MT = new GameMetrics();
            MT.InitMetrics();
            playerLivesRemaining = playerLives; // Lives counter (currently unused)
            Debug.Log("--GAME VARIABLES READY--");
        }

        private void Start()
        {
            Debug.Log("--GAME MANAGER START--");

            // Spawn Player Base
            if (playerCharacterSO != null)
            {
                SpawnPlayerBase();
            }
            else
            {
                Debug.LogError("Player Character SO not assigned in GameMng Inspector! Player base won't spawn.");
            }

            // Spawn Enemy Base
            if (enemyCharacterSO != null)
            {
                SpawnEnemyBase();
            }
            else
            {
                Debug.LogError("Enemy Character SO not assigned in GameMng Inspector! Enemy base won't spawn.");
            }

            // Ensure both bases spawned before declaring ready
            if (Targets[0] != null && Targets[1] != null)
            {
                Debug.Log("--GAME MANAGER READY--");
            }
            else
            {
                 Debug.LogError("--GAME MANAGER FAILED TO INITIALIZE BASES!--");
            }
        }

        // --- New Player Spawning Function ---
        private void SpawnPlayerBase()
        {
            if (playerCharacterSO == null || playerCharacterSO.BasePrefab == null)
            {
                Debug.LogError("Cannot spawn player base: Player Character SO or its BasePrefab is null!");
                return;
            }

            int playerId = 1;
            Faction playerFaction = Faction.Player;
            int playerBaseIndex = 1; // Blue base position index

            GameObject playerObject = Instantiate(playerCharacterSO.BasePrefab, BS_Positions[playerBaseIndex], Quaternion.identity);
            Player playerComponent = playerObject.GetComponent<Player>();
            Unit playerUnit = playerObject.GetComponent<Unit>();

            if (playerComponent == null || playerUnit == null)
            {
                Debug.LogError($"Player base prefab '{playerCharacterSO.BasePrefab.name}' is missing required components (Player, Unit)! Destroying instance.");
                Destroy(playerObject);
                return;
            }
            
            // --- Initialize Unit ---
            playerUnit.IsDeath = false; // Ensure alive state from the start
            
            // Set faction (this will automatically set MyTeam and PlayerId through properties)
            playerUnit.MyFaction = playerFaction;
            
            playerUnit.setId(GenerateUnitId());

            // Apply SO overrides for stats BEFORE base initialization
            playerCharacterSO.ApplyOverridesToUnit(playerUnit);

            // Ensure minimum health/shield AFTER overrides
            if (playerUnit.HitPoints <= 0) {
                 Debug.LogWarning($"Player base HP was {playerUnit.HitPoints} after SO override, setting to 100.");
                 playerUnit.SetMaxHitPoints(100);
                 playerUnit.HitPoints = 100;
            }
            if (playerUnit.Shield < 0) { // Allow 0 shield, but not negative
                 Debug.LogWarning($"Player base Shield was {playerUnit.Shield} after SO override, setting to 0.");
                 playerUnit.SetMaxShield(0);
                 playerUnit.Shield = 0;
            }

            // --- Initialize Player ---
            playerComponent.ID = playerId;
            playerComponent.MyFaction = playerFaction;
            // GameMng.P should be set in Player's Start method

            // --- Register ---
            Targets[playerBaseIndex] = playerUnit;
            AddUnit(playerUnit);
            playerUnit.OnUnitDeath += HandlePlayerBaseStationDeath; // Subscribe to death event

            Debug.Log($"Player base spawned: {playerUnit.gameObject.name} | HP: {playerUnit.HitPoints}/{playerUnit.GetMaxHitPoints()} | Shield: {playerUnit.Shield}/{playerUnit.GetMaxShield()}");
        }

        // --- New Enemy Spawning Function ---
        private void SpawnEnemyBase()
        {
             if (enemyCharacterSO == null || enemyCharacterSO.BasePrefab == null)
            {
                Debug.LogError("Cannot spawn enemy base: Enemy Character SO or its BasePrefab is null!");
                return;
            }

            // Use Faction enum directly instead of separate Team and PlayerId values
            Faction enemyFaction = Faction.Enemy;
            int enemyBaseIndex = 0; // Red base position index

            GameObject enemyObject = Instantiate(enemyCharacterSO.BasePrefab, BS_Positions[enemyBaseIndex], Quaternion.identity);
            Unit enemyUnit = enemyObject.GetComponent<Unit>();

            if (enemyUnit == null) // Unit is essential
            {
                Debug.LogError($"Enemy base prefab '{enemyCharacterSO.BasePrefab.name}' is missing Unit component! Destroying instance.");
                Destroy(enemyObject);
                return;
            }

            // --- Initialize Unit ---
            enemyUnit.IsDeath = false; // Ensure alive state
            
            // Set faction (this will automatically set MyTeam and PlayerId through properties)
            enemyUnit.MyFaction = enemyFaction;
            
            enemyUnit.setId(GenerateUnitId());

            // Apply SO overrides for stats
            enemyCharacterSO.ApplyOverridesToUnit(enemyUnit);

            // Ensure minimum health/shield AFTER overrides
             if (enemyUnit.HitPoints <= 0) {
                 Debug.LogWarning($"Enemy base HP was {enemyUnit.HitPoints} after SO override, setting to 100.");
                 enemyUnit.SetMaxHitPoints(100);
                 enemyUnit.HitPoints = 100;
            }
            if (enemyUnit.Shield < 0) {
                 Debug.LogWarning($"Enemy base Shield was {enemyUnit.Shield} after SO override, setting to 0.");
                 enemyUnit.SetMaxShield(0);
                 enemyUnit.Shield = 0;
            }

            // --- Register ---
            Targets[enemyBaseIndex] = enemyUnit;
            AddUnit(enemyUnit);
            // Optionally subscribe to enemy death: enemyUnit.OnUnitDeath += HandleEnemyBaseStationDeath;

            Debug.Log($"Enemy base spawned: {enemyUnit.gameObject.name} | HP: {enemyUnit.HitPoints}/{enemyUnit.GetMaxHitPoints()} | Shield: {enemyUnit.Shield}/{enemyUnit.GetMaxShield()} | Bot Component: N/A");
        }

        // Handle player base station death - make this much more direct and aggressive
        private void HandlePlayerBaseStationDeath(Unit baseStation)
        {
            if (P == null || baseStation.GetComponent<Player>() != P || isRespawning)
            {
                // Ignore if it's not the player, or if already respawning, or if player isn't set yet
                return;
            }

            Debug.Log("Player base station destroyed - initiating reset...");
            
            // IMMEDIATELY deactivate the player - don't wait for animation
            baseStation.gameObject.SetActive(false);
            
            // Start the reset process
            StartCoroutine(ResetPlayerCharacter(baseStation));
        }

        // Completely rewrite the player reset function - make it direct and immediate
        private IEnumerator ResetPlayerCharacter(Unit playerUnit)
        {
            isRespawning = true;

            // Determine the correct spawn position based on the player's faction
            Team playerTeam = FactionManager.ConvertFactionToTeam(P.MyFaction);
            int playerBaseIndex = playerTeam == Team.Blue ? 1 : 0;
            Vector3 respawnPosition = BS_Positions[playerBaseIndex];

            // Wait for respawn delay with object disabled
            yield return new WaitForSeconds(respawnDelay);

            if (GameOver) // Check if game ended during delay
            {
                isRespawning = false;
                yield break;
            }

            // Create respawn effect if available
            if (respawnEffectPrefab != null)
            {
                GameObject effect = Instantiate(respawnEffectPrefab, respawnPosition, Quaternion.identity);
                Destroy(effect, 3f);
            }
            
            // Force teleport to respawn position BEFORE activating
            playerUnit.transform.position = respawnPosition;
            playerUnit.transform.rotation = Quaternion.identity;

            // Reset health, shield, and state flags
            playerUnit.IsDeath = false;
            playerUnit.HitPoints = playerUnit.GetMaxHitPoints();
            playerUnit.Shield = playerUnit.GetMaxShield();
            
            // Reset visual elements and components
            if (playerUnit.UI != null && playerUnit.UI.Canvas != null) 
            {
                playerUnit.UI.Canvas.SetActive(true);
                playerUnit.UI.SetHPBar(1f);
                playerUnit.UI.SetShieldBar((float)playerUnit.Shield / (float)playerUnit.GetMaxShield());
            }
            
            // Reset shield visual
            if (playerUnit.ShieldGameObject != null)
            {
                playerUnit.ShieldGameObject.SetActive(false);
            }
            
            // Make sure ALL colliders are enabled
            Collider[] colliders = playerUnit.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                col.enabled = true;
            }
            
            // Reset shooter
            Shooter shooter = playerUnit.GetComponent<Shooter>();
            if (shooter != null)
            {
                shooter.CanAttack = true;
                shooter.ResetShooter();
            }
            
            // Reset animator
            if (playerUnit.GetAnimator() != null)
            {
                Animator anim = playerUnit.GetAnimator();
                anim.Rebind();
                anim.Update(0f);
                anim.ResetTrigger("Die");
                anim.SetBool("Idle", true);
            }
            
            // Make mesh visible
            if (playerUnit.Mesh != null)
            {
                playerUnit.Mesh.SetActive(true);
            }

            // AFTER everything is set up, activate the player object
            playerUnit.gameObject.SetActive(true);
            
            Debug.Log($"PLAYER RESET COMPLETE - HP: {playerUnit.HitPoints}/{playerUnit.GetMaxHitPoints()}");
            isRespawning = false;
        }

        public Unit CreateUnit(GameObject obj, Vector3 position, Team team, string nftKey = "none", int playerId = -1)
        {
            if (obj == null) {
                Debug.LogError("CreateUnit called with a null prefab!");
                return null;
            }
            GameObject unitObj = Instantiate(obj, position, Quaternion.identity);
            Unit unit = unitObj.GetComponent<Unit>();

            if (unit != null)
            {
                // CRITICAL: Set IsDeath flag first
                unit.IsDeath = false;

                // Initialize base stats before applying NFTs
                unit.SetMaxHitPoints(Mathf.Max(10, unit.GetMaxHitPoints())); // Sensible minimum
                unit.HitPoints = unit.GetMaxHitPoints();
                unit.SetMaxShield(Mathf.Max(0, unit.GetMaxShield())); // Allow 0 shield
                unit.Shield = unit.GetMaxShield();

                // Set faction - convert from legacy team and player ID
                Faction unitFaction = team == Team.Blue ? Faction.Player : Faction.Enemy;
                if (playerId != -1) {
                    // If explicit player ID provided, use that to determine faction
                    unitFaction = playerId == 1 ? Faction.Player : Faction.Enemy;
                }
                
                // Set the faction directly
                unit.MyFaction = unitFaction;

                unit.setId(GenerateUnitId());

                // Apply NFT data if provided - fix PlayerId warning
                int derivedPlayerId = unit.MyFaction == Faction.Player ? 1 : 2;
                NFTsUnit nftData = GetNftCardData(nftKey, derivedPlayerId) as NFTsUnit;
                if (nftData != null) {
                    unit.SetNfts(nftData); // Assuming SetNfts applies stats from the NFT

                    // Verify health/shield AFTER NFT application
                    if (unit.HitPoints <= 0)
                    {
                        Debug.LogWarning($"Unit HP was {unit.HitPoints} after NFT '{nftKey}', forcing to 10.");
                        unit.SetMaxHitPoints(Mathf.Max(10, unit.GetMaxHitPoints())); // Re-ensure minimum if NFT reduced it
                        unit.HitPoints = 10;
                    }
                     if (unit.Shield < 0)
                    {
                        Debug.LogWarning($"Unit Shield was {unit.Shield} after NFT '{nftKey}', forcing to 0.");
                        unit.SetMaxShield(Mathf.Max(0, unit.GetMaxShield()));
                        unit.Shield = 0;
                    }
                }

                // Register with game manager
                AddUnit(unit);

                //Debug.Log($"Unit created: {unit.gameObject.name} | HP: {unit.HitPoints}/{unit.GetMaxHitPoints()} | Shield: {unit.Shield}/{unit.GetMaxShield()} | Team: {unit.MyFaction}");
            }
            else
            {
                Debug.LogError($"Failed to get Unit component from instantiated object: {obj.name}");
                Destroy(unitObj); // Clean up unusable object
            }

            return unit;
        }

        public Spell CreateSpell(GameObject obj, Vector3 position, Team team, string nftKey = "none", int playerId = -1)
        {
            if (obj == null) {
                Debug.LogError("CreateSpell called with a null prefab!");
                return null;
            }
            
            GameObject spellObj = Instantiate(obj, position, Quaternion.identity);
            Spell spell = spellObj.GetComponent<Spell>();
            
            if (spell != null)
            {
                // Convert from legacy team/playerId to unified faction system
                Faction spellFaction = team == Team.Blue ? Faction.Player : Faction.Enemy;
                if (playerId != -1) {
                    // If explicit player ID provided, use that to determine faction
                    spellFaction = playerId == 1 ? Faction.Player : Faction.Enemy;
                }
                
                // Set faction directly
                spell.MyFaction = spellFaction;
                
                // Use setId instead of Init
                spell.setId(GenerateUnitId());
                
                if(!string.IsNullOrEmpty(nftKey)) {
                    // Apply NFT data if available
                    NFTsSpell nFTsSpell = GetNftSpellData(nftKey, spellFaction == Faction.Player ? 1 : 2);
                    if (nFTsSpell != null) {
                        spell.SetNfts(nFTsSpell);
                    }
                }
                
                AddSpell(spell);
            }
            
            return spell;
        }

        public void AddUnit(Unit unit)
        {
             if (unit == null) {
                Debug.LogWarning("Attempted to add a null unit.");
                return;
            }
            if (!units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        public void AddSpell(Spell spell)
        {
            if (spell == null) {
                Debug.LogWarning("Attempted to add a null spell.");
                return;
            }
            if (!spells.Contains(spell))
            {
                spells.Add(spell);
            }
        }

        public void DeleteUnit(Unit unit)
        {
            if (unit != null && units.Contains(unit))
            {
                units.Remove(unit);
                //Debug.Log($"DeleteUnit called for {unit.gameObject.name} - Team: {unit.MyFaction}, ID: {unit.getId()}");
                Destroy(unit.gameObject);
            }
        }

        public void DeleteSpell(Spell spell)
        {
            if (spell != null && spells.Contains(spell))
            {
                spells.Remove(spell);
                Destroy(spell.gameObject);
            }
        }

        public void KillUnit(Unit unit)
        {
            if (unit != null && !unit.IsDeath) // Only kill if not already dead
            {
                unit.Die(); // Trigger death sequence
                // Let the Die() method handle removal/destruction usually via OnUnitDeath event
            }
        }

        public void EndGame(Faction winner)
        {
            // Prevent the game from actually ending for infinite play
            Team legacyTeam = winner == Faction.Player ? Team.Blue : Team.Red;
            Debug.Log($"Game would have ended with {winner} faction winning, but continuing for infinite play.");
            // GameOver = true; // Keep commented out for infinite play

            if (UI != null && UI.ResultsScreen != null) // Check if UI and ResultsScreen exist
            {
                UI.SetGameOver(legacyTeam); // Pass legacy team for backward compatibility
            }
            else
            {
                Debug.LogWarning("Cannot show game over screen: UI Manager or Results Screen not found.");
            }
        }
        
        // Overload for backward compatibility
        public void EndGame(Team winner)
        {
            Faction winnerFaction = winner == Team.Blue ? Faction.Player : Faction.Enemy;
            EndGame(winnerFaction);
        }

        public Color GetColorUnit(Faction faction, int playerId = -1)
        {
            // Simplified color logic based on faction
            return faction == Faction.Player ? Color.green : Color.red;
        }
        
        // Backward compatibility method
        public Color GetColorUnit(Team team, int playerId)
        {
            return GetColorUnit(team == Team.Blue ? Faction.Player : Faction.Enemy, playerId);
        }

        public Vector3 GetDefaultTargetPosition(Faction faction)
        {
            // Return the opponent's base position
            int index = faction == Faction.Player ? 0 : 1; // Player targets Enemy (0), Enemy targets Player (1)
            if (Targets[index] != null)
                return Targets[index].transform.position;
            else // Fallback if target base doesn't exist yet
                return BS_Positions[index];
        }
        
        // Backward compatibility method
        public Vector3 GetDefaultTargetPosition(Team team)
        {
            return GetDefaultTargetPosition(team == Team.Blue ? Faction.Player : Faction.Enemy);
        }

        public Transform GetFinalTransformTarget(Faction faction)
        {
            if (GameOver) return transform; // Return GameMng transform if game over

            int targetIndex = faction == Faction.Player ? 0 : 1; // Player targets Enemy (0), Enemy targets Player (1)
            return Targets[targetIndex] != null ? Targets[targetIndex].transform : transform; // Fallback to GameMng transform
        }
        
        // Backward compatibility method
        public Transform GetFinalTransformTarget(Team team)
        {
            return GetFinalTransformTarget(team == Team.Blue ? Faction.Player : Faction.Enemy);
        }

        public bool IsGameOver()
        {
            return GameOver;
        }

        public bool MainStationsExist()
        {
            // Check if both base stations (Targets) exist and are not marked as dead
            return Targets[0] != null && !Targets[0].IsDeath && Targets[1] != null && !Targets[1].IsDeath;
        }

        // Get the number of player lives remaining (currently unused)
        public int GetPlayerLivesRemaining()
        {
            return playerLivesRemaining;
        }

        private int GenerateUnitId()
        {
            idCounter++;
            return idCounter;
        }

         // --- NFT Data Management ---
        // Consider moving NFT logic to a separate manager if it gets complex

        public void AddNftCardData(NFTsUnit nFTsCard, int playerId)
        {
            if (nFTsCard == null) return;
            string finalKey = $"{playerId}_{nFTsCard.KeyId}";
            if (!allPlayersNfts.ContainsKey(finalKey))
            {
                allPlayersNfts.Add(finalKey, nFTsCard);
            }
            else
            {
                //Debug.LogWarning($"Duplicate NFT Unit key ignored: {finalKey}");
            }
        }

        public void AddNftCardData(NFTsSpell nFTsSpell, int playerId)
        {
             if (nFTsSpell == null) return;
            string finalKey = $"{playerId}_{nFTsSpell.KeyId}";
             if (!allPlayersNfts.ContainsKey(finalKey))
            {
                allPlayersNfts.Add(finalKey, nFTsSpell);
            }
             else
            {
                //Debug.LogWarning($"Duplicate NFT Spell key ignored: {finalKey}");
            }
        }

        private NFTsUnit GetNftCardData(string nftKey, int playerId)
        {
             if (string.IsNullOrEmpty(nftKey) || nftKey == "none") return null;
            string finalKey = $"{playerId}_{nftKey}";
            return allPlayersNfts.TryGetValue(finalKey, out NFTsCard card) ? card as NFTsUnit : null;
        }

        private NFTsSpell GetNftSpellData(string nftKey, int playerId)
        {
             if (string.IsNullOrEmpty(nftKey) || nftKey == "none") return null;
            string finalKey = $"{playerId}_{nftKey}";
            return allPlayersNfts.TryGetValue(finalKey, out NFTsCard card) ? card as NFTsSpell : null;
        }

        // --- Utility and Information Methods ---

        public int CountUnits()
        {
            // Filter out potentially destroyed units before counting
            units.RemoveAll(unit => unit == null);
            return units.Count;
        }

        public int CountUnits(Faction faction)
        {
            return units.Count(unit => unit.MyFaction == faction);
        }
        
        // Backward compatibility method
        public int CountUnits(Team team)
        {
            return CountUnits(team == Team.Blue ? Faction.Player : Faction.Enemy);
        }

        public int GetRemainingSecs()
        {
            // Implement time logic if needed, otherwise return a default
            // TimeSpan currentTime = timeOut.Add(startTime - DateTime.Now);
            // return Mathf.Max(0, (int)currentTime.TotalSeconds);
            return 999; // Placeholder
        }

        // Helper to get the player unit instance
        public Unit GetPlayerUnit()
        {
            return (P != null) ? P.GetComponent<Unit>() : null;
        }

         // Helper to get the enemy base unit instance
        public Unit GetEnemyBaseUnit()
        {
             // Return Targets[0] directly, checking for null
             return (Targets != null && Targets.Length > 0) ? Targets[0] : null;
             // return (BOT != null) ? BOT.GetComponent<Unit>() : null; // Old implementation
        }

        // Add the missing GetUnitsListClone method
        public List<Unit> GetUnitsListClone()
        {
            // Create a copy of the units list to avoid modification issues during iteration
            return new List<Unit>(units);
        }
    }
}
