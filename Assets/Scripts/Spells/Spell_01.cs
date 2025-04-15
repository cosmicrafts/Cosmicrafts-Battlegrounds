namespace Cosmicrafts {
    
using System.Collections.Generic;
using UnityEngine;
/*
 * Spell 1 Laser Beam
 */
public class Spell_01 : Spell
{
    //List of the affected units
    List<Unit> Targets;
    //Damage delays
    float delaydmg;
    //Line renderer reference
    public LineRenderer Lazer;
    //The start of the laser
    public GameObject StartLazer;
    //The end of the laser
    public GameObject EndLazer;
    //Multiplier for shield damage (set by character skills)
    public float ShieldDamageMultiplier = 1.0f;
    //Base damage amount
    public int BaseDamage = 100;
    //Critical hit support like Shooter
    [Range(0f, 1f)] public float criticalStrikeChance = 0f;
    public float criticalStrikeMultiplier = 2.0f;
    //Damage type (shield by default)
    public TypeDmg damageType = TypeDmg.Shield;
    //Width of the laser beam for hit detection
    public float laserWidth = 2.5f;
    //Damage applied per second
    public int damagePerSecond = 100;
    //Length of the laser beam
    public float laserLength = 100f;
    //Player reference
    private Transform playerTransform;
    //Offset from player position (for better visual placement)
    public Vector3 laserOriginOffset = new Vector3(0, 0.5f, 0);
    
    // Laser beam start and end positions
    private Vector3 laserStart;
    private Vector3 laserEnd;
    // Direction of the laser
    private Vector3 laserDirection;

    // Start is called before the first frame update
    protected override void Start()
    {
        //Initialize basic variables
        base.Start();
        Targets = new List<Unit>();
        delaydmg = 0.25f;
        
        // Find player object - only look for the Player class
        Player playerComponent = GameObject.FindFirstObjectByType<Player>();
        if (playerComponent != null)
        {
            playerTransform = playerComponent.transform;
            Debug.Log("Found player transform for laser reference");
        }
        
        if (playerTransform == null)
        {
            Debug.LogWarning("Could not find player transform for laser - using station-to-station mode");
            
            // Fallback to old behavior if player not found
            if (GameMng.GM.MainStationsExist())
            {
                transform.position = Vector3.zero;
                laserEnd = GameMng.GM.Targets[MyTeam == Team.Blue ? 0 : 1].transform.position;
                laserStart = GameMng.GM.Targets[MyTeam == Team.Blue ? 1 : 0].transform.position;
                
                // Set initial laser position
                if (Lazer != null)
                {
                    Lazer.SetPosition(0, laserStart);
                    Lazer.SetPosition(1, laserEnd);
                }
                
                if (StartLazer != null) StartLazer.transform.position = laserStart;
                if (EndLazer != null) EndLazer.transform.position = laserEnd;
            }
        }
        else
        {
            // Initially position at player
            UpdateLaserPositions();
        }

        // Set BaseDamage to be per-tick based on damagePerSecond
        BaseDamage = Mathf.RoundToInt(damagePerSecond * delaydmg);
    }

    protected override void Update()
    {
        base.Update();
        
        // Update laser position to follow player
        if (playerTransform != null)
        {
            UpdateLaserPositions();
        }
        
        // Scan for targets every frame to make sure we're hitting everything
        ScanForTargets();

        //Damage time delay 
        if (delaydmg > 0f)
        {
            delaydmg -= Time.deltaTime;
        } 
        else
        {
            //Apply damage to the targets - reset timer
            delaydmg = 0.25f;
            
            // Only apply damage if we have targets
            if (Targets.Count > 0)
            {
                Debug.Log($"Applying damage to {Targets.Count} targets in laser beam");
                
                foreach(Unit unit in Targets)
                {
                    if (unit != null && !unit.GetIsDeath())
                    {
                        // Calculate damage with critical hit chance, similar to how Shooter does it
                        int finalDamage = CalculateDamage();
                        // Apply the damage using the unit's damage method
                        unit.AddDmg(finalDamage, damageType);
                        // Visual feedback
                        Debug.Log($"Laser did {finalDamage} {damageType} damage to {unit.name}");
                    }
                }
            }
        }
    }
    
    // Update laser beam position and direction based on player
    private void UpdateLaserPositions()
    {
        if (playerTransform == null) return;
        
        // Use player position with offset
        laserStart = playerTransform.position + playerTransform.rotation * laserOriginOffset;
        
        // Use player's forward direction
        laserDirection = playerTransform.forward;
        
        // Calculate end position
        laserEnd = laserStart + (laserDirection * laserLength);
        
        // Update line renderer positions
        if (Lazer != null)
        {
            Lazer.SetPosition(0, laserStart);
            Lazer.SetPosition(1, laserEnd);
        }
        
        // Update visual objects
        if (StartLazer != null)
        {
            StartLazer.transform.position = laserStart;
            StartLazer.transform.rotation = Quaternion.LookRotation(laserDirection);
        }
        
        if (EndLazer != null)
        {
            EndLazer.transform.position = laserEnd;
            EndLazer.transform.rotation = Quaternion.LookRotation(laserDirection);
        }
        
        // Update this transform
        transform.position = laserStart;
        transform.rotation = Quaternion.LookRotation(laserDirection);
    }
    
    // Scan for targets along the laser beam path
    private void ScanForTargets()
    {
        // Clear current targets
        Targets.Clear();
        
        // Get all units in the scene - use newer API
        Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        foreach (Unit unit in allUnits)
        {
            // Skip if unit is on our team or dead
            if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                continue;
            
            // Check if unit is in the laser beam
            if (IsUnitInLaserBeam(unit))
            {
                Targets.Add(unit);
            }
        }
    }
    
    // Check if a unit is inside the laser beam
    private bool IsUnitInLaserBeam(Unit unit)
    {
        if (unit == null)
            return false;
            
        // Get the center of the unit
        Vector3 unitPosition = unit.transform.position;
        
        // Find the closest point on the laser line to the unit
        Vector3 closestPoint = FindClosestPointOnLine(laserStart, laserEnd, unitPosition);
        
        // Calculate the distance from the unit to the closest point on the laser
        float distance = Vector3.Distance(unitPosition, closestPoint);
        
        // Check if the unit is within the laser width
        return distance <= laserWidth / 2f;
    }
    
    // Find the closest point on a line segment to a given point
    private Vector3 FindClosestPointOnLine(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLength = line.magnitude;
        line = line / lineLength;
        
        // Calculate how far along the line the closest point is
        float projectLength = Mathf.Clamp(Vector3.Dot(point - lineStart, line), 0f, lineLength);
        
        // Get the closest point on the line
        return lineStart + line * projectLength;
    }

    // Calculate damage with critical hit chance, similar to Shooter's implementation
    private int CalculateDamage()
    {
        // Apply shield damage multiplier from character skills
        int baseDamageWithMultiplier = Mathf.RoundToInt(BaseDamage * ShieldDamageMultiplier);
        
        // Check for critical hit
        bool isCritical = Random.value < criticalStrikeChance;
        int finalDamage = isCritical ? 
            Mathf.RoundToInt(baseDamageWithMultiplier * criticalStrikeMultiplier) : 
            baseDamageWithMultiplier;
            
        return finalDamage;
    }

    // Clean up the targets list (check for destroyed/null units)
    private void CleanTargetsList()
    {
        for (int i = Targets.Count - 1; i >= 0; i--)
        {
            if (Targets[i] == null || Targets[i].GetIsDeath())
            {
                Targets.RemoveAt(i);
            }
        }
    }

    // Draw the laser beam in the editor for visualization
    private void OnDrawGizmos()
    {
        if (Lazer != null && Lazer.positionCount >= 2)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 start = Lazer.GetPosition(0);
            Vector3 end = Lazer.GetPosition(1);
            
            // Create a simple capsule-like shape to represent the laser width
            Vector3 right = Vector3.Cross(end - start, Vector3.up).normalized * (laserWidth / 2f);
            
            // Draw the laser boundaries
            Gizmos.DrawLine(start + right, end + right);
            Gizmos.DrawLine(start - right, end - right);
            Gizmos.DrawLine(start + right, start - right);
            Gizmos.DrawLine(end + right, end - right);
        }
    }
}}