using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Linq;

namespace Cosmicrafts
{
    public static class InputManager
    {
        private static GameInputActions inputActions;
        private static bool isMobile = false;
        
        // Cache of last known joystick pointer ID for avoiding conflicts
        private static int joystickPointerId = -1;

        static InputManager()
        {
            inputActions = new GameInputActions();
            inputActions.Enable();
            
            // Enable both Player and UI action maps
            inputActions.Player.Enable();
            inputActions.UI.Enable();
            
            // Check if we're on a mobile platform
            #if UNITY_ANDROID || UNITY_IOS
                isMobile = true;
            #endif
        }

        // Set the joystick pointer ID to exclude that pointer from UI operations
        public static void SetJoystickTouchId(int pointerId)
        {
            joystickPointerId = pointerId;
        }
        
        // Clear the joystick pointer ID when it's no longer active
        public static void ClearJoystickTouchId()
        {
            joystickPointerId = -1;
        }
        
        // Get the current joystick pointer ID
        public static int GetJoystickPointerId()
        {
            return joystickPointerId;
        }

        public static Vector2 GetMoveInput()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            // Use both joystick and keyboard on all platforms for better compatibility
            Vector2 keyboardInput = inputActions.Player.Move.ReadValue<Vector2>();
            
            // If keyboard/gamepad input is detected, prioritize it
            if (keyboardInput.sqrMagnitude > 0.01f)
            {
                return keyboardInput;
            }
            
            // Otherwise use virtual joystick if active
            if (VirtualJoystick.IsActive)
            {
                return VirtualJoystick.Input;
            }
            
            return Vector2.zero;
        }

        public static Vector3 GetMouseWorldPosition()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            Vector2 screenPos = GetPointerPosition();

            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, 1 << 9))
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        public static Vector2 GetMousePosition()
        {
            return GetPointerPosition();
        }

        public static Vector2 GetPointerPosition()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }

            // Check for touchscreen first
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                // If possible, use a touch that's not the joystick
                foreach (var touch in touchscreen.touches)
                {
                    // Skip if this is the joystick touch
                    if (touch.touchId.ReadValue() != joystickPointerId && touch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                    {
                        return touch.position.ReadValue();
                    }
                }
                
                // If we only have one touch or couldn't find a non-joystick touch, use the first touch
                return touchscreen.primaryTouch.position.ReadValue();
            }
            
            // Fall back to mouse position
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.position.ReadValue();
            }
            
            // Last resort, use the UI action
            return inputActions.UI.PointerPosition.ReadValue<Vector2>();
        }
        
        // Get a pointer position for UI interactions, explicitly avoiding the joystick touch
        public static Vector2 GetUIPointerPosition()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }
            
            // Check for touchscreen first
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                // Try to find a touch that's not the joystick
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.touchId.ReadValue() != joystickPointerId && touch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                    {
                        return touch.position.ReadValue();
                    }
                }
            }
            
            // Fall back to mouse position if no non-joystick touch is found
            var mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.position.ReadValue();
            }
            
            // Last resort, use the UI action
            return inputActions.UI.PointerPosition.ReadValue<Vector2>();
        }
        
        public static bool IsPrimaryActionPressed()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }
            
            // Check for touch input
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                // Look for a non-joystick touch that just began
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.touchId.ReadValue() != joystickPointerId && 
                        touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        return true;
                    }
                }
            }
            
            return inputActions.UI.PrimaryAction.WasPressedThisFrame();
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
                var touchscreen = Touchscreen.current;
                if (touchscreen != null && touchscreen.touches.Count >= 2)
                {
                    // Get two different touches (excluding joystick if possible)
                    TouchControl touch0 = null;
                    TouchControl touch1 = null;
                    
                    foreach (var touch in touchscreen.touches)
                    {
                        if (touch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                        {
                            if (touch0 == null)
                            {
                                touch0 = touch;
                            }
                            else if (touch1 == null && touch.touchId.ReadValue() != touch0.touchId.ReadValue())
                            {
                                touch1 = touch;
                                break;
                            }
                        }
                    }
                    
                    if (touch0 != null && touch1 != null)
                    {
                        // Current positions
                        Vector2 pos0 = touch0.position.ReadValue();
                        Vector2 pos1 = touch1.position.ReadValue();
                        
                        // Previous positions
                        Vector2 prevPos0 = pos0 - touch0.delta.ReadValue();
                        Vector2 prevPos1 = pos1 - touch1.delta.ReadValue();
                        
                        // Calculate the magnitudes
                        float prevMagnitude = (prevPos0 - prevPos1).magnitude;
                        float currentMagnitude = (pos0 - pos1).magnitude;
                        
                        float difference = currentMagnitude - prevMagnitude;
                        return new Vector2(0, difference * 0.01f);
                    }
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
            
            return inputActions.Player.Dash.WasPressedThisFrame();
        }
        
        public static bool IsCardSelected(int cardNumber)
        {
            // Allow for number keys 1-9 to select cards
            if (cardNumber < 1 || cardNumber > 9)
                return false;
                
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }
            
            // This uses the Input System package instead of the legacy Input.GetKeyDown
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            
            switch (cardNumber)
            {
                case 1: return keyboard.digit1Key.wasPressedThisFrame;
                case 2: return keyboard.digit2Key.wasPressedThisFrame;
                case 3: return keyboard.digit3Key.wasPressedThisFrame;
                case 4: return keyboard.digit4Key.wasPressedThisFrame;
                case 5: return keyboard.digit5Key.wasPressedThisFrame;
                case 6: return keyboard.digit6Key.wasPressedThisFrame;
                case 7: return keyboard.digit7Key.wasPressedThisFrame;
                case 8: return keyboard.digit8Key.wasPressedThisFrame;
                case 9: return keyboard.digit9Key.wasPressedThisFrame;
                default: return false;
            }
        }

        // Card selection callback handling
        private static System.Action<InputAction.CallbackContext>[] cardCallbacks = new System.Action<InputAction.CallbackContext>[9];
        
        public static void SubscribeToCardSelection(int cardNumber, System.Action<InputAction.CallbackContext> callback)
        {
            if (cardNumber < 1 || cardNumber > 8)
                return;
                
            cardCallbacks[cardNumber - 1] = callback;
        }
        
        public static void UnsubscribeFromCardSelection(int cardNumber, System.Action<InputAction.CallbackContext> callback)
        {
            if (cardNumber < 1 || cardNumber > 8)
                return;
                
            cardCallbacks[cardNumber - 1] = null;
        }
        
        // Primary action callback handling
        private static System.Action<InputAction.CallbackContext> primaryActionCallback;
        
        public static void SubscribeToPrimaryAction(System.Action<InputAction.CallbackContext> callback)
        {
            primaryActionCallback = callback;
        }
        
        public static void UnsubscribeFromPrimaryAction(System.Action<InputAction.CallbackContext> callback)
        {
            primaryActionCallback = null;
        }
    }
} 