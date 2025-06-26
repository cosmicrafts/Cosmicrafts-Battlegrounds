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
        public GameObject Prefab;
        
        [Tooltip("Spell icon sprite")]
        [Header("Spell Icon")]
        [SerializeField]
        public Sprite IconSprite;

        [Tooltip("Name of the spell")]
        [Header("Spell Name")]
        [SerializeField]
        public string Name;

        [Tooltip("Local ID of the spell")]
        [Header("Local ID")]
        [SerializeField]
        public int LocalID;

        [Tooltip("Faction of the spell")]
        [Header("Faction")]
        [SerializeField]
        public Factions Faction;

        [Tooltip("Energy cost of the spell")]
        [Header("Spell Cost")]
        [SerializeField]
        [Range(1, 9999)]
        public int EnergyCost;

        [Tooltip("Level of the spell")]
        [Header("Level")]
        [SerializeField]
        [Range(1, 99)]
        public int Level = 1;

        [Tooltip("Base damage of the spell")]
        [Header("Damage")]
        [SerializeField]
        [Range(0, 9999)]
        public int BaseDamage = 0;

        [Tooltip("Type of damage the spell deals")]
        [Header("Damage Type")]
        [SerializeField]
        public TypeDmg DamageType = TypeDmg.Normal;

        #endregion

        #region Read Variables

        public GameObject prefab => Prefab;
        public Sprite iconSprite => IconSprite;
        public string cardName => Name;
        public int localId => LocalID;
        public int faction => (int)Faction;
        public int cost => EnergyCost;
        public int level => Level;
        public int baseDamage => BaseDamage;
        public TypeDmg damageType => DamageType;

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
                IconSprite = iconSprite,
                Level = level,
                BaseDamage = baseDamage,
                DamageType = damageType
            };
            
            if (nFTsCard.IconSprite == null)
            {
                nFTsCard.IconSprite = ResourcesServices.LoadCardIcon(nFTsCard.KeyId);
            }
            
            return nFTsCard;
        }

        // Method to get scaled damage based on level
        public int GetScaledDamage()
        {
            return Mathf.RoundToInt(BaseDamage * (1 + (Level - 1) * 0.1f));
        }

        #endregion
    }
}
