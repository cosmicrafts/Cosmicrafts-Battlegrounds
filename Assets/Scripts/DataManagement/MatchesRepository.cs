using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;
using EdjCase.ICP.Candid.Models;

// Type aliases
using TokenId = EdjCase.ICP.Candid.Models.UnboundedUInt;
using UnboundedUInt = EdjCase.ICP.Candid.Models.UnboundedUInt;

/// <summary>
/// Repository for match history and match data
/// </summary>
public class MatchesRepository : BaseRepository
{
    // Match data
    public List<MatchDataInfo> MatchHistory { get; private set; } = new List<MatchDataInfo>();
    public OptionalValue<FullMatchData> CurrentMatch { get; private set; }
    public UnboundedUInt CurrentMatchID { get; private set; }
    
    // Events
    public event Action<List<MatchDataInfo>> OnMatchHistoryLoaded;
    public event Action<FullMatchData> OnCurrentMatchLoaded;
    public event Action<MatchDataInfo> OnMatchDetailsLoaded;
    
    /// <summary>
    /// Structure to store match data with stats
    /// </summary>
    public class MatchDataInfo
    {
        public UnboundedUInt MatchId { get; set; }
        public MatchData MatchData { get; set; }
        public BasicStats Stats { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public bool WasVictory { get; set; }
    }
    
    /// <summary>
    /// Load match data from blockchain
    /// </summary>
    public override async Task LoadAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            // Load current match if any
            await LoadCurrentMatch(canister);
            
            // Load match history
            await LoadMatchHistory(canister);
            
            NotifyDataLoaded();
            Log("Match data loaded successfully");
        }
        catch (Exception e)
        {
            LogError($"Error loading match data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh match data from blockchain
    /// </summary>
    public override async Task RefreshAsync(BackendApiClient canister)
    {
        await LoadAsync(canister);
        NotifyDataUpdated();
    }
    
    /// <summary>
    /// Load current match if player is in one
    /// </summary>
    private async Task LoadCurrentMatch(BackendApiClient canister)
    {
        try
        {
            var result = await canister.GetMyMatchData();
            CurrentMatch = result.ReturnArg0;
            CurrentMatchID = result.ReturnArg1;
            
            if (CurrentMatch.HasValue)
            {
                Log($"Player is currently in match ID: {CurrentMatchID}");
                OnCurrentMatchLoaded?.Invoke(CurrentMatch.ValueOrDefault);
            }
            else
            {
                Log("Player is not currently in a match");
            }
        }
        catch (Exception e)
        {
            LogError($"Error loading current match: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load match history
    /// </summary>
    private async Task LoadMatchHistory(BackendApiClient canister)
    {
        try
        {
            // Get player's principal
            var playerId = GameDataManager.Instance?.Player?.CurrentPlayer?.Id;
            if (playerId == null)
            {
                LogError("Cannot load match history: Player ID not available");
                return;
            }
            
            var history = await canister.GetMatchHistoryByPrincipal(playerId);
            MatchHistory.Clear();
            
            foreach (var item in history)
            {
                var match = new MatchDataInfo
                {
                    MatchId = item.F0
                };
                
                if (item.F1.HasValue)
                {
                    match.Stats = item.F1.ValueOrDefault;
                    
                    // Determine if this was a victory
                    match.WasVictory = match.Stats.PlayerStats != null && 
                                       match.Stats.PlayerStats.Count > 0 && 
                                       match.Stats.PlayerStats.Exists(ps => 
                                           ps.PlayerId.Equals(playerId) && ps.WonGame);
                }
                
                MatchHistory.Add(match);
                
                // Load match details in background
                _ = LoadMatchDetails(canister, match);
            }
            
            Log($"Loaded {MatchHistory.Count} matches from history");
            OnMatchHistoryLoaded?.Invoke(MatchHistory);
        }
        catch (Exception e)
        {
            LogError($"Error loading match history: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load details for a specific match
    /// </summary>
    private async Task LoadMatchDetails(BackendApiClient canister, MatchDataInfo match)
    {
        try
        {
            var details = await canister.GetMatchDetails(match.MatchId);
            if (details.HasValue)
            {
                var (matchData, playerStats) = details.ValueOrDefault;
                match.MatchData = matchData;
                
                // Extract player info
                foreach (var player in playerStats.Keys)
                {
                    match.Players.Add(player);
                }
                
                OnMatchDetailsLoaded?.Invoke(match);
            }
        }
        catch (Exception e)
        {
            LogError($"Error loading match details for match {match.MatchId}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Submit a feedback for a match
    /// </summary>
    public async Task<bool> SubmitFeedback(BackendApiClient canister, UnboundedUInt matchId, string feedback)
    {
        try
        {
            var result = await canister.SubmitFeedback(matchId, feedback);
            if (result)
            {
                Log($"Feedback submitted successfully for match {matchId}");
                return true;
            }
            else
            {
                LogError($"Error submitting feedback for match {matchId}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception submitting feedback: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Dispute a match result
    /// </summary>
    public async Task<bool> DisputeMatch(BackendApiClient canister, UnboundedUInt matchId, UnboundedUInt playerId, string reason)
    {
        try
        {
            var result = await canister.DisputeMatch(matchId, playerId, reason);
            if (result)
            {
                Log($"Match dispute submitted successfully for match {matchId}");
                return true;
            }
            else
            {
                LogError($"Error disputing match {matchId}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception disputing match: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Get a match by ID
    /// </summary>
    public MatchDataInfo GetMatchById(UnboundedUInt matchId)
    {
        return MatchHistory.Find(m => m.MatchId.Equals(matchId));
    }
    
    /// <summary>
    /// Clear all match data
    /// </summary>
    public override void Clear()
    {
        MatchHistory.Clear();
        CurrentMatch = default;
        CurrentMatchID = default;
        IsLoaded = false;
        Log("Match data cleared");
    }
} 