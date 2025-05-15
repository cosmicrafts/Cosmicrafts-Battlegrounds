using UnityEngine;
using UnityEngine.InputSystem;

namespace Cosmicrafts
{
    public static class InputManager
    {
        private static GameInputActions inputActions;
        private static bool isMobile = false;

        static InputManager()
        {
            inputActions = new GameInputActions();
            inputActions.Enable();
            
            // Check if we're on a mobile platform
            #if UNITY_ANDROID || UNITY_IOS
                isMobile = true;
            #endif
        }

        public static Vector2 GetMoveInput()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            #if UNITY_EDITOR
            // In editor, allow both WASD and joystick
            Vector2 keyboardInput = inputActions.Player.Move.ReadValue<Vector2>();
            if (keyboardInput != Vector2.zero)
            {
                return keyboardInput;
            }
            return VirtualJoystick.IsActive ? VirtualJoystick.Input : Vector2.zero;
            #else
            if (isMobile && VirtualJoystick.IsActive)
            {
                return VirtualJoystick.Input;
            }
            return inputActions.Player.Move.ReadValue<Vector2>();
            #endif
        }

        public static Vector3 GetMouseWorldPosition()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            Vector2 screenPos;
            if (isMobile && Input.touchCount > 0)
            {
                screenPos = Input.GetTouch(0).position;
            }
            else
            {
                screenPos = inputActions.Player.MousePosition.ReadValue<Vector2>();
            }

            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, 1 << 9))
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        public static Vector2 GetMousePosition()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            if (isMobile && Input.touchCount > 0)
            {
                return Input.GetTouch(0).position;
            }
            
            return inputActions.Player.MousePosition.ReadValue<Vector2>();
        }

        public static Vector2 GetZoomInput()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            if (isMobile)
            {
                // Handle pinch zoom on mobile
                if (Input.touchCount == 2)
                {
                    Touch touch0 = Input.GetTouch(0);
                    Touch touch1 = Input.GetTouch(1);

                    Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                    Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                    float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
                    float currentMagnitude = (touch0.position - touch1.position).magnitude;

                    float difference = currentMagnitude - prevMagnitude;
                    return new Vector2(0, difference * 0.01f);
                }
                return Vector2.zero;
            }
            
            return inputActions.Player.Zoom.ReadValue<Vector2>();
        }

        public static bool GetDashPressed()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            if (isMobile)
            {
                // You might want to add a UI button for dash on mobile
                // For now, returning false to avoid null reference errors
                return false;
            }
            
            return inputActions.Player.Dash.WasPressedThisFrame();
        }
    }
} 