namespace Cosmicrafts {
using UnityEngine;
/*
    This is the in-game parent spell controller
 */
public class Spell : MonoBehaviour
{
    //The NFT data source
    protected NFTsSpell NFTs;
    
    //The spell's faction in the game
    public Faction MyFaction = Faction.Player;
    
    //The spell's team in the game - kept for backwards compatibility
    [System.Obsolete("Use MyFaction instead")]
    public Team MyTeam 
    {
        get { return MyFaction == Faction.Player ? Team.Blue : Team.Red; }
        set { MyFaction = value == Team.Blue ? Faction.Player : Faction.Enemy; }
    }
    
    //The owner's ID - kept for backwards compatibility
    [System.Obsolete("Use MyFaction instead")]
    public int PlayerId 
    {
        get { return MyFaction == Faction.Player ? 1 : 2; }
        set { MyFaction = value == 1 ? Faction.Player : Faction.Enemy; }
    }
    
    //The spell ID in the game
    protected int Id;

    //Duration of the spell, before die
    [Range(0, 300)]
    public float Duration = 1;

    // Start is called before the first frame update
    protected virtual void Start()
    {
        //Save the reference in the game manager
        GameMng.GM.AddSpell(this);
        //Destroy after duration
        if (Duration > 0)
        {
            Destroy(gameObject, Duration);
        }
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        
    }

    //Returns the NFT key
    public string getKey()
    {
        return NFTs.KeyId;
    }

    //Sets the ID of the spell
    public void setId(int id)
    {
        Id = id;
    }

    //Returns the ID of the spell
    public int getId()
    {
        return Id;
    }

    //When destroys, delete the reference on the game manager
    private void OnDestroy()
    {
        if (GameMng.GM != null)
        {
            GameMng.GM.DeleteSpell(this);
        }
    }

    //Sets the NFT data source
    public virtual void SetNfts(NFTsSpell nFTsSpell)
    {
        NFTs = nFTsSpell;

        if (nFTsSpell == null || GlobalManager.GMD == null || GlobalManager.GMD.DebugMode)
            return;
    }
}
}