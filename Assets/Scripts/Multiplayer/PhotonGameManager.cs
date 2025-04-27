using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using Cosmicrafts;

/// <summary>
/// PhotonGameManager handles the multiplayer game state using Photon.
/// It integrates with the existing GameMng class to adapt the game for networked play.
/// </summary>
public class PhotonGameManager : MonoBehaviourPunCallbacks
{
    public static PhotonGameManager Instance;
    
    [Header("Player Prefabs")]
    [Tooltip("Prefab for the player character with networking components")]
    public GameObject playerPrefab;
    [Tooltip("Prefab for the player base with networking components")]
    public GameObject playerBasePrefab;
    
    [Header("Spawning")]
    [Tooltip("Spawn points for players: 0=Blue player, 1=Red player")]
    public Transform[] playerSpawnPoints;
    [Tooltip("Spawn points for player bases: 0=Blue base, 1=Red base")]
    public Transform[] baseSpawnPoints;
    
    [Header("Game Settings")]
    [Tooltip("Length of countdown before game starts")]
    public float gameStartCountdown = 5f;
    [Tooltip("Maximum time for a match in seconds")]
    public float matchLength = 600f; // 10 minutes
    
    // Player tracking
    private Dictionary<int, NetworkPlayer> players = new Dictionary<int, NetworkPlayer>();
    private NetworkPlayer localPlayer;
    
    // Game state
    private bool gameStarted = false;
    private float gameStartTime = 0f;
    private Coroutine countdownCoroutine;
    
    // References to Unity UI
    private UIGameMng uiManager;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        // Get a reference to the UI Manager
        uiManager = FindObjectOfType<UIGameMng>();
        
        // If we're in a networked game, set up the multiplayer features
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Set the seed for procedural generation to match for all players
            // Use the room name as a seed for consistency
            if (PhotonNetwork.CurrentRoom != null)
            {
                string roomName = PhotonNetwork.CurrentRoom.Name;
                int seed = roomName.GetHashCode();
                Random.InitState(seed);
                Debug.Log($"[PhotonGameManager] Setting random seed to {seed} from room '{roomName}'");
            }
            
            // Start the multiplayer game setup
            InitializeMultiplayerGame();
        }
        else
        {
            // We're not connected, warn and potentially redirect to main menu
            Debug.LogWarning("[PhotonGameManager] Not connected to Photon. Game will run in single player mode.");
        }
    }
    
    private void InitializeMultiplayerGame()
    {
        // Validate prefabs and spawn points
        if (playerPrefab == null || playerBasePrefab == null)
        {
            Debug.LogError("[PhotonGameManager] Player or base prefab not assigned!");
            return;
        }
        
        if (playerSpawnPoints == null || playerSpawnPoints.Length < 2 || 
            baseSpawnPoints == null || baseSpawnPoints.Length < 2)
        {
            Debug.LogError("[PhotonGameManager] Spawn points not properly set up!");
            return;
        }
        
        // Determine player index based on player count (0 for master, 1 for client)
        int playerIndex = PhotonNetwork.IsMasterClient ? 0 : 1;
        
        // Spawn player and base
        SpawnPlayerAndBase(playerIndex);
        
        // Start the countdown when both players have joined
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
        {
            photonView.RPC("RPC_StartGameCountdown", RpcTarget.All);
        }
    }
    
    private void SpawnPlayerAndBase(int playerIndex)
    {
        // Spawn positions (player index 0 uses team blue/positions[1], player index 1 uses team red/positions[0])
        // This is to match the existing game logic where player is blue (index 1) and enemy is red (index 0)
        int teamIndex = playerIndex == 0 ? 1 : 0; // Convert player index to team index (0=red, 1=blue)
        
        // Get spawn transforms
        Transform baseSpawn = baseSpawnPoints[teamIndex];
        Transform playerSpawn = playerSpawnPoints[teamIndex];
        
        // Spawn player base
        GameObject playerBase = PhotonNetwork.Instantiate(
            playerBasePrefab.name, 
            baseSpawn.position, 
            baseSpawn.rotation
        );
        
        // Spawn player character
        GameObject playerCharacter = PhotonNetwork.Instantiate(
            playerPrefab.name, 
            playerSpawn.position, 
            playerSpawn.rotation
        );
        
        Debug.Log($"[PhotonGameManager] Spawned player {playerIndex} (Team {teamIndex}) at position {playerSpawn.position}");
        
        // Set up player references
        if (playerCharacter.TryGetComponent<NetworkPlayer>(out NetworkPlayer netPlayer))
        {
            // This will be set up in the NetworkPlayer Start method
            // The player will register itself via RegisterPlayer
        }
    }
    
    [PunRPC]
    private void RPC_StartGameCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }
        
        countdownCoroutine = StartCoroutine(GameStartCountdown());
    }
    
    private IEnumerator GameStartCountdown()
    {
        // Show countdown UI if available
        if (uiManager != null)
        {
            uiManager.ShowGameStartCountdown(gameStartCountdown);
        }
        
        float countdown = gameStartCountdown;
        while (countdown > 0)
        {
            Debug.Log($"Game starting in {countdown}...");
            
            // Update UI if available
            if (uiManager != null)
            {
                uiManager.UpdateCountdown(countdown);
            }
            
            yield return new WaitForSeconds(1.0f);
            countdown -= 1.0f;
        }
        
        // Start the game
        StartGame();
    }
    
    private void StartGame()
    {
        gameStarted = true;
        gameStartTime = Time.time;
        
        // Reset UI if available
        if (uiManager != null)
        {
            uiManager.HideGameStartCountdown();
        }
        
        Debug.Log("Game started!");
        
        // Notify GameMng that the game has started (if needed)
        if (GameMng.GM != null)
        {
            // Add any necessary GameMng initialization here
            // GameMng.GM.StartMatch();
        }
    }
    
    private void Update()
    {
        if (gameStarted)
        {
            // Handle match timing
            float elapsedTime = Time.time - gameStartTime;
            if (elapsedTime >= matchLength)
            {
                EndGame();
            }
            
            // Update UI if available
            if (uiManager != null)
            {
                int timeRemaining = Mathf.Max(0, Mathf.FloorToInt(matchLength - elapsedTime));
                uiManager.UpdateMatchTime(timeRemaining);
            }
        }
    }
    
    private void EndGame()
    {
        if (!gameStarted) return;
        
        gameStarted = false;
        
        // Determine winner based on remaining health of bases
        DetermineWinner();
    }
    
    private void DetermineWinner()
    {
        // Check base health to determine winner
        Unit playerBase = null;
        Unit enemyBase = null;
        
        if (GameMng.GM != null && GameMng.GM.Targets != null && GameMng.GM.Targets.Length >= 2)
        {
            playerBase = GameMng.GM.Targets[1]; // Blue/Player
            enemyBase = GameMng.GM.Targets[0]; // Red/Enemy
        }
        
        Faction winningFaction = Faction.Neutral;
        
        if (playerBase == null && enemyBase == null)
        {
            // Draw - both bases destroyed
            winningFaction = Faction.Neutral;
        }
        else if (playerBase == null || (enemyBase != null && playerBase.HitPoints < enemyBase.HitPoints))
        {
            // Enemy wins
            winningFaction = Faction.Enemy;
        }
        else if (enemyBase == null || (playerBase != null && playerBase.HitPoints >= enemyBase.HitPoints))
        {
            // Player wins
            winningFaction = Faction.Player;
        }
        
        // Call RPCs to ensure all clients show the same winner
        photonView.RPC("RPC_ShowGameResults", RpcTarget.All, (int)winningFaction);
    }
    
    [PunRPC]
    private void RPC_ShowGameResults(int winningFactionInt)
    {
        Faction winningFaction = (Faction)winningFactionInt;
        
        // Show appropriate UI for the winner
        if (uiManager != null)
        {
            bool isLocalPlayerWinner = (localPlayer != null && 
                                      localPlayer.GetComponent<Unit>().MyFaction == winningFaction) ||
                                      (winningFaction == Faction.Neutral);
                                      
            uiManager.ShowGameResults(isLocalPlayerWinner);
        }
        
        // Let GameMng handle game end if needed
        if (GameMng.GM != null)
        {
            GameMng.GM.EndGame(winningFaction);
        }
        
        // Start return to lobby countdown
        StartCoroutine(ReturnToLobbyCountdown());
    }
    
    private IEnumerator ReturnToLobbyCountdown()
    {
        yield return new WaitForSeconds(5.0f);
        
        // Return to lobby
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("Lobby");
        }
    }
    
    // Player registration methods
    public void RegisterPlayer(NetworkPlayer player)
    {
        if (player.photonView != null)
        {
            players[player.photonView.Owner.ActorNumber] = player;
            Debug.Log($"[PhotonGameManager] Registered player {player.photonView.Owner.ActorNumber}");
        }
    }
    
    public void SetLocalPlayer(NetworkPlayer player)
    {
        localPlayer = player;
        Debug.Log("[PhotonGameManager] Set local player");
    }
    
    public NetworkPlayer GetPlayer(int actorNumber)
    {
        return players.TryGetValue(actorNumber, out var player) ? player : null;
    }
    
    public NetworkPlayer GetLocalPlayer()
    {
        return localPlayer;
    }
    
    // Photon callbacks
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[PhotonGameManager] Player {otherPlayer.ActorNumber} left the room");
        
        // Handle player disconnection during the game
        if (gameStarted)
        {
            // End the game with the remaining player as winner if the game was already in progress
            if (PhotonNetwork.IsMasterClient)
            {
                Faction winningFaction = localPlayer != null ? localPlayer.GetComponent<Unit>().MyFaction : Faction.Player;
                photonView.RPC("RPC_ShowGameResults", RpcTarget.All, (int)winningFaction);
            }
        }
    }
} 