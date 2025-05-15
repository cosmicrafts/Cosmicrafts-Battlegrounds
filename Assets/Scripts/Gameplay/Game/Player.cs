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

    List<NFTsCard> PlayerDeck;
    Mesh[] UnitsMeshs;
    Material[] UnitMaterials;
    GameObject[] ShipPreviews;
    GameObject[] SpellPreviews;
    int DragingCard;
    int SelectedCard;
    GameCharacter MyCharacter;

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

private void Awake()
{
    Debug.Log("--PLAYER AWAKES--");
    GameMng.P = this;
    DeckUnits = new Dictionary<string, GameObject>();
    DragingCard = -1;
    SelectedCard = -1;
    
    // Get player movement component
    playerMovement = GetComponent<PlayerMovement>();

    GameObject basePrefabToUse = null;

    if (characterPrefab != null)
    {
        MyCharacter = Instantiate(characterPrefab, transform).GetComponent<GameCharacter>();

        if (MyCharacter.characterBaseSO != null && MyCharacter.characterBaseSO.BasePrefab != null)
        {
            basePrefabToUse = MyCharacter.characterBaseSO.BasePrefab;

            // Pass the prefab to GameMng and get the instantiated base station
            Unit baseStation = GameMng.GM.InitBaseStations(basePrefabToUse);

            // Apply overrides to the instantiated base station
            if (baseStation != null)
            {
                MyCharacter.characterBaseSO.ApplyOverridesToUnit(baseStation);
            }
        }
        else
        {
            Debug.LogError("CharacterBaseSO or BasePrefab is missing!");
        }
    }

    Debug.Log("--PLAYER END AWAKE--");
}

private void Start()
{
    Debug.Log("--PLAYER STARTS--");
    Keys = new KeyCode[8] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8 };
    UnitDrag = FindFirstObjectByType<DragUnitCtrl>();
    UnitDrag.setMeshActive(false);
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

    GameMng.UI.InitGameCards(PlayerDeck.ToArray());
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
                Unit unit = GameMng.GM.CreateUnit(unitPrefab, CMath.GetMouseWorldPos(), MyTeam, nftcard.KeyId, ID);
                if (MyCharacter != null)
                {
                    MyCharacter.DeployUnit(unit);
                }
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
                    Spell spell = GameMng.GM.CreateSpell(spellPrefab, CMath.GetMouseWorldPos(), MyTeam, nftcard.KeyId);
                    if (spell != null)
                    {
                        spell.PlayerId = ID; // Ensure PlayerId is set to match the player
                        if (MyCharacter != null)
                        {
                            MyCharacter.DeploySpell(spell);
                        }
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
