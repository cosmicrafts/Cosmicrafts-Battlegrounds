using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Cosmicrafts
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("UI References")]
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;
        
        [Header("Settings")]
        [SerializeField] private float handleRange = 1f;
        [SerializeField] private float deadZone = 0f;
        [SerializeField] private bool centerOnPress = true;

        private Canvas canvas;
        private Camera cam;
        private Vector2 input = Vector2.zero;
        private Vector2 center;
        
        private static VirtualJoystick instance;
        public static Vector2 Input => instance != null ? instance.input : Vector2.zero;
        public static bool IsActive => instance != null && instance.gameObject.activeSelf;

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
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (centerOnPress)
            {
                joystickBackground.position = eventData.position;
            }
            
            center = joystickBackground.position;
            joystickHandle.position = eventData.position;
            input = Vector2.zero;
            
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 direction = eventData.position - (Vector2)center;
            input = (direction.magnitude > handleRange) ? direction.normalized : direction / handleRange;
            
            if (input.magnitude < deadZone)
            {
                input = Vector2.zero;
            }
            
            Vector2 handlePosition = (Vector2)center + (input * handleRange);
            joystickHandle.position = handlePosition;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            input = Vector2.zero;
            joystickHandle.position = center;
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