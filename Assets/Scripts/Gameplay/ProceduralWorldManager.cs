namespace Cosmicrafts
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Linq;

    /// <summary>
    /// ProceduralWorldManager handles procedural generation of the game world, including enemies.
    /// It uses a sector-based approach where the world is divided into grid cells.
    /// </summary>
    public class ProceduralWorldManager : MonoBehaviour
    {
        public static ProceduralWorldManager Instance;

        [Header("Sector Settings")]
        [Tooltip("Size of each sector in world units")]
        public int sectorSize = 250; 
        
        [Tooltip("Size of the visible grid around player (1=just player's sector, 3=3x3 grid with player in center)")]
        [Range(1, 5)]
        public int visibleGridSize = 1; 
        
        [Tooltip("Additional sectors to generate beyond visible grid for seamless transitions")]
        [Range(0, 2)]
        public int generationPerimeter = 1; 

        [Header("Bot Spawning")]
        [Tooltip("Maximum bots spawned per sector")]
        [Range(0, 10)]
        public int maxBotsPerSector = 1; 
        
        [Tooltip("Global limit on total active bots in the world")]
        [Range(1, 20)]
        public int maxTotalBots = 4;

        [System.Serializable]
        public class BotSpawnSetting
        {
            [Tooltip("The bot prefab to spawn")]
            public GameObject botPrefab;
            
            [Tooltip("Relative spawn chance (0=never, 100=common)")]
            [Range(0f, 100f)]
            public float spawnRate = 100f;
            
            [Tooltip("Optional description to identify this bot in the Inspector")]
            public string botDescription;
        }

        // List exposed in the Inspector for configuration
        [Tooltip("Configure different bot types with their relative spawn rates")]
        public List<BotSpawnSetting> botSpawnSettings = new List<BotSpawnSetting>();

        [Tooltip("Delay in seconds before respawning bots in a sector")]
        public float respawnDelay = 30f;

        [Header("Debug Visualization")]
        [Tooltip("Show sector boundaries in the Scene view")]
        public bool showSectorBounds = true;
        
        [Tooltip("Color of sector boundary lines")]
        public Color sectorBoundColor = new Color(0, 1, 0, 0.3f);

        // Private fields
        private Dictionary<Vector2Int, bool> generatedSectors = new Dictionary<Vector2Int, bool>();
        private Vector2Int currentPlayerSector;
        private Transform playerTransform;

        // Object tracking lists - using arrays for more efficient memory management
        private List<GameObject> activeBots = new List<GameObject>(20);  // Pre-allocate capacity
        
        // Bot count tracking
        private Dictionary<Vector2Int, int> sectorBotCounts = new Dictionary<Vector2Int, int>();
        
        // Object pools - reuse same objects instead of creating/destroying
        private Queue<GameObject> botPool = new Queue<GameObject>(20);

        private bool isInitialized = false;
        private bool isRunning = false;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }

        private void Start()
        {
            // Start initialization process - don't initialize immediately
            StartCoroutine(SafeInitialize());
        }
        
        private IEnumerator SafeInitialize()
        {
            // Safety delay to let other systems initialize
            yield return new WaitForSeconds(1f);
            
            // Try to find player - wait up to 5 seconds max to avoid infinite wait
            float timeWaited = 0;
            float maxWaitTime = 5f;
            
            while (timeWaited < maxWaitTime)
            {
                if (TryFindPlayer())
                {
                    isInitialized = true;
                    isRunning = true;
                    
                    // Generate initial sectors once player is found
                    currentPlayerSector = WorldToSector(playerTransform.position);
                    GenerateInitialSectors();
                    
                    Debug.Log($"[ProceduralWorld] Successfully initialized. Player found at sector {currentPlayerSector}");
                    
                    yield break;
                }
                
                yield return new WaitForSeconds(0.5f);
                timeWaited += 0.5f;
            }
            
            Debug.LogWarning("[ProceduralWorld] Could not initialize after waiting. Will not generate sectors.");
        }
        
        private bool TryFindPlayer()
        {
            try
            {
                // Try to get player from GameMng first
                if (GameMng.GM != null && GameMng.P != null)
                {
                    Unit playerUnit = GameMng.P.GetComponent<Unit>();
                    if (playerUnit != null)
                    {
                        playerTransform = playerUnit.transform;
                        return true;
                    }
                }
                
                // Try to get base station from GameMng as fallback
                if (GameMng.GM != null && GameMng.GM.Targets != null && GameMng.GM.Targets.Length > 1 && GameMng.GM.Targets[1] != null)
                {
                    playerTransform = GameMng.GM.Targets[1].transform;
                    return true;
                }
                
                // If GameMng approach fails, try to find main camera as a last resort
                if (Camera.main != null)
                {
                    playerTransform = Camera.main.transform;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error finding player: {e.Message}");
            }
            
            return false;
        }

        private void Update()
        {
            if (!isInitialized || !isRunning || playerTransform == null)
                return;
                
            try
            {
                // Check if player has moved to a new sector
                Vector2Int newPlayerSector = WorldToSector(playerTransform.position);
                if (newPlayerSector != currentPlayerSector)
                {
                    // Player moved to a new sector
                    currentPlayerSector = newPlayerSector;
                    UpdateVisibleSectors();
                }
                
                // Enforce the bot limit periodically
                if (Time.frameCount % 60 == 0) // Check every 60 frames
                {
                    if (activeBots.Count > maxTotalBots)
                    {
                        RemoveExcessBots();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error in Update: {e.Message}");
                isRunning = false; // Stop processing to prevent more errors
            }
        }
        
        private void RemoveExcessBots()
        {
            int excessCount = activeBots.Count - maxTotalBots;
            if (excessCount <= 0)
                return;
                
            // Remove bots starting from the end of the list (usually furthest from spawn point)
            for (int i = activeBots.Count - 1; i >= 0 && excessCount > 0; i--)
            {
                if (activeBots[i] != null)
                {
                    GameObject bot = activeBots[i];
                    activeBots.RemoveAt(i);
                    
                    // Update sector count
                    Vector2Int sector = WorldToSector(bot.transform.position);
                    if (sectorBotCounts.ContainsKey(sector))
                    {
                        sectorBotCounts[sector] = Mathf.Max(0, sectorBotCounts[sector] - 1);
                    }
                    
                    // Return to pool or destroy
                    RecycleBot(bot);
                    excessCount--;
                }
            }
        }

        private void GenerateInitialSectors()
        {
            int fullSize = visibleGridSize + 2 * generationPerimeter;
            int halfSize = fullSize / 2;
            
            for (int x = -halfSize; x <= halfSize; x++)
            {
                for (int y = -halfSize; y <= halfSize; y++)
                {
                    Vector2Int sectorCoord = new Vector2Int(currentPlayerSector.x + x, currentPlayerSector.y + y);
                    GenerateSector(sectorCoord);
                }
            }
        }

        private void UpdateVisibleSectors()
        {
            try
            {
                // Calculate the sectors that should be visible
                HashSet<Vector2Int> visibleSectors = new HashSet<Vector2Int>();
                
                int fullSize = visibleGridSize + 2 * generationPerimeter;
                int halfSize = fullSize / 2;
                
                // Determine sectors that should be active
                for (int x = -halfSize; x <= halfSize; x++)
                {
                    for (int y = -halfSize; y <= halfSize; y++)
                    {
                        Vector2Int sectorCoord = new Vector2Int(currentPlayerSector.x + x, currentPlayerSector.y + y);
                        visibleSectors.Add(sectorCoord);
                        
                        // Generate any new sectors needed
                        if (!generatedSectors.ContainsKey(sectorCoord))
                        {
                            GenerateSector(sectorCoord);
                        }
                    }
                }
                
                // Find sectors to remove (those that are no longer visible)
                List<Vector2Int> sectorsToRemove = new List<Vector2Int>();
                foreach (var sector in generatedSectors.Keys)
                {
                    if (!visibleSectors.Contains(sector))
                    {
                        sectorsToRemove.Add(sector);
                    }
                }
                
                // Remove sectors that are too far away
                foreach (Vector2Int sector in sectorsToRemove)
                {
                    RemoveSector(sector);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error updating sectors: {e.Message}");
            }
        }

        private void GenerateSector(Vector2Int sectorCoord)
        {
            if (generatedSectors.ContainsKey(sectorCoord))
                return;
            
            try
            {
                // Mark as generated
                generatedSectors[sectorCoord] = true;
                sectorBotCounts[sectorCoord] = 0;
                
                // Only generate bots if we're under the limit
                if (activeBots.Count < maxTotalBots)
                {
                    GenerateBots(sectorCoord);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error generating sector {sectorCoord}: {e.Message}");
            }
        }

        private void RemoveSector(Vector2Int sectorCoord)
        {
            if (!generatedSectors.ContainsKey(sectorCoord))
                return;
            
            try
            {
                // Delete all objects in this sector
                RecycleObjectsInSector(sectorCoord);
                
                // Remove from tracking
                generatedSectors.Remove(sectorCoord);
                sectorBotCounts.Remove(sectorCoord);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error removing sector {sectorCoord}: {e.Message}");
            }
        }

        private void RecycleObjectsInSector(Vector2Int sectorCoord)
        {
            Vector3 sectorBottomLeft = SectorToWorld(sectorCoord);
            Vector3 sectorTopRight = sectorBottomLeft + new Vector3(sectorSize, 0, sectorSize);
            
            // Recycle bots
            for (int i = activeBots.Count - 1; i >= 0; i--)
            {
                GameObject bot = activeBots[i];
                if (bot != null && IsPositionInSector(bot.transform.position, sectorBottomLeft, sectorTopRight))
                {
                    RecycleBot(bot);
                    activeBots.RemoveAt(i);
                }
            }
        }

        private bool IsPositionInSector(Vector3 position, Vector3 sectorBottomLeft, Vector3 sectorTopRight)
        {
            return position.x >= sectorBottomLeft.x && position.x < sectorTopRight.x && 
                   position.z >= sectorBottomLeft.z && position.z < sectorTopRight.z;
        }

        private void GenerateBots(Vector2Int sectorCoord)
        {
            if (botSpawnSettings == null || botSpawnSettings.Count == 0)
                return;
                
            if (activeBots.Count >= maxTotalBots)
                return;
                
            try
            {
                // Calculate world position of sector
                Vector3 sectorPosition = SectorToWorld(sectorCoord);
                
                // Get current bot count for this sector
                int currentBotCount = sectorBotCounts.ContainsKey(sectorCoord) ? sectorBotCounts[sectorCoord] : 0;
                
                // Calculate how many more bots to spawn
                int botsToSpawn = Mathf.Min(
                    maxBotsPerSector - currentBotCount,
                    maxTotalBots - activeBots.Count
                );
                
                // Spawn bots up to the max
                for (int i = 0; i < botsToSpawn; i++)
                {
                    // Get a random bot prefab based on spawn rates
                    GameObject botPrefab = GetRandomBotPrefab();
                    if (botPrefab == null) continue;
                    
                    // Calculate position
                    Vector3 botPosition = sectorPosition + new Vector3(
                        Random.Range(0.1f * sectorSize, 0.9f * sectorSize),
                        0,
                        Random.Range(0.1f * sectorSize, 0.9f * sectorSize)
                    );
                    
                    // Spawn bot
                    GameObject bot = SpawnBot(botPrefab, botPosition);
                    if (bot == null) continue;
                    
                    // Add to tracking
                    activeBots.Add(bot);
                    
                    // Increment sector bot count
                    if (!sectorBotCounts.ContainsKey(sectorCoord))
                    {
                        sectorBotCounts[sectorCoord] = 0;
                    }
                    sectorBotCounts[sectorCoord]++;
                    
                    // Setup death events
                    Unit unitComponent = bot.GetComponent<Unit>();
                    if (unitComponent != null)
                    {
                        // Store the sector coordinates for later use
                        Vector2Int sectorCopy = sectorCoord;
                        unitComponent.OnUnitDeath += (deadUnit) => 
                        {
                            // Decrement the sector bot count
                            if (sectorBotCounts.ContainsKey(sectorCopy))
                            {
                                sectorBotCounts[sectorCopy] = Mathf.Max(0, sectorBotCounts[sectorCopy] - 1);
                            }
                            
                            // Remove from active list
                            if (activeBots.Contains(deadUnit.gameObject))
                            {
                                activeBots.Remove(deadUnit.gameObject);
                            }
                            
                            // Return to pool
                            RecycleBot(deadUnit.gameObject);
                        };
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error generating bots: {e.Message}");
            }
        }

        private GameObject GetRandomBotPrefab()
        {
            if (botSpawnSettings == null || botSpawnSettings.Count == 0)
                return null;
                
            float totalWeight = 0f;
            
            // Calculate total weight
            foreach (var setting in botSpawnSettings)
            {
                if (setting != null && setting.botPrefab != null && setting.spawnRate > 0f)
                {
                    totalWeight += setting.spawnRate;
                }
            }
            
            if (totalWeight <= 0f)
                return null;
                
            // Select based on weight
            float randomValue = Random.Range(0f, totalWeight);
            float currentTotal = 0f;
            
            foreach (var setting in botSpawnSettings)
            {
                if (setting != null && setting.botPrefab != null && setting.spawnRate > 0f)
                {
                    currentTotal += setting.spawnRate;
                    if (randomValue <= currentTotal)
                    {
                        return setting.botPrefab;
                    }
                }
            }
            
            // Fallback to first valid
            foreach (var setting in botSpawnSettings)
            {
                if (setting != null && setting.botPrefab != null && setting.spawnRate > 0f)
                {
                    return setting.botPrefab;
                }
            }
            
            return null;
        }

        private GameObject SpawnBot(GameObject botPrefab, Vector3 position)
        {
            if (botPrefab == null)
                return null;
                
            try
            {
                // Get from pool if available 
                if (botPool.Count > 0)
                {
                    GameObject bot = botPool.Dequeue();
                    
                    // Reset and position
                    bot.SetActive(true);
                    bot.transform.position = position;
                    
                    // Reset unit component
                    Unit unit = bot.GetComponent<Unit>();
                    if (unit != null)
                    {
                        unit.IsDeath = false;
                        unit.HitPoints = unit.GetMaxHitPoints();
                        unit.Shield = unit.GetMaxShield();
                    }
                    
                    return bot;
                }
                
                // Create new if pool empty
                GameObject newBot = Instantiate(botPrefab, position, Quaternion.identity);
                
                // Set to enemy faction
                Unit newUnit = newBot.GetComponent<Unit>();
                if (newUnit != null)
                {
                    newUnit.MyFaction = Faction.Enemy;
                }
                
                return newBot;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error spawning bot: {e.Message}");
                return null;
            }
        }

        private void RecycleBot(GameObject bot)
        {
            if (bot == null)
                return;
                
            try
            {
                // Deactivate
                bot.SetActive(false);
                
                // Limit pool size
                if (botPool.Count < 20)
                {
                    botPool.Enqueue(bot);
                }
                else
                {
                    Destroy(bot);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error recycling bot: {e.Message}");
                Destroy(bot);
            }
        }

        private Vector2Int WorldToSector(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / sectorSize),
                Mathf.FloorToInt(worldPosition.z / sectorSize)
            );
        }

        private Vector3 SectorToWorld(Vector2Int sectorCoord)
        {
            return new Vector3(
                sectorCoord.x * sectorSize,
                0,
                sectorCoord.y * sectorSize
            );
        }

        private void OnDrawGizmos()
        {
            if (!showSectorBounds || !Application.isPlaying)
                return;
                
            // Draw sector boundaries for debugging
            Gizmos.color = sectorBoundColor;
            
            foreach (var sector in generatedSectors.Keys)
            {
                Vector3 sectorPosition = SectorToWorld(sector);
                Gizmos.DrawWireCube(
                    sectorPosition + new Vector3(sectorSize / 2, 0, sectorSize / 2),
                    new Vector3(sectorSize, 0.1f, sectorSize)
                );
            }
        }

        private void OnDestroy()
        {
            // Clean up all spawned objects when the manager is destroyed
            isRunning = false;
            
            // Clean up bots
            foreach (var bot in activeBots)
            {
                if (bot != null)
                {
                    Destroy(bot);
                }
            }
            activeBots.Clear();
            
            // Clean up pooled objects
            while (botPool.Count > 0)
            {
                GameObject bot = botPool.Dequeue();
                if (bot != null)
                {
                    Destroy(bot);
                }
            }
        }
    }
} 