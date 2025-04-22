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
    
    // Position tracking
    private Unit _playerUnit;
    private Vector3 followOffset = new Vector3(0, 0, 0);
    
    protected override void Start()
    {
        base.Start();
        
        // Apply skill modifiers to the actual values
        float finalRadius = explosionRadius * radiusMultiplier;
        
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
                if (unit != null && !unit.GetIsDeath() && unit.MyFaction != MyFaction && !damagedUnits.Contains(unit.getId()))
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
            Gizmos.DrawSphere(transform.position, explosionRadius * radiusMultiplier);
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

    private void UpdatePositionToPlayer()
    {
        // Use the SpellUtils method to find the player character
        _playerUnit = SpellUtils.FindPlayerCharacter(MyFaction);
        
        // Update position only if player unit found
        if (_playerUnit != null)
        {
            transform.position = _playerUnit.transform.position + followOffset;
        }
        else
        {
            // If no player found through FindPlayerCharacter, fallback to base station
            Team playerTeam = FactionManager.ConvertFactionToTeam(MyFaction);
            int baseIndex = playerTeam == Team.Blue ? 1 : 0;
            if (GameMng.GM != null && GameMng.GM.Targets.Length > baseIndex)
            {
                transform.position = GameMng.GM.Targets[baseIndex].transform.position + followOffset;
            }
        }
    }
}
} 