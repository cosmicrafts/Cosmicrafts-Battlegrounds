namespace Cosmicrafts {
    
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Spell 4 - Missile Strike
 * Launches a missile that follows a trajectory towards the target and explodes on impact
 */
public class Spell_04 : Spell
{
    [Header("Missile Settings")]
    [Tooltip("The missile prefab to spawn")]
    public GameObject missilePrefab;
    [Tooltip("Base damage of the explosion")]
    public int explosionDamage = 200;
    [Tooltip("Speed of the missile")]
    public float missileSpeed = 15f;
    
    // Runtime variables
    private Unit _mainStationUnit;
    private Unit _targetUnit;
    
    protected override void Start()
    {
        base.Start();
        
        // Find the MainStation for the appropriate team
        var (mainStation, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
        _mainStationUnit = mainStationUnit;
        
        if (_mainStationUnit == null)
        {
            Destroy(gameObject);
            return;
        }
        
        // Find the nearest enemy
        _targetUnit = Shooter.FindNearestEnemyFromPoint(_mainStationUnit.transform.position, MyTeam, 50f);
        
        // Launch the missile regardless of target
        LaunchMissile();
    }
    
    private void LaunchMissile()
    {
        if (missilePrefab == null) return;
        
        // Create the missile at the main station's position
        Vector3 spawnPosition = _mainStationUnit.transform.position;
        if (_mainStationUnit.GetComponent<Shooter>()?.powerUpOrigin != null)
        {
            spawnPosition = _mainStationUnit.GetComponent<Shooter>().powerUpOrigin.position;
        }
        
        GameObject missileObj = Instantiate(missilePrefab, spawnPosition, Quaternion.identity);
        Projectile missile = missileObj.GetComponent<Projectile>();
        
        if (missile != null)
        {
            // Set up the missile properties
            missile.MyTeam = MyTeam;
            missile.Speed = missileSpeed;
            missile.Dmg = explosionDamage;
            
            if (_targetUnit != null)
            {
                // If we have a target, set it
                missile.SetTarget(_targetUnit.gameObject);
            }
            else
            {
                // If no target, shoot in the unit's forward direction
                Vector3 targetPosition = _mainStationUnit.transform.position + (_mainStationUnit.transform.forward * 100f);
                
                // Create a temporary target at the target position
                GameObject tempTarget = new GameObject("TempMissileTarget");
                tempTarget.transform.position = targetPosition;
                
                // Set the temporary target and then destroy it after a frame
                missile.SetTarget(tempTarget);
                Destroy(tempTarget, 0.1f);
            }
        }
        
        // Destroy the spell object after launching
        Destroy(gameObject);
    }
}
} 