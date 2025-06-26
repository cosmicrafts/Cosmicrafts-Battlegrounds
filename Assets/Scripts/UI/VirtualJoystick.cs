using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cosmicrafts
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("UI References")]
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;
        [SerializeField] private RectTransform directionIndicator;
        
        [Header("Settings")]
        [SerializeField] private float handleRange = 1f;
        [SerializeField] private float deadZone = 0f;
        [SerializeField] private bool centerOnPress = true;
        [SerializeField] private bool showDirectionIndicator = true;

        private Canvas canvas;
        private Camera cam;
        private Vector2 input = Vector2.zero;
        private Vector2 center;
        
        private static VirtualJoystick instance;
        public static Vector2 Input => instance != null ? instance.input : Vector2.zero;
        public static bool IsActive => instance != null && instance.gameObject.activeSelf;

        // Tracking for multi-touch support
        private int controllingPointerId = -1;
        private bool isControlling = false;

        private void Awake()
        {
            instance = this;
            canvas = GetComponentInParent<Canvas>();
            cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
            
            if (joystickBackground != null)
            {
                center = joystickBackground.position;
                if (joystickHandle != null)
                {
                    joystickHandle.position = center;
                }
            }

            // Initialize direction indicator
            if (directionIndicator != null)
            {
                directionIndicator.gameObject.SetActive(showDirectionIndicator);
            }
        }

        private void OnEnable()
        {
            // Reset state when enabled
            ResetJoystick();
        }

        private void OnDisable()
        {
            // Reset state when disabled
            ResetJoystick();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Only accept pointer down if we're not already controlling
            if (!isControlling)
            {
                // Get pointer ID for tracking
                controllingPointerId = eventData.pointerId;
                isControlling = true;
                
                // Notify InputManager of the pointer ID we're using
                if (controllingPointerId >= 0)
                {
                    InputManager.SetJoystickTouchId(controllingPointerId);
                }
                
                if (centerOnPress)
                {
                    joystickBackground.position = eventData.position;
                }
                
                center = joystickBackground.position;
                joystickHandle.position = eventData.position;
                input = Vector2.zero;
                
                // Apply initial drag
                OnDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Only process if this is the pointer we're tracking
            if (isControlling && eventData.pointerId == controllingPointerId)
            {
                Vector2 direction = eventData.position - (Vector2)center;
                
                // Calculate normalized input
                if (direction.magnitude > handleRange)
                {
                    input = direction.normalized;
                }
                else
                {
                    input = direction / handleRange;
                }
                
                // Apply dead zone
                if (input.magnitude < deadZone)
                {
                    input = Vector2.zero;
                }
                
                // Update handle position
                Vector2 handlePosition = (Vector2)center + (input * handleRange);
                joystickHandle.position = handlePosition;

                // Update direction indicator rotation and position
                if (directionIndicator != null && showDirectionIndicator)
                {
                    if (input != Vector2.zero)
                    {
                        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
                        directionIndicator.rotation = Quaternion.Euler(0, 0, angle);
                        directionIndicator.position = center;
                        directionIndicator.gameObject.SetActive(true);

                        // Counter-rotate the background
                        if (joystickBackground != null)
                        {
                            joystickBackground.rotation = Quaternion.Euler(0, 0, -angle);
                        }
                    }
                    else
                    {
                        directionIndicator.gameObject.SetActive(false);
                        // Reset background rotation
                        if (joystickBackground != null)
                        {
                            joystickBackground.rotation = Quaternion.identity;
                        }
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Only process if this is the pointer we're tracking
            if (isControlling && eventData.pointerId == controllingPointerId)
            {
                ResetJoystick();
            }
        }
        
        private void ResetJoystick()
        {
            input = Vector2.zero;
            
            // Reset handle position
            if (joystickHandle != null && joystickBackground != null)
            {
                center = joystickBackground.position;
                joystickHandle.position = center;
                joystickBackground.rotation = Quaternion.identity; // Reset background rotation
            }
            
            // Reset direction indicator
            if (directionIndicator != null)
            {
                directionIndicator.gameObject.SetActive(false);
            }
            
            isControlling = false;
            
            // Tell InputManager we're no longer using this touch
            if (controllingPointerId >= 0)
            {
                InputManager.ClearJoystickTouchId();
            }
            
            controllingPointerId = -1;
        }
        
        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
} 