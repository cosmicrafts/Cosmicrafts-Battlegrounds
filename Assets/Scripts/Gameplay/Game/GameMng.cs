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
        public static Bot BOT; // Bot component reference (for the enemy base)
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
            Team playerTeam = Team.Blue;
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
            playerUnit.PlayerId = playerId;
            playerUnit.MyTeam = playerTeam;
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
            playerComponent.MyTeam = playerTeam;
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

            int enemyId = 2;
            Team enemyTeam = Team.Red;
            int enemyBaseIndex = 0; // Red base position index

            GameObject enemyObject = Instantiate(enemyCharacterSO.BasePrefab, BS_Positions[enemyBaseIndex], Quaternion.identity);
            Bot botComponent = enemyObject.GetComponent<Bot>(); // Check for Bot component
            Unit enemyUnit = enemyObject.GetComponent<Unit>();

            if (enemyUnit == null) // Unit is essential
            {
                Debug.LogError($"Enemy base prefab '{enemyCharacterSO.BasePrefab.name}' is missing Unit component! Destroying instance.");
                Destroy(enemyObject);
                return;
            }
            if (botComponent == null) // Bot component is also expected
            {
                 Debug.LogWarning($"Enemy base prefab '{enemyCharacterSO.BasePrefab.name}' is missing Bot component! AI will not function.");
                 // Proceed without AI, but log warning
            }

            // --- Initialize Unit ---
            enemyUnit.IsDeath = false; // Ensure alive state
            enemyUnit.PlayerId = enemyId;
            enemyUnit.MyTeam = enemyTeam;
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

            // --- Initialize Bot (if present) ---
            if (botComponent != null)
            {
                botComponent.ID = enemyId;
                botComponent.MyTeam = enemyTeam;
                BOT = botComponent; // Set static reference
            }

            // --- Register ---
            Targets[enemyBaseIndex] = enemyUnit;
            AddUnit(enemyUnit);
            // Optionally subscribe to enemy death: enemyUnit.OnUnitDeath += HandleEnemyBaseStationDeath;

            Debug.Log($"Enemy base spawned: {enemyUnit.gameObject.name} | HP: {enemyUnit.HitPoints}/{enemyUnit.GetMaxHitPoints()} | Shield: {enemyUnit.Shield}/{enemyUnit.GetMaxShield()} | Bot Component: {(botComponent != null)}");
        }

        // Handle player base station death
        private void HandlePlayerBaseStationDeath(Unit baseStation)
        {
            if (P == null || baseStation.GetComponent<Player>() != P || isRespawning)
            {
                // Ignore if it's not the player, or if already respawning, or if player isn't set yet
                return;
            }

            Debug.Log("Player base station destroyed - initiating reset...");
            StartCoroutine(ResetPlayerCharacter(baseStation));
        }

        // Renamed from ResetPlayerBaseStation - Resets the player character instance
        private IEnumerator ResetPlayerCharacter(Unit playerUnit)
        {
            isRespawning = true;

            // Determine the correct spawn position based on the player's team
            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            Vector3 respawnPosition = BS_Positions[playerBaseIndex];

            // Wait for respawn delay
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
                Destroy(effect, 3f); // Destroy effect after 3 seconds
            }

            // Reset the unit's state (health, status flags, etc.)
            playerUnit.ResetUnit(); // Assuming Unit.cs has a ResetUnit method

            // Force position using teleport
            playerUnit.transform.SetPositionAndRotation(respawnPosition, Quaternion.identity);

            // Ensure the unit is visually active again if disabled earlier
            if (!playerUnit.gameObject.activeSelf)
                 playerUnit.gameObject.SetActive(true);

            Debug.Log($"[Reset] Player character reset complete at {playerUnit.transform.position}");

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

                // Set team and ID
                unit.MyTeam = team;
                unit.PlayerId = playerId == -1 ? (team == Team.Blue ? 1 : 2) : playerId; // Assign default ID if none provided
                unit.setId(GenerateUnitId());

                // Apply NFT data if provided
                NFTsUnit nftData = GetNftCardData(nftKey, unit.PlayerId) as NFTsUnit;
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

                //Debug.Log($"Unit created: {unit.gameObject.name} | HP: {unit.HitPoints}/{unit.GetMaxHitPoints()} | Shield: {unit.Shield}/{unit.GetMaxShield()} | Team: {unit.MyTeam}");
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
             // Check if the prefab has a Spell component
            if (obj.GetComponent<Spell>() == null)
            {
                Debug.LogError($"Spell prefab '{obj.name}' is missing Spell component! NFT Key: {nftKey}");
                return null;
            }

            Spell spell = Instantiate(obj, position, Quaternion.identity).GetComponent<Spell>();
            spell.MyTeam = team;
            spell.PlayerId = playerId == -1 ? (team == Team.Blue ? 1 : 2) : playerId; // Assign default ID
            spell.setId(GenerateUnitId());

            // Set NFT data similar to CreateUnit method
            NFTsSpell nftData = GetNftSpellData(nftKey, spell.PlayerId) as NFTsSpell;
            if (nftData != null) {
                spell.SetNfts(nftData); // Assuming SetNfts applies spell properties from NFT
            }

            AddSpell(spell);
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
                //Debug.Log($"DeleteUnit called for {unit.gameObject.name} - Team: {unit.MyTeam}, ID: {unit.getId()}");
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

        public void EndGame(Team winner)
        {
            // Prevent the game from actually ending for infinite play
            Debug.Log($"Game would have ended with {winner} team winning, but continuing for infinite play.");
            // GameOver = true; // Keep commented out for infinite play

            if (UI != null && UI.ResultsScreen != null) // Check if UI and ResultsScreen exist
            {
                UI.SetGameOver(winner);
            }
             else
            {
                Debug.LogWarning("Cannot show game over screen: UI Manager or Results Screen not found.");
            }
        }

        public Color GetColorUnit(Team team, int playerId)
        {
            // Simplified color logic
            return team == Team.Blue ? Color.green : Color.red;
        }

        public Vector3 GetDefaultTargetPosition(Team team)
        {
            // Return the opponent's base position
            int index = team == Team.Blue ? 0 : 1; // Blue targets Red (0), Red targets Blue (1)
             if (Targets[index] != null)
                 return Targets[index].transform.position;
             else // Fallback if target base doesn't exist yet
                 return BS_Positions[index];
        }

        public Transform GetFinalTransformTarget(Team team)
        {
             if (GameOver) return transform; // Return GameMng transform if game over

            int targetIndex = team == Team.Blue ? 0 : 1; // Blue targets Red (0), Red targets Blue (1)
            return Targets[targetIndex] != null ? Targets[targetIndex].transform : transform; // Fallback to GameMng transform
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

        public int CountUnits(Team team)
        {
            units.RemoveAll(unit => unit == null);
            return units.Count(u => u.IsMyTeam(team));
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
             return (BOT != null) ? BOT.GetComponent<Unit>() : null;
        }
    }
}
