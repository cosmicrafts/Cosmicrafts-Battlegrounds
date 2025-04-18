using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cosmicrafts
{
    /// <summary>
    /// Attaches to game cards in the UI and sets up the necessary event triggers
    /// to work with the static Player methods
    /// </summary>
    [RequireComponent(typeof(EventTrigger))]
    public class UIGameCardHandler : MonoBehaviour
    {
        public int cardIndex = -1;
        
        private EventTrigger eventTrigger;
        private UIGameCard gameCard;
        
        private void Awake()
        {
            // Get required components
            eventTrigger = GetComponent<EventTrigger>();
            gameCard = GetComponent<UIGameCard>();
            
            if (eventTrigger == null)
            {
                Debug.LogError("Missing EventTrigger component on game card");
                return;
            }
            
            if (gameCard == null)
            {
                Debug.LogError("Missing UIGameCard component on game card");
                return;
            }
            
            // Make sure we have a valid card index
            if (cardIndex < 0)
            {
                // Try to auto-detect the card index based on sibling index
                cardIndex = transform.GetSiblingIndex();
            }
            
            // Set up event triggers
            SetupEventTriggers();
        }
        
        private void SetupEventTriggers()
        {
            // Clear existing triggers
            if (eventTrigger.triggers != null)
            {
                eventTrigger.triggers.Clear();
            }
            
            // Click event - calls Player.OnUICardClick
            EventTrigger.Entry clickEntry = new EventTrigger.Entry();
            clickEntry.eventID = EventTriggerType.PointerClick;
            clickEntry.callback.AddListener((data) => { Player.OnUICardClick(cardIndex); });
            eventTrigger.triggers.Add(clickEntry);
            
            // Begin Drag event - calls Player.OnUICardDragStart
            EventTrigger.Entry beginDragEntry = new EventTrigger.Entry();
            beginDragEntry.eventID = EventTriggerType.BeginDrag;
            beginDragEntry.callback.AddListener((data) => { Player.OnUICardDragStart(cardIndex); });
            eventTrigger.triggers.Add(beginDragEntry);
            
            // End Drag event - calls Player.OnUICardDragEnd
            EventTrigger.Entry endDragEntry = new EventTrigger.Entry();
            endDragEntry.eventID = EventTriggerType.EndDrag;
            endDragEntry.callback.AddListener((data) => { Player.OnUICardDragEnd(); });
            eventTrigger.triggers.Add(endDragEntry);
        }
    }
} 