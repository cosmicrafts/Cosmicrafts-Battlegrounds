namespace Cosmicrafts
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using System.Linq;
    using UnityEngine.Serialization;

    /// <summary>
    /// ProceduralWorldManager handles procedural generation of the game world, including tiles, enemies,
    /// and other objects. It uses a sector-based approach where the world is divided into grid cells.
    /// </summary>
    public class ProceduralWorldManager : MonoBehaviour
    {
        public static ProceduralWorldManager Instance;

        [Header("Sector Settings")]
        public int sectorSize = 100; // World units per sector
        public int visibleGridSize = 3; // Size of the visible grid (3x3)
        public int generationPerimeter = 1; // Additional perimeter of sectors to generate beyond visible grid

        [Header("Tile Settings")]
        public GameObject tilePrefab; // Base tile prefab
        public float tileSize = 10f; // Size of each tile
        [Range(0f, 1f)]
        public float tileVariationChance = 0.3f; // Chance for tile variation
        public List<Material> tileMaterials; // Different materials for tile variations

        [Header("Bot Spawning")]
        public int maxBotsPerSector = 3; // Maximum number of bots to spawn per sector

        [System.Serializable]
        public class BotSpawnSetting
        {
            public GameObject botPrefab; // The bot prefab to spawn
            [Range(0f, 100f)]
            public float spawnRate = 100f; // 0% = never spawn, 100% = common spawn
            [Tooltip("A descriptive name to help identify this bot in the Inspector")]
            public string botDescription;
        }

        // List exposed in the Inspector for configuration
        public List<BotSpawnSetting> botSpawnSettings = new List<BotSpawnSetting>();

        public float respawnDelay = 30f; // Delay before a bot can respawn in seconds

        [Header("Resource Settings")]
        [Range(0f, 1f)]
        public float resourceSpawnChance = 0.3f; // Chance to spawn resources
        public List<GameObject> resourcePrefabs; // Resource prefabs

        [Header("Debug Visualization")]
        public bool showSectorBounds = true;
        public Color sectorBoundColor = new Color(0, 1, 0, 0.3f);
        public bool showSpawnPoints = false;
        public Color spawnPointColor = new Color(1, 0, 0, 0.5f);

        // Private fields
        private Dictionary<Vector2Int, Sector> sectors = new Dictionary<Vector2Int, Sector>();
        private Vector2Int currentPlayerSector;
        private Transform playerTransform;
        private HashSet<Vector2Int> generatedSectors = new HashSet<Vector2Int>();
        
        // Object Pools
        private Dictionary<string, Queue<GameObject>> tilePool = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<GameObject, Queue<GameObject>> botPool = new Dictionary<GameObject, Queue<GameObject>>();
        private Dictionary<GameObject, Queue<GameObject>> resourcePool = new Dictionary<GameObject, Queue<GameObject>>();

        // Track active objects for cleanup
        private List<GameObject> activeTiles = new List<GameObject>();
        private List<GameObject> activeBots = new List<GameObject>();
        private List<GameObject> activeResources = new List<GameObject>();

        // Track sector probabilities
        private Dictionary<Vector2Int, int> sectorBotCounts = new Dictionary<Vector2Int, int>();
        
        // Random number generator with seed for consistency
        private System.Random rng;

        // Coroutines for respawn management
        private Dictionary<Vector2Int, Coroutine> sectorRespawnCoroutines = new Dictionary<Vector2Int, Coroutine>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize RNG with a seed based on time or fixed value
            rng = new System.Random(System.DateTime.Now.Millisecond);
        }

        private void Start()
        {
            // Find player reference - we'll use the base station from GameMng
            StartCoroutine(FindPlayer());
            
            // Initialize pools
            InitializePools();
        }

        private IEnumerator FindPlayer()
        {
            // Wait until GameMng is properly initialized
            yield return new WaitUntil(() => GameMng.GM != null && GameMng.P != null);
            
            // Use player's transform or transform of player's base
            if (GameMng.P != null && GameMng.P.GetComponent<Unit>() != null)
            {
                playerTransform = GameMng.P.GetComponent<Unit>().transform;
            }
            else if (GameMng.GM.Targets.Length > 1 && GameMng.GM.Targets[1] != null)
            {
                // Use player base station as fallback
                playerTransform = GameMng.GM.Targets[1].transform;
            }
            
            if (playerTransform != null)
            {
                // Generate initial sectors
                currentPlayerSector = WorldToSector(playerTransform.position);
                GenerateInitialSectors();
            }
            else
            {
                Debug.LogError("ProceduralWorldManager could not find player or base station transform!");
            }
        }

        private void Update()
        {
            if (playerTransform == null)
                return;
                
            // Check if player has moved to a new sector
            Vector2Int newPlayerSector = WorldToSector(playerTransform.position);
            if (newPlayerSector != currentPlayerSector)
            {
                // Player moved to a new sector
                currentPlayerSector = newPlayerSector;
                UpdateVisibleSectors();
            }
        }

        private void InitializePools()
        {
            // Initialize tile pool
            tilePool["default"] = new Queue<GameObject>();
            
            // Initialize bot pools
            botPool.Clear();
            foreach (var setting in botSpawnSettings)
            {
                if (setting != null && setting.botPrefab != null)
                {
                    botPool[setting.botPrefab] = new Queue<GameObject>();
                }
            }
            
            // Initialize resource pools
            foreach (var resourcePrefab in resourcePrefabs)
            {
                if (resourcePrefab != null)
                {
                    resourcePool[resourcePrefab] = new Queue<GameObject>();
                }
            }
        }

        #region Sector Management

        private void GenerateInitialSectors()
        {
            int perimeterSize = visibleGridSize + 2 * generationPerimeter;
            int halfSize = perimeterSize / 2;
            
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
            // Calculate new sectors to generate and old sectors to remove
            HashSet<Vector2Int> newSectorsNeeded = new HashSet<Vector2Int>();
            HashSet<Vector2Int> sectorsToRemove = new HashSet<Vector2Int>(generatedSectors);
            
            int fullSize = visibleGridSize + 2 * generationPerimeter;
            int halfSize = fullSize / 2;
            
            // Determine sectors that should be active
            for (int x = -halfSize; x <= halfSize; x++)
            {
                for (int y = -halfSize; y <= halfSize; y++)
                {
                    Vector2Int sectorCoord = new Vector2Int(currentPlayerSector.x + x, currentPlayerSector.y + y);
                    newSectorsNeeded.Add(sectorCoord);
                    sectorsToRemove.Remove(sectorCoord); // Keep this sector
                }
            }
            
            // Generate new sectors
            foreach (Vector2Int coord in newSectorsNeeded)
            {
                if (!generatedSectors.Contains(coord))
                {
                    GenerateSector(coord);
                }
            }
            
            // Remove sectors that are too far away
            foreach (Vector2Int coord in sectorsToRemove)
            {
                RemoveSector(coord);
            }
        }

        private void GenerateSector(Vector2Int sectorCoord)
        {
            if (generatedSectors.Contains(sectorCoord))
                return;
                
            // Create sector data object
            Sector newSector = ScriptableObject.CreateInstance<Sector>();
            newSector.sectorName = $"Sector ({sectorCoord.x}, {sectorCoord.y})";
            newSector.coordinate = sectorCoord;
            
            // Add to tracking collections
            sectors[sectorCoord] = newSector;
            generatedSectors.Add(sectorCoord);
            sectorBotCounts[sectorCoord] = 0;
            
            // Generate tiles for this sector
            GenerateTiles(sectorCoord);
            
            // Generate bots for this sector
            GenerateBots(sectorCoord);
            
            // Generate resources for this sector
            GenerateResources(sectorCoord);
        }

        private void RemoveSector(Vector2Int sectorCoord)
        {
            if (!generatedSectors.Contains(sectorCoord))
                return;
                
            // Get the sector
            if (sectors.TryGetValue(sectorCoord, out Sector sector))
            {
                // Return all objects for this sector to pools
                RecycleObjectsInSector(sectorCoord);
                
                // Stop any respawn coroutines for this sector
                if (sectorRespawnCoroutines.TryGetValue(sectorCoord, out Coroutine coroutine))
                {
                    if (coroutine != null)
                    {
                        StopCoroutine(coroutine);
                    }
                    sectorRespawnCoroutines.Remove(sectorCoord);
                }
                
                // Clean up sector data
                sectors.Remove(sectorCoord);
                generatedSectors.Remove(sectorCoord);
                sectorBotCounts.Remove(sectorCoord);
                
                // Destroy the sector ScriptableObject
                if (Application.isPlaying)
                {
                    Destroy(sector);
                }
            }
        }

        private void RecycleObjectsInSector(Vector2Int sectorCoord)
        {
            Vector3 sectorBottomLeft = SectorToWorld(sectorCoord);
            Vector3 sectorTopRight = sectorBottomLeft + new Vector3(sectorSize, 0, sectorSize);
            
            // Recycle tiles
            for (int i = activeTiles.Count - 1; i >= 0; i--)
            {
                GameObject tile = activeTiles[i];
                if (tile != null && IsPositionInSector(tile.transform.position, sectorBottomLeft, sectorTopRight))
                {
                    RecycleTile(tile);
                    activeTiles.RemoveAt(i);
                }
            }
            
            // Recycle bots
            for (int i = activeBots.Count - 1; i >= 0; i--)
            {
                GameObject bot = activeBots[i];
                if (bot != null && IsPositionInSector(bot.transform.position, sectorBottomLeft, sectorTopRight))
                {
                    RecycleBot(bot);
                    activeBots.RemoveAt(i);
                    
                    // Reduce bot count for this sector
                    if (sectorBotCounts.ContainsKey(sectorCoord))
                    {
                        sectorBotCounts[sectorCoord] = Mathf.Max(0, sectorBotCounts[sectorCoord] - 1);
                    }
                }
            }
            
            // Recycle resources
            for (int i = activeResources.Count - 1; i >= 0; i--)
            {
                GameObject resource = activeResources[i];
                if (resource != null && IsPositionInSector(resource.transform.position, sectorBottomLeft, sectorTopRight))
                {
                    RecycleResource(resource);
                    activeResources.RemoveAt(i);
                }
            }
        }

        private bool IsPositionInSector(Vector3 position, Vector3 sectorBottomLeft, Vector3 sectorTopRight)
        {
            return position.x >= sectorBottomLeft.x && position.x < sectorTopRight.x && 
                   position.z >= sectorBottomLeft.z && position.z < sectorTopRight.z;
        }

        #endregion

        #region Tile Generation

        private void GenerateTiles(Vector2Int sectorCoord)
        {
            if (tilePrefab == null)
                return;
                
            // Calculate world position of sector
            Vector3 sectorPosition = SectorToWorld(sectorCoord);
            
            // Determine how many tiles we need based on sector and tile size
            int tilesPerRow = Mathf.CeilToInt(sectorSize / tileSize);
            
            for (int x = 0; x < tilesPerRow; x++)
            {
                for (int z = 0; z < tilesPerRow; z++)
                {
                    // Calculate tile position within sector
                    Vector3 tilePosition = sectorPosition + new Vector3(x * tileSize, 0, z * tileSize);
                    
                    // Get tile from pool
                    GameObject tile = GetTileFromPool();
                    
                    // Position the tile
                    tile.transform.position = tilePosition;
                    
                    // Apply random variation
                    if (tileMaterials.Count > 0 && Random.value < tileVariationChance)
                    {
                        Renderer renderer = tile.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material = tileMaterials[Random.Range(0, tileMaterials.Count)];
                        }
                    }
                    
                    // Add to active tiles list
                    activeTiles.Add(tile);
                }
            }
        }

        private GameObject GetTileFromPool()
        {
            string poolKey = "default";
            
            // Check if pool exists and has available tiles
            if (tilePool.TryGetValue(poolKey, out Queue<GameObject> pool) && pool.Count > 0)
            {
                GameObject tile = pool.Dequeue();
                tile.SetActive(true);
                return tile;
            }
            
            // Create new tile if none in pool
            GameObject newTile = Instantiate(tilePrefab, transform);
            return newTile;
        }

        private void RecycleTile(GameObject tile)
        {
            if (tile == null)
                return;
                
            string poolKey = "default";
            
            // Reset tile properties
            tile.SetActive(false);
            
            // Return to pool
            if (!tilePool.ContainsKey(poolKey))
            {
                tilePool[poolKey] = new Queue<GameObject>();
            }
            
            tilePool[poolKey].Enqueue(tile);
        }

        #endregion

        #region Bot Generation

        private void GenerateBots(Vector2Int sectorCoord)
        {
            if (botSpawnSettings == null || botSpawnSettings.Count == 0)
                return;
            
            // Calculate world position of sector
            Vector3 sectorPosition = SectorToWorld(sectorCoord);
            
            // Get current bot count for this sector
            int currentBotCount = sectorBotCounts.TryGetValue(sectorCoord, out int count) ? count : 0;
            
            // Calculate how many more bots to spawn
            int botsToSpawn = maxBotsPerSector - currentBotCount;
            
            // Spawn bots up to the max
            for (int i = 0; i < botsToSpawn; i++)
            {
                // Get a random bot prefab based on spawn rates
                GameObject botPrefab = GetRandomBotPrefabByWeight();
                
                if (botPrefab != null)
                {
                    // Calculate random position within sector (avoid edges)
                    Vector3 botPosition = sectorPosition + new Vector3(
                        Random.Range(0.1f * sectorSize, 0.9f * sectorSize),
                        0, // Y position (height) - can adjust if needed
                        Random.Range(0.1f * sectorSize, 0.9f * sectorSize)
                    );
                    
                    // Spawn bot
                    GameObject spawnedBot = SpawnBot(botPrefab, botPosition);
                    
                    if (spawnedBot != null)
                    {
                        // Add to active bots list
                        activeBots.Add(spawnedBot);
                        
                        // Increment bot count for this sector
                        sectorBotCounts[sectorCoord]++;
                        
                        // Add event listener for bot death
                        Unit unitComponent = spawnedBot.GetComponent<Unit>();
                        if (unitComponent != null)
                        {
                            unitComponent.OnUnitDeath += (deadUnit) => OnBotDeath(deadUnit, sectorCoord);
                        }
                    }
                }
            }
        }

        // Gets a random bot prefab based on spawn rates
        private GameObject GetRandomBotPrefabByWeight()
        {
            if (botPool.Count == 0)
                return null;

            // Sum total weight
            float totalWeight = 0f;
            foreach (var setting in botSpawnSettings)
            {
                if (setting != null && setting.botPrefab != null && setting.spawnRate > 0f)
                {
                    totalWeight += setting.spawnRate;
                }
            }

            if (totalWeight <= 0f)
                return null;

            // Get random value based on total weight
            float randomValue = Random.Range(0f, totalWeight);
            float currentTotal = 0f;

            // Find the bot prefab that corresponds to the random value
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

            // Fallback to first valid prefab if something went wrong
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
                
            // Check if we have this bot type in the pool
            if (botPool.TryGetValue(botPrefab, out Queue<GameObject> pool) && pool.Count > 0)
            {
                // Get bot from pool
                GameObject botInstance = pool.Dequeue();
                
                // Reactivate and position the bot
                botInstance.SetActive(true);
                botInstance.transform.position = position;
                
                // Reset bot state if it has a Unit component
                Unit unitComponent = botInstance.GetComponent<Unit>();
                if (unitComponent != null)
                {
                    unitComponent.IsDeath = false;
                    unitComponent.HitPoints = unitComponent.GetMaxHitPoints();
                    unitComponent.Shield = unitComponent.GetMaxShield();
                }
                
                return botInstance;
            }
            
            // Create new bot if none in pool
            GameObject newBot = Instantiate(botPrefab, position, Quaternion.identity);
            
            // Setup any additional bot properties here
            // For example, setting Team, Faction, etc.
            Unit unit = newBot.GetComponent<Unit>();
            if (unit != null)
            {
                unit.MyFaction = Faction.Enemy; // Set faction to enemy
            }
            
            return newBot;
        }

        private void RecycleBot(GameObject bot)
        {
            if (bot == null)
                return;
                
            // First try to find which prefab this bot is based on
            GameObject matchingPrefab = null;
            foreach (var setting in botSpawnSettings)
            {
                if (setting != null && setting.botPrefab != null && 
                    bot.name.Contains(setting.botPrefab.name))
                {
                    matchingPrefab = setting.botPrefab;
                    break;
                }
            }
            
            // If we found the prefab, return to pool
            if (matchingPrefab != null)
            {
                // Deactivate the bot
                bot.SetActive(false);
                
                // Initialize pool queue if needed
                if (!botPool.ContainsKey(matchingPrefab))
                {
                    botPool[matchingPrefab] = new Queue<GameObject>();
                }
                
                // Return to pool
                botPool[matchingPrefab].Enqueue(bot);
            }
            else
            {
                // If we can't determine bot type, just destroy it
                Destroy(bot);
            }
        }

        private void OnBotDeath(Unit deadUnit, Vector2Int sectorCoord)
        {
            if (deadUnit == null || deadUnit.gameObject == null)
                return;
                
            // Remove from active bots list
            activeBots.Remove(deadUnit.gameObject);
            
            // Reduce bot count for this sector
            if (sectorBotCounts.ContainsKey(sectorCoord))
            {
                sectorBotCounts[sectorCoord] = Mathf.Max(0, sectorBotCounts[sectorCoord] - 1);
            }
            
            // Start respawn timer for this sector
            if (!sectorRespawnCoroutines.ContainsKey(sectorCoord))
            {
                sectorRespawnCoroutines[sectorCoord] = StartCoroutine(RespawnBotsInSector(sectorCoord));
            }
        }

        private IEnumerator RespawnBotsInSector(Vector2Int sectorCoord)
        {
            // Wait for respawn delay
            yield return new WaitForSeconds(respawnDelay);
            
            // Only respawn if sector is still active
            if (generatedSectors.Contains(sectorCoord))
            {
                // Regenerate bots in this sector
                GenerateBots(sectorCoord);
            }
            
            // Clear respawn coroutine reference
            sectorRespawnCoroutines.Remove(sectorCoord);
        }

        #endregion

        #region Resource Generation

        private void GenerateResources(Vector2Int sectorCoord)
        {
            if (resourcePrefabs == null || resourcePrefabs.Count == 0)
                return;
                
            // Calculate world position of sector
            Vector3 sectorPosition = SectorToWorld(sectorCoord);
            
            // Determine number of resources to spawn based on probability
            int resourcesToSpawn = 0;
            for (int i = 0; i < 5; i++) // Up to 5 resources per sector
            {
                if (Random.value < resourceSpawnChance)
                {
                    resourcesToSpawn++;
                }
            }
            
            // Spawn resources
            for (int i = 0; i < resourcesToSpawn; i++)
            {
                // Select a random resource type
                GameObject resourcePrefab = resourcePrefabs[Random.Range(0, resourcePrefabs.Count)];
                
                // Calculate random position within sector
                Vector3 resourcePosition = sectorPosition + new Vector3(
                    Random.Range(0f, sectorSize),
                    Random.Range(0f, sectorSize),
                    0
                );
                
                // Spawn resource
                GameObject resource = GetResourceFromPool(resourcePrefab);
                
                if (resource != null)
                {
                    resource.transform.position = resourcePosition;
                    resource.SetActive(true);
                    activeResources.Add(resource);
                }
            }
        }

        private GameObject GetResourceFromPool(GameObject prefab)
        {
            if (prefab == null)
                return null;
                
            // Check if pool exists and has available resources
            if (resourcePool.TryGetValue(prefab, out Queue<GameObject> pool) && pool.Count > 0)
            {
                GameObject resource = pool.Dequeue();
                resource.SetActive(true);
                return resource;
            }
            
            // Create new resource if none in pool
            GameObject newResource = Instantiate(prefab, transform);
            return newResource;
        }

        private void RecycleResource(GameObject resource)
        {
            if (resource == null)
                return;
                
            // Find which prefab this resource is based on
            GameObject matchingPrefab = null;
            foreach (var prefab in resourcePrefabs)
            {
                if (prefab != null && resource.name.Contains(prefab.name))
                {
                    matchingPrefab = prefab;
                    break;
                }
            }
            
            if (matchingPrefab != null)
            {
                // Reset resource properties
                resource.SetActive(false);
                
                // Return to pool
                if (!resourcePool.ContainsKey(matchingPrefab))
                {
                    resourcePool[matchingPrefab] = new Queue<GameObject>();
                }
                
                resourcePool[matchingPrefab].Enqueue(resource);
            }
            else
            {
                // If we can't determine resource type, just destroy it
                Destroy(resource);
            }
        }

        #endregion

        #region Utility Methods

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
                0, // Y is now height/up
                sectorCoord.y * sectorSize
            );
        }

        private void OnDrawGizmos()
        {
            if (!showSectorBounds && !showSpawnPoints)
                return;
                
            // Only show gizmos for generated sectors
            foreach (Vector2Int sectorCoord in generatedSectors)
            {
                Vector3 sectorPosition = SectorToWorld(sectorCoord);
                
                // Draw sector bounds
                if (showSectorBounds)
                {
                    Gizmos.color = sectorBoundColor;
                    Gizmos.DrawWireCube(
                        sectorPosition + new Vector3(sectorSize / 2, 0, sectorSize / 2),
                        new Vector3(sectorSize, 0.1f, sectorSize)
                    );
                }
                
                // Draw spawn points
                if (showSpawnPoints)
                {
                    Gizmos.color = spawnPointColor;
                    
                    // Calculate spawn points
                    for (int i = 0; i < maxBotsPerSector; i++)
                    {
                        Vector3 spawnPosition = sectorPosition + new Vector3(
                            Random.Range(0.1f * sectorSize, 0.9f * sectorSize),
                            0,
                            Random.Range(0.1f * sectorSize, 0.9f * sectorSize)
                        );
                        
                        Gizmos.DrawSphere(spawnPosition, 1f);
                    }
                }
            }
        }

        // Create a Sector ScriptableObject
        [System.Serializable]
        public class Sector : ScriptableObject
        {
            public string sectorName;
            public Vector2Int coordinate;
            public List<GameObject> activeObjects = new List<GameObject>();
        }

        #endregion
    }
} 