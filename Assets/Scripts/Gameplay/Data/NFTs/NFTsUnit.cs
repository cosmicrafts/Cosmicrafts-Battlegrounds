using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    public class NFTsUnit : NFTsCard
    {
        public int HitPoints;
        public int Shield;
        public int Damage;
        public float Speed;
        
        // Combat range properties 
        public float AttackRange = 10f;
        public float DetectionRange = 15f;
        
        // Using 'new' keyword to explicitly hide the base class property
        public new GameObject Prefab;
    }
} 