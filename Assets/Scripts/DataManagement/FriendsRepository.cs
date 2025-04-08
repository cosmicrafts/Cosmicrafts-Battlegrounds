using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using Cosmicrafts.backend.Models;
using EdjCase.ICP.Candid.Models;

// Type aliases in case they're needed
using UnboundedUInt = EdjCase.ICP.Candid.Models.UnboundedUInt;

/// <summary>
/// Repository for friend-related data, including friend requests
/// </summary>
public class FriendsRepository : BaseRepository
{
    // Friends lists
    public List<Principal> Friends { get; private set; } = new List<Principal>();
    public List<FriendRequest> FriendRequests { get; private set; } = new List<FriendRequest>();
    public List<Principal> BlockedUsers { get; private set; } = new List<Principal>();
    
    // Friend info cache
    private Dictionary<Principal, Player> friendProfiles = new Dictionary<Principal, Player>();
    
    // Events
    public event Action<List<Principal>> OnFriendsListLoaded;
    public event Action<List<FriendRequest>> OnFriendRequestsLoaded;
    public event Action<List<Principal>> OnBlockedUsersLoaded;
    public event Action<Principal> OnFriendAdded;
    public event Action<Principal> OnFriendRemoved;
    public event Action<Principal> OnFriendRequestSent;
    public event Action<FriendRequest> OnFriendRequestReceived;
    
    /// <summary>
    /// Load friends data from blockchain
    /// </summary>
    public override async Task LoadAsync(BackendApiClient canister)
    {
        if (canister == null) throw new ArgumentNullException(nameof(canister));
        
        try
        {
            // Load friends list
            await LoadFriendsList(canister);
            
            // Load friend requests
            await LoadFriendRequests(canister);
            
            // Load blocked users
            await LoadBlockedUsers(canister);
            
            NotifyDataLoaded();
            Log("Friends data loaded successfully");
        }
        catch (Exception e)
        {
            LogError($"Error loading friends data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh friends data from blockchain
    /// </summary>
    public override async Task RefreshAsync(BackendApiClient canister)
    {
        await LoadAsync(canister);
        NotifyDataUpdated();
    }
    
    /// <summary>
    /// Load friends list
    /// </summary>
    private async Task LoadFriendsList(BackendApiClient canister)
    {
        try
        {
            var friendsResult = await canister.GetFriendsList();
            Friends.Clear();
            friendProfiles.Clear();
            
            if (friendsResult.HasValue)
            {
                Friends.AddRange(friendsResult.ValueOrDefault);
                Log($"Loaded {Friends.Count} friends");
                OnFriendsListLoaded?.Invoke(Friends);
                
                // Load friend profiles in background
                _ = LoadFriendProfiles(canister, Friends);
            }
            else
            {
                Log("No friends found");
            }
        }
        catch (Exception e)
        {
            LogError($"Error loading friends list: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load friend requests
    /// </summary>
    private async Task LoadFriendRequests(BackendApiClient canister)
    {
        try
        {
            var requestsResult = await canister.GetFriendRequests();
            FriendRequests.Clear();
            
            FriendRequests.AddRange(requestsResult);
            Log($"Loaded {FriendRequests.Count} friend requests");
            OnFriendRequestsLoaded?.Invoke(FriendRequests);
        }
        catch (Exception e)
        {
            LogError($"Error loading friend requests: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load blocked users
    /// </summary>
    private async Task LoadBlockedUsers(BackendApiClient canister)
    {
        try
        {
            var blockedResult = await canister.GetBlockedUsers();
            BlockedUsers.Clear();
            
            BlockedUsers.AddRange(blockedResult);
            Log($"Loaded {BlockedUsers.Count} blocked users");
            OnBlockedUsersLoaded?.Invoke(BlockedUsers);
        }
        catch (Exception e)
        {
            LogError($"Error loading blocked users: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load friend profiles in batches
    /// </summary>
    private async Task LoadFriendProfiles(BackendApiClient canister, List<Principal> friendIds)
    {
        const int batchSize = 5;
        List<Task<OptionalValue<Player>>> tasks = new List<Task<OptionalValue<Player>>>();
        
        for (int i = 0; i < friendIds.Count; i += batchSize)
        {
            tasks.Clear();
            
            // Create a batch of tasks
            for (int j = 0; j < batchSize && i + j < friendIds.Count; j++)
            {
                var friendId = friendIds[i + j];
                tasks.Add(canister.GetProfile(friendId));
            }
            
            // Wait for the batch to complete
            var results = await Task.WhenAll(tasks);
            
            // Process results
            for (int j = 0; j < results.Length; j++)
            {
                if (results[j].HasValue)
                {
                    var player = results[j].ValueOrDefault;
                    friendProfiles[player.Id] = player;
                }
            }
        }
        
        Log($"Loaded {friendProfiles.Count} friend profiles");
    }
    
    /// <summary>
    /// Send friend request
    /// </summary>
    public async Task<bool> SendFriendRequest(BackendApiClient canister, Principal playerId)
    {
        try
        {
            var result = await canister.SendFriendRequest(playerId);
            if (result.ReturnArg0)
            {
                Log($"Friend request sent successfully");
                OnFriendRequestSent?.Invoke(playerId);
                return true;
            }
            else
            {
                LogError($"Error sending friend request: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception sending friend request: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Accept friend request
    /// </summary>
    public async Task<bool> AcceptFriendRequest(BackendApiClient canister, Principal playerId)
    {
        try
        {
            var result = await canister.AcceptFriendRequest(playerId);
            if (result.ReturnArg0)
            {
                Log($"Friend request accepted successfully");
                
                // Update local state
                FriendRequests.RemoveAll(r => r.From.Equals(playerId));
                if (!Friends.Contains(playerId))
                {
                    Friends.Add(playerId);
                    OnFriendAdded?.Invoke(playerId);
                }
                
                return true;
            }
            else
            {
                LogError($"Error accepting friend request: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception accepting friend request: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Decline friend request
    /// </summary>
    public async Task<bool> DeclineFriendRequest(BackendApiClient canister, Principal playerId)
    {
        try
        {
            var result = await canister.DeclineFriendRequest(playerId);
            if (result.ReturnArg0)
            {
                Log($"Friend request declined successfully");
                
                // Update local state
                FriendRequests.RemoveAll(r => r.From.Equals(playerId));
                
                return true;
            }
            else
            {
                LogError($"Error declining friend request: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception declining friend request: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Block a user
    /// </summary>
    public async Task<bool> BlockUser(BackendApiClient canister, Principal playerId)
    {
        try
        {
            var result = await canister.BlockUser(playerId);
            if (result.ReturnArg0)
            {
                Log($"User blocked successfully");
                
                // Update local state
                if (!BlockedUsers.Contains(playerId))
                {
                    BlockedUsers.Add(playerId);
                }
                
                // Remove from friends if present
                if (Friends.Contains(playerId))
                {
                    Friends.Remove(playerId);
                    OnFriendRemoved?.Invoke(playerId);
                }
                
                return true;
            }
            else
            {
                LogError($"Error blocking user: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception blocking user: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Unblock a user
    /// </summary>
    public async Task<bool> UnblockUser(BackendApiClient canister, Principal playerId)
    {
        try
        {
            var result = await canister.UnblockUser(playerId);
            if (result.ReturnArg0)
            {
                Log($"User unblocked successfully");
                
                // Update local state
                BlockedUsers.Remove(playerId);
                
                return true;
            }
            else
            {
                LogError($"Error unblocking user: {result.ReturnArg1}");
                return false;
            }
        }
        catch (Exception e)
        {
            LogError($"Exception unblocking user: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Get cached friend profile
    /// </summary>
    public Player GetFriendProfile(Principal friendId)
    {
        if (friendProfiles.TryGetValue(friendId, out var profile))
        {
            return profile;
        }
        return null;
    }
    
    /// <summary>
    /// Clear all friends data
    /// </summary>
    public override void Clear()
    {
        Friends.Clear();
        FriendRequests.Clear();
        BlockedUsers.Clear();
        friendProfiles.Clear();
        IsLoaded = false;
        Log("Friends data cleared");
    }
} 