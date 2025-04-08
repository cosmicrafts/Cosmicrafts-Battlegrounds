using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ICPTestPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button togglePanelButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button getPlayerButton;
    [SerializeField] private Button registerPlayerButton;
    [SerializeField] private Button mintDeckButton;
    
    private ICPLoginTest loginTest;
    
    void Start()
    {
        // Find or add ICPLoginTest component
        loginTest = GetComponent<ICPLoginTest>();
        if (loginTest == null)
        {
            loginTest = gameObject.AddComponent<ICPLoginTest>();
        }
        
        // Set up the login test references
        if (loginTest != null)
        {
            // Use reflection to set the serialized fields
            System.Type type = loginTest.GetType();
            System.Reflection.FieldInfo statusTextField = type.GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo getPlayerButtonField = type.GetField("getPlayerButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo registerPlayerButtonField = type.GetField("registerPlayerButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo usernameInputField = type.GetField("usernameInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (statusTextField != null) statusTextField.SetValue(loginTest, statusText);
            if (getPlayerButtonField != null) getPlayerButtonField.SetValue(loginTest, getPlayerButton);
            if (registerPlayerButtonField != null) registerPlayerButtonField.SetValue(loginTest, registerPlayerButton);
            if (usernameInputField != null) usernameInputField.SetValue(loginTest, usernameInput);
        }
        
        // Add toggle panel button listener
        if (togglePanelButton != null && panelRoot != null)
        {
            togglePanelButton.onClick.AddListener(() => {
                panelRoot.SetActive(!panelRoot.activeSelf);
            });
        }
        
        // Add mint deck button listener
        if (mintDeckButton != null)
        {
            mintDeckButton.onClick.AddListener(MintDeckButtonClicked);
        }
    }
    
    private async void MintDeckButtonClicked()
    {
        if (ICPManager.Instance == null || !ICPManager.Instance.IsInitialized)
        {
            Debug.LogError("ICPManager not initialized");
            return;
        }
        
        statusText.text = "Minting starter deck...";
        
        try
        {
            bool result = await ICPManager.Instance.MintDeck();
            statusText.text = result ? "Deck minted successfully!" : "Failed to mint deck.";
        }
        catch (System.Exception e)
        {
            statusText.text = $"Error minting deck: {e.Message}";
            Debug.LogError($"Mint deck error: {e}");
        }
    }
    
    private void OnDestroy()
    {
        // Clean up button listeners
        if (togglePanelButton != null)
        {
            togglePanelButton.onClick.RemoveAllListeners();
        }
        
        if (mintDeckButton != null)
        {
            mintDeckButton.onClick.RemoveAllListeners();
        }
    }
}
