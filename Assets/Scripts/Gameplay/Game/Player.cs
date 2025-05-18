namespace Cosmicrafts {
using System.Collections.Generic;
using UnityEngine;
/*
 ! This is the Player code
 ? Controls his energy, gameplay, deck, etc.
 * Contains the player data references and his ID and team on the game
 */
public class Player : MonoBehaviour
{
    [HideInInspector]
    public int ID = 1;
    [HideInInspector]
    public Team MyTeam = Team.Blue;
    bool InControl;
    bool CanGenEnergy;
    DragUnitCtrl UnitDrag;
    [HideInInspector]
    public Dictionary<string, GameObject> DeckUnits;

    [SerializeField] private GameObject characterPrefab;
    
    // Spawn points setup
    [Header("Unit Spawning")]
    [Tooltip("List of empty GameObjects that will act as spawn points. Units will spawn at these local positions.")]
    public List<Transform> spawnPoints = new List<Transform>();
    [Tooltip("Whether to automatically deploy cards without drag & drop")]
    public bool useAutoDeployment = true;
    
    // Object Pooling Settings
    [Header("Object Pooling")]
    public int maxActiveUnits = 8; // Maximum active units for this player
    private List<Unit> activeUnits = new List<Unit>(); // Currently active units
    private Dictionary<string, List<Unit>> unitPool = new Dictionary<string, List<Unit>>(); // Pooled units by KeyId
    // Track the mapping between cards and their original ScriptableObjects for pooling
    private Dictionary<string, ScriptableObject> cardSOMapping = new Dictionary<string, ScriptableObject>();

    // Make PlayerDeck accessible
    private List<NFTsCard> playerDeck = new List<NFTsCard>();
    public List<NFTsCard> PlayerDeck => playerDeck;

    Mesh[] UnitsMeshs;
    Material[] UnitMaterials;
    GameObject[] ShipPreviews;
    GameObject[] SpellPreviews;
    int DragingCard;
    int SelectedCard;

    [Range(0, 99)]
    public float CurrentEnergy = 5;
    [Range(0, 99)]
    public float MaxEnergy = 10;
    [Range(0, 99)]
    public float SpeedEnergy = 1;
    
    [Header("Dash Settings")]
    [Range(0, 10)]
    public float dashEnergyCost = 2f;

    public ScriptableObject[] TestingDeck = new ScriptableObject[8];

    KeyCode[] Keys;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        Debug.Log("--PLAYER AWAKES--");
        GameMng.P = this;
        DeckUnits = new Dictionary<string, GameObject>();
        DragingCard = -1;
        SelectedCard = -1;
        
        playerMovement = GetComponent<PlayerMovement>();
        
        // Initialize unit pools
        activeUnits = new List<Unit>();
        unitPool = new Dictionary<string, List<Unit>>();

        // Initialize spawn points if none exist
        InitializeSpawnPoints();
    }

    // Initialize spawn points if none exist
    private void InitializeSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.Log("No spawn points found, creating default spawn points");
            
            // Create a container for spawn points
            GameObject spawnPointsContainer = new GameObject("SpawnPoints");
            spawnPointsContainer.transform.SetParent(transform);
            spawnPointsContainer.transform.localPosition = Vector3.zero;
            
            // Create default spawn points in an arc
            int numSpawnPoints = 5;
            float arcAngle = 120f;
            float radius = 20f;
            
            for (int i = 0; i < numSpawnPoints; i++)
            {
                float angle = -arcAngle/2 + (arcAngle * i / (numSpawnPoints - 1));
                float radians = angle * Mathf.Deg2Rad;
                
                // Create spawn point GameObject
                GameObject spawnPoint = new GameObject($"SpawnPoint_{i + 1}");
                spawnPoint.transform.SetParent(spawnPointsContainer.transform);
                
                // Set local position in arc formation
                spawnPoint.transform.localPosition = new Vector3(
                    Mathf.Sin(radians) * radius,
                    0f,
                    Mathf.Cos(radians) * radius
                );
                
                spawnPoints.Add(spawnPoint.transform);
            }
            
            Debug.Log($"Created {numSpawnPoints} default spawn points");
        }
    }

    // Get a spawn position in world space and its transform
    private (Vector3 position, Transform spawnTransform) GetSpawnPositionAndTransform()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return (transform.position, null);

        // Get a random spawn point
        int index = Random.Range(0, spawnPoints.Count);
        Transform spawnPoint = spawnPoints[index];
        if (spawnPoint == null)
            return (transform.position, null);

        // Return both the position and transform
        return (spawnPoint.position, spawnPoint);
    }
    
    // Maintain the original method for backward compatibility
    private Vector3 GetSpawnPosition()
    {
        var (position, _) = GetSpawnPositionAndTransform();
        return position;
    }
    
    // Helper method to clean up the active units list
    private void CleanupActiveUnitsList()
    {
        for (int i = activeUnits.Count - 1; i >= 0; i--)
        {
            Unit unit = activeUnits[i];
            
            // Remove if null or destroyed
            if (unit == null || unit.gameObject == null || !unit.gameObject.activeInHierarchy || unit.GetIsDeath())
            {
                activeUnits.RemoveAt(i);
            }
        }
    }
    
    // Get a unit from the pool or create a new one if none available
    private Unit GetUnitFromPool(string keyId, Vector3 position)
    {
        GameObject prefab = null;

        // First try to get the prefab from DeckUnits
        if (!string.IsNullOrEmpty(keyId) && DeckUnits.ContainsKey(keyId))
        {
            prefab = DeckUnits[keyId];
        }
        // If that fails, try to get it from the ScriptableObject mapping
        else if (!string.IsNullOrEmpty(keyId) && cardSOMapping.ContainsKey(keyId))
        {
            // Get prefab from ScriptableObject
            if (cardSOMapping[keyId] is ShipsDataBase shipCard)
            {
                prefab = shipCard.prefab;
                // Add to DeckUnits for future reference
                if (prefab != null && !DeckUnits.ContainsKey(keyId))
                {
                    DeckUnits[keyId] = prefab;
                }
            }
        }

        // If we still don't have a valid prefab, log error and exit
        if (prefab == null)
        {
            Debug.LogWarning($"Cannot get unit from pool: No prefab found for key {keyId}");
            return null;
        }
        
        // Initialize pool for this unit type if it doesn't exist
        if (!unitPool.ContainsKey(keyId))
        {
            unitPool[keyId] = new List<Unit>();
        }
        
        List<Unit> pool = unitPool[keyId];
        
        // First, clean up any null references in the pool
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null)
            {
                pool.RemoveAt(i);
            }
        }
        
        // Check if there's an available unit in the pool
        if (pool.Count > 0)
        {
            Unit unit = pool[0];
            pool.RemoveAt(0);
            
            // Double-check the unit is valid
            if (unit == null)
            {
                // If somehow our unit is null, create a new one
                return CreateNewUnit(keyId, position);
            }
            
            // Reactivate the unit
            unit.gameObject.SetActive(true);
            unit.transform.position = position;
            
            try
            {
                unit.ResetUnit(); // Reset the unit's state
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error resetting unit: {e.Message}");
                // If there's an error resetting, create a new unit instead
                GameMng.GM.DeleteUnit(unit);
                return CreateNewUnit(keyId, position);
            }
            
            return unit;
        }
        
        // If no unit in pool, create a new one
        return CreateNewUnit(keyId, position);
    }
    
    // Helper method to create a new unit
    private Unit CreateNewUnit(string keyId, Vector3 position)
    {
        GameObject prefab = null;
        
        // Try to get prefab from DeckUnits first
        if (DeckUnits.ContainsKey(keyId))
        {
            prefab = DeckUnits[keyId];
        }
        // If not in DeckUnits, try to get from ScriptableObject
        else if (cardSOMapping.ContainsKey(keyId) && cardSOMapping[keyId] is ShipsDataBase shipCard)
        {
            prefab = shipCard.prefab;
            // Add to DeckUnits for future reference
            if (prefab != null && !DeckUnits.ContainsKey(keyId))
            {
                DeckUnits[keyId] = prefab;
            }
        }
        
        if (prefab == null)
        {
            Debug.LogError($"Cannot create unit: prefab is null for key {keyId}");
            return null;
        }
        
        Unit newUnit = GameMng.GM.CreateUnit(prefab, position, MyTeam, keyId, ID);
        
        if (newUnit != null)
        {
            // Subscribe to unit's death event to return it to pool
            newUnit.OnUnitDeath += ReturnUnitToPool;
        }
        
        return newUnit;
    }
    
    // Return a unit to the pool when it dies
    private void ReturnUnitToPool(Unit unit)
    {
        if (unit == null)
        {
            Debug.LogWarning("Cannot return null unit to pool");
            return;
        }
        
        string keyId = unit.getKey();
        if (string.IsNullOrEmpty(keyId) || !unitPool.ContainsKey(keyId))
        {
            // If we can't determine the unit type or it's not in our pool, just destroy it
            GameMng.GM.DeleteUnit(unit);
            return;
        }
        
        try 
        {
            // Deactivate the unit instead of destroying it
            unit.gameObject.SetActive(false);
            
            // Add back to the pool
            unitPool[keyId].Add(unit);
            
            // Remove from active units list
            if (activeUnits.Contains(unit))
            {
                activeUnits.Remove(unit);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error returning unit to pool: {e.Message}");
            // If we can't return it to the pool safely, just destroy it
            GameMng.GM.DeleteUnit(unit);
        }
    }

    private void Start()
    {
        Debug.Log("--PLAYER STARTS--");
        Keys = new KeyCode[8] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8 };
        UnitDrag = FindFirstObjectByType<DragUnitCtrl>();
        UnitDrag.setMeshActive(false);
        InControl = CanGenEnergy = true;

        playerDeck = new List<NFTsCard>();
        
        // Track which card keys we've already processed to handle duplicates
        HashSet<string> processedCardKeys = new HashSet<string>();
        
        // Generate card instances
        int uniqueIdCounter = 1; // Used to create unique IDs for duplicates
        foreach (ScriptableObject card in TestingDeck)
        {
            if (card is ShipsDataBase shipCard)
            {
                NFTsUnit cardData = shipCard.ToNFTCard();
                
                // If this key already exists, generate a unique variant
                string originalKey = cardData.KeyId;
                while (processedCardKeys.Contains(cardData.KeyId))
                {
                    // Modify LocalID to make it unique
                    cardData.LocalID = cardData.LocalID * 100 + uniqueIdCounter;
                    uniqueIdCounter++;
                    
                    // This will update the KeyId through the property
                    string newKey = cardData.KeyId;
                    Debug.Log($"Created unique variant of card: {originalKey} -> {newKey}");
                }
                
                processedCardKeys.Add(cardData.KeyId);
                playerDeck.Add(cardData);
                // Add the NFT data to GameMng
                GameMng.GM.AddNftCardData(cardData, ID);
                // Store the mapping between the card KeyId and the original ScriptableObject
                cardSOMapping[cardData.KeyId] = card;
                Debug.Log($"Added card to SO mapping: {cardData.KeyId} -> {card.name}");
            }
            else if (card is SpellsDataBase spellCard)
            {
                NFTsSpell cardData = spellCard.ToNFTCard();
                
                // If this key already exists, generate a unique variant
                string originalKey = cardData.KeyId;
                while (processedCardKeys.Contains(cardData.KeyId))
                {
                    // Modify LocalID to make it unique
                    cardData.LocalID = cardData.LocalID * 100 + uniqueIdCounter;
                    uniqueIdCounter++;
                    
                    // This will update the KeyId through the property
                    string newKey = cardData.KeyId;
                    Debug.Log($"Created unique variant of card: {originalKey} -> {newKey}");
                }
                
                processedCardKeys.Add(cardData.KeyId);
                playerDeck.Add(cardData);
                // Add the NFT data to GameMng
                GameMng.GM.AddNftCardData(cardData, ID);
                // Store the mapping between the card KeyId and the original ScriptableObject
                cardSOMapping[cardData.KeyId] = card;
                Debug.Log($"Added card to SO mapping: {cardData.KeyId} -> {card.name}");
            }
        }

        SpellPreviews = new GameObject[8];
        ShipPreviews = new GameObject[8];
        UnitsMeshs = new Mesh[8];
        UnitMaterials = new Material[8];

        if (playerDeck.Count == 8)
        {
            for (int i = 0; i < 8; i++)
            {
                NFTsCard card = playerDeck[i];
                bool isSpell = (NFTClass)card.EntType == NFTClass.Skill;
                
                // Use the prefab from the NFTs data directly instead of loading from Resources
                GameObject prefab = card.Prefab;
                
                // Create a placeholder prefab if the original doesn't exist
                if (prefab == null)
                {
                    Debug.LogWarning($"Creating placeholder for missing prefab: {card.KeyId}, isSpell: {isSpell}");
                    prefab = new GameObject($"PlaceholderCard_{card.KeyId}");
                    
                    // Add a GameCard component to the placeholder
                    if (isSpell)
                    {
                        SpellCard placeholder = prefab.AddComponent<SpellCard>();
                        placeholder.PreviewEffect = new GameObject($"PlaceholderPreview_{card.KeyId}");
                        SpellPreviews[i] = placeholder.PreviewEffect;
                    }
                    else
                    {
                        UnitCard placeholder = prefab.AddComponent<UnitCard>();
                        GameObject unitMesh = new GameObject($"PlaceholderMesh_{card.KeyId}");
                        placeholder.UnitMesh = unitMesh;
                        
                        // Create minimal mesh structure
                        GameObject meshHolder = new GameObject("MeshHolder");
                        meshHolder.transform.SetParent(unitMesh.transform);
                        SkinnedMeshRenderer renderer = meshHolder.AddComponent<SkinnedMeshRenderer>();
                        renderer.sharedMesh = CreatePrimitiveMesh();
                        renderer.sharedMaterial = new Material(Shader.Find("Standard"));
                        
                        ShipPreviews[i] = placeholder.UnitMesh;
                        UnitsMeshs[i] = renderer.sharedMesh;
                        UnitMaterials[i] = renderer.sharedMaterial;
                    }
                }
                else
                {
                    GameCard gameCard = prefab.GetComponent<GameCard>();
                    
                    // Skip if GameCard component is missing
                    if (gameCard == null)
                    {
                        Debug.LogWarning($"Prefab for card {card.KeyId} is missing GameCard component, creating placeholder");
                        // Create a basic GameCard component on the prefab
                        if (isSpell)
                        {
                            SpellCard placeholder = prefab.AddComponent<SpellCard>();
                            placeholder.PreviewEffect = new GameObject($"PlaceholderPreview_{card.KeyId}");
                            SpellPreviews[i] = placeholder.PreviewEffect;
                        }
                        else
                        {
                            UnitCard placeholder = prefab.AddComponent<UnitCard>();
                            GameObject unitMesh = new GameObject($"PlaceholderMesh_{card.KeyId}");
                            placeholder.UnitMesh = unitMesh;
                            
                            // Create minimal mesh structure
                            GameObject meshHolder = new GameObject("MeshHolder");
                            meshHolder.transform.SetParent(unitMesh.transform);
                            SkinnedMeshRenderer renderer = meshHolder.AddComponent<SkinnedMeshRenderer>();
                            renderer.sharedMesh = CreatePrimitiveMesh();
                            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
                            
                            ShipPreviews[i] = placeholder.UnitMesh;
                            UnitsMeshs[i] = renderer.sharedMesh;
                            UnitMaterials[i] = renderer.sharedMaterial;
                        }
                    }
                    else if (isSpell)
                    {
                        SpellCard spell = gameCard as SpellCard;
                        SpellPreviews[i] = spell.PreviewEffect;
                    }
                    else
                    {
                        UnitCard unit = gameCard as UnitCard;
                        ShipPreviews[i] = unit.UnitMesh;
                        
                        // Ensure UnitMesh and renderers exist
                        if (unit.UnitMesh != null && 
                            unit.UnitMesh.transform.childCount > 0 &&
                            unit.UnitMesh.transform.GetChild(0).GetComponentInChildren<SkinnedMeshRenderer>() != null)
                        {
                            UnitsMeshs[i] = unit.UnitMesh.transform.GetChild(0).GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                            UnitMaterials[i] = unit.UnitMesh.transform.GetChild(0).GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                        }
                        else
                        {
                            Debug.LogWarning($"UnitCard for {card.KeyId} has invalid UnitMesh or SkinnedMeshRenderer, creating placeholder");
                            // Create fallback mesh and material
                            UnitsMeshs[i] = CreatePrimitiveMesh();
                            UnitMaterials[i] = new Material(Shader.Find("Standard"));
                        }
                    }
                }
                
                // Add to the deck only after ensuring we have a valid prefab
                // Check if key already exists to avoid duplicate key error
                if (!DeckUnits.ContainsKey(card.KeyId))
                {
                    DeckUnits.Add(card.KeyId, prefab);
                    
                    // Initialize pool for each card
                    unitPool[card.KeyId] = new List<Unit>();
                }
                else
                {
                    Debug.LogWarning($"Card with key {card.KeyId} already exists in the deck, not adding duplicate to dictionary");
                }
            }
        }

        GameMng.UI.InitGameCards(playerDeck.ToArray());
        Debug.Log("--PLAYER END START--");
    }

    private void Update()
    {
        // Update energy
        if (CanGenEnergy)
        {
            if (CurrentEnergy < MaxEnergy)
            {
                CurrentEnergy += SpeedEnergy * Time.deltaTime;
                if (CurrentEnergy > MaxEnergy)
                {
                    CurrentEnergy = MaxEnergy;
                }
                GameMng.UI.UpdateEnergy(CurrentEnergy, MaxEnergy);
            }
        }

        // Update control state
        if (InControl)
        {
            // Any input-related code here should use InputManager
            // For example, if you have any direct input checks, replace them with InputManager calls
        }
        
        // Clean up destroyed units from active units list
        CleanupActiveUnitsList();
    }

    public void SelectCard(int idu)
    {
        if (!InControl || idu < 0 || idu >= PlayerDeck.Count)
        {
            return;
        }

        if (SelectedCard == idu)
        {
            SelectedCard = -1;
            GameMng.UI.DeselectCards();
            UnitDrag.setMeshActive(false);
        }
        else
        {
            GameMng.UI.DeselectCards();
            SelectedCard = idu;
            GameMng.UI.SelectCard(idu);
            
            // Safely access preview objects with null checks
            if ((NFTClass)PlayerDeck[idu].EntType == NFTClass.Skill)
            {
                if (SpellPreviews != null && idu < SpellPreviews.Length && SpellPreviews[idu] != null)
                {
                    PrepareDeploy(SpellPreviews[idu], PlayerDeck[idu].EnergyCost);
                }
                else
                {
                    Debug.LogWarning($"Missing SpellPreview for card at index {idu}");
                    UnitDrag.setMeshActive(false);
                }
            }
            else
            {
                if (ShipPreviews != null && idu < ShipPreviews.Length && ShipPreviews[idu] != null)
                {
                    PrepareDeploy(ShipPreviews[idu], PlayerDeck[idu].EnergyCost);
                }
                else
                {
                    Debug.LogWarning($"Missing ShipPreview for card at index {idu}");
                    UnitDrag.setMeshActive(false);
                }
            }
        }
    }

    public void DragDeckUnit(int idu)
    {
        if (!InControl || idu < 0 || idu >= PlayerDeck.Count)
        {
            return;
        }

        DragingCard = idu;
        if (SelectedCard != -1 && SelectedCard != DragingCard)
        {
            SelectedCard = -1;
            GameMng.UI.DeselectCards();
        }

        // Safely access preview objects with null checks
        if ((NFTClass)PlayerDeck[idu].EntType == NFTClass.Skill)
        {
            if (SpellPreviews != null && idu < SpellPreviews.Length && SpellPreviews[idu] != null)
            {
                PrepareDeploy(SpellPreviews[idu], PlayerDeck[idu].EnergyCost);
            }
            else
            {
                Debug.LogWarning($"Missing SpellPreview for card at index {idu}");
                UnitDrag.setMeshActive(false);
            }
        }
        else
        {
            if (ShipPreviews != null && idu < ShipPreviews.Length && ShipPreviews[idu] != null)
            {
                PrepareDeploy(ShipPreviews[idu], PlayerDeck[idu].EnergyCost);
            }
            else
            {
                Debug.LogWarning($"Missing ShipPreview for card at index {idu}");
                UnitDrag.setMeshActive(false);
            }
        }
    }

    public void DropDeckUnit()
    {
        if (!InControl)
        {
            return;
        }

        if (UnitDrag.IsValid() && (DragingCard != -1 || SelectedCard != -1))
        {
            int cardIndex = DragingCard == -1 ? SelectedCard : DragingCard;
            if (cardIndex >= 0 && cardIndex < PlayerDeck.Count)
            {
                DeplyUnit(PlayerDeck[cardIndex]);
            }
        }
        UnitDrag.setMeshActive(false);
        DragingCard = -1;
        SelectedCard = -1;
        GameMng.UI.DeselectCards();
    }

    public void SetInControl(bool incontrol)
    {
        InControl = incontrol;
        if (!InControl)
        {
            UnitDrag.setMeshActive(false);
            DragingCard = -1;
        }
    }

    public void SetCanGenEnergy(bool can)
    {
        CanGenEnergy = can;
    }

    public void AddEnergy(float value)
    {
        if (!CanGenEnergy)
            return;

        if (CurrentEnergy < MaxEnergy)
        {
            CurrentEnergy += value;
            GameMng.MT.AddEnergyGenerated(value);
        }
        else if (CurrentEnergy >= MaxEnergy)
        {
            CurrentEnergy = MaxEnergy;
            GameMng.MT.AddEnergyWasted(value);
        }
        GameMng.UI.UpdateEnergy(CurrentEnergy, MaxEnergy);
    }

    public void RestEnergy(float value)
    {
        CurrentEnergy -= value;
        GameMng.MT.AddEnergyUsed(value);
        GameMng.UI.UpdateEnergy(CurrentEnergy, MaxEnergy);
    }

    public bool IsPreparingDeploy()
    {
        return DragingCard != -1 || SelectedCard != -1;
    }

    public void PrepareDeploy(Mesh mesh, Material mat, float cost)
    {
        UnitDrag.setMeshActive(true);
        UnitDrag.SetMeshAndTexture(mesh, mat);
        UnitDrag.transform.position = CMath.GetMouseWorldPos();
        UnitDrag.TargetCost = cost;
    }

    public void PrepareDeploy(GameObject preview, float cost)
    {
        UnitDrag.setMeshActive(false);
        UnitDrag.CreatePreviewObj(preview);
        UnitDrag.transform.position = CMath.GetMouseWorldPos();
        UnitDrag.TargetCost = cost;
    }

    // Original method with no specific position (uses random spawn position)
    public void DeplyUnit(NFTsCard nftcard)
    {
        DeplyUnit(nftcard, Vector3.zero);
    }

    // Modified method to use object pooling
    public void DeplyUnit(NFTsCard nftcard, Vector3 targetPosition)
    {
        if (nftcard.EnergyCost <= CurrentEnergy)
        {
            // Check if we've reached maximum active units for non-spell cards
            if ((NFTClass)nftcard.EntType != NFTClass.Skill && activeUnits.Count >= maxActiveUnits)
            {
                Debug.LogWarning($"Maximum active units ({maxActiveUnits}) reached, cannot deploy more units");
                return;
            }
            
            Debug.Log($"Attempting to deploy unit: {nftcard.KeyId}, SO mapping exists: {cardSOMapping.ContainsKey(nftcard.KeyId)}, DeckUnits exists: {DeckUnits.ContainsKey(nftcard.KeyId)}");
            
            Vector3 spawnPosition;
            Transform usedSpawnPoint = null;
            
            if (targetPosition != Vector3.zero)
            {
                spawnPosition = targetPosition;
            }
            else if (useAutoDeployment && spawnPoints != null && spawnPoints.Count > 0)
            {
                // Get a spawn point using a deterministic method to maintain units' assignment to spawn points
                int spawnPointIndex = 0;
                
                // If using hashed ID from card to pick spawn point
                if (nftcard != null && !string.IsNullOrEmpty(nftcard.KeyId))
                {
                    // Use card ID hash to pick a consistent spawn point for this card
                    spawnPointIndex = Mathf.Abs(nftcard.KeyId.GetHashCode()) % spawnPoints.Count;
                }
                else
                {
                    // Fallback to random spawn point
                    spawnPointIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
                }
                
                // Store the actual Transform reference
                usedSpawnPoint = spawnPoints[spawnPointIndex];
                spawnPosition = usedSpawnPoint.position;
            }
            else
            {
                spawnPosition = CMath.GetMouseWorldPos();
            }
            
            if ((NFTClass)nftcard.EntType != NFTClass.Skill)
            {
                // Use pooling for unit cards
                Unit unit = GetUnitFromPool(nftcard.KeyId, spawnPosition);
                
                if (unit != null)
                {
                    // Add to active units list
                    activeUnits.Add(unit);
                    
                    // Set spawn point and player reference for ships
                    Ship ship = unit as Ship;
                    if (ship != null)
                    {
                        // Pass both the world position and the actual spawn point Transform
                        ship.SetSpawnPoint(spawnPosition, usedSpawnPoint);
                        ship.SetPlayerTransform(transform);
                        
                        // Set initial follow properties based on unit type if needed
                        ship.followPlayerWhenIdle = true;
                        ship.returnToSpawnWhenIdle = true;
                    }
                    
                    RestEnergy(nftcard.EnergyCost);
                    GameMng.MT.AddDeploys(1);
                }
                else
                {
                    Debug.LogWarning($"Failed to get or create unit for {nftcard.KeyId}");
                }
            }
            else
            {
                // For spells, continue using the existing non-pooled approach
                GameObject spellPrefab = nftcard.Prefab ?? (DeckUnits.ContainsKey(nftcard.KeyId) ? DeckUnits[nftcard.KeyId] : null);
                
                if (spellPrefab != null)
                {
                    Vector3 targetSpellPosition = spawnPosition;
                    
                    if (useAutoDeployment && targetPosition == Vector3.zero)
                    {
                        Unit targetUnit = FindNearestEnemyUnit(spawnPosition);
                        if (targetUnit != null)
                        {
                            targetSpellPosition = targetUnit.transform.position;
                        }
                    }
                    
                    NFTsSpell spellCard = nftcard as NFTsSpell;
                    if (spellCard != null)
                    {
                        Spell spell = GameMng.GM.CreateSpell(spellPrefab, targetSpellPosition, MyTeam, nftcard.KeyId);
                        if (spell != null)
                        {
                            spell.PlayerId = ID;
                            RestEnergy(nftcard.EnergyCost);
                        }
                    }
                }
            }
        }
    }

    // Method to find the nearest enemy unit for auto-targeting spells
    private Unit FindNearestEnemyUnit(Vector3 fromPosition)
    {
        Unit nearestEnemy = null;
        float minDistance = float.MaxValue;
        
        // Get all units in the scene
        Unit[] allUnits = GameObject.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        foreach (Unit unit in allUnits)
        {
            // Skip if null, dead, or on the same team
            if (unit == null || unit.GetIsDeath() || unit.IsMyTeam(MyTeam))
                continue;
                
            float distance = Vector3.Distance(fromPosition, unit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestEnemy = unit;
            }
        }
        
        return nearestEnemy;
    }

    public int GetVsTeamInt()
    {
        return MyTeam == Team.Red ? 0 : 1;
    }

    public Team GetVsTeam()
    {
        return MyTeam == Team.Red ? Team.Blue : Team.Red;
    }

    public int GetVsId()
    {
        return ID == 1 ? 2 : 1;
    }

    // Add a helper method for creating a simple mesh
    private Mesh CreatePrimitiveMesh()
    {
        // Create a temporary cube and grab its mesh
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempCube);
        return cubeMesh;
    }
}
}
