namespace Cosmicrafts
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "New Spell", menuName = "Create New Spell")]
    public class SpellsDataBase : ScriptableObject
    {
        #region DataBase

        [Tooltip("Associated Prefab")]
        [Header("Prefab")]
        [SerializeField]
        protected GameObject Prefab;
        
        [Tooltip("Spell icon sprite")]
        [Header("Spell Icon")]
        [SerializeField]
        protected Sprite IconSprite;

        [Tooltip("Name of the spell")]
        [Header("Spell Name")]
        [SerializeField]
        protected string Name;

        [Tooltip("Local ID of the spell")]
        [Header("Local ID")]
        [SerializeField]
        protected int LocalID;

        [Tooltip("Faction of the spell")]
        [Header("Faction")]
        [SerializeField]
        protected Factions Faction;

        [Tooltip("Energy cost of the spell")]
        [Header("Spell Cost")]
        [SerializeField]
        [Range(1, 9999)]
        protected int EnergyCost;

        #endregion

        #region Read Variables

        public GameObject prefab => Prefab;
        public Sprite iconSprite => IconSprite;
        public string cardName => Name;
        public int localId => LocalID;
        public int faction => (int)Faction;
        public int cost => EnergyCost;

        public NFTsSpell ToNFTCard()
        {
            NFTsSpell nFTsCard = new NFTsSpell()
            {
                EnergyCost = cost,
                Faction = faction,
                EntType = (int)NFTClass.Skill,
                LocalID = localId,
                TypePrefix = NFTsCollection.NFTsPrefix[(int)NFTClass.Skill],
                FactionPrefix = NFTsCollection.NFTsFactionsPrefixs[(Factions)faction],
                Prefab = prefab,
                IconSprite = iconSprite
            };
            
            if (nFTsCard.IconSprite == null)
            {
                nFTsCard.IconSprite = ResourcesServices.LoadCardIcon(nFTsCard.KeyId);
            }
            
            return nFTsCard;
        }

        #endregion
    }
}
