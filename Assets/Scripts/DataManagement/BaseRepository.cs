using System;
using System.Threading.Tasks;
using Cosmicrafts.backend;
using UnityEngine;

/// <summary>
/// Base class for all data repositories that handle caching and refreshing data from the blockchain
/// </summary>
public abstract class BaseRepository
{
    // Events
    public event Action OnDataLoaded;
    public event Action OnDataUpdated;
    
    // Status
    public bool IsLoaded { get; protected set; }
    public DateTime LastUpdated { get; protected set; }
    
    /// <summary>
    /// Load data from blockchain canister
    /// </summary>
    public abstract Task LoadAsync(BackendApiClient canister);
    
    /// <summary>
    /// Refresh cached data with fresh blockchain data
    /// </summary>
    public abstract Task RefreshAsync(BackendApiClient canister);
    
    /// <summary>
    /// Clear cached data
    /// </summary>
    public abstract void Clear();
    
    /// <summary>
    /// Helper to notify subscribers that data has been loaded
    /// </summary>
    protected void NotifyDataLoaded()
    {
        IsLoaded = true;
        LastUpdated = DateTime.Now;
        OnDataLoaded?.Invoke();
    }
    
    /// <summary>
    /// Helper to notify subscribers that data has been updated
    /// </summary>
    protected void NotifyDataUpdated()
    {
        LastUpdated = DateTime.Now;
        OnDataUpdated?.Invoke();
    }
    
    // Logging helpers
    protected void Log(string message) => Debug.Log($"[{GetType().Name}] {message}");
    protected void LogWarning(string message) => Debug.LogWarning($"[{GetType().Name}] {message}");
    protected void LogError(string message) => Debug.LogError($"[{GetType().Name}] {message}");
} 