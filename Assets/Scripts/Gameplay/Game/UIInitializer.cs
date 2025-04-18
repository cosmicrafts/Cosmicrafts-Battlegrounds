using UnityEngine;
using UnityEngine.UI;

namespace Cosmicrafts
{
    /// <summary>
    /// Helper script to initialize UI components at runtime
    /// Handles making sure all card handlers are set up properly
    /// </summary>
    public class UIInitializer : MonoBehaviour
    {
        [Header("Card UI Settings")]
        [Tooltip("Parent transform containing all card UI elements")]
        public Transform cardDeckContainer;
        
        [Header("Drag Unit Settings")]
        [Tooltip("Reference to the DragUnitCtrl in the scene")]
        public DragUnitCtrl dragUnitControl;
        
        private void Start()
        {
            EnsureCardHandlersSetup();
            EnsureDragUnitControlExists();
        }
        
        private void EnsureCardHandlersSetup()
        {
            if (cardDeckContainer == null)
            {
                Debug.LogWarning("Card deck container not assigned. Cannot set up card handlers.");
                return;
            }
            
            // Find all UIGameCard components in the card deck container
            UIGameCard[] gameCards = cardDeckContainer.GetComponentsInChildren<UIGameCard>();
            if (gameCards == null || gameCards.Length == 0)
            {
                Debug.LogWarning("No UI game cards found in the container.");
                return;
            }
            
            // Make sure each card has a UIGameCardHandler component
            for (int i = 0; i < gameCards.Length; i++)
            {
                UIGameCard card = gameCards[i];
                if (card == null) continue;
                
                // Get or add card handler component
                UIGameCardHandler handler = card.GetComponent<UIGameCardHandler>();
                if (handler == null)
                {
                    handler = card.gameObject.AddComponent<UIGameCardHandler>();
                }
                
                // Set the card index based on its position in the container
                handler.cardIndex = i;
                
                // Make sure it has an EventTrigger component
                if (card.GetComponent<UnityEngine.EventSystems.EventTrigger>() == null)
                {
                    card.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                }
            }
            
            Debug.Log($"Successfully set up {gameCards.Length} card handlers.");
        }
        
        private void EnsureDragUnitControlExists()
        {
            if (dragUnitControl != null)
            {
                // DragUnitCtrl is already assigned, make sure it's properly set up
                return;
            }
            
            // Try to find DragUnitCtrl in the scene
            DragUnitCtrl existingControl = FindFirstObjectByType<DragUnitCtrl>();
            if (existingControl != null)
            {
                dragUnitControl = existingControl;
                return;
            }
            
            // Create a new DragUnitCtrl if none exists
            GameObject dragControlObject = new GameObject("DragUnitControl");
            dragUnitControl = dragControlObject.AddComponent<DragUnitCtrl>();
            
            // Set up basic components needed for the drag controller
            // This is a simplified setup - you may need to customize this based on your specific needs
            dragControlObject.AddComponent<SphereCollider>().isTrigger = true;
            
            // Create a mesh object child for previews
            GameObject meshObject = new GameObject("PreviewMesh");
            meshObject.transform.SetParent(dragControlObject.transform);
            
            // Add required components
            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            EPOOutline.Outlinable outlinable = meshObject.AddComponent<EPOOutline.Outlinable>();
            
            // Assign components to DragUnitCtrl
            dragUnitControl.MyMesh = meshRenderer;
            dragUnitControl.MyMeshFilter = meshFilter;
            dragUnitControl.Outline = outlinable;
            
            Debug.Log("Created new DragUnitCtrl object in the scene.");
        }
    }
} 