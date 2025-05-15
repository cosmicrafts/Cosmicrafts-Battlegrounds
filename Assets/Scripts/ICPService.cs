using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Runtime.InteropServices;

// ICP libraries
using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Candid.Models;
using Org.BouncyCastle.Crypto.Parameters;

// Project-specific
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;

// Additional types
using Username = System.String;
using Avatarid1 = EdjCase.ICP.Candid.Models.UnboundedUInt;
using AvatarID = EdjCase.ICP.Candid.Models.UnboundedUInt;
using ReferralCode = System.String;
using PlayerId = EdjCase.ICP.Candid.Models.Principal;

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
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Log("Initialized");
    }
    
    private async void Start()
    {
        // Auto-initialize in Editor with development mode if enabled
        #if UNITY_EDITOR
        if (useDevelopmentModeInEditor)
        {
            Log("Using development mode");
            await InitializeWithSeedPhrase(devSeedPhrase);
        }
        #elif UNITY_WEBGL
        Log("WebGL mode - waiting for identity from web app");
        // WebGL initialization will be triggered externally
        RequestAuthenticationData();
        #endif
    }
    
    /// <summary>
    /// Initialize ICP identity using a seed phrase
    /// </summary>
    public async Task InitializeWithSeedPhrase(string seedPhrase)
    {
        if (IsInitialized) return;
        
        Log("Initializing with seed phrase");
        
        try
        {
            // Create identity from seed phrase
            Ed25519Identity identity = await CreateIdentityFromSeedPhrase(seedPhrase);
            
            // Create HttpAgent with the identity
            var httpClient = new UnityHttpClient();
            var agent = new HttpAgent(httpClient, identity);
            
            // Store the principal ID
            PrincipalId = identity.GetPublicKey().ToPrincipal().ToText();
            Log($"Identity initialized with principal: {PrincipalId}");
            
            // Initialize the BackendApiClient with the agent and canister ID
            Principal canisterPrincipal = Principal.FromText(canisterId);
            MainCanister = new BackendApiClient(agent, canisterPrincipal);
            
            IsInitialized = true;
            
            // Fetch player data after initialization
            await GetPlayerData();
            
            // Notify listeners that initialization is complete
            OnICPInitialized?.Invoke();
        }
        catch (Exception e)
        {
            LogError($"Initialization failed: {e.Message}");
            IsInitialized = false;
        }
    }
    
    /// <summary>
    /// Helper method to create identity from seed phrase
    /// </summary>
    private async Task<Ed25519Identity> CreateIdentityFromSeedPhrase(string seedPhrase)
    {
        // BIP39 derivation
        byte[] seedBytes = MnemonicToSeed(seedPhrase);
        
        // Use first 32 bytes for private key
        byte[] privateKeyBytes = new byte[32];
        Array.Copy(seedBytes, 0, privateKeyBytes, 0, 32);
        
        // Simulate an async operation
        await Task.Delay(10);
        
        // Use the seed for Ed25519 key generation
        var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);
        
        // Derive the public key
        var publicKey = privateKey.GeneratePublicKey();
        
        // Create the identity
        return new Ed25519Identity(publicKey.GetEncoded(), privateKey.GetEncoded());
    }
    
    /// <summary>
    /// Fetches the current player's data from the blockchain
    /// </summary>
    public async Task<Player> GetPlayerData()
    {
        if (!IsInitialized)
        {
            LogError("Cannot get player data: not initialized");
            return null;
        }
        
        Log("Fetching player data");
        
        try
        {
            var playerInfo = await MainCanister.GetPlayer();
            
            if (playerInfo.HasValue)
            {
                CurrentPlayer = playerInfo.ValueOrDefault;
                Log($"Player found: {CurrentPlayer.Username} (Level {CurrentPlayer.Level})");
                
                // Notify listeners
                OnPlayerDataReceived?.Invoke(CurrentPlayer);
                
                return CurrentPlayer;
            }
            else
            {
                Log("No player found for current identity");
                
                // Handle the case where no player is found - prompt for signup
                await SignupNewPlayer();
                
                return CurrentPlayer;
            }
        }
        catch (Exception e)
        {
            LogError($"Error fetching player data: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Creates a new player account when none exists
    /// </summary>
    public async Task<bool> SignupNewPlayer(string username = null, int avatarId = 1, string referralCode = null, string language = "en")
    {
        if (!IsInitialized)
        {
            LogError("Cannot signup new player: not initialized");
            return false;
        }
        
        try
        {
            // If username is null, generate a random one
            username = username ?? $"Player{UnityEngine.Random.Range(1000, 9999)}";
            
            // Create username from string
            var usernameObj = username;
            
            // Create avatar ID from int (1-12 as mentioned)
            var avatarIdObj = (UnboundedUInt)avatarId;
            
            // Create optional referral code
            var referralCodeArg = string.IsNullOrEmpty(referralCode) 
                ? new BackendApiClient.SignupArg2() 
                : new BackendApiClient.SignupArg2(referralCode);
            
            // Call signup method
            Log($"Signing up new player with username: {username}, avatarId: {avatarId}, language: {language}");
            var result = await MainCanister.Signup(usernameObj, avatarIdObj, referralCodeArg, language);
            
            if (result.ReturnArg0)
            {
                Log("Signup successful");
                
                // Store the new player data
                if (result.ReturnArg1.HasValue)
                {
                    CurrentPlayer = result.ReturnArg1.ValueOrDefault;
                    Log($"Player created: {CurrentPlayer.Username} (Level {CurrentPlayer.Level})");
                    
                    // Notify listeners
                    OnPlayerDataReceived?.Invoke(CurrentPlayer);
                    
                    return true;
                }
                else
                {
                    LogWarning("Signup successful but no player data returned");
                    return true;
                }
            }
            else
            {
                LogError($"Signup failed: {result.ReturnArg2}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Error during signup: {e.Message}");
            return false;
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
    
    // Simplified logging helpers
    private void Log(string message) => Debug.Log($"[ICPService] {message}");
    private void LogWarning(string message) => Debug.LogWarning($"[ICPService] {message}");
    private void LogError(string message) => Debug.LogError($"[ICPService] {message}");
    
    /// <summary>
    /// Receives authentication data from the web app
    /// </summary>
    public void ReceiveAuthData(string authDataJson)
    {
        Log($"Received auth data from web app: {authDataJson}");
        
        try
        {
            // Process auth data from JSON
            // Implementation depends on your auth data structure
            Log("Auth data received successfully");
        }
        catch (Exception e)
        {
            LogError($"Error processing auth data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Sets the ICP identity from web app
    /// </summary>
    public async void SetICPIdentity(string icpIdentityJson)
    {
        Log($"Received ICP identity from web app");
        
        try
        {
            // Parse the identity data
            ICPIdentityData identityData = JsonUtility.FromJson<ICPIdentityData>(icpIdentityJson);
            
            // Initialize with the seed phrase
            if (!string.IsNullOrEmpty(identityData.seedPhrase))
            {
                await InitializeWithSeedPhrase(identityData.seedPhrase);
            }
            else
            {
                LogError("No seed phrase provided in identity data");
            }
        }
        catch (Exception e)
        {
            LogError($"Error setting ICP identity: {e.Message}");
        }
    }
    
    /// <summary>
    /// Sets player data received from web app
    /// </summary>
    public void SetPlayerData(string playerDataJson)
    {
        Log($"Received player data from web app");
        
        try
        {
            // Process player data if needed
            // This might be used to update local cache without a blockchain call
            Log("Player data received successfully");
        }
        catch (Exception e)
        {
            LogError($"Error processing player data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Request authentication data from the web app
    /// </summary>
    public void RequestAuthenticationData()
    {
        Log("Requesting authentication data from web app");
        #if !UNITY_EDITOR && UNITY_WEBGL
            RequestAuthData();
        #else
            Log("Not in WebGL mode, cannot request auth data from web app");
        #endif
    }
    
    /// <summary>
    /// Request logout from the web app
    /// </summary>
    public void RequestLogoutFromWebApp()
    {
        Log("Requesting logout from web app");
        #if !UNITY_EDITOR && UNITY_WEBGL
            RequestLogout();
        #else
            Log("Not in WebGL mode, cannot request logout from web app");
        #endif
    }
    
    /// <summary>
    /// Save player data to the web app
    /// </summary>
    public void SavePlayerDataToWebApp(string playerDataJson)
    {
        Log($"Saving player data to web app");
        #if !UNITY_EDITOR && UNITY_WEBGL
            SavePlayerData(playerDataJson);
        #else
            Log("Not in WebGL mode, cannot save player data to web app");
        #endif
    }
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