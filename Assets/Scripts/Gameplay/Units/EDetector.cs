namespace Cosmicrafts
{
    using UnityEngine;
    using System.Collections;

    /*
     * This is the unit enemy detector
     */

    public class EDetector : MonoBehaviour
    {
        //Unit data reference
        public Unit MyUnit;

        //Shooter script reference
        public Shooter MyShooter;
        
        // Debug flag
        [Header("Debug")]
        public bool showDebugLogs = false;
        
        // Optimization - cache team data
        private Team myTeam;
        private int layerMask;
        
        private void Start()
        {
            // Make sure references are valid
            if (MyUnit == null)
            {
                MyUnit = GetComponentInParent<Unit>();
                if (MyUnit == null)
                {
                    Debug.LogError($"EDetector on {gameObject.name} has no Unit reference!");
                    return;
                }
            }
            
            if (MyShooter == null)
            {
                MyShooter = GetComponentInParent<Shooter>();
                if (MyShooter == null)
                {
                    Debug.LogError($"EDetector on {gameObject.name} has no Shooter reference!");
                    return;
                }
            }
            
            // Cache team
            myTeam = MyUnit.MyTeam;
            
            // Ensure the detector radius matches the shooter's range
            SphereCollider detector = GetComponent<SphereCollider>();
            if (detector != null && MyShooter != null)
            {
                detector.radius = MyShooter.RangeDetector;
                if (!detector.isTrigger)
                {
                    Debug.LogWarning($"EDetector collider on {gameObject.name} is not set as a trigger! Setting it now.");
                    detector.isTrigger = true;
                }
            }
            
            // Start a periodic check for nearby enemies to handle edge cases
            StartCoroutine(PeriodicEnemyCheck());
        }
        
        // Periodic check for enemies (handles edge cases where triggers fail)
        private IEnumerator PeriodicEnemyCheck()
        {
            WaitForSeconds wait = new WaitForSeconds(1.0f);
            
            while (enabled && MyUnit != null && !MyUnit.GetIsDeath())
            {
                yield return wait;
                
                // Skip if shooter is not active
                if (MyShooter == null || !MyShooter.CanAttack) continue;
                
                // If we don't have a target, do a sphere cast to find nearby enemies
                if (MyShooter.GetCurrentTarget() == null)
                {
                    ScanForNearbyEnemies();
                }
            }
        }
        
        // Alternative detection method for reliability
        private void ScanForNearbyEnemies()
        {
            if (MyUnit == null || MyShooter == null) return;
            
            // Find all colliders in range
            Collider[] colliders = Physics.OverlapSphere(transform.position, MyShooter.RangeDetector);
            
            foreach (Collider col in colliders)
            {
                // Quick tag check first
                if (!col.CompareTag("Unit")) continue;
                
                Unit unit = col.GetComponent<Unit>();
                if (unit != null && !unit.GetIsDeath() && !unit.IsMyTeam(myTeam))
                {
                    // Found an enemy - add it to shooter
                    MyShooter.AddEnemy(unit);
                    if (showDebugLogs)
                    {
                        Debug.Log($"[{myTeam}] Detector: Found enemy {unit.name} with scan");
                    }
                    break; // We only need one valid target
                }
            }
        }

        //New enemy detected (add to enemys list)
        private void OnTriggerEnter(Collider other)
        {
            // Check references first
            if (MyUnit == null || MyShooter == null) return;
            
            // Quick optimization - skip processing for non-unit objects
            if (!other.CompareTag("Unit")) return;
            
            // Get the Unit component
            Unit otherUnit = other.GetComponent<Unit>();
            if (otherUnit == null) return;
            
            // Skip immediately if it's the same team (no need for further checks)
            if (otherUnit.MyTeam == myTeam) return;
            
            // Process only enemy units
            bool isAlive = !otherUnit.GetIsDeath();
            
            if (isAlive)
            {
                // Add the unit to the enemies list
                MyShooter.AddEnemy(otherUnit);
                
                if (showDebugLogs)
                {
                  //  Debug.Log($"[{myTeam}] Detector: Added enemy {otherUnit.name} to targets list");
                }
            }
        }

        //Enemy out of range (delete from enemys list)
        private void OnTriggerExit(Collider other)
        {
            // Check references first
            if (MyUnit == null || MyShooter == null) return;
            
            // Quick optimization
            if (!other.CompareTag("Unit")) return;
            
            // Get unit
            Unit otherUnit = other.GetComponent<Unit>();
            if (otherUnit == null) return;
            
            // Only process enemy units
            if (otherUnit.MyTeam != myTeam)
            {
                // Delete the unit from the enemies list
                MyShooter.RemoveEnemy(otherUnit);
                
                if (showDebugLogs)
                {
                   // Debug.Log($"[{myTeam}] Detector: Removed enemy {otherUnit.name} from targets list");
                }
            }
        }
        
        // Keep the detector radius in sync with shooter's range
        private void Update()
        {
            if (MyShooter != null)
            {
                SphereCollider detector = GetComponent<SphereCollider>();
                if (detector != null && !Mathf.Approximately(detector.radius, MyShooter.RangeDetector))
                {
                    detector.radius = MyShooter.RangeDetector;
                    if (showDebugLogs)
                    {
                        Debug.Log($"[{myTeam}] Detector: Updated radius to {detector.radius}");
                    }
                }
            }
        }
        
        // For manual debugging - call from inspector or other scripts
        public void LogDetectorState()
        {
            if (MyUnit == null || MyShooter == null)
            {
                Debug.LogWarning("Detector references not set properly!");
                return;
            }
            
            Debug.Log($"Detector on {gameObject.name}, Team: {myTeam}, CanAttack: {MyShooter.CanAttack}, Range: {MyShooter.RangeDetector}");
            
            // Check if the sphere collider matches the shooter's range
            SphereCollider detector = GetComponent<SphereCollider>();
            if (detector != null)
            {
                bool radiusMatch = Mathf.Approximately(detector.radius, MyShooter.RangeDetector);
                Debug.Log($"Detector collider radius: {detector.radius}, matches shooter range: {radiusMatch}");
                Debug.Log($"Detector collider isTrigger: {detector.isTrigger}");
            }
            else
            {
                Debug.LogError("No SphereCollider found on detector!");
            }
        }

        // Called by taunt units to force a target refresh
        public void ForceRefreshTarget()
        {
            if (MyShooter == null) return;

            // Clear current target
            MyShooter.SetTarget(null);
            
            // Force an immediate scan for nearby enemies
            ScanForNearbyEnemies();
            
            if (showDebugLogs)
            {
                Debug.Log($"[{myTeam}] Detector: Force refreshed target due to taunt");
            }
        }
    }
}