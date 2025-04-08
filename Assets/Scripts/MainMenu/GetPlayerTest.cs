using UnityEngine;
using System.Threading.Tasks;

public class GetPlayerTest : MonoBehaviour
{
    private async void Start()
    {
        // Wait for ICPManager to initialize first
        await WaitForICPManagerInitialization();
        
        // First test GetPlayer
        await TestGetPlayer();
        
        // Then test MintDeck
        await TestMintDeck();
    }
    
    private async Task WaitForICPManagerInitialization()
    {
        Debug.Log("[GetPlayerTest] Waiting for ICPManager initialization...");
        
        int maxAttempts = 20;
        int attempt = 0;
        
        while (attempt < maxAttempts)
        {
            if (ICPManager.Instance != null && ICPManager.Instance.IsInitialized)
            {
                Debug.Log($"[GetPlayerTest] ICPManager is initialized with principal: {ICPManager.Instance.PrincipalId}");
                return;
            }
            
            attempt++;
            await Task.Delay(500); // Wait half a second before checking again
        }
        
        Debug.LogError("[GetPlayerTest] Timed out waiting for ICPManager initialization");
    }
    
    private async Task TestGetPlayer()
    {
        Debug.Log("[GetPlayerTest] Testing GetPlayer() call...");
        try
        {
            var playerInfo = await ICPManager.Instance.MainCanister.GetPlayer();
            
            if (playerInfo.HasValue)
            {
                var player = playerInfo.ValueOrDefault;
                Debug.Log($"[GetPlayerTest] PLAYER FOUND! ID: {player.Id}, Username: {player.Username}, Level: {player.Level}");
            }
            else
            {
                Debug.Log("[GetPlayerTest] No player found for current identity. You may need to register first.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GetPlayerTest] Error calling GetPlayer: {e.Message}");
            Debug.LogError($"[GetPlayerTest] Stack trace: {e.StackTrace}");
        }
    }
    
    private async Task TestMintDeck()
    {
        Debug.Log("[GetPlayerTest] Testing MintDeck() call...");
        try
        {
            var mintResult = await ICPManager.Instance.MainCanister.MintDeck();
            Debug.Log($"[GetPlayerTest] MINT DECK RESULT: Success={mintResult.ReturnArg0}, Message={mintResult.ReturnArg1}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GetPlayerTest] Error calling MintDeck: {e.Message}");
            Debug.LogError($"[GetPlayerTest] Stack trace: {e.StackTrace}");
        }
    }
} 