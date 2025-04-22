namespace Cosmicrafts
{
    using UnityEngine;

    /*
     * This is the unit enemy detector
     * Detects potential targets for the unit's shooter component
     */

    public class EDetector : MonoBehaviour
    {
        // Unit data reference
        public Unit MyUnit;

        // Shooter script reference
        public Shooter MyShooter;

        private void Start()
        {
            // Safety check for references
            if (MyUnit == null)
            {
                MyUnit = GetComponentInParent<Unit>();
                if (MyUnit == null)
                {
                    Debug.LogError($"EDetector on {gameObject.name} has no Unit reference!", this);
                    enabled = false;
                }
            }

            if (MyShooter == null)
            {
                MyShooter = GetComponentInParent<Shooter>();
                if (MyShooter == null)
                {
                    Debug.LogError($"EDetector on {gameObject.name} has no Shooter reference!", this);
                    enabled = false;
                }
            }
        }

        // New enemy detected (add to enemies list)
        private void OnTriggerEnter(Collider other)
        {
            // Skip if references are missing
            if (MyUnit == null || MyShooter == null || !MyShooter.CanAttack) return;

            // Try to get a Unit component from the collider
            Unit otherUnit = other.GetComponent<Unit>();
            if (otherUnit == null)
            {
                // If no Unit on this object, try to find one in the parent
                otherUnit = other.GetComponentInParent<Unit>();
            }

            // Process unit if found and is an enemy
            if (otherUnit != null && !otherUnit.GetIsDeath() && MyUnit.IsEnemy(otherUnit))
            {
                MyShooter.AddEnemy(otherUnit);
            }
        }

        // Enemy out of range (delete from enemies list)
        private void OnTriggerExit(Collider other)
        {
            // Skip if references are missing
            if (MyUnit == null || MyShooter == null) return;

            // Try to get a Unit component from the collider
            Unit otherUnit = other.GetComponent<Unit>();
            if (otherUnit == null)
            {
                // If no Unit on this object, try to find one in the parent
                otherUnit = other.GetComponentInParent<Unit>();
            }

            // Remove unit from enemies list if it's an enemy
            if (otherUnit != null && MyUnit.IsEnemy(otherUnit))
            {
                MyShooter.RemoveEnemy(otherUnit);
            }
        }
    }
}