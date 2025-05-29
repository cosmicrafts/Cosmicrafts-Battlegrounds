using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    public class BotSpawner : MonoBehaviour
    {
        [Header("Bot Settings")]
        public CharacterBaseSO botBaseSO; // ScriptableObject containing bot data
        public int numberOfBots = 3; // Number of bots to spawn (including the base station)
        
        [Header("Bot Positioning")]
        [Tooltip("Center position for spawning additional bots")]
        public Vector3 spawnCenter = new Vector3(50, 0, 50);
        [Tooltip("Radius around the spawn center to place bots")]
        public float spawnRadius = 30f;
        [Tooltip("Minimum spacing between bots")]
        public float minBotSpacing = 10f;
        [Tooltip("Maximum attempts to find valid positions")]
        public int maxPositioningAttempts = 20;
        
        // Runtime data
        private List<BotEnemy> bots = new List<BotEnemy>();
        private Unit botBaseStation;
        private Team botTeam = Team.Red;
        private Vector3 baseStationPosition;
        private List<Vector3> usedPositions = new List<Vector3>();
        
        private void Awake()
        {
            // Set default spawn center if not changed
            if (spawnCenter == Vector3.zero)
            {
                spawnCenter = new Vector3(50, 0, 50);
            }
        }
        
        // Initialize with player team
        public void Initialize(Team playerTeam, Vector3 playerBasePosition)
        {
            botTeam = playerTeam == Team.Blue ? Team.Red : Team.Blue;
            
            // Calculate base station position on opposite side from player
            baseStationPosition = -playerBasePosition + (Vector3.up * playerBasePosition.y);
            if (baseStationPosition.magnitude < 20)
            {
                // Fallback if player position is near center
                baseStationPosition = playerBasePosition + new Vector3(100, 0, 100);
            }
            
            // Add base station position to used positions
            usedPositions.Add(playerBasePosition);
            usedPositions.Add(baseStationPosition);
            
            // Debug.Log($"BotSpawner initialized with team {botTeam}, base at {baseStationPosition}");
        }
        
        // Spawn everything needed
        public void SpawnBots()
        {
            if (numberOfBots <= 0 || botBaseSO == null || botBaseSO.BasePrefab == null)
            {
                Debug.Log("No bots to spawn or missing bot prefab");
                return;
            }
            
            // Clear any existing bots
            ClearBots();
            
            // Spawn bot base station
            SpawnBotBaseStation();
            
            // Spawn additional bots
            SpawnAdditionalBots();
            
            Debug.Log($"Spawned {bots.Count} total bots");
        }
        
        // Spawn the bot base station
        private void SpawnBotBaseStation()
        {
            // Create bot base station
            GameObject botBaseObj = botBaseSO.BasePrefab.GetComponent<BotEnemy>().prefabBaseStation;
            botBaseStation = Instantiate(botBaseObj, baseStationPosition, Quaternion.identity).GetComponent<Unit>();
            botBaseStation.PlayerId = 2;
            botBaseStation.MyTeam = botTeam;
            
            // Generate a unique ID
            botBaseStation.setId(Random.Range(1000, 9999));
            
            // Add base station's BotEnemy to the bots list (first bot)
            BotEnemy baseBot = botBaseStation.GetComponent<BotEnemy>();
            if (baseBot != null)
            {
                bots.Add(baseBot);
                baseBot.botName = "Bot_Base";
                // Debug.Log($"Bot base station created at {baseStationPosition}");
            }
        }
        
        // Spawn additional bots beyond the base station
        private void SpawnAdditionalBots()
        {
            if (botBaseStation == null || numberOfBots <= 1)
            {
                return;
            }
            
            // Calculate how many more bots to spawn
            int additionalBots = numberOfBots - 1;
            // Debug.Log($"Spawning {additionalBots} additional bots");
            
            for (int i = 0; i < additionalBots; i++)
            {
                // Find a valid position for this bot
                Vector3 botPosition = GetValidBotPosition();
                
                // Spawn the bot
                GameObject botObj = Instantiate(botBaseSO.BasePrefab, botPosition, Quaternion.identity);
                BotEnemy bot = botObj.GetComponent<BotEnemy>();
                
                if (bot == null)
                {
                    Debug.LogError("Bot prefab doesn't contain BotEnemy component!");
                    Destroy(botObj);
                    continue;
                }
                
                // Configure the bot
                Unit botUnit = bot.GetComponent<Unit>();
                if (botUnit != null)
                {
                    botUnit.MyTeam = botTeam;
                    botUnit.PlayerId = 2;
                    
                    // Apply character data from the scriptable object
                    botBaseSO.ApplyOverridesToUnit(botUnit);
                    botBaseSO.ApplySkillsOnDeploy(botUnit);
                    
                    botUnit.setId(Random.Range(10000, 99999));
                    // Debug.Log($"Additional bot {i+1} spawned at {botPosition}");
                }
                
                // Track the bot
                bots.Add(bot);
                bot.botName = $"Bot_{i+1}";
                
                // Remember this position
                usedPositions.Add(botPosition);
            }
        }
        
        // Find a valid position for a bot that's not too close to other positions
        private Vector3 GetValidBotPosition()
        {
            Vector3 position = Vector3.zero;
            bool validPosition = false;
            int attempts = 0;
            
            while (!validPosition && attempts < maxPositioningAttempts)
            {
                attempts++;
                
                // Get random position within spawn radius
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(0f, spawnRadius);
                
                // Calculate position using polar coordinates
                position = spawnCenter + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance
                );
                
                // Check if it's far enough from other positions
                validPosition = true;
                foreach (Vector3 usedPos in usedPositions)
                {
                    if (Vector3.Distance(position, usedPos) < minBotSpacing)
                    {
                        validPosition = false;
                        break;
                    }
                }
                
                // Always push away from base stations
                if (Vector3.Distance(position, baseStationPosition) < minBotSpacing * 2)
                {
                    validPosition = false;
                }
            }
            
            if (!validPosition)
            {
                // If we couldn't find a good position, use a fallback
                Debug.LogWarning("Couldn't find valid bot position - using fallback");
                position = spawnCenter + new Vector3(
                    Random.Range(-1f, 1f) * spawnRadius,
                    0f,
                    Random.Range(-1f, 1f) * spawnRadius
                );
            }
            
            return position;
        }
        
        public void RespawnBots(int newBotCount = -1)
        {
            // Update bot count if specified
            if (newBotCount >= 0)
            {
                numberOfBots = newBotCount;
            }
            
            // Respawn all bots
            SpawnBots();
        }
        
        public List<BotEnemy> GetBots()
        {
            return bots;
        }
        
        public void ClearBots()
        {
            foreach (BotEnemy bot in bots)
            {
                if (bot != null && bot.gameObject != null)
                {
                    Destroy(bot.gameObject);
                }
            }
            bots.Clear();
        }
        
        public Unit GetBotBaseStation()
        {
            return botBaseStation;
        }
    }
} 