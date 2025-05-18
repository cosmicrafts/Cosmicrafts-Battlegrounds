namespace Cosmicrafts
{
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;

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
        [Range(1f, 20f)]
        public float beamWidth = 8f;
        [Tooltip("Type of damage dealt by the laser")]
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
        [Tooltip("Shader Graph material for the laser effect")]
        public Material laserMaterial;
        [Tooltip("Hit effect prefab")]
        public GameObject hitEffectPrefab;
        [Tooltip("Color of the laser beam")]
        public Color laserColor = Color.blue;
        
        // Private variables
        private Unit _mainStationUnit;
        private Shooter _shooter;
        private HashSet<int> _damagedUnitsThisTick = new HashSet<int>();
        private HashSet<int> _hitUnitsLastFrame = new HashSet<int>();
        private Dictionary<int, GameObject> _activeHitEffects = new Dictionary<int, GameObject>();
        private float _damageTimer;
        private Vector3[] _laserPositions = new Vector3[2];
        private int _baseDamagePerTick;
        
        // Store original range for restoration
        private float originalShooterRange;
        private bool hasModifiedRange = false;
        
        // Method to update the beam width during gameplay
        public void SetBeamWidth(float width)
        {
            beamWidth = Mathf.Clamp(width, 1f, 20f);
            
            // Update LineRenderer if it exists
            if (laserLineRenderer != null)
            {
                laserLineRenderer.startWidth = beamWidth * 1.5f;
                laserLineRenderer.endWidth = beamWidth * 0.5f;
            }
            
            // Update VFX scales
            if (laserStartVFX != null)
            {
                laserStartVFX.transform.localScale = Vector3.one * beamWidth * 1.2f;
            }
            
            if (laserEndVFX != null)
            {
                laserEndVFX.transform.localScale = Vector3.one * beamWidth * 0.8f;
            }
            
            // Update hit effect scales
            foreach (var hitEffect in _activeHitEffects.Values)
            {
                if (hitEffect != null)
                {
                    hitEffect.transform.localScale = Vector3.one * beamWidth * 0.3f;
                }
            }
        }
        
        protected override void Start()
        {
            base.Start();
            
            _damageTimer = damageInterval;
            _baseDamagePerTick = Mathf.RoundToInt(damagePerSecond * damageInterval);
            
            var (_, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
            _mainStationUnit = mainStationUnit;
            
            // Get the shooter component and modify its range
            if (_mainStationUnit != null)
            {
                _shooter = _mainStationUnit.GetComponent<Shooter>();
                if (_shooter != null)
                {
                    // Store original range and double it
                    originalShooterRange = _shooter.RangeDetector;
                    _shooter.RangeDetector *= 2f;
                    hasModifiedRange = true;
                }
            }
            
            SetupLineRenderer();
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Validate MainStation
            if (_mainStationUnit == null || _mainStationUnit.GetIsDeath())
            {
                var (_, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
                _mainStationUnit = mainStationUnit;
                if (_mainStationUnit != null)
                {
                    _shooter = _mainStationUnit.GetComponent<Shooter>();
                }
            }
            
            // Update beam position and check for hits
            UpdateLaserBeam();
            
            // Clear last frame's hit units
            _hitUnitsLastFrame.Clear();
            
            // Find new hits
            FindTargetsInBeam();
            
            // Clean up hit effects for units no longer being hit
            CleanupHitEffects();
            
            // Apply damage on interval
            if (_damageTimer <= 0)
            {
                ApplyDamageToTargets();
                _damageTimer = damageInterval;
                _damagedUnitsThisTick.Clear();
            }
            else
            {
                _damageTimer -= Time.deltaTime;
            }
        }
        
        private void FindTargetsInBeam()
        {
            float detectionWidth = beamWidth * 1.5f;
            
            Vector3 beamCenter = (_laserPositions[0] + _laserPositions[1]) * 0.5f;
            Vector3 beamDirection = (_laserPositions[1] - _laserPositions[0]);
            float beamLen = beamDirection.magnitude;
            beamDirection.Normalize();
            
            Vector3 boxSize = new Vector3(detectionWidth, 10f, beamLen);
            Quaternion boxRotation = Quaternion.LookRotation(beamDirection);
            
            Collider[] hitColliders = Physics.OverlapBox(beamCenter, boxSize * 0.5f, boxRotation);
            
            foreach (Collider hitCollider in hitColliders)
            {
                Unit unit = hitCollider.GetComponent<Unit>();
                
                if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                {
                    continue;
                }
                
                // Get the closest point on the collider to the beam
                Vector3 hitPoint = hitCollider.ClosestPoint(_laserPositions[0]);
                float distanceToBeam = DistancePointToLine(hitPoint, _laserPositions[0], _laserPositions[1]);
                
                if (distanceToBeam <= detectionWidth * 0.5f)
                {
                    int unitId = unit.getId();
                    
                    // Mark this unit as hit this frame
                    _hitUnitsLastFrame.Add(unitId);
                    
                    // Queue for damage on next tick
                    if (_damageTimer <= 0.05f && !_damagedUnitsThisTick.Contains(unitId))
                    {
                        _damagedUnitsThisTick.Add(unitId);
                    }
                    
                    // Update or create hit effect at the actual hit point
                    if (hitEffectPrefab != null)
                    {
                        UpdateHitEffect(unit, hitPoint);
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
                    unit.AddDmg(damage, damageType);
                    
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
        
        private void UpdateHitEffect(Unit unit, Vector3 hitPoint)
        {
            int unitId = unit.getId();
            Vector3 beamDirection = (_laserPositions[1] - _laserPositions[0]).normalized;
            
            // If we already have an effect for this unit, update its position
            if (_activeHitEffects.TryGetValue(unitId, out GameObject existingEffect) && existingEffect != null)
            {
                existingEffect.transform.position = hitPoint;
                existingEffect.transform.rotation = Quaternion.LookRotation(beamDirection);
                existingEffect.SetActive(true);
            }
            // Otherwise create a new effect
            else
            {
                GameObject hitEffect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(beamDirection));
                hitEffect.transform.localScale = Vector3.one * beamWidth * 0.3f;
                
                // Store the effect
                _activeHitEffects[unitId] = hitEffect;
            }
        }
        
        private void CleanupHitEffects()
        {
            // Find units that were hit before but aren't being hit now
            List<int> unitsToRemove = new List<int>();
            
            foreach (var unitIdEffectPair in _activeHitEffects)
            {
                int unitId = unitIdEffectPair.Key;
                GameObject effect = unitIdEffectPair.Value;
                
                // If this unit is no longer being hit, destroy its effect
                if (!_hitUnitsLastFrame.Contains(unitId))
                {
                    if (effect != null)
                    {
                        Destroy(effect);
                    }
                    unitsToRemove.Add(unitId);
                }
            }
            
            // Remove entries for units no longer being hit
            foreach (int unitId in unitsToRemove)
            {
                _activeHitEffects.Remove(unitId);
            }
        }
        
        private bool IsUnitInBeam(Unit unit, Vector3 beamStart, Vector3 beamEnd, float beamWidth)
        {
            if (unit == null) return false;
            
            Collider unitCollider = unit.GetComponent<Collider>();
            if (unitCollider == null) return false;
            
            Bounds bounds = unitCollider.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            
            Vector3[] checkPoints = new Vector3[]
            {
                center,
                center + new Vector3(extents.x, 0, extents.z),
                center + new Vector3(-extents.x, 0, extents.z),
                center + new Vector3(extents.x, 0, -extents.z),
                center + new Vector3(-extents.x, 0, -extents.z),
                center + new Vector3(0, 0, extents.z),
                center + new Vector3(0, 0, -extents.z),
                center + new Vector3(extents.x, 0, 0),
                center + new Vector3(-extents.x, 0, 0)
            };
            
            Vector2 beamStart2D = new Vector2(beamStart.x, beamStart.z);
            Vector2 beamEnd2D = new Vector2(beamEnd.x, beamEnd.z);
            Vector2 beamDir2D = (beamEnd2D - beamStart2D).normalized;
            float beamLength2D = Vector2.Distance(beamStart2D, beamEnd2D);
            
            foreach (Vector3 point in checkPoints)
            {
                Vector2 point2D = new Vector2(point.x, point.z);
                Vector2 toPoint2D = point2D - beamStart2D;
                
                float projection = Vector2.Dot(toPoint2D, beamDir2D);
                
                if (projection < 0 || projection > beamLength2D)
                    continue;
                    
                Vector2 closestPoint2D = beamStart2D + beamDir2D * projection;
                float distance = Vector2.Distance(point2D, closestPoint2D);
                
                if (distance <= beamWidth * 0.5f)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private void SetupLineRenderer()
        {
            if (laserLineRenderer == null)
            {
                laserLineRenderer = gameObject.AddComponent<LineRenderer>();
                laserLineRenderer.positionCount = 2;
                laserLineRenderer.useWorldSpace = true;
                laserLineRenderer.startWidth = beamWidth * 1.5f;
                laserLineRenderer.endWidth = beamWidth * 0.5f;
            }
            
            // Apply the material and color
            if (laserMaterial != null)
            {
                laserLineRenderer.material = laserMaterial;
            }
            
            // Set the color gradient
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(laserColor, 0.0f),
                    new GradientColorKey(laserColor, 1.0f) 
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.5f, 1.0f)
                }
            );
            laserLineRenderer.colorGradient = gradient;
            
            if (laserStartVFX == null)
            {
                // Create a simple placeholder if no VFX provided
                GameObject startVfx = new GameObject("LaserStartVFX");
                startVfx.transform.parent = transform;
                startVfx.transform.localPosition = Vector3.zero;
                
                laserStartVFX = startVfx;
            }
            
            if (laserEndVFX == null)
            {
                // Create a simple placeholder if no VFX provided
                GameObject endVfx = new GameObject("LaserEndVFX");
                endVfx.transform.parent = transform;
                endVfx.transform.localPosition = Vector3.forward * 10f;
                
                laserEndVFX = endVfx;
            }
        }
        
        private void UpdateLaserBeam()
        {
            if (_mainStationUnit == null) return;
            
            Vector3 stationPosition = _mainStationUnit.transform.position;
            Vector3 direction;
            
            // Check if we have a valid shooter with a target
            if (_shooter != null && _shooter.GetCurrentTarget() != null)
            {
                Unit target = _shooter.GetCurrentTarget();
                direction = (target.transform.position - stationPosition).normalized;
            }
            else
            {
                // No target - use unit's forward direction
                direction = _mainStationUnit.transform.forward;
            }
            
            // Update transform to match unit
            transform.position = stationPosition;
            transform.rotation = Quaternion.LookRotation(direction);
            
            // Calculate beam positions
            _laserPositions[0] = stationPosition + Vector3.up * 2.0f;
            _laserPositions[1] = _laserPositions[0] + (direction * beamLength);
            
            // Update line renderer
            if (laserLineRenderer != null)
            {
                laserLineRenderer.useWorldSpace = true;
                laserLineRenderer.SetPositions(_laserPositions);
                laserLineRenderer.startWidth = beamWidth * 1.5f;
                laserLineRenderer.endWidth = beamWidth * 0.5f;
            }
            else
            {
                SetupLineRenderer();
            }
            
            // Update VFX positions
            if (laserStartVFX != null)
            {
                laserStartVFX.transform.position = _laserPositions[0];
                laserStartVFX.transform.localScale = Vector3.one * beamWidth * 1.2f;
                laserStartVFX.SetActive(true);
            }
            
            if (laserEndVFX != null)
            {
                laserEndVFX.transform.position = _laserPositions[1];
                laserEndVFX.transform.localScale = Vector3.one * beamWidth * 0.8f;
                laserEndVFX.SetActive(true);
            }
        }
        
        private Vector3 FindBeamIntersectionPoint(Unit unit, Vector3 startPos, Vector3 direction)
        {
            Collider collider = unit.GetComponent<Collider>();
            if (collider != null)
            {
                RaycastHit hit;
                if (collider.Raycast(new Ray(startPos, direction), out hit, beamLength))
                {
                    return hit.point;
                }
            }
            
            return unit.transform.position;
        }
        
        private int CalculateDamage()
        {
            int baseDamageWithMultiplier = Mathf.RoundToInt(_baseDamagePerTick * ShieldDamageMultiplier);
            bool isCritical = Random.value < criticalStrikeChance;
            return isCritical ? 
                Mathf.RoundToInt(baseDamageWithMultiplier * criticalStrikeMultiplier) : 
                baseDamageWithMultiplier;
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
                if (Application.isPlaying)
                {
                    Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                    if (_mainStationUnit != null)
                    {
                        Gizmos.DrawWireSphere(_mainStationUnit.transform.position, 50f);
                    }
                }
            }
        }
        
        private void OnDisable()
        {
            // Restore original range when spell is disabled
            if (hasModifiedRange && _shooter != null)
            {
                _shooter.RangeDetector = originalShooterRange;
            }

            // Clean up all active hit effects
            foreach (var effect in _activeHitEffects.Values)
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
            }
            _activeHitEffects.Clear();
        }
        
        // Add a method to update the laser color at runtime
        public void SetLaserColor(Color newColor)
        {
            laserColor = newColor;
            if (laserLineRenderer != null)
            {
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(newColor, 0.0f),
                        new GradientColorKey(newColor, 1.0f) 
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(0.5f, 1.0f)
                    }
                );
                laserLineRenderer.colorGradient = gradient;
            }
        }
        
        // Helper method to calculate point-to-line distance
        private float DistancePointToLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 line = lineEnd - lineStart;
            float len = line.magnitude;
            line.Normalize();
            
            Vector3 v = point - lineStart;
            float d = Vector3.Dot(v, line);
            d = Mathf.Clamp(d, 0f, len);
            
            Vector3 nearestPoint = lineStart + line * d;
            
            return Vector3.Distance(point, nearestPoint);
        }
    }
}