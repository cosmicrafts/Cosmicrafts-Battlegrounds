namespace Cosmicrafts {
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

/* 
 * This is the AI controller
 * Works with a timing loop and has 3 behavior modes
 * Uses its own energy and deck
 * The positions to deploy units are predefined
 */
public class BotEnemy : MonoBehaviour
{

    //Bot player Name 
    public string botName = "DefaultScriptName";
    //Bot player Avatar 
    public int botAvatar = 1;
    
    //Bot player ID (always 2)
    [HideInInspector]
    public readonly int ID = 2;

    //Bot game team (always red)
    [HideInInspector]
    public readonly Team MyTeam = Team.Red;

    // Spawn points setup - copied from Player.cs
    [Header("Unit Spawning")]
    [Tooltip("List of empty GameObjects that will act as spawn points. Units will spawn at these local positions.")]
    public List<Transform> spawnPoints = new List<Transform>();

    //Prefab Base Station (assign in inspector)
    public GameObject prefabBaseStation;

    //Prefabs Deck units (assign in inspector)
    public ShipsDataBase[] DeckUnits = new ShipsDataBase[8];
    
    //Current Energy
    [Range(0, 99)]
    public float CurrentEnergy = 30;

    //Max Energy
    [Range(0, 99)]
    public float MaxEnergy = 30;

    //Energy regeneration speed
    [Range(0, 99)]
    public float SpeedEnergy = 5;

    //Random Number created for the time between AI Spawn Units
    [Range(0, 3)]
    public float waitSpawn = 0.75f;

    // Object Pooling Settings
    [Header("Object Pooling")]
    public int maxActiveUnits = 8; // Maximum active units for this bot
    private List<Unit> activeUnits = new List<Unit>(); // Currently active units
    private Dictionary<ShipsDataBase, List<Unit>> unitPool = new Dictionary<ShipsDataBase, List<Unit>>(); // Pooled units by type

    //Delta time to make a decision
    WaitForSeconds IADelta;

    //Current own units in the game
    List<Unit> MyUnits;

    //NFTs data
    Dictionary<ShipsDataBase, NFTsUnit> DeckNfts;

    //Bot's enemy base station
    Unit TargetUnit;

    //The cost of the most expensive card
    int MaxCostUnit;
    //The cost of the cheapest card
    int MinCostUnit;

    //Random class service
    private System.Random rng;

    //Allows to generate energy
    private bool CanGenEnergy;
    private bool isPlayerAlive = true; // Track player state

    // Add reference to Unit component
    private Unit unitComponent;

    private void Awake() { }
    // Start is called before the first frame update
    void Start()
    {
        //Init Basic variables
        IADelta = new WaitForSeconds(waitSpawn);
        MyUnits = new List<Unit>();
        activeUnits = new List<Unit>();
        CanGenEnergy = true;
        rng = new System.Random();

        // Get Unit component
        unitComponent = GetComponent<Unit>();
        if (unitComponent != null)
        {
            // Subscribe to player state changes
            GameMng.GM.OnPlayerStateChanged += HandlePlayerStateChanged;
            
            // Subscribe to XP updates to sync level with player
            if (GameMng.UI != null)
            {
                GameMng.UI.OnXPUpdated += OnPlayerLevelUpdated;
            }
        }

        // Initialize spawn points if none exist
        InitializeSpawnPoints();

        //Add bot's base station to bot's units list and set the bot's enemy base station
        MyUnits.Add(GameMng.GM.Targets[0]);
        TargetUnit = GameMng.GM.Targets[1];

        //Init Deck Cards info with the units prefabs info
        DeckNfts = new Dictionary<ShipsDataBase, NFTsUnit>();
        unitPool = new Dictionary<ShipsDataBase, List<Unit>>();
        
        for (int i = 0; i < DeckUnits.Length; i++)
        {
            if (DeckUnits[i] != null)
            {
                NFTsUnit nFTsCard = DeckUnits[i].ToNFTCard();
                GameMng.GM.AddNftCardData(nFTsCard, 2);
                DeckNfts.Add(DeckUnits[i], nFTsCard);
                
                // Initialize pool for each unit type
                unitPool.Add(DeckUnits[i], new List<Unit>());
            }
        }

        //Set the max and min cost of the bot's deck
        MaxCostUnit = DeckUnits.Where(unit => unit != null).Max(f => f.cost);
        MinCostUnit = DeckUnits.Where(unit => unit != null).Min(f => f.cost);

        //Start AI loop
        StartCoroutine(IA());
    }

    private void OnDestroy()
    {
        // Unsubscribe from player state changes
        if (GameMng.GM != null)
        {
            GameMng.GM.OnPlayerStateChanged -= HandlePlayerStateChanged;
        }
        
        // Unsubscribe from XP updates
        if (GameMng.UI != null)
        {
            GameMng.UI.OnXPUpdated -= OnPlayerLevelUpdated;
        }
    }

    private void HandlePlayerStateChanged(bool isAlive)
    {
        isPlayerAlive = isAlive;
    }

    // Add method to handle player level updates
    private void OnPlayerLevelUpdated(int currentXP, int maxXP, int playerLevel)
    {
        if (unitComponent != null)
        {
            // Update bot's level to match player
            unitComponent.SetLevel(playerLevel);
            
            // Update UI if available
            UIUnit uiUnit = GetComponentInChildren<UIUnit>();
            if (uiUnit != null)
            {
                uiUnit.UpdateLevelText(playerLevel);
            }
        }
    }

    // Initialize spawn points if none exist - simplified from Player.cs
    private void InitializeSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.Log("Bot: No spawn points found, creating default spawn points");
            
            // Create a container for spawn points
            GameObject spawnPointsContainer = new GameObject("BotSpawnPoints");
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
                GameObject spawnPoint = new GameObject($"BotSpawnPoint_{i + 1}");
                spawnPoint.transform.SetParent(spawnPointsContainer.transform);
                
                // Set local position in arc formation
                spawnPoint.transform.localPosition = new Vector3(
                    Mathf.Sin(radians) * radius,
                    0f,
                    Mathf.Cos(radians) * radius
                );
                
                spawnPoints.Add(spawnPoint.transform);
            }
            
            Debug.Log($"Bot: Created {numSpawnPoints} default spawn points");
        }
    }

    // Get a spawn position from the spawn points - simplified from Player.cs
    private Vector3 GetSpawnPosition(ShipsDataBase unitData)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return transform.position;

        // Create a deterministic but unique mapping between units and spawn points
        int index = 0;
        
        if (unitData != null)
        {
            // Hash the unit name for consistent spawn point assignment
            index = Mathf.Abs(unitData.name.GetHashCode()) % spawnPoints.Count;
            
            // Check if this spawn point is already in use
            bool spawnPointInUse = false;
            foreach (Unit activeUnit in activeUnits)
            {
                if (activeUnit != null && 
                    Vector3.Distance(activeUnit.transform.position, spawnPoints[index].position) < 2f)
                {
                    spawnPointInUse = true;
                    break;
                }
            }
            
            // If spawn point is in use, find the next available one
            if (spawnPointInUse)
            {
                int attempts = 0;
                while (spawnPointInUse && attempts < spawnPoints.Count)
                {
                    index = (index + 1) % spawnPoints.Count;
                    attempts++;
                    
                    // Check if this new point is in use
                    spawnPointInUse = false;
                    foreach (Unit activeUnit in activeUnits)
                    {
                        if (activeUnit != null && 
                            Vector3.Distance(activeUnit.transform.position, spawnPoints[index].position) < 2f)
                        {
                            spawnPointInUse = true;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            // Fallback to random spawn point
            index = Random.Range(0, spawnPoints.Count);
        }

        Transform spawnPoint = spawnPoints[index];
        if (spawnPoint == null)
            return transform.position;

        return spawnPoint.position;
    }

    // Update is called once per frame
    void Update()
    {
        //Generate energy if the bot can
        if (!CanGenEnergy)
            return;
        
        if (CurrentEnergy < MaxEnergy)
        {
            CurrentEnergy += Time.deltaTime * SpeedEnergy;
        }
        else if (CurrentEnergy > MaxEnergy)
        {
            CurrentEnergy = MaxEnergy;
        }
        
        // Clean up destroyed units from active units list
        CleanupActiveUnitsList();
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
        
        // Log warning if somehow we exceed max units
        if (activeUnits.Count > maxActiveUnits)
        {
            Debug.LogWarning($"Bot has {activeUnits.Count} active units, exceeding maximum of {maxActiveUnits}");
        }
    }

    //Set if the bot can generate energy
    public void SetCanGenEnergy(bool can)
    {
        CanGenEnergy = can;
    }
    
    // Modify GetUnitFromPool to ensure spawned units match player level
    private Unit GetUnitFromPool(ShipsDataBase unitData, Vector3 position)
    {
        if (unitData == null || unitData.prefab == null)
        {
            Debug.LogWarning("Cannot get unit from pool: unitData or prefab is null");
            return null;
        }
        
        List<Unit> pool = unitPool[unitData];
        
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
                return CreateNewUnit(unitData, position);
            }
            
            // Reactivate the unit
            unit.gameObject.SetActive(true);
            unit.transform.position = position;
            
            try
            {
                unit.ResetUnit(); // Reset the unit's state
                
                // Set level to match player
                if (GameMng.P != null)
                {
                    unit.SetLevel(GameMng.P.PlayerLevel);
                    
                    // Update UI if available
                    UIUnit uiUnit = unit.GetComponentInChildren<UIUnit>();
                    if (uiUnit != null)
                    {
                        uiUnit.UpdateLevelText(GameMng.P.PlayerLevel);
                    }
                }
            }
            catch (System.Exception)
            {
                // If there's an error resetting, create a new unit instead
                GameMng.GM.DeleteUnit(unit);
                return CreateNewUnit(unitData, position);
            }
            
            return unit;
        }
        
        // If no unit in pool, create a new one
        return CreateNewUnit(unitData, position);
    }
    
    // Modify CreateNewUnit to ensure new units match player level
    private Unit CreateNewUnit(ShipsDataBase unitData, Vector3 position)
    {
        if (unitData.prefab == null)
        {
            Debug.LogError($"Cannot create unit: prefab is null for unit {unitData.name}");
            return null;
        }
        
        Unit newUnit = GameMng.GM.CreateUnit(unitData.prefab, position, MyTeam, DeckNfts[unitData].KeyId, 2);
        
        if (newUnit != null)
        {
            // Subscribe to unit's death event to return it to pool
            newUnit.OnUnitDeath += ReturnUnitToPool;
            
            // Set level to match player
            if (GameMng.P != null)
            {
                newUnit.SetLevel(GameMng.P.PlayerLevel);
                
                // Update UI if available
                UIUnit uiUnit = newUnit.GetComponentInChildren<UIUnit>();
                if (uiUnit != null)
                {
                    uiUnit.UpdateLevelText(GameMng.P.PlayerLevel);
                }
            }
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
        
        // Find which unit type this is
        ShipsDataBase unitType = null;
        foreach (var kvp in DeckNfts)
        {
            if (!string.IsNullOrEmpty(unit.getKey()) && unit.getKey() == kvp.Value.KeyId)
            {
                unitType = kvp.Key;
                break;
            }
        }
        
        if (unitType != null)
        {
            try 
            {
                // Deactivate the unit instead of destroying it
                unit.gameObject.SetActive(false);
                
                // Add back to the pool
                unitPool[unitType].Add(unit);
                
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
        else
        {
            // If we can't determine the unit type, just destroy it
            GameMng.GM.DeleteUnit(unit);
        }
    }

    //AI Decision algorithm
    IEnumerator IA()
    {
        //Check if the game is not ended
        while(!GameMng.GM.IsGameOver())
        {
            yield return IADelta;//Add a delay time to think
            //Check if the game is not ended and the player base station still exists 
            if (TargetUnit == null || GameMng.GM.IsGameOver())
            {
                break;
            }
            
            // Skip spawning if player is dead
            if (!isPlayerAlive)
            {
                continue;
            }
            
            // Check if we've reached maximum active units
            if (activeUnits.Count >= maxActiveUnits)
            {
                continue;
            }
            
            // Get only non-null units from the deck
            ShipsDataBase[] validUnits = DeckUnits.Where(unit => unit != null).ToArray();
            
            // Skip if there are no valid units
            if (validUnits.Length == 0)
            {
                yield return IADelta;
                continue;
            }
            
            //Select first unit as default to spawn
            ShipsDataBase SelectedUnit = validUnits[0];
            
            //Mix game cards
            validUnits = validUnits.OrderBy(r => rng.Next()).ToArray();
            
            //Select a unit depending on the AI mode and current energy
            if (CurrentEnergy < MaxCostUnit)
            {
                continue;
            }
            
            for (int i = 0; i < 10; i++)
            {
                SelectedUnit = validUnits[Random.Range(0, validUnits.Length)];
                if (SelectedUnit.cost <= CurrentEnergy)
                {
                    break;
                }
            }

            //Check if the bot has enough energy
            if (SelectedUnit.cost <= CurrentEnergy && activeUnits.Count < maxActiveUnits)
            {
                // Use spawn points system instead of random child position
                Vector3 PositionSpawn = GetSpawnPosition(SelectedUnit);
                
                // Debug.Log($"Bot spawning {SelectedUnit.name} at position {PositionSpawn}");

                //Get or create unit from pool
                Unit unit = GetUnitFromPool(SelectedUnit, PositionSpawn);
                
                // Add to active units list only if unit is valid
                if (unit != null && unit.gameObject != null)
                {
                    activeUnits.Add(unit);
                    CurrentEnergy -= SelectedUnit.cost;
                }
            }
        }
    }
}
}