using UnityEngine;
using UnityEngine.UI; // Required for UI
using UnityEngine.InputSystem;
using Cosmicrafts;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float zoomSmoothTime = 0.1f;
    public float minZoom = 25f;
    public float maxZoom = 100f;
    [Tooltip("Multiplier for mouse wheel sensitivity")]
    public float mouseWheelMultiplier = 3f;

    [Header("Follow Settings")]
    public Transform targetToFollow; // This will be the base station
    public Vector3 offset = new Vector3(0, 90f, -30f); // Back to original offset
    public bool followTarget = true;
    
    [Header("UI Buttons (Optional)")]
    public Button zoomInButton;
    public Button zoomOutButton;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity = 0f;
    private bool isSearchingForBaseStation = true;
    private float searchInterval = 0.5f;
    private float nextSearchTime = 0f;
    
    // Track the last frame we received input to avoid processing duplicate events
    private int lastInputFrame = -1;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;

        // Assign button events (if buttons exist)
        if (zoomInButton != null) zoomInButton.onClick.AddListener(ZoomIn);
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(ZoomOut);
        
        // Try to find the base station immediately
        FindBaseStation();
        
        // Subscribe to scroll events from Input System
        InputManager.SubscribeToZoomInput(HandleZoomInput);
        
        Debug.Log("Camera controller initialized with Input System zoom support");
    }
    
    void OnDestroy()
    {
        // Clean up subscription
        InputManager.UnsubscribeFromZoomInput(HandleZoomInput);
    }

    void Update()
    {
        // Process mouse wheel zoom via Input System callback
        // Now handled in HandleZoomInput method
        
        // Handle pinch zoom for mobile devices
        if (Cosmicrafts.InputManager.IsMobile())
        {
            // Process pinch zoom on mobile
            Vector2 pinchInput = Cosmicrafts.InputManager.GetZoomInput();
            if (pinchInput.y != 0f)
            {
                targetZoom -= pinchInput.y * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                Debug.Log($"Pinch zoom detected: {pinchInput.y}, target zoom: {targetZoom}");
            }
        }
        
        // Still handle the smooth zoom application here
        ApplyZoom();
        
        // Periodically try to find the base station if we haven't found it yet
        if (isSearchingForBaseStation && Time.time > nextSearchTime)
        {
            FindBaseStation();
            nextSearchTime = Time.time + searchInterval;
        }
    }
    
    void LateUpdate()
    {
        // Simple following - no extra adjustments
        if (targetToFollow != null && followTarget)
        {
            // Just directly set position to target + offset
            transform.position = targetToFollow.position + offset;
        }
    }
    
    // This will be called by InputManager when zoom input is detected
    private void HandleZoomInput(InputAction.CallbackContext context)
    {
        // Only process if this is a new frame to avoid duplicate processing
        if (lastInputFrame == Time.frameCount)
            return;
            
        lastInputFrame = Time.frameCount;
        
        // Get the scroll value (typically y component of Vector2)
        Vector2 scrollValue = context.ReadValue<Vector2>();
        float scrollInput = scrollValue.y * mouseWheelMultiplier;
        
        // Mouse wheel input is inverted by default in Unity, so we flip it
        scrollInput = -scrollInput;
        
        if (scrollInput != 0f)
        {
            // Apply zoom change
            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            Debug.Log($"Mouse wheel zoom: {scrollInput}, target zoom: {targetZoom}");
        }
    }
    
    // Apply the zoom smoothly
    private void ApplyZoom()
    {
        // Apply smooth zoom transition
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
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
            // Get the player's team
            Cosmicrafts.Team playerTeam = Cosmicrafts.Team.Blue; // Default
            
            if (Cosmicrafts.GameMng.P != null)
            {
                playerTeam = Cosmicrafts.GameMng.P.MyTeam;
            }
            
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

    // Legacy method for mouse wheel zoom, now called by HandleZoomInput
    void HandleZoom()
    {
        // This method is no longer needed as zoom is handled by HandleZoomInput
        // Keeping it for backwards compatibility
        Vector2 scroll = Cosmicrafts.InputManager.GetZoomInput();
        float scrollInput = scroll.y;

        if (scrollInput != 0f)
        {
            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }

    public void ZoomIn()
    {
        targetZoom -= zoomSpeed;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    public void ZoomOut()
    {
        targetZoom += zoomSpeed;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
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
}
