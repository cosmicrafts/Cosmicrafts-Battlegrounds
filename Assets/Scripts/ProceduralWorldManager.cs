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

        [System.Serializable]
        public class SpaceLayerSetting
        {
            [Tooltip("Descriptive name for this layer (e.g., 'Distant Stars', 'Nebulae')")]
            public string layerName = "New Space Layer";
            [Tooltip("Prefabs to randomly spawn for this layer.")]
            public GameObject[] layerPrefabs;
            [Tooltip("How many objects of this layer to spawn per sector.")]
            [Range(0, 100)]
            public int objectsPerSector = 10;
            [Tooltip("Parallax factor (0 = stationary in world space, 1 = fixed to camera)")]
            [Range(0f, 1f)]
            public float parallaxFactor = 0.1f;
            [Tooltip("Minimum and maximum height (Y) offset for objects in this layer")]
            public float minYOffset = -5f;
            public float maxYOffset = 5f;
            [Tooltip("Minimum and maximum scale for spawned objects.")]
            public Vector2 scaleRange = new Vector2(0.5f, 1.5f);
            [Tooltip("Optional custom parent transform for this layer. If null, a container will be created automatically.")]
            public Transform layerParentOverride;
            [Tooltip("Apply an extra speed multiplier for more varied parallax effects")]
            public Vector2 speedMultiplier = Vector2.one;
        }

        [Header("Space Layers")]
        [Tooltip("Configure different layers of procedurally generated space objects.")]
        public List<SpaceLayerSetting> spaceLayers = new List<SpaceLayerSetting>();

        [Header("Debug Visualization")]
        [Tooltip("Show sector boundaries in the Scene view")]
        public bool showSectorBounds = true;
        
        [Tooltip("Color of sector boundary lines")]
        public Color sectorBoundColor = new Color(0, 1, 0, 0.3f);

        // Private fields
        private Dictionary<Vector2Int, bool> generatedSectors = new Dictionary<Vector2Int, bool>();
        private Dictionary<Vector2Int, List<GameObject>> activeSectorObjects = new Dictionary<Vector2Int, List<GameObject>>();
        private Vector2Int currentPlayerSector;
        private Transform playerTransform;

        // Object tracking lists - using arrays for more efficient memory management
        
        // Bot count tracking
        
        // Object pools - reuse same objects instead of creating/destroying

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
            
            // Since this is attached to the camera that's following the player,
            // we can use our own transform as the reference point
            playerTransform = transform;
            isInitialized = true;
            isRunning = true;
            
            // Generate initial sectors once player is found
            currentPlayerSector = WorldToSector(playerTransform.position);
            GenerateInitialSectors();
            
            Debug.Log($"[ProceduralWorld] Successfully initialized at sector {currentPlayerSector}");
        }
        
        private bool TryFindPlayer()
        {
            // If we're attached to the camera, we can just use our own transform
            playerTransform = transform;
            return true;
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
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error in Update: {e.Message}");
                isRunning = false; // Stop processing to prevent more errors
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
                activeSectorObjects[sectorCoord] = new List<GameObject>(); // Initialize list for this sector

                // Calculate sector world position
                Vector3 sectorPosition = SectorToWorld(sectorCoord);

                // Spawn objects for each layer
                foreach (SpaceLayerSetting layerSetting in spaceLayers)
                {
                    if (layerSetting.layerPrefabs == null || layerSetting.layerPrefabs.Length == 0)
                    {
                        Debug.LogWarning($"[ProceduralWorld] Layer '{layerSetting.layerName}' has no prefabs assigned. Skipping.");
                        continue;
                    }

                    // Find or create the layer container
                    Transform layerParent = layerSetting.layerParentOverride;
                    if (layerParent == null)
                    {
                        string containerName = layerSetting.layerName + "_Container";
                        GameObject layerContainer = GameObject.Find(containerName);
                        
                        if (layerContainer == null)
                        {
                            layerContainer = new GameObject(containerName);
                            
                            // Add parallax controller component
                            ParallaxLayer parallaxController = layerContainer.AddComponent<ParallaxLayer>();
                            parallaxController.parallaxFactor = layerSetting.parallaxFactor;
                            parallaxController.speedMultiplier = layerSetting.speedMultiplier;
                        }
                        
                        layerParent = layerContainer.transform;
                    }

                    for (int i = 0; i < layerSetting.objectsPerSector; i++)
                    {
                        // Skip if no valid prefabs
                        if (layerSetting.layerPrefabs.Length == 0) continue;
                        
                        // Pick a random prefab from the array
                        int prefabIndex = Random.Range(0, layerSetting.layerPrefabs.Length);
                        GameObject prefabToSpawn = layerSetting.layerPrefabs[prefabIndex];
                        if (prefabToSpawn == null) continue;

                        // Generate random position within the sector
                        float randomX = Random.Range(0f, sectorSize);
                        float randomZ = Random.Range(0f, sectorSize);
                        float randomYOffset = Random.Range(layerSetting.minYOffset, layerSetting.maxYOffset);

                        // Calculate final position in world space
                        Vector3 spawnPosition = sectorPosition + new Vector3(randomX, randomYOffset, randomZ);

                        // Instantiate the object
                        GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
                        
                        // Apply random scale
                        float scale = Random.Range(layerSetting.scaleRange.x, layerSetting.scaleRange.y);
                        spawnedObject.transform.localScale = Vector3.one * scale;
                        
                        // Name it for easier debugging
                        spawnedObject.name = $"{layerSetting.layerName}_{sectorCoord.x}_{sectorCoord.y}_{i}";
                        
                        // Parent to the layer container
                        spawnedObject.transform.SetParent(layerParent, true);

                        // Add to tracking list for cleanup
                        activeSectorObjects[sectorCoord].Add(spawnedObject);
                    }
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
                if (activeSectorObjects.ContainsKey(sectorCoord))
                {
                    foreach (GameObject obj in activeSectorObjects[sectorCoord])
                    {
                        if (obj != null)
                        {
                            Destroy(obj);
                        }
                    }
                    activeSectorObjects[sectorCoord].Clear(); // Clear the list for this sector
                }
                
                // Remove from tracking
                generatedSectors.Remove(sectorCoord);
                activeSectorObjects.Remove(sectorCoord); // Also remove the entry from activeSectorObjects
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProceduralWorld] Error removing sector {sectorCoord}: {e.Message}");
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
            
            // Clean up all spawned procedural objects
            foreach (var sectorObjectList in activeSectorObjects.Values)
            {
                foreach (GameObject obj in sectorObjectList)
                {
                    if (obj != null)
                    {
                        Destroy(obj);
                    }
                }
            }
            activeSectorObjects.Clear();

            // Clean up pooled objects
        }
    }
} 