using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    /// <summary>
    /// Manages a grid of procedural space background chunks around a target transform (e.g., the camera).
    /// Creates an infinite scrolling effect by pooling and regenerating chunks as the target moves.
    /// </summary>
    public class InfiniteSpaceGenerator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The transform to follow (typically the main camera)")]
        public Transform targetToFollow;
        [Tooltip("The prefab for a single background chunk (Plane with SpaceProceduralBackground script)")]
        public GameObject chunkPrefab;

        [Header("Grid Settings")]
        [Tooltip("Size of the grid (e.g., 3 means a 3x3 grid centered on the target)")]
        public int gridSize = 3; // Must be odd number for centering
        [Tooltip("The size of each chunk in world units")]
        public float chunkWorldSize = 1000f; // Should match the scale of the Plane in the prefab
        [Tooltip("The Z position for the background chunks")]
        public float backgroundZPosition = 100f;

        [Header("Generation Settings")]
        [Tooltip("Base seed used for generating chunk variations. Combined with chunk coordinates.")]
        public int baseSeed = 12345;

        [Header("Update Settings")]
        [Tooltip("How far the target must move (as a fraction of chunk size) before the grid updates")]
        [Range(0.1f, 1.0f)]
        public float moveUpdateThreshold = 0.5f;

        // --- Private Fields ---
        private GameObject[,] activeChunks;
        private Queue<GameObject> chunkPool = new Queue<GameObject>();
        private Vector2Int currentCenterGridCoord; // Integer grid coordinates of the center chunk
        private float actualMoveThreshold; // Calculated threshold in world units
        private bool isInitialized = false;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            if (chunkPrefab == null)
            {
                Debug.LogError("[InfiniteSpaceGenerator] Chunk Prefab is not assigned!", this);
                enabled = false;
                return;
            }
            if (targetToFollow == null)
            {
                Debug.LogWarning("[InfiniteSpaceGenerator] Target To Follow not assigned. Attempting to use Main Camera.");
                targetToFollow = Camera.main?.transform;
                if (targetToFollow == null)
                {
                    Debug.LogError("[InfiniteSpaceGenerator] Main Camera not found. Cannot initialize.", this);
                    enabled = false;
                    return;
                }
            }

            if (gridSize % 2 == 0)
            {
                gridSize++; // Ensure grid size is odd for proper centering
                Debug.LogWarning($"[InfiniteSpaceGenerator] Grid Size must be odd. Adjusting to {gridSize}.", this);
            }

            activeChunks = new GameObject[gridSize, gridSize];
            actualMoveThreshold = chunkWorldSize * moveUpdateThreshold;

            // Calculate initial center grid coordinate based on target position
            currentCenterGridCoord = GetGridCoordinates(targetToFollow.position);

            // Populate the initial grid
            PopulateInitialGrid();

            isInitialized = true;
            Debug.Log($"[InfiniteSpaceGenerator] Initialized with grid size {gridSize}x{gridSize} and chunk size {chunkWorldSize}. Center: {currentCenterGridCoord}");
        }

        void Update()
        {
            if (!isInitialized || targetToFollow == null) return;

            // Check if the target has moved enough to trigger a grid update
            Vector2Int targetGridCoord = GetGridCoordinates(targetToFollow.position);
            Vector2Int gridShift = targetGridCoord - currentCenterGridCoord;

            if (Mathf.Abs(gridShift.x) >= 1 || Mathf.Abs(gridShift.y) >= 1)
            {
                // The target has moved into a new grid cell, shift the active grid
                Debug.Log($"[InfiniteSpaceGenerator] Target moved to {targetGridCoord}. Shifting grid by {gridShift}.");
                ShiftGrid(gridShift);
                currentCenterGridCoord = targetGridCoord;
            }
            // --- Optional: Smoother update threshold check ---
            // Vector3 currentCenterWorldPos = GetWorldPosition(currentCenterGridCoord);
            // float distanceX = Mathf.Abs(targetToFollow.position.x - currentCenterWorldPos.x);
            // float distanceY = Mathf.Abs(targetToFollow.position.y - currentCenterWorldPos.y); // Assuming Y is up/down in your 2D plane view
            //
            // if (distanceX >= actualMoveThreshold || distanceY >= actualMoveThreshold)
            // {
            //     Vector2Int targetGridCoord = GetGridCoordinates(targetToFollow.position);
            //     Vector2Int gridShift = targetGridCoord - currentCenterGridCoord;
            //     if (gridShift.x != 0 || gridShift.y != 0) // Only shift if the target cell actually changed
            //     {
            //         Debug.Log($"[InfiniteSpaceGenerator] Target moved beyond threshold to {targetGridCoord}. Shifting grid by {gridShift}.");
            //         ShiftGrid(gridShift);
            //         currentCenterGridCoord = targetGridCoord;
            //     }
            // }
        }

        /// <summary>
        /// Calculates the integer grid coordinates corresponding to a world position.
        /// </summary>
        Vector2Int GetGridCoordinates(Vector3 worldPosition)
        {
            // Offset by half a chunk size before dividing to center the grid cells
            int gridX = Mathf.FloorToInt((worldPosition.x + chunkWorldSize * 0.5f) / chunkWorldSize);
            int gridY = Mathf.FloorToInt((worldPosition.y + chunkWorldSize * 0.5f) / chunkWorldSize); // Assuming Y is the relevant axis for vertical movement in your view
            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// Calculates the center world position for a given grid coordinate.
        /// </summary>
        Vector3 GetWorldPosition(Vector2Int gridCoord)
        {
            float worldX = gridCoord.x * chunkWorldSize;
            float worldY = gridCoord.y * chunkWorldSize; // Assuming Y is the relevant axis
            return new Vector3(worldX, worldY, backgroundZPosition);
        }

        /// <summary>
        /// Fills the initial grid with chunks centered around the starting target position.
        /// </summary>
        void PopulateInitialGrid()
        {
            int halfGrid = gridSize / 2;
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    // Calculate the actual grid coordinate for this slot
                    Vector2Int chunkGridCoord = currentCenterGridCoord + new Vector2Int(x - halfGrid, y - halfGrid);
                    activeChunks[x, y] = ActivateChunk(chunkGridCoord);
                }
            }
        }

        /// <summary>
        /// Shifts the active grid by the specified amount, pooling old chunks and activating new ones.
        /// </summary>
        void ShiftGrid(Vector2Int shiftAmount)
        {
            if (shiftAmount.x == 0 && shiftAmount.y == 0) return;

            GameObject[,] newActiveChunks = new GameObject[gridSize, gridSize];
            int halfGrid = gridSize / 2;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    // Calculate the old grid position this chunk *would* have come from
                    int oldX = x - shiftAmount.x;
                    int oldY = y - shiftAmount.y;

                    // Check if the old position was within the bounds of the previous grid
                    if (oldX >= 0 && oldX < gridSize && oldY >= 0 && oldY < gridSize)
                    {
                        // This chunk already existed, move it in the new grid
                        newActiveChunks[x, y] = activeChunks[oldX, oldY];
                        // Null out the spot in the old grid so we know what to pool later
                        activeChunks[oldX, oldY] = null;
                    }
                    else
                    {
                        // This is a new chunk that needs to be activated
                        Vector2Int chunkGridCoord = currentCenterGridCoord + shiftAmount + new Vector2Int(x - halfGrid, y - halfGrid);
                        newActiveChunks[x, y] = ActivateChunk(chunkGridCoord);
                    }
                }
            }

            // Pool any remaining chunks from the old grid that weren't moved
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (activeChunks[x, y] != null)
                    {
                        ReturnChunkToPool(activeChunks[x, y]);
                    }
                }
            }

            // Update the active chunks array
            activeChunks = newActiveChunks;
        }

        /// <summary>
        /// Gets a chunk from the pool or creates a new one, positions it, and triggers its generation.
        /// </summary>
        GameObject ActivateChunk(Vector2Int gridCoord)
        {
            GameObject chunk;
            if (chunkPool.Count > 0)
            {
                chunk = chunkPool.Dequeue();
                chunk.SetActive(true);
                //Debug.Log($"Reusing chunk for {gridCoord}");
            }
            else
            {
                chunk = Instantiate(chunkPrefab, transform); // Instantiate as child of the manager
                //Debug.Log($"Instantiating new chunk for {gridCoord}");
            }

            // Position the chunk
            chunk.transform.position = GetWorldPosition(gridCoord);
            chunk.name = $"SpaceChunk_{gridCoord.x}_{gridCoord.y}";

            // Get the procedural background component and generate the texture
            ProceduralSpaceBackground backgroundGenerator = chunk.GetComponent<ProceduralSpaceBackground>();
            if (backgroundGenerator != null)
            {
                // --- Call the generation method with chunk-specific coordinates and base seed ---
                backgroundGenerator.GenerateForChunk(gridCoord, baseSeed);
            }
            else
            {
                Debug.LogError($"Chunk Prefab '{chunkPrefab.name}' does not have ProceduralSpaceBackground component!", chunk);
            }

            return chunk;
        }

        /// <summary>
        /// Deactivates a chunk and returns it to the object pool.
        /// </summary>
        void ReturnChunkToPool(GameObject chunk)
        {
            if (chunk != null)
            {
                //Debug.Log($"Pooling chunk {chunk.name}");
                chunk.SetActive(false);
                chunkPool.Enqueue(chunk);
            }
        }

        // Optional: Clean up pool on destroy
        void OnDestroy()
        {
            while(chunkPool.Count > 0)
            {
                Destroy(chunkPool.Dequeue());
            }
            // Also destroy active chunks if needed
            if (activeChunks != null)
            {
                 for (int x = 0; x < gridSize; x++)
                 {
                    for (int y = 0; y < gridSize; y++)
                    {
                        if(activeChunks[x, y] != null) Destroy(activeChunks[x, y]);
                    }
                 }
            }
        }
    }
} 