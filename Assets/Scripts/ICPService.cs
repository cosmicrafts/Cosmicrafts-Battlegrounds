using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;
using System.Linq;

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
/// Refactored to avoid coroutines and Task.Run for initialization.
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
            ListActiveGameObjects(); // Keep for debugging JS calls
        #elif UNITY_EDITOR // Use #elif UNITY_EDITOR for clarity
            if (useDevelopmentModeInEditor) {
                Log("Editor: Using development seed phrase for initialization.");
                 _ = InitializeAsync(devSeedPhrase); // Fire-and-forget async call
            }
            else {
                 LogWarning("Editor: Development mode disabled. Waiting for manual seed phrase input or other trigger.");
            }
        #endif
    }
    
    /// <summary>
    /// Called by JavaScript (via WebGLBridge) or GameManager to set the seed phrase.
    /// Triggers the asynchronous initialization process.
    /// </summary>
    public void SetSeedPhrase(string phraseFromJS)
    {
        if (string.IsNullOrEmpty(phraseFromJS))
        {
            LogError("Received empty or null seed phrase.");
            return;
        }

        Log($"Received seed phrase (length: {phraseFromJS.Length}). Triggering initialization.");
        Log("First 10 chars: " + (phraseFromJS.Length > 10 ? phraseFromJS.Substring(0, 10) + "..." : phraseFromJS));

        #if UNITY_WEBGL && !UNITY_EDITOR
        // HACK: Force reset IsInitialized in WebGL just before attempting init.
        // If this works, it confirms something else set it true beforehand.
        Log("[HACK] Forcing IsInitialized = false in SetSeedPhrase (WebGL)");
        IsInitialized = false; 
        #endif

        // Trigger the async initialization process.
        // We don't typically await calls originating from Unity messages or JS directly.
         _ = InitializeAsync(phraseFromJS); // Fire-and-forget async call
    }
    
    /// <summary>
    /// Asynchronous initialization process. Orchestrates identity creation and player data fetching.
    /// </summary>
    private async Task InitializeAsync(string seedPhrase)
    {
        Log($"InitializeAsync started. Current IsInitialized: {IsInitialized}");
        if (IsInitialized)
        {
            LogWarning("InitializeAsync: Already initialized, ignoring.");
            return;
        }
         if (string.IsNullOrEmpty(seedPhrase))
        {
             LogError("InitializeAsync: Seed phrase is null or empty. Aborting.");
             return;
        }

        Log("InitializeAsync: Creating identity...");
        Ed25519Identity createdIdentity = null;
        try
        {
            // *** Run SYNCHRONOUSLY on the current (main) thread ***
            createdIdentity = CreateIdentityFromSeedPhrase(seedPhrase);
        }
        catch (Exception e)
        {
             LogError($"InitializeAsync: Exception during CreateIdentityFromSeedPhrase: {e.Message}");
             LogError($"Stack Trace: {e.StackTrace}");
             // Optionally set some state to indicate failure
             return; // Stop initialization
        }


        if (createdIdentity == null)
        {
            LogError("InitializeAsync: Failed to create identity (CreateIdentityFromSeedPhrase returned null). Aborting.");
            return;
        }

        Log("InitializeAsync: Identity created successfully. Setting up Agent and Client...");

        try
        {
            // Create HttpAgent with the identity
            var httpClient = new UnityHttpClient(); // Ensure this works correctly in WebGL
            var agent = new HttpAgent(httpClient, createdIdentity);

            // Store the principal ID
            PrincipalId = createdIdentity.GetPublicKey().ToPrincipal().ToText();
            Log($"InitializeAsync: Principal ID: {PrincipalId}");

            // Initialize the BackendApiClient
            Principal canisterPrincipal = Principal.FromText(canisterId);
            MainCanister = new BackendApiClient(agent, canisterPrincipal);
            Log($"InitializeAsync: BackendApiClient initialized for canister: {canisterId}");

            // *** ADDING LOG BEFORE SETTING IsInitialized = true ***
            Log("InitializeAsync: *** Setting IsInitialized = true NOW! ***");
            IsInitialized = true; // Mark as initialized *before* fetching data
            Log("InitializeAsync: ICP Agent and Client setup complete. IsInitialized = true.");

            // Notify listeners about base initialization completion
             OnICPInitialized?.Invoke();

             Log("InitializeAsync: Fetching player data...");
             await GetPlayerDataAsync(); // Await the async player data fetch
        }
        catch (Exception e)
        {
            LogError($"InitializeAsync: Failed during Agent/Client setup or GetPlayerDataAsync: {e.Message}");
            LogError($"Stack Trace: {e.StackTrace}");
            IsInitialized = false; // Reset initialization state on failure
            PrincipalId = null;
            MainCanister = null;
        }
    }
    
    /// <summary>
    /// Creates an Ed25519Identity from a seed phrase SYNCHRONOUSLY.
    /// Runs on the calling thread (main thread in WebGL).
    /// </summary>
    private Ed25519Identity CreateIdentityFromSeedPhrase(string seedPhrase)
    {
         Log($"CreateIdentityFromSeedPhrase: Starting synchronous creation (length: {seedPhrase.Length})");
         if (string.IsNullOrEmpty(seedPhrase))
         {
              LogWarning("CreateIdentityFromSeedPhrase: Input seed phrase is null or empty.");
              return null;
         }

         try
         {
             Log("CreateIdentityFromSeedPhrase: Converting mnemonic to seed bytes...");
             byte[] seedBytes = MnemonicToSeed(seedPhrase); // This might be slow
             Log($"CreateIdentityFromSeedPhrase: Seed bytes length: {seedBytes.Length}");
             if (seedBytes == null || seedBytes.Length < 32)
             {
                  LogError("CreateIdentityFromSeedPhrase: Failed to generate sufficient seed bytes from mnemonic.");
                  return null;
             }

             byte[] privateKeyBytes = new byte[32];
             Array.Copy(seedBytes, 0, privateKeyBytes, 0, 32);
             Log("CreateIdentityFromSeedPhrase: Private key bytes extracted.");

             Log("CreateIdentityFromSeedPhrase: Generating Ed25519 key pair...");
             var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);
             var publicKey = privateKey.GeneratePublicKey();
             Log("CreateIdentityFromSeedPhrase: Public key generated.");

             var identity = new Ed25519Identity(publicKey.GetEncoded(), privateKey.GetEncoded());
             Log("CreateIdentityFromSeedPhrase: Ed25519Identity object created successfully.");
             return identity;
         }
         catch (Exception e)
         {
             LogError($"CreateIdentityFromSeedPhrase: Error during identity creation: {e.Message}");
             LogError($"Stack Trace: {e.StackTrace}");
             return null; // Return null on failure
         }
    }
    
    /// <summary>
    /// Fetches the current player's data from the blockchain asynchronously.
    /// </summary>
    private async Task GetPlayerDataAsync()
    {
        Log("GetPlayerDataAsync: Started.");
        if (!IsInitialized || MainCanister == null)
        {
            LogError("GetPlayerDataAsync: Cannot get player data - not initialized or MainCanister is null.");
            return;
        }

        Log("GetPlayerDataAsync: Calling MainCanister.GetPlayer()...");
        try
        {
            // Assume MainCanister.GetPlayer() returns Task<OptionalValue<Player>>
            // Use ConfigureAwait(false) if possible, though Unity's context might handle it.
            OptionalValue<Player> playerInfo = await MainCanister.GetPlayer(); //.ConfigureAwait(false);

            Log("GetPlayerDataAsync: GetPlayer call completed.");

            if (playerInfo.HasValue)
            {
                CurrentPlayer = playerInfo.ValueOrDefault;
                Log($"GetPlayerDataAsync: Player found: {CurrentPlayer.Username} (Level {CurrentPlayer.Level})");
                OnPlayerDataReceived?.Invoke(CurrentPlayer); // Notify listeners
            }
            else
            {
                CurrentPlayer = null; // Ensure player is null if not found
                LogWarning("GetPlayerDataAsync: No player found for the current identity.");
                // Handle player signup logic or state here if needed
            }
        }
        catch (Exception e)
        {
            LogError($"GetPlayerDataAsync: Error fetching or processing player data: {e.Message}");
            LogError($"Stack Trace: {e.StackTrace}");
            CurrentPlayer = null; // Clear player data on error
        }
        Log("GetPlayerDataAsync: Finished.");
    }
    
    /// <summary>
    /// BIP39 mnemonic to seed conversion
    /// </summary>
    private byte[] MnemonicToSeed(string mnemonic)
    {
        Log("MnemonicToSeed: Converting...");
        string salt = "mnemonic"; // Standard BIP39 salt
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic.Normalize(NormalizationForm.FormKD));
        
        // PBKDF2 with HMAC-SHA512, 2048 iterations, 64 bytes output (BIP39 Standard)
        using (var deriveBytes = new System.Security.Cryptography.Rfc2898DeriveBytes(
            mnemonicBytes, saltBytes, 2048, System.Security.Cryptography.HashAlgorithmName.SHA512))
        {
            byte[] seed = deriveBytes.GetBytes(64); // 512 bits = 64 bytes
            Log("MnemonicToSeed: Conversion complete.");
            return seed;
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