using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cosmicrafts;
using UnityEngine.InputSystem;

/*
 * This code represents a in the game card instance
 * The card has the UI refrences from the inspector, to show the Icon and energy cost NFT data
 */
public class UIGameCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //The UI card in game

    //Index of the card (0 - 8)
    public int IdCardDeck = 0;
    //Cost of the card
    public int EnergyCost = 99;
    //Text reference for the cost
    public Text TextCost;
    //Image reference for the card icon
    public Image SpIcon;
    //Selection Icon
    public GameObject Selection;
    
    // Reference to game manager and UI manager
    private UIGameMng uiGameMng;
    
    // For drag functionality
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private bool isDragging = false;
    
    // Store the pointerId that's controlling this card's drag
    private int dragPointerId = -1;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Create a CanvasGroup if it doesn't exist
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Get reference to game manager
        uiGameMng = Object.FindFirstObjectByType<UIGameMng>();
    }
    
    private void Start()
    {
        // Register for hotkey input if this card has an IdCardDeck between 1-8
        if (IdCardDeck >= 0 && IdCardDeck < 8)
        {
            // Use actual card index + 1 to match hotkey numbers (1-8)
            int hotkeyNumber = IdCardDeck + 1;
            
            // Subscribe to card selection events via InputManager
            InputManager.SubscribeToCardSelection(hotkeyNumber, OnCardHotkeyPressed);
            
            Debug.Log($"Card {IdCardDeck} registered for hotkey {hotkeyNumber}");
        }
    }
    
    private void OnCardHotkeyPressed(InputAction.CallbackContext context)
    {
        Debug.Log($"Hotkey pressed for card {IdCardDeck}");
        
        // Only trigger card if the context is performed
        if (context.performed)
        {
            // Get reference to Player
            Player player = GameMng.P;
            
            // Check if auto-deployment is enabled
            if (player != null && player.useAutoDeployment)
            {
                // Auto-deploy the card if we have enough energy
                if (player.CurrentEnergy >= EnergyCost)
                {
                    // Use UIGameMng to deploy the card
                    if (uiGameMng != null)
                    {
                        uiGameMng.DeployCard(IdCardDeck, Vector3.zero);
                    }
                }
                else
                {
                    Debug.Log($"Not enough energy to deploy card {IdCardDeck}");
                }
            }
            else
            {
                // Traditional selection if auto-deployment is disabled
                uiGameMng.DeselectCards();
                uiGameMng.SelectCard(IdCardDeck);
            }
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from card selection events
        if (IdCardDeck >= 0 && IdCardDeck < 8)
        {
            int hotkeyNumber = IdCardDeck + 1;
            InputManager.UnsubscribeFromCardSelection(hotkeyNumber, OnCardHotkeyPressed);
        }
    }

    //Shows or hides the selection icon
    public void SetSelection(bool selected)
    {
        Selection.SetActive(selected);
    }
    
    // IPointerClickHandler implementation
    public void OnPointerClick(PointerEventData eventData)
    {
        // Only handle click if we're not dragging
        if (!isDragging && uiGameMng != null)
        {
            // Check if auto-deployment is enabled in Player
            Player player = GameMng.P;
            if (player != null && player.useAutoDeployment)
            {
                // Get enough energy to deploy the card
                if (player.CurrentEnergy >= EnergyCost)
                {
                    // Auto-deploy card immediately
                    uiGameMng.DeployCard(IdCardDeck, Vector3.zero); // Position is determined in Player.cs
                }
                else
                {
                    // Not enough energy - provide visual feedback
                    Debug.Log($"Not enough energy to deploy card {IdCardDeck}. Need {EnergyCost}, have {Mathf.FloorToInt(player.CurrentEnergy)}");
                    // Flash the card red or play sound effect for feedback
                }
            }
            else
            {
                // Original toggle selection behavior if auto-deployment is disabled
                bool isCurrentlySelected = Selection.activeSelf;
                uiGameMng.DeselectCards();
                
                if (!isCurrentlySelected)
                {
                    uiGameMng.SelectCard(IdCardDeck);
                }
            }
        }
    }
    
    // IBeginDragHandler implementation
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Don't start dragging if this is the joystick's pointer
        if (uiGameMng != null && !IsDragPointerJoystick(eventData.pointerId))
        {
            // Store the pointer ID that's controlling this drag
            dragPointerId = eventData.pointerId;
            
            // Save original position for return if needed
            originalPosition = rectTransform.anchoredPosition;
            
            // Create visual feedback that we're dragging
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
            isDragging = true;
            
            // Make sure this card is selected
            uiGameMng.DeselectCards();
            uiGameMng.SelectCard(IdCardDeck);

            // Log drag start for debugging
            Debug.Log($"Started dragging card {IdCardDeck} with pointer ID {dragPointerId}");
        }
    }
    
    // Helper method to check if this pointer is being used by the joystick
    private bool IsDragPointerJoystick(int pointerId)
    {
        // If the joystick is using this pointer, don't start a drag
        return pointerId == InputManager.GetJoystickPointerId();
    }
    
    // IDragHandler implementation
    public void OnDrag(PointerEventData eventData)
    {
        // Only process drags from the pointer that initiated the drag
        if (isDragging && canvas != null && eventData.pointerId == dragPointerId)
        {
            // Get position from the event data directly since we know it's the right pointer
            Vector2 pointerPos = eventData.position;
            
            // Convert screen position to a position within the canvas
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, 
                pointerPos, 
                canvas.worldCamera, 
                out localPos);
                
            // Update card position using the canvas position
            rectTransform.position = canvas.transform.TransformPoint(localPos);
        }
    }
    
    // IEndDragHandler implementation
    public void OnEndDrag(PointerEventData eventData)
    {
        // Only process end drag for the pointer that initiated the drag
        if (isDragging && eventData.pointerId == dragPointerId)
        {
            isDragging = false;
            dragPointerId = -1; // Reset pointer ID
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            
            // Deploy the card at the pointer position without area restrictions
            Vector2 pointerPos = eventData.position;
            Ray ray = Camera.main.ScreenPointToRay(pointerPos);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, 100f))
            {
                // Valid drop - deploy the card at hit position
                Debug.Log($"Card {IdCardDeck} dropped at position {hit.point}");
                
                // Deploy the card using game manager
                uiGameMng.DeployCard(IdCardDeck, hit.point);
            }
            else
            {
                // No hit - use auto-deployment
                Debug.Log($"Card {IdCardDeck} dropped - using auto-deployment");
                uiGameMng.DeployCard(IdCardDeck, Vector3.zero);
            }
            
            // Deselect all cards after drag
            uiGameMng.DeselectCards();
        }
    }
}
