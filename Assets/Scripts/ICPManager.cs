using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

// Correct namespaces based on current project structure
using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;

// Project-specific namespaces
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;

public class ICPManager : MonoBehaviour
{
    // Singleton instance
    public static ICPManager Instance { get; private set; }
    
    // Reference to the authentication manager
    private AuthenticationManager authManager;
    
    // BackendApiClient for canister interactions
    public BackendApiClient MainCanister { get; private set; }
    
    // Status properties
    public bool IsInitialized { get; private set; }
    public bool IsAuthenticated => MainCanister != null;
    public string PrincipalId { get; private set; }
    public string EnvironmentMode { get; private set; }

    // Event for when initialization completes
    public event Action OnICPInitialized;
    
    // Development mode settings - for editor testing without Vue frontend
    [Header("Development Settings")]
    [SerializeField] private bool useDevelopmentMode = true;
    [SerializeField] private string devPublicKey = "e0ea0a893ac05bc27b3d914f5c3d7de2f366891dcd8d273dca913b614aa7e407";
    [SerializeField] private string devSeedPhrase = "coconut teach old consider vivid leader minute canoe original suspect skirt pause";
    [SerializeField] private string devExpectedPrincipalId = "bhd6d-elzi6-6nug4-bmohh-pj4re-ndd44-63xoc-plrye-iurpi-7igjm-nqe";
    
    private void Awake()
    {
        // Ensure singleton behavior
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Set environment mode
        #if UNITY_EDITOR
            EnvironmentMode = "EDITOR";
        #elif UNITY_WEBGL
            EnvironmentMode = "WEBGL";
        #else
            EnvironmentMode = "OTHER";
        #endif
        
        Debug.Log($"ICPManager initialized in {EnvironmentMode} mode");
    }
    
    private async void Start()
    {
        // Get reference to AuthenticationManager
        authManager = AuthenticationManager.Instance;
        if (authManager == null)
        {
            Debug.LogError("AuthenticationManager not found. ICP interactions will not be available.");
            return;
        }
        
        // Subscribe to authentication events
        authManager.OnICPIdentityReceived += OnICPIdentityReceived;
        
        // Initialize if identity is already available
        if (authManager.ICPIdentity != null && !string.IsNullOrEmpty(authManager.ICPIdentity.principalId))
        {
            Debug.Log($"[{EnvironmentMode}] Using identity from AuthenticationManager");
            await InitializeICPWithIdentity(authManager.ICPIdentity);
        }
        #if UNITY_EDITOR
        else if (useDevelopmentMode)
        {
            // Use development mode in editor for testing without Vue
            Debug.Log($"[{EnvironmentMode}] Using development mode with hardcoded keys");
            await InitializeWithDevKeys();
        }
        #endif
    }
    
    #if UNITY_EDITOR
    // Initialize using hardcoded development keys for editor testing
    private async Task InitializeWithDevKeys()
    {
        Debug.Log($"[{EnvironmentMode}] Initializing with development keys");
        
        var identityData = new ICPIdentityData
        {
            principalId = devExpectedPrincipalId,
            publicKey = devPublicKey,
            seedPhrase = devSeedPhrase,
            derivationPath = "m/44'/223'/0'/0/0"
        };
        
        await InitializeICPWithIdentity(identityData);
    }
    #endif
    
    private void OnDestroy()
    {
        if (authManager != null)
        {
            authManager.OnICPIdentityReceived -= OnICPIdentityReceived;
        }
    }
    
    private async void OnICPIdentityReceived(ICPIdentityData identityData)
    {
        Debug.Log($"[{EnvironmentMode}] ICPManager received identity data");
        await InitializeICPWithIdentity(identityData);
    }
    
    private async Task InitializeICPWithIdentity(ICPIdentityData identityData)
    {
        Debug.Log($"[{EnvironmentMode}] Initializing ICP with identity: {identityData.principalId}");
        
        try
        {
            // Use keys directly from authentication manager when available
            Ed25519Identity identity;
            
            if (!string.IsNullOrEmpty(identityData.publicKey) && !string.IsNullOrEmpty(identityData.seedPhrase))
            {
                // If we have the public key and seed phrase from Vue, use them directly
                Debug.Log($"[{EnvironmentMode}] Using identity keys provided by Vue frontend");
                
                // Convert the public key from hex to bytes
                byte[] publicKeyBytes = HexStringToByteArray(identityData.publicKey);
                
                // BIP39 derivation matching Vue.js implementation
                byte[] seedBytes = MnemonicToSeed(identityData.seedPhrase);
                
                // Take the first 32 bytes as Vue.js does with .slice(0, 32)
                byte[] privateKeyBytes = new byte[32];
                Array.Copy(seedBytes, 0, privateKeyBytes, 0, 32);
                
                // Log some diagnostic info about the keys (excluding sensitive info)
                Debug.Log($"[{EnvironmentMode}] Public key length: {publicKeyBytes.Length}, Private key first 4 bytes: {BitConverter.ToString(privateKeyBytes, 0, 4)}");
                
                // Create the Ed25519Identity directly using the BIP39 derived seed
                var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);
                identity = new Ed25519Identity(publicKeyBytes, privateKey.GetEncoded());
                
                // Compare with expected principal ID from Vue
                string calculatedPrincipal = identity.GetPublicKey().ToPrincipal().ToText();
                Debug.Log($"[{EnvironmentMode}] Principal ID comparison:");
                Debug.Log($"[{EnvironmentMode}] - Calculated: {calculatedPrincipal}");
                Debug.Log($"[{EnvironmentMode}] - Expected  : {identityData.principalId}");
                Debug.Log($"[{EnvironmentMode}] - Match     : {calculatedPrincipal == identityData.principalId}");
            }
            else
            {
                // Fallback to seed phrase only if that's all we have
                Debug.Log($"[{EnvironmentMode}] Using seed phrase to generate identity");
                identity = await CreateIdentityFromSeedPhrase(identityData.seedPhrase);
            }
            
            // Create HttpAgent with the identity and UnityHttpClient
            var httpClient = new UnityHttpClient();
            var agent = new HttpAgent(httpClient, identity);
            // FetchRootKey property doesn't exist in this version of HttpAgent
            // For development environments, you may need to handle root keys differently
            
            // Get the principal ID using the correct method
            PrincipalId = identity.GetPublicKey().ToPrincipal().ToText();
            Debug.Log($"[{EnvironmentMode}] ICP identity initialized with principal: {PrincipalId}");
            
            // Initialize the BackendApiClient with the agent and canister ID
            Principal canisterId = Principal.FromText("opcce-byaaa-aaaak-qcgda-cai"); // Replace with your actual canister ID
            MainCanister = new BackendApiClient(agent, canisterId);
            
            IsInitialized = true;
            OnICPInitialized?.Invoke();
            
            Debug.Log($"[{EnvironmentMode}] ICP initialization complete. Ready for blockchain interactions.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{EnvironmentMode}] Error initializing ICP identity: {e.Message}");
            IsInitialized = false;
        }
    }
    
    private async Task<Ed25519Identity> CreateIdentityFromSeedPhrase(string seedPhrase)
    {
        // BIP39 derivation matching Vue.js implementation
        byte[] seedBytes = MnemonicToSeed(seedPhrase);
        
        // Use first 32 bytes as Vue.js does with .slice(0, 32)
        byte[] privateKeyBytes = new byte[32];
        Array.Copy(seedBytes, 0, privateKeyBytes, 0, 32);
        
        // For testing, we'll simulate an async operation
        await Task.Delay(10);
        
        // Use the seed for Ed25519 key generation
        var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);
        
        // Derive the public key
        var publicKey = privateKey.GeneratePublicKey();
        
        // Create the identity
        return new Ed25519Identity(publicKey.GetEncoded(), privateKey.GetEncoded());
    }
    
    // BIP39 mnemonic to seed conversion (simplified implementation)
    private byte[] MnemonicToSeed(string mnemonic)
    {
        // This is a simplified PBKDF2 implementation of BIP39 seed derivation
        // In a production environment, consider using a proper BIP39 library
        
        string salt = "mnemonic" + "";  // BIP39 spec uses "mnemonic" + optional passphrase
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic.Normalize(NormalizationForm.FormKD));
        
        // PBKDF2 with HMAC-SHA512, 2048 iterations, 64 bytes output
        using (var deriveBytes = new System.Security.Cryptography.Rfc2898DeriveBytes(
            mnemonicBytes, saltBytes, 2048, System.Security.Cryptography.HashAlgorithmName.SHA512))
        {
            return deriveBytes.GetBytes(64);  // 512 bits = 64 bytes
        }
    }
    
    // Helper to convert hex string to byte array
    private byte[] HexStringToByteArray(string hex)
    {
        if (hex.Length % 2 == 1)
            throw new Exception("Hex string must have an even length");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }
    
    // Check if the player exists
    public async Task<bool> CheckPlayerExists()
    {
        try
        {
            Debug.Log("Checking if player exists...");
            
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("ICP not initialized. Please authenticate first.");
            }
            
            // Call GetPlayer on the canister
            var playerInfo = await MainCanister.GetPlayer();
            
            bool playerExists = playerInfo.HasValue;
            Debug.Log($"Player exists: {playerExists}");
            return playerExists;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error checking player: {e.Message}");
            return false;
        }
    }
    
    // Mint a starter deck
    public async Task<bool> MintDeck()
    {
        try
        {
            Debug.Log("Minting starter deck...");
            
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("ICP not initialized. Please authenticate first.");
            }
            
            // Call MintDeck on the canister
            var result = await MainCanister.MintDeck();
            
            bool success = result.ReturnArg0;
            Debug.Log($"Deck minting result: {success}");
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error minting deck: {e.Message}");
            return false;
        }
    }
    
    // Register a new player
    public async Task<bool> RegisterPlayer(string username, int avatarId = 1)
    {
        try
        {
            Debug.Log($"Registering player with username: {username}");
            
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("ICP not initialized. Please authenticate first.");
            }
            
            // Convert avatarId to UnboundedUInt - matching the signature in your Login class
            var avatarIDValue = UnboundedUInt.FromUInt64((ulong)avatarId);
            
            // TODO: Implement the actual call once RegisterPlayer is available in BackendApiClient
            // For now, this is a mock implementation
            Debug.LogWarning("RegisterPlayer method is not implemented in BackendApiClient");
            await Task.Delay(100); // Simulate network call
            
            // Mock success for testing
            bool success = true;
            Debug.Log($"Player registration result: {success}");
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error registering player: {e.Message}");
            return false;
        }
    }
    
    // Get player data
    public async Task<Cosmicrafts.backend.Models.Player> GetPlayerData()
    {
        try
        {
            Debug.Log("Getting player data...");
            
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("ICP not initialized. Please authenticate first.");
            }
            
            var playerInfo = await MainCanister.GetPlayer();
            
            if (playerInfo.HasValue)
            {
                var player = playerInfo.ValueOrDefault;
                Debug.Log($"Retrieved player data. Username: {player.Username}, Level: {player.Level}");
                return player;
            }
            else
            {
                Debug.Log("No player data available");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting player data: {e.Message}");
            return null;
        }
    }
    
    // Get player stats
    public async Task<Cosmicrafts.backend.Models.PlayerGamesStats> GetPlayerStats()
    {
        try
        {
            Debug.Log("Getting player stats...");
            
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("ICP not initialized. Please authenticate first.");
            }
            
            var statsInfo = await MainCanister.GetMyStats();
            
            if (statsInfo.HasValue)
            {
                var stats = statsInfo.ValueOrDefault;
                Debug.Log($"Retrieved player stats. Games won: {stats.GamesWon}, Games lost: {stats.GamesLost}");
                return stats;
            }
            else
            {
                Debug.Log("No player stats available");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting player stats: {e.Message}");
            return null;
        }
    }
} 