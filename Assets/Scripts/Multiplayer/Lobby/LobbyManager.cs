using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using System.Collections;

/// <summary>
/// LobbyManager handles the player lobby experience, including room creation, joining, and displaying available rooms.
/// </summary>
public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Connection Status")]
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private GameObject connectionPanel;
    
    [Header("Lobby UI")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button setNameButton;
    
    [Header("Room Creation")]
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button cancelCreateButton;
    
    [Header("Room List")]
    [SerializeField] private GameObject roomListPanel;
    [SerializeField] private GameObject roomListContent;
    [SerializeField] private GameObject roomListItemPrefab;
    [SerializeField] private Button refreshRoomsButton;
    [SerializeField] private Button createRoomMenuButton;
    
    [Header("Room")]
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI roomPlayersText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private GameObject playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    
    [Header("Error Panel")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private Button closeErrorButton;
    
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();
    private Dictionary<string, GameObject> roomListItems = new Dictionary<string, GameObject>();
    private Dictionary<int, GameObject> playerListItems = new Dictionary<int, GameObject>();
    
    private void Awake()
    {
        // Make sure we don't have multiple instances
        PhotonNetwork.AutomaticallySyncScene = true;
    }
    
    private void Start()
    {
        // Set up button listeners
        if (setNameButton != null) setNameButton.onClick.AddListener(SetPlayerName);
        if (createRoomButton != null) createRoomButton.onClick.AddListener(CreateRoom);
        if (cancelCreateButton != null) cancelCreateButton.onClick.AddListener(CancelCreateRoom);
        if (refreshRoomsButton != null) refreshRoomsButton.onClick.AddListener(RefreshRoomList);
        if (createRoomMenuButton != null) createRoomMenuButton.onClick.AddListener(OpenCreateRoomPanel);
        if (startGameButton != null) startGameButton.onClick.AddListener(StartGame);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(LeaveRoom);
        if (closeErrorButton != null) closeErrorButton.onClick.AddListener(CloseErrorPanel);
        
        // Set up default player name
        if (playerNameInput != null)
        {
            string defaultName = $"Player_{Random.Range(1000, 9999)}";
            playerNameInput.text = defaultName;
            PhotonNetwork.NickName = defaultName;
            
            if (playerNameText != null)
            {
                playerNameText.text = $"Player: {defaultName}";
            }
        }
        
        // Hide all panels initially
        HideAllPanels();
        
        // Show connection panel
        if (connectionPanel != null)
        {
            connectionPanel.SetActive(true);
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "Connecting to Photon...";
            }
        }
        
        // Connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            ConnectToPhoton();
        }
        else
        {
            // Already connected, show lobby
            ShowLobbyPanel();
        }
    }
    
    private void ConnectToPhoton()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = "Connecting to Photon...";
        }
        
        // Use NetworkManager if available
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Connect();
        }
        else
        {
            // If no NetworkManager, connect directly
            PhotonNetwork.ConnectUsingSettings();
        }
    }
    
    private void HideAllPanels()
    {
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (createRoomPanel != null) createRoomPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
    }
    
    private void ShowLobbyPanel()
    {
        HideAllPanels();
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        
        // Show current player name
        if (playerNameText != null)
        {
            playerNameText.text = $"Player: {PhotonNetwork.NickName}";
        }
        
        // Refresh room list
        RefreshRoomList();
    }
    
    private void OpenCreateRoomPanel()
    {
        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(true);
            
            // Generate random room name
            if (roomNameInput != null)
            {
                roomNameInput.text = $"Room_{Random.Range(1000, 9999)}";
            }
        }
    }
    
    private void CancelCreateRoom()
    {
        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(false);
        }
    }
    
    private void SetPlayerName()
    {
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            string newName = playerNameInput.text;
            PhotonNetwork.NickName = newName;
            
            // Update displayed name
            if (playerNameText != null)
            {
                playerNameText.text = $"Player: {newName}";
            }
        }
    }
    
    private void CreateRoom()
    {
        string roomName = roomNameInput != null ? roomNameInput.text : $"Room_{Random.Range(1000, 9999)}";
        
        if (string.IsNullOrWhiteSpace(roomName))
        {
            roomName = $"Room_{Random.Range(1000, 9999)}";
        }
        
        // Create room options - max 2 players for 1v1
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };
        
        // Create the room
        PhotonNetwork.CreateRoom(roomName, roomOptions);
        
        // Hide create panel
        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(false);
        }
        
        // Show waiting status
        if (connectionStatusText != null)
        {
            connectionStatusText.text = "Creating room...";
        }
        if (connectionPanel != null)
        {
            HideAllPanels();
            connectionPanel.SetActive(true);
        }
    }
    
    private void RefreshRoomList()
    {
        // Clear existing list
        ClearRoomList();
        
        // Request room list update from Photon
        PhotonNetwork.GetCustomRoomList(TypedLobby.Default, "");
    }
    
    private void ClearRoomList()
    {
        // Destroy existing room list GameObjects
        foreach (var item in roomListItems.Values)
        {
            Destroy(item);
        }
        roomListItems.Clear();
    }
    
    private void UpdatePlayerList()
    {
        // Clear existing player list
        foreach (var item in playerListItems.Values)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        // Add players in the room
        if (PhotonNetwork.CurrentRoom != null)
        {
            foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                GameObject playerItem = Instantiate(playerListItemPrefab, playerListContent.transform);
                TextMeshProUGUI playerText = playerItem.GetComponentInChildren<TextMeshProUGUI>();
                
                if (playerText != null)
                {
                    playerText.text = player.NickName;
                    
                    // Add "you" to local player
                    if (player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        playerText.text += " (you)";
                    }
                    
                    // Add master client indicator
                    if (player.IsMasterClient)
                    {
                        playerText.text += " [Host]";
                    }
                }
                
                playerListItems[player.ActorNumber] = playerItem;
            }
        }
        
        // Update room info
        if (roomNameText != null && PhotonNetwork.CurrentRoom != null)
        {
            roomNameText.text = PhotonNetwork.CurrentRoom.Name;
        }
        
        if (roomPlayersText != null && PhotonNetwork.CurrentRoom != null)
        {
            roomPlayersText.text = $"Players: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
        }
        
        // Only master client can start the game
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
            startGameButton.interactable = PhotonNetwork.CurrentRoom.PlayerCount >= 2;
        }
    }
    
    private void ShowRoomPanel()
    {
        HideAllPanels();
        if (roomPanel != null) roomPanel.SetActive(true);
        
        UpdatePlayerList();
    }
    
    private void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Set room to not visible/joinable while the game is in progress
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            
            // Start the game
            PhotonNetwork.LoadLevel("Game");
        }
    }
    
    private void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
        
        // Show connection status
        if (connectionStatusText != null)
        {
            connectionStatusText.text = "Leaving room...";
        }
        if (connectionPanel != null)
        {
            HideAllPanels();
            connectionPanel.SetActive(true);
        }
    }
    
    private void ShowError(string message)
    {
        HideAllPanels();
        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
            if (errorText != null)
            {
                errorText.text = message;
            }
        }
    }
    
    private void CloseErrorPanel()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
            ShowLobbyPanel();
        }
    }
    
    // Photon Callbacks
    public override void OnConnectedToMaster()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = "Connected to Photon! Joining lobby...";
        }
        
        // Join the default lobby
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Photon Lobby");
        ShowLobbyPanel();
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
        
        if (connectionStatusText != null)
        {
            connectionStatusText.text = $"Disconnected: {cause}. Reconnecting...";
        }
        if (connectionPanel != null)
        {
            HideAllPanels();
            connectionPanel.SetActive(true);
        }
        
        // Try to reconnect after a short delay
        Invoke("ConnectToPhoton", 2.0f);
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        ShowRoomPanel();
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player joined: {newPlayer.NickName}");
        UpdatePlayerList();
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left: {otherPlayer.NickName}");
        UpdatePlayerList();
    }
    
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"New master client: {newMasterClient.NickName}");
        UpdatePlayerList();
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create room failed: {message} ({returnCode})");
        ShowError($"Failed to create room: {message}");
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join room failed: {message} ({returnCode})");
        ShowError($"Failed to join room: {message}");
    }
    
    public override void OnLeftRoom()
    {
        Debug.Log("Left room");
        ShowLobbyPanel();
    }
    
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log($"Room list updated: {roomList.Count} rooms available");
        
        // Update cached room list
        foreach (var room in roomList)
        {
            if (room.RemovedFromList)
            {
                cachedRoomList.Remove(room.Name);
            }
            else
            {
                cachedRoomList[room.Name] = room;
            }
        }
        
        // Update UI
        UpdateRoomList();
    }
    
    private void UpdateRoomList()
    {
        // Clear existing room list
        ClearRoomList();
        
        // Create room list items
        foreach (var room in cachedRoomList.Values)
        {
            if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
            {
                GameObject roomItem = Instantiate(roomListItemPrefab, roomListContent.transform);
                
                // Set up room details
                RoomListing roomListing = roomItem.GetComponent<RoomListing>();
                if (roomListing != null)
                {
                    roomListing.SetRoomInfo(room.Name, $"{room.PlayerCount}/{room.MaxPlayers}");
                }
                
                // Add join button listener
                Button joinButton = roomItem.GetComponentInChildren<Button>();
                if (joinButton != null)
                {
                    string roomName = room.Name; // Capture for closure
                    joinButton.onClick.AddListener(() => JoinRoom(roomName));
                }
                
                roomListItems[room.Name] = roomItem;
            }
        }
    }
    
    private void JoinRoom(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return;
        
        // Show connection status
        if (connectionStatusText != null)
        {
            connectionStatusText.text = $"Joining room {roomName}...";
        }
        if (connectionPanel != null)
        {
            HideAllPanels();
            connectionPanel.SetActive(true);
        }
        
        // Join the room
        PhotonNetwork.JoinRoom(roomName);
    }
} 