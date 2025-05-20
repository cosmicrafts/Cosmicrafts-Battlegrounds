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
 * 3. Creates visual effects along the path
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
    [Tooltip("Whether to create visual trail effects")]
    public bool leavesTrail = true;
    [Tooltip("Prefab for the dash effect")]
    public GameObject dashEffectPrefab;

    [Header("Dash Animation")]
    [Tooltip("Controls how the dash accelerates and decelerates")]
    public AnimationCurve dashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0f, 2f)]
    [Tooltip("Higher values make the dash 'overshoot' the target before settling")]
    public float overshootAmount = 0.15f;
    [Range(0f, 1f)]
    [Tooltip("Controls how much the character slows down at the end of the dash")]
    public float endSlowdownFactor = 0.2f;
    [Range(0.1f, 2f)]
    [Tooltip("Controls the size of the trail elements")]
    public float trailScale = 0.3f;

    // Runtime variables
    private bool isDashing = false;
    private float dashTimer = 0f;
    private Vector3 dashStartPosition;
    private Vector3 dashEndPosition;
    private Vector3 dashDirection;
    private Unit playerUnit;
    private Collider playerCollider;
    private bool wasCollisionEnabled = true;

    // Trail effect pool
    private Queue<GameObject> markerPool = new Queue<GameObject>();
    private int poolSize = 50;

    protected override void Start()
    {
        base.Start();

        // Find the player's unit
        var (_, mainStationUnit) = SpellUtils.FindPlayerMainStation(MyTeam, PlayerId);
        playerUnit = mainStationUnit;

        if (playerUnit != null)
        {
            playerCollider = playerUnit.GetComponent<Collider>();
            InitializeMarkerPool();
            StartDash();
        }
        else
        {
            Debug.LogWarning("Could not find player unit for dash spell");
            Destroy(gameObject);
            return;
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

    private void InitializeMarkerPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            marker.SetActive(false);
            
            // Remove collider to prevent physics interactions
            Destroy(marker.GetComponent<Collider>());
            
            markerPool.Enqueue(marker);
        }
    }

    private void StartDash()
    {
        if (playerUnit == null) return;

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

        // If it's a blink, just teleport
        if (useBlink)
        {
            // Perform raycast to ensure we don't blink through walls
            RaycastHit hit;
            if (Physics.Raycast(dashStartPosition, dashDirection, out hit, dashDistance))
            {
                // If we hit something, blink to just before the hit point
                dashEndPosition = hit.point - (dashDirection * 0.5f);
            }

            // Teleport to end position
            playerUnit.transform.position = dashEndPosition;

            // Create effect if prefab is assigned
            if (dashEffectPrefab != null)
            {
                Instantiate(dashEffectPrefab, dashStartPosition, Quaternion.identity);
                Instantiate(dashEffectPrefab, dashEndPosition, Quaternion.identity);
            }

            // End dash immediately
            EndDash();
        }
    }

    private void ExecuteDash()
    {
        if (playerUnit == null) return;

        // Increment timer
        dashTimer += Time.deltaTime;

        // Calculate progress (0 to 1)
        float progress = Mathf.Clamp01(dashTimer / dashDuration);

        // Store previous position for trail effects
        Vector3 oldPosition = playerUnit.transform.position;

        // Apply animation curve for smooth motion
        float curvedProgress = dashCurve.Evaluate(progress);

        // Apply overshoot if configured
        float animatedProgress = curvedProgress;
        if (overshootAmount > 0 && progress > 0.5f && progress < 0.9f)
        {
            animatedProgress += Mathf.Sin((progress - 0.5f) * 3f * Mathf.PI) * overshootAmount * (1f - progress);
        }

        // Apply slight slowdown at the end
        if (progress > 0.7f)
        {
            float slowdownFactor = Mathf.Lerp(1f, 1f - endSlowdownFactor, (progress - 0.7f) / 0.3f);
            animatedProgress = Mathf.Lerp(curvedProgress, 1f, slowdownFactor);
        }

        // Calculate the current position along the dash path
        Vector3 targetPosition = Vector3.Lerp(dashStartPosition, dashEndPosition, animatedProgress);

        // Apply a subtle arc to the dash path
        if (progress > 0.1f && progress < 0.9f)
        {
            float arcHeight = dashDistance * 0.05f * Mathf.Sin(progress * Mathf.PI);
            targetPosition += Vector3.up * arcHeight;
        }

        // Move the player
        playerUnit.transform.position = targetPosition;

        // Create trail effects
        if (leavesTrail)
        {
            CreateTrailEffects(oldPosition, targetPosition, progress);
        }

        // Create destination effect near completion
        if (progress > 0.8f && progress < 0.85f && dashEffectPrefab != null)
        {
            Instantiate(dashEffectPrefab, dashEndPosition, Quaternion.identity);
        }

        // Check if dash is complete
        if (progress >= 1.0f)
        {
            EndDash();
        }
    }

    private void CreateTrailEffects(Vector3 oldPosition, Vector3 newPosition, float progress)
    {
        // Calculate trail density based on speed
        float distanceMoved = Vector3.Distance(oldPosition, newPosition);
        int trailCount = Mathf.Clamp(Mathf.CeilToInt(distanceMoved * 2f), 1, 3);

        for (int i = 0; i < trailCount; i++)
        {
            float lerpFactor = i / (float)trailCount;
            Vector3 trailPos = Vector3.Lerp(oldPosition, newPosition, lerpFactor);

            GameObject marker = GetMarker();
            
            // Scale marker based on speed and progress
            float dynamicScale = trailScale * (1f - 0.5f * progress);
            marker.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);
            marker.transform.position = trailPos;

            // Color progression
            if (marker.GetComponent<Renderer>())
            {
                Color startColor = new Color(1f, 0.8f, 0.2f);  // Vibrant yellow
                Color endColor = new Color(1f, 0.2f, 0.1f);    // Deep red
                Color color = Color.Lerp(startColor, endColor, progress);
                color.a = Mathf.Lerp(0.8f, 0.4f, progress);
                marker.GetComponent<Renderer>().material.color = color;
            }

            StartCoroutine(ReturnMarkerAfterDelay(marker, 0.7f * (1f - progress * 0.5f)));
        }

        // Create flash effects at start/end
        if (dashTimer < Time.deltaTime)
        {
            CreateFlashEffect(dashStartPosition, new Color(1f, 0.9f, 0.2f, 0.9f), 0.8f);
        }
    }

    private void CreateFlashEffect(Vector3 position, Color color, float scale)
    {
        GameObject flash = GetMarker();
        flash.transform.localScale = new Vector3(scale, scale, scale);
        flash.transform.position = position;
        if (flash.GetComponent<Renderer>())
        {
            flash.GetComponent<Renderer>().material.color = color;
        }
        StartCoroutine(ReturnMarkerAfterDelay(flash, 1.0f));
    }

    private void EndDash()
    {
        isDashing = false;

        // Restore collision
        if (playerCollider != null)
        {
            playerCollider.enabled = wasCollisionEnabled;
        }

        // Create end flash effect
        if (leavesTrail)
        {
            CreateFlashEffect(dashEndPosition, new Color(1f, 0.3f, 0.1f, 0.9f), 0.8f);
        }

        // Destroy the spell object after effects are done
        float cleanupDelay = leavesTrail ? 1.5f : 0.1f;
        Destroy(gameObject, cleanupDelay);
    }

    private GameObject GetMarker()
    {
        if (markerPool.Count > 0)
        {
            GameObject marker = markerPool.Dequeue();
            marker.SetActive(true);
            return marker;
        }
        return GameObject.CreatePrimitive(PrimitiveType.Sphere);
    }

    private void ReturnMarker(GameObject marker)
    {
        marker.SetActive(false);
        markerPool.Enqueue(marker);
    }

    private IEnumerator ReturnMarkerAfterDelay(GameObject marker, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnMarker(marker);
    }
}
} 