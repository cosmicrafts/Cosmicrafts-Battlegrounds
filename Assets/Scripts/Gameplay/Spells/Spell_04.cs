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
            Debug.LogError("[Spell_04] Failed to find main station unit!");
            Destroy(gameObject);
            return;
        }
        
        // Find the nearest enemy
        _targetUnit = Shooter.FindNearestEnemyFromPoint(_mainStationUnit.transform.position, MyTeam, 50f);
        
        if (_targetUnit != null)
        {
            LaunchMissile();
        }
        else
        {
            Destroy(gameObject);
        }
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
            missile.SetTarget(_targetUnit.gameObject);
            missile.Speed = missileSpeed;
            missile.Dmg = explosionDamage;
        }
        
        // Destroy the spell object after launching
        Destroy(gameObject);
    }
}
} 