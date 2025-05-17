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

        [Header("Player Setup")]
        [Tooltip("The character SO that defines player properties and base prefab")]
        public CharacterBaseSO characterSO;
        
        public Vector3[] BS_Positions; // Base stations positions
        
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

            // Initialize player from CharacterBaseSO
            if (characterSO != null && characterSO.BasePrefab != null)
            {
                InitializePlayer();
            }
            else
            {
                Debug.LogError("CharacterBaseSO or its BasePrefab not assigned in GameMng!");
            }
        }

        private void InitializePlayer()
        {
            // Store base station prefab reference
            playerBaseStationPrefab = characterSO.BasePrefab;
            
            // Instantiate the player from the SO's base prefab
            GameObject playerObj = Instantiate(characterSO.BasePrefab);
            P = playerObj.GetComponent<Player>();
            
            if (P == null)
            {
                Debug.LogError("Player component not found on CharacterBaseSO's BasePrefab!");
                return;
            }

            // Apply character overrides and skills
            if (characterSO != null)
            {
                // Apply base station overrides
                Unit baseStation = InitBaseStations(characterSO.BasePrefab);
                if (baseStation != null)
                {
                    characterSO.ApplyOverridesToUnit(baseStation);
                }
                
                // Apply gameplay modifiers
                characterSO.ApplyGameplayModifiers();
            }
        }

        public Unit InitBaseStations(GameObject baseStationPrefab)
        {
            int playerBaseIndex = P.MyTeam == Team.Blue ? 1 : 0;
            
            // Store the player base station prefab for respawning
            playerBaseStationPrefab = baseStationPrefab;

            // Create player base station
            Unit playerBaseStation = Instantiate(baseStationPrefab, BS_Positions[playerBaseIndex], Quaternion.identity).GetComponent<Unit>();
            Targets[playerBaseIndex] = playerBaseStation;
            
            // Configure player base station
            playerBaseStation.MyTeam = P.MyTeam;
            playerBaseStation.PlayerId = P.ID;
            playerBaseStation.setId(GenerateUnitId());
            
            // Subscribe to player base station death event
            playerBaseStation.OnUnitDeath += HandlePlayerBaseStationDeath;
            
            Debug.Log($"Player base station created with team: {playerBaseStation.MyTeam}, at position: {BS_Positions[playerBaseIndex]}");
            
            // Find BotSpawner in the scene
            BotSpawner botSpawner = FindFirstObjectByType<BotSpawner>();
            if (botSpawner != null)
            {
                // Let BotSpawner handle bot creation
                botSpawner.Initialize(P.MyTeam, BS_Positions[playerBaseIndex]);
                botSpawner.SpawnBots();
                
                // Store enemy base station in Targets array
                int botBaseIndex = P.MyTeam == Team.Blue ? 0 : 1;
                Targets[botBaseIndex] = botSpawner.GetBotBaseStation();
            }
            else
            {
                Debug.LogError("No BotSpawner found in the scene! Enemy team won't be created.");
            }
            
            return playerBaseStation;
        }

        // Public method to reinitialize base stations
        public void ReInitializeBaseStations()
        {
            if (playerBaseStationPrefab != null)
            {
                InitBaseStations(playerBaseStationPrefab);
            }
            else
            {
                Debug.LogError("Cannot reinitialize - playerBaseStationPrefab is null");
            }
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
                }
            }
        }
        
        // Coroutine to reset player base station
        private IEnumerator ResetPlayerBaseStation(Unit baseStation, int baseIndex)
        {
            isRespawning = true;
            
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
                
                // Reset unit first (makes it active again)
                targetUnit.ResetUnit();
                
                // Force position using teleport
                targetUnit.transform.SetPositionAndRotation(BS_Positions[baseIndex], Quaternion.identity);
                
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
            }
            
            isRespawning = false;
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

        public int GetRemainingSecs()
        {
            TimeSpan currentTime = timeOut.Add(startTime - DateTime.Now);
            return Mathf.Max(0, (int)currentTime.TotalSeconds);
        }
    }
}
