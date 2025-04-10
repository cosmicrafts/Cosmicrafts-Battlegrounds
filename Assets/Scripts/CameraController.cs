using UnityEngine;
using UnityEngine.UI; // Required for UI

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float zoomSmoothTime = 0.1f;
    public float minZoom = 25f;
    public float maxZoom = 100f;

    [Header("Pan Settings")]
    public float panSpeed = 0.5f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 5f;

    [Header("UI Buttons (Optional)")]
    public Button zoomInButton;
    public Button zoomOutButton;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity = 0f;
    private Vector3 lastMousePosition;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize; // ✅ Corrected for Orthographic camera

        // Assign button events (if buttons exist)
        if (zoomInButton != null) zoomInButton.onClick.AddListener(ZoomIn);
        if (zoomOutButton != null) zoomOutButton.onClick.AddListener(ZoomOut);
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        HandleRotation();
    }

    void HandleZoom()
    {
        // ✅ Fix: Adjust orthographicSize instead of fieldOfView
        float scrollInput = Input.mouseScrollDelta.y;

        if (scrollInput != 0f)
        {
            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // Apply smooth zoom transition
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
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

    void HandlePan()
    {
        if (Input.GetMouseButtonDown(1))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            transform.Translate(-delta.x * panSpeed * Time.deltaTime, -delta.y * panSpeed * Time.deltaTime, 0, Space.Self);
            lastMousePosition = Input.mousePosition;
        }
    }

    void HandleRotation()
    {
        if (Input.GetMouseButtonDown(2))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            transform.Rotate(Vector3.up, -delta.x * rotationSpeed * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.right, delta.y * rotationSpeed * Time.deltaTime, Space.Self);
            lastMousePosition = Input.mousePosition;
        }
    }
}
