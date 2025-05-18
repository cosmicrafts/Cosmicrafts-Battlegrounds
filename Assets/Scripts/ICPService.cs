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
    
    // Settings
    [Header("ICP Settings")]
    [SerializeField] private string canisterId = "opcce-byaaa-aaaak-qcgda-cai";
    [SerializeField] private string seedPhrase = "coconut teach old consider vivid leader minute canoe original suspect skirt pause";
    
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
    
    private void Start()
    {
        Debug.Log("[ICPService] Starting initialization...");
        _ = InitializeWithSeedPhrase(seedPhrase);
    }
    
    /// <summary>
    /// Initialize ICP identity using a seed phrase
    /// </summary>
    public async Task InitializeWithSeedPhrase(string seedPhrase)
    {
        if (IsInitialized)
        {
            Log("Already initialized, skipping...");
            return;
        }
        
        Log($"Starting initialization with seed phrase...");
        
        try
        {
            // Create identity from seed phrase
            Ed25519Identity identity = await CreateIdentityFromSeedPhrase(seedPhrase);
            
            // Create HttpAgent with the identity
            var httpClient = new UnityHttpClient();
            var agent = new HttpAgent(httpClient, identity);
            
            // Store the principal ID
            PrincipalId = identity.GetPublicKey().ToPrincipal().ToText();
            Log($"Identity initialized successfully with principal: {PrincipalId}");
            
            // Initialize the BackendApiClient with the agent and canister ID
            Principal canisterPrincipal = Principal.FromText(canisterId);
            MainCanister = new BackendApiClient(agent, canisterPrincipal);
            
            IsInitialized = true;
            Log("Service fully initialized, fetching player data...");
            
            // Fetch player data after initialization
            await GetPlayerData();
            
            // Notify listeners that initialization is complete
            OnICPInitialized?.Invoke();
            Log("Initialization complete and events fired");
        }
        catch (Exception e)
        {
            LogError($"Initialization failed with error: {e.Message}\nStack trace: {e.StackTrace}");
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
            LogError("Cannot get player data: service not initialized");
            return null;
        }
        
        Log("Starting player data fetch...");
        
        try
        {
            var playerInfo = await MainCanister.GetPlayer();
            
            if (playerInfo.HasValue)
            {
                CurrentPlayer = playerInfo.ValueOrDefault;
                Log($"Player data fetched successfully: {CurrentPlayer.Username} (Level {CurrentPlayer.Level})");
                
                // Notify listeners
                OnPlayerDataReceived?.Invoke(CurrentPlayer);
                Log("Player data event fired to listeners");
                
                return CurrentPlayer;
            }
            else
            {
                Log("No player found, attempting to create new player...");
                
                // Handle the case where no player is found - prompt for signup
                await SignupNewPlayer();
                
                return CurrentPlayer;
            }
        }
        catch (Exception e)
        {
            LogError($"Error fetching player data: {e.Message}\nStack trace: {e.StackTrace}");
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
                LogError("Signup failed");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Error during signup: {e.Message}\nStack trace: {e.StackTrace}");
            return false;
        }
    }
    
    private byte[] MnemonicToSeed(string mnemonic)
    {
        // Simple implementation - in production, use a proper BIP39 implementation
        return Encoding.UTF8.GetBytes(mnemonic);
    }
    
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