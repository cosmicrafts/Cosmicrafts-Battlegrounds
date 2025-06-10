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
        
        // Runtime data
        private List<Bot> activeBots = new List<Bot>();
        private Unit botBaseStation;
        private Team botTeam = Team.Red;
        private Vector3 baseStationPosition;
        private float stepDistanceCounter = 0f;
        private Vector3 lastPlayerPosition;
        
        private void Start()
        {
            if (GameMng.P != null)
            {
                lastPlayerPosition = GameMng.P.transform.position;
            }
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

            // Select a bot type based on spawn rates
            BotSpawnConfig selectedConfig = SelectBotConfig();
            if (selectedConfig == null) return null;
            
            // Spawn the bot
            GameObject botObj = Instantiate(selectedConfig.botBaseSO.BasePrefab, spawnPosition, Quaternion.identity);
            Bot bot = botObj.GetComponent<Bot>();
            
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
                    RecycleBot(bot);
                }
            }
        }

        private void RecycleBot(Bot bot)
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
                        foreach (var config in botConfigs)
                        {
                            if (config.botBaseSO != null)
                            {
                                config.botBaseSO.ApplyOverridesToUnit(botUnit);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
} 