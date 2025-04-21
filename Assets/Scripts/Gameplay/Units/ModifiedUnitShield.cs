using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    // This shows how to modify your Unit.cs to use VFXPool for shield effects
    public class ModifiedUnitShield : MonoBehaviour
    {
        // Original Unit.cs fields
        public bool IsDeath;
        public int Shield = 0;
        public GameObject ShieldGameObject;
        private float shieldVisualTimer = 0f;

        // Method to handle shield impacts using VFXPool
        public void OnImpactShield(int dmg)
        {
            // Early return if object is inactive or destroyed
            if (this == null || !gameObject || !gameObject.activeInHierarchy || IsDeath)
            {
                return;
            }
            
            // Show shield visual mesh if available
            if (ShieldGameObject != null)
            {
                ShieldGameObject.SetActive(true);
                shieldVisualTimer = 1f;
            }
            
            // Play shield impact VFX from pool
            if (SimpleVFXPool.Instance != null)
            {
                // Scale the effect based on damage (larger effect for more damage)
                float scale = Mathf.Clamp(dmg / 10f, 0.8f, 2.0f);
                
                // Get shield impact effect from pool
                GameObject shieldEffect = SimpleVFXPool.Instance.PlayShieldEffect(transform.position);
                
                // Optional: Scale the effect based on damage
                if (shieldEffect != null)
                {
                    shieldEffect.transform.localScale *= scale;
                }
            }
            
            // Apply damage (original code would go here)
            // AddDmg(dmg);
        }

        // Shield activation effect (for when shields recharge or are activated)
        public void ActivateShield()
        {
            if (IsDeath || Shield <= 0) return;
            
            // Show shield visual mesh
            if (ShieldGameObject != null)
            {
                ShieldGameObject.SetActive(true);
            }
            
            // Play shield activation effect
            if (SimpleVFXPool.Instance != null)
            {
                // Use a named effect for shield activation (this would be configured in VFXPool)
                SimpleVFXPool.Instance.PlayEffect(ShieldGameObject, transform.position);
            }
        }

        // Update method for shield visuals timer
        private void Update()
        {
            // Handle shield visual timer
            if (shieldVisualTimer > 0)
            {
                shieldVisualTimer -= Time.deltaTime;
                if (shieldVisualTimer <= 0 && ShieldGameObject != null)
                {
                    ShieldGameObject.SetActive(false);
                }
            }
        }
    }

    public class SimpleVFXPool : MonoBehaviour
    {
        private static SimpleVFXPool instance;
        public static SimpleVFXPool Instance => instance;

        // Configure your effects here
        public GameObject impactEffect;
        public GameObject shieldEffect;
        public GameObject armorEffect;

        // Dictionary to store pools of effects
        private Dictionary<GameObject, List<GameObject>> pools = new Dictionary<GameObject, List<GameObject>>();

        private void Awake()
        {
            instance = this;
            InitPools();
        }

        private void InitPools()
        {
            // Initialize pools for each effect
            CreatePool(impactEffect, 10);
            CreatePool(shieldEffect, 10);
            CreatePool(armorEffect, 10);
        }

        private void CreatePool(GameObject prefab, int size)
        {
            if (prefab == null) return;

            List<GameObject> pool = new List<GameObject>();
            for (int i = 0; i < size; i++)
            {
                GameObject obj = CreateInstance(prefab);
                pool.Add(obj);
            }
            pools[prefab] = pool;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            return obj;
        }

        // Use this for standard impacts
        public GameObject PlayEffect(GameObject prefab, Vector3 position)
        {
            return GetFromPool(prefab, position, Quaternion.identity);
        }

        // Specific methods for common effects
        public GameObject PlayImpact(Vector3 position)
        {
            return GetFromPool(impactEffect, position, Quaternion.identity);
        }

        public GameObject PlayShieldEffect(Vector3 position)
        {
            return GetFromPool(shieldEffect, position, Quaternion.identity);
        }

        private GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            // Create pool if it doesn't exist
            if (!pools.ContainsKey(prefab))
                CreatePool(prefab, 5);

            // Find an inactive object
            List<GameObject> pool = pools[prefab];
            GameObject obj = null;

            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].activeInHierarchy)
                {
                    obj = pool[i];
                    break;
                }
            }

            // If we couldn't find one, create a new instance
            if (obj == null)
            {
                obj = CreateInstance(prefab);
                pool.Add(obj);
            }

            // Set up the object
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            // Auto-disable after some time
            StartCoroutine(DisableAfterDelay(obj, 2f));

            return obj;
        }

        private System.Collections.IEnumerator DisableAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
                obj.SetActive(false);
        }
    }
} 