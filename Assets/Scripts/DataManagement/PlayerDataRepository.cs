using System;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;
using EdjCase.ICP.Candid.Models;

/// <summary>
/// Repository for player data and player-related statistics
/// </summary>
public class PlayerDataRepository : BaseRepository
{
    // Core player data
    public Player CurrentPlayer { get; private set; }
    
    // Player statistics
    public PlayerGamesStats Stats { get; private set; }
    public AverageStats AverageStats { get; private set; }
    
    // Player balance/resources
    public ulong Stardust { get; private set; }
    
    // Additional events
    public event Action<Player> OnPlayerUpdated;
    public event Action<PlayerGamesStats> OnStatsUpdated;
    
    /// <summary>
    /// Initialize with existing player data (typically from ICPService)
    /// </summary>
    public void Initialize(Player player)
    {
        CurrentPlayer = player;
        Log($"Player data initialized: {player.Username} (Level {player.Level})");
        NotifyDataLoaded();
        OnPlayerUpdated?.Invoke(player);
    }
    
    /// <summary>
    /// Load player data and related information
    /// </summary>
    public override async Task LoadAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            // Check if we already have player data
            if (CurrentPlayer == null)
            {
                var playerResult = await canister.GetPlayer();
                if (playerResult.HasValue)
                {
                    CurrentPlayer = playerResult.ValueOrDefault;
                    Log($"Player data loaded: {CurrentPlayer.Username}");
                }
                else
                {
                    LogWarning("No player data found on blockchain");
                    return;
                }
            }
            
            // Load stats
            await LoadPlayerStats(canister);
            
            // Load resources
            await LoadResources(canister);
            
            NotifyDataLoaded();
        }
        catch (Exception e)
        {
            LogError($"Error loading player data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh all player data
    /// </summary>
    public override async Task RefreshAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            // Refresh core player data
            var playerResult = await canister.GetPlayer();
            if (playerResult.HasValue)
            {
                CurrentPlayer = playerResult.ValueOrDefault;
                OnPlayerUpdated?.Invoke(CurrentPlayer);
                Log($"Player data refreshed: {CurrentPlayer.Username}");
            }
            
            // Refresh stats
            await LoadPlayerStats(canister);
            
            // Refresh resources
            await LoadResources(canister);
            
            NotifyDataUpdated();
        }
        catch (Exception e)
        {
            LogError($"Error refreshing player data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load player statistics
    /// </summary>
    private async Task LoadPlayerStats(BackendApiClient canister)
    {
        try
        {
            // Get player game stats
            var statsResult = await canister.GetMyStats();
            if (statsResult.HasValue)
            {
                Stats = statsResult.ValueOrDefault;
                Log($"Player stats loaded: Games Won: {Stats.GamesWon}, Games Lost: {Stats.GamesLost}");
                OnStatsUpdated?.Invoke(Stats);
            }
            
            // Get player average stats
            var principalId = CurrentPlayer.Id;
            var avgStatsResult = await canister.GetPlayerAverageStats(principalId);
            if (avgStatsResult.HasValue)
            {
                AverageStats = avgStatsResult.ValueOrDefault;
                Log("Player average stats loaded");
            }
        }
        catch (Exception e)
        {
            LogError($"Error loading player stats: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load player resources (stardust, etc.)
    /// </summary>
    private async Task LoadResources(BackendApiClient canister)
    {
        try
        {
            var principalId = CurrentPlayer.Id;
            var mintedInfo = await canister.GetMintedInfo(principalId);
            
            // Use the extension method to convert
            Stardust = mintedInfo.Stardust.ToUInt64();
            Log($"Player resources loaded: Stardust: {Stardust}");
        }
        catch (Exception e)
        {
            LogError($"Error loading player resources: {e.Message}");
        }
    }
    
    /// <summary>
    /// Update player username
    /// </summary>
    public async Task<bool> UpdateUsername(BackendApiClient canister, string newUsername)
    {
        try
        {
            var result = await canister.UpdateUsername(newUsername);
            if (result.ReturnArg0)
            {
                Log($"Username updated to: {newUsername}");
                await RefreshAsync(canister); // Refresh to get updated data
                return true;
            }
            else
            {
                LogError($"Error updating username: {result.ReturnArg2}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception updating username: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Update player description
    /// </summary>
    public async Task<bool> UpdateDescription(BackendApiClient canister, string newDescription)
    {
        try
        {
            var result = await canister.UpdateDescription(newDescription);
            if (result.ReturnArg0)
            {
                Log($"Description updated");
                await RefreshAsync(canister); // Refresh to get updated data
                return true;
            }
            else
            {
                LogError($"Error updating description: {result.ReturnArg2}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception updating description: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Clear all cached player data
    /// </summary>
    public override void Clear()
    {
        CurrentPlayer = null;
        Stats = null;
        AverageStats = null;
        Stardust = 0;
        IsLoaded = false;
        Log("Player data cleared");
    }
} 