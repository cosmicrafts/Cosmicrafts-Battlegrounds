namespace Cosmicrafts {
    
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
 * Spell 03 - Dash/Blink
 * 
 * A mobility spell that allows quick movement in a direction:
 * 1. Can be configured as a dash (smooth movement) or blink (instant teleport)
 * 2. Uses player's movement direction or facing direction
 * 3. VFX follows the unit's tail transform
 */
public class Spell_03 : Spell
{
    [Header("Dash Settings")]
    [Tooltip("Distance covered by the dash")]
    public float dashDistance = 15f;
    [Tooltip("Duration of the dash movement")]
    public float dashDuration = 0.1f;
    [Tooltip("Whether to instantly teleport instead of dashing")]
    public bool useBlink = false;

    [Header("Dash Animation")]
    [Tooltip("Controls how the dash accelerates and decelerates")]
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Higher values make the dash 'overshoot' the target before settling")]
    [Range(0f, 2f)]
    public float overshootAmount = 0.15f;
    [Tooltip("Controls how much the character slows down at the end of the dash")]
    [Range(0f, 1f)]
    public float endSlowdownFactor = 0.2f;
    
    [Header("Advanced Motion")]
    [Tooltip("Adds a vertical arc to the dash")]
    [Range(0f, 2f)]
    public float arcHeight = 0.5f;
    [Tooltip("Controls the smoothness of direction changes during dash")]
    [Range(1f, 20f)]
    public float turnSpeed = 10f;

    // Runtime variables
    private bool isDashing = false;
    private float dashTimer = 0f;
    private Vector3 dashStartPosition;
    private Vector3 dashEndPosition;
    private Vector3 dashDirection;
    private Unit playerUnit;
    private Collider playerCollider;
    private bool wasCollisionEnabled = true;

    void Awake()
    {
        Debug.Log($"Spell_03 (Dash) Awake called");
    }

    public override void SetNfts(NFTsSpell nFTsSpell)
    {
        Debug.Log($"Spell_03 SetNfts called with NFT: {(nFTsSpell != null ? nFTsSpell.KeyId : "null")}");
        base.SetNfts(nFTsSpell);
        
        if (nFTsSpell == null)
        {
            Debug.LogWarning($"Spell_03 SetNfts early return - nFTsSpell is null");
            return;
        }

        NFTs = nFTsSpell;
        Debug.Log($"Spell_03 (Dash) initialized with NFT data. Key: {NFTs.KeyId}, Team: {MyTeam}, PlayerId: {PlayerId}");
    }

    protected override void Start()
    {
        base.Start();

        // Get the player directly since we have the PlayerId and Team
        Player player = GameMng.P;
        if (player != null && player.ID == PlayerId && player.MyTeam == MyTeam)
        {
            // Get the Unit component directly from the player
            playerUnit = player.GetComponent<Unit>();
            if (playerUnit != null)
            {
                playerCollider = playerUnit.GetComponent<Collider>();
                
                // Debug log to check if we have the tail point
                if (playerUnit.TailPoint != null)
                {
                    Debug.Log($"Found player unit tail point at {playerUnit.TailPoint.position}");
                }
                else
                {
                    Debug.LogWarning("Player unit found but TailPoint is not set!");
                }
                
                StartDash();
            }
            else
            {
                Debug.LogError("Player GameObject found but missing Unit component!");
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.LogError($"Could not find player. ID: {PlayerId}, Team: {MyTeam}");
            Destroy(gameObject);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isDashing)
        {
            ExecuteDash();
        }
    }

    private void StartDash()
    {
        if (playerUnit == null)
        {
            Debug.LogError("Spell_03 StartDash called with null playerUnit");
            return;
        }

        Debug.Log($"Spell_03 StartDash - Getting movement direction for player at {playerUnit.transform.position}");

        // Get player's movement direction or facing direction
        dashDirection = playerUnit.transform.forward;
        PlayerMovement movement = playerUnit.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            Vector3 moveDir = movement.GetLastMoveDirection();
            if (moveDir.sqrMagnitude > 0.01f)
            {
                dashDirection = moveDir;
            }
        }

        Debug.Log($"Spell_03 dash direction set to {dashDirection}");

        // Calculate start and end positions
        dashStartPosition = playerUnit.transform.position;
        dashEndPosition = dashStartPosition + (dashDirection * dashDistance);

        // Start the dash
        isDashing = true;
        dashTimer = 0f;

        // Temporarily disable collisions during the dash
        if (playerCollider != null)
        {
            wasCollisionEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        // If using blink, teleport immediately
        if (useBlink)
        {
            RaycastHit hit;
            if (Physics.Raycast(dashStartPosition, dashDirection, out hit, dashDistance))
            {
                dashEndPosition = hit.point - (dashDirection * 0.5f);
                Debug.Log($"Spell_03 blink hit obstacle at {hit.point}, adjusted end position to {dashEndPosition}");
            }

            playerUnit.transform.position = dashEndPosition;
            EndDash();
        }

        // Parent this spell object to the unit's tail point if it exists
        if (playerUnit.TailPoint != null)
        {
            transform.SetParent(playerUnit.TailPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void ExecuteDash()
    {
        if (playerUnit == null) return;

        dashTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(dashTimer / dashDuration);
        float curvedProgress = dashCurve.Evaluate(progress);

        // Calculate advanced motion effects
        Vector3 targetPosition = Vector3.Lerp(dashStartPosition, dashEndPosition, curvedProgress);

        // Add arc
        if (arcHeight > 0)
        {
            float arcOffset = Mathf.Sin(progress * Mathf.PI) * arcHeight;
            targetPosition += Vector3.up * arcOffset;
        }

        // Apply overshoot
        if (overshootAmount > 0 && progress > 0.5f && progress < 0.9f)
        {
            float overshoot = Mathf.Sin((progress - 0.5f) * 3f * Mathf.PI) * overshootAmount * (1f - progress);
            targetPosition = Vector3.Lerp(targetPosition, dashEndPosition + dashDirection * overshoot, (progress - 0.5f) * 2);
        }

        // Apply end slowdown
        if (progress > 0.7f)
        {
            float slowdown = Mathf.Lerp(1f, 1f - endSlowdownFactor, (progress - 0.7f) / 0.3f);
            targetPosition = Vector3.Lerp(targetPosition, dashEndPosition, slowdown);
        }

        // Move the player
        playerUnit.transform.position = targetPosition;

        // Check if dash is complete
        if (progress >= 1.0f)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        Debug.Log($"Spell_03 EndDash called. Position: {(playerUnit != null ? playerUnit.transform.position.ToString() : "null player")}");
        isDashing = false;

        // Restore collision
        if (playerCollider != null)
        {
            playerCollider.enabled = wasCollisionEnabled;
        }

        // Let the VFX finish playing before destroying
        Destroy(gameObject, 2f);
    }
}
} 