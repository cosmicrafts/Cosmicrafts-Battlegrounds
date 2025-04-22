namespace Cosmicrafts {
    using UnityEngine;

/*
 * The animation controller script for units
 */

public class UnitAnimLis : MonoBehaviour
{
    //Unit data reference
    Unit MyUnit;

    // Start is called before the first frame update
    void Start()
    {
        //Get unit data
        MyUnit = transform.parent.GetComponent<Unit>();
        
        // If our unit is alive, make sure we're not playing a death animation
        // Removed the forceful animator reset logic as it was confusing and likely redundant
        // if (MyUnit != null && !MyUnit.IsDeath)
        // {
        //     Animator anim = GetComponent<Animator>();
        //     if (anim != null)
        //     {
        //         // Forcefully reset all animation states and triggers
        //         anim.Rebind();
        //         anim.Update(0f);
        //         
        //         // Clear the Die trigger
        //         anim.ResetTrigger("Die");
        //         
        //         // Force idle state
        //         anim.SetBool("Idle", true);
        //         
        //         Debug.Log($"Forcing animator reset on {MyUnit.gameObject.name} to prevent death animation");
        //     }
        // }
        
        //Set the attack animation speed
        // Ensure MyUnit and Animator are valid before proceeding
        if (MyUnit != null && MyUnit.GetAnimator() != null)
        {
            AnimationClip attack_clip = MyUnit.GetAnimationClip("Attack");
            Shooter shooter = transform.parent.GetComponent<Shooter>();
            if (attack_clip != null && shooter != null && shooter.CoolDown > 0) // Added check for CoolDown > 0
            {
                 MyUnit.GetAnimator().SetFloat("AttackSpeed", attack_clip.length / shooter.CoolDown * 2);
            }
            else if (shooter != null && shooter.CoolDown <= 0)
            {
                Debug.LogWarning($"Shooter CoolDown is zero or negative for {MyUnit.gameObject.name}, cannot set AttackSpeed.");
            }
        }
        else
        {
            Debug.LogWarning($"Unit or Animator not found when trying to set AttackSpeed in UnitAnimLis for {transform.parent?.name}");
        }
    }

    //Called when the deth animation ends
    public void AE_EndDeath()
    {
        // Don't destroy units, reset them for infinite play instead
        if (MyUnit != null)
        {
            // For Red team (enemy) units, respawn them
            if (FactionManager.ConvertFactionToTeam(MyUnit.MyFaction) == Team.Red)
            {
                Debug.Log($"Respawning enemy unit {MyUnit.gameObject.name} instead of destroying it");
                
                // Reset the unit instead of destroying it
                MyUnit.ResetUnit();
                
                // Teleport back to a suitable position if needed
                if (GameMng.GM != null)
                {
                    Vector3 respawnPos = GameMng.GM.GetDefaultTargetPosition(Team.Red);
                    MyUnit.transform.position = respawnPos + new Vector3(
                        Random.Range(-10f, 10f),
                        0,
                        Random.Range(-10f, 10f)
                    );
                }
            }
            else
            {
                // For player units, they already handle respawning through GameMng
                // We shouldn't need to do anything here
                Debug.Log($"Player unit death animation completed: {MyUnit.gameObject.name}");
            }
        }
    }

    //Called an explosion effect
    public void AE_BlowUpUnit()
    {
        //Kill the unit
        MyUnit.BlowUpEffect();
    }
}
}