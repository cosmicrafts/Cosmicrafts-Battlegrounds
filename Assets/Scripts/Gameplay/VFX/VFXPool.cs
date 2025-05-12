using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace Cosmicrafts
{
    /// <summary>
    ///  Unified pooling system for VFX, projectiles, and impact effects
    ///  to improve performance and solve cleanup issues.
    /// </summary>
    public class VFXPool : MonoBehaviour
    {
        public static VFXPool Instance { get; private set; }

        [Header("Explosion Prefabs (Auto‑populated)")]
        public List<GameObject> explosionPrefabs = new List<GameObject>();

        [Header("Projectile & Impact Prefabs")]
        public List<GameObject> projectilePrefabs = new List<GameObject>();
        public List<GameObject> impactPrefabs = new List<GameObject>();
        
        [Header("VFX Categories")]
        [Tooltip("Impact effects for shield hits (VFX only, not shield triggers)")]
        public List<GameObject> shieldImpactPrefabs = new List<GameObject>();
        [Tooltip("Impact effects for armor/health damage")]
        public List<GameObject> armorImpactPrefabs = new List<GameObject>();

        [Header("Pool Settings")]
        [Range(1, 50)] public int poolSizePerPrefab = 10;
        [Range(0.5f, 10f)] public float defaultLifetime = 3f;
        [Range(0.1f, 2f)] public float impactLifetime = 0.5f;

        [Header("VFX Scaling")]
        [Tooltip("Global scale multiplier for all VFX - default is 3x larger than original size")]
        [Range(0.5f, 10f)] public float globalScaleMultiplier = 3f;

        // Internal pools mapped by prefab reference
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
        
        // Tracking of unit-specific VFX
        private readonly Dictionary<int, List<GameObject>> _unitProjectiles = new();
        private readonly Dictionary<int, GameObject> _unitShieldImpacts = new();
        private readonly Dictionary<int, GameObject> _unitArmorImpacts = new();
        private readonly Dictionary<int, GameObject> _unitExplosions = new();

#if UNITY_EDITOR
        // Automatically populate the explosionPrefabs list whenever values change in inspector
        private void OnValidate()
        {
            AutoPopulateExplosionPrefabs();
        }

        private void AutoPopulateExplosionPrefabs()
        {
            explosionPrefabs.Clear();
            // Path containing AllIn1VFX demo prefabs
            string searchFolder = "Assets/Plugins/AllIn1VfxToolkit/Demo & Assets/Demo/Prefabs";
            if (!Directory.Exists(searchFolder)) return;

            // Search for all prefabs in the folder whose names contain "explosion"
            string[] guids = AssetDatabase.FindAssets("explosion t:prefab", new[] { searchFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && !explosionPrefabs.Contains(prefab))
                {
                    explosionPrefabs.Add(prefab);
                }
            }

            // Sort for consistency (alphabetically)
            explosionPrefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            // Mark the component dirty so the change persists
            EditorUtility.SetDirty(this);
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }

        private void InitializePools()
        {
            // Initialize explosion pools
            InitializePoolForPrefabs(explosionPrefabs);
            
            // Initialize projectile pools
            InitializePoolForPrefabs(projectilePrefabs);
            
            // Initialize impact pools
            InitializePoolForPrefabs(impactPrefabs);
            
            // Initialize shield impact pools
            InitializePoolForPrefabs(shieldImpactPrefabs);
            
            // Initialize armor impact pools
            InitializePoolForPrefabs(armorImpactPrefabs);
        }
        
        private void InitializePoolForPrefabs(List<GameObject> prefabs)
        {
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;

                // Check if pool already exists
                if (_pools.ContainsKey(prefab)) continue;

                var queue = new Queue<GameObject>(poolSizePerPrefab);
                for (int i = 0; i < poolSizePerPrefab; i++)
                {
                    var instance = CreateInstance(prefab);
                    queue.Enqueue(instance);
                }

                _pools[prefab] = queue;
            }
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);

            // Ensure particle systems don't auto-destroy so we can reuse them
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None;
            }

            return go;
        }

        /// <summary>
        /// Plays a random explosion at the specified position. Scale multiplier is applied uniformly.
        /// </summary>
        public GameObject PlayExplosion(Vector3 position, float scale = 1f)
        {
            if (explosionPrefabs.Count == 0)
            {
                Debug.LogWarning("VFXPool: No explosion prefabs configured (auto-population may have failed)." );
                return null;
            }

            // Pick a random prefab from the list
            var prefab = explosionPrefabs[Random.Range(0, explosionPrefabs.Count)];
            if (prefab == null) return null;

            var instance = GetFromPool(prefab);
            if (instance == null) return null;

            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale * globalScaleMultiplier;
            instance.SetActive(true);

            // Restart particle system if present
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play();
            }

            StartCoroutine(ReturnToPoolAfterLifetime(instance, prefab, defaultLifetime));
            return instance;
        }
        
        /// <summary>
        /// Gets a projectile from the pool.
        /// </summary>
        public GameObject GetProjectile(GameObject prefab)
        {
            if (prefab == null) return null;
            
            if (!projectilePrefabs.Contains(prefab))
            {
                // Add prefab to list if it's not already there
                projectilePrefabs.Add(prefab);
            }
            
            var instance = GetFromPool(prefab);
            if (instance == null) return null;
            
            // Apply global scale
            instance.transform.localScale = Vector3.one * globalScaleMultiplier;
            
            // Reset the projectile
            var projectile = instance.GetComponent<Projectile>();
            if (projectile != null)
            {
                // Reset any projectile-specific state here
                projectile.ResetProjectile();
            }
            
            return instance;
        }
        
        /// <summary>
        /// Returns a projectile to the pool.
        /// </summary>
        public void ReturnProjectile(GameObject projectileInstance, GameObject prefab)
        {
            if (projectileInstance == null || prefab == null) return;
            
            projectileInstance.SetActive(false);
            projectileInstance.transform.SetParent(transform);
            
            // Re-enqueue
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }
            queue.Enqueue(projectileInstance);
        }
        
        /// <summary>
        /// Plays an impact effect at the specified position and returns it to the pool after a short duration.
        /// </summary>
        public GameObject PlayImpact(GameObject impactPrefab, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (impactPrefab == null) return null;
            
            if (!impactPrefabs.Contains(impactPrefab))
            {
                // Add prefab to list if it's not already there
                impactPrefabs.Add(impactPrefab);
            }
            
            var instance = GetFromPool(impactPrefab);
            if (instance == null) return null;
            
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = Vector3.one * scale * globalScaleMultiplier;
            instance.SetActive(true);
            
            // Restart particle system if present
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play();
            }
            
            StartCoroutine(ReturnToPoolAfterLifetime(instance, impactPrefab, impactLifetime));
            return instance;
        }
        
        /// <summary>
        /// Plays a shield impact effect at the specified position and returns it to the pool after a short duration.
        /// Note: This handles only the visual impact effects, not shield triggers.
        /// </summary>
        public GameObject PlayShieldImpact(Vector3 position, Quaternion rotation, float scale = 1f, int unitId = 0)
        {
            // Try to use unit-specific shield impact if available
            GameObject impactPrefab = null;
            
            if (unitId > 0 && _unitShieldImpacts.TryGetValue(unitId, out var unitImpact))
            {
                impactPrefab = unitImpact;
            }
            else if (shieldImpactPrefabs.Count > 0)
            {
                impactPrefab = shieldImpactPrefabs[Random.Range(0, shieldImpactPrefabs.Count)];
            }
            else if (impactPrefabs.Count > 0)
            {
                // Fallback to general impact if no shield impact is available
                impactPrefab = impactPrefabs[Random.Range(0, impactPrefabs.Count)];
            }
            
            if (impactPrefab == null)
            {
                Debug.LogWarning("VFXPool: No shield impact prefabs configured.");
                return null;
            }
            
            return PlayImpact(impactPrefab, position, rotation, scale);
        }
        
        /// <summary>
        /// Plays an armor impact effect at the specified position and returns it to the pool after a short duration.
        /// </summary>
        public GameObject PlayArmorImpact(Vector3 position, Quaternion rotation, float scale = 1f, int unitId = 0)
        {
            // Try to use unit-specific armor impact if available
            GameObject impactPrefab = null;
            
            if (unitId > 0 && _unitArmorImpacts.TryGetValue(unitId, out var unitImpact))
            {
                impactPrefab = unitImpact;
            }
            else if (armorImpactPrefabs.Count > 0)
            {
                impactPrefab = armorImpactPrefabs[Random.Range(0, armorImpactPrefabs.Count)];
            }
            else if (impactPrefabs.Count > 0)
            {
                // Fallback to general impact if no armor impact is available
                impactPrefab = impactPrefabs[Random.Range(0, impactPrefabs.Count)];
            }
            
            if (impactPrefab == null)
            {
                Debug.LogWarning("VFXPool: No armor impact prefabs configured.");
                return null;
            }
            
            return PlayImpact(impactPrefab, position, rotation, scale);
        }
        
        /// <summary>
        /// Plays a unit-specific explosion or a random explosion if not registered
        /// </summary>
        public GameObject PlayUnitExplosion(Vector3 position, float scale = 1f, int unitId = 0)
        {
            // Try to use unit-specific explosion if available
            GameObject explosionPrefab = null;
            
            if (unitId > 0 && _unitExplosions.TryGetValue(unitId, out var unitExplosion))
            {
                explosionPrefab = unitExplosion;
            }
            
            if (explosionPrefab != null)
            {
                var instance = GetFromPool(explosionPrefab);
                if (instance != null)
                {
                    instance.transform.position = position;
                    instance.transform.rotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one * scale * globalScaleMultiplier;
                    instance.SetActive(true);
                    
                    // Restart particle system if present
                    var ps = instance.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Clear();
                        ps.Play();
                    }
                    
                    StartCoroutine(ReturnToPoolAfterLifetime(instance, explosionPrefab, defaultLifetime));
                    return instance;
                }
            }
            
            // Fall back to random explosion
            return PlayExplosion(position, scale);
        }

        private GameObject GetFromPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                // Lazy-create pool if missing (shouldn't normally happen)
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
                
                // Initialize the pool with some instances
                for (int i = 0; i < poolSizePerPrefab; i++)
                {
                    var instance = CreateInstance(prefab);
                    queue.Enqueue(instance);
                }
            }

            if (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                return obj;
            }

            // Pool exhausted – create a new instance (kept small for performance)
            return CreateInstance(prefab);
        }

        private IEnumerator ReturnToPoolAfterLifetime(GameObject obj, GameObject prefab, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj == null) yield break;

            // Stop particle system safely
            var ps = obj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform);

            // Re-enqueue
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }
            queue.Enqueue(obj);
        }
        
        #region Unit VFX Registration
        
        /// <summary>
        /// Registers a unit's projectile prefab with the pool
        /// </summary>
        public void RegisterUnitProjectile(int unitId, GameObject projectilePrefab)
        {
            if (projectilePrefab == null || unitId <= 0) return;
            
            // Add to global projectile list if not present
            if (!projectilePrefabs.Contains(projectilePrefab))
            {
                projectilePrefabs.Add(projectilePrefab);
                InitializePoolForPrefabs(new List<GameObject> { projectilePrefab });
            }
            
            // Track as unit-specific
            if (!_unitProjectiles.TryGetValue(unitId, out var projectiles))
            {
                projectiles = new List<GameObject>();
                _unitProjectiles[unitId] = projectiles;
            }
            
            if (!projectiles.Contains(projectilePrefab))
            {
                projectiles.Add(projectilePrefab);
            }
        }
        
        /// <summary>
        /// Registers a unit's shield impact effect with the pool
        /// Note: Only register visual impact effects here, not shield triggers
        /// </summary>
        public void RegisterUnitShieldImpact(int unitId, GameObject shieldImpactPrefab)
        {
            if (shieldImpactPrefab == null || unitId <= 0) return;
            
            // Add to shield impacts list if not present
            if (!shieldImpactPrefabs.Contains(shieldImpactPrefab))
            {
                shieldImpactPrefabs.Add(shieldImpactPrefab);
                InitializePoolForPrefabs(new List<GameObject> { shieldImpactPrefab });
            }
            
            // Track as unit-specific
            _unitShieldImpacts[unitId] = shieldImpactPrefab;
        }
        
        /// <summary>
        /// Registers a unit's armor impact effect with the pool
        /// </summary>
        public void RegisterUnitArmorImpact(int unitId, GameObject armorImpactPrefab)
        {
            if (armorImpactPrefab == null || unitId <= 0) return;
            
            // Add to armor impacts list if not present
            if (!armorImpactPrefabs.Contains(armorImpactPrefab))
            {
                armorImpactPrefabs.Add(armorImpactPrefab);
                InitializePoolForPrefabs(new List<GameObject> { armorImpactPrefab });
            }
            
            // Track as unit-specific
            _unitArmorImpacts[unitId] = armorImpactPrefab;
        }
        
        /// <summary>
        /// Registers a unit's explosion effect with the pool
        /// </summary>
        public void RegisterUnitExplosion(int unitId, GameObject explosionPrefab)
        {
            if (explosionPrefab == null || unitId <= 0) return;
            
            // Add to explosions list if not present
            if (!explosionPrefabs.Contains(explosionPrefab))
            {
                explosionPrefabs.Add(explosionPrefab);
                InitializePoolForPrefabs(new List<GameObject> { explosionPrefab });
            }
            
            // Track as unit-specific
            _unitExplosions[unitId] = explosionPrefab;
        }
        
        /// <summary>
        /// Gets a projectile specifically for a unit
        /// </summary>
        public GameObject GetUnitProjectile(int unitId)
        {
            if (unitId <= 0 || !_unitProjectiles.TryGetValue(unitId, out var projectiles) || projectiles.Count == 0)
            {
                // Fall back to a random projectile if none registered for this unit
                if (projectilePrefabs.Count > 0)
                {
                    return GetProjectile(projectilePrefabs[Random.Range(0, projectilePrefabs.Count)]);
                }
                return null;
            }
            
            // Get a random projectile from this unit's registered projectiles
            GameObject prefab = projectiles[Random.Range(0, projectiles.Count)];
            return GetProjectile(prefab);
        }
        
        #endregion
    }
} 