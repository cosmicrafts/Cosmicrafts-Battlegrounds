using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProceduralSpaceBackground : MonoBehaviour
{
    // Basic parameters
    public int textureWidth = 1024;
    public int textureHeight = 1024;
    
    // Scene type selector
    public enum SceneType { DenseNebula, StarField, DeepVoid, MolecularCloud, EnergyRift }
    public SceneType sceneType = SceneType.DenseNebula;
    public bool randomizeOnStart = true;
    
    // Noise parameters for nebula clouds
    public float nebulaScale = 20.0f;
    public int nebulaOctaves = 4;
    public float nebulaPersistence = 0.5f;
    public float nebulaLacunarity = 2.0f;
    public float nebulaIntensity = 1.0f;
    
    // Domain warping for turbulence
    public bool enableTurbulence = true;
    public float turbulenceStrength = 10.0f;
    public float turbulenceScale = 5.0f;
    
    // Dust lanes and dark nebulae
    public bool enableDustLanes = true;
    public float dustLaneScale = 40.0f;
    public float dustLaneThreshold = 0.55f;
    public float dustLaneIntensity = 0.7f;
    
    // Using predefined textures for noise instead of procedural generation
    [HideInInspector] public bool useTextureForNoise = true;
    
    // Colors - Using Unity's Gradient for better control
    public Gradient nebulaGradient; // This will show as a visual gradient editor in Inspector
    
    // Star parameters
    public float starDensity = 0.005f;
    public float starThreshold = 0.98f;
    public float starBrightness = 1.0f;
    public bool enableStarColorVariation = true;
    public bool enableStarClusters = true;
    public float starClusterScale = 30.0f;
    
    // Star color and intensity properties
    [HideInInspector] public Color starColorDim = new Color(1.0f, 0.6f, 0.3f, 1.0f); // Orange-ish for dim stars
    [HideInInspector] public Color starColorBright = new Color(0.8f, 0.9f, 1.0f, 1.0f); // Blue-white for bright stars
    [HideInInspector] public float minStarIntensity = 0.4f;
    [HideInInspector] public float maxStarIntensity = 1.2f;
    
    // Using star sprites from the toolkit
    [HideInInspector] public bool useStarSprites = true;
    [HideInInspector] public float starSpriteScale = 0.01f; // Size of star sprites relative to texture
    [HideInInspector] public float minStarSize = 0.5f;
    [HideInInspector] public float maxStarSize = 2.0f;
    
    // Galaxy overlay parameters
    [HideInInspector] public bool useGalaxyOverlays = true;
    [HideInInspector] public float galaxyOverlayChance = 0.1f; // Chance of a galaxy appearing
    [HideInInspector] public float galaxyOverlayScale = 0.2f; // Size relative to texture
    
    // Texture settings
    public FilterMode textureFilterMode = FilterMode.Bilinear;
    public TextureWrapMode textureWrapMode = TextureWrapMode.Clamp;
    
    // Seed for reproducibility
    public int seed = 0;
    public Vector2 offset = Vector2.zero;
    
    // Texture collections
    private List<Texture2D> noiseTextures = new List<Texture2D>();
    private List<Texture2D> starSpriteTextures = new List<Texture2D>();
    private List<Texture2D> galaxyTextures = new List<Texture2D>();
    
    // Working textures for current generation
    private Texture2D nebulaNoiseTexture;
    private Texture2D dustLaneNoiseTexture;
    private Texture2D turbulenceNoiseTexture;
    
    private Texture2D generatedTexture;
    private Material backgroundMaterial;
    private Color[] pixelColors; // For SetPixels optimization
    
    // Points for Worley noise
    private Vector2[] worleyPoints;
    private int worleyPointCount = 32;
    
    // Textures folder paths (Direct paths to the VFX toolkit)
    // private readonly string TOOLKIT_BASE_PATH = "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures";
    private readonly string[] NOISE_TEXTURE_PATHS = {
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/PerlinNoise.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/PerlinNoise2.png", 
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/PerlinNoise3.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/SmokeNoise.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/Noise80.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/Noise84.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/Noise90.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/Noise95.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Noise/Noise97.png"
    };
    
    private readonly string[] STAR_TEXTURE_PATHS = {
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star1.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star2.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star3.png", 
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star4.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star5.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star6.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Star7.png"
    };
    
    private readonly string[] GALAXY_TEXTURE_PATHS = {
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Spiral4.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Spiral5.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Spiral6.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Spiral7.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Spiral8.png",
        "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures/Shapes/Spiral9.png"
    };
    
    [Header("Textures")]
    [Tooltip("Auto-load textures from @Textures folder")]
    public bool autoLoadTextures = true;
    [Tooltip("If not auto-loading, assign nebula textures here")]
    public List<Texture2D> nebulaTextures = new List<Texture2D>();
    [Tooltip("If not auto-loading, assign dust textures here")]
    public List<Texture2D> dustTextures = new List<Texture2D>();
    [Tooltip("If not auto-loading, assign star textures here")]
    public List<Texture2D> starTextures = new List<Texture2D>();
    
    private Texture2D backgroundTexture;
    private Color[] backgroundPixels;
    
    [Header("Texture Layering")]
    [Tooltip("Number of nebula layers to add")]
    public int nebulaeLayers = 2;
    [Tooltip("Number of dust layers to add")]
    public int dustLayers = 1;
    [Tooltip("Number of star layers to add")]
    public int starLayers = 1;
    
    [Tooltip("Opacity of nebula texture layers")]
    [Range(0f, 1f)]
    public float nebulaOpacity = 0.7f;
    [Tooltip("Opacity of dust texture layers")]
    [Range(0f, 1f)]
    public float dustOpacity = 0.5f;
    [Tooltip("Opacity of star texture layers")]
    [Range(0f, 1f)]
    public float starOpacity = 0.8f;
    
    [Tooltip("Whether to auto-regenerate on start")]
    public bool autoRegenerate = true;
    
    [Tooltip("Background color for space")]
    public Color backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);
    
    [Header("Generation Control")]
    [Tooltip("Should this generate automatically on Start? Set to false if controlled by a manager (e.g., InfiniteSpaceGenerator).")]
    public bool generateOnStart = false;
    
    private void OnEnable()
    {
        // Initialize lists if needed
        if (nebulaTextures == null) nebulaTextures = new List<Texture2D>();
        if (dustTextures == null) dustTextures = new List<Texture2D>();
        if (starSpriteTextures == null) starSpriteTextures = new List<Texture2D>();
        
        // Check if we need to load textures
        if (nebulaTextures.Count == 0 || dustTextures.Count == 0 || starSpriteTextures.Count == 0)
        {
            LoadTextures();
        }
        
        // Make sure we have a renderer
        if (GetComponent<Renderer>() == null)
        {
            Debug.LogError("Space background requires a renderer component!");
        }
    }

    private void Start()
    {
        // Only generate automatically if the flag is set
        if (generateOnStart)
        {
            Debug.Log($"[{this.name}] Auto-generating background on Start.");
            // If texture lists are still empty after attempting to load, generate one procedurally
            if ((nebulaTextures == null || nebulaTextures.Count == 0) &&
                (dustTextures == null || dustTextures.Count == 0) &&
                (starSpriteTextures == null || starSpriteTextures.Count == 0)) // Updated check
            {
                Debug.LogWarning($"[{this.name}] No space background textures were found. Loading defaults or generating procedurally.");
                LoadTextures(); // Try loading again to get defaults
            }

            // Make sure we have a gradient initialized
            InitializeGradient();

            // Check if we need to generate the background texture
            bool regenerate = false;

            // Force regeneration if background texture is null or not set
            if (backgroundTexture == null)
            {
                regenerate = true;
                // Don't create the texture here, GenerateTexture will do it.
            }

            // Also regenerate if autoRegenerate is set
            if (autoRegenerate) 
            {
                regenerate = true;
            }

            // Generate the texture if needed
            if (regenerate)
            {                
                GenerateTexture(); // Use default seed/offset
                ApplyTextureToMaterial();
            }

            // Log texture status
            if (nebulaTextures.Count > 0 || dustTextures.Count > 0 || starSpriteTextures.Count > 0)
            {
                Debug.Log($"[{this.name}] Space background ready with {nebulaTextures.Count} nebulae, {dustTextures.Count} dust clouds, and {starSpriteTextures.Count} stars");
            }
            else
            {
                Debug.LogWarning($"[{this.name}] Space background has no textures! Procedural generation may not look correct.");
            }
        }
        else
        {
             Debug.Log($"[{this.name}] generateOnStart is false. Generation controlled externally.");
        }
    }
    
    private void LoadTextures()
    {
        Debug.Log("Loading space background textures...");
        
        // Clear existing textures
        nebulaTextures.Clear();
        dustTextures.Clear();
        starSpriteTextures.Clear();
        
        // Try direct loading from the @Textures folder
#if UNITY_EDITOR
        LoadTexturesFromDirectPaths();
#else
        // In builds, try Resources folder as fallback
        LoadTexturesFromResources();
#endif
        
        // Log the results
        Debug.Log($"Loaded {nebulaTextures.Count} nebula textures, {dustTextures.Count} dust textures, and {starSpriteTextures.Count} star textures");
    }
    
    private void LoadTexturesFromResources()
    {
        // Try to load from Resources folder
        Texture2D[] resourceTextures = Resources.LoadAll<Texture2D>("@Textures");
        
        foreach (Texture2D texture in resourceTextures)
        {
            if (texture.name.ToLower().Contains("nebula"))
            {
                nebulaTextures.Add(texture);
            }
            else if (texture.name.ToLower().Contains("dust"))
            {
                dustTextures.Add(texture);
            }
            else if (texture.name.ToLower().Contains("star"))
            {
                starSpriteTextures.Add(texture);
            }
        }
        
        // Also try specific folders
        Texture2D[] nebulaResources = Resources.LoadAll<Texture2D>("@Textures/Nebula");
        Texture2D[] dustResources = Resources.LoadAll<Texture2D>("@Textures/Dust");
        Texture2D[] starResources = Resources.LoadAll<Texture2D>("@Textures/Stars");
        
        nebulaTextures.AddRange(nebulaResources);
        dustTextures.AddRange(dustResources);
        starSpriteTextures.AddRange(starResources);
    }
    
    // Helper method to create a readable copy of a texture
    private Texture2D CreateReadableCopy(Texture2D source)
    {
        if (source == null) return null;

        // Create a temporary RenderTexture
        RenderTexture rt = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );

        // Copy the texture to the render texture
        Graphics.Blit(source, rt);
        
        // Remember the active render texture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        
        // Create a new readable texture
        Texture2D readableCopy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readableCopy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readableCopy.Apply();
        
        // Clean up
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        
        // Copy the name for identification
        readableCopy.name = source.name + " (Readable)";
        
        return readableCopy;
    }

    private void LoadTexturesFromDirectPaths()
    {
#if UNITY_EDITOR
        // This approach only works in the Unity Editor
        try
        {
            // Use the actual paths where the textures are located
            List<string> textureBasePaths = new List<string>
            {
                "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures",
                "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Textures Demo/Textures",
                "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Demo/Texutres"
            };
            
            Debug.Log($"Searching for textures in VFX Toolkit folders...");
            
            // Check each base path
            foreach (string basePath in textureBasePaths)
            {
                if (Directory.Exists(basePath))
                {
                    Debug.Log($"Found texture directory: {basePath}");
                    
                    // Check for specific subfolders first
                    string noisePath = Path.Combine(basePath, "Noise");
                    string shapesPath = Path.Combine(basePath, "Shapes");
                    
                    // Load noise textures (for nebulae)
                    if (Directory.Exists(noisePath))
                    {
                        string[] noisePaths = Directory.GetFiles(noisePath, "*.png", SearchOption.AllDirectories);
                        Debug.Log($"Found {noisePaths.Length} textures in {noisePath}");
                        foreach (string path in noisePaths)
                        {
                            string normalizedPath = path.Replace('\\', '/');
                            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
                            if (texture != null) 
                            {
                                // Create a readable copy of the texture if needed
                                Texture2D readableTexture = CreateReadableCopy(texture);
                                
                                if (readableTexture != null)
                                {
                                    noiseTextures.Add(readableTexture);
                                    // Also use appropriate noise textures as nebula textures
                                    if (texture.name.ToLower().Contains("perlin") || 
                                        texture.name.ToLower().Contains("noise") ||
                                        texture.name.ToLower().Contains("cloud"))
                                    {
                                        nebulaTextures.Add(readableTexture);
                                    }
                                }
                            }
                        }
                    }
                    
                    // Load shape textures (for stars and galaxies)
                    if (Directory.Exists(shapesPath))
                    {
                        string[] shapePaths = Directory.GetFiles(shapesPath, "*.png", SearchOption.AllDirectories);
                        Debug.Log($"Found {shapePaths.Length} textures in {shapesPath}");
                        foreach (string path in shapePaths)
                        {
                            string normalizedPath = path.Replace('\\', '/');
                            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
                            if (texture != null)
                            {
                                // Create a readable copy of the texture
                                Texture2D readableTexture = CreateReadableCopy(texture);
                                if (readableTexture != null)
                                {
                                    string lowerName = texture.name.ToLower();
                                    if (lowerName.Contains("star"))
                                    {
                                        starSpriteTextures.Add(readableTexture);
                                    }
                                    else if (lowerName.Contains("spiral") || lowerName.Contains("galaxy"))
                                    {
                                        galaxyTextures.Add(readableTexture);
                                    }
                                }
                            }
                        }
                    }
                    
                    // Also look at root level of each texture folder
                    string[] rootPaths = Directory.GetFiles(basePath, "*.png", SearchOption.TopDirectoryOnly);
                    Debug.Log($"Found {rootPaths.Length} textures in {basePath} root");
                    foreach (string path in rootPaths)
                    {
                        string normalizedPath = path.Replace('\\', '/');
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
                        if (texture != null)
                        {
                            // Create a readable copy of the texture
                            Texture2D readableTexture = CreateReadableCopy(texture);
                            if (readableTexture != null)
                            {
                                string lowerName = texture.name.ToLower();
                                
                                // Try to categorize based on name
                                if (lowerName.Contains("nebula") || lowerName.Contains("cloud"))
                                {
                                    nebulaTextures.Add(readableTexture);
                                }
                                else if (lowerName.Contains("dust") || lowerName.Contains("dirt"))
                                {
                                    dustTextures.Add(readableTexture);
                                }
                                else if (lowerName.Contains("star"))
                                {
                                    starSpriteTextures.Add(readableTexture);
                                }
                                else if (lowerName.Contains("gradient") || lowerName.Contains("noise"))
                                {
                                    // Generic textures can be used for both nebula and dust
                                    nebulaTextures.Add(readableTexture);
                                    dustTextures.Add(readableTexture);
                                }
                            }
                        }
                    }
                    
                    // Check for Gradient folders which can be used for nebulae
                    string gradientPath = Path.Combine(basePath, "Greyscale Gradients");
                    if (Directory.Exists(gradientPath))
                    {
                        string[] gradientPaths = Directory.GetFiles(gradientPath, "*.png", SearchOption.AllDirectories);
                        Debug.Log($"Found {gradientPaths.Length} textures in {gradientPath}");
                        foreach (string path in gradientPaths)
                        {
                            string normalizedPath = path.Replace('\\', '/');
                            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
                            if (texture != null)
                            {
                                // Create a readable copy of the texture
                                Texture2D readableTexture = CreateReadableCopy(texture);
                                if (readableTexture != null)
                                {
                                    // Use gradients for nebula and dust textures
                                    nebulaTextures.Add(readableTexture);
                                    dustTextures.Add(readableTexture);
                                }
                            }
                        }
                    }
                }
            }
            
            // If we still don't have any textures, try to load from the NOISE_TEXTURE_PATHS, STAR_TEXTURE_PATHS, etc.
            if (noiseTextures.Count == 0 && nebulaTextures.Count == 0 && starSpriteTextures.Count == 0)
            {
                Debug.Log("No textures found in VFX Toolkit folders, trying direct paths...");
                
                // Load noise textures from paths
                foreach (string path in NOISE_TEXTURE_PATHS)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                    {
                        // Create a readable copy of the texture
                        Texture2D readableTexture = CreateReadableCopy(texture);
                        if (readableTexture != null)
                        {
                            noiseTextures.Add(readableTexture);
                            nebulaTextures.Add(readableTexture); // Use noise for nebulae too
                        }
                    }
                }
                
                // Load star textures from paths
                foreach (string path in STAR_TEXTURE_PATHS)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                    {
                        // Create a readable copy of the texture
                        Texture2D readableTexture = CreateReadableCopy(texture);
                        if (readableTexture != null)
                        {
                            starSpriteTextures.Add(readableTexture);
                        }
                    }
                }
                
                // Load galaxy textures from paths
                foreach (string path in GALAXY_TEXTURE_PATHS)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                    {
                        // Create a readable copy of the texture
                        Texture2D readableTexture = CreateReadableCopy(texture);
                        if (readableTexture != null)
                        {
                            galaxyTextures.Add(readableTexture);
                        }
                    }
                }
            }
            
            // Fallback to procedural if still no textures
            if (nebulaTextures.Count == 0 && dustTextures.Count == 0 && starSpriteTextures.Count == 0)
            {
                Debug.LogWarning("No textures found. Falling back to procedural generation.");
                useTextureForNoise = false;
                CreateProceduralTextureFallbacks();
            }
            else
            {
                Debug.Log($"Successfully loaded textures: {nebulaTextures.Count} nebulae, {dustTextures.Count} dust, {starSpriteTextures.Count} stars, {noiseTextures.Count} noise, {galaxyTextures.Count} galaxies");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Error loading textures directly: " + e.Message);
            useTextureForNoise = false; // Fall back to procedural
        }
#endif
    }
    
    // Select appropriate textures based on scene type
    void SelectTexturesForCurrentSceneType()
    {
        if (noiseTextures.Count == 0) {
            Debug.LogWarning("No noise textures available for selection. Using procedural generation.");
            useTextureForNoise = false;
            return;
        }
        
        // Use Random with the current seed for consistent selection
        Random.InitState(seed);
        
        // Categorize noise textures by examining their names
        List<Texture2D> smoothNoiseTextures = new List<Texture2D>();
        List<Texture2D> detailedNoiseTextures = new List<Texture2D>();
        List<Texture2D> sharpNoiseTextures = new List<Texture2D>();
        
        foreach (Texture2D tex in noiseTextures) {
            if (tex == null) continue;
            
            string name = tex.name.ToLower();
            
            // Categorize by texture name and visual characteristics
            if (name.Contains("perlin") || name.Contains("smooth") || name.Contains("cloud")) {
                smoothNoiseTextures.Add(tex);
            }
            else if (name.Contains("detail") || name.Contains("cell") || name.Contains("voronoi")) {
                detailedNoiseTextures.Add(tex);
            }
            else if (name.Contains("sharp") || name.Contains("ridge") || name.Contains("edge")) {
                sharpNoiseTextures.Add(tex);
            }
            else {
                // If we can't categorize, add it to all lists to increase options
                smoothNoiseTextures.Add(tex);
                detailedNoiseTextures.Add(tex);
                sharpNoiseTextures.Add(tex);
            }
        }
        
        // Select nebula base texture based on scene type
        switch (sceneType) {
            case SceneType.DenseNebula:
                // Prefer smoother, cloudier textures
                nebulaNoiseTexture = GetRandomTexture(smoothNoiseTextures.Count > 0 ? smoothNoiseTextures : noiseTextures);
                break;
                
            case SceneType.StarField:
                // Very subtle background noise
                nebulaNoiseTexture = GetRandomTexture(smoothNoiseTextures.Count > 0 ? smoothNoiseTextures : noiseTextures);
                break;
                
            case SceneType.DeepVoid:
                // Low contrast, subtle texture
                nebulaNoiseTexture = GetRandomTexture(smoothNoiseTextures.Count > 0 ? smoothNoiseTextures : noiseTextures);
                break;
                
            case SceneType.MolecularCloud:
                // Detailed, complex patterns
                nebulaNoiseTexture = GetRandomTexture(detailedNoiseTextures.Count > 0 ? detailedNoiseTextures : noiseTextures);
                break;
                
            case SceneType.EnergyRift:
                // Sharp, high contrast
                nebulaNoiseTexture = GetRandomTexture(sharpNoiseTextures.Count > 0 ? sharpNoiseTextures : noiseTextures);
                break;
                
            default:
                // Fallback to a random noise texture
                nebulaNoiseTexture = GetRandomTexture(noiseTextures);
                break;
        }
        
        // Select dust lane texture - ideally different from the nebula texture
        List<Texture2D> potentialDustTextures = noiseTextures.Where(t => t != nebulaNoiseTexture).ToList();
        dustLaneNoiseTexture = GetRandomTexture(potentialDustTextures.Count > 0 ? potentialDustTextures : noiseTextures);
        
        // Select turbulence texture - ideally different from both nebula and dust lane textures
        List<Texture2D> potentialTurbulenceTextures = noiseTextures
            .Where(t => t != nebulaNoiseTexture && t != dustLaneNoiseTexture).ToList();
        turbulenceNoiseTexture = GetRandomTexture(potentialTurbulenceTextures.Count > 0 ? potentialTurbulenceTextures : noiseTextures);
        
        // Enable texture features based on what we have
        useStarSprites = starSpriteTextures.Count > 0;
        useGalaxyOverlays = galaxyTextures.Count > 0;
        
        Debug.Log($"Selected textures - Nebula: {nebulaNoiseTexture.name}, Dust: {dustLaneNoiseTexture.name}, Turbulence: {turbulenceNoiseTexture.name}");
    }
    
    // Helper to get a random texture from a list
    Texture2D GetRandomTexture(List<Texture2D> textures)
    {
        if (textures == null || textures.Count == 0) return null;
        return textures[Random.Range(0, textures.Count)];
    }
    
    void InitializeGradient()
    {
        // If nebulaGradient not set up, create defaults
        if (nebulaGradient == null)
        {
            nebulaGradient = new Gradient();
        }
        
        if (nebulaGradient.colorKeys == null || nebulaGradient.colorKeys.Length == 0)
        {
            // Create a default gradient (deep blue to purple)
            GradientColorKey[] colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey(Color.black, 0.0f);
            colorKeys[1] = new GradientColorKey(new Color(0.1f, 0.1f, 0.5f), 0.3f);
            colorKeys[2] = new GradientColorKey(new Color(0.5f, 0.0f, 0.5f), 0.6f);
            colorKeys[3] = new GradientColorKey(new Color(0.7f, 0.3f, 0.8f), 1.0f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
            
            nebulaGradient.SetKeys(colorKeys, alphaKeys);
        }
    }
    
    // Apply parameter presets based on scene type
    void ApplySceneTypePreset()
    {
        // Initialize random for consistent results from seed
        Random.InitState(seed);
        
        switch (sceneType) {
            case SceneType.DenseNebula:
                nebulaIntensity = Random.Range(0.8f, 1.2f);
                nebulaOctaves = Random.Range(4, 7);
                nebulaScale = Random.Range(15f, 25f);
                starDensity = Random.Range(0.002f, 0.005f);
                enableTurbulence = true;
                turbulenceStrength = Random.Range(8f, 15f);
                enableDustLanes = Random.Range(0, 2) == 1;
                break;
                
            case SceneType.StarField:
                nebulaIntensity = Random.Range(0.2f, 0.4f);
                starDensity = Random.Range(0.008f, 0.015f);
                starBrightness = Random.Range(1.0f, 1.5f);
                enableTurbulence = false;
                enableStarClusters = true;
                starClusterScale = Random.Range(20f, 40f);
                enableDustLanes = Random.Range(0, 10) < 3; // 30% chance
                break;
                
            case SceneType.DeepVoid:
                nebulaIntensity = Random.Range(0.1f, 0.3f);
                nebulaScale = Random.Range(30f, 50f);
                starDensity = Random.Range(0.001f, 0.003f);
                starBrightness = Random.Range(0.7f, 1.0f);
                enableTurbulence = false;
                enableDustLanes = true;
                dustLaneIntensity = Random.Range(0.5f, 0.9f);
                break;
                
            case SceneType.MolecularCloud:
                nebulaIntensity = Random.Range(0.6f, 1.0f);
                nebulaScale = Random.Range(15f, 30f);
                nebulaPersistence = Random.Range(0.4f, 0.6f);
                starDensity = Random.Range(0.001f, 0.004f);
                enableTurbulence = true;
                turbulenceStrength = Random.Range(15f, 25f);
                enableDustLanes = true;
                dustLaneIntensity = Random.Range(0.7f, 0.9f);
                break;
                
            case SceneType.EnergyRift:
                nebulaIntensity = Random.Range(0.9f, 1.3f);
                nebulaOctaves = Random.Range(3, 6);
                nebulaScale = Random.Range(10f, 20f);
                enableTurbulence = true;
                turbulenceStrength = Random.Range(20f, 30f);
                turbulenceScale = Random.Range(3f, 8f);
                starDensity = Random.Range(0.003f, 0.007f);
                break;
        }
        
        // Select textures for the current scene type
        SelectTexturesForCurrentSceneType();
    }
    
    // Sample a texture with wraparound (tiling)
    float SampleTexture(Texture2D tex, float x, float y)
    {
        if (tex == null) return 0.5f;
        
        // Apply tiling
        x = x % 1.0f;
        y = y % 1.0f;
        
        // Convert to 0-1 range if negative
        if (x < 0) x += 1.0f;
        if (y < 0) y += 1.0f;
        
        // Sample the texture
        return tex.GetPixelBilinear(x, y).r;
    }
    
    // Generate Worley points for noise
    void GenerateWorleyPoints() {
        Random.InitState(seed);
        worleyPoints = new Vector2[worleyPointCount];
        
        for (int i = 0; i < worleyPointCount; i++) {
            worleyPoints[i] = new Vector2(Random.value, Random.value);
        }
    }
    
    // Calculate Worley noise (F1 - distance to closest point)
    float WorleyNoiseF1(float x, float y) {
        float minDist = 1.0f;
        
        // Scale coordinates to create larger/smaller cells
        x = x * worleyPointCount * dustLaneScale / 5.0f;
        y = y * worleyPointCount * dustLaneScale / 5.0f;
        
        // Cell position
        int cellX = Mathf.FloorToInt(x);
        int cellY = Mathf.FloorToInt(y);
        
        // Check 3x3 grid of cells around current position for points
        for (int i = -1; i <= 1; i++) {
            for (int j = -1; j <= 1; j++) {
                // Cell coordinates (wrapping around)
                int ci = (cellX + i + worleyPointCount) % worleyPointCount;
                int cj = (cellY + j + worleyPointCount) % worleyPointCount;
                
                // Get point in this cell
                Vector2 point = worleyPoints[(ci + cj * worleyPointCount) % worleyPointCount];
                
                // Cell corner position
                float cornerX = cellX + i + point.x;
                float cornerY = cellY + j + point.y;
                
                // Distance to point
                float dx = cornerX - x;
                float dy = cornerY - y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                minDist = Mathf.Min(minDist, dist);
            }
        }
        
        return minDist;
    }
    
    // Calculate Worley F2-F1 (difference between distances to closest and second closest points)
    float WorleyNoiseF2MinusF1(float x, float y) {
        float f1 = 1.0f; // Distance to closest point
        float f2 = 1.0f; // Distance to second closest point
        
        // Scale coordinates
        x = x * worleyPointCount * dustLaneScale / 5.0f;
        y = y * worleyPointCount * dustLaneScale / 5.0f;
        
        // Cell position
        int cellX = Mathf.FloorToInt(x);
        int cellY = Mathf.FloorToInt(y);
        
        // Check 3x3 grid
        for (int i = -1; i <= 1; i++) {
            for (int j = -1; j <= 1; j++) {
                int ci = (cellX + i + worleyPointCount) % worleyPointCount;
                int cj = (cellY + j + worleyPointCount) % worleyPointCount;
                
                Vector2 point = worleyPoints[(ci + cj * worleyPointCount) % worleyPointCount];
                
                float cornerX = cellX + i + point.x;
                float cornerY = cellY + j + point.y;
                
                float dx = cornerX - x;
                float dy = cornerY - y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist < f1) {
                    f2 = f1;
                    f1 = dist;
                } else if (dist < f2) {
                    f2 = dist;
                }
            }
        }
        
        // Return difference, normalized to 0-1 range
        return Mathf.Clamp01((f2 - f1) * 2.0f);
    }
    
    float SampleNoise(float x, float y, float scale, int octaves, float persistence, float lacunarity)
    {
        // If using texture for noise
        if (useTextureForNoise && nebulaNoiseTexture != null) {
            // Add some octaves of the texture by sampling at different scales
            float noiseValue = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float totalAmplitude = 0f;
            
            // Apply domain warping (turbulence) if enabled
            if (enableTurbulence && turbulenceNoiseTexture != null) {
                float warpX = SampleTexture(turbulenceNoiseTexture, x / turbulenceScale, y / turbulenceScale) * turbulenceStrength;
                float warpY = SampleTexture(turbulenceNoiseTexture, x / turbulenceScale + 0.5f, y / turbulenceScale + 0.5f) * turbulenceStrength;
                x += warpX;
                y += warpY;
            }
            
            for (int i = 0; i < octaves; i++) {
                float noise = SampleTexture(nebulaNoiseTexture, x * frequency / scale, y * frequency / scale);
                noiseValue += noise * amplitude;
                totalAmplitude += amplitude;
                
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            return noiseValue / totalAmplitude;
        }
        // Otherwise use procedural noise
        else {
            float noiseValue = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float totalAmplitude = 0f;
            
            // Apply domain warping (turbulence) if enabled
            if (enableTurbulence) {
                float warpX = Mathf.PerlinNoise(x / turbulenceScale, y / turbulenceScale) * turbulenceStrength;
                float warpY = Mathf.PerlinNoise(x / turbulenceScale + 100, y / turbulenceScale + 100) * turbulenceStrength;
                x += warpX;
                y += warpY;
            }
            
            // Octave Loop for layered noise (more detail)
            for (int i = 0; i < octaves; i++) {
                // Mathf.PerlinNoise returns values between 0.0 and 1.0
                float perlin = Mathf.PerlinNoise(x * frequency / scale, y * frequency / scale);
                noiseValue += perlin * amplitude;
                totalAmplitude += amplitude;
                
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            // Normalize
            return noiseValue / totalAmplitude;
        }
    }
    
    Color GetStarColor(float brightness)
    {
        if (!enableStarColorVariation)
            return Color.white;
            
        // Approximate blackbody radiation colors for stars
        if (brightness > 0.85f) // Hot blue-white stars (O, B class)
            return new Color(0.8f, 0.85f, 1.0f);
        else if (brightness > 0.7f) // White stars (A class)
            return new Color(1.0f, 1.0f, 1.0f);
        else if (brightness > 0.5f) // Yellow-white stars (F class)
            return new Color(1.0f, 0.98f, 0.9f);
        else if (brightness > 0.3f) // Yellow stars like our sun (G class)
            return new Color(1.0f, 0.93f, 0.7f);
        else if (brightness > 0.2f) // Orange stars (K class)
            return new Color(1.0f, 0.78f, 0.5f);
        else // Red dwarf stars (M class)
            return new Color(1.0f, 0.5f, 0.2f);
    }
    
    // Screen blend mode (1 - (1-a) * (1-b)) - good for adding light
    Color ScreenBlend(Color baseColor, Color blendColor)
    {
        return new Color(
            1 - (1 - baseColor.r) * (1 - blendColor.r),
            1 - (1 - baseColor.g) * (1 - blendColor.g),
            1 - (1 - baseColor.b) * (1 - blendColor.b),
            baseColor.a
        );
    }
    
    // Add a star with soft edges if enabled, or use star sprites if available
    void AddStar(int x, int y, float brightness)
    {
        if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight) return;
        
        // Determine star size based on brightness
        float size = Mathf.Lerp(minStarSize, maxStarSize, brightness);
        
        // Choose color based on brightness (brighter stars have more white/blue, dimmer are more red/yellow)
        Color starColor = Color.Lerp(starColorDim, starColorBright, brightness);
        
        // Set intensity based on brightness
        float intensity = Mathf.Lerp(minStarIntensity, maxStarIntensity, brightness);
        starColor *= intensity;
        
        // The option to use for this star (0 = texture, 1 = soft circle, 2 = point)
        int starOption = Random.Range(0, 3);
        
        // Option 0: Use a texture if available (for brighter stars)
        if (starOption == 0 && starSpriteTextures.Count > 0 && brightness > 0.7f)
        {
            // Choose a random star texture
            Texture2D starTexture = starSpriteTextures[Random.Range(0, starSpriteTextures.Count)];
            int texWidth = starTexture.width;
            int texHeight = starTexture.height;
            
            // Calculate the area to sample from the texture and place on the background
            int halfSize = Mathf.CeilToInt(size / 2f);
            
            // Draw the star texture onto the background
            for (int sy = -halfSize; sy <= halfSize; sy++)
            {
                for (int sx = -halfSize; sx <= halfSize; sx++)
                {
                    // Calculate pixel position on background
                    int pixelX = Mathf.RoundToInt(x + sx);
                    int pixelY = Mathf.RoundToInt(y + sy);
                    
                    // Skip if outside background texture
                    if (pixelX < 0 || pixelX >= textureWidth || pixelY < 0 || pixelY >= textureHeight) continue;
                    
                    // Sample from texture (normalized coordinates)
                    float texU = (sx + halfSize) / (size + 1);
                    float texV = (sy + halfSize) / (size + 1);
                    
                    // Convert to pixel coordinates
                    int texPixelX = Mathf.Clamp(Mathf.FloorToInt(texU * texWidth), 0, texWidth - 1);
                    int texPixelY = Mathf.Clamp(Mathf.FloorToInt(texV * texHeight), 0, texHeight - 1);
                    
                    // Get color from texture
                    Color texColor = starTexture.GetPixel(texPixelX, texPixelY);
                    
                    // Skip if nearly transparent
                    if (texColor.a < 0.05f) continue;
                    
                    // Apply star color and intensity
                    texColor = new Color(
                        texColor.r * starColor.r,
                        texColor.g * starColor.g,
                        texColor.b * starColor.b,
                        texColor.a
                    );
                    
                    // Calculate pixel index
                    int pixelIndex = pixelY * textureWidth + pixelX;
                    
                    // Blend with background using screen blend mode
                    pixelColors[pixelIndex] = ScreenBlend(pixelColors[pixelIndex], texColor);
                }
            }
        }
        // Option 1: Use a procedural soft circle (for medium stars)
        else if (starOption == 1 || (starOption == 0 && starSpriteTextures.Count == 0 && brightness > 0.5f))
        {
            // ... existing procedural star code ...
        }
        // Option 2: Simple point star (for dimmer stars)
        else
        {
            // ... existing point star code ...
        }
    }
    
    // Add a galaxy overlay from provided textures
    void AddGalaxyOverlay(int centerX, int centerY, int galaxyIndex, float brightness)
    {
        if (galaxyTextures == null || galaxyTextures.Count == 0 || galaxyIndex >= galaxyTextures.Count)
            return;
            
        Texture2D galaxyTexture = galaxyTextures[galaxyIndex];
        if (galaxyTexture == null)
            return;
            
        // Calculate size and position for galaxy overlay
        int galaxyWidth = Mathf.RoundToInt(galaxyTexture.width * galaxyOverlayScale);
        int galaxyHeight = Mathf.RoundToInt(galaxyTexture.height * galaxyOverlayScale);
        
        // Calculate top-left position to center the galaxy
        int startX = centerX - galaxyWidth / 2;
        int startY = centerY - galaxyHeight / 2;
        
        // Blend galaxy texture onto the background
        for (int x = 0; x < galaxyWidth; x++) {
            for (int y = 0; y < galaxyHeight; y++) {
                int bgX = startX + x;
                int bgY = startY + y;
                
                // Skip if outside texture bounds
                if (bgX < 0 || bgX >= textureWidth || bgY < 0 || bgY >= textureHeight)
                    continue;
                    
                // Sample galaxy texture with bilinear scaling
                float u = (float)x / galaxyWidth;
                float v = (float)y / galaxyHeight;
                Color galaxyColor = galaxyTexture.GetPixelBilinear(u, v);
                
                // Skip fully transparent or black pixels
                if (galaxyColor.r < 0.05f && galaxyColor.g < 0.05f && galaxyColor.b < 0.05f)
                    continue;
                    
                // Apply brightness
                galaxyColor *= brightness;
                
                // Get background pixel
                int pixelIndex = bgY * textureWidth + bgX;
                Color bgColor = pixelColors[pixelIndex];
                
                // Blend using screen blend mode for better highlights
                pixelColors[pixelIndex] = ScreenBlend(bgColor, galaxyColor);
            }
        }
    }
    
    // Modified GenerateTexture to accept seed/offset (or create a new method)
    // We'll overload GenerateTexture for now
    private void GenerateTexture()
    {
        // Calls the main generation with the default seed/offset from the inspector
        GenerateTexture(this.seed, this.offset);
    }

    private void GenerateTexture(int generationSeed, Vector2 generationOffset)
    {
        // Initialize Random state with the specific seed for this chunk
        Random.InitState(generationSeed);
        
        // Apply scene preset based on the *inspector* settings, but use the chunk's seed for randomness within the preset
        // Note: This applies the *same preset* to all chunks, only the internal randomization (texture selection, parameter ranges) changes.
        // If you want different scene types per chunk, the manager needs to pass that info too.
        ApplySceneTypePreset(); // This uses Random.InitState(seed) internally, which is now set to chunkSeed
        
        // Make sure texture is created if it doesn't exist
        if (backgroundTexture == null || backgroundTexture.width != textureWidth || backgroundTexture.height != textureHeight)
        {
            if (backgroundTexture != null) Destroy(backgroundTexture); // Clean up old texture
            backgroundTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            backgroundTexture.name = $"ProcSpaceBG_{generationSeed}"; // Unique name
            backgroundTexture.filterMode = textureFilterMode;
            backgroundTexture.wrapMode = textureWrapMode; // Clamp is likely best for non-tiling chunks
        }

        // Initialize pixel array
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = backgroundColor;
        }
        // Assign to pixelColors field if used by AddStar/AddGalaxyOverlay
        pixelColors = pixels;

        // Load textures if they haven't been loaded yet (might happen if GenerateForChunk is called before Start)
        if (nebulaTextures.Count == 0 && dustTextures.Count == 0 && starSpriteTextures.Count == 0)
        {
            LoadTextures();
        }

        // Use the provided generationOffset when sampling noise, etc. 
        // The existing SampleNoise/AddStar etc need to be aware of this offset.
        // For simplicity, let's update the member 'offset' variable before generating layers.
        // A cleaner way would be passing it through all relevant methods.
        Vector2 originalOffset = this.offset;
        this.offset = generationOffset; // Temporarily set offset for this generation

        // Add procedural elements based on available textures
        bool hasTextures = nebulaTextures.Count > 0 || dustTextures.Count > 0 || starSpriteTextures.Count > 0;

        if (hasTextures)
        {
            //Debug.Log("Using loaded textures to generate space background for chunk");

            // Add nebulae if available
            if (nebulaTextures.Count > 0 && nebulaeLayers > 0)
            {
                //Debug.Log($"Adding {nebulaeLayers} nebula layers using {nebulaTextures.Count} available textures");
                for (int i = 0; i < nebulaeLayers; i++)
                {
                    Texture2D nebulaTexture = nebulaTextures[Random.Range(0, nebulaTextures.Count)];
                    AddTextureLayer(nebulaTexture, nebulaOpacity, true); // AddTextureLayer needs to use the 'offset'
                }
            }

            // Add dust if available
            if (dustTextures.Count > 0 && dustLayers > 0)
            {
                //Debug.Log($"Adding {dustLayers} dust layers using {dustTextures.Count} available textures");
                for (int i = 0; i < dustLayers; i++)
                {
                    Texture2D dustTexture = dustTextures[Random.Range(0, dustTextures.Count)];
                    AddTextureLayer(dustTexture, dustOpacity, true); // AddTextureLayer needs to use the 'offset'
                }
            }

            // Add stars if available
            if (starSpriteTextures.Count > 0 && starLayers > 0)
            {
                //Debug.Log($"Adding {starLayers} star layers using {starSpriteTextures.Count} available textures");
                for (int i = 0; i < starLayers; i++)
                {
                    Texture2D starTexture = starSpriteTextures[Random.Range(0, starSpriteTextures.Count)];
                    AddTextureLayer(starTexture, starOpacity, false); // AddTextureLayer needs to use the 'offset'
                }
            }
            // Apply the pixels generated by AddTextureLayer back to the texture
             backgroundTexture.SetPixels(pixelColors); // Assuming AddTextureLayer modifies pixelColors directly
        }
        else
        {
            //Debug.Log("No textures available, generating procedural starfield for chunk");
            GenerateStars(pixels); // GenerateStars needs to use the 'offset'
            backgroundTexture.SetPixels(pixels);
        }
        
        // Restore original offset if needed
        this.offset = originalOffset;

        // Apply all changes to the texture
        backgroundTexture.Apply();

        // ApplyTextureToMaterial() will be called by GenerateForChunk
        // Debug.Log($"[{this.name}] Space background texture generated successfully for chunk Seed:{generationSeed} Offset:{generationOffset}");
    }
    
    // Modify AddTextureLayer and GenerateStars to respect the 'this.offset' field
    // Example modification for AddTextureLayer:
    private void AddTextureLayer(Texture2D sourceTexture, float opacity, bool useBlending)
    {
        // Skip if source texture is null
        if (sourceTexture == null)
        {
            Debug.LogWarning("Attempted to add null texture layer");
            return;
        }

        // Validate pixel array
        if(pixelColors == null || pixelColors.Length != textureWidth * textureHeight) {
            Debug.LogError("PixelColors array not initialized correctly in AddTextureLayer");
            return;
        }

        // Randomly position and scale the texture
        float scale = Random.Range(0.5f, 2.0f);
        float xLayerOffset = Random.Range(0f, 1f);
        float yLayerOffset = Random.Range(0f, 1f);

        // Sample the source texture with random position and scale
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                // Incorporate offset
                float u = (x / (float)textureWidth + xLayerOffset + this.offset.x) * scale;
                float v = (y / (float)textureHeight + yLayerOffset + this.offset.y) * scale;

                // Use frac to wrap around (0-1 range)
                u = u - Mathf.Floor(u);
                v = v - Mathf.Floor(v);

                // Sample the source texture
                Color sourceColor = sourceTexture.GetPixelBilinear(u, v);

                // Apply a random hue shift if it's a nebula or dust
                if (useBlending)
                {
                    float hue, saturation, value;
                    Color.RGBToHSV(sourceColor, out hue, out saturation, out value);
                    hue = (hue + Random.Range(-0.2f, 0.2f)) % 1f;
                    if (hue < 0) hue += 1f; // Ensure hue is positive
                    sourceColor = Color.HSVToRGB(hue, saturation, value);
                    
                    // Ensure decent opacity for visibility
                    if (sourceColor.a < 0.1f) sourceColor.a = 0.3f;
                }

                // Apply opacity
                sourceColor.a *= opacity;

                // Get the destination index
                int index = y * textureWidth + x;

                // Blend the source color with the base pixel - use screen blend for nebulae
                Color baseColor = pixelColors[index];
                
                if (useBlending)
                {
                    // Screen blend for nebulae and clouds
                    pixelColors[index] = ScreenBlend(baseColor, sourceColor);
                }
                else
                {
                    // Normal alpha blend for stars and other elements
                    pixelColors[index] = Color.Lerp(baseColor, sourceColor, sourceColor.a);
                }
            }
        }
    }

    // Modify GenerateStars to use the offset
    private void GenerateStars(Color[] pixels) // Keep parameter here
    {
        // Add a simple procedural starfield
        int numStars = Mathf.RoundToInt(textureWidth * textureHeight * starDensity);
        
        // Use the CHUNK's random state
        // Random.InitState(seed); // No - already initialized in GenerateTexture
        
        // Need a noise source that respects the offset for star placement/clustering if desired
        // For simple random placement, offset doesn't matter directly, but seed does.

        for (int i = 0; i < numStars; i++)
        {
            // Use chunk's random state for position
            int x = Random.Range(0, textureWidth);
            int y = Random.Range(0, textureHeight);
            int index = y * textureWidth + x;

            // Star brightness - use chunk's random state
            float brightness = Random.Range(0.3f, 1.0f);
            Color starColor = Color.white * brightness;

            // Add some color variation - use chunk's random state
            if (Random.value < 0.3f)
            {
                // Random hue for some stars
                float hue = Random.Range(0f, 1f);
                float saturation = Random.Range(0.0f, 0.3f);
                starColor = Color.HSVToRGB(hue, saturation, brightness);
            }

            pixels[index] = Color.Lerp(pixels[index], starColor, starOpacity);

            // Occasionally add a bigger star with bloom - use chunk's random state
            if (Random.value < 0.1f)
            {
                int bloomSize = Random.Range(1, 3);
                float bloomIntensity = Random.Range(0.3f, 0.7f);

                for (int bx = -bloomSize; bx <= bloomSize; bx++)
                {
                    for (int by = -bloomSize; by <= bloomSize; by++)
                    {
                        // Skip the center pixel (already set)
                        if (bx == 0 && by == 0) continue;

                        int bloomX = x + bx;
                        int bloomY = y + by;

                        // Make sure we're in bounds
                        if (bloomX >= 0 && bloomX < textureWidth && bloomY >= 0 && bloomY < textureHeight)
                        {
                            int bloomIndex = bloomY * textureWidth + bloomX;
                            float distance = Mathf.Sqrt(bx * bx + by * by);
                            float intensity = bloomIntensity * (1f - distance / (bloomSize + 1f));

                            pixels[bloomIndex] = Color.Lerp(pixels[bloomIndex], starColor, intensity * starOpacity);
                        }
                    }
                }
            }
        }
    }

    // Regenerate with current settings
    [ContextMenu("Regenerate Texture")]
    void Regenerate() {
        if (backgroundMaterial == null) {
            backgroundMaterial = GetComponent<Renderer>().material;
        }
        GenerateTexture();
    }
    
    // Generate a new random scene
    [ContextMenu("Generate Random Scene")]
    void GenerateRandom() {
        seed = Random.Range(0, 10000);
        
        // Optionally randomize scene type too
        sceneType = (SceneType)Random.Range(0, System.Enum.GetValues(typeof(SceneType)).Length);
        
        ApplySceneTypePreset();
        Regenerate();
    }

    private void ApplyTextureToMaterial()
    {
        // Make sure we have a renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("No renderer found on this GameObject. Please add a MeshRenderer.");
            return;
        }
        
        // Make sure we have a material
        if (renderer.material == null)
        {
            Debug.LogError("No material found on the renderer. Please assign a material.");
            return;
        }
        
        // Apply the texture to the material
        if (backgroundTexture != null)
        {
            Material mat = renderer.material;
            mat.mainTexture = backgroundTexture;
            
            // Make sure material is set to correctly display texture
            // The default shader should be set to a shader that can display textures like "Unlit/Texture"
            if (mat.shader.name.Contains("Unlit") || mat.shader.name.Contains("Particles") || mat.shader.name.Contains("Sprite"))
            {
                // These shaders typically work well with the texture
            }
            else
            {
                Debug.LogWarning($"Current shader {mat.shader.name} might not display texture correctly. Consider using Unlit/Texture.");
            }
            
            Debug.Log("Applied background texture to material successfully");
        }
        else
        {
            Debug.LogError("Cannot apply null texture to material. Background texture was not generated properly.");
        }
    }

    // Debug method to log all available textures - can be called from the Inspector
    [ContextMenu("Debug - List All Textures")]
    public void DebugListAllTextures()
    {
        // Try to load textures first if the lists are empty
        if (nebulaTextures.Count == 0 && dustTextures.Count == 0 && starSpriteTextures.Count == 0)
        {
            Debug.Log("No textures loaded yet, attempting to load textures first...");
            LoadTextures();
        }
        
        // Log the textures we have
        Debug.Log($"=== SPACE BACKGROUND TEXTURE REPORT ===");
        Debug.Log($"Total textures loaded: {nebulaTextures.Count + dustTextures.Count + starSpriteTextures.Count}");
        
        // Log nebula textures
        Debug.Log($"NEBULA TEXTURES ({nebulaTextures.Count}):");
        for (int i = 0; i < nebulaTextures.Count; i++)
        {
            if (nebulaTextures[i] != null)
            {
                Debug.Log($"  [{i}] {nebulaTextures[i].name} - {nebulaTextures[i].width}x{nebulaTextures[i].height}");
            }
            else
            {
                Debug.Log($"  [{i}] NULL TEXTURE REFERENCE");
            }
        }
        
        // Log dust textures
        Debug.Log($"DUST TEXTURES ({dustTextures.Count}):");
        for (int i = 0; i < dustTextures.Count; i++)
        {
            if (dustTextures[i] != null)
            {
                Debug.Log($"  [{i}] {dustTextures[i].name} - {dustTextures[i].width}x{dustTextures[i].height}");
            }
            else
            {
                Debug.Log($"  [{i}] NULL TEXTURE REFERENCE");
            }
        }
        
        // Log star textures
        Debug.Log($"STAR TEXTURES ({starSpriteTextures.Count}):");
        for (int i = 0; i < starSpriteTextures.Count; i++)
        {
            if (starSpriteTextures[i] != null)
            {
                Debug.Log($"  [{i}] {starSpriteTextures[i].name} - {starSpriteTextures[i].width}x{starSpriteTextures[i].height}");
            }
            else
            {
                Debug.Log($"  [{i}] NULL TEXTURE REFERENCE");
            }
        }
        
        // Log texture search paths
        Debug.Log("TEXTURE SEARCH PATHS:");
        Debug.Log($"  Resources/@Textures");
        Debug.Log($"  Resources/@Textures/Nebula");
        Debug.Log($"  Resources/@Textures/Dust");
        Debug.Log($"  Resources/@Textures/Stars");
        Debug.Log($"  Assets/@Textures/");
        Debug.Log($"  (Plus subdirectories)");
        
        Debug.Log($"=== END OF REPORT ===");
    }

    // Emergency method to try to fix texture loading issues by scanning the filesystem directly
    [ContextMenu("Emergency Texture Loading Fix")]
    public void EmergencyTextureLoadingFix()
    {
        Debug.Log("Attempting emergency texture loading fix...");
        
        // Clear existing textures first
        nebulaTextures.Clear();
        dustTextures.Clear();
        starSpriteTextures.Clear();
        
        // Try the standard loading first
        LoadTextures();
        
        // Check if we found any textures
        if (nebulaTextures.Count > 0 || dustTextures.Count > 0 || starSpriteTextures.Count > 0)
        {
            Debug.Log("Standard texture loading worked, no emergency needed.");
            return;
        }
        
        Debug.Log("Standard loading failed, trying emergency direct filesystem scan...");
        
#if UNITY_EDITOR
        // Try a more aggressive approach by scanning the entire Assets folder
        try
        {
            // Use AssetDatabase to find all textures in the project
            string[] allTexturePaths = UnityEditor.AssetDatabase.FindAssets("t:Texture2D");
            Debug.Log($"Found {allTexturePaths.Length} textures in entire project");
            
            foreach (string guid in allTexturePaths)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string filename = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
                
                if (path.Contains("@Textures") || filename.Contains("nebula") || filename.Contains("dust") || 
                    filename.Contains("star") || filename.Contains("space") || filename.Contains("galaxy"))
                {
                    Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    
                    if (texture != null)
                    {
                        // Categorize based on filename
                        if (filename.Contains("nebula") || path.Contains("Nebula"))
                        {
                            nebulaTextures.Add(texture);
                            Debug.Log($"Emergency loaded nebula texture: {path}");
                        }
                        else if (filename.Contains("dust") || path.Contains("Dust"))
                        {
                            dustTextures.Add(texture);
                            Debug.Log($"Emergency loaded dust texture: {path}");
                        }
                        else if (filename.Contains("star") || path.Contains("Star"))
                        {
                            starSpriteTextures.Add(texture);
                            Debug.Log($"Emergency loaded star texture: {path}");
                        }
                        // If we can't categorize based on name, try to guess by visual characteristics
                        else if (path.Contains("@Textures"))
                        {
                            // Just categorize as a generic texture
                            starSpriteTextures.Add(texture);
                            Debug.Log($"Emergency loaded generic texture: {path}");
                        }
                    }
                }
            }
            
            Debug.Log($"Emergency loading complete. Found {nebulaTextures.Count} nebulae, {dustTextures.Count} dust clouds, and {starSpriteTextures.Count} stars.");
            
            // If we still don't have any textures, create some simple procedural ones
            if (nebulaTextures.Count == 0 && dustTextures.Count == 0 && starSpriteTextures.Count == 0)
            {
                Debug.LogWarning("Emergency loading did not find any textures. Creating procedural fallbacks...");
                CreateProceduralTextureFallbacks();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during emergency texture loading: {e.Message}");
        }
#else
        Debug.LogWarning("Emergency texture loading is only available in the Unity Editor.");
#endif
    }
    
    private void CreateProceduralTextureFallbacks()
    {
#if UNITY_EDITOR
        Debug.Log("Creating procedural texture fallbacks...");
        
        // Create a simple star texture
        Texture2D starTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Color[] starPixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                // Calculate distance from center
                float dx = x - 64;
                float dy = y - 64;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                
                // Create a radial gradient
                float intensity = 1f - Mathf.Clamp01(distance / 32f);
                intensity = Mathf.Pow(intensity, 2); // Square to make it fall off faster
                
                starPixels[y * 128 + x] = new Color(1f, 1f, 1f, intensity);
            }
        }
        starTexture.SetPixels(starPixels);
        starTexture.Apply();
        starTexture.name = "ProceduralStar";
        starSpriteTextures.Add(starTexture);
        
        // Create a simple nebula texture with perlin noise
        Texture2D nebulaTexture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        Color[] nebulaPixels = new Color[512 * 512];
        
        System.Random random = new System.Random(12345); // Fixed seed for reproducibility
        
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                // Use Perlin noise for nebula-like patterns
                float scale = 0.01f;
                float noise = Mathf.PerlinNoise(x * scale, y * scale);
                float noise2 = Mathf.PerlinNoise((x + 500) * scale * 2, (y + 500) * scale * 2);
                
                // Combine different octaves
                float combined = noise * 0.7f + noise2 * 0.3f;
                
                // Apply threshold for cloud-like appearance
                float alpha = combined > 0.5f ? Mathf.Clamp01((combined - 0.5f) * 2f) : 0f;
                
                // Random color for the nebula
                float r = 0.5f + 0.5f * (float)random.NextDouble();
                float g = 0.2f + 0.3f * (float)random.NextDouble();
                float b = 0.7f + 0.3f * (float)random.NextDouble();
                
                nebulaPixels[y * 512 + x] = new Color(r, g, b, alpha * 0.5f);
            }
        }
        nebulaTexture.SetPixels(nebulaPixels);
        nebulaTexture.Apply();
        nebulaTexture.name = "ProceduralNebula";
        nebulaTextures.Add(nebulaTexture);
        
        Debug.Log("Created procedural fallback textures");
#endif
    }

    // --- NEW PUBLIC METHOD FOR CHUNK GENERATION ---
    
    /// <summary>
    /// Generates the procedural background texture for a specific grid chunk.
    /// Uses the grid coordinates to determine a unique seed/offset.
    /// </summary>
    /// <param name="gridCoord">The integer coordinates of the chunk in the infinite grid.</param>
    /// <param name="managerBaseSeed">A base seed from the managing generator to ensure variation between sessions.</param>
    public void GenerateForChunk(Vector2Int gridCoord, int managerBaseSeed)
    {
        // 1. Determine Unique Seed/Offset based on gridCoord
        // Combine base seed with grid coords for deterministic but unique generation per chunk
        // Using a simple hash-like combination. Ensure large enough offsets.
        int chunkSeed = managerBaseSeed ^ (gridCoord.x * 73856093) ^ (gridCoord.y * 19349663);
        // We could also use the gridCoord to directly calculate the offset for Perlin noise sampling
        // This might be better for potential seamless tiling if noise function allows.
        Vector2 chunkOffset = new Vector2(gridCoord.x, gridCoord.y) * 0.5f; // Example offset scaling factor - adjust as needed!
        
        Debug.Log($"[{this.name}] Generating for Chunk {gridCoord} with Seed: {chunkSeed}, Offset: {chunkOffset}");
        
        // 2. Generate the texture using this seed/offset
        // We need to modify GenerateTexture or pass these values in.
        // Let's modify GenerateTexture to accept seed/offset temporarily. 
        // A better approach might be to have separate generation functions.
        GenerateTexture(chunkSeed, chunkOffset);
        
        // 3. Apply to material
        ApplyTextureToMaterial();
    }
}