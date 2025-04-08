using System;
using System.Threading.Tasks;
using UnityEngine;
using Cosmicrafts.backend.Models;

/// <summary>
/// Central data manager that coordinates all game data repositories
/// and provides a clean interface for accessing cached blockchain data.
/// </summary>
public class GameDataManager : MonoBehaviour
{
    // Singleton instance
    public static GameDataManager Instance { get; private set; }
    
    // Repositories
    public PlayerDataRepository Player { get; private set; }
    public NFTRepository NFTs { get; private set; }
    public MissionsRepository Missions { get; private set; }
    public MatchesRepository Matches { get; private set; }
    public FriendsRepository Friends { get; private set; }
    public AchievementsRepository Achievements { get; private set; }
    
    // Events
    public event Action OnDataInitialized;
    
    // Initialization status
    public bool IsInitialized { get; private set; }
    
    // References
    private ICPService icpService;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Create repositories
        Player = new PlayerDataRepository();
        NFTs = new NFTRepository();
        Missions = new MissionsRepository();
        Matches = new MatchesRepository();
        Friends = new FriendsRepository();
        Achievements = new AchievementsRepository();
        
        Log("Game Data Manager initialized");
    }
    
    private void Start()
    {
        // Get reference to ICPService
        icpService = ICPService.Instance;
        if (icpService == null)
        {
            LogError("ICPService not found. Unable to initialize data repositories.");
            return;
        }
        
        // Subscribe to ICP initialization
        icpService.OnICPInitialized += OnICPInitialized;
        
        // If already initialized, initialize data
        if (icpService.IsInitialized && icpService.CurrentPlayer != null)
        {
            InitializeData(icpService.CurrentPlayer);
        }
    }
    
    private void OnDestroy()
    {
        if (icpService != null)
        {
            icpService.OnICPInitialized -= OnICPInitialized;
        }
    }
    
    /// <summary>
    /// Called when ICP is initialized and ready for blockchain calls
    /// </summary>
    private void OnICPInitialized()
    {
        Log("ICP initialized, initializing data repositories");
        
        // If player data is already available, use it
        if (icpService.CurrentPlayer != null)
        {
            InitializeData(icpService.CurrentPlayer);
        }
        else
        {
            // Otherwise fetch it
            RefreshPlayerData();
        }
    }
    
    /// <summary>
    /// Initialize all repositories with player data
    /// </summary>
    private void InitializeData(Player player)
    {
        Player.Initialize(player);
        
        // Start loading other repositories
        _ = InitializeRepositoriesAsync();
    }
    
    /// <summary>
    /// Initialize repositories with async data loading
    /// </summary>
    private async Task InitializeRepositoriesAsync()
    {
        try
        {
            if (icpService.MainCanister == null)
            {
                LogError("Cannot initialize repositories: MainCanister is null");
                return;
            }
            
            Log("Loading all game data in parallel...");
            
            // Create tasks but don't wrap them in Task.Run to keep them on the main thread
            var nftTask = NFTs.LoadAsync(icpService.MainCanister);
            var missionsTask = Missions.LoadAsync(icpService.MainCanister);
            var matchesTask = Matches.LoadAsync(icpService.MainCanister);
            var friendsTask = Friends.LoadAsync(icpService.MainCanister);
            var achievementsTask = Achievements.LoadAsync(icpService.MainCanister);
            
            // Start all tasks in parallel
            await Task.WhenAll(nftTask, missionsTask, matchesTask, friendsTask, achievementsTask);
            
            IsInitialized = true;
            Log("All data repositories initialized");
            OnDataInitialized?.Invoke();
        }
        catch (Exception e)
        {
            LogError($"Error initializing repositories: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh core player data from blockchain
    /// </summary>
    public async Task RefreshPlayerData()
    {
        if (icpService == null || !icpService.IsInitialized)
        {
            LogError("Cannot refresh player data: ICPService not initialized");
            return;
        }
        
        try
        {
            Log("Refreshing player data from blockchain");
            var player = await icpService.GetPlayerData();
            
            if (player != null)
            {
                Player.Initialize(player);
                
                // If this is our first initialization, load other repositories
                if (!IsInitialized)
                {
                    _ = InitializeRepositoriesAsync();
                }
            }
            else
            {
                LogError("Failed to refresh player data: null response");
            }
        }
        catch (Exception e)
        {
            LogError($"Error refreshing player data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Refresh all repositories with latest blockchain data
    /// </summary>
    public async Task RefreshAllData()
    {
        await RefreshPlayerData();
        
        if (icpService.MainCanister == null)
        {
            LogError("Cannot refresh repositories: MainCanister is null");
            return;
        }
        
        Log("Refreshing all game data in parallel...");
        
        // Create tasks but don't wrap them in Task.Run to keep them on the main thread
        var nftTask = NFTs.RefreshAsync(icpService.MainCanister);
        var missionsTask = Missions.RefreshAsync(icpService.MainCanister);
        var matchesTask = Matches.RefreshAsync(icpService.MainCanister);
        var friendsTask = Friends.RefreshAsync(icpService.MainCanister);
        var achievementsTask = Achievements.RefreshAsync(icpService.MainCanister);
        
        // Start all tasks in parallel
        await Task.WhenAll(nftTask, missionsTask, matchesTask, friendsTask, achievementsTask);
        
        Log("All data refreshed");
    }
    
    // Logging helpers
    private void Log(string message) => Debug.Log($"[GameDataManager] {message}");
    private void LogWarning(string message) => Debug.LogWarning($"[GameDataManager] {message}");
    private void LogError(string message) => Debug.LogError($"[GameDataManager] {message}");
} 