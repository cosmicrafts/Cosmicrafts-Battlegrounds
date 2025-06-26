namespace Cosmicrafts {
    using UnityEngine;

/*
 * The animation controller script for units
 */

public class UnitAnimLis : MonoBehaviour
{
    private Unit myUnit;
    private Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        // Get references
        if (transform.parent != null)
        {
            myUnit = transform.parent.GetComponent<Unit>();
            if (myUnit != null)
            {
                animator = myUnit.GetAnimator();
                if (animator != null)
                {
                    // Set up any initial animation parameters if needed
                    RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                    if (controller != null)
                    {
                        Shooter shooter = transform.parent.GetComponent<Shooter>();
                        if (shooter != null)
                        {
                            animator.SetFloat("AttackSpeed", 1f / shooter.CoolDown * 2);
                        }
                    }
                }
            }
        }
    }

    // Simple death animation end handler
    public void AE_EndDeath()
    {
        if (myUnit != null)
        {
            myUnit.OnUnitDeathHandler();
        }
    }

    // Simple explosion effect handler
    public void AE_BlowUpUnit()
    {
        if (myUnit != null)
        {
            myUnit.BlowUpEffect();
        }
    }
}
}