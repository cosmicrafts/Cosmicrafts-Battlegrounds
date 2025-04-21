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
    ///  Minimal, high‑performance pool dedicated *only* to the Explosion effects
    ///  listed in AllIn1VfxToolkitEffects.md. The list is auto‑populated in the
    ///  editor so no manual drag‑and‑drop is required.
    /// </summary>
    public class VFXPool : MonoBehaviour
    {
        public static VFXPool Instance { get; private set; }

        [Header("Explosion Prefabs (Auto‑populated)")]
        public List<GameObject> explosionPrefabs = new List<GameObject>();

        [Header("Pool Settings")]
        [Range(1, 50)] public int poolSizePerPrefab = 10;
        [Range(0.5f, 10f)] public float defaultLifetime = 3f;

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
            foreach (var prefab in explosionPrefabs)
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
        /// Plays a random explosion at the specified position.  Scale multiplier is applied uniformly.
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