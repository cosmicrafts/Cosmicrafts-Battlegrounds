//NFT master card for deck (can be unit or spell)

using System;
using UnityEngine;

public abstract class NFTsCard : NFTs
{
    public override string KeyId
    {
        get => $"{TypePrefix}_{FactionPrefix}_{LocalID}";
        set => base.KeyId = value;
    }

    public int EnergyCost { get; set; }
    
    // Direct reference to prefab to avoid resource loading issues
    public GameObject Prefab { get; set; }
}
