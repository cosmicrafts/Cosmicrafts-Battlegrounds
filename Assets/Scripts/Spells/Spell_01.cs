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
    public float beamWidth = 2.5f;
    public TypeDmg damageType = TypeDmg.Shield;
    
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
        // EXAMPLE: How to use SpellUtils to find the player's MainStation
        var (mainStation, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
        _mainStationUnit = mainStationUnit;
        
        // Setup the layer mask for target detection
        _targetLayerMask = LayerMask.GetMask("Unit", "Default");
        
        // Initialize the line renderer using SpellUtils
        SetupLineRenderer();
        
        // Initial position update
        UpdateLaserBeam();
        
        Debug.Log($"Laser Beam initialized with DPS: {damagePerSecond}, shield multiplier: {ShieldDamageMultiplier}");
    }
    
    private void SetupLineRenderer()
    {
        // EXAMPLE: How to use SpellUtils to create a LineRenderer for a beam
        if (laserLineRenderer == null)
        {
            laserLineRenderer = SpellUtils.CreateBeamLineRenderer(gameObject, beamWidth * 0.5f, laserColor, laserIntensity);
            Debug.Log("Created new LineRenderer using SpellUtils");
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
        
        // Log that visual components are set up
        Debug.Log($"Visual components created - LineRenderer: {laserLineRenderer != null}, StartVFX: {laserStartVFX != null}, EndVFX: {laserEndVFX != null}");
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
            
            // If still not found, use fallback position
            if (_mainStationUnit == null)
            {
                Debug.LogWarning("MainStation not found or destroyed. Using default position.");
            }
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
            
            // Trace movement
            Debug.Log($"MainStation position: {stationPosition}, rotation: {stationRotation.eulerAngles}");
        }
        else
        {
            // Use fallback position if no MainStation is found
            stationPosition = new Vector3(30, 0, 20);
            stationRotation = Quaternion.identity;
        }
        
        // EXAMPLE: How to use SpellUtils to get direction toward enemy base
        Vector3 direction = SpellUtils.GetDirectionToEnemyBase(stationPosition, MyTeam);
        
        // Move this GameObject to follow the MainStation
        transform.position = stationPosition;
        transform.rotation = Quaternion.LookRotation(direction);
        
        // Calculate laser start position (at the station position)
        _laserPositions[0] = stationPosition + Vector3.up * 2.0f; // Increased height offset for better visibility
        
        // Cast a ray to find what the laser hits
        RaycastHit hit;
        Vector3 endPos;
        
        if (Physics.Raycast(_laserPositions[0], direction, out hit, beamLength, _targetLayerMask))
        {
            // Laser hits something
            endPos = hit.point;
        }
        else
        {
            // Laser doesn't hit anything - use full length
            endPos = _laserPositions[0] + (direction * beamLength);
        }
        
        // Set the end position
        _laserPositions[1] = endPos;
        
        // Set positions directly on the line renderer
        if (laserLineRenderer != null)
        {
            // Force line renderer to use world space
            laserLineRenderer.useWorldSpace = true;
            
            // Update positions
            laserLineRenderer.SetPositions(_laserPositions);
            
            // Make the effect stronger by adding width variation
            laserLineRenderer.startWidth = beamWidth * 0.8f;
            laserLineRenderer.endWidth = beamWidth * 0.3f;
            
            // Set glow parameter if using Standard Unlit shader
            if (laserLineRenderer.material.HasProperty("_Glow"))
                laserLineRenderer.material.SetFloat("_Glow", 1.5f);
            
            // Debug positions
            Debug.Log($"Line positions: Start={_laserPositions[0]}, End={_laserPositions[1]}, Length={Vector3.Distance(_laserPositions[0], _laserPositions[1])}");
        }
        else
        {
            Debug.LogError("LineRenderer is missing! Call SetupLineRenderer first");
            SetupLineRenderer(); // Try to recreate it
        }
        
        // Position the VFX objects
        if (laserStartVFX != null)
        {
            laserStartVFX.transform.position = _laserPositions[0];
            laserStartVFX.SetActive(true); // Ensure it's active
        }
        
        if (laserEndVFX != null)
        {
            laserEndVFX.transform.position = _laserPositions[1];
            laserEndVFX.SetActive(true); // Ensure it's active
        }
    }
    
    private void FindTargetsInBeam()
    {
        // EXAMPLE: How to use SpellUtils to find units in a beam
        _targetsInBeam = SpellUtils.FindUnitsInBeam(
            MyTeam,           // Our team
            _laserPositions[0], // Beam start
            _laserPositions[1], // Beam end
            beamWidth         // Beam width
        );
    }
    
    private void ApplyDamageToTargets()
    {
        if (_targetsInBeam.Count == 0)
        {
            // Even if no targets, log that we're trying to apply damage
            Debug.Log("Checking for targets to damage - none found in beam");
            return;
        }
            
        Debug.Log($"Applying damage to {_targetsInBeam.Count} targets in laser beam");
        
        foreach (Unit unit in _targetsInBeam)
        {
            if (unit != null && !unit.GetIsDeath())
            {
                // Calculate damage with critical hit chance
                int damage = CalculateDamage();
                
                // Apply damage
                unit.AddDmg(damage, damageType);
                
                // Visual feedback
                Debug.Log($"Laser did {damage} {damageType} damage to {unit.name}");
                
                // EXAMPLE: How to use SpellUtils to create hit effects
                SpellUtils.CreateHitEffect(unit.transform.position, 0.5f, Color.yellow, 0.3f);
            }
        }
    }
    
    // Calculate damage with shield multiplier and critical hit chance
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
    
    // Draw debug visualization in editor
    private void OnDrawGizmos()
    {
        if (laserLineRenderer != null && laserLineRenderer.positionCount >= 2)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 start = laserLineRenderer.GetPosition(0);
            Vector3 end = laserLineRenderer.GetPosition(1);
            
            // Draw main beam line
            Gizmos.DrawLine(start, end);
            
            // Draw beam boundaries
            Vector3 right = Vector3.Cross(end - start, Vector3.up).normalized * (beamWidth / 2f);
            
            Gizmos.DrawLine(start + right, end + right);
            Gizmos.DrawLine(start - right, end - right);
            Gizmos.DrawLine(start + right, start - right);
            Gizmos.DrawLine(end + right, end - right);
        }
    }
}
}