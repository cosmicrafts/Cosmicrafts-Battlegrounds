using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Configuration class for Unity-specific settings and initialization
/// </summary>
public static class UnityConfig
{
    /// <summary>
    /// Initialize any required configurations before scene load
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Debug.Log("[UnityConfig] Initializing global configuration...");

        // Configure ICP.NET for WebGL
        #if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[UnityConfig] Setting up WebGL HTTP provider for ICP.NET");
        // ICP.NET already has the UnityHttpClient implementation in the Plugins folder
        // No need to set it manually
        #endif

        // Initialize other global settings here
        Application.lowMemory += OnLowMemory;
        
        Debug.Log("[UnityConfig] Global configuration initialized");
    }
    
    /// <summary>
    /// Handle low memory situations
    /// </summary>
    private static void OnLowMemory()
    {
        Debug.LogWarning("[UnityConfig] Low memory detected! Cleaning up resources...");
        System.GC.Collect();
        Resources.UnloadUnusedAssets();
    }
} 