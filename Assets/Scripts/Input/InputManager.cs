using UnityEngine;
using UnityEngine.InputSystem;

namespace Cosmicrafts
{
    public static class InputManager
    {
        private static GameInputActions inputActions;

        static InputManager()
        {
            inputActions = new GameInputActions();
            inputActions.Enable();
        }

        public static Vector2 GetMoveInput()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }
            return inputActions.Player.Move.ReadValue<Vector2>();
        }

        public static Vector3 GetMouseWorldPosition()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
            }
            Vector2 mousePos = inputActions.Player.MousePosition.ReadValue<Vector2>();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
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
            return inputActions.Player.MousePosition.ReadValue<Vector2>();
        }

        public static Vector2 GetZoomInput()
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();
                inputActions.Enable();
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
    }
} 