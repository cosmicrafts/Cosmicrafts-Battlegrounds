namespace Cosmicrafts 
{
    using System.Collections.Generic;
    using UnityEngine;

    /*
     * Bot script - the counterpart to Player.cs
     * Handles bot-specific logic for enemy units and bases
     */
    public class Bot : MonoBehaviour
    {
        [HideInInspector]
        public int ID = 2; // Default bot ID
        [HideInInspector]
        public Team MyTeam = Team.Red; // Default bot team

        [Header("Bot Settings")]
        public string botName = "EnemyBot";
        public float difficultyMultiplier = 1.0f; // Affects stats like damage/health

        // Reference to the bot's Unit component
        private Unit unitComponent;
        
        // Reference to the GameCharacter component (if any)
        private GameCharacter characterComponent;

        // For wave-based games
        [HideInInspector]
        public int waveNumber = 0;

        private void Awake()
        {
            // Get required components
            unitComponent = GetComponent<Unit>();
            characterComponent = GetComponent<GameCharacter>();

            if (unitComponent == null)
            {
                Debug.LogError($"Bot {name} is missing required Unit component!");
            }
        }

        private void Start()
        {
            // Ensure Unit component has matching team/ID
            if (unitComponent != null)
            {
                unitComponent.MyTeam = MyTeam;
                unitComponent.PlayerId = ID;
            }

            // Apply character overrides if available
            if (characterComponent != null && characterComponent.characterBaseSO != null)
            {
                characterComponent.characterBaseSO.ApplyOverridesToUnit(unitComponent);
                
                // Apply any bot-specific gameplay modifiers
                characterComponent.ApplyGameplayModifiers();
            }
            
            // Apply difficulty scaling if needed
            if (difficultyMultiplier != 1.0f)
            {
                ApplyDifficultyScaling();
            }
        }
        
        // Scale bot stats based on difficulty
        private void ApplyDifficultyScaling()
        {
            if (unitComponent == null) return;
            
            // Scale health and shield
            int scaledHP = Mathf.RoundToInt(unitComponent.HitPoints * difficultyMultiplier);
            int scaledShield = Mathf.RoundToInt(unitComponent.Shield * difficultyMultiplier);
            
            unitComponent.HitPoints = scaledHP;
            unitComponent.Shield = scaledShield;
            unitComponent.SetMaxHitPoints(scaledHP);
            unitComponent.SetMaxShield(scaledShield);
            
            // Scale other components too (damage for shooters, etc.)
            Shooter shooter = GetComponent<Shooter>();
            if (shooter != null)
            {
                shooter.BulletDamage = Mathf.RoundToInt(shooter.BulletDamage * difficultyMultiplier);
            }
            
            Ship ship = GetComponent<Ship>();
            if (ship != null)
            {
                ship.MaxSpeed *= Mathf.Sqrt(difficultyMultiplier); // Lower scaling for speed
            }
        }
        
        // Method to take damage (forwards to Unit component)
        public void TakeDamage(int damage, TypeDmg damageType = TypeDmg.Normal)
        {
            if (unitComponent != null)
            {
                unitComponent.AddDmg(damage, damageType);
            }
        }
        
        // Method to deploy a unit or ability (for bot AI)
        public Unit DeployUnit(GameObject unitPrefab, Vector3 position)
        {
            if (unitPrefab == null) return null;
            
            // Create the unit with the bot's team and ID
            Unit unit = GameMng.GM.CreateUnit(unitPrefab, position, MyTeam, "bot_unit", ID);
            
            // Apply bot's character effects if any
            if (characterComponent != null)
            {
                characterComponent.DeployUnit(unit);
            }
            
            return unit;
        }
        
        // Called when this bot is part of a wave system and its wave becomes active
        public void OnWaveActivated(int waveNumber)
        {
            this.waveNumber = waveNumber;
            
            // Adjust difficulty based on wave number if desired
            difficultyMultiplier = 1.0f + (waveNumber * 0.1f); // Example: 10% increase per wave
            ApplyDifficultyScaling();
            
            // Enable any wave-specific behavior
            enabled = true;
        }
    }
} 