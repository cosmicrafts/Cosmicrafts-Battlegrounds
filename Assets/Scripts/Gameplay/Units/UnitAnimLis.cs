namespace Cosmicrafts {
    using UnityEngine;

/*
 * The animation controller script for units
 */

public class UnitAnimLis : MonoBehaviour
{
    //Unit data reference
    Unit MyUnit;
    private Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        //Get unit data
        MyUnit = transform.parent.GetComponent<Unit>();
        animator = MyUnit.GetAnimator();
        
        if (animator != null)
        {
            // Get all animation clips from the animator controller
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller != null)
            {
                AnimationClip[] clips = controller.animationClips;
                AnimationClip attackClip = null;
                
                // Find the attack animation clip
                foreach (AnimationClip clip in clips)
                {
                    if (clip.name.ToLower().Contains("attack"))
                    {
                        attackClip = clip;
                        break;
                    }
                }
                
                // Set the attack animation speed if we found the clip and have a shooter
                Shooter shooter = transform.parent.GetComponent<Shooter>();
                if (attackClip != null && shooter != null)
                {
                    float attackSpeed = attackClip.length / shooter.CoolDown * 2;
                    animator.SetFloat("AttackSpeed", attackSpeed);
                    
                    // Debug log to help track animation setup
                    Debug.Log($"Set attack speed to {attackSpeed} for unit {MyUnit.name} (clip length: {attackClip.length}, cooldown: {shooter.CoolDown})");
                }
                else
                {
                    Debug.LogWarning($"Could not find attack animation clip for unit {MyUnit.name} or shooter component is missing");
                }
            }
            else
            {
                Debug.LogWarning($"No RuntimeAnimatorController found on unit {MyUnit.name}");
            }
        }
        else
        {
            Debug.LogWarning($"No Animator component found on unit {MyUnit.name}");
        }
    }

    //Called when the death animation ends
    public void AE_EndDeath()
    {
        if (MyUnit != null)
        {
            // Instead of destroying the unit, trigger the death event for pooling
            MyUnit.OnUnitDeathHandler();
        }
    }

    //Called for explosion effect
    public void AE_BlowUpUnit()
    {
        if (MyUnit != null)
        {
            MyUnit.BlowUpEffect();
        }
    }
}
}