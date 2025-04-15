namespace Cosmicrafts {
using DG.Tweening;
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
    //Bot player Level 
    public int botLv = 5;
    //Bot player Avatar 
    public int botAvatar = 1;
    
    //Bot player ID (always 2)
    [HideInInspector]
    public readonly int ID = 2;

    //Bot game team (always red)
    [HideInInspector]
    public readonly Team MyTeam = Team.Red;

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
    bool CanGenEnergy;
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
    }

    //Set if the bot can generate energy
    public void SetCanGenEnergy(bool can)
    {
        CanGenEnergy = can;
    }
    
    // Get a unit from the pool or create a new one if none available
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
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error resetting unit: {e.Message}");
                // If there's an error resetting, create a new unit instead
                GameMng.GM.DeleteUnit(unit);
                return CreateNewUnit(unitData, position);
            }
            
            return unit;
        }
        
        // If no unit in pool, create a new one
        return CreateNewUnit(unitData, position);
    }
    
    // Helper method to create a new unit
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
                //Select a random position (check the child game objects of the bot)
                Vector3 PositionSpawn = transform.GetChild(Random.Range(0, transform.childCount)).position;

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