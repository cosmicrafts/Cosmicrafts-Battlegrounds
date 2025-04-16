using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;

// ICP libraries
using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Candid.Models;
using Org.BouncyCastle.Crypto.Parameters;

// Project-specific
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;

/// <summary>
/// GameManager component that receives the seed phrase from Vue and passes it to ICPService.
/// This matches what Vue is sending to.
/// </summary>
public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        // Ensure this GameObject is named "GameManager" so Vue can find it
        gameObject.name = "GameManager";
        Debug.Log("[GameManager] Initialized. GameObject name set to: GameManager");
    }

    /// <summary>
    /// Receives seed phrase from Vue and passes it to ICPService
    /// This method name must match exactly what Vue is calling: ReceiveSeedPhrase
    /// </summary>
    public void ReceiveSeedPhrase(string seedPhrase)
    {
        Debug.Log("[GameManager] Received seed phrase from Vue. Forwarding to ICPService.");
        
        // Find ICPService and forward the seed phrase
        ICPService icpService = UnityEngine.Object.FindFirstObjectByType<ICPService>();
        if (icpService != null)
        {
            icpService.SetSeedPhrase(seedPhrase);
        }
        else
        {
            Debug.LogError("[GameManager] ICPService not found in scene! Cannot forward seed phrase.");
        }
    }
}

/// <summary>
/// Minimal service to handle ICP authentication and player data retrieval.
/// </summary>
public class ICPService : MonoBehaviour
{
    // Singleton instance
    public static ICPService Instance { get; private set; }
    
    // Public properties
    public bool IsInitialized { get; private set; }
    public string PrincipalId { get; private set; }
    public BackendApiClient MainCanister { get; private set; }
    public Player CurrentPlayer { get; private set; }
    
    // Events
    public event Action OnICPInitialized;
    public event Action<Player> OnPlayerDataReceived;
    
    // Development settings
    [Header("Development Settings")]
    [SerializeField] private bool useDevelopmentModeInEditor = true;
    [SerializeField] private string devSeedPhrase = "coconut teach old consider vivid leader minute canoe original suspect skirt pause";
    [SerializeField] private string canisterId = "opcce-byaaa-aaaak-qcgda-cai";

    private string seedPhrase;

    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RequestSeedPhraseFromJS();
    
    [DllImport("__Internal")]
    private static extern void ListActiveGameObjects();
    #endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Ensure this GameObject is named "ICPService"
        gameObject.name = "ICPService";
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Print information about this GameObject for debugging
        Debug.Log($"[ICPService] Initialized. GameObject name is forced to: {gameObject.name}");
        Debug.Log($"[ICPService] Transform path: {GetTransformPath(transform)}");
        
        // Register to static functions for easier access from JavaScript
        WebGLBridge.OnSeedPhraseReceived = SetSeedPhrase;
        
        // In WebGL, ensure GameManager exists to receive messages from Vue
        #if UNITY_WEBGL && !UNITY_EDITOR
        EnsureGameManagerExists();
        #endif
    }

    /// <summary>
    /// Ensures a GameManager exists in the scene to receive Vue messages
    /// </summary>
    private void EnsureGameManagerExists()
    {
        GameManager gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            // Create a new GameObject with GameManager component
            GameObject gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();
            DontDestroyOnLoad(gameManagerObject);
            Debug.Log("[ICPService] Created GameManager to receive Vue messages");
        }
    }

    void Start()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
            Log("WebGL mode - requesting seed phrase from web app");
            RequestSeedPhraseFromJS();
            // Also list active GameObjects to help diagnose the hierarchy
            ListActiveGameObjects();
        #else
            if (useDevelopmentModeInEditor) {
                Log("Using development seed phrase");
                seedPhrase = devSeedPhrase;
                StartCoroutine(InitializeWithSeedPhrase(seedPhrase));
            }
        #endif
    }
    
    /// <summary>
    /// This method is called by JavaScript (via SendMessage) to set the seed phrase from the Vue frontend,
    /// or by GameManager when it receives the seed phrase from Vue
    /// </summary>
    public void SetSeedPhrase(string phraseFromJS)
    {
        if (string.IsNullOrEmpty(phraseFromJS))
        {
            LogError("Received empty seed phrase from JavaScript");
            return;
        }
        
        Log("Received seed phrase from web app: " + (phraseFromJS.Length > 10 ? phraseFromJS.Substring(0, 10) + "..." : phraseFromJS));
        seedPhrase = phraseFromJS;
        StartCoroutine(InitializeWithSeedPhrase(seedPhrase));
    }
    
    /// <summary>
    /// Initialize ICP identity using a seed phrase
    /// </summary>
    public IEnumerator InitializeWithSeedPhrase(string seedPhrase)
    {
        if (IsInitialized) yield break;
        
        Log("Initializing with seed phrase");
        
        Ed25519Identity createdIdentity = null;
        bool identityCreationFailed = false;

        // Start the identity creation coroutine and wait for it to finish
        yield return StartCoroutine(CreateIdentityFromSeedPhrase(seedPhrase, identity => {
            if (identity == null)
            {
                LogError("Failed to create identity from seed phrase");
                identityCreationFailed = true;
            }
            else
            {
                createdIdentity = identity;
            }
        }));

        // If identity creation failed, stop here
        if (identityCreationFailed || createdIdentity == null)
        {
            LogError("Stopping initialization due to identity creation failure.");
            IsInitialized = false;
            yield break;
        }
        
        // Now that identity is created, proceed with the rest inside a try block
        try
        {
            // Create HttpAgent with the identity
            var httpClient = new UnityHttpClient();
            var agent = new HttpAgent(httpClient, createdIdentity); // Use the created identity
            
            // Store the principal ID
            PrincipalId = createdIdentity.GetPublicKey().ToPrincipal().ToText(); // Use the created identity
            Log($"Identity initialized with principal: {PrincipalId}");
            
            // Initialize the BackendApiClient with the agent and canister ID
            Principal canisterPrincipal = Principal.FromText(canisterId);
            MainCanister = new BackendApiClient(agent, canisterPrincipal);
            
            IsInitialized = true;
            
            // Notify listeners that initialization is complete
            OnICPInitialized?.Invoke();
            
            // Don't yield inside try-catch
            Log("Initialization complete, now fetching player data");
        }
        catch (Exception e)
        {
            LogError($"Initialization failed after identity creation: {e.Message}");
            IsInitialized = false;
            yield break; // Exit if initialization failed
        }
        
        // If we got here, initialization succeeded, so fetch player data
        if (IsInitialized)
        {
            // Fetch player data after initialization - moved outside try-catch
            yield return StartCoroutine(GetPlayerData());
            
            // Check if we need to handle player creation (when no player data found)
            if (CurrentPlayer == null)
            {
                Log("No player found for current identity, may need to handle signup");
                // Uncomment and implement the following when you add SignupNewPlayer functionality
                // yield return StartCoroutine(SignupNewPlayer());
            }
        }
    }
    
    /// <summary>
    /// Helper method to create identity from seed phrase using coroutine
    /// </summary>
    private IEnumerator CreateIdentityFromSeedPhrase(string seedPhrase, Action<Ed25519Identity> callback)
    {
        // Run on a separate thread via Task to avoid freezing the UI
        Task<Ed25519Identity> task = Task.Run(() => {
            try
            {
                // BIP39 derivation
                byte[] seedBytes = MnemonicToSeed(seedPhrase);
                
                // Use first 32 bytes for private key
                byte[] privateKeyBytes = new byte[32];
                Array.Copy(seedBytes, 0, privateKeyBytes, 0, 32);
                
                // Use the seed for Ed25519 key generation
                var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);
                
                // Derive the public key
                var publicKey = privateKey.GeneratePublicKey();
                
                // Create the identity
                return new Ed25519Identity(publicKey.GetEncoded(), privateKey.GetEncoded());
            }
            catch (Exception e)
            {
                Debug.LogError($"[ICPService] Error creating identity: {e.Message}");
                return null;
            }
        });
        
        // Small delay to ensure we don't block the main thread
        yield return new WaitForSeconds(0.1f);
        
        // Wait for task to complete
        while (!task.IsCompleted)
        {
            yield return null;
        }
        
        // Get result and invoke callback
        callback?.Invoke(task.Result);
    }
    
    /// <summary>
    /// Fetches the current player's data from the blockchain
    /// </summary>
    public IEnumerator GetPlayerData()
    {
        if (!IsInitialized)
        {
            LogError("Cannot get player data: not initialized");
            yield break;
        }
        
        Log("Fetching player data");
        
        // Create a task to fetch player data
        Task<OptionalValue<Player>> playerTask = MainCanister.GetPlayer();
        
        // Wait for task to complete
        while (!playerTask.IsCompleted)
        {
            yield return null;
        }
        
        try
        {
            if (playerTask.IsFaulted)
            {
                LogError($"Error fetching player data: {playerTask.Exception.Message}");
                yield break;
            }
            
            var playerInfo = playerTask.Result;
            
            if (playerInfo.HasValue)
            {
                CurrentPlayer = playerInfo.ValueOrDefault;
                Log($"Player found: {CurrentPlayer.Username} (Level {CurrentPlayer.Level})");
                
                // Notify listeners
                OnPlayerDataReceived?.Invoke(CurrentPlayer);
            }
            else
            {
                Log("No player found for current identity");
                // We'll need to implement player signup logic here in the future
            }
        }
        catch (Exception e)
        {
            LogError($"Error processing player data: {e.Message}");
        }
    }
    
    /// <summary>
    /// BIP39 mnemonic to seed conversion
    /// </summary>
    private byte[] MnemonicToSeed(string mnemonic)
    {
        string salt = "mnemonic";
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic.Normalize(NormalizationForm.FormKD));
        
        // PBKDF2 with HMAC-SHA512, 2048 iterations, 64 bytes output
        using (var deriveBytes = new System.Security.Cryptography.Rfc2898DeriveBytes(
            mnemonicBytes, saltBytes, 2048, System.Security.Cryptography.HashAlgorithmName.SHA512))
        {
            return deriveBytes.GetBytes(64);
        }
    }
    
    /// <summary>
    /// Get a full path string for debugging purposes
    /// </summary>
    private string GetTransformPath(Transform transform)
    {
        if (transform.parent == null)
            return transform.name;
        return GetTransformPath(transform.parent) + "/" + transform.name;
    }
    
    // Simplified logging helpers
    private void Log(string message) => Debug.Log($"[ICPService] {message}");
    private void LogWarning(string message) => Debug.LogWarning($"[ICPService] {message}");
    private void LogError(string message) => Debug.LogError($"[ICPService] {message}");
}

/// <summary>
/// Data structure for ICP identity information
/// </summary>
public class ICPIdentityData
{
    public string principalId;
    public string publicKey;
    public string seedPhrase;
    public string derivationPath;
}

/// <summary>
/// Static bridge class that helps JavaScript find Unity methods regardless of GameObject name
/// </summary>
public static class WebGLBridge
{
    // Reference to the SetSeedPhrase method
    public static Action<string> OnSeedPhraseReceived;
    
    // This method can be called by JavaScript without knowing the specific GameObject
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RegisterGlobalFunctions()
    {
        Debug.Log("[WebGLBridge] Registering global functions for JavaScript interop");
    }
    
    // This static method can be called directly from JavaScript 
    public static void SetSeedPhraseGlobal(string seedPhrase)
    {
        Debug.Log("[WebGLBridge] Received seed phrase via global function");
        if (OnSeedPhraseReceived != null)
        {
            OnSeedPhraseReceived(seedPhrase);
        }
        else
        {
            Debug.LogError("[WebGLBridge] OnSeedPhraseReceived is not registered. ICPService might not be initialized yet.");
            
            // Try to find the ICPService manually
            ICPService service = UnityEngine.Object.FindAnyObjectByType<ICPService>();
            if (service != null)
            {
                Debug.Log("[WebGLBridge] Found ICPService manually, calling SetSeedPhrase");
                service.SetSeedPhrase(seedPhrase);
            }
            else
            {
                Debug.LogError("[WebGLBridge] Could not find ICPService in scene");
            }
        }
    }
} 