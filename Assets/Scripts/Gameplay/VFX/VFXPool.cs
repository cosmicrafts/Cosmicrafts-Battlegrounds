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

        [Header("Pool Settings")]
        [Range(1, 50)] public int poolSizePerPrefab = 10;
        [Range(0.5f, 10f)] public float defaultLifetime = 3f;
        [Range(0.1f, 2f)] public float impactLifetime = 0.5f;

        // Internal pools mapped by prefab reference
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();

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
        }
        
        private void InitializePoolForPrefabs(List<GameObject> prefabs)
        {
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;

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
            instance.transform.localScale = Vector3.one * scale;
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
            instance.transform.localScale = Vector3.one * scale;
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

        private GameObject GetFromPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var queue))
            {
                // Lazy-create pool if missing (shouldn't normally happen)
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
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
    }
} 