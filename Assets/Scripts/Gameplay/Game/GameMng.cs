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
        public static Player P;
        public static GameMetrics MT;
        public static UIGameMng UI;
        public List<BotEnemy> Bots = new List<BotEnemy>(); // Replace single BOT with multiple Bots

        public CharacterBaseSO BotBaseSO; // ScriptableObject containing bot data
        public Vector3[] BS_Positions; // Base stations positions
        
        [Header("Bot Settings")]
        public int numberOfBots = 3; // Number of bots to spawn
        public float botSpacing = 100f; // Distance between bots in units
        public Vector3 botStartPosition = new Vector3(0, 0, 0); // Starting position for the first bot
        public Vector3 botSpacingDirection = new Vector3(1, 0, 0); // Direction to space the bots
        
        [Header("Player Respawn Settings")]
        public int playerLives = 9; // Number of lives before game over
        public float respawnDelay = 5f; // Seconds to wait before respawning
        public GameObject respawnEffectPrefab; // Optional visual effect for respawn
        
        private int playerLivesRemaining;
        private GameObject playerBaseStationPrefab; // Store reference to player's base station prefab
        private bool isRespawning = false;

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
            Debug.Log("--GAME MANAGER AWAKE--");

            // Init static unique controllers
            GM = this;

            Debug.Log("--GAME VARIABLES READY--");

            MT = new GameMetrics();
            MT.InitMetrics();
            
            // Initialize player lives
            playerLivesRemaining = playerLives;
        }

        private void Start()
        {
            Debug.Log("--GAME MANAGER START--");

            // Bots are now spawned in InitBaseStations
            
            Debug.Log("--GAME MANAGER READY--");
        }
        
        public Unit InitBaseStations(GameObject baseStationPrefab)
        {
            // Clear the bots list before spawning any new units
            Bots.Clear();

            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            int botBaseIndex = P.MyTeam == Team.Red ? 1 : 0;

            // Store the player base station prefab for respawning
            playerBaseStationPrefab = baseStationPrefab;

            Unit playerBaseStation = null;
            playerBaseStation = Instantiate(baseStationPrefab, BS_Positions[playerBaseIndex], Quaternion.identity).GetComponent<Unit>();
            Targets[playerBaseIndex] = playerBaseStation;
            
            // Explicitly set player base station team
            playerBaseStation.MyTeam = P.MyTeam;
            playerBaseStation.PlayerId = P.ID;
            
            // Debug log player base station
            Debug.Log($"Player base station created with team: {playerBaseStation.MyTeam}, at position: {BS_Positions[playerBaseIndex]}");

            // Create bot base station only if numberOfBots > 0, otherwise create a minimal placeholder
            if (numberOfBots > 0)
            {
                GameObject botBaseStation = BotBaseSO.BasePrefab.GetComponent<BotEnemy>().prefabBaseStation;
                Targets[botBaseIndex] = Instantiate(botBaseStation, BS_Positions[botBaseIndex], Quaternion.identity).GetComponent<Unit>();
                Targets[botBaseIndex].PlayerId = 2;
                Targets[botBaseIndex].MyTeam = Team.Red;
                
                // Base station should count as 1 bot
                int remainingBots = numberOfBots - 1;
                
                // Debug log bot base station
                Debug.Log($"Bot base station created with team: {Targets[botBaseIndex].MyTeam}, at position: {BS_Positions[botBaseIndex]}");
                
                // Add base station's BotEnemy to the Bots list (first bot)
                BotEnemy baseBot = Targets[botBaseIndex].GetComponent<BotEnemy>();
                if (baseBot != null)
                {
                    Bots.Add(baseBot);
                    baseBot.botName = "Bot_Base";
                    Debug.Log($"Added base station to Bots list (counts as 1 bot)");
                }
                
                // Set the IDs of the base stations
                for (int i = 0; i < Targets.Length; i++)
                {
                    if (Targets[i] != null)
                    {
                        Targets[i].setId(GenerateUnitId());
                    }
                }
                
                // Subscribe to player base station death event
                if (playerBaseStation != null)
                {
                    playerBaseStation.OnUnitDeath += HandlePlayerBaseStationDeath;
                }
                
                // Now spawn REMAINING bots with the same team as the bot base station
                if (remainingBots > 0 && Targets[botBaseIndex] != null)
                {
                    Team botTeam = Targets[botBaseIndex].MyTeam;
                    
                    // Ensure we have a valid character base SO
                    if (BotBaseSO == null || BotBaseSO.BasePrefab == null)
                    {
                        Debug.LogError("Bot Character Base SO or its BasePrefab is missing!");
                        return playerBaseStation;
                    }
                    
                    // Debug output before spawning
                    Debug.Log($"Starting additional bot spawning. Target position: {botStartPosition}, remaining bots: {remainingBots}");
                    
                    // CRITICAL CHECK: Ensure botStartPosition is far from both base stations
                    for (int i = 0; i < BS_Positions.Length; i++)
                    {
                        if (Vector3.Distance(botStartPosition, BS_Positions[i]) < 10f)
                        {
                            Debug.LogWarning($"Bot start position too close to base station {i}! Adjusting...");
                            botStartPosition = BS_Positions[i] + new Vector3(20f, 0f, 20f);
                        }
                    }
                    
                    // Spawn REMAINING bots at different positions
                    for (int i = 0; i < remainingBots; i++)
                    {
                        // Calculate position - ensure it's not at player position
                        Vector3 botPosition = botStartPosition + (botSpacingDirection.normalized * botSpacing * i);
                        
                        // Skip if too close to player base station
                        if (Vector3.Distance(botPosition, BS_Positions[playerBaseIndex]) < 5f)
                        {
                            botPosition += botSpacingDirection.normalized * 10f; // Push away from player
                            Debug.Log($"Adjusted bot position to avoid player base station");
                        }
                        
                        Debug.Log($"Spawning additional bot {i+1} at position {botPosition}");
                        
                        // Instantiate from the base prefab in the SO
                        GameObject botObj = Instantiate(BotBaseSO.BasePrefab, botPosition, Quaternion.identity);
                        BotEnemy bot = botObj.GetComponent<BotEnemy>();
                        
                        if (bot == null)
                        {
                            Debug.LogError("Bot prefab doesn't contain BotEnemy component!");
                            Destroy(botObj);
                            continue;
                        }
                        
                        // Apply scriptable object overrides to the bot unit
                        Unit botUnit = bot.GetComponent<Unit>();
                        if (botUnit != null)
                        {
                            // Set team to SAME as bot base station
                            botUnit.MyTeam = botTeam;
                            
                            // Apply character overrides (HP, shield, damage, etc.)
                            BotBaseSO.ApplyOverridesToUnit(botUnit);
                            
                            // Apply 'on deploy' skills
                            BotBaseSO.ApplySkillsOnDeploy(botUnit);
                            
                            // Log team assignment for debugging
                            Debug.Log($"Additional bot {i+1} spawned with team: {botUnit.MyTeam}, Player team: {P.MyTeam}, Position: {botObj.transform.position}");
                        }
                        
                        // Add to the Bots list
                        Bots.Add(bot);
                        
                        // Set unique bot name with index
                        bot.botName = $"Bot_{i+1}";
                        
                        // Debug output for detection troubleshooting
                        Shooter shooter = bot.GetComponent<Shooter>();
                        if (shooter != null)
                        {
                            Debug.Log($"Bot {i+1} CanAttack: {shooter.CanAttack}, RangeDetector: {shooter.RangeDetector}");
                        }
                    }
                    
                    Debug.Log($"Total bots in list: {Bots.Count} (should be {numberOfBots})");
                }
                else
                {
                    Debug.Log("No additional bots needed (numberOfBots = 1, base station counts as the bot)");
                }
            }
            else
            {
                // If numberOfBots is 0, don't create the full bot base station
                Debug.Log("No bots requested (numberOfBots = 0), skipping bot base station spawn");
                // We still need a placeholder in the Targets array
                Targets[botBaseIndex] = null;
            }

            return playerBaseStation;
        }
        
        // Handle player base station death
        private void HandlePlayerBaseStationDeath(Unit baseStation)
        {
            // Check if this is the player's base station
            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            
            if (baseStation == Targets[playerBaseIndex] && !isRespawning)
            {
                playerLivesRemaining--;
                Debug.Log($"Player base station destroyed! Lives remaining: {playerLivesRemaining}");
                
                if (playerLivesRemaining <= 0)
                {
                    // No more lives, end the game
                    EndGame(P.MyTeam == Team.Blue ? Team.Red : Team.Blue);
                }
                else
                {
                    // Update UI to show lives remaining - use Debug.Log instead of non-existent UI method
                    Debug.Log($"Player lives remaining: {playerLivesRemaining}");
                    
                    // Immediately reset position and health - no waiting
                    baseStation.transform.position = BS_Positions[playerBaseIndex];
                    baseStation.HitPoints = baseStation.GetMaxHitPoints();
                    baseStation.Shield = baseStation.GetMaxShield();
                    
                    // Make UI visible again
                    if (baseStation.UI != null && baseStation.UI.Canvas != null)
                    {
                        baseStation.UI.Canvas.SetActive(true);
                        baseStation.UI.SetHPBar(1f);
                        baseStation.UI.SetShieldBar(1f);
                    }
                    
                    // Reset death flag
                    baseStation.IsDeath = false;
                    
                    // Re-enable the collider
                    Collider collider = baseStation.Mesh.GetComponent<Collider>();
                    if (collider != null)
                    {
                        collider.enabled = true;
                    }
                    
                    //Debug.Log($"Instantly reset player base station to position {BS_Positions[playerBaseIndex]}");
                }
            }
        }
        
        // Coroutine to reset player base station
        private IEnumerator ResetPlayerBaseStation(Unit baseStation, int baseIndex)
        {
            isRespawning = true;
            
            // Debug the current position
            //Debug.Log($"[Reset] Base station current position: {baseStation.transform.position}, target position: {BS_Positions[baseIndex]}");
            
            // Wait for respawn delay
            yield return new WaitForSeconds(respawnDelay);
            
            if (GameOver)
            {
                isRespawning = false;
                yield break;
            }
            
            // Use SpellUtils to ensure we get the correct base station
            var (station, unit) = SpellUtils.FindPlayerMainStation(P.MyTeam, P.ID);
            
            // Fallback to our original reference if needed
            Unit targetUnit = unit != null ? unit : baseStation;
            
            // Create respawn effect if available
            if (respawnEffectPrefab != null)
            {
                GameObject effect = Instantiate(respawnEffectPrefab, BS_Positions[baseIndex], Quaternion.identity);
                Destroy(effect, 3f);
            }
            
            if (targetUnit != null)
            {
                // Make sure we're resetting the actual base station
                //Debug.Log($"[Reset] Found base station unit ID {targetUnit.getId()}, resetting...");
                
                // Reset unit first (makes it active again)
                targetUnit.ResetUnit();
                
                // Log before repositioning
                //Debug.Log($"[Reset] Before repositioning: {targetUnit.transform.position}");
                
                // Force position using teleport
                targetUnit.transform.SetPositionAndRotation(BS_Positions[baseIndex], Quaternion.identity);
                
                // Log after repositioning
                //Debug.Log($"[Reset] After repositioning: {targetUnit.transform.position}, Target: {BS_Positions[baseIndex]}");
                
                // Update the Targets reference
                Targets[baseIndex] = targetUnit;
                
                // Ensure it's in the units list
                if (!units.Contains(targetUnit))
                {
                    units.Add(targetUnit);
                }
                
                // Explicitly restart any needed components
                var shooter = targetUnit.GetComponent<Shooter>();
                if (shooter != null) 
                {
                    shooter.enabled = true;
                }
                
                //Debug.Log($"[Reset] Player base station reset complete at {targetUnit.transform.position}");
            }
            else
            {
                Debug.LogError("[Reset] Failed to find base station unit to reset!");
                
                // Last resort: recreate the base station
                Unit newBaseStation = Instantiate(playerBaseStationPrefab, BS_Positions[baseIndex], Quaternion.identity).GetComponent<Unit>();
                newBaseStation.setId(GenerateUnitId());
                newBaseStation.MyTeam = P.MyTeam;
                newBaseStation.PlayerId = P.ID;
                
                // Subscribe to its death event
                newBaseStation.OnUnitDeath += HandlePlayerBaseStationDeath;
                
                // Update the reference in Targets array
                Targets[baseIndex] = newBaseStation;
                
                // Add to units list
                AddUnit(newBaseStation);
                
                //Debug.Log($"[Reset] Player base station recreated at {BS_Positions[baseIndex]}");
            }
            
            isRespawning = false;
        }

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
            
            return unit;
        }

        public Spell CreateSpell(GameObject obj, Vector3 position, Team team, string nftKey = "none")
        {
            // Check if the prefab has a Spell component
            if (obj.GetComponent<Spell>() == null)
            {
                Debug.LogError($"Spell prefab is missing Spell component! NFT Key: {nftKey}");
                return null;
            }
            
            Spell spell = Instantiate(obj, position, Quaternion.identity).GetComponent<Spell>();
            spell.MyTeam = team;
            spell.setId(GenerateUnitId());
            
            // Set NFT data similar to CreateUnit method
            NFTsSpell nftData = GetNftSpellData(nftKey, spell.PlayerId) as NFTsSpell;
            if (nftData != null) {
                spell.SetNfts(nftData);
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
            // Return appropriate color based on team and player ID
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

        private int GenerateUnitId()
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

        public int GetRemainingSecs()
        {
            TimeSpan currentTime = timeOut.Add(startTime - DateTime.Now);
            return Mathf.Max(0, (int)currentTime.TotalSeconds);
        }

        // Add public method to respawn bots
        public void RespawnBots(int newBotCount = -1)
        {
            // Clean up existing bots
            foreach (BotEnemy bot in Bots)
            {
                if (bot != null && bot.gameObject != null)
                {
                    Destroy(bot.gameObject);
                }
            }
            Bots.Clear();
            
            // Update bot count if specified
            if (newBotCount >= 0)
            {
                numberOfBots = newBotCount;
                Debug.Log($"Updated numberOfBots to {numberOfBots}");
            }
            
            // Only spawn if greater than 0
            if (playerBaseStationPrefab != null)
            {
                Debug.Log($"Respawning with {numberOfBots} bots");
                InitBaseStations(playerBaseStationPrefab);
            }
            else
            {
                Debug.LogError("Cannot respawn bots: playerBaseStationPrefab is null");
            }
        }
    }
}
