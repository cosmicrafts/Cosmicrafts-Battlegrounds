using UnityEngine;
using Photon.Pun;
using Cosmicrafts;

/// <summary>
/// NetworkSpell component handles synchronization of spell-related data over the network.
/// Attach this to spell objects that need to be synchronized.
/// </summary>
[RequireComponent(typeof(Spell))]
[RequireComponent(typeof(PhotonView))]
public class NetworkSpell : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Network Settings")]
    [Tooltip("How often to send transform updates (seconds)")]
    public float sendRate = 0.05f;
    [Tooltip("How smooth position lerping should be")]
    public float positionLerpSpeed = 15f;
    [Tooltip("How smooth rotation lerping should be")]
    public float rotationLerpSpeed = 15f;
    
    // References
    private PhotonView photonView;
    private Spell spellComponent;
    
    // Synchronization variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private bool networkIsActive;
    private Faction networkFaction;
    private int networkOwnerId;
    
    // Last sent state to reduce network traffic
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentVelocity;
    private bool lastSentIsActive;
    
    // Timing
    private float lastSendTime;
    
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        spellComponent = GetComponent<Spell>();
        
        // Initialize network state
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        lastSentPosition = networkPosition;
        lastSentRotation = networkRotation;
    }
    
    private void Update()
    {
        if (photonView.IsMine)
        {
            // We own this spell, so send state periodically
            SendSpellState();
        }
        else
        {
            // Remote spell, interpolate movement based on received network state
            InterpolateMovement();
        }
    }
    
    private void SendSpellState()
    {
        if (Time.time - lastSendTime < sendRate)
        {
            return;
        }
        
        // Gather current state
        Vector3 currentPosition = transform.position;
        Quaternion currentRotation = transform.rotation;
        Vector3 currentVelocity = spellComponent.GetVelocity();
        bool currentIsActive = spellComponent.IsActive();
        
        // Check if significant state change has occurred
        bool positionChanged = Vector3.Distance(currentPosition, lastSentPosition) > 0.05f;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastSentRotation) > 1f;
        bool velocityChanged = Vector3.Distance(currentVelocity, lastSentVelocity) > 0.1f;
        bool activeStateChanged = currentIsActive != lastSentIsActive;
        
        // Only send if something significant changed or time threshold exceeded
        bool significantChange = positionChanged || rotationChanged || velocityChanged || activeStateChanged;
        
        if (significantChange || Time.time - lastSendTime > sendRate * 5f)
        {
            // Update last sent values
            lastSentPosition = currentPosition;
            lastSentRotation = currentRotation;
            lastSentVelocity = currentVelocity;
            lastSentIsActive = currentIsActive;
            
            // The IPunObservable.OnPhotonSerializeView method will be called
            // by Photon to send these updated values to other players
            lastSendTime = Time.time;
        }
    }
    
    private void InterpolateMovement()
    {
        // Only interpolate if the spell is active
        if (!networkIsActive) return;
        
        // Smoothly interpolate position
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
        
        // Smoothly interpolate rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * rotationLerpSpeed);
        
        // Apply other networked state to this remote spell
        if (spellComponent != null)
        {
            // Set velocity for physics and effects
            spellComponent.SetVelocity(networkVelocity);
            
            // Ensure faction is correct (this should only happen once typically)
            if (spellComponent.MyFaction != networkFaction)
            {
                spellComponent.MyFaction = networkFaction;
            }
            
            // Set owner ID
            if (spellComponent.OwnerId != networkOwnerId)
            {
                spellComponent.OwnerId = networkOwnerId;
            }
            
            // Activate/deactivate the spell
            if (spellComponent.IsActive() != networkIsActive)
            {
                if (networkIsActive)
                {
                    spellComponent.Activate();
                }
                else
                {
                    spellComponent.Deactivate();
                }
            }
        }
    }
    
    // IPunObservable implementation for Photon's automatic serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this spell: send our data to network
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            
            // Send spell state
            if (spellComponent != null)
            {
                stream.SendNext(spellComponent.GetVelocity());
                stream.SendNext(spellComponent.IsActive());
                stream.SendNext((int)spellComponent.MyFaction);
                stream.SendNext(spellComponent.OwnerId);
            }
            else
            {
                // Fallback defaults
                stream.SendNext(Vector3.zero);
                stream.SendNext(false);
                stream.SendNext((int)Faction.Neutral);
                stream.SendNext(-1);
            }
        }
        else
        {
            // Network spell: receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            networkIsActive = (bool)stream.ReceiveNext();
            networkFaction = (Faction)stream.ReceiveNext();
            networkOwnerId = (int)stream.ReceiveNext();
        }
    }
    
    // RPC methods for specific actions
    [PunRPC]
    public void RPC_ApplyDamage(int targetId, int damage)
    {
        if (spellComponent != null)
        {
            // Find target by ID
            Unit targetUnit = GameMng.GM?.GetUnitById(targetId);
            if (targetUnit != null)
            {
                spellComponent.ApplyDamage(targetUnit, damage);
            }
        }
    }
    
    // Helper methods to call RPCs
    public void ApplyDamageNetworked(int targetId, int damage)
    {
        photonView.RPC("RPC_ApplyDamage", RpcTarget.All, targetId, damage);
    }
} 