using UnityEngine;
using Photon.Pun;
using Cosmicrafts;

/// <summary>
/// NetworkUnit component handles synchronization of unit-related data over the network.
/// Attach this to unit objects that need to be synchronized (like bots, spawned units, etc).
/// </summary>
[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(PhotonView))]
public class NetworkUnit : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Network Settings")]
    [Tooltip("How often to send transform updates (seconds)")]
    public float sendRate = 0.1f;
    [Tooltip("How smooth position lerping should be")]
    public float positionLerpSpeed = 5f;
    [Tooltip("How smooth rotation lerping should be")]
    public float rotationLerpSpeed = 5f;
    
    // References
    private PhotonView photonView;
    private Unit unitComponent;
    
    // Synchronization variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkMovementDirection;
    private bool networkIsAttacking;
    private int networkHealth;
    private int networkShield;
    private Faction networkFaction;
    
    // Last sent state to reduce network traffic
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentMovementDirection;
    private bool lastSentIsAttacking;
    private int lastSentHealth;
    private int lastSentShield;
    
    // Timing
    private float lastSendTime;
    
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        unitComponent = GetComponent<Unit>();
        
        // Initialize network state
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        lastSentPosition = networkPosition;
        lastSentRotation = networkRotation;
        
        // By default, let the unit's owner control it
        if (!photonView.IsMine)
        {
            // This is a remote unit, so apply networked properties
            unitComponent.SetOwnershipState(false);
        }
    }
    
    private void Update()
    {
        if (photonView.IsMine)
        {
            // We own this unit, so send state periodically
            SendUnitState();
        }
        else
        {
            // Remote unit, interpolate movement based on received network state
            InterpolateMovement();
        }
    }
    
    private void SendUnitState()
    {
        if (Time.time - lastSendTime < sendRate)
        {
            return;
        }
        
        // Gather current state
        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        Vector3 currentDirection = unitComponent.GetMovementDirection();
        bool currentIsAttacking = unitComponent.IsAttacking();
        int currentHealth = unitComponent.HitPoints;
        int currentShield = unitComponent.Shield;
        
        // Check if significant state change has occurred
        bool positionChanged = Vector3.Distance(currentPosition, lastSentPosition) > 0.1f;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastSentRotation) > 1f;
        bool directionChanged = Vector3.Distance(currentDirection, lastSentMovementDirection) > 0.1f;
        bool attackStateChanged = currentIsAttacking != lastSentIsAttacking;
        bool healthChanged = currentHealth != lastSentHealth;
        bool shieldChanged = currentShield != lastSentShield;
        
        // Only send if something significant changed or time threshold exceeded
        bool significantChange = positionChanged || rotationChanged || directionChanged || 
                              attackStateChanged || healthChanged || shieldChanged;
        
        if (significantChange || Time.time - lastSendTime > sendRate * 5f)
        {
            // Update last sent values
            lastSentPosition = currentPosition;
            lastSentRotation = currentRotation;
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
        
        // Apply other networked state to this remote unit
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
            
            // Set movement direction for animation purposes
            unitComponent.SetMovementDirection(networkMovementDirection);
            
            // Handle attack state
            if (networkIsAttacking)
            {
                unitComponent.StartAttack();
            }
            
            // Ensure faction is correct (this should only happen once typically)
            if (unitComponent.MyFaction != networkFaction)
            {
                unitComponent.MyFaction = networkFaction;
            }
        }
    }
    
    // IPunObservable implementation for Photon's automatic serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this unit: send our data to network
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            
            // Send unit state
            if (unitComponent != null)
            {
                stream.SendNext(unitComponent.GetMovementDirection());
                stream.SendNext(unitComponent.IsAttacking());
                stream.SendNext(unitComponent.HitPoints);
                stream.SendNext(unitComponent.Shield);
                stream.SendNext((int)unitComponent.MyFaction);
            }
            else
            {
                // Fallback defaults
                stream.SendNext(Vector3.zero);
                stream.SendNext(false);
                stream.SendNext(100);
                stream.SendNext(0);
                stream.SendNext((int)Faction.Neutral);
            }
        }
        else
        {
            // Network unit: receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkMovementDirection = (Vector3)stream.ReceiveNext();
            networkIsAttacking = (bool)stream.ReceiveNext();
            networkHealth = (int)stream.ReceiveNext();
            networkShield = (int)stream.ReceiveNext();
            networkFaction = (Faction)stream.ReceiveNext();
        }
    }
    
    // RPC methods for specific actions
    [PunRPC]
    public void RPC_TakeDamage(int damage, int attackerId)
    {
        if (unitComponent != null)
        {
            unitComponent.TakeDamage(damage, attackerId);
        }
    }
    
    [PunRPC]
    public void RPC_SetTarget(Vector3 targetPosition)
    {
        if (unitComponent != null)
        {
            unitComponent.SetTargetPosition(targetPosition);
        }
    }
    
    // Helper methods to call RPCs
    public void TakeDamageNetworked(int damage, int attackerId)
    {
        photonView.RPC("RPC_TakeDamage", RpcTarget.All, damage, attackerId);
    }
    
    public void SetTargetNetworked(Vector3 targetPosition)
    {
        photonView.RPC("RPC_SetTarget", RpcTarget.All, targetPosition);
    }
} 