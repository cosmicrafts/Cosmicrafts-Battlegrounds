namespace Cosmicrafts {
    
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Spell 01 - Laser Beam
 * 
 * This spell demonstrates how to create a beam weapon that:
 * 1. Finds the player's MainStation using SpellUtils.FindPlayerMainStation
 * 2. Creates a visible beam using SpellUtils rendering utilities
 * 3. Detects and damages enemy units in the beam path
 * 
 * Use this as a template for creating other beam-like spells.
 */
public class Spell_01 : Spell
{
    [Header("Laser Configuration")]
    public int damagePerSecond = 250;
    public float damageInterval = 0.25f;
    public float beamLength = 100f;
    public float beamWidth = 8f;  // This controls both visual width and collision detection width
    public TypeDmg damageType = TypeDmg.Shield;
    
    [Header("Detection Settings")]
    [Tooltip("Multiplier applied to beam width for hit detection. Increase this if beam misses targets it should hit.")]
    public float hitDetectionWidthMultiplier = 1.5f;  // Makes hit detection wider than visual beam
    
    [Header("Critical Hit Settings")]
    [Range(0f, 1f)] public float criticalStrikeChance = 0f;
    public float criticalStrikeMultiplier = 2.0f;
    
    [Header("Skill Modifiers")]
    public float ShieldDamageMultiplier = 1.0f;
    
    [Header("Visual Components")]
    public LineRenderer laserLineRenderer;
    public GameObject laserStartVFX;
    public GameObject laserEndVFX;
    public Material laserMaterial;
    public Color laserColor = Color.red;
    public float laserIntensity = 5.0f;
    [Tooltip("Whether to create additional VFX where the beam penetrates through targets")]
    public bool createPenetrationEffects = true;
    
    // Tracking references
    private Unit _mainStationUnit;
    private List<Unit> _targetsInBeam = new List<Unit>();
    private float _damageTimer;
    private Vector3[] _laserPositions = new Vector3[2];
    private int _baseDamagePerTick;
    
    // Cached layer mask for target detection
    private int _targetLayerMask;
    
    protected override void Start()
    {
        base.Start();
        
        // Initialize damage timer
        _damageTimer = damageInterval;
        
        // Calculate base damage per tick based on DPS
        _baseDamagePerTick = Mathf.RoundToInt(damagePerSecond * damageInterval);
        
        // Find the MainStation for the appropriate team using the utility
        var (mainStation, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
        _mainStationUnit = mainStationUnit;
        
        // Setup the layer mask for target detection
        _targetLayerMask = LayerMask.GetMask("Unit", "Default");
        
        // Initialize the line renderer using SpellUtils
        SetupLineRenderer();
        
        // Initial position update
        UpdateLaserBeam();
    }
    
    private void SetupLineRenderer()
    {
        // EXAMPLE: How to use SpellUtils to create a LineRenderer for a beam
        if (laserLineRenderer == null)
        {
            laserLineRenderer = SpellUtils.CreateBeamLineRenderer(gameObject, beamWidth * 0.5f, laserColor, laserIntensity);
        }
        
        // Create VFX objects if not assigned using SpellUtils
        if (laserStartVFX == null)
        {
            laserStartVFX = SpellUtils.CreateBeamEffect(
                transform.position,  // Initial position (will be updated later)
                beamWidth,           // Size
                laserColor,          // Color
                laserIntensity,      // Intensity
                transform            // Parent to this transform
            );
            laserStartVFX.name = "LaserStartVFX";
        }
        
        if (laserEndVFX == null)
        {
            laserEndVFX = SpellUtils.CreateBeamEffect(
                transform.position + Vector3.forward * 10f,  // Initial position (will be updated later)
                beamWidth * 0.8f,                           // Size
                laserColor,                                 // Color
                laserIntensity,                             // Intensity
                transform                                   // Parent to this transform
            );
            laserEndVFX.name = "LaserEndVFX";
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Check if MainStation is still valid
        if (_mainStationUnit == null || _mainStationUnit.GetIsDeath())
        {
            // If MainStation is destroyed, try to find it again using SpellUtils
            var (_, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
            _mainStationUnit = mainStationUnit;
        }
        
        // Update laser beam position based on MainStation
        UpdateLaserBeam();
        
        // Find targets in the beam path
        FindTargetsInBeam();
        
        // Apply damage on interval
        if (_damageTimer <= 0)
        {
            ApplyDamageToTargets();
            _damageTimer = damageInterval;
        }
        else
        {
            _damageTimer -= Time.deltaTime;
        }
    }
    
    private void UpdateLaserBeam()
    {
        Vector3 stationPosition;
        Quaternion stationRotation;
        
        // Get position from MainStation if available
        if (_mainStationUnit != null)
        {
            stationPosition = _mainStationUnit.transform.position;
            stationRotation = _mainStationUnit.transform.rotation;
        }
        else
        {
            // Use fallback position if no MainStation is found
            stationPosition = new Vector3(30, 0, 20);
            stationRotation = Quaternion.identity;
        }
        
        // Get direction toward enemy base
        Vector3 direction = SpellUtils.GetDirectionToEnemyBase(stationPosition, MyTeam);
        
        // If direction is roughly Vector3.forward (indicates no enemy found), use a more interesting direction
        if (Vector3.Distance(direction.normalized, Vector3.forward) < 0.1f)
        {
            // Use the rotation of the station + a time-based offset for movement
            float angle = Time.time * 20f; // Rotate 20 degrees per second
            direction = Quaternion.Euler(0, angle, 0) * stationRotation * Vector3.forward;
        }
        
        // Move this GameObject to follow the MainStation
        transform.position = stationPosition;
        transform.rotation = Quaternion.LookRotation(direction);
        
        // Calculate laser start position (at the station position)
        _laserPositions[0] = stationPosition + Vector3.up * 2.0f; // Increased height offset for better visibility
        
        // IMPORTANT CHANGE: Always use full beam length - don't stop at first hit
        _laserPositions[1] = _laserPositions[0] + (direction * beamLength);
        
        // Set positions directly on the line renderer
        if (laserLineRenderer != null)
        {
            // Force line renderer to use world space
            laserLineRenderer.useWorldSpace = true;
            
            // Update positions
            laserLineRenderer.SetPositions(_laserPositions);
            
            // Make the effect stronger by adding width variation
            laserLineRenderer.startWidth = beamWidth * 1.5f; // Increased width at start
            laserLineRenderer.endWidth = beamWidth * 0.5f;  // Narrower at end
            
            // Set glow parameter if using Standard Unlit shader
            if (laserLineRenderer.material.HasProperty("_Glow"))
                laserLineRenderer.material.SetFloat("_Glow", 1.5f);
        }
        else
        {
            SetupLineRenderer(); // Try to recreate it
        }
        
        // Position the VFX objects
        if (laserStartVFX != null)
        {
            laserStartVFX.transform.position = _laserPositions[0];
            laserStartVFX.transform.localScale = Vector3.one * beamWidth * 1.2f; // Scale up
            laserStartVFX.SetActive(true); // Ensure it's active
        }
        
        if (laserEndVFX != null)
        {
            laserEndVFX.transform.position = _laserPositions[1];
            laserEndVFX.transform.localScale = Vector3.one * beamWidth * 0.8f; // Scale up
            laserEndVFX.SetActive(true); // Ensure it's active
        }
    }
    
    private void FindTargetsInBeam()
    {
        // Calculate detection width (use multiplier for easier collision detection)
        float detectionWidth = beamWidth * hitDetectionWidthMultiplier;
        
        // Direct approach to find units - similar to Shooter.cs
        // Get all units in the scene - this is more expensive but more reliable for beam weapons
        Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        // Clear previous targets
        _targetsInBeam.Clear();
        
        foreach (Unit unit in allUnits)
        {
            // Skip units on our team, null, or dead units
            if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                continue;
                
            // Skip MainStation units (they're not damage targets)
            if (unit.GetComponent<MainStation>() != null)
                continue;
                
            // Check if unit is in beam path
            if (IsUnitInBeam(unit, _laserPositions[0], _laserPositions[1], detectionWidth))
            {
                _targetsInBeam.Add(unit);
                
                // Only create visual effects if we're close to damage application
                if (_damageTimer <= 0.05f && createPenetrationEffects)
                {
                    // Calculate beam direction
                    Vector3 beamDirection = (_laserPositions[1] - _laserPositions[0]).normalized;
                    
                    // Create hit effect at entry point (front of unit relative to beam)
                    Vector3 frontPos = FindBeamIntersectionPoint(unit, _laserPositions[0], beamDirection);
                    SpellUtils.CreateHitEffect(frontPos, beamWidth * 0.3f, Color.yellow, 0.3f);
                }
            }
        }
    }
    
    // More efficient unit detection for beam (similar to Shooter approach)
    private bool IsUnitInBeam(Unit unit, Vector3 beamStart, Vector3 beamEnd, float beamWidth)
    {
        Collider unitCollider = unit.GetComponent<Collider>();
        if (unitCollider == null) return false;
        
        // Check if collider bounds intersect with beam
        Vector3 center = unitCollider.bounds.center;
        Vector3 extents = unitCollider.bounds.extents;
        
        // Simple distance check from center to beam line
        Vector3 beamDir = (beamEnd - beamStart).normalized;
        Vector3 toCenter = center - beamStart;
        
        // Project toCenter onto beam direction
        float projLength = Vector3.Dot(toCenter, beamDir);
        
        // If projection is outside beam length, unit is not in beam
        if (projLength < 0 || projLength > Vector3.Distance(beamStart, beamEnd))
            return false;
            
        // Find closest point on beam to center
        Vector3 projectedPoint = beamStart + beamDir * projLength;
        
        // Check if distance from center to beam is less than beam width + unit extents
        float distance = Vector3.Distance(projectedPoint, center);
        
        // Use the largest horizontal extent for beam collision
        float horizontalExtent = Mathf.Max(extents.x, extents.z);
        
        // If distance is less than beam width + unit extent, unit is in beam
        return distance < (beamWidth / 2 + horizontalExtent);
    }
    
    private void ApplyDamageToTargets()
    {
        if (_targetsInBeam.Count == 0) return;
        
        // Calculate base damage with multipliers, similar to Shooter.cs
        int damage = CalculateDamage();
        
        foreach (Unit unit in _targetsInBeam)
        {
            if (unit != null && !unit.GetIsDeath())
            {
                // Apply damage directly with type - similar to Shooter.cs projectile implementation
                unit.AddDmg(damage, damageType);
                
                // Create penetration VFX to show beam passing through target
                if (createPenetrationEffects)
                {
                    // Calculate beam direction
                    Vector3 beamDirection = (_laserPositions[1] - _laserPositions[0]).normalized;
                    
                    // Create hit effect at entry point (front of unit relative to beam)
                    Vector3 frontPos = FindBeamIntersectionPoint(unit, _laserPositions[0], beamDirection);
                    SpellUtils.CreateHitEffect(frontPos, beamWidth * 0.3f, Color.yellow, 0.3f);
                    
                    // Create hit effect at exit point (back of unit relative to beam)
                    Vector3 backPos = FindBeamIntersectionPoint(unit, frontPos + beamDirection * 0.1f, beamDirection);
                    SpellUtils.CreateHitEffect(backPos, beamWidth * 0.2f, Color.red, 0.3f);
                }
            }
        }
    }
    
    // Calculate damage with shield multiplier and critical hit chance - similar to Shooter.cs
    private int CalculateDamage()
    {
        // Apply shield damage multiplier from character skills
        int baseDamageWithMultiplier = Mathf.RoundToInt(_baseDamagePerTick * ShieldDamageMultiplier);
        
        // Check for critical hit
        bool isCritical = Random.value < criticalStrikeChance;
        int finalDamage = isCritical ? 
            Mathf.RoundToInt(baseDamageWithMultiplier * criticalStrikeMultiplier) : 
            baseDamageWithMultiplier;
            
        return finalDamage;
    }
    
    // Calculate a point where the beam intersects with a unit's collider
    private Vector3 FindBeamIntersectionPoint(Unit unit, Vector3 startPos, Vector3 direction)
    {
        // Use the unit's collider if available
        Collider collider = unit.GetComponent<Collider>();
        if (collider != null)
        {
            RaycastHit hit;
            if (collider.Raycast(new Ray(startPos, direction), out hit, beamLength))
            {
                return hit.point;
            }
        }
        
        // Fallback to unit's position if no collider or no hit
        return unit.transform.position;
    }
    
    // Draw debug visualization in editor - simplified version
    private void OnDrawGizmos()
    {
        if (laserLineRenderer != null && laserLineRenderer.positionCount >= 2)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 start = laserLineRenderer.GetPosition(0);
            Vector3 end = laserLineRenderer.GetPosition(1);
            
            // Draw main beam line
            Gizmos.DrawLine(start, end);
        }
    }
}
}