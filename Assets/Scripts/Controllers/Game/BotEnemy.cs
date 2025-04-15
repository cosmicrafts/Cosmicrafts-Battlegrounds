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
        CanGenEnergy = true;
        rng = new System.Random();

        //Add bot's base station to bot's units list and set the bot's enemy base station
        MyUnits.Add(GameMng.GM.Targets[0]);
        TargetUnit = GameMng.GM.Targets[1];

        //Init Deck Cards info with the units prefabs info
        DeckNfts = new Dictionary<ShipsDataBase, NFTsUnit>();
        for (int i = 0; i < DeckUnits.Length; i++)
        {
            if (DeckUnits[i] != null)
            {
                NFTsUnit nFTsCard = DeckUnits[i].ToNFTCard();
                GameMng.GM.AddNftCardData(nFTsCard, 2);
                DeckNfts.Add(DeckUnits[i], nFTsCard);
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
    }

    //Set if the bot can generate energy
    public void SetCanGenEnergy(bool can)
    {
        CanGenEnergy = can;
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
            if (SelectedUnit.cost <= CurrentEnergy && GameMng.GM.CountUnits(Team.Red) < 30)
            {
                //Select a random position (check the child game objects of the bot)
                Vector3 PositionSpawn = transform.GetChild(Random.Range(0, transform.childCount)).position;

                //Spawn selected unit and subtract energy
                Unit unit = GameMng.GM.CreateUnit(SelectedUnit.prefab,
                                                 PositionSpawn,
                                                 MyTeam,
                                                 DeckNfts[SelectedUnit].KeyId, 2);

                CurrentEnergy -= SelectedUnit.cost;
            }
        }
    }
}
}