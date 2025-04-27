using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using Cosmicrafts;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance;
    
    [Header("Connection Settings")]
    [Tooltip("Game version - must match for players to join the same matchmaking pool")]
    public string gameVersion = "1.0";
    [Tooltip("Region to connect to (leave empty for automatic best region)")]
    public string region = "";
    [Tooltip("Whether to automatically connect on start")]
    public bool autoConnect = true;
    
    [Header("Room Settings")]
    [Tooltip("Maximum players per room")]
    public byte maxPlayersPerRoom = 2;
    [Tooltip("Time to live for empty rooms in seconds")]
    public int emptyRoomTTL = 60;
    
    // Connection state
    private bool isConnecting = false;
    private const string PLAYER_LOADED_LEVEL = "PlayerLoadedLevel";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Set up Photon settings
        PhotonNetwork.AutomaticallySyncScene = true;
    }
    
    private void Start()
    {
        if (autoConnect)
        {
            Connect();
        }
    }
    
    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Already connected to Photon servers.");
            return;
        }
        
        isConnecting = true;
        
        // Set up region if specified
        if (!string.IsNullOrEmpty(region))
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = region;
        }
        
        // Set version
        PhotonNetwork.GameVersion = gameVersion;
        
        // Connect
        Debug.Log("Connecting to Photon servers...");
        PhotonNetwork.ConnectUsingSettings();
    }
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server.");
        isConnecting = false;
        
        // Auto-join lobby when connected
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon. Reason: {cause}");
        isConnecting = false;
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Photon lobby. Ready to join/create rooms.");
    }
    
    // Room management methods
    public void CreateRoom(string roomName = "")
    {
        // Create room options
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            EmptyRoomTtl = emptyRoomTTL,
            // Set initial custom properties if needed
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                {"GameStarted", false}
            },
            // Define which custom properties are visible in lobby
            CustomRoomPropertiesForLobby = new string[] {"GameStarted"}
        };
        
        // Generate room name if not provided
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = $"Room_{Random.Range(1000, 9999)}";
        }
        
        // Create the room
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }
    
    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }
    
    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }
    
    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }
    
    // Room callback handlers
    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        
        // Set player properties
        PhotonNetwork.LocalPlayer.NickName = $"Player_{Random.Range(1000, 9999)}";
        
        // Call custom room join event
        OnRoomJoined();
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"Failed to join room. Error: {message}");
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"Failed to create room. Error: {message}");
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player joined: {newPlayer.NickName}");
        
        // Check if room is full to start the game
        if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            Debug.Log("Room is full. Starting game...");
            
            // Only master client can start the game
            if (PhotonNetwork.IsMasterClient)
            {
                StartGame();
            }
        }
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player left: {otherPlayer.NickName}");
        
        // Handle player leaving during gameplay
        if (PhotonNetwork.IsMasterClient)
        {
            // Master client code - handle player leaving
        }
    }
    
    // Game management
    public void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Set room as started
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable {{"GameStarted", true}}
            );
            
            // Load the game scene
            PhotonNetwork.LoadLevel("Game");
        }
    }
    
    // Level synchronization
    private void OnRoomJoined()
    {
        // If the game is already in progress, load the game scene
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameStarted", out object gameStarted) 
            && (bool)gameStarted)
        {
            // The game has already started, so load the scene
            StartCoroutine(LoadGameScene());
        }
    }
    
    private IEnumerator LoadGameScene()
    {
        // Wait a moment before loading
        yield return new WaitForSeconds(1.0f);
        
        // Load the game scene
        PhotonNetwork.LoadLevel("Game");
    }
    
    // Utility methods
    public bool IsRoomFull()
    {
        return PhotonNetwork.CurrentRoom != null && 
               PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers;
    }
    
    public int GetPlayerCount()
    {
        return PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
    }
    
    public string GetRoomName()
    {
        return PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "";
    }
} 