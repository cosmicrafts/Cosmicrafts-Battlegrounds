namespace Cosmicrafts {
    
using UnityEngine;
using System.Collections.Generic;

/*
 * SpellUtils - Utility class for Spell scripts
 * 
 * Contains helper methods for finding game objects like player stations,
 * targeting enemies, and other common operations needed by spells.
 */
public static class SpellUtils
{
    /// <summary>
    /// Finds the player's MainStation based on team and player ID.
    /// </summary>
    /// <param name="team">The team to find the station for</param>
    /// <param name="playerId">The player ID to find the station for</param>
    /// <returns>A tuple containing (MainStation, Unit) - either can be null if not found</returns>
    public static (MainStation station, Unit unit) FindPlayerMainStation(Team team, int playerId)
    {
        // First try to find exact match with both team and playerID
        MainStation[] allStations = GameObject.FindObjectsByType<MainStation>(FindObjectsSortMode.None);
        
        // Search for exact match (team + playerId)
        foreach (MainStation station in allStations)
        {
            Unit stationUnit = station.GetComponent<Unit>();
            
            if (stationUnit != null && 
                stationUnit.IsMyTeam(team) && 
                stationUnit.PlayerId == playerId)
            {
                // Debug.Log($"[SpellUtils] Found exact MainStation match: Team={team}, PlayerId={playerId}, UnitId={stationUnit.getId()}");
                return (station, stationUnit);
            }
        }
        
        // If no exact match, try team-only match
        foreach (MainStation station in allStations)
        {
            Unit stationUnit = station.GetComponent<Unit>();
            
            if (stationUnit != null && stationUnit.IsMyTeam(team))
            {
                // Debug.Log($"[SpellUtils] Found team-only MainStation match: Team={team}, PlayerId={stationUnit.PlayerId}, UnitId={stationUnit.getId()}");
                return (station, stationUnit);
            }
        }
        
        // Fallback to GameMng targets
        if (GameMng.GM != null && GameMng.GM.MainStationsExist())
        {
            int stationIndex = team == Team.Blue ? 1 : 0;
            if (GameMng.GM.Targets[stationIndex] != null)
            {
                Unit targetUnit = GameMng.GM.Targets[stationIndex];
                // Debug.Log($"[SpellUtils] Using GameMng Target as fallback: Team={targetUnit.MyTeam}, PlayerId={targetUnit.PlayerId}");
                return (null, targetUnit);
            }
        }
        
        // Nothing found
        Debug.LogWarning($"[SpellUtils] No MainStation found for Team={team}, PlayerId={playerId}");
        return (null, null);
    }
    
    /// <summary>
    /// Gets direction from source position toward enemy base
    /// </summary>
    /// <param name="sourcePosition">Starting position</param>
    /// <param name="team">The team to find the enemy base for</param>
    /// <returns>Direction vector pointing toward enemy base, or forward if enemy base not found</returns>
    public static Vector3 GetDirectionToEnemyBase(Vector3 sourcePosition, Team team)
    {
        // Default direction
        Vector3 direction = Vector3.forward;
        
        // Find the enemy base to aim at
        if (GameMng.GM != null && GameMng.GM.MainStationsExist())
        {
            // Get enemy base index (opposite of current team)
            int enemyIndex = team == Team.Blue ? 0 : 1;
            if (GameMng.GM.Targets[enemyIndex] != null)
            {
                // Aim toward the enemy base
                direction = (GameMng.GM.Targets[enemyIndex].transform.position - sourcePosition).normalized;
                direction.y = 0; // Keep on horizontal plane
            }
        }
        
        return direction;
    }
    
    /// <summary>
    /// Finds all enemy units in the beam path
    /// </summary>
    /// <param name="team">The spell's team</param>
    /// <param name="lineStart">Start point of the beam</param>
    /// <param name="lineEnd">End point of the beam</param>
    /// <param name="beamWidth">Width of the beam for hit detection</param>
    /// <returns>List of enemy units in the beam</returns>
    public static List<Unit> FindUnitsInBeam(Team team, Vector3 lineStart, Vector3 lineEnd, float beamWidth)
    {
        List<Unit> targetsInBeam = new List<Unit>();
        
        // Get all units in the scene
        Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        // Log how many units we're checking
        // Debug.Log($"[SpellUtils] Checking {allUnits.Length} units for beam collision. Beam width: {beamWidth}");
        
        // Draw debug for beam visualization
        Debug.DrawLine(lineStart, lineEnd, Color.red, 1.0f);
        Debug.DrawLine(lineStart + Vector3.up * 0.1f, lineEnd + Vector3.up * 0.1f, Color.yellow, 1.0f);
        
        Vector3 beamDirection = (lineEnd - lineStart).normalized;
        Vector3 right = Vector3.Cross(beamDirection, Vector3.up).normalized * (beamWidth / 2);
        
        // Draw beam boundaries for debug
        Debug.DrawLine(lineStart + right, lineEnd + right, Color.green, 1.0f);
        Debug.DrawLine(lineStart - right, lineEnd - right, Color.green, 1.0f);
        
        // Use a slightly larger width for detection to ensure hits
        float detectionWidth = beamWidth * 1.2f;
        
        foreach (Unit unit in allUnits)
        {
            // Skip units on our team, null, or dead units
            if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(team))
                continue;
            
            // Skip MainStation units (they're not damage targets)
            if (unit.GetComponent<MainStation>() != null)
                continue;
                
            // Instead of just checking position, check multiple points on the unit's collider if available
            Collider unitCollider = unit.GetComponent<Collider>();
            bool inBeam = false;
            
            if (unitCollider != null)
            {
                // Check center and bounds extents
                Vector3 center = unitCollider.bounds.center;
                Vector3 extents = unitCollider.bounds.extents;
                
                // Check multiple points of the collider
                inBeam = IsPointInBeam(center, lineStart, lineEnd, detectionWidth);
                
                // If center isn't in beam, check 8 corners of the bounding box
                if (!inBeam)
                {
                    // Check corners
                    for (int x = -1; x <= 1; x += 2)
                    {
                        for (int y = -1; y <= 1; y += 2)
                        {
                            for (int z = -1; z <= 1; z += 2)
                            {
                                Vector3 cornerPoint = center + new Vector3(x * extents.x, y * extents.y, z * extents.z);
                                if (IsPointInBeam(cornerPoint, lineStart, lineEnd, detectionWidth))
                                {
                                    inBeam = true;
                                    Debug.DrawLine(cornerPoint, center, Color.yellow, 1.0f); // Debug line to show which point is hit
                                    break;
                                }
                            }
                            if (inBeam) break;
                        }
                        if (inBeam) break;
                    }
                }
            }
            else
            {
                // No collider, just check the transform position
                inBeam = IsPointInBeam(unit.transform.position, lineStart, lineEnd, detectionWidth);
            }
            
            if (inBeam)
            {
                targetsInBeam.Add(unit);
                // Debug.Log($"[SpellUtils] Unit {unit.name} is in beam!");
                
                // Draw debug line to hit unit
                Debug.DrawLine(lineStart, unit.transform.position, Color.magenta, 1.0f);
            }
        }
        
        return targetsInBeam;
    }
    
    /// <summary>
    /// Check if a point is inside a beam represented by a line with width
    /// </summary>
    private static bool IsPointInBeam(Vector3 point, Vector3 lineStart, Vector3 lineEnd, float beamWidth)
    {
        // Find closest point on beam line
        Vector3 closestPoint = GetClosestPointOnLine(lineStart, lineEnd, point);
        
        // Check if point is within beam width and beam length
        float distanceToBeam = Vector3.Distance(point, closestPoint);
        
        // Check if the closest point is actually on the line segment
        bool isOnLineSegment = IsPointOnLineSegment(lineStart, lineEnd, closestPoint);
        
        // Draw debug line from point to closest point on beam
        if (distanceToBeam <= beamWidth/2)
        {
            Debug.DrawLine(point, closestPoint, Color.green, 1.0f);
        }
        
        return isOnLineSegment && distanceToBeam <= beamWidth/2;
    }
    
    /// <summary>
    /// Calculate the closest point on a line segment to a given point
    /// </summary>
    private static Vector3 GetClosestPointOnLine(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLength = line.magnitude;
        line.Normalize();
        
        Vector3 pointVector = point - lineStart;
        float dot = Vector3.Dot(pointVector, line);
        dot = Mathf.Clamp(dot, 0, lineLength);
        
        return lineStart + line * dot;
    }
    
    /// <summary>
    /// Check if a point is on a line segment (between start and end)
    /// </summary>
    private static bool IsPointOnLineSegment(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        // Check if the point is between the start and end of the line segment
        float lineLengthSquared = (lineEnd - lineStart).sqrMagnitude;
        
        // If line has no length, just check distance to start point
        if (lineLengthSquared < 0.0001f)
            return (point - lineStart).sqrMagnitude < 0.0001f;
            
        // Calculate how far along the line the closest point is (0-1)
        float t = Vector3.Dot(point - lineStart, lineEnd - lineStart) / lineLengthSquared;
        
        // If t is between 0-1, the point is on the line segment
        return (t >= 0 && t <= 1);
    }
    
    /// <summary>
    /// Creates a LineRenderer component with standard beam settings
    /// </summary>
    public static LineRenderer CreateBeamLineRenderer(GameObject owner, float width, Color color, float intensity = 5.0f)
    {
        // Get existing LineRenderer or add a new one
        LineRenderer lineRenderer = owner.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = owner.AddComponent<LineRenderer>();
        }
        
        // Configure basic properties
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        
        // Increased width for better visibility
        lineRenderer.startWidth = width * 1.5f;
        lineRenderer.endWidth = width * 0.8f;
        lineRenderer.enabled = true;
        
        // Create emissive material with double intensity for better visibility
        Material beamMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
        beamMaterial.SetColor("_Color", color);
        beamMaterial.SetColor("_EmissionColor", color * intensity * 2.0f); // Double intensity
        beamMaterial.EnableKeyword("_EMISSION");
        
        // Configure transparency
        beamMaterial.SetFloat("_Mode", 2); // Fade mode
        beamMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        beamMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        beamMaterial.SetInt("_ZWrite", 0);
        beamMaterial.DisableKeyword("_ALPHATEST_ON");
        beamMaterial.EnableKeyword("_ALPHABLEND_ON");
        beamMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        beamMaterial.renderQueue = 3000;
        
        lineRenderer.material = beamMaterial;
        
        return lineRenderer;
    }
    
    /// <summary>
    /// Creates a beam effect GameObject (sphere with emissive material)
    /// </summary>
    public static GameObject CreateBeamEffect(Vector3 position, float size, Color color, float intensity = 5.0f, Transform parent = null)
    {
        GameObject effectObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effectObject.name = "BeamEffect";
        Object.Destroy(effectObject.GetComponent<Collider>());
        
        effectObject.transform.position = position;
        effectObject.transform.localScale = new Vector3(size, size, size);
        
        if (parent != null)
        {
            effectObject.transform.parent = parent;
        }
        
        // Create emissive material
        var renderer = effectObject.GetComponent<Renderer>();
        Material effectMaterial = new Material(Shader.Find("Standard"));
        effectMaterial.SetColor("_Color", color);
        effectMaterial.SetColor("_EmissionColor", color * intensity);
        effectMaterial.EnableKeyword("_EMISSION");
        renderer.material = effectMaterial;
        
        return effectObject;
    }
    
    /// <summary>
    /// Creates a temporary hit effect at the specified position
    /// </summary>
    public static void CreateHitEffect(Vector3 position, float size = 0.5f, Color? color = null, float duration = 0.3f)
    {
        Color effectColor = color ?? Color.yellow;
        
        // Create a simple hit effect (sphere)
        GameObject hitEffect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hitEffect.name = "HitEffect";
        Object.Destroy(hitEffect.GetComponent<Collider>());
        hitEffect.transform.position = position;
        hitEffect.transform.localScale = new Vector3(size, size, size);
        
        // Add a bright material
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetColor("_Color", effectColor);
        mat.SetColor("_EmissionColor", effectColor * 2.0f);
        mat.EnableKeyword("_EMISSION");
        hitEffect.GetComponent<Renderer>().material = mat;
        
        // Destroy after a short time
        Object.Destroy(hitEffect, duration);
    }
}
} 