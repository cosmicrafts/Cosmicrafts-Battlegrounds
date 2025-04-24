namespace Cosmicrafts {
    using UnityEngine;
    using System.Collections;

/*
 * The animation controller script for units
 * 
 * This handles all animation states for spaceships and units:
 * - Idle: Default state when not moving
 * - Moving: When the unit is in motion (looping animation)
 * - Attacking: When the unit is firing weapons (looping animation)
 * - Warp: Special effect for teleporting/dashing (one-shot animation)
 * - PowerUp: Special effect for spell buffs (one-shot animation)
 * - Entry: Intro animation played when unit first appears (one-shot animation)
 * - Death: Final animation on unit destruction
 */

public class UnitAnimLis : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Blend time between idle and movement animations")]
    public float movementBlendTime = 0.25f;
    [Tooltip("Use attack animation speed based on weapon cooldown")]
    public bool syncAttackWithWeaponSpeed = true;
    [Tooltip("Enable debug logs for animation state changes")]
    public bool debugAnimations = false;
    [Tooltip("Automatically play entry animation when unit spawns")]
    public bool autoPlayEntryAnimation = true;
    
    [Header("Advanced Animation")]
    [Tooltip("Enable automatic animator parameter updates")]
    public bool autoUpdateAnimatorParams = true;
    [Tooltip("Update frequency in seconds")]
    public float updateFrequency = 0.1f;
    
    // Unit data reference
    private Unit myUnit;
    // Shooter reference
    private Shooter myShooter;
    // Player movement reference (if this is a player unit)
    private PlayerMovement playerMovement;
    // Animator reference
    private Animator animator;
    
    // Animation state tracking
    private bool wasMoving = false;
    private bool wasAttacking = false;
    private bool isDead = false;
    private bool hasPlayedEntryAnimation = false;
    
    // Update timer
    private float updateTimer = 0f;
    
    // Start is called before the first frame update
    void Start()
    {
        // Get unit data
        myUnit = transform.parent.GetComponent<Unit>();
        
        // Get shooter component if available
        myShooter = transform.parent.GetComponent<Shooter>();
        
        // Get player movement component if available
        playerMovement = transform.parent.GetComponent<PlayerMovement>();
        
        // Get animator component
        animator = GetComponent<Animator>();
        
        // Ensure the animator exists
        if (animator == null)
        {
            Debug.LogError($"No Animator component found on {gameObject.name}! Animation control will not function.");
            return;
        }
        
        // Verify animator has required parameters
        VerifyAnimatorParameters();
        
        // Set initial animation states
        UpdateAnimationStates();
        
        // Configure attack animation speed if needed
        if (syncAttackWithWeaponSpeed && myShooter != null && animator != null)
        {
            AnimationClip attackClip = GetAnimationClip("Attack");
            if (attackClip != null && myShooter.CoolDown > 0)
            {
                float attackSpeed = attackClip.length / myShooter.CoolDown;
                animator.SetFloat("AttackSpeed", attackSpeed);
                
                if (debugAnimations)
                {
                    Debug.Log($"Attack animation speed set to {attackSpeed} based on weapon cooldown {myShooter.CoolDown}s");
                }
            }
        }
        
        // Play entry animation if enabled
        if (autoPlayEntryAnimation && HasParameter("Entry") && !hasPlayedEntryAnimation)
        {
            PlayEntryAnimation();
        }
    }
    
    // Verify animator has required parameters
    private void VerifyAnimatorParameters()
    {
        if (animator == null) return;
        
        // Verify essential parameters
        CheckParameter("Idle", AnimatorControllerParameterType.Bool);
        CheckParameter("Moving", AnimatorControllerParameterType.Bool);
        CheckParameter("Attacking", AnimatorControllerParameterType.Bool);
        CheckParameter("Die", AnimatorControllerParameterType.Trigger);
        CheckParameter("Warp", AnimatorControllerParameterType.Trigger);
        CheckParameter("PowerUp", AnimatorControllerParameterType.Trigger);
        CheckParameter("Entry", AnimatorControllerParameterType.Trigger);
        
        // Optional parameters
        CheckParameter("AttackSpeed", AnimatorControllerParameterType.Float, false);
        CheckParameter("MoveSpeed", AnimatorControllerParameterType.Float, false);
    }
    
    // Check if parameter exists and is the right type
    private void CheckParameter(string paramName, AnimatorControllerParameterType expectedType, bool required = true)
    {
        bool paramExists = false;
        bool typeCorrect = false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
            {
                paramExists = true;
                typeCorrect = param.type == expectedType;
                break;
            }
        }
        
        if (required && !paramExists)
        {
            Debug.LogWarning($"Required animator parameter '{paramName}' missing on {gameObject.name}. Add a {expectedType} parameter.");
        }
        else if (paramExists && !typeCorrect)
        {
            Debug.LogWarning($"Animator parameter '{paramName}' exists but is wrong type. Should be {expectedType}.");
        }
    }
    
    // Get AnimationClip by name
    private AnimationClip GetAnimationClip(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return null;
        
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name.Contains(clipName))
            {
                return clip;
            }
        }
        
        return null;
    }
    
    // Update is called every frame
    void Update()
    {
        // Skip if unit or animator is missing
        if (myUnit == null || animator == null) return;
        
        // Don't update animations if unit is dead
        if (myUnit.GetIsDeath())
        {
            if (!isDead)
            {
                isDead = true;
                // This will be handled by the Die() call in Unit.cs
            }
            return;
        }
        
        // Only update on timer if auto-update is enabled
        if (autoUpdateAnimatorParams)
        {
            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateFrequency;
                UpdateAnimationStates();
            }
        }
        
        // Always check for warp animation from player movement
        if (playerMovement != null && playerMovement.IsWarping)
        {
            if (debugAnimations)
            {
                Debug.Log($"Triggered Warp animation from PlayerMovement on {gameObject.name}");
            }
            animator.SetTrigger("Warp");
        }
    }
    
    // Update animation states based on unit properties
    public void UpdateAnimationStates()
    {
        if (myUnit == null || animator == null || myUnit.GetIsDeath()) return;
        
        // Get current states
        bool isMoving = myUnit.IsMoving();
        bool isAttacking = myShooter != null && myShooter.IsEngagingTarget();
        
        // Set animator parameters
        animator.SetBool("Moving", isMoving);
        animator.SetBool("Attacking", isAttacking);
        animator.SetBool("Idle", !isMoving && !isAttacking);
        
        // Set normalized move speed for blend trees (if exists)
        if (HasParameter("MoveSpeed"))
        {
            animator.SetFloat("MoveSpeed", myUnit.GetNormalizedSpeed());
        }
        
        // Log state changes for debugging
        if (debugAnimations && (isMoving != wasMoving || isAttacking != wasAttacking))
        {
            Debug.Log($"Animation state change for {gameObject.name}: Moving={isMoving}, Attacking={isAttacking}");
        }
        
        // Remember states for next frame
        wasMoving = isMoving;
        wasAttacking = isAttacking;
    }
    
    // Check if animator has a specific parameter
    private bool HasParameter(string paramName)
    {
        if (animator == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        
        return false;
    }
    
    // Play entry animation manually
    public void PlayEntryAnimation()
    {
        if (animator != null && HasParameter("Entry"))
        {
            if (debugAnimations)
            {
                Debug.Log($"Playing Entry animation on {gameObject.name}");
            }
            
            animator.SetTrigger("Entry");
            hasPlayedEntryAnimation = true;
        }
    }

    // Animation Event Handlers
    // These are called directly from animation events

    // Called when entry animation starts
    public void AE_EntryStart()
    {
        if (debugAnimations)
        {
            Debug.Log($"Entry animation started on {gameObject.name}");
        }
        
        // Add entry visual effects, particle systems, etc.
    }
    
    // Called when entry animation ends
    public void AE_EntryEnd()
    {
        if (debugAnimations)
        {
            Debug.Log($"Entry animation completed on {gameObject.name}");
        }
        
        // Can enable unit interactions or behaviors after intro completes
    }
    
    // Called when a power-up animation starts
    public void AE_PowerUpStart()
    {
        if (debugAnimations)
        {
            Debug.Log($"Power-Up animation started on {gameObject.name}");
        }
        
        // Add visual effects, particle systems, etc.
    }
    
    // Called when a power-up animation ends
    public void AE_PowerUpEnd()
    {
        if (debugAnimations)
        {
            Debug.Log($"Power-Up animation completed on {gameObject.name}");
        }
    }
    
    // Called when warp animation starts
    public void AE_WarpStart()
    {
        if (debugAnimations)
        {
            Debug.Log($"Warp animation started on {gameObject.name}");
        }
        
        // Add warp visual effects, particle systems, etc.
    }
    
    // Called when warp animation ends
    public void AE_WarpEnd()
    {
        if (debugAnimations)
        {
            Debug.Log($"Warp animation completed on {gameObject.name}");
        }
    }
    
    // Called when attack animation starts
    public void AE_AttackStart()
    {
        if (debugAnimations)
        {
            Debug.Log($"Attack animation started on {gameObject.name}");
        }
    }
    
    // Called when attack animation reaches its firing point
    public void AE_FireWeapon()
    {
        if (debugAnimations)
        {
            Debug.Log($"Fire weapon event on {gameObject.name}");
        }
        
        // Could trigger weapon effects here
    }

    // Called when the death animation ends
    public void AE_EndDeath()
    {
        // Don't destroy units, reset them for infinite play instead
        if (myUnit != null)
        {
            // For Red team (enemy) units, respawn them
            if (FactionManager.ConvertFactionToTeam(myUnit.MyFaction) == Team.Red)
            {
                Debug.Log($"Respawning enemy unit {myUnit.gameObject.name} instead of destroying it");
                
                // Reset the unit instead of destroying it
                myUnit.ResetUnit();
                
                // Teleport back to a suitable position if needed
                if (GameMng.GM != null)
                {
                    Vector3 respawnPos = GameMng.GM.GetDefaultTargetPosition(Team.Red);
                    myUnit.transform.position = respawnPos + new Vector3(
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
                Debug.Log($"Player unit death animation completed: {myUnit.gameObject.name}");
            }
        }
    }

    // Called to trigger an explosion effect
    public void AE_BlowUpUnit()
    {
        // Kill the unit
        myUnit.BlowUpEffect();
    }
}
}