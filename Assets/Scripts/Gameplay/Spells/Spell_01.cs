namespace Cosmicrafts {
    
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Spell 01 - Laser Beam
 * 
 * A high-performance laser beam weapon that:
 * 1. Locks onto a target and tracks it
 * 2. Uses efficient enemy detection
 * 3. Applies continuous damage to all enemies in the beam path
 */
public class Spell_01 : Spell
{
    [Header("Laser Configuration")]
    [Tooltip("Damage dealt per second")]
    public int damagePerSecond = 250;
    [Tooltip("How often damage is applied")]
    public float damageInterval = 0.25f;
    [Tooltip("Maximum length of the beam")]
    public float beamLength = 100f;
    [Tooltip("Width of the beam for visuals and hit detection")]
    public float beamWidth = 8f;
    [Tooltip("Type of damage dealt by the laser")]
    public TypeDmg damageType = TypeDmg.Shield;
    
    [Header("Targeting Settings")]
    [Tooltip("Whether to automatically target enemies")]
    public bool useAutoTargeting = true;
    [Tooltip("Maximum range for targeting enemies")]
    public float maxTargetingRange = 50f;
    [Tooltip("Width multiplier for hit detection")]
    public float hitDetectionWidthMultiplier = 1.5f;
    
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
    
    // Private variables
    private Unit _mainStationUnit;
    private Unit _currentTarget;
    private HashSet<int> _damagedUnitsThisTick = new HashSet<int>();
    private float _damageTimer;
    private Vector3[] _laserPositions = new Vector3[2];
    private int _baseDamagePerTick;
    
    protected override void Start()
    {
        base.Start();
        
        _damageTimer = damageInterval;
        _baseDamagePerTick = Mathf.RoundToInt(damagePerSecond * damageInterval);
        
        var (_, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
        _mainStationUnit = mainStationUnit;
        
        SetupLineRenderer();
        
        if (useAutoTargeting)
        {
            FindBestTarget();
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Validate MainStation
        if (_mainStationUnit == null || _mainStationUnit.GetIsDeath())
        {
            var (_, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
            _mainStationUnit = mainStationUnit;
        }
        
        // Check target validity only if using auto-targeting
        if (useAutoTargeting && (_currentTarget == null || _currentTarget.GetIsDeath() || !IsEnemyInRange(_currentTarget)))
        {
            FindBestTarget();
        }
        
        // Update beam position and check for hits
        UpdateLaserBeam();
        FindTargetsInBeam();
        
        // Apply damage on interval
        if (_damageTimer <= 0)
        {
            ApplyDamageToTargets();
            _damageTimer = damageInterval;
            _damagedUnitsThisTick.Clear(); // Reset damaged units for next tick
        }
        else
        {
            _damageTimer -= Time.deltaTime;
        }
    }
    
    private void FindTargetsInBeam()
    {
        float detectionWidth = beamWidth * hitDetectionWidthMultiplier;
        
        // Use OverlapBox for more accurate hit detection
        Vector3 beamCenter = (_laserPositions[0] + _laserPositions[1]) * 0.5f;
        Vector3 beamDirection = (_laserPositions[1] - _laserPositions[0]);
        float beamLength = beamDirection.magnitude;
        beamDirection.Normalize();
        
        // Create a box that encompasses the entire beam
        Vector3 boxSize = new Vector3(detectionWidth, 2f, beamLength);
        Quaternion boxRotation = Quaternion.LookRotation(beamDirection);
        
        // Get all colliders in the beam area
        Collider[] hitColliders = Physics.OverlapBox(beamCenter, boxSize * 0.5f, boxRotation);
        
        foreach (Collider hitCollider in hitColliders)
        {
            Unit unit = hitCollider.GetComponent<Unit>();
            
            // Skip invalid units, dead units, or units on our team
            if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                continue;
                
            // Skip MainStation units
            if (unit.GetComponent<MainStation>() != null)
                continue;
                
            // Use precise beam intersection check
            if (IsUnitInBeam(unit, _laserPositions[0], _laserPositions[1], detectionWidth))
            {
                if (_damageTimer <= 0.05f && !_damagedUnitsThisTick.Contains(unit.getId()))
                {
                    _damagedUnitsThisTick.Add(unit.getId());
                    
                    if (createPenetrationEffects)
                    {
                        CreateBeamHitEffects(unit);
                    }
                }
            }
        }
    }
    
    private void ApplyDamageToTargets()
    {
        int damage = CalculateDamage();
        
        foreach (int unitId in _damagedUnitsThisTick)
        {
            Unit unit = FindUnitById(unitId);
            if (unit != null && !unit.GetIsDeath())
            {
                // Apply damage and track it in metrics
                unit.AddDmg(damage, damageType);
                
                // Add to game metrics if it's an enemy unit
                if (!unit.IsMyTeam(MyTeam))
                {
                    GameMng.MT?.AddDamage(damage);
                }
            }
        }
    }
    
    private Unit FindUnitById(int id)
    {
        Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in allUnits)
        {
            if (unit != null && unit.getId() == id)
                return unit;
        }
        return null;
    }
    
    private void CreateBeamHitEffects(Unit unit)
    {
        Vector3 beamDirection = (_laserPositions[1] - _laserPositions[0]).normalized;
        
        // Entry point effect
        Vector3 frontPos = FindBeamIntersectionPoint(unit, _laserPositions[0], beamDirection);
        SpellUtils.CreateHitEffect(frontPos, beamWidth * 0.3f, Color.yellow, 0.3f);
        
        // Exit point effect
        Vector3 backPos = FindBeamIntersectionPoint(unit, frontPos + beamDirection * 0.1f, beamDirection);
        SpellUtils.CreateHitEffect(backPos, beamWidth * 0.2f, Color.red, 0.3f);
    }
    
    private bool IsUnitInBeam(Unit unit, Vector3 beamStart, Vector3 beamEnd, float beamWidth)
    {
        if (unit == null) return false;
        
        Collider unitCollider = unit.GetComponent<Collider>();
        if (unitCollider == null) return false;
        
        // For isometric view, work in XZ plane
        Vector2 beamStart2D = new Vector2(beamStart.x, beamStart.z);
        Vector2 beamEnd2D = new Vector2(beamEnd.x, beamEnd.z);
        Vector2 unitPos2D = new Vector2(unit.transform.position.x, unit.transform.position.z);
        
        // Calculate beam direction in 2D
        Vector2 beamDir2D = (beamEnd2D - beamStart2D).normalized;
        Vector2 toUnit2D = unitPos2D - beamStart2D;
        
        // Project unit position onto beam line
        float projection = Vector2.Dot(toUnit2D, beamDir2D);
        
        // Check if the projection point is within beam length
        if (projection < 0 || projection > Vector2.Distance(beamStart2D, beamEnd2D))
            return false;
            
        // Find closest point on beam to unit in 2D
        Vector2 closestPoint2D = beamStart2D + beamDir2D * projection;
        
        // Calculate distance from unit to beam in 2D
        float distance = Vector2.Distance(unitPos2D, closestPoint2D);
        
        // Get the unit's bounds
        Bounds bounds = unitCollider.bounds;
        float unitRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        
        // Check if unit is within beam width plus unit radius
        return distance <= (beamWidth * 0.5f + unitRadius);
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
        
        // Get direction toward target or enemy base
        Vector3 direction;
        
        if (useAutoTargeting && _currentTarget != null)
        {
            // Direct the beam toward the current target
            direction = (_currentTarget.transform.position - stationPosition).normalized;
        }
        else
        {
            // Use default direction toward enemy base
            direction = SpellUtils.GetDirectionToEnemyBase(stationPosition, MyTeam);
            
            // If direction is roughly Vector3.forward (indicates no enemy found), use a more interesting direction
            if (Vector3.Distance(direction.normalized, Vector3.forward) < 0.1f)
            {
                // Use the rotation of the station + a time-based offset for movement
                float angle = Time.time * 20f; // Rotate 20 degrees per second
                direction = Quaternion.Euler(0, angle, 0) * stationRotation * Vector3.forward;
            }
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
    
    private void FindBestTarget()
    {
        if (_mainStationUnit == null) return;
        
        // Use the static utility method from Shooter to find the nearest enemy
        _currentTarget = Shooter.FindNearestEnemyFromPoint(_mainStationUnit.transform.position, MyTeam, maxTargetingRange);
    }
    
    private bool IsEnemyInRange(Unit enemy)
    {
        if (_mainStationUnit == null || enemy == null) return false;
        
        float distance = Vector3.Distance(_mainStationUnit.transform.position, enemy.transform.position);
        return distance <= maxTargetingRange;
    }
    
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
            
            // Draw targeting range if auto-targeting is enabled
            if (useAutoTargeting && Application.isPlaying)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                if (_mainStationUnit != null)
                {
                    Gizmos.DrawWireSphere(_mainStationUnit.transform.position, maxTargetingRange);
                }
            }
        }
    }
}
}