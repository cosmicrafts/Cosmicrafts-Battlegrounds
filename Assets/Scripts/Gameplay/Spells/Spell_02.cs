namespace Cosmicrafts {
    
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Spell 2 - Explosion
 * Creates an explosion at the target location, damaging all enemy units within radius
 */
public class Spell_02 : Spell
{
    // Explosion settings
    [Header("Explosion Settings")]
    [Tooltip("Radius of the explosion effect")]
    public float explosionRadius = 10f;
    [Tooltip("Base damage of the explosion")]
    public int explosionDamage = 200;
    [Tooltip("Type of damage the explosion deals")]
    public TypeDmg damageType = TypeDmg.Normal;
    [Tooltip("Visual effect for the explosion")]
    public ParticleSystem explosionEffect;
    [Tooltip("Audio effect for the explosion")]
    public AudioSource explosionSound;
    
    [Header("Auto-Targeting")]
    [Tooltip("Whether to automatically seek the best explosion position")]
    public bool useAutoTargeting = true;
    [Tooltip("Maximum range to search for targets")]
    public float maxTargetSearchRange = 50f;
    [Tooltip("Minimum number of enemies to consider a position optimal")]
    public int minEnemiesForOptimalTarget = 2;
    
    // Skill modifiers
    [Header("Skill Modifiers")]
    [Tooltip("Damage multiplier from character skills")]
    public float damageMultiplier = 1.0f;
    [Tooltip("Radius multiplier from character skills")]
    public float radiusMultiplier = 1.0f;
    [Tooltip("Critical hit chance")]
    [Range(0f, 1f)] public float criticalStrikeChance = 0f;
    [Tooltip("Critical hit damage multiplier")]
    public float criticalStrikeMultiplier = 2.0f;
    
    // Visualization
    [Header("Visual Settings")]
    [Tooltip("Debug sphere to show explosion radius")]
    public bool showRadiusGizmo = true;
    [Tooltip("Color of the radius gizmo")]
    public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);
    
    // Runtime variables
    private Unit _mainStationUnit;
    private Vector3 _optimalPosition;
    private bool _positionOptimized = false;
    
    protected override void Start()
    {
        base.Start();
        
        // Find the MainStation for the appropriate team
        var (mainStation, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
        _mainStationUnit = mainStationUnit;
        
        // Apply skill modifiers to the actual values
        float finalRadius = explosionRadius * radiusMultiplier;
        
        // If auto-targeting is enabled, find the optimal position before exploding
        if (useAutoTargeting && !_positionOptimized)
        {
            Vector3 bestPosition = FindOptimalExplosionPosition();
            if (bestPosition != Vector3.zero)
            {
                transform.position = bestPosition;
                _optimalPosition = bestPosition;
                _positionOptimized = true;
            }
        }
        
        // Create the explosion
        Explode(finalRadius);
        
        // Play effects
        if (explosionEffect != null)
        {
            // Scale the particle system to match the radius
            explosionEffect.transform.localScale = Vector3.one * finalRadius / 5f;
            explosionEffect.Play();
        }
        
        // Play sound
        if (explosionSound != null)
        {
            explosionSound.Play();
        }
        
        // Destroy after effects finish
        float particleDuration = explosionEffect != null ? explosionEffect.main.duration : 2f;
        Destroy(gameObject, particleDuration + 0.5f);
    }
    
    // Find the optimal position for the explosion to hit multiple enemies
    private Vector3 FindOptimalExplosionPosition()
    {
        if (_mainStationUnit == null) return Vector3.zero;
        
        // Get all enemy units in range
        List<Unit> enemiesInRange = FindEnemiesInRange(_mainStationUnit.transform.position, maxTargetSearchRange);
        
        // If not enough enemies found, use the nearest one
        if (enemiesInRange.Count < minEnemiesForOptimalTarget)
        {
            Unit nearestEnemy = Shooter.FindNearestEnemyFromPoint(_mainStationUnit.transform.position, MyTeam, maxTargetSearchRange);
            if (nearestEnemy != null)
            {
                return nearestEnemy.transform.position;
            }
            return transform.position; // Keep current position if no enemies found
        }
        
        // Find clusters of enemies to maximize explosion impact
        Vector3 bestPosition = Vector3.zero;
        int maxEnemiesHit = 0;
        
        // Try each enemy position as a center
        foreach (Unit potentialCenter in enemiesInRange)
        {
            Vector3 testPosition = potentialCenter.transform.position;
            int enemiesHit = CountEnemiesInRadius(testPosition, explosionRadius * radiusMultiplier, enemiesInRange);
            
            if (enemiesHit > maxEnemiesHit)
            {
                maxEnemiesHit = enemiesHit;
                bestPosition = testPosition;
            }
        }
        
        // If we found a good position, use it
        if (maxEnemiesHit >= minEnemiesForOptimalTarget)
        {
            Debug.Log($"Found optimal position hitting {maxEnemiesHit} enemies");
            return bestPosition;
        }
        
        // Fallback to nearest enemy
        Unit nearest = Shooter.FindNearestEnemyFromPoint(_mainStationUnit.transform.position, MyTeam, maxTargetSearchRange);
        if (nearest != null)
        {
            return nearest.transform.position;
        }
        
        return transform.position; // Keep current position if all else fails
    }
    
    // Find all enemy units within a specified range
    private List<Unit> FindEnemiesInRange(Vector3 center, float range)
    {
        List<Unit> enemies = new List<Unit>();
        Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        foreach (Unit unit in allUnits)
        {
            // Skip units on our team, null, or dead units
            if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                continue;
                
            // Skip main stations
            if (unit.GetComponent<MainStation>() != null)
                continue;
                
            float distance = Vector3.Distance(center, unit.transform.position);
            if (distance <= range)
            {
                enemies.Add(unit);
            }
        }
        
        return enemies;
    }
    
    // Count how many enemies would be hit by an explosion at the given position
    private int CountEnemiesInRadius(Vector3 center, float radius, List<Unit> enemies)
    {
        int count = 0;
        foreach (Unit unit in enemies)
        {
            if (unit == null || unit.GetIsDeath()) continue;
            
            float distance = Vector3.Distance(center, unit.transform.position);
            if (distance <= radius)
            {
                count++;
            }
        }
        return count;
    }
    
    private void Explode(float radius)
    {
        // Get all colliders in the explosion radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius);
        
        // List to keep track of already damaged units to avoid duplicates
        HashSet<int> damagedUnits = new HashSet<int>();
        
        int hitCount = 0;
        
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Unit"))
            {
                Unit unit = hitCollider.GetComponent<Unit>();
                
                // Only damage enemy units and avoid duplicates
                if (unit != null && !unit.GetIsDeath() && !unit.IsMyTeam(MyTeam) && !damagedUnits.Contains(unit.getId()))
                {
                    damagedUnits.Add(unit.getId());
                    
                    // Calculate damage with falloff based on distance from center
                    float distance = Vector3.Distance(transform.position, unit.transform.position);
                    float damageFalloff = 1f - Mathf.Clamp01(distance / radius);
                    
                    // Calculate final damage
                    int finalDamage = CalculateDamage(damageFalloff);
                    
                    // Apply damage
                    unit.AddDmg(finalDamage, damageType);
                    
                    // Visual feedback
                    // Debug.Log($"Explosion hit {unit.name} for {finalDamage} damage at distance {distance:F2}m");
                    
                    // Apply knockback effect (optional)
                    ApplyKnockback(unit, distance, radius);
                    
                    hitCount++;
                }
            }
        }
        
        // Debug.Log($"Explosion affected {hitCount} enemy units with radius {radius:F2}m");
    }
    
    // Calculate damage with critical hit chance and distance falloff
    private int CalculateDamage(float damageFalloff)
    {
        // Apply damage multiplier from character skills
        int baseDamageWithMultiplier = Mathf.RoundToInt(explosionDamage * damageMultiplier);
        
        // Apply distance falloff
        int damageWithFalloff = Mathf.RoundToInt(baseDamageWithMultiplier * damageFalloff);
        
        // Check for critical hit
        bool isCritical = Random.value < criticalStrikeChance;
        int finalDamage = isCritical ? 
            Mathf.RoundToInt(damageWithFalloff * criticalStrikeMultiplier) : 
            damageWithFalloff;
            
        return finalDamage;
    }
    
    // Optional - apply knockback force to units
    private void ApplyKnockback(Unit unit, float distance, float radius)
    {
        Rigidbody rb = unit.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Calculate direction away from explosion
            Vector3 direction = (unit.transform.position - transform.position).normalized;
            
            // Calculate force based on distance (stronger when closer)
            float knockbackForce = 10f * (1f - distance / radius);
            
            // Apply the force
            rb.AddForce(direction * knockbackForce, ForceMode.Impulse);
        }
    }
    
    // Draw the explosion radius in the editor
    private void OnDrawGizmos()
    {
        if (showRadiusGizmo)
        {
            Gizmos.color = gizmoColor;
            float radius = explosionRadius * radiusMultiplier;
            Gizmos.DrawSphere(transform.position, radius);
            
            // If auto-targeting and in play mode, show the targeting range from main station
            if (useAutoTargeting && Application.isPlaying && _mainStationUnit != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(_mainStationUnit.transform.position, maxTargetSearchRange);
                
                // Show optimal position if found
                if (_positionOptimized && _optimalPosition != Vector3.zero)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(_mainStationUnit.transform.position, _optimalPosition);
                    Gizmos.DrawWireCube(_optimalPosition, Vector3.one * 2f);
                }
            }
        }
    }
    
    // Method for updating skill modifiers from character skills
    public void UpdateSkillModifiers(float damageMulti, float radiusMulti, float critChance)
    {
        damageMultiplier = damageMulti;
        radiusMultiplier = radiusMulti;
        criticalStrikeChance = critChance;
        
        // Log the updated values
        // Debug.Log($"Explosion spell updated with: Damage Multi={damageMultiplier}, Radius Multi={radiusMultiplier}, Crit Chance={criticalStrikeChance}");
    }
}
} 