using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class ICPLoginTest : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button getPlayerButton;
    [SerializeField] private Button registerPlayerButton;
    [SerializeField] private TMP_InputField usernameInput;

    private bool isProcessing = false;

    private void Start()
    {
        if (statusText != null) 
            statusText.text = "Waiting for ICP initialization...";

        if (getPlayerButton != null)
            getPlayerButton.onClick.AddListener(GetPlayerButtonClicked);
            
        if (registerPlayerButton != null)
            registerPlayerButton.onClick.AddListener(RegisterPlayerButtonClicked);

        // Subscribe to ICPManager initialization event
        if (ICPManager.Instance != null)
        {
            if (ICPManager.Instance.IsInitialized)
            {
                UpdateStatus($"ICP initialized with principal: {ICPManager.Instance.PrincipalId}");
            }
            else
            {
                ICPManager.Instance.OnICPInitialized += OnICPInitialized;
            }
        }
        else
        {
            UpdateStatus("Error: ICPManager not found!");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (ICPManager.Instance != null)
        {
            ICPManager.Instance.OnICPInitialized -= OnICPInitialized;
        }

        // Remove button listeners
        if (getPlayerButton != null)
            getPlayerButton.onClick.RemoveListener(GetPlayerButtonClicked);
            
        if (registerPlayerButton != null)
            registerPlayerButton.onClick.RemoveListener(RegisterPlayerButtonClicked);
    }

    private void OnICPInitialized()
    {
        UpdateStatus($"ICP initialized with principal: {ICPManager.Instance.PrincipalId}");
    }

    private void UpdateStatus(string message)
    {
        Debug.Log($"[ICPLoginTest] {message}");
        if (statusText != null)
            statusText.text = message;
    }

    private async void GetPlayerButtonClicked()
    {
        if (isProcessing || ICPManager.Instance == null || !ICPManager.Instance.IsInitialized)
        {
            UpdateStatus("Cannot check player: ICP not initialized or already processing");
            return;
        }

        isProcessing = true;
        UpdateStatus("Checking if player exists...");

        try
        {
            // Check if player exists
            bool playerExists = await ICPManager.Instance.CheckPlayerExists();
            
            if (playerExists)
            {
                var playerData = await ICPManager.Instance.GetPlayerData();
                UpdateStatus($"Player found! Username: {playerData.Username}, Level: {playerData.Level}");
                
                // Get stats too
                var stats = await ICPManager.Instance.GetPlayerStats();
                if (stats != null)
                {
                    UpdateStatus($"Player found! Username: {playerData.Username}, Level: {playerData.Level}\n" +
                                $"Stats: Games Won: {stats.GamesWon}, Games Lost: {stats.GamesLost}");
                }
            }
            else
            {
                UpdateStatus("No player found with current identity. Please register.");
            }
        }
        catch (System.Exception e)
        {
            UpdateStatus($"Error checking player: {e.Message}");
        }
        
        isProcessing = false;
    }
    
    private async void RegisterPlayerButtonClicked()
    {
        if (isProcessing || ICPManager.Instance == null || !ICPManager.Instance.IsInitialized)
        {
            UpdateStatus("Cannot register player: ICP not initialized or already processing");
            return;
        }
        
        string username = usernameInput != null ? usernameInput.text : "TestPlayer";
        if (string.IsNullOrEmpty(username))
        {
            UpdateStatus("Please enter a username");
            return;
        }

        isProcessing = true;
        UpdateStatus($"Registering player with username: {username}...");

        try
        {
            // First mint a deck
            UpdateStatus("Minting starter deck...");
            bool deckMinted = await ICPManager.Instance.MintDeck();
            
            if (deckMinted)
            {
                UpdateStatus("Deck minted successfully! Registering player...");
                
                // Register player
                bool registered = await ICPManager.Instance.RegisterPlayer(username);
                
                if (registered)
                {
                    UpdateStatus($"Player '{username}' registered successfully!");
                    
                    // Get the player data to confirm
                    var playerData = await ICPManager.Instance.GetPlayerData();
                    if (playerData != null)
                    {
                        UpdateStatus($"Registration confirmed! Username: {playerData.Username}, Level: {playerData.Level}");
                    }
                }
                else
                {
                    UpdateStatus("Failed to register player.");
                }
            }
            else
            {
                UpdateStatus("Failed to mint starter deck.");
            }
        }
        catch (System.Exception e)
        {
            UpdateStatus($"Error registering player: {e.Message}");
        }
        
        isProcessing = false;
    }
} 