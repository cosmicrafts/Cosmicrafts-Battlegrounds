using UnityEngine;
using TMPro;

/// <summary>
/// RoomListing handles the UI for a single room in the lobby room list.
/// </summary>
public class RoomListing : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    
    private string roomName;
    
    public void SetRoomInfo(string name, string playerCount)
    {
        this.roomName = name;
        
        if (roomNameText != null)
        {
            roomNameText.text = name;
        }
        
        if (playerCountText != null)
        {
            playerCountText.text = playerCount;
        }
    }
    
    public string GetRoomName()
    {
        return roomName;
    }
} 