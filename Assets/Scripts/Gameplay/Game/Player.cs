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
    // These will be set by GameMng during instantiation
    [HideInInspector]
    public int ID = 1;
    
    // Replace Team with Faction
    [HideInInspector]
    public Faction MyFaction = Faction.Player;
    
    // Keep for backwards compatibility - automatically computed from MyFaction
    [System.Obsolete("Use MyFaction instead")]
    [HideInInspector]
    public Team MyTeam 
    {
        get { return MyFaction == Faction.Player ? Team.Blue : Team.Red; }
        set { MyFaction = value == Team.Blue ? Faction.Player : Faction.Enemy; }
    }

    bool InControl;
    bool CanGenEnergy;
    [HideInInspector]
    public Dictionary<string, GameObject> DeckUnits;

    List<NFTsCard> PlayerDeck;
    Mesh[] UnitsMeshs;
    Material[] UnitMaterials;
    GameObject[] ShipPreviews;
    GameObject[] SpellPreviews;
    int DragingCard;
    int SelectedCard;
    // Remove GameCharacter reference
    
    [Range(0, 99)]
    public float CurrentEnergy = 5;
    [Range(0, 99)]
    public float MaxEnergy = 10;
    [Range(0, 99)]
    public float SpeedEnergy = 1;
    
    [Header("Dash Settings")]
    [Range(0, 10)]
    public float dashEnergyCost = 2f; // Energy cost for dashing

    // This array is for you to populate directly in the inspector with your ships and spells
    public ScriptableObject[] TestingDeck = new ScriptableObject[8]; 

    KeyCode[] Keys;
    private PlayerMovement playerMovement;

    // Static methods for UI events to access the player instance
    public static void OnUICardClick(int cardIndex)
    {
        if (GameMng.P != null)
        {
            GameMng.P.SelectCard(cardIndex);
        }
        else
        {
            Debug.LogWarning("Player instance not found when trying to select card.");
        }
    }
    
    public static void OnUICardDragStart(int cardIndex)
    {
        if (GameMng.P != null)
        {
            GameMng.P.DragDeckUnit(cardIndex);
        }
        else
        {
            Debug.LogWarning("Player instance not found when trying to drag card.");
        }
    }
    
    public static void OnUICardDragEnd()
    {
        if (GameMng.P != null)
        {
            GameMng.P.DropDeckUnit();
        }
        else
        {
            Debug.LogWarning("Player instance not found when trying to drop card.");
        }
    }

    private void Awake()
    {
        Debug.Log("--PLAYER AWAKES--");
        // GameMng.P is set in Start after ID/Team are potentially set
        DeckUnits = new Dictionary<string, GameObject>();
        DragingCard = -1;
        SelectedCard = -1;

        // Get components from this GameObject
        playerMovement = GetComponent<PlayerMovement>();
        
        // No longer need to get or create GameCharacter
        Debug.Log("--PLAYER END AWAKE--");
    }

    private void Start()
    {
        // Set the static reference now that ID/Team should be initialized
        GameMng.P = this;
        Debug.Log("--PLAYER STARTS--");
        Keys = new KeyCode[8] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8 };
        
        // Check if DragUnitCtrl exists in the scene, if not, create it
        if (DragUnitCtrl.Instance == null)
        {
            GameObject dragCtrlObj = new GameObject("DragUnitCtrl");
            DragUnitCtrl dragCtrl = dragCtrlObj.AddComponent<DragUnitCtrl>();
            
            // Add required components
            dragCtrlObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = dragCtrlObj.AddComponent<MeshRenderer>();
            // Create a child object for the visual
            GameObject visualObj = new GameObject("Visual");
            visualObj.transform.SetParent(dragCtrlObj.transform);
            
            // Set up the required components
            dragCtrl.MyMesh = meshRenderer;
            dragCtrl.MyMeshFilter = dragCtrlObj.GetComponent<MeshFilter>();
            
            // Add lightweight outline controller
            OutlineController outlineComp = visualObj.AddComponent<OutlineController>();
            dragCtrl.Outline = outlineComp;
            
            Debug.Log("Created DragUnitCtrl GameObject automatically");
        }
        
        // Check if it exists now before using it
        if (DragUnitCtrl.Instance != null) {
            DragUnitCtrl.Instance.setMeshActive(false);
        }
        else {
            Debug.LogWarning("DragUnitCtrl.Instance is still null after attempted creation.");
        }

        InControl = CanGenEnergy = true;

        PlayerDeck = new List<NFTsCard>();
        
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
                PlayerDeck.Add(cardData);
                // Add the NFT data to GameMng
                GameMng.GM.AddNftCardData(cardData, ID);
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
                PlayerDeck.Add(cardData);
                // Add the NFT data to GameMng
                GameMng.GM.AddNftCardData(cardData, ID);
            }
        }

        SpellPreviews = new GameObject[8];
        ShipPreviews = new GameObject[8];
        UnitsMeshs = new Mesh[8];
        UnitMaterials = new Material[8];

        if (PlayerDeck.Count == 8)
        {
            for (int i = 0; i < 8; i++)
            {
                NFTsCard card = PlayerDeck[i];
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
                }
                else
                {
                    Debug.LogWarning($"Card with key {card.KeyId} already exists in the deck, not adding duplicate to dictionary");
                }
            }
        }

        if (GameMng.UI != null)
        {
            GameMng.UI.InitGameCards(PlayerDeck.ToArray());
        } else {
             Debug.LogWarning("GameMng.UI is null, cannot initialize game cards UI.");
        }
        Debug.Log("--PLAYER END START--");
    }

    private void Update()
    {
        if (!InControl)
        {
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            if (Input.GetKeyDown(Keys[i]) && DragUnitCtrl.Instance.IsValid())
                DeplyUnit(PlayerDeck[i]);
        }

        AddEnergy(Time.deltaTime * SpeedEnergy);
    }

    public void SelectCard(int idu)
    {
        if (!InControl || idu < 0 || idu >= PlayerDeck.Count || DragUnitCtrl.Instance == null)
        {
            return;
        }

        if (SelectedCard == idu)
        {
            SelectedCard = -1;
            GameMng.UI.DeselectCards();
            DragUnitCtrl.Instance.setMeshActive(false);
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
                    DragUnitCtrl.Instance.setMeshActive(false);
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
                    DragUnitCtrl.Instance.setMeshActive(false);
                }
            }
        }
    }

    public void DragDeckUnit(int idu)
    {
        if (!InControl || idu < 0 || idu >= PlayerDeck.Count || DragUnitCtrl.Instance == null)
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
                DragUnitCtrl.Instance.setMeshActive(false);
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
                DragUnitCtrl.Instance.setMeshActive(false);
            }
        }
    }

    public void DropDeckUnit()
    {
        if (!InControl || DragUnitCtrl.Instance == null)
        {
            return;
        }

        if (DragUnitCtrl.Instance.IsValid() && (DragingCard != -1 || SelectedCard != -1))
        {
            int cardIndex = DragingCard == -1 ? SelectedCard : DragingCard;
            if (cardIndex >= 0 && cardIndex < PlayerDeck.Count)
            {
                DeplyUnit(PlayerDeck[cardIndex]);
            }
        }
        DragUnitCtrl.Instance.setMeshActive(false);
        DragingCard = -1;
        SelectedCard = -1;
        GameMng.UI.DeselectCards();
    }

    public void SetInControl(bool incontrol)
    {
        InControl = incontrol;
        if (!InControl && DragUnitCtrl.Instance != null)
        {
            DragUnitCtrl.Instance.setMeshActive(false);
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
        // Null check for UI
        if (GameMng.UI != null)
        {
            GameMng.UI.UpdateEnergy(CurrentEnergy, MaxEnergy);
        } else {
             Debug.LogWarning("GameMng.UI is null, cannot update energy UI.");
        }
    }

    public bool IsPreparingDeploy()
    {
        return DragingCard != -1 || SelectedCard != -1;
    }

    public void PrepareDeploy(Mesh mesh, Material mat, float cost)
    {
        if (DragUnitCtrl.Instance == null) return;
        
        DragUnitCtrl.Instance.setMeshActive(true);
        DragUnitCtrl.Instance.SetMeshAndTexture(mesh, mat);
        DragUnitCtrl.Instance.transform.position = CMath.GetMouseWorldPos();
        DragUnitCtrl.Instance.TargetCost = cost;
    }

    public void PrepareDeploy(GameObject preview, float cost)
    {
        if (DragUnitCtrl.Instance == null) return;
        
        DragUnitCtrl.Instance.setMeshActive(false);
        DragUnitCtrl.Instance.CreatePreviewObj(preview);
        DragUnitCtrl.Instance.transform.position = CMath.GetMouseWorldPos();
        DragUnitCtrl.Instance.TargetCost = cost;
    }

    public void DeplyUnit(NFTsCard nftcard)
    {
        if (nftcard.EnergyCost <= CurrentEnergy)
        {
            if ((NFTClass)nftcard.EntType != NFTClass.Skill)
            {
                // First try to use the prefab from the NFTsCard directly
                GameObject unitPrefab = nftcard.Prefab;
                
                // If not available, fall back to the deck prefab
                if (unitPrefab == null && DeckUnits.ContainsKey(nftcard.KeyId))
                {
                    unitPrefab = DeckUnits[nftcard.KeyId];
                }
                
                if (unitPrefab != null)
                {
                    Unit unit = GameMng.GM.CreateUnit(unitPrefab, CMath.GetMouseWorldPos(), FactionManager.ConvertFactionToTeam(MyFaction), nftcard.KeyId, ID);
                    
                    // No longer using GameCharacter for deployment
                    
                    RestEnergy(nftcard.EnergyCost);
                    GameMng.MT.AddDeploys(1);
                }
                else
                {
                    Debug.LogWarning($"Attempted to deploy missing unit prefab for {nftcard.KeyId}");
                }
            }
            else // If the card is a spell
            {
                // First try to use the prefab from the NFTsCard directly
                GameObject spellPrefab = nftcard.Prefab;
                
                // If not available, fall back to the deck prefab
                if (spellPrefab == null && DeckUnits.ContainsKey(nftcard.KeyId))
                {
                    spellPrefab = DeckUnits[nftcard.KeyId];
                }
                
                if (spellPrefab != null)
                {
                    // Pass ID to create spell to ensure NFT data gets properly set
                    NFTsSpell spellCard = nftcard as NFTsSpell;
                    if (spellCard != null)
                    {
                        Spell spell = GameMng.GM.CreateSpell(spellPrefab, CMath.GetMouseWorldPos(), FactionManager.ConvertFactionToTeam(MyFaction), nftcard.KeyId);
                        if (spell != null)
                        {
                            spell.MyFaction = MyFaction; // Set the spell's faction to match the player
                            
                            // No longer using GameCharacter for deployment
                            
                            RestEnergy(nftcard.EnergyCost);
                        }
                        else
                        {
                            Debug.LogError($"Failed to create spell from prefab for {nftcard.KeyId}. Make sure the prefab has a Spell component.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to cast NFTsCard to NFTsSpell for {nftcard.KeyId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Attempted to deploy missing spell prefab for {nftcard.KeyId}");
                }
            }
        }
    }

    public Team GetVsTeam()
    {
        // Convert opposing faction to team for backwards compatibility
        Faction opposingFaction = MyFaction == Faction.Player ? Faction.Enemy : Faction.Player;
        return FactionManager.ConvertFactionToTeam(opposingFaction);
    }
    
    public Faction GetVsFaction()
    {
        // Get opposing faction directly
        return MyFaction == Faction.Player ? Faction.Enemy : Faction.Player;
    }
    
    public int GetVsTeamInt()
    {
        return GetVsTeam() == Team.Blue ? 1 : 2;
    }
    
    public int GetVsId()
    {
        // Return ID of the opponent
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
