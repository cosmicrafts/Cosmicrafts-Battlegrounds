namespace Cosmicrafts
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Linq;
    using System.Collections;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;

    public class GameMng : MonoBehaviour
    {
        public static GameMng GM;
        public static Player P;
        public static GameMetrics MT;
        public static UIGameMng UI;

        [Header("Player Setup")]
        [Tooltip("The character SO that defines player properties and base prefab")]
        public CharacterBaseSO characterSO;
        
        public Vector3[] BS_Positions; // Base stations positions
        
        [Header("Player Respawn Settings")]
        [Tooltip("Panel that shows when player dies (should contain respawn button)")]
        public GameObject deathPanel;
        [Tooltip("Button that triggers respawn")]
        public UnityEngine.UI.Button respawnButton;
        [Tooltip("Global Volume in the scene to switch profiles")]
        public Volume globalVolume;
        [Tooltip("Profile to use when player is dead")]
        public VolumeProfile deathProfile;
        [Tooltip("Profile to use when player is alive")]
        public VolumeProfile aliveProfile;
        
        private int playerLives = 9; // Number of lives before game over
        private int playerLivesRemaining;
        private GameObject playerBaseStationPrefab; // Store reference to player's base station prefab for respawn
        private bool isRespawning = false;

        // Player state event
        public delegate void PlayerStateChangedHandler(bool isAlive);
        public event PlayerStateChangedHandler OnPlayerStateChanged;

        // Public method to set player state and trigger the event
        public void SetPlayerState(bool isAlive)
        {
            OnPlayerStateChanged?.Invoke(isAlive);
        }

        private List<Unit> units = new List<Unit>();
        private List<Spell> spells = new List<Spell>();
        private int idCounter = 0;
        private Dictionary<string, NFTsCard> allPlayersNfts = new Dictionary<string, NFTsCard>();

        public Unit[] Targets = new Unit[2]; // Array for managing base stations
        bool GameOver = false;

        // Time variables
        private TimeSpan timeOut;
        private DateTime startTime;

        private void Awake()
        {
            // Debug.Log("--GAME MANAGER AWAKE--");

            // Init static unique controllers
            GM = this;

            // Debug.Log("--GAME VARIABLES READY--");

            MT = new GameMetrics();
            MT.InitMetrics();
            
            // Initialize player lives
            playerLivesRemaining = playerLives;
            
            // Initialize player from CharacterBaseSO
            if (characterSO != null && characterSO.BasePrefab != null)
            {
                InitializePlayer();
            }
            else
            {
                Debug.LogError("CharacterBaseSO or its BasePrefab not assigned in GameMng!");
            }

            // Setup respawn button listener
            if (respawnButton != null)
            {
                respawnButton.onClick.AddListener(OnRespawnButtonClicked);
            }
            else
            {
                Debug.LogWarning("Respawn button not assigned in GameMng!");
            }

            // Ensure death panel starts disabled
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }
            else
            {
                Debug.LogWarning("Death panel not assigned in GameMng!");
            }
        }

        private void OnDestroy()
        {
            // Clean up button listener
            if (respawnButton != null)
            {
                respawnButton.onClick.RemoveListener(OnRespawnButtonClicked);
            }
        }

        private void InitializePlayer()
        {
            if (characterSO == null || characterSO.BasePrefab == null)
            {
                Debug.LogError("InitializePlayer: CharacterBaseSO or its BasePrefab is null!");
                return;
            }

            // Store base station prefab reference for potential respawn logic later
            playerBaseStationPrefab = characterSO.BasePrefab;

            // 1. Instantiate the player's base station (which includes the Player component)
            GameObject playerGameObject = Instantiate(characterSO.BasePrefab);
            P = playerGameObject.GetComponent<Player>();

            if (P == null)
            {
                Debug.LogError("Player component not found on CharacterBaseSO's BasePrefab! Destroying instantiated object.");
                Destroy(playerGameObject);
                return;
            }

            // Determine the player's base position index based on their team (P.MyTeam should be set by Player.Awake or Start or on prefab)
            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            if (BS_Positions == null || BS_Positions.Length <= playerBaseIndex)
            {
                Debug.LogError($"BS_Positions array is not set up correctly to accommodate index {playerBaseIndex}. Player Team: {P.MyTeam}");
                Destroy(playerGameObject);
                return;
            }
            playerGameObject.transform.position = BS_Positions[playerBaseIndex];
            playerGameObject.transform.rotation = Quaternion.identity;

            Unit playerBaseUnit = playerGameObject.GetComponent<Unit>();
            if (playerBaseUnit == null)
            {
                Debug.LogError("Unit component not found on CharacterBaseSO's BasePrefab (Player's base)! Player object will exist, but base functionality might be impaired.");
                // Not returning here as P is valid, but logging the error.
            }
            else
            {
                // 2. Apply character overrides from CharacterBaseSO to the player's base unit
                characterSO.ApplyOverridesToUnit(playerBaseUnit);
            }
            
            // 3. Setup this base unit in the game, and initialize enemy base/bots
            // P and playerBaseUnit are now initialized from the single instantiation.
            SetupTeamBasesAndBots(playerBaseUnit);

            // 4. Apply gameplay modifiers from CharacterBaseSO (e.g., global buffs)
            characterSO.ApplyGameplayModifiers();

            // Debug.Log($"Player initialized. Player Component: {P.name}, Base Unit: {(playerBaseUnit != null ? playerBaseUnit.name : "MISSING_UNIT_COMPONENT")} at {playerGameObject.transform.position}");
        }

        // Renamed and refactored from InitBaseStations
        private void SetupTeamBasesAndBots(Unit playerBaseStationInstance)
        {
            if (P == null)
            {
                // Debug.LogError("SetupTeamBasesAndBots: Player (P) is null. Cannot proceed.");
                return;
            }

            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            Team playerTeam = P.MyTeam;
            Team enemyTeam = (playerTeam == Team.Blue) ? Team.Red : Team.Blue;

            if (playerBaseStationInstance != null)
            {
                Targets[playerBaseIndex] = playerBaseStationInstance;

                // Ensure team and ID are correctly set on the Unit component of the player's base
                playerBaseStationInstance.MyTeam = playerTeam;
                playerBaseStationInstance.PlayerId = P.ID;
                playerBaseStationInstance.setId(GenerateUnitId()); // Assign a game-specific ID

                // Force update outline color to ensure proper team colors
                playerBaseStationInstance.UpdateOutlineColor();

                playerBaseStationInstance.OnUnitDeath -= HandlePlayerBaseStationDeath; // Ensure no double subscription
                playerBaseStationInstance.OnUnitDeath += HandlePlayerBaseStationDeath;

                // Debug.Log($"Player base station ({playerBaseStationInstance.name}) registered. Team: {playerBaseStationInstance.MyTeam}, PlayerId: {playerBaseStationInstance.PlayerId}, Pos: {playerBaseStationInstance.transform.position}");
            }
            else
            {
                 Debug.LogWarning($"SetupTeamBasesAndBots: playerBaseStationInstance is null. Player base might not be correctly registered in Targets array or configured.");
            }
            
            // Initialize Bot Spawner for the enemy team
            BotSpawner botSpawner = FindFirstObjectByType<BotSpawner>();
            if (botSpawner != null)
            {
                // BotSpawner needs the player's team and the *player's* designated base position slot.
                // It will then deduce the enemy team and the enemy's base position slot.
                if (BS_Positions == null || BS_Positions.Length <= playerBaseIndex)
                {
                     Debug.LogError("BS_Positions array not valid for bot spawner initialization.");
                     return;
                }
                botSpawner.Initialize(playerTeam, BS_Positions[playerBaseIndex]); 
                botSpawner.SpawnBots(); // This should create the enemy base and units

                Unit enemyBaseStation = botSpawner.GetBotBaseStation();
                int enemyBaseIndex = playerTeam == Team.Blue ? 0 : 1; 
                if (enemyBaseStation != null)
                {
                    if (Targets.Length <= enemyBaseIndex)
                    {
                        Debug.LogError("Targets array not large enough for enemy base index.");
                        return;
                    }
                    
                    // Double-check enemy team is set correctly
                    enemyBaseStation.MyTeam = enemyTeam;
                    // Ensure enemy base uses a different player ID than player
                    enemyBaseStation.PlayerId = (P.ID == 1) ? 2 : 1;
                    
                    // Force update outline color to ensure proper enemy coloring
                    enemyBaseStation.UpdateOutlineColor();
                    
                    Targets[enemyBaseIndex] = enemyBaseStation;
                    // Debug.Log($"Enemy base station ({enemyBaseStation.name}) registered. Team: {enemyBaseStation.MyTeam}, PlayerId: {enemyBaseStation.PlayerId}");
                }
                else
                {
                    Debug.LogError("BotSpawner did not provide an enemy base station!");
                }
            }
            else
            {
                Debug.LogError("No BotSpawner found in the scene! Enemy team won't be created.");
            }
        }

        public void ReInitializeBaseStations()
        {
            Debug.LogWarning("ReInitializeBaseStations called. This will perform a full player and enemy base re-initialization.");
            
            // Clean up existing player GameObject if P is assigned
            if (P != null && P.gameObject != null)
            {
                Destroy(P.gameObject); // This should also destroy the associated Unit component if it's on the same GameObject
                P = null;
            }

            // Clean up target references. The GameObjects they point to might have been destroyed with P.gameObject or need separate destruction.
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i] != null && Targets[i].gameObject != null)
                {
                    // Check if it's not the player's base we might have just destroyed
                    if (P == null || Targets[i].gameObject != P?.gameObject) 
                    {
                        //Destroy(Targets[i].gameObject); // Risky if BotSpawner manages enemy base destruction elsewhere
                    }
                }
                Targets[i] = null; // Clear the reference
            }
            // Reset units and spells lists (or handle more gracefully depending on desired reset behavior)
            // For now, just clearing for simplicity, actual units might need to be destroyed.
            units.Clear(); 
            spells.Clear();
            // Consider resetting idCounter, allPlayersNfts, GameOver flag etc. for a true full reset.

            InitializePlayer(); // Re-run the full player and enemy setup initialization logic
        }
        
        // Handle respawn button click
        private void OnRespawnButtonClicked()
        {
            if (!isRespawning)
            {
                StartCoroutine(RespawnPlayerBaseStation());
            }
        }
        
        // Handle player base station death
        private void HandlePlayerBaseStationDeath(Unit baseStation)
        {
            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            
            if (baseStation == Targets[playerBaseIndex] && !isRespawning)
            {
                // Debug.Log("[GameMng] Player base station died, triggering death state");
                
                // Show death panel
                if (deathPanel != null)
                {
                    deathPanel.SetActive(true);
                }
                
                // Switch to death profile
                if (globalVolume != null && deathProfile != null)
                {
                    globalVolume.profile = deathProfile;
                }
                
                // Disable the base station
                baseStation.IsDeath = true;
                baseStation.UI.HideUI();
                
                // Enable ghost effect
                baseStation.SetGhostEffect(true);
                
                // Disable collider
                Collider collider = baseStation.Mesh.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                // Update player state
                if (P != null)
                {
                    P.IsAlive = false;
                }

                // Hide low health warning when player dies
                if (UI != null)
                {
                    UI.ShowLowHealthWarning(false);
                }

                // Return all player units to pool
                foreach (Unit unit in units.ToList()) // Use ToList to avoid collection modification issues
                {
                    if (unit != null && unit.IsMyTeam(P.MyTeam) && !unit.IsBaseStation)
                    {
                        // Return to pool through the player's system
                        P.ReturnUnitToPool(unit);
                    }
                }

                // Disable all enemy units
                foreach (Unit unit in units)
                {
                    if (unit != null && !unit.IsMyTeam(P.MyTeam))
                    {
                        unit.DisableUnit();
                        Shooter shooter = unit.GetComponent<Shooter>();
                        if (shooter != null)
                        {
                            shooter.StopAttack();
                        }
                    }
                }
            }
        }
        
        // Coroutine to respawn player base station
        private IEnumerator RespawnPlayerBaseStation()
        {
            isRespawning = true;
            // Debug.Log("[GameMng] Starting player respawn process");
            
            // Hide death panel
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }
            
            // Switch back to alive profile
            if (globalVolume != null && aliveProfile != null)
            {
                globalVolume.profile = aliveProfile;
            }
            
            // Get player base station
            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            Unit baseStation = Targets[playerBaseIndex];
            
            if (baseStation != null)
            {
                // Use the current position of the base station instead of the original position
                Vector3 respawnPosition = baseStation.transform.position;
                
                // Reset the base station
                baseStation.ResetUnit();
                baseStation.transform.position = respawnPosition;
                
                // Disable ghost effect
                baseStation.SetGhostEffect(false);
                
                // Ensure it's in the units list
                if (!units.Contains(baseStation))
                {
                    units.Add(baseStation);
                }
                
                // Restart shooter component if present
                var shooter = baseStation.GetComponent<Shooter>();
                if (shooter != null) 
                {
                    shooter.enabled = true;
                }

                // Update player state
                if (P != null)
                {
                    P.IsAlive = true;
                }

                // Re-enable all enemy units
                foreach (Unit unit in units)
                {
                    if (unit != null && !unit.IsMyTeam(P.MyTeam))
                    {
                        unit.EnableUnit();
                        Shooter unitShooter = unit.GetComponent<Shooter>();
                        if (unitShooter != null)
                        {
                            unitShooter.CanAttack = true;
                        }
                    }
                }
                
                // Debug.Log("[GameMng] Player respawn completed successfully");
            }
            else
            {
                Debug.LogError("[GameMng] Failed to find base station unit to respawn!");
            }
            
            isRespawning = false;
            yield break;
        }

        // Modified to handle skill application directly
        public Unit CreateUnit(GameObject obj, Vector3 position, Team team, string nftKey = "none", int playerId = -1)
        {
            Unit unit = Instantiate(obj, position, Quaternion.identity).GetComponent<Unit>();
            unit.MyTeam = team;
            unit.PlayerId = playerId == -1 ? P.ID : playerId;
            unit.setId(GenerateUnitId());
            
            NFTsUnit nftData = GetNftCardData(nftKey, unit.PlayerId) as NFTsUnit;
            if (nftData != null) {
                unit.SetNfts(nftData);
            }

            // Apply character skills to the unit
            if (characterSO != null)
            {
                characterSO.ApplySkillsOnDeploy(unit);
            }
            
            return unit;
        }

        // Modified to handle skill application directly
        public Spell CreateSpell(GameObject obj, Vector3 position, Team team, string nftKey = "none")
        {
            if (obj.GetComponent<Spell>() == null)
            {
                Debug.LogError($"Spell prefab is missing Spell component! NFT Key: {nftKey}");
                return null;
            }
            
            Spell spell = Instantiate(obj, position, Quaternion.identity).GetComponent<Spell>();
            spell.MyTeam = team;
            spell.setId(GenerateUnitId());
            
            NFTsSpell nftData = GetNftSpellData(nftKey, spell.PlayerId) as NFTsSpell;
            if (nftData != null) {
                spell.SetNfts(nftData);
            }

            // Apply character skills to the spell
            if (characterSO != null)
            {
                characterSO.ApplySkillsOnDeploy(spell);
            }
            
            AddSpell(spell);
            return spell;
        }

        public void AddUnit(Unit unit)
        {
            if (!units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        public void AddSpell(Spell spell)
        {
            if (!spells.Contains(spell))
            {
                spells.Add(spell);
            }
        }

        public void DeleteUnit(Unit unit)
        {
            if (units.Contains(unit))
            {
                units.Remove(unit);
                Destroy(unit.gameObject); // Ensure the unit's GameObject is destroyed
            }
        }

        public void DeleteSpell(Spell spell)
        {
            if (spells.Contains(spell))
            {
                spells.Remove(spell);
                Destroy(spell.gameObject); // Ensure the spell's GameObject is destroyed
            }
        }

        public void KillUnit(Unit unit)
        {
            if (unit != null)
            {
                unit.Die();
                DeleteUnit(unit); // Ensure the unit is removed from the list
            }
        }

        public void EndGame(Team winner)
        {
            if (GameOver) return; // Prevent multiple calls
            
            GameOver = true;
            Debug.Log($"Game Over! {winner} team wins!");
            UI.SetGameOver(winner);  // Update the UI with the game over status
        }

        public Color GetColorUnit(Team team, int playerId)
        {
            // Check if this is a player-controlled unit first (player's team and ID match)
            if (P != null && team == P.MyTeam && playerId == P.ID)
            {
                // Return a distinct color for the player's own units (green)
                return Color.green;
            }
            
            // Otherwise use team colors for enemies and allies not controlled by player
            return team == Team.Blue ? Color.blue : Color.red;
        }

        public Vector3 GetDefaultTargetPosition(Team team)
        {
            // Return the default target position based on team
            int index = team == Team.Blue ? 0 : 1;
            return BS_Positions[index];
        }

        public Transform GetFinalTransformTarget(Team team)
        {
            if (GameOver)
                return transform;

            // If player is dead and this is the enemy team targeting the player's team
            if (!P.IsAlive && team == Team.Red && Targets[1] != null && Targets[1].MyTeam == P.MyTeam)
            {
                // Return a fallback position (current position) instead of targeting dead player
                return transform;
            }

            return Targets[(int)team] != null ? Targets[(int)team].transform : transform;
        }

        public bool IsGameOver()
        {
            return GameOver;
        }

        public bool MainStationsExist()
        {
            // Check if both main stations (Targets) exist
            return Targets[0] != null && Targets[1] != null;
        }
        
        // Get the number of player lives remaining
        public int GetPlayerLivesRemaining()
        {
            return playerLivesRemaining;
        }

        public int GenerateUnitId()
        {
            idCounter++;
            return idCounter;
        }

        public void AddNftCardData(NFTsUnit nFTsCard, int playerId)
        {
            // Implement the logic to store or manage the NFTs data associated with a player.
            string finalKey = $"{playerId}_{nFTsCard.KeyId}";
            if (!allPlayersNfts.ContainsKey(finalKey))
            {
                allPlayersNfts.Add(finalKey, nFTsCard);
            }
        }
        
        // Add a method for spell data
        public void AddNftCardData(NFTsSpell nFTsSpell, int playerId)
        {
            // Similar logic as for units
            string finalKey = $"{playerId}_{nFTsSpell.KeyId}";
            if (!allPlayersNfts.ContainsKey(finalKey))
            {
                allPlayersNfts.Add(finalKey, nFTsSpell);
            }
        }

        public int CountUnits()
        {
            return units.Count;
        }

        public int CountUnits(Team team)
        {
            return units.Where(f => f.IsMyTeam(team)).Count();
        }

        private NFTsUnit GetNftCardData(string nftKey, int playerId)
        {
            string finalKey = $"{playerId}_{nftKey}";
            return allPlayersNfts.ContainsKey(finalKey) ? allPlayersNfts[finalKey] as NFTsUnit : null;
        }
        
        // Add a method for getting spell data
        private NFTsSpell GetNftSpellData(string nftKey, int playerId)
        {
            string finalKey = $"{playerId}_{nftKey}";
            return allPlayersNfts.ContainsKey(finalKey) ? allPlayersNfts[finalKey] as NFTsSpell : null;
        }

        // Get the SpellDataBase SO from a spell key
        public SpellsDataBase GetSpellSO(string spellKey)
        {
            // Try to get the SO from the player's deck first
            if (P != null && P.PlayerDeck != null)
            {
                foreach (NFTsCard card in P.PlayerDeck)
                {
                    if (card is NFTsSpell spell && spell.KeyId == spellKey)
                    {
                        // Get the SO from the card mapping using the new method
                        ScriptableObject so = P.GetCardSO(spellKey);
                        if (so is SpellsDataBase spellSO)
                        {
                            return spellSO;
                        }
                    }
                }
            }
            
            // If not found in player's deck, try to find it in the bot's deck
            BotEnemy bot = FindFirstObjectByType<BotEnemy>();
            if (bot != null)
            {
                foreach (SpellsDataBase spellSO in bot.DeckSpells)
                {
                    if (spellSO != null && spellSO.ToNFTCard().KeyId == spellKey)
                    {
                        return spellSO;
                    }
                }
            }
            
            return null;
        }

        public int GetRemainingSecs()
        {
            TimeSpan currentTime = timeOut.Add(startTime - DateTime.Now);
            return Mathf.Max(0, (int)currentTime.TotalSeconds);
        }
    }
}
