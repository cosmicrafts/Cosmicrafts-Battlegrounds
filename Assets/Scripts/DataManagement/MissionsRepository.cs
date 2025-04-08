using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;
using EdjCase.ICP.Candid.Models;

// Type aliases
using UnboundedUInt = EdjCase.ICP.Candid.Models.UnboundedUInt;

/// <summary>
/// Repository for missions data
/// </summary>
public class MissionsRepository : BaseRepository
{
    // Missions data
    public List<MissionsUser> UserMissions { get; private set; } = new List<MissionsUser>();
    public List<MissionsUser> GeneralMissions { get; private set; } = new List<MissionsUser>();
    
    // Events
    public event Action<List<MissionsUser>> OnUserMissionsLoaded;
    public event Action<List<MissionsUser>> OnGeneralMissionsLoaded;
    public event Action<MissionsUser> OnMissionUpdated;
    public event Action<MissionsUser> OnMissionCompleted;
    
    /// <summary>
    /// Load missions data from blockchain
    /// </summary>
    public override async Task LoadAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            // Load user missions
            await LoadUserMissions(canister);
            
            // Load general missions
            await LoadGeneralMissions(canister);
            
            NotifyDataLoaded();
            Log("Missions data loaded successfully");
        }
        catch (Exception e)
        {
            LogError($"Error loading missions data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh missions data from blockchain
    /// </summary>
    public override async Task RefreshAsync(BackendApiClient canister)
    {
        await LoadAsync(canister);
        NotifyDataUpdated();
    }
    
    /// <summary>
    /// Load user-specific missions
    /// </summary>
    private async Task LoadUserMissions(BackendApiClient canister)
    {
        try
        {
            var missionsResult = await canister.GetUserMissions();
            UserMissions.Clear();
            
            UserMissions.AddRange(missionsResult);
            Log($"Loaded {UserMissions.Count} user missions");
            OnUserMissionsLoaded?.Invoke(UserMissions);
            
            // Check for completed missions
            CheckForCompletedMissions(UserMissions);
        }
        catch (Exception e)
        {
            LogError($"Error loading user missions: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load general missions
    /// </summary>
    private async Task LoadGeneralMissions(BackendApiClient canister)
    {
        try
        {
            var missionsResult = await canister.GetGeneralMissions();
            GeneralMissions.Clear();
            
            GeneralMissions.AddRange(missionsResult);
            Log($"Loaded {GeneralMissions.Count} general missions");
            OnGeneralMissionsLoaded?.Invoke(GeneralMissions);
            
            // Check for completed missions
            CheckForCompletedMissions(GeneralMissions);
        }
        catch (Exception e)
        {
            LogError($"Error loading general missions: {e.Message}");
        }
    }
    
    /// <summary>
    /// Check for completed missions and fire events
    /// </summary>
    private void CheckForCompletedMissions(List<MissionsUser> missions)
    {
        foreach (var mission in missions)
        {
            if (mission.Progress == mission.Total && !mission.Finished)
            {
                OnMissionCompleted?.Invoke(mission);
            }
        }
    }
    
    /// <summary>
    /// Claim a mission reward
    /// </summary>
    public async Task<bool> ClaimMissionReward(BackendApiClient canister, UnboundedUInt missionId, bool isUserMission)
    {
        try
        {
            bool success;
            string message;
            
            if (isUserMission)
            {
                var result = await canister.ClaimUserReward(missionId);
                success = result.ReturnArg0;
                message = result.ReturnArg1;
            }
            else
            {
                var result = await canister.ClaimGeneralReward(missionId);
                success = result.ReturnArg0;
                message = result.ReturnArg1;
            }
            
            if (success)
            {
                Log($"Mission reward claimed successfully: {message}");
                
                // Update local mission data
                var missions = isUserMission ? UserMissions : GeneralMissions;
                var mission = missions.Find(m => m.IdMission.Equals(missionId));
                if (mission != null)
                {
                    mission.Finished = true;
                    OnMissionUpdated?.Invoke(mission);
                }
                
                return true;
            }
            else
            {
                LogError($"Error claiming mission reward: {message}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception claiming mission reward: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Get a mission by ID
    /// </summary>
    public MissionsUser GetMissionById(UnboundedUInt missionId, bool isUserMission)
    {
        var missions = isUserMission ? UserMissions : GeneralMissions;
        return missions.Find(m => m.IdMission.Equals(missionId));
    }
    
    /// <summary>
    /// Clear all missions data
    /// </summary>
    public override void Clear()
    {
        UserMissions.Clear();
        GeneralMissions.Clear();
        IsLoaded = false;
        Log("Missions data cleared");
    }
} 