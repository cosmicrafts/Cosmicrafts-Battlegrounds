using UnityEngine;
using Cosmicrafts;

namespace Cosmicrafts
{
    //NFT Spell class
    public class NFTsSpell : NFTsCard
    {
        public int BaseDamage { get; set; }
        public TypeDmg DamageType { get; set; }
        public new int Level { get; set; }
    }
}
