using UnityEngine;
using UnityEngine.UI; // Required for UI
using Cosmicrafts; // For faction access

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float zoomSmoothTime = 0.1f;
    public float minZoom = 25f;
    public float maxZoom = 100f;

    [Header("Follow Settings")]
    public Transform targetToFollow; // This will be the base station
    public Vector3 offset = new Vector3(0, 90f, -30f); // Back to original offset
    public bool followTarget = true;
    
    [Header("Death Camera Settings")]
    public float deathZoomIn = 40f; // Zoom in amount during death
    public float deathZoomTransitionSpeed = 2f; // How fast to zoom
    public GameObject deathEffectOverlay; // Optional UI overlay for death effect
    
    [Header("UI Buttons (Optional)")]
    public Button zoomInButton;
    public Button zoomOutButton;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity = 0f;
    private bool isSearchingForBaseStation = true;
    private float searchInterval = 0.5f;
    private float nextSearchTime = 0f;
    
    // Death camera state variables
    private float originalZoom;
    private bool inDeathSequence = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
        originalZoom = targetZoom;
        
        // Make sure death effect overlay is initially disabled
        if (deathEffectOverlay != null)
        {
            deathEffectOverlay.SetActive(false);
        }

        // Assign button events (if buttons exist)
        if (zoomInButton != null) zoomInButton.onClick.AddListener(ZoomIn);
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(ZoomOut);
        
        // Try to find the base station immediately
        FindBaseStation();
    }

    void Update()
    {
        HandleZoom();
        
        // Periodically try to find the base station if we haven't found it yet
        if (isSearchingForBaseStation && Time.time > nextSearchTime)
        {
            FindBaseStation();
            nextSearchTime = Time.time + searchInterval;
        }
    }
    
    void LateUpdate()
    {
        // Simple following with original offset
        if (targetToFollow != null && followTarget)
        {
            transform.position = targetToFollow.position + offset;
        }
    }
    
    void FindBaseStation()
    {
        // If we already have a target and it's still valid, don't search
        if (targetToFollow != null)
        {
            isSearchingForBaseStation = false;
            return;
        }
        
        // First check if GameMng.GM is initialized
        if (Cosmicrafts.GameMng.GM != null)
        {
            // Get the player's faction and convert to team if needed
            Cosmicrafts.Faction playerFaction = Cosmicrafts.Faction.Player; // Default
            
            if (Cosmicrafts.GameMng.P != null)
            {
                playerFaction = Cosmicrafts.GameMng.P.MyFaction;
            }
            
            // Convert to Team for backward compatibility
            Cosmicrafts.Team playerTeam = Cosmicrafts.FactionManager.ConvertFactionToTeam(playerFaction);
            
            // Get the correct base station based on team (index 1 for Blue, 0 for Red)
            int baseStationIndex = playerTeam == Cosmicrafts.Team.Blue ? 1 : 0;
            
            // Check if the Targets array is initialized and has the right index
            if (Cosmicrafts.GameMng.GM.Targets != null && 
                Cosmicrafts.GameMng.GM.Targets.Length > baseStationIndex && 
                Cosmicrafts.GameMng.GM.Targets[baseStationIndex] != null)
            {
                // We found our base station!
                targetToFollow = Cosmicrafts.GameMng.GM.Targets[baseStationIndex].transform;
                
                // Set camera position immediately
                transform.position = targetToFollow.position + offset;
                
                // We can stop searching now
                isSearchingForBaseStation = false;
            }
        }
    }

    void HandleZoom()
    {
        float scrollInput = Input.mouseScrollDelta.y;

        if (scrollInput != 0f && !inDeathSequence)
        {
            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // Apply smooth zoom transition
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
    }

    public void ZoomIn()
    {
        if (!inDeathSequence)
        {
            targetZoom -= zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }

    public void ZoomOut()
    {
        if (!inDeathSequence)
        {
            targetZoom += zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }
    
    // Method to manually set the follow target
    public void SetTarget(Transform newTarget)
    {
        targetToFollow = newTarget;
        followTarget = true;
        
        // Stop searching for base station
        isSearchingForBaseStation = false;
    }
    
    // Force a new search for the base station
    public void ForceBaseStationSearch()
    {
        isSearchingForBaseStation = true;
    }
    
    // Start the death camera sequence - simplified to just zoom in
    public void StartDeathSequence()
    {
        inDeathSequence = true;
        
        // Store original zoom and set target zoom for death - zoom in close
        originalZoom = cam.orthographicSize;
        targetZoom = deathZoomIn;
        
        // Enable death effect overlay if available
        if (deathEffectOverlay != null)
        {
            deathEffectOverlay.SetActive(true);
        }
    }
    
    // Reset camera to normal
    public void ResetCamera()
    {
        inDeathSequence = false;
        
        // Reset zoom to original
        targetZoom = originalZoom;
        
        // Disable death effect overlay
        if (deathEffectOverlay != null)
        {
            deathEffectOverlay.SetActive(false);
        }
    }
}
