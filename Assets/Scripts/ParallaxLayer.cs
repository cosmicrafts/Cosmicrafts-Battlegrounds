namespace Cosmicrafts
{
    using UnityEngine;
    using System.Collections.Generic;

    /// <summary>
    /// Controls the parallax movement of a space layer relative to the camera.
    /// Lower parallax factors will move more slowly, creating a sense of depth.
    /// Can also generate a grid of planes to create a tiled background.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("How fast this layer moves relative to camera (0 = fixed in world space, 1 = fixed to camera)")]
        [Range(0f, 1f)]
        public float parallaxFactor = 0.1f;

        [Tooltip("Apply an extra speed factor to make layers move at different rates")]
        public Vector2 speedMultiplier = Vector2.one;

        [Header("Grid Generation")]
        [Tooltip("Size of the grid in X and Z (e.g. 3 = 3x3 grid)")]
        [Range(1, 5)]
        public int gridSize = 1;

        [Tooltip("If true, will generate the grid on start")]
        public bool generateGridOnStart = false;

        [Tooltip("Size of each grid tile. If zero, will use the object's renderer bounds.")]
        public Vector2 tileSize = Vector2.zero;

        // Used to track camera movement
        private Vector3 lastCameraPosition;
        private Camera mainCamera;
        
        // Flag to prevent recursive grid generation
        private bool isGeneratingGrid = false;
        
        // Track created grid objects
        private List<GameObject> gridObjects = new List<GameObject>();

        private void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                lastCameraPosition = mainCamera.transform.position;
            }
            else
            {
                Debug.LogWarning("ParallaxLayer could not find main camera. Using current position as fallback.");
                lastCameraPosition = transform.position;
            }
            
            // Generate grid if enabled
            if (generateGridOnStart && !isGeneratingGrid)
            {
                GenerateGrid();
            }
        }

        private void LateUpdate()
        {
            if (mainCamera == null) return;

            // Calculate camera movement delta this frame
            Vector3 cameraDelta = mainCamera.transform.position - lastCameraPosition;
            
            // For top-down game, we only care about X and Z movement
            // In a top-down perspective, Y is height/altitude, not depth
            cameraDelta.y = 0;

            // Apply parallax effect based on factor (inverse because we want things to move in the opposite direction)
            // 0 = stationary in world space, 1 = fixed to camera
            Vector3 parallaxDelta = cameraDelta * (1f - parallaxFactor);
            
            // Apply custom speed multiplier
            parallaxDelta.x *= speedMultiplier.x;
            parallaxDelta.z *= speedMultiplier.y;

            // Move the layer
            transform.position -= parallaxDelta;

            // Update last camera position
            lastCameraPosition = mainCamera.transform.position;
        }
        
        /// <summary>
        /// Generates a grid of duplicate planes around the original plane.
        /// </summary>
        public void GenerateGrid()
        {
            // Prevent recursive calls
            if (isGeneratingGrid) return;
            isGeneratingGrid = true;
            
            try
            {
                // First clean up any existing grid
                CleanupGrid();
                
                // If grid size is 1, we don't need to do anything
                if (gridSize <= 1)
                {
                    return;
                }
                
                // Determine tile size
                Vector2 gridTileSize = tileSize;
                
                // Auto-detect size if not specified
                if (gridTileSize.x <= 0 || gridTileSize.y <= 0)
                {
                    // Try to find a renderer to get dimensions
                    Renderer renderer = GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        renderer = GetComponentInChildren<Renderer>();
                    }
                    
                    if (renderer != null)
                    {
                        Bounds bounds = renderer.bounds;
                        gridTileSize = new Vector2(bounds.size.x, bounds.size.z);
                    }
                    else
                    {
                        // Default size
                        gridTileSize = new Vector2(10f, 10f);
                        Debug.LogWarning("ParallaxLayer: Could not find a Renderer to determine tile size. Using default size of 10x10.");
                    }
                }
                
                // Calculate grid dimensions
                int halfGrid = gridSize / 2;
                int startOffset = -halfGrid;
                int endOffset = halfGrid + (gridSize % 2); // Add 1 for odd grid sizes
                
                // Store original GameObject to duplicate
                GameObject originalObject = gameObject;
                
                // Create grid tiles
                for (int x = startOffset; x < endOffset; x++)
                {
                    for (int z = startOffset; z < endOffset; z++)
                    {
                        // Skip the center tile (that's the original)
                        if (x == 0 && z == 0) continue;
                        
                        // Calculate position offset for this tile
                        Vector3 positionOffset = new Vector3(x * gridTileSize.x, 0, z * gridTileSize.y);
                        
                        // Duplicate the original plane
                        GameObject duplicate = Instantiate(originalObject, originalObject.transform.parent);
                        
                        // Remove the original ParallaxLayer component from the duplicate to avoid recursion
                        ParallaxLayer duplicateLayer = duplicate.GetComponent<ParallaxLayer>();
                        if (duplicateLayer != null)
                        {
                            Destroy(duplicateLayer);
                        }

                        // Add a new ParallaxLayer component with grid generation disabled
                        ParallaxLayer newLayer = duplicate.AddComponent<ParallaxLayer>();
                        // Copy settings from the original
                        newLayer.parallaxFactor = this.parallaxFactor;
                        newLayer.speedMultiplier = this.speedMultiplier;
                        // Disable grid generation to prevent recursion
                        newLayer.gridSize = 1;
                        newLayer.generateGridOnStart = false;
                        
                        // Position the duplicate with the correct offset
                        duplicate.transform.position = originalObject.transform.position + positionOffset;
                        
                        // Name it for easier identification
                        duplicate.name = originalObject.name + $"_Tile_{x}_{z}";
                        
                        // Add to tracking list
                        gridObjects.Add(duplicate);
                    }
                }
            }
            finally
            {
                // Always clear the flag when done
                isGeneratingGrid = false;
            }
        }
        
        /// <summary>
        /// Cleans up any previously generated grid objects.
        /// </summary>
        public void CleanupGrid()
        {
            foreach (GameObject obj in gridObjects)
            {
                if (obj != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(obj);
                    }
                    else
                    {
                        DestroyImmediate(obj);
                    }
                }
            }
            gridObjects.Clear();
        }
        
        /// <summary>
        /// Helper method that can be called from a button in the editor or other scripts.
        /// </summary>
        public void RegenerateGrid()
        {
            CleanupGrid();
            GenerateGrid();
        }
        
        private void OnDestroy()
        {
            CleanupGrid();
        }
    }
} 