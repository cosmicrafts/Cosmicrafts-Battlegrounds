using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    /// <summary>
    /// Unified space background generator that creates and manages a grid of procedural
    /// background planes that move with the player/camera. Designed for isometric/top-down views.
    /// </summary>
    public class SpaceBackgroundGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        [Tooltip("Transform to follow (usually the camera/player)")]
        public Transform targetToFollow;
        [Tooltip("Size of each background tile in world units")]
        public float tileWorldSize = 1000f;
        [Tooltip("Number of tiles in the grid (e.g., 3x3, 5x5)")]
        public int gridSize = 3;
        [Tooltip("Y position for the background grid (lower values = further below)")]
        public float backgroundY = -10f;

        [Header("Rendering Settings")]
        [Tooltip("Does your game use 2D sorting layers or 3D depth?")]
        public bool use2DRendering = false;
        [Tooltip("Sorting layer name (only for 2D rendering)")]
        public string sortingLayerName = "Background";
        [Tooltip("Rendering sorting order (lower = further back)")]
        public int sortingOrder = -100;

        [Header("Visual Settings")]
        [Tooltip("Procedural background scene type")]
        public SceneType sceneType = SceneType.StarField;
        [Tooltip("Background base color")]
        public Color backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);
        [Tooltip("Random seed for generation (0 = random each time)")]
        public int seed = 0;

        [Header("Texture Settings")]
        [Tooltip("Width of the generated textures")]
        public int textureSize = 1024;
        [Tooltip("Whether to randomize the look on start")]
        public bool randomizeOnStart = true;

        [Header("Nebula Settings")]
        [Range(0f, 1f)]
        public float nebulaIntensity = 0.7f;
        [Range(0f, 40f)]
        public float nebulaScale = 20f;
        public bool enableTurbulence = true;
        [Range(0f, 20f)]
        public float turbulenceStrength = 10f;

        [Header("Star Settings")]
        [Range(0.001f, 0.02f)]
        public float starDensity = 0.005f;
        [Range(0.5f, 2f)]
        public float starBrightness = 1f;
        public bool enableStarColorVariation = true;
        public bool enableStarClusters = true;

        [Header("Dust Settings")]
        public bool enableDustLanes = true;
        [Range(0f, 1f)]
        public float dustLaneIntensity = 0.7f;

        // Scene type enum
        public enum SceneType 
        { 
            DenseNebula, 
            StarField, 
            DeepVoid, 
            MolecularCloud, 
            EnergyRift 
        }

        // Private variables for grid management
        private GameObject[,] activeChunks;
        private Queue<GameObject> chunkPool = new Queue<GameObject>();
        private Vector2Int currentCenterCoord;
        private float moveThreshold;
        private Material sharedMaterial;
        private int currentSeed;

        private void Start()
        {
            // Initialize seed
            currentSeed = seed == 0 ? Random.Range(1, 10000) : seed;
            
            // Find camera if target not specified
            if (targetToFollow == null)
            {
                targetToFollow = Camera.main?.transform;
                if (targetToFollow == null)
                {
                    Debug.LogError("No target to follow and no main camera found!");
                    enabled = false;
                    return;
                }
            }

            // Make sure gridSize is odd for better centering
            if (gridSize % 2 == 0)
            {
                gridSize++;
                Debug.LogWarning($"Grid size adjusted to {gridSize} (must be odd for proper centering)");
            }

            // Create a shared material for all planes
            CreateSharedMaterial();

            // Initialize grid and threshold
            activeChunks = new GameObject[gridSize, gridSize];
            moveThreshold = tileWorldSize * 0.3f; // Move when 30% of the way into a new cell

            // Get initial grid coordinate
            currentCenterCoord = GetGridCoord(targetToFollow.position);

            // Set up the initial grid
            InitializeGrid();
        }

        private void Update()
        {
            if (targetToFollow == null) return;

            // Calculate target position and grid coordinate
            Vector2Int targetCoord = GetGridCoord(targetToFollow.position);
            Vector2Int offset = targetCoord - currentCenterCoord;

            // Check if we need to shift the grid
            if (offset.x != 0 || offset.y != 0)
            {
                ShiftGrid(offset);
                currentCenterCoord = targetCoord;
            }
        }

        private void CreateSharedMaterial()
        {
            // Create a new material based on a simple unlit shader
            sharedMaterial = new Material(Shader.Find("Unlit/Texture"));
            sharedMaterial.color = Color.white; // Full color, texture will provide the color
            
            // Generate a texture based on current settings
            Texture2D generatedTexture = GenerateSpaceTexture(currentSeed, Vector2.zero);
            sharedMaterial.mainTexture = generatedTexture;
        }

        private void InitializeGrid()
        {
            int halfGrid = gridSize / 2;
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    // Position tiles centered around currentCenterCoord
                    Vector2Int tileCoord = currentCenterCoord + new Vector2Int(x - halfGrid, y - halfGrid);
                    activeChunks[x, y] = CreateBackgroundTile(tileCoord);
                }
            }
        }

        private void ShiftGrid(Vector2Int offset)
        {
            // Create a new grid with shifted content
            GameObject[,] newGrid = new GameObject[gridSize, gridSize];
            int halfGrid = gridSize / 2;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    // Calculate where this tile should come from in the old grid
                    int oldX = x - offset.x;
                    int oldY = y - offset.y;

                    if (oldX >= 0 && oldX < gridSize && oldY >= 0 && oldY < gridSize)
                    {
                        // This tile still exists in the grid, just moved
                        newGrid[x, y] = activeChunks[oldX, oldY];
                    }
                    else
                    {
                        // This is a new tile that needs to be created
                        Vector2Int tileCoord = currentCenterCoord + new Vector2Int(x - halfGrid, y - halfGrid) + offset;
                        newGrid[x, y] = CreateBackgroundTile(tileCoord);
                    }
                }
            }

            // Find tiles that are no longer needed and return them to the pool
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    bool stillInNewGrid = false;
                    // Check if this tile is still in the new grid
                    foreach (GameObject tile in newGrid)
                    {
                        if (activeChunks[x, y] == tile)
                        {
                            stillInNewGrid = true;
                            break;
                        }
                    }

                    if (!stillInNewGrid && activeChunks[x, y] != null)
                    {
                        ReturnTileToPool(activeChunks[x, y]);
                    }
                }
            }

            // Update our active chunks
            activeChunks = newGrid;
        }

        private GameObject CreateBackgroundTile(Vector2Int coord)
        {
            GameObject tile;
            // For top-down/isometric, use X and Z for positioning with Y as the height
            Vector3 position = new Vector3(coord.x * tileWorldSize, backgroundY, coord.y * tileWorldSize);

            // Try to get from pool
            if (chunkPool.Count > 0)
            {
                tile = chunkPool.Dequeue();
                tile.SetActive(true);
                tile.transform.position = position;
                
                // Update rendering settings based on rendering mode
                if (use2DRendering)
                {
                    SpriteRenderer spriteRenderer = tile.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sortingLayerName = sortingLayerName;
                        spriteRenderer.sortingOrder = sortingOrder;
                    }
                }
            }
            else
            {
                // Create a new tile based on rendering mode
                if (use2DRendering)
                {
                    // Create a quad with sprite renderer for 2D
                    tile = new GameObject($"SpaceTile_{coord.x}_{coord.y}");
                    SpriteRenderer spriteRenderer = tile.AddComponent<SpriteRenderer>();
                    spriteRenderer.sortingLayerName = sortingLayerName;
                    spriteRenderer.sortingOrder = sortingOrder;
                    
                    // Create the texture and assign it
                    Texture2D uniqueTexture = GenerateSpaceTexture(currentSeed, new Vector2(coord.x, coord.y));
                    Sprite sprite = Sprite.Create(uniqueTexture, new Rect(0, 0, uniqueTexture.width, uniqueTexture.height), 
                                                  new Vector2(0.5f, 0.5f), 100f);
                    spriteRenderer.sprite = sprite;
                    
                    // Scale to match the specified world size
                    float scale = tileWorldSize / 10f;
                    tile.transform.localScale = new Vector3(scale, scale, 1f);
                }
                else
                {
                    // Create a 3D plane for isometric/3D
                    tile = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    
                    // Scale to match the specified world size
                    float scale = tileWorldSize / 10f; // Default plane is 10x10 units
                    tile.transform.localScale = new Vector3(scale, 1f, scale);
                    
                    // Set material
                    Renderer renderer = tile.GetComponent<Renderer>();
                    
                    // Create a unique copy of the material for this tile with a unique texture
                    Material uniqueMaterial = new Material(sharedMaterial);
                    Texture2D uniqueTexture = GenerateSpaceTexture(currentSeed, new Vector2(coord.x, coord.y));
                    uniqueMaterial.mainTexture = uniqueTexture;
                    renderer.material = uniqueMaterial;
                }
                
                // Common setup
                tile.name = $"SpaceTile_{coord.x}_{coord.y}";
                tile.transform.position = position;
                tile.transform.SetParent(transform);
            }

            return tile;
        }

        private void ReturnTileToPool(GameObject tile)
        {
            tile.SetActive(false);
            chunkPool.Enqueue(tile);
        }

        private Vector2Int GetGridCoord(Vector3 worldPosition)
        {
            // For top-down/isometric view, use X and Z coordinates
            int x = Mathf.FloorToInt(worldPosition.x / tileWorldSize);
            int z = Mathf.FloorToInt(worldPosition.z / tileWorldSize);
            return new Vector2Int(x, z);
        }

        private Texture2D GenerateSpaceTexture(int textureSeed, Vector2 offset)
        {
            // Create a new texture
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            // Initialize with background color
            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = backgroundColor;
            }

            // Set the random seed for consistent generation
            Random.InitState(textureSeed + offset.x.GetHashCode() + offset.y.GetHashCode());

            // Apply visual elements based on scene type
            ApplyNebulaToTexture(pixels, offset);
            ApplyStarsToTexture(pixels, offset);
            if (enableDustLanes) ApplyDustToTexture(pixels, offset);

            // Apply the pixels to the texture
            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        private void ApplyNebulaToTexture(Color[] pixels, Vector2 offset)
        {
            // Apply different nebula patterns based on scene type
            float baseIntensity = nebulaIntensity;
            
            switch (sceneType)
            {
                case SceneType.DenseNebula:
                    baseIntensity *= 1.2f;
                    break;
                case SceneType.StarField:
                    baseIntensity *= 0.3f;
                    break;
                case SceneType.DeepVoid:
                    baseIntensity *= 0.2f;
                    break;
                case SceneType.MolecularCloud:
                    baseIntensity *= 0.8f;
                    break;
                case SceneType.EnergyRift:
                    baseIntensity *= 1.0f;
                    break;
            }

            // Apply procedural nebula pattern
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    // Normalized coordinates (0-1)
                    float nx = (float)x / textureSize;
                    float ny = (float)y / textureSize;
                    
                    // Apply offset for seamless tiling
                    float ox = nx + offset.x;
                    float oy = ny + offset.y;
                    
                    // Generate perlin noise for nebula with multiple octaves
                    float noise = 0f;
                    float scale = nebulaScale;
                    float amplitude = 1f;
                    float frequency = 1f;
                    float totalAmplitude = 0f;
                    
                    // Apply turbulence if enabled
                    if (enableTurbulence)
                    {
                        float turbulence = Mathf.PerlinNoise(ox * 2f, oy * 2f) * turbulenceStrength;
                        ox += turbulence * 0.01f;
                        oy += turbulence * 0.01f;
                    }
                    
                    // Add multiple layers of noise
                    for (int i = 0; i < 4; i++)
                    {
                        noise += Mathf.PerlinNoise(ox * frequency / scale, oy * frequency / scale) * amplitude;
                        totalAmplitude += amplitude;
                        amplitude *= 0.5f;
                        frequency *= 2f;
                    }
                    
                    // Normalize the noise
                    noise /= totalAmplitude;
                    
                    // Apply intensity and threshold
                    noise = Mathf.Pow(noise, 1.5f) * baseIntensity;
                    
                    // Skip if noise is too low
                    if (noise < 0.05f) continue;
                    
                    // Calculate pixel index
                    int index = y * textureSize + x;
                    
                    // Get a color from the nebula gradient based on the noise
                    Color nebulaColor = GetNebulaColor(noise);
                    
                    // Blend with the background
                    pixels[index] = Color.Lerp(pixels[index], nebulaColor, noise * 0.8f);
                }
            }
        }

        private Color GetNebulaColor(float value)
        {
            // Simple color mapping based on scene type
            switch (sceneType)
            {
                case SceneType.DenseNebula:
                    return Color.Lerp(
                        Color.Lerp(new Color(0.1f, 0.1f, 0.3f), new Color(0.3f, 0.1f, 0.5f), value), 
                        new Color(0.7f, 0.3f, 0.7f), 
                        value * value
                    );
                
                case SceneType.StarField:
                    return Color.Lerp(
                        Color.Lerp(new Color(0.05f, 0.05f, 0.1f), new Color(0.1f, 0.1f, 0.3f), value),
                        new Color(0.2f, 0.2f, 0.4f),
                        value * value
                    );
                
                case SceneType.DeepVoid:
                    return Color.Lerp(
                        Color.Lerp(new Color(0.02f, 0.02f, 0.05f), new Color(0.05f, 0.05f, 0.1f), value),
                        new Color(0.1f, 0.05f, 0.2f),
                        value * value
                    );
                
                case SceneType.MolecularCloud:
                    return Color.Lerp(
                        Color.Lerp(new Color(0.05f, 0.1f, 0.05f), new Color(0.1f, 0.2f, 0.1f), value),
                        new Color(0.2f, 0.4f, 0.3f),
                        value * value
                    );
                
                case SceneType.EnergyRift:
                    return Color.Lerp(
                        Color.Lerp(new Color(0.2f, 0.05f, 0f), new Color(0.5f, 0.1f, 0f), value),
                        new Color(0.8f, 0.4f, 0.1f),
                        value * value
                    );
                
                default:
                    return Color.Lerp(Color.black, new Color(0.3f, 0.3f, 0.6f), value);
            }
        }

        private void ApplyStarsToTexture(Color[] pixels, Vector2 offset)
        {
            // Number of stars based on density
            int starCount = Mathf.FloorToInt(textureSize * textureSize * starDensity);
            
            // Adjust star count based on scene type
            switch (sceneType)
            {
                case SceneType.StarField:
                    starCount = Mathf.FloorToInt(starCount * 2f);
                    break;
                case SceneType.DeepVoid:
                    starCount = Mathf.FloorToInt(starCount * 0.7f);
                    break;
                case SceneType.DenseNebula:
                    starCount = Mathf.FloorToInt(starCount * 0.8f);
                    break;
            }
            
            // Generate stars
            for (int i = 0; i < starCount; i++)
            {
                // Random position
                int x = Random.Range(0, textureSize);
                int y = Random.Range(0, textureSize);
                
                // Star brightness
                float brightness = Random.Range(0.5f, 1.0f) * starBrightness;
                
                // Star color
                Color starColor = Color.white;
                if (enableStarColorVariation)
                {
                    // Color based on brightness (hotter stars are bluer, cooler are redder)
                    float temp = Random.Range(0f, 1f);
                    if (temp > 0.8f) // Hot blue stars
                        starColor = new Color(0.8f, 0.85f, 1.0f);
                    else if (temp > 0.6f) // White stars
                        starColor = new Color(1f, 1f, 1f);
                    else if (temp > 0.4f) // Yellow stars
                        starColor = new Color(1f, 0.95f, 0.8f);
                    else if (temp > 0.2f) // Orange stars
                        starColor = new Color(1f, 0.7f, 0.3f);
                    else // Red stars
                        starColor = new Color(1f, 0.5f, 0.3f);
                }
                
                // Apply star color with brightness
                starColor *= brightness;
                
                // Calculate pixel index
                int index = y * textureSize + x;
                
                // Add the star (screen blend)
                pixels[index] = new Color(
                    1f - (1f - pixels[index].r) * (1f - starColor.r),
                    1f - (1f - pixels[index].g) * (1f - starColor.g),
                    1f - (1f - pixels[index].b) * (1f - starColor.b),
                    pixels[index].a
                );
                
                // For brighter stars, add a small bloom effect
                if (brightness > 0.7f)
                {
                    AddStarBloom(pixels, x, y, starColor, Mathf.CeilToInt(brightness * 2));
                }
            }
            
            // Add star clusters if enabled
            if (enableStarClusters)
            {
                AddStarClusters(pixels, offset);
            }
        }

        private void AddStarBloom(Color[] pixels, int x, int y, Color starColor, int size)
        {
            // Apply a simple bloom effect around the star
            for (int dy = -size; dy <= size; dy++)
            {
                for (int dx = -size; dx <= size; dx++)
                {
                    // Skip the center (already set)
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    // Check bounds
                    if (nx < 0 || nx >= textureSize || ny < 0 || ny >= textureSize) continue;
                    
                    // Calculate distance from star center
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > size) continue; // Outside bloom radius
                    
                    // Calculate falloff
                    float intensity = 1f - (distance / size);
                    intensity = intensity * intensity * 0.5f; // Quadratic falloff
                    
                    // Apply bloom with screen blend
                    Color bloomColor = starColor * intensity;
                    int index = ny * textureSize + nx;
                    
                    pixels[index] = new Color(
                        1f - (1f - pixels[index].r) * (1f - bloomColor.r),
                        1f - (1f - pixels[index].g) * (1f - bloomColor.g),
                        1f - (1f - pixels[index].b) * (1f - bloomColor.b),
                        pixels[index].a
                    );
                }
            }
        }

        private void AddStarClusters(Color[] pixels, Vector2 offset)
        {
            // Add 1-3 star clusters per texture
            int clusterCount = Random.Range(1, 4);
            
            for (int c = 0; c < clusterCount; c++)
            {
                // Random cluster position
                int centerX = Random.Range(0, textureSize);
                int centerY = Random.Range(0, textureSize);
                
                // Cluster size and density
                int clusterSize = Random.Range(50, 200);
                int clusterStars = Random.Range(20, 100);
                
                // Cluster color tendency
                float clusterTemp = Random.value; // 0-1 temperature range
                Color clusterBaseColor = Color.white;
                
                if (clusterTemp > 0.7f) // Blue cluster
                    clusterBaseColor = new Color(0.8f, 0.9f, 1.0f);
                else if (clusterTemp > 0.4f) // White-yellow cluster
                    clusterBaseColor = new Color(1.0f, 0.95f, 0.8f);
                else // Red-orange cluster
                    clusterBaseColor = new Color(1.0f, 0.7f, 0.5f);
                
                // Generate cluster stars
                for (int s = 0; s < clusterStars; s++)
                {
                    // Use gaussian distribution for cluster
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float distance = Random.Range(0f, 1f) * Random.Range(0f, 1f) * clusterSize;
                    
                    int starX = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * distance);
                    int starY = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * distance);
                    
                    // Check bounds
                    if (starX < 0 || starX >= textureSize || starY < 0 || starY >= textureSize)
                        continue;
                    
                    // Star brightness (brighter near center)
                    float brightness = Mathf.Lerp(0.4f, 1.0f, 1f - (distance / clusterSize)) * starBrightness;
                    
                    // Slight color variation around cluster base color
                    Color starColor = clusterBaseColor * Random.Range(0.9f, 1.1f);
                    starColor *= brightness;
                    
                    // Calculate pixel index
                    int index = starY * textureSize + starX;
                    
                    // Apply star with screen blend
                    pixels[index] = new Color(
                        1f - (1f - pixels[index].r) * (1f - starColor.r),
                        1f - (1f - pixels[index].g) * (1f - starColor.g),
                        1f - (1f - pixels[index].b) * (1f - starColor.b),
                        pixels[index].a
                    );
                    
                    // Add bloom for brighter stars
                    if (brightness > 0.6f)
                    {
                        AddStarBloom(pixels, starX, starY, starColor, 1);
                    }
                }
            }
        }

        private void ApplyDustToTexture(Color[] pixels, Vector2 offset)
        {
            // Apply dust lanes based on a different noise pattern
            float dustScale = nebulaScale * 1.5f; // Larger scale for dust
            
            // Apply a noise-based pattern
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    // Normalized coordinates (0-1)
                    float nx = (float)x / textureSize;
                    float ny = (float)y / textureSize;
                    
                    // Apply offset for seamless tiling
                    float ox = nx + offset.x + 100f; // Different offset from nebula
                    float oy = ny + offset.y + 100f;
                    
                    // Generate noise for dust
                    float noise = Mathf.PerlinNoise(ox / dustScale, oy / dustScale);
                    
                    // Apply threshold and intensity
                    if (noise > 0.5f) // Only apply dust where noise is above threshold
                    {
                        float dustAmount = (noise - 0.5f) * 2f * dustLaneIntensity;
                        
                        // Calculate pixel index
                        int index = y * textureSize + x;
                        
                        // Darken the pixel by dust amount
                        pixels[index] = new Color(
                            pixels[index].r * (1f - dustAmount * 0.7f),
                            pixels[index].g * (1f - dustAmount * 0.7f),
                            pixels[index].b * (1f - dustAmount * 0.7f),
                            pixels[index].a
                        );
                    }
                }
            }
        }

        // Cleanup on destroy
        private void OnDestroy()
        {
            // Clean up pooled objects
            foreach (var chunk in chunkPool)
            {
                if (chunk != null)
                {
                    Destroy(chunk);
                }
            }
            
            // Clean up active chunks
            if (activeChunks != null)
            {
                foreach (var chunk in activeChunks)
                {
                    if (chunk != null)
                    {
                        Destroy(chunk);
                    }
                }
            }
            
            // Clean up shared material
            if (sharedMaterial != null)
            {
                Destroy(sharedMaterial);
            }
        }

        // Helper method to manually regenerate textures (can be called via Inspector button)
        [ContextMenu("Regenerate Textures")]
        public void RegenerateTextures()
        {
            if (seed == 0) currentSeed = Random.Range(1, 10000);
            
            // Regenerate material if it exists
            if (sharedMaterial != null)
            {
                Texture2D newTexture = GenerateSpaceTexture(currentSeed, Vector2.zero);
                sharedMaterial.mainTexture = newTexture;
            }
            
            // Regenerate all active chunks
            if (activeChunks != null)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    for (int y = 0; y < gridSize; y++)
                    {
                        if (activeChunks[x, y] != null)
                        {
                            Vector2Int coord = GetGridCoord(activeChunks[x, y].transform.position);
                            Renderer renderer = activeChunks[x, y].GetComponent<Renderer>();
                            
                            if (renderer != null)
                            {
                                // Create new unique texture for this tile
                                Texture2D uniqueTexture = GenerateSpaceTexture(currentSeed, new Vector2(coord.x, coord.y));
                                renderer.material.mainTexture = uniqueTexture;
                            }
                        }
                    }
                }
            }
            
            Debug.Log("Regenerated all textures with new seed: " + currentSeed);
        }

        // Helper method to randomize the look (can be called via Inspector button)
        [ContextMenu("Randomize Look")]
        public void RandomizeLook()
        {
            // Choose a random scene type
            sceneType = (SceneType)Random.Range(0, System.Enum.GetValues(typeof(SceneType)).Length);
            
            // Randomize parameters based on the scene type
            switch (sceneType)
            {
                case SceneType.DenseNebula:
                    nebulaIntensity = Random.Range(0.6f, 1.0f);
                    nebulaScale = Random.Range(15f, 25f);
                    enableTurbulence = true;
                    turbulenceStrength = Random.Range(8f, 15f);
                    starDensity = Random.Range(0.003f, 0.006f);
                    starBrightness = Random.Range(0.8f, 1.2f);
                    enableDustLanes = Random.Range(0, 2) == 1;
                    break;
                
                case SceneType.StarField:
                    nebulaIntensity = Random.Range(0.2f, 0.4f);
                    nebulaScale = Random.Range(20f, 30f);
                    enableTurbulence = false;
                    starDensity = Random.Range(0.008f, 0.015f);
                    starBrightness = Random.Range(1.0f, 1.5f);
                    enableDustLanes = Random.Range(0, 3) == 0; // Less likely
                    break;
                
                case SceneType.DeepVoid:
                    nebulaIntensity = Random.Range(0.1f, 0.3f);
                    nebulaScale = Random.Range(30f, 40f);
                    enableTurbulence = false;
                    starDensity = Random.Range(0.001f, 0.003f);
                    starBrightness = Random.Range(0.7f, 1.0f);
                    enableDustLanes = true;
                    dustLaneIntensity = Random.Range(0.6f, 0.9f);
                    break;
                
                case SceneType.MolecularCloud:
                    nebulaIntensity = Random.Range(0.5f, 0.8f);
                    nebulaScale = Random.Range(10f, 20f);
                    enableTurbulence = true;
                    turbulenceStrength = Random.Range(10f, 20f);
                    starDensity = Random.Range(0.002f, 0.005f);
                    starBrightness = Random.Range(0.7f, 1.0f);
                    enableDustLanes = true;
                    dustLaneIntensity = Random.Range(0.7f, 1.0f);
                    break;
                
                case SceneType.EnergyRift:
                    nebulaIntensity = Random.Range(0.7f, 1.0f);
                    nebulaScale = Random.Range(8f, 15f);
                    enableTurbulence = true;
                    turbulenceStrength = Random.Range(15f, 25f);
                    starDensity = Random.Range(0.003f, 0.007f);
                    starBrightness = Random.Range(0.9f, 1.3f);
                    enableDustLanes = Random.Range(0, 2) == 1;
                    break;
            }
            
            // New random seed
            currentSeed = Random.Range(1, 10000);
            
            // Regenerate textures
            RegenerateTextures();
            
            Debug.Log($"Randomized to {sceneType} with seed {currentSeed}");
        }
    }
} 