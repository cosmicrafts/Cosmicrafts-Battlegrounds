using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;
using EdjCase.ICP.Candid.Models;

// Type aliases
using UnboundedUInt = EdjCase.ICP.Candid.Models.UnboundedUInt;

/// <summary>
/// Repository for achievements data
/// </summary>
public class AchievementsRepository : BaseRepository
{
    // Achievements data
    public List<AchievementCategory> Categories { get; private set; } = new List<AchievementCategory>();
    
    // Events
    public event Action<List<AchievementCategory>> OnAchievementsLoaded;
    public event Action<AchievementCategory> OnCategoryUpdated;
    
    /// <summary>
    /// Load achievements data from blockchain
    /// </summary>
    public override async Task LoadAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            var result = await canister.GetUserAchievementsStructureByCaller();
            Categories.Clear();
            
            Categories.AddRange(result);
            Log($"Loaded {Categories.Count} achievement categories");
            OnAchievementsLoaded?.Invoke(Categories);
            
            NotifyDataLoaded();
        }
        catch (Exception e)
        {
            LogError($"Error loading achievements data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh achievements data from blockchain
    /// </summary>
    public override async Task RefreshAsync(BackendApiClient canister)
    {
        await LoadAsync(canister);
        NotifyDataUpdated();
    }
    
    /// <summary>
    /// Claim an individual achievement reward
    /// </summary>
    public async Task<bool> ClaimIndividualAchievementReward(BackendApiClient canister, UnboundedUInt achievementId)
    {
        try
        {
            var result = await canister.ClaimIndividualAchievementReward(achievementId);
            if (result.ReturnArg0)
            {
                Log($"Achievement reward claimed successfully: {result.ReturnArg1}");
                return true;
            }
            else
            {
                LogError($"Error claiming achievement reward: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception claiming achievement reward: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Claim an achievement line reward
    /// </summary>
    public async Task<bool> ClaimAchievementLineReward(BackendApiClient canister, UnboundedUInt lineId)
    {
        try
        {
            var result = await canister.ClaimAchievementLineReward(lineId);
            if (result.ReturnArg0)
            {
                Log($"Achievement line reward claimed successfully: {result.ReturnArg1}");
                return true;
            }
            else
            {
                LogError($"Error claiming achievement line reward: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception claiming achievement line reward: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Claim a category achievement reward
    /// </summary>
    public async Task<bool> ClaimCategoryAchievementReward(BackendApiClient canister, UnboundedUInt categoryId)
    {
        try
        {
            var result = await canister.ClaimCategoryAchievementReward(categoryId);
            if (result.ReturnArg0)
            {
                Log($"Category achievement reward claimed successfully: {result.ReturnArg1}");
                return true;
            }
            else
            {
                LogError($"Error claiming category achievement reward: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception claiming category achievement reward: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Clear all achievements data
    /// </summary>
    public override void Clear()
    {
        Categories.Clear();
        IsLoaded = false;
        Log("Achievements data cleared");
    }
} 