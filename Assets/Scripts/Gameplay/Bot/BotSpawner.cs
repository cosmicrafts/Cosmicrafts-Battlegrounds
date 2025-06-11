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
        
        [Header("Encounter Settings")]
        [Tooltip("Chance to trigger an encounter per step (0-1)")]
        [Range(0f, 1f)]
        public float encounterProbabilityPerStep = 0.1f;
        
        [Tooltip("Distance player must move to count as a 'step'")]
        public float stepDistance = 2f;
        
        [Header("Spawn Distance")]
        [Tooltip("Minimum distance from player to spawn bots")]
        public float minSpawnDistance = 100f;
        [Tooltip("Maximum distance from player to spawn bots")]
        public float maxSpawnDistance = 250f;
        [Tooltip("Distance at which bots are recycled back to pool")]
        public float recycleDistance = 500f;
        
        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 20;
        private Dictionary<BotSpawnConfig, Queue<Bot>> botPools = new Dictionary<BotSpawnConfig, Queue<Bot>>();
        private Dictionary<Bot, BotSpawnConfig> botConfigMap = new Dictionary<Bot, BotSpawnConfig>();
        
        // Runtime data
        private List<Bot> activeBots = new List<Bot>();
        private Unit botBaseStation;
        private Team botTeam = Team.Red;
        private Vector3 baseStationPosition;
        private float stepDistanceCounter = 0f;
        private Vector3 lastPlayerPosition;
        
        private void InitializePools()
        {
            botPools.Clear();
            botConfigMap.Clear();

            foreach (var config in botConfigs)
            {
                if (config.botBaseSO != null && config.botBaseSO.BasePrefab != null)
                {
                    botPools[config] = new Queue<Bot>();
                    
                    // Pre-populate the pool
                    for (int i = 0; i < initialPoolSize; i++)
                    {
                        CreatePooledBot(config);
                    }
                }
            }
        }

        private void CreatePooledBot(BotSpawnConfig config)
        {
            GameObject botObj = Instantiate(config.botBaseSO.BasePrefab);
            Bot bot = botObj.GetComponent<Bot>();
            
            if (bot != null)
            {
                Unit botUnit = bot.GetComponent<Unit>();
                if (botUnit != null)
                {
                    // Initialize with player level
                    int playerLevel = GameMng.P != null ? GameMng.P.PlayerLevel : 1;
                    botUnit.Level = playerLevel;
                    Debug.Log($"Created new pooled bot with level {playerLevel}");
                }
                
                bot.gameObject.SetActive(false);
                botPools[config].Enqueue(bot);
                botConfigMap[bot] = config;
            }
        }

        private Bot GetBotFromPool(BotSpawnConfig config)
        {
            if (!botPools.ContainsKey(config) || botPools[config].Count == 0)
            {
                Debug.Log("Creating new bot for pool");
                CreatePooledBot(config);
            }

            if (botPools[config].Count > 0)
            {
                Bot bot = botPools[config].Dequeue();
                bot.gameObject.SetActive(true);
                
                // Get the current level before resetting
                Unit botUnit = bot.GetComponent<Unit>();
                if (botUnit != null)
                {
                    int currentLevel = botUnit.Level;
                    Debug.Log($"Getting bot from pool, current level {currentLevel}");
                    
                    // Reset the unit's state
                    botUnit.ResetUnit();
                    
                    // Restore the level immediately after reset
                    botUnit.Level = currentLevel;
                    Debug.Log($"Restored bot level to {currentLevel} after reset");
                }
                
                return bot;
            }

            return null;
        }

        private void ReturnBotToPool(Bot bot)
        {
            if (bot == null || !botConfigMap.ContainsKey(bot)) return;

            // Reset the bot's state before returning to pool
            Unit botUnit = bot.GetComponent<Unit>();
            if (botUnit != null)
            {
                int currentLevel = botUnit.Level;
                Debug.Log($"Returning bot to pool with level {currentLevel}");
                
                // Store the level before reset
                botUnit.ResetUnit();
                
                // Restore the level after reset
                botUnit.Level = currentLevel;
            }

            BotSpawnConfig config = botConfigMap[bot];
            bot.gameObject.SetActive(false);
            botPools[config].Enqueue(bot);
        }

        private void Start()
        {
            if (GameMng.P != null)
            {
                lastPlayerPosition = GameMng.P.transform.position;
            }
            InitializePools();
        }
        
        private void Update()
        {
            if (GameMng.P == null) return;

            // Track player movement for steps
            TrackPlayerSteps();

            // Check for bots that need recycling
            CheckAndRecycleBots();
        }

        private void TrackPlayerSteps()
        {
            Vector3 currentPos = GameMng.P.transform.position;
            float distanceMoved = Vector3.Distance(currentPos, lastPlayerPosition);
            stepDistanceCounter += distanceMoved;
            lastPlayerPosition = currentPos;

            // Trigger encounter check every "step"
            if (stepDistanceCounter >= stepDistance)
            {
                stepDistanceCounter = 0f;
                if (Random.value <= encounterProbabilityPerStep && activeBots.Count < maxActiveBots)
                {
                    TriggerRandomEncounter();
                }
            }
        }

        private void TriggerRandomEncounter()
        {
            // Get random position around player (circle, not grid)
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float spawnDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 spawnPos = GameMng.P.transform.position + new Vector3(randomDir.x, 0, randomDir.y) * spawnDistance;

            // Spawn the bot
            Bot newBot = TrySpawnNewBot(spawnPos);
            if (newBot != null)
            {
                activeBots.Add(newBot);
            }
        }

        private Bot TrySpawnNewBot(Vector3 spawnPosition)
        {
            // Get player level
            int playerLevel = GameMng.P != null ? GameMng.P.PlayerLevel : 1;
            Debug.Log($"Spawning new bot with player level: {playerLevel}");

            // Select a bot type based on spawn rates
            BotSpawnConfig selectedConfig = SelectBotConfig();
            if (selectedConfig == null) return null;
            
            // Get bot from pool
            Bot bot = GetBotFromPool(selectedConfig);
            if (bot != null)
            {
                bot.transform.position = spawnPosition;
                bot.transform.rotation = Quaternion.identity;
                
                Unit botUnit = bot.GetComponent<Unit>();
                if (botUnit != null)
                {
                    Debug.Log($"Setting bot level to {playerLevel} (was {botUnit.Level})");
                    // Set level and team BEFORE applying character data
                    botUnit.Level = playerLevel;
                    botUnit.MyTeam = botTeam;
                    botUnit.PlayerId = 2;
                    
                    // Apply character data with the correct level
                    selectedConfig.botBaseSO.ApplyOverridesToUnit(botUnit);
                    selectedConfig.botBaseSO.ApplySkillsOnDeploy(botUnit);
                    
                    // Set ID after all other properties are set
                    botUnit.setId(Random.Range(10000, 99999));

                    // Ensure UI is updated with the correct level immediately
                    UIUnit uiUnit = botUnit.GetComponentInChildren<UIUnit>();
                    if (uiUnit != null)
                    {
                        uiUnit.UpdateLevelText(playerLevel);
                    }
                }
                
                // Set the bot's CharacterBaseSO reference
                bot.botCharacterSO = selectedConfig.botBaseSO;
                bot.botName = $"Bot_{activeBots.Count}";
                return bot;
            }
            
            return null;
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

        private void CheckAndRecycleBots()
        {
            for (int i = activeBots.Count - 1; i >= 0; i--)
            {
                Bot bot = activeBots[i];
                if (bot == null || bot.gameObject == null)
                {
                    activeBots.RemoveAt(i);
                    continue;
                }

                float distanceToPlayer = Vector3.Distance(bot.transform.position, GameMng.P.transform.position);
                
                // If bot is too far, recycle it
                if (distanceToPlayer > recycleDistance)
                {
                    Debug.Log($"Recycling bot at distance {distanceToPlayer} (threshold: {recycleDistance})");
                    RecycleBot(bot);
                }
            }
        }

        private void RecycleBot(Bot bot)
        {
            if (bot == null) return;

            // Remove from active bots
            activeBots.Remove(bot);
            
            // Return to pool instead of destroying
            ReturnBotToPool(bot);
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
            GameObject botBaseObj = botConfigs[0].botBaseSO.BasePrefab.GetComponent<Bot>().prefabBaseStation;
            botBaseStation = Instantiate(botBaseObj, baseStationPosition, Quaternion.identity).GetComponent<Unit>();
            botBaseStation.PlayerId = 2;
            botBaseStation.MyTeam = botTeam;
            
            // Generate a unique ID
            botBaseStation.setId(Random.Range(1000, 9999));
            
            // Apply character data from SO
            if (botConfigs[0].botBaseSO != null)
            {
                botConfigs[0].botBaseSO.ApplyOverridesToUnit(botBaseStation);
                botConfigs[0].botBaseSO.ApplySkillsOnDeploy(botBaseStation);
            }
            
            // Add base station's Bot to the bots list (first bot)
            Bot baseBot = botBaseStation.GetComponent<Bot>();
            if (baseBot != null)
            {
                activeBots.Add(baseBot);
                baseBot.botName = "Bot_Base";
                baseBot.botCharacterSO = botConfigs[0].botBaseSO; // Set the SO reference
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
        
        public List<Bot> GetBots()
        {
            return activeBots;
        }
        
        public void ClearBots()
        {
            foreach (Bot bot in activeBots)
            {
                if (bot != null)
                {
                    ReturnBotToPool(bot);
                }
            }
            activeBots.Clear();
        }
        
        public Unit GetBotBaseStation()
        {
            return botBaseStation;
        }

        // Add method to update all active bots to match player level
        public void UpdateBotLevels()
        {
            if (GameMng.P == null) return;
            
            int playerLevel = GameMng.P.PlayerLevel;
            
            foreach (Bot bot in activeBots)
            {
                if (bot != null && bot.gameObject != null)
                {
                    Unit botUnit = bot.GetComponent<Unit>();
                    if (botUnit != null)
                    {
                        botUnit.Level = playerLevel;
                        
                        // Reapply character data to ensure stats are updated
                        if (bot.botCharacterSO != null)
                        {
                            bot.botCharacterSO.ApplyOverridesToUnit(botUnit);
                            bot.botCharacterSO.ApplySkillsOnDeploy(botUnit);
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up all pooled bots
            foreach (var pool in botPools.Values)
            {
                while (pool.Count > 0)
                {
                    Bot bot = pool.Dequeue();
                    if (bot != null)
                    {
                        Destroy(bot.gameObject);
                    }
                }
            }
            botPools.Clear();
            botConfigMap.Clear();
        }
    }
} 