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
        public Material laserMaterial;
        public Color laserColor = Color.red;
        public float laserIntensity = 5.0f;
        [Tooltip("Whether to create additional VFX where the beam penetrates through targets")]
        public bool createPenetrationEffects = true;
        
        // Private variables
        private Unit _mainStationUnit;
        private Shooter _shooter;
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
            
            // Get the shooter component
            if (_mainStationUnit != null)
            {
                _shooter = _mainStationUnit.GetComponent<Shooter>();
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
            FindTargetsInBeam();
            
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
            float beamLength = beamDirection.magnitude;
            beamDirection.Normalize();
            
            Vector3 boxSize = new Vector3(detectionWidth, 10f, beamLength);
            Quaternion boxRotation = Quaternion.LookRotation(beamDirection);
            
            Collider[] hitColliders = Physics.OverlapBox(beamCenter, boxSize * 0.5f, boxRotation);
            
            foreach (Collider hitCollider in hitColliders)
            {
                Unit unit = hitCollider.GetComponent<Unit>();
                
                if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                {
                    continue;
                }
                
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
                    unit.AddDmg(damage, damageType);
                    
                    if (!unit.IsMyTeam(MyTeam))
                    {
                        GameMng.MT?.AddDamage(damage);
                    }
                }
            }
            
            _damagedUnitsThisTick.Clear();
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
            
            Vector3 frontPos = FindBeamIntersectionPoint(unit, _laserPositions[0], beamDirection);
            SpellUtils.CreateHitEffect(frontPos, beamWidth * 0.3f, Color.yellow, 0.3f);
            
            Vector3 backPos = FindBeamIntersectionPoint(unit, frontPos + beamDirection * 0.1f, beamDirection);
            SpellUtils.CreateHitEffect(backPos, beamWidth * 0.2f, Color.red, 0.3f);
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
                laserLineRenderer = SpellUtils.CreateBeamLineRenderer(gameObject, beamWidth * 0.5f, laserColor, laserIntensity);
            }
            
            if (laserStartVFX == null)
            {
                laserStartVFX = SpellUtils.CreateBeamEffect(
                    transform.position,
                    beamWidth,
                    laserColor,
                    laserIntensity,
                    transform
                );
                laserStartVFX.name = "LaserStartVFX";
            }
            
            if (laserEndVFX == null)
            {
                laserEndVFX = SpellUtils.CreateBeamEffect(
                    transform.position + Vector3.forward * 10f,
                    beamWidth * 0.8f,
                    laserColor,
                    laserIntensity,
                    transform
                );
                laserEndVFX.name = "LaserEndVFX";
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
                
                if (laserLineRenderer.material.HasProperty("_Glow"))
                    laserLineRenderer.material.SetFloat("_Glow", 1.5f);
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
    }
}