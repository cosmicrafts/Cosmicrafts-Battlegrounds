using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    public class BotSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class BotSpawnConfig
        {
            public CharacterBaseSO botBaseSO;
            [Range(0f, 1f)]
            public float spawnRate = 0.5f; // Probability of this bot type spawning
        }

        [Header("Bot Settings")]
        public List<BotSpawnConfig> botConfigs = new List<BotSpawnConfig>();
        public int maxActiveBots = 10; // Maximum number of bots active at once
        
        [Header("Spawn Distance")]
        [Tooltip("Minimum distance from player to spawn bots")]
        public float minSpawnDistance = 100f;
        [Tooltip("Maximum distance from player to spawn bots")]
        public float maxSpawnDistance = 250f;
        [Tooltip("Distance at which bots are recycled back to pool")]
        public float recycleDistance = 500f;
        
        // Runtime data
        private List<BotEnemy> activeBots = new List<BotEnemy>();
        private Unit botBaseStation;
        private Team botTeam = Team.Red;
        private Vector3 baseStationPosition;
        
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
        }
        
        // Spawn everything needed
        public void SpawnBots()
        {
            if (maxActiveBots <= 0 || botConfigs.Count == 0 || botConfigs[0].botBaseSO == null || botConfigs[0].botBaseSO.BasePrefab == null)
            {
                Debug.Log("No bots to spawn or missing bot prefab");
                return;
            }
            
            // Clear any existing bots
            ClearBots();
            
            // Spawn bot base station
            SpawnBotBaseStation();
            
            Debug.Log($"Spawned {activeBots.Count} total bots");
        }
        
        // Spawn the bot base station
        private void SpawnBotBaseStation()
        {
            // Create bot base station
            GameObject botBaseObj = botConfigs[0].botBaseSO.BasePrefab.GetComponent<BotEnemy>().prefabBaseStation;
            botBaseStation = Instantiate(botBaseObj, baseStationPosition, Quaternion.identity).GetComponent<Unit>();
            botBaseStation.PlayerId = 2;
            botBaseStation.MyTeam = botTeam;
            
            // Generate a unique ID
            botBaseStation.setId(Random.Range(1000, 9999));
            
            // Add base station's BotEnemy to the bots list (first bot)
            BotEnemy baseBot = botBaseStation.GetComponent<BotEnemy>();
            if (baseBot != null)
            {
                activeBots.Add(baseBot);
                baseBot.botName = "Bot_Base";
            }
        }
        
        public void RespawnBots(int newBotCount = -1)
        {
            // Update bot count if specified
            if (newBotCount >= 0)
            {
                maxActiveBots = newBotCount;
            }
            
            // Respawn all bots
            SpawnBots();
        }
        
        public List<BotEnemy> GetBots()
        {
            return activeBots;
        }
        
        public void ClearBots()
        {
            foreach (BotEnemy bot in activeBots)
            {
                if (bot != null && bot.gameObject != null)
                {
                    Destroy(bot.gameObject);
                }
            }
            activeBots.Clear();
        }
        
        public Unit GetBotBaseStation()
        {
            return botBaseStation;
        }

        private void Update()
        {
            if (GameMng.P == null) return;

            // Check for bots that need recycling
            CheckAndRecycleBots();

            // Try to spawn new bots if we're under the limit
            if (activeBots.Count < maxActiveBots)
            {
                TrySpawnNewBot();
            }
        }

        private void CheckAndRecycleBots()
        {
            for (int i = activeBots.Count - 1; i >= 0; i--)
            {
                BotEnemy bot = activeBots[i];
                if (bot == null || bot.gameObject == null) continue;

                float distanceToPlayer = Vector3.Distance(bot.transform.position, GameMng.P.transform.position);
                
                // If bot is too far, recycle it
                if (distanceToPlayer > recycleDistance)
                {
                    RecycleBot(bot);
                }
            }
        }

        private void TrySpawnNewBot()
        {
            // Get player position and level
            Vector3 playerPos = GameMng.P.transform.position;
            int playerLevel = GameMng.P.PlayerLevel;

            // Select a bot type based on spawn rates
            BotSpawnConfig selectedConfig = SelectBotConfig();
            if (selectedConfig == null) return;

            // Get spawn position
            Vector3 spawnPos = GetSpawnPositionAroundPlayer(playerPos);
            
            // Spawn the bot
            GameObject botObj = Instantiate(selectedConfig.botBaseSO.BasePrefab, spawnPos, Quaternion.identity);
            BotEnemy bot = botObj.GetComponent<BotEnemy>();
            
            if (bot != null)
            {
                Unit botUnit = bot.GetComponent<Unit>();
                if (botUnit != null)
                {
                    botUnit.MyTeam = botTeam;
                    botUnit.PlayerId = 2;
                    botUnit.Level = playerLevel; // Match player level
                    
                    // Apply character data
                    selectedConfig.botBaseSO.ApplyOverridesToUnit(botUnit);
                    selectedConfig.botBaseSO.ApplySkillsOnDeploy(botUnit);
                    
                    botUnit.setId(Random.Range(10000, 99999));
                }
                
                activeBots.Add(bot);
                bot.botName = $"Bot_{activeBots.Count}";
            }
        }

        private BotSpawnConfig SelectBotConfig()
        {
            if (botConfigs.Count == 0) return null;

            // Calculate total spawn rate
            float totalRate = 0f;
            foreach (var config in botConfigs)
            {
                totalRate += config.spawnRate;
            }

            // Select random value
            float random = Random.Range(0f, totalRate);
            float current = 0f;

            // Find selected config
            foreach (var config in botConfigs)
            {
                current += config.spawnRate;
                if (random <= current)
                {
                    return config;
                }
            }

            return botConfigs[0]; // Fallback
        }

        private Vector3 GetSpawnPositionAroundPlayer(Vector3 playerPos)
        {
            // Get random angle and distance
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
            
            // Calculate position
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );
            
            return playerPos + offset;
        }

        private void RecycleBot(BotEnemy bot)
        {
            if (bot == null) return;

            // Remove from active bots
            activeBots.Remove(bot);
            
            // Destroy the bot
            if (bot.gameObject != null)
            {
                Destroy(bot.gameObject);
            }
        }
    }
} 