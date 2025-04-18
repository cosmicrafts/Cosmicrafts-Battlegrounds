using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Cosmicrafts {
public class WaveController : MonoBehaviour
{
    public static WaveController instance;

    public GameObject[] waves;
    public GameObject[] BSwaves;
    private int actualWave = 0;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }  { instance = this; }
    }

    private void Start()
    {
        // Initial setup - disable the default enemy base and set up our wave system
        if (GameMng.GM.Targets.Length > 0 && GameMng.GM.Targets[0] != null)
        {
            GameMng.GM.Targets[0].gameObject.SetActive(false);
        }
        
        // Activate the first wave
        if (waves.Length > 0 && waves[actualWave] != null)
        {
            waves[actualWave].SetActive(true);
        }
        
        // Set up the enemy base for the first wave
        SetupEnemyBaseForCurrentWave();
    }

    // Setup the enemy base for the current wave
    private void SetupEnemyBaseForCurrentWave()
    {
        if (BSwaves.Length <= actualWave || BSwaves[actualWave] == null)
        {
            Debug.LogError($"Enemy base prefab for wave {actualWave} is missing!");
            return;
        }
        
        // Get the Unit component from the base
        Unit baseUnit = BSwaves[actualWave].GetComponent<Unit>();
        if (baseUnit == null)
        {
            Debug.LogError($"Enemy base for wave {actualWave} has no Unit component!");
            return;
        }
        
        // Add Bot component if not already present
        Bot botComponent = baseUnit.GetComponent<Bot>();
        if (botComponent == null)
        {
            botComponent = baseUnit.gameObject.AddComponent<Bot>();
            botComponent.botName = $"WaveBot_{actualWave}";
            botComponent.waveNumber = actualWave;
        }
        
        // Notify the bot that its wave is active
        botComponent.OnWaveActivated(actualWave);
        
        // Set it as the enemy base in GameMng
        GameMng.GM.Targets[0] = baseUnit;
        
        // Configure it as the enemy (Red team, Player ID 2)
        baseUnit.MyTeam = Team.Red;
        baseUnit.PlayerId = 2;
    }

    public void OnBaseDestroyed()
    {
        // Deactivate the current wave
        if (waves.Length > actualWave && waves[actualWave] != null)
        {
            waves[actualWave].SetActive(false);
        }
           
        // Move to the next wave
        actualWave += 1;
        
        // Check if there are more waves
        if (waves.Length > actualWave)
        {
            // Activate the next wave
            waves[actualWave].SetActive(true);
            
            // Setup the new enemy base
            SetupEnemyBaseForCurrentWave();
            
            // Find and destroy all player ships (respawn cleanup)
            Ship[] ships = FindObjectsByType<Ship>(FindObjectsSortMode.None);
            foreach(Ship ship in ships)
            { 
                if (ship.MyTeam == Team.Blue)
                {
                    // Don't destroy the player's character - only other ships
                    if (GameMng.P == null || GameMng.P.GetComponent<Ship>() != ship)
                    {
                        Destroy(ship.gameObject);
                        GameMng.GM.DeleteUnit(ship);
                    }
                }
            }
            
            // TODO: Call UI For Wave Complete
        }
        else
        {
            // End the game - player wins
            GameMng.GM.EndGame(Team.Blue);
        }
    }
}
}