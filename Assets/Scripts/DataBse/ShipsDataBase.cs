namespace Cosmicrafts {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName ="New Ship", menuName ="Create New Ship")]
    public class ShipsDataBase : ScriptableObject
    {
        #region DataBase

        //----Prefab-----------------------------------------------------------
        [Tooltip("Associated Prefab")]
        [Header("Prefab")]
        [SerializeField]
        protected GameObject Prefab;

        //----Ship's Icon-----------------------------------------------------------
        [Tooltip("Unit icon sprite")]
        [Header("Unit Icon")]
        [SerializeField]
        protected Sprite IconSprite;

        //----Ship's name-----------------------------------------------------------
        [Tooltip("Name of the card")]
        [Header("Unit Name")]
        [SerializeField]
        protected string Name;

        //----Nft local ID-----------------------------------------------------------
        [Tooltip("NFT local ID")]
        [Header("Local ID")]
        [SerializeField]
        protected int LocalID;

        //----Nft faction-----------------------------------------------------------
        [Tooltip("NFT Faction")]
        [Header("Faction")]
        [SerializeField]
        protected Factions Faction;

        //----Nft card type-----------------------------------------------------------
        [Tooltip("NFT Type")]
        [Header("NFT Type")]
        [SerializeField]
        protected NFTClass NftType;

        //----Unit HP-----------------------------------------------------------
        [Tooltip("Unit hit points")]
        [Header("HP")]
        [Range(1, 9999)]
        [SerializeField]
        protected int HitPoints;

        //----Unit Shield-----------------------------------------------------------
        [Tooltip("Unit shield points")]
        [Header("Shield")]
        [SerializeField]
        [Range(1, 9999)]
        protected int Shield;

        //----Unit card cost-----------------------------------------------------------
        [Tooltip("Energy cost of the unit")]
        [Header("Unit Cost")]
        [SerializeField]
        [Range(1, 9999)]
        protected int EnergyCost;

        //----Unit Damage-----------------------------------------------------------
        [Tooltip("Damage points per bullet")]
        [Header("Damage")]
        [SerializeField]
        [Range(0, 9999)]
        protected int Dammage;

        //----Unit Speed-----------------------------------------------------------
        [Tooltip("Ship speed")]
        [Header("Movement Speed")]
        [SerializeField]
        [Range(0, 99)]
        protected float Speed;

        //----Unit Level-----------------------------------------------------------
        [Tooltip("Unit level")]
        [Header("Level")]
        [SerializeField]
        [Range(1, 99)]
        protected int Level;

        #endregion

        #region Read Variables

        public GameObject prefab => Prefab;
        
        public Sprite iconSprite => IconSprite;

        public string cardName => Name;

        public int localId => LocalID;

        public int faction => (int)Faction;

        public int type => (int)NftType;

        public int hp => HitPoints;

        public int shield => Shield;

        public int cost => EnergyCost;

        public int dmg => Dammage;

        public float speed => Speed;

        public int level => Level;

        public NFTsUnit ToNFTCard()
        {
            NFTsUnit nFTsCard = new NFTsUnit()
            {
                EnergyCost = cost,
                HitPoints = hp,
                Shield = shield,
                Dammage = dmg,
                Faction = faction,
                EntType = type,
                LocalID = localId,
                TypePrefix = NFTsCollection.NFTsPrefix[type],
                FactionPrefix = NFTsCollection.NFTsFactionsPrefixs[(Factions)faction],
                Level = level,
                Speed = speed,
                Prefab = prefab,
                IconSprite = iconSprite // Use the sprite directly from the SO
            };
            
            // Fallback to resource loading only if no sprite is assigned
            if (nFTsCard.IconSprite == null)
            {
                nFTsCard.IconSprite = ResourcesServices.LoadCardIcon(nFTsCard.KeyId);
            }
            
            return nFTsCard;
        }

        #endregion
    }
}
