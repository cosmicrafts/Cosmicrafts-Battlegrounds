using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AuthenticationTest : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI identityText;

    private void Start()
    {
        // Subscribe to authentication events
        if (AuthenticationManager.Instance != null)
        {
            AuthenticationManager.Instance.OnAuthDataReceived += OnAuthDataReceived;
            AuthenticationManager.Instance.OnICPIdentityReceived += OnICPIdentityReceived;
            AuthenticationManager.Instance.OnLoggedOut += OnLoggedOut;
            
            // Update UI with current status
            UpdateStatusText();
            UpdateIdentityText();
        }
        else
        {
            Debug.LogError("AuthenticationManager not found in scene");
            if (statusText != null)
            {
                statusText.text = "Error: AuthenticationManager not found";
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events when destroyed
        if (AuthenticationManager.Instance != null)
        {
            AuthenticationManager.Instance.OnAuthDataReceived -= OnAuthDataReceived;
            AuthenticationManager.Instance.OnICPIdentityReceived -= OnICPIdentityReceived;
            AuthenticationManager.Instance.OnLoggedOut -= OnLoggedOut;
        }
    }

    private void OnAuthDataReceived(AuthData authData)
    {
        Debug.Log("Auth data received in test script");
        UpdateStatusText();
    }
    
    private void OnICPIdentityReceived(ICPIdentityData icpIdentity)
    {
        Debug.Log("ICP identity received in test script");
        UpdateIdentityText();
    }

    private void OnLoggedOut()
    {
        Debug.Log("Logged out in test script");
        UpdateStatusText();
        UpdateIdentityText();
    }

    private void UpdateStatusText()
    {
        if (statusText == null || AuthenticationManager.Instance == null)
            return;

        AuthData authData = AuthenticationManager.Instance.AuthData;
        
        string status = $"Authentication Status:\n";
        status += $"Authenticated: {authData.authenticated}\n";
        status += $"Registered: {authData.registered}\n";
        
        if (authData.keys != null)
        {
            status += $"Principal ID: {authData.keys.principalId}\n";
            status += $"Public Key: {TruncateString(authData.keys.publicKey, 20)}\n";
            
            if (authData.keys.derivedAddresses != null && authData.keys.derivedAddresses.Length > 0)
            {
                status += $"\nDerived Addresses:\n";
                foreach (var addr in authData.keys.derivedAddresses)
                {
                    status += $"- {addr.name} ({addr.index}): {TruncateString(addr.principalId, 15)}\n";
                }
            }
        }
        
        statusText.text = status;
    }
    
    private void UpdateIdentityText()
    {
        if (identityText == null || AuthenticationManager.Instance == null)
            return;
            
        ICPIdentityData identity = AuthenticationManager.Instance.ICPIdentity;
        
        string identityInfo = "ICP Identity:\n";
        if (!string.IsNullOrEmpty(identity.principalId))
        {
            identityInfo += $"Principal ID: {identity.principalId}\n";
            identityInfo += $"Public Key: {TruncateString(identity.publicKey, 20)}\n";
            identityInfo += $"Seed Phrase: {TruncateString(identity.seedPhrase, 30)}\n";
            identityInfo += $"Derivation Path: {identity.derivationPath}\n";
            identityInfo += "\nReady for ICP Interactions";
        }
        else
        {
            identityInfo += "No ICP identity data available";
        }
        
        identityText.text = identityInfo;
    }

    // Truncate long strings for display
    private string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Length <= maxLength ? input : input.Substring(0, maxLength) + "...";
    }

    // Button click event handler to request logout
    public void OnLogoutButtonClicked()
    {
        if (AuthenticationManager.Instance != null)
        {
            AuthenticationManager.Instance.LogoutFromGame();
        }
    }

    // Button click event handler to save test player data
    public void OnSaveDataButtonClicked()
    {
        if (AuthenticationManager.Instance != null)
        {
            // Example player data
            string testPlayerData = JsonUtility.ToJson(new TestPlayerData
            {
                level = 5,
                score = 2500,
                username = "TestPlayer"
            });
            
            AuthenticationManager.Instance.SaveGameDataToWebApp(testPlayerData);
        }
    }
    
    // Button click event handler to test ICP interaction
    public void OnTestICPInteractionClicked()
    {
        if (AuthenticationManager.Instance != null && 
            !string.IsNullOrEmpty(AuthenticationManager.Instance.ICPIdentity.principalId))
        {
            Debug.Log("Testing ICP interaction with identity: " + 
                      AuthenticationManager.Instance.ICPIdentity.principalId);
            
            // Here you would call your ICP interaction methods
            // Example: ICPManager.GetBalance(AuthenticationManager.Instance.ICPIdentity);
        }
        else
        {
            Debug.LogWarning("No ICP identity available for testing interactions");
        }
    }
}

[System.Serializable]
public class TestPlayerData
{
    public int level;
    public int score;
    public string username;
} 