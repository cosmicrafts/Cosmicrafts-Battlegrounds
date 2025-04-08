using UnityEngine;
using System;
using System.Runtime.InteropServices;

[Serializable]
public class AuthData
{
    public bool authenticated;
    public bool registered;
    public KeyDetails keys;
    public string playerData;
}

[Serializable]
public class KeyDetails
{
    public string principalId;
    public string publicKey;
    public string seedPhrase;
    public DerivedAddress[] derivedAddresses;
}

[Serializable]
public class DerivedAddress
{
    public int index;
    public string principalId;
    public string publicKey;
    public string name;
}

[Serializable]
public class ICPIdentityData
{
    public string principalId;
    public string seedPhrase;
    public string publicKey;
    public string derivationPath;
}

public class AuthenticationManager : MonoBehaviour
{
    // Singleton instance
    public static AuthenticationManager Instance { get; private set; }

    // Auth data received from the web app
    public AuthData AuthData { get; private set; }
    
    // ICP identity for blockchain interactions
    public ICPIdentityData ICPIdentity { get; private set; }

    // JS functions defined in JavaScriptBridge.jslib
    [DllImport("__Internal")]
    private static extern void RequestAuthData();

    [DllImport("__Internal")]
    private static extern void RequestLogout();

    [DllImport("__Internal")]
    private static extern void SavePlayerData(string playerDataJson);

    // Events
    public event Action<AuthData> OnAuthDataReceived;
    public event Action<ICPIdentityData> OnICPIdentityReceived;
    public event Action OnLoggedOut;

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

        AuthData = new AuthData();
        ICPIdentity = new ICPIdentityData();
        
        Debug.Log("AuthenticationManager initialized");
    }

    private void Start()
    {
        // Request auth data when the game starts
        Debug.Log("Requesting auth data from web app");
        #if !UNITY_EDITOR && UNITY_WEBGL
            RequestAuthData();
        #endif
    }

    // Called by the web app with the authentication data
    public void ReceiveAuthData(string authDataJson)
    {
        Debug.Log($"Received auth data: {authDataJson}");
        
        try
        {
            AuthData = JsonUtility.FromJson<AuthData>(authDataJson);
            OnAuthDataReceived?.Invoke(AuthData);
            
            Debug.Log($"Auth data processed: Authenticated={AuthData.authenticated}, PrincipalId={AuthData.keys?.principalId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing auth data: {e.Message}");
        }
    }

    // Called from Vue to set the ICP identity data
    public void SetICPIdentity(string icpIdentityJson)
    {
        Debug.Log($"Received ICP identity data: {icpIdentityJson}");
        
        try
        {
            ICPIdentity = JsonUtility.FromJson<ICPIdentityData>(icpIdentityJson);
            OnICPIdentityReceived?.Invoke(ICPIdentity);
            
            Debug.Log($"ICP Identity set: Principal={ICPIdentity.principalId}, Path={ICPIdentity.derivationPath}");
            
            // Here you'd initialize your ICP interaction library
            InitializeICPInteraction(ICPIdentity);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing ICP identity data: {e.Message}");
        }
    }

    // Individual setters for auth data properties
    public void SetPrincipalId(string principalId)
    {
        Debug.Log($"Setting principal ID: {principalId}");
        if (AuthData.keys == null)
        {
            AuthData.keys = new KeyDetails();
        }
        AuthData.keys.principalId = principalId;
    }

    public void SetPlayerData(string playerDataJson)
    {
        Debug.Log($"Setting player data: {playerDataJson}");
        AuthData.playerData = playerDataJson;
    }

    // Initialize ICP interaction with the identity
    private void InitializeICPInteraction(ICPIdentityData identity)
    {
        // This is where you'd initialize your ICP interaction
        // For example, if you have a plugin for ICP interactions:
        
        // ICPManager.Initialize(identity.seedPhrase, identity.derivationPath);
        
        Debug.Log("Ready for ICP blockchain interaction");
        
        // You can now use this identity for:
        // - Querying balances
        // - Making transactions
        // - Interacting with canisters
        // - etc.
    }

    // Methods to interact with the web app
    public void LogoutFromGame()
    {
        Debug.Log("Requesting logout from web app");
        #if !UNITY_EDITOR && UNITY_WEBGL
            RequestLogout();
        #endif
        
        // Clear local auth data
        AuthData = new AuthData();
        ICPIdentity = new ICPIdentityData();
        OnLoggedOut?.Invoke();
    }

    public void SaveGameDataToWebApp(string playerDataJson)
    {
        Debug.Log($"Saving game data to web app: {playerDataJson}");
        #if !UNITY_EDITOR && UNITY_WEBGL
            SavePlayerData(playerDataJson);
        #endif
    }

    // Utility methods to check auth status
    public bool IsAuthenticated()
    {
        return AuthData.authenticated;
    }

    public bool IsRegistered()
    {
        return AuthData.registered;
    }

    public string GetPrincipalId()
    {
        return AuthData.keys?.principalId;
    }

    public string GetPublicKey()
    {
        return AuthData.keys?.publicKey;
    }
    
    public string GetSeedPhrase()
    {
        return AuthData.keys?.seedPhrase;
    }
    
    // Get the ICP identity for blockchain interactions
    public ICPIdentityData GetICPIdentity()
    {
        return ICPIdentity;
    }
} 