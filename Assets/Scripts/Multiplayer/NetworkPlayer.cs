using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Cosmicrafts;
using System.Collections;

/// <summary>
/// NetworkPlayer component handles synchronization of player-related data over the network.
/// Attach this to player objects that need to be synchronized.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(PhotonView))]
public class NetworkPlayer : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Network Settings")]
    [Tooltip("How often to send transform updates (seconds)")]
    public float sendRate = 0.05f;
    [Tooltip("How smooth position lerping should be")]
    public float positionLerpSpeed = 10f;
    [Tooltip("How smooth rotation lerping should be")]
    public float rotationLerpSpeed = 8f;
    
    // References
    private PhotonView photonView;
    private Player playerComponent;
    private Unit unitComponent;
    
    // Synchronization variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private Vector3 networkMovementDirection;
    private bool networkIsAttacking;
    private int networkHealth;
    private int networkShield;
    
    // Last sent state to reduce network traffic
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentVelocity;
    private Vector3 lastSentMovementDirection;
    private bool lastSentIsAttacking;
    private int lastSentHealth;
    private int lastSentShield;
    
    // Timing
    private float lastSendTime;
    
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        playerComponent = GetComponent<Player>();
        unitComponent = GetComponent<Unit>();
        
        // Initialize network state
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        lastSentPosition = networkPosition;
        lastSentRotation = networkRotation;
    }
    
    private void Start()
    {
        // Register with the PhotonGameManager
        PhotonGameManager.Instance?.RegisterPlayer(this);
        
        // If we own this player, register it as the local player
        if (photonView.IsMine)
        {
            PhotonGameManager.Instance?.SetLocalPlayer(this);
            
            // Also register with GameMng (existing system)
            if (GameMng.GM != null && playerComponent != null)
            {
                // Set the player in GameMng
                playerComponent.MyFaction = Faction.Player;
                GameMng.P = playerComponent;
            }
        }
        else
        {
            // For remote players, disable input scripts but keep visuals and physics
            DisableLocalControl();
        }
    }
    
    private void DisableLocalControl()
    {
        // Disable input-related components if this is a remote player
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }
        
        // Set faction to Enemy for the networked opponent
        if (unitComponent != null)
        {
            unitComponent.MyFaction = Faction.Enemy;
        }
    }
    
    private void Update()
    {
        if (photonView.IsMine)
        {
            // We own this player, so send state periodically
            SendPlayerState();
        }
        else
        {
            // Remote player, interpolate movement based on received network state
            InterpolateMovement();
        }
    }
    
    private void SendPlayerState()
    {
        if (Time.time - lastSendTime < sendRate)
        {
            return;
        }
        
        // Gather current state
        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        Vector3 currentVelocity = unitComponent.GetVelocity();
        Vector3 currentDirection = unitComponent.GetMovementDirection();
        bool currentIsAttacking = unitComponent.IsAttacking();
        int currentHealth = unitComponent.HitPoints;
        int currentShield = unitComponent.Shield;
        
        // Check if significant state change has occurred
        bool positionChanged = Vector3.Distance(currentPosition, lastSentPosition) > 0.05f;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastSentRotation) > 1f;
        bool velocityChanged = Vector3.Distance(currentVelocity, lastSentVelocity) > 0.1f;
        bool directionChanged = Vector3.Distance(currentDirection, lastSentMovementDirection) > 0.1f;
        bool attackStateChanged = currentIsAttacking != lastSentIsAttacking;
        bool healthChanged = currentHealth != lastSentHealth;
        bool shieldChanged = currentShield != lastSentShield;
        
        // Only send if something significant changed or time threshold exceeded
        bool significantChange = positionChanged || rotationChanged || velocityChanged || 
                                directionChanged || attackStateChanged || healthChanged || shieldChanged;
        
        if (significantChange || Time.time - lastSendTime > sendRate * 5f)
        {
            // Update last sent values
            lastSentPosition = currentPosition;
            lastSentRotation = currentRotation;
            lastSentVelocity = currentVelocity;
            lastSentMovementDirection = currentDirection;
            lastSentIsAttacking = currentIsAttacking;
            lastSentHealth = currentHealth;
            lastSentShield = currentShield;
            
            // The IPunObservable.OnPhotonSerializeView method will be called
            // by Photon to send these updated values to other players
            lastSendTime = Time.time;
        }
    }
    
    private void InterpolateMovement()
    {
        // Smoothly interpolate position
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
        
        // Smoothly interpolate rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * rotationLerpSpeed);
        
        // Apply other networked state to this remote player
        if (unitComponent != null)
        {
            // Ensure the health and shield values are synchronized
            if (unitComponent.HitPoints != networkHealth)
            {
                unitComponent.HitPoints = networkHealth;
            }
            
            if (unitComponent.Shield != networkShield)
            {
                unitComponent.Shield = networkShield;
            }
            
            // Set movement direction and velocity for animation purposes
            unitComponent.SetMovementDirection(networkMovementDirection);
            unitComponent.SetVelocity(networkVelocity);
            
            // Handle attack state
            if (networkIsAttacking)
            {
                unitComponent.StartAttack();
            }
        }
    }
    
    // IPunObservable implementation for Photon's automatic serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send our data to network
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            
            // Send unit state
            if (unitComponent != null)
            {
                stream.SendNext(unitComponent.GetVelocity());
                stream.SendNext(unitComponent.GetMovementDirection());
                stream.SendNext(unitComponent.IsAttacking());
                stream.SendNext(unitComponent.HitPoints);
                stream.SendNext(unitComponent.Shield);
            }
            else
            {
                // Fallback defaults
                stream.SendNext(Vector3.zero);
                stream.SendNext(Vector3.zero);
                stream.SendNext(false);
                stream.SendNext(100);
                stream.SendNext(0);
            }
        }
        else
        {
            // Network player: receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            networkMovementDirection = (Vector3)stream.ReceiveNext();
            networkIsAttacking = (bool)stream.ReceiveNext();
            networkHealth = (int)stream.ReceiveNext();
            networkShield = (int)stream.ReceiveNext();
        }
    }
    
    // RPC methods for specific actions
    [PunRPC]
    public void RPC_UseAbility(int abilityIndex)
    {
        if (playerComponent != null)
        {
            playerComponent.UseAbility(abilityIndex);
        }
    }
    
    [PunRPC]
    public void RPC_TakeDamage(int damage, int attackerId)
    {
        if (unitComponent != null)
        {
            unitComponent.TakeDamage(damage, attackerId);
        }
    }
    
    // Helper method to call RPCs
    public void UseAbilityNetworked(int abilityIndex)
    {
        photonView.RPC("RPC_UseAbility", RpcTarget.All, abilityIndex);
    }
    
    public void TakeDamageNetworked(int damage, int attackerId)
    {
        photonView.RPC("RPC_TakeDamage", RpcTarget.All, damage, attackerId);
    }
} 