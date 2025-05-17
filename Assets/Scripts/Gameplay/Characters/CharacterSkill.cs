using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cosmicrafts
{

    public enum SkillApplicationType
    {
        OnDeployUnit,
        GameplayModifier
    }

    public enum SkillType
    {
        Offensive,
        Defensive,
        Utility
    }

    public enum SkillTarget
    {
        Self,
        Enemy,
        AllUnits,
        Allies,
        Global
    }
    public enum SkillName
    {
        CriticalStrikeChance,
        CoolDown,
        ShieldMultiplier,
        DodgeChance,
        SpawnAreaSize,
        RangeDetector,
        HitPointsMultiplier,
        MaxSpeedMultiplier,
        SizeMultiplier,
        SpellDurationMultiplier,
        SpeedEnergyMultiplier,
        BotEnergyMultiplier,
        RadiusMultiplier // New skill for explosion spell radius
        // Add more as needed
    }

    [Serializable]
    public class CharacterSkill
    {
        public SkillName skillName;  // Use the enum instead of a string
        public SkillType Type;
        public SkillTarget Target;
        public float Multiplier;
        public SkillApplicationType ApplicationType;

        // Constructor
        public CharacterSkill(SkillName skillName, SkillType type, SkillTarget target, float multiplier, SkillApplicationType applicationType)
        {
            this.skillName = skillName;
            Type = type;
            Target = target;
            Multiplier = multiplier;
            ApplicationType = applicationType;
        }

        // Default constructor for Unity serialization
        public CharacterSkill() { }

        // Method to apply the skill's effect to a Unit or a Spell
        public void ApplySkill(object targetObject)
        {
            if (targetObject is Unit unit)
            {
                ApplySkillToUnit(unit);
            }
            else if (targetObject is Spell spell)
            {
                ApplySkillToSpell(spell);
            }
            else
            {
                Debug.LogWarning($"Skill {skillName} not applicable to the target object.");
            }
        }

        // Apply the skill's effect to a unit
        private void ApplySkillToUnit(Unit unit)
        {
            switch (skillName)
            {
                case SkillName.CriticalStrikeChance:
                    unit.GetComponent<Shooter>().criticalStrikeChance += Multiplier;
                    break;
                case SkillName.CoolDown:
                    unit.GetComponent<Shooter>().CoolDown *= Multiplier;
                    break;
                case SkillName.ShieldMultiplier:
                    unit.Shield = (int)(unit.Shield * Multiplier);
                    unit.SetMaxShield(unit.Shield);
                    break;
                case SkillName.DodgeChance:
                    unit.DodgeChance += Multiplier;
                    break;
                case SkillName.SpawnAreaSize:
                    unit.GetComponent<Ship>().SpawnAreaSize += Multiplier;
                    break;
                case SkillName.RangeDetector:
                    unit.GetComponent<Shooter>().RangeDetector += Multiplier;
                    break;
                case SkillName.HitPointsMultiplier:
                    unit.HitPoints = (int)(unit.HitPoints * Multiplier);
                    unit.SetMaxHitPoints(unit.HitPoints);
                    break;
                case SkillName.MaxSpeedMultiplier:
                    unit.GetComponent<Ship>().MaxSpeed *= Multiplier;
                    break;
                case SkillName.SizeMultiplier:
                    unit.Size *= Multiplier;
                    break;
                default:
                    Debug.LogWarning($"Skill {skillName} not recognized for unit deployment.");
                    break;
            }
        }

        // Apply the skill's effect to a spell
        private void ApplySkillToSpell(Spell spell)
        {
            switch (skillName)
            {
                case SkillName.SpellDurationMultiplier:
                    spell.Duration *= Multiplier;
                    break;
                case SkillName.ShieldMultiplier:
                    // For Spell_01 (Laser), adjust shield damage
                    Spell_01 laserSpell = spell as Spell_01;
                    if (laserSpell != null)
                    {
                        // Store the multiplier in the spell to use when doing damage
                        laserSpell.ShieldDamageMultiplier = Multiplier;
                        
                        // Also increase the base damage for stronger effects
                        laserSpell.damagePerSecond = Mathf.RoundToInt(laserSpell.damagePerSecond * Multiplier);
                        
                        //Debug.Log($"Applied ShieldMultiplier {Multiplier} to Laser Beam spell. DPS now: {laserSpell.damagePerSecond}");
                    }
                    else
                    {
                        // For Spell_02 (Explosion), adjust damage
                        Spell_02 explosionSpell = spell as Spell_02;
                        if (explosionSpell != null)
                        {
                            // Boost explosion damage
                            explosionSpell.damageMultiplier *= Multiplier;
                           // Debug.Log($"Applied ShieldMultiplier {Multiplier} to Explosion spell. Damage multiplier now: {explosionSpell.damageMultiplier}");
                        }
                        else
                        {
                           // Debug.Log($"Applied ShieldMultiplier {Multiplier} to spell {spell.getKey()}");
                        }
                    }
                    break;
                case SkillName.CriticalStrikeChance:
                    // Apply critical strike chance to Spell_01 (Laser)
                    Spell_01 spellWithCrits = spell as Spell_01;
                    if (spellWithCrits != null)
                    {
                        spellWithCrits.criticalStrikeChance += Multiplier;
                       // Debug.Log($"Applied CriticalStrikeChance {Multiplier} to Laser spell {spell.getKey()}");
                    }
                    else 
                    {
                        // Apply critical strike chance to Spell_02 (Explosion)
                        Spell_02 explosionWithCrits = spell as Spell_02;
                        if (explosionWithCrits != null)
                        {
                            explosionWithCrits.criticalStrikeChance += Multiplier;
                         //   Debug.Log($"Applied CriticalStrikeChance {Multiplier} to Explosion spell {spell.getKey()}");
                        }
                    }
                    break;
                case SkillName.RadiusMultiplier:
                    // Apply radius multiplier to Spell_02 (Explosion)
                    Spell_02 explosionRadius = spell as Spell_02;
                    if (explosionRadius != null)
                    {
                        explosionRadius.radiusMultiplier *= Multiplier;
                      //  Debug.Log($"Applied RadiusMultiplier {Multiplier} to Explosion spell. Radius multiplier now: {explosionRadius.radiusMultiplier}");
                    }
                    break;
                default:
                    Debug.LogWarning($"Skill {skillName} not recognized for spell deployment.");
                    break;
            }
        }

        public void ApplyGameplayModifier()
        {
            if (ApplicationType == SkillApplicationType.GameplayModifier)
            {
                switch (skillName)
                {
                    case SkillName.SpeedEnergyMultiplier:
                        GameMng.P.SpeedEnergy *= Multiplier;
                        break;
                    case SkillName.BotEnergyMultiplier:
                        // Find the BotSpawner directly
                        BotSpawner botSpawner = UnityEngine.Object.FindAnyObjectByType<BotSpawner>();
                        if (botSpawner != null)
                        {
                            var bots = botSpawner.GetBots();
                            if (bots != null && bots.Count > 0)
                                bots[0].SpeedEnergy *= Multiplier;
                        }
                        break;
                    default:
                        Debug.LogWarning($"Skill {skillName} not recognized for gameplay modification.");
                        break;
                }
            }
        }
    }
}
