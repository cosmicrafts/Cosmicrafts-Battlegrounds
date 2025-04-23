namespace Cosmicrafts {
    using UnityEngine;
    using UnityEngine.Animations;
    using System.Collections.Generic;

/*
 * The animation controller script for units
 */

public class UnitAnimLis : MonoBehaviour
{
    //Unit data reference
    Unit MyUnit;
    
    [Header("Animation Loop Control")]
    [Tooltip("Enable custom loop section within the Idle animation")]
    public bool useCustomLoopSection = false;
    [Tooltip("Time marker name for loop start in Idle animation")]
    public string loopStartMarker = "LoopStart";
    [Tooltip("Time marker name for loop end in Idle animation")]
    public string loopEndMarker = "LoopEnd";
    
    // Loop tracking variables
    private bool isInLoopSection = false;
    private float loopStartTime = -1f;
    private float loopEndTime = -1f;
    private Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        //Get unit data
        MyUnit = transform.parent.GetComponent<Unit>();
        animator = GetComponent<Animator>();
        
        // Ensure the animator exists
        if (animator == null)
        {
            Debug.LogError($"No Animator component found on {gameObject.name}!");
            return;
        }
        
        // Find animation loop markers in the animations
        FindAnimationLoopMarkers();
        
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
    
    private void Update()
    {
        // Handle animation loop logic if enabled
        if (useCustomLoopSection && animator != null && loopStartTime >= 0 && loopEndTime >= 0)
        {
            ManageAnimationLoop();
        }
    }
    
    /// <summary>
    /// Finds animation loop markers in the Idle animation
    /// </summary>
    private void FindAnimationLoopMarkers()
    {
        if (animator == null) return;
        
        // Get all animation clips from the animator
        AnimationClip[] clips = null;
        
        // Try to get animation clips from the runtime animator controller
        if (animator.runtimeAnimatorController != null)
        {
            clips = animator.runtimeAnimatorController.animationClips;
        }
        
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning("Could not find animation clips in controller");
            return;
        }
        
        foreach (AnimationClip clip in clips)
        {
            // Skip null clips
            if (clip == null) continue;
            
            // Look for the Idle animation
            if (clip.name.Contains("Idle") || clip.name.Contains("Movement"))
            {
                // Find animation events that mark loop points
                foreach (AnimationEvent animEvent in clip.events)
                {
                    if (animEvent.functionName == loopStartMarker)
                    {
                        loopStartTime = animEvent.time;
                        Debug.Log($"Found loop start marker at time: {loopStartTime}");
                    }
                    else if (animEvent.functionName == loopEndMarker)
                    {
                        loopEndTime = animEvent.time;
                        Debug.Log($"Found loop end marker at time: {loopEndTime}");
                    }
                }
                
                // Check if we found both markers
                if (loopStartTime >= 0 && loopEndTime >= 0)
                {
                    Debug.Log($"Animation loop section defined in {clip.name}: {loopStartTime} to {loopEndTime}");
                    break;
                }
            }
        }
        
        // Warn if loop points weren't found but were requested
        if (useCustomLoopSection && (loopStartTime < 0 || loopEndTime < 0))
        {
            Debug.LogWarning($"Custom loop section enabled but markers '{loopStartMarker}' and '{loopEndMarker}' were not found in any animation!");
        }
    }
    
    /// <summary>
    /// Controls the animation loop logic
    /// </summary>
    private void ManageAnimationLoop()
    {
        // Check if we're in the Idle state
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") || 
            animator.GetCurrentAnimatorStateInfo(0).IsName("Movement"))
        {
            // Get normalized time and convert to actual time
            float stateTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            
            if (clipInfo.Length > 0)
            {
                float clipTime = stateTime * clipInfo[0].clip.length;
                
                // If we've reached the end of our loop section, jump back to start
                if (clipTime >= loopEndTime)
                {
                    // Set the normalized time to jump back to loop start
                    float normalizedLoopStart = loopStartTime / clipInfo[0].clip.length;
                    animator.Play("Idle", 0, normalizedLoopStart);
                    isInLoopSection = true;
                }
                else if (clipTime >= loopStartTime && !isInLoopSection)
                {
                    // Just entered the loop section
                    isInLoopSection = true;
                }
                else if (clipTime < loopStartTime)
                {
                    // Before the loop section
                    isInLoopSection = false;
                }
            }
        }
        else
        {
            // Not in Idle/Movement state
            isInLoopSection = false;
        }
    }
    
    // Animation Event Handlers
    // These are called directly from animation events
    
    // Called when loop section should start
    public void AE_LoopStart()
    {
        isInLoopSection = true;
        Debug.Log("Animation loop section started");
    }
    
    // Called when loop section should end and loop back
    public void AE_LoopEnd()
    {
        // This will reset back to loop start on next frame
        Debug.Log("Animation loop section ended, looping back");
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