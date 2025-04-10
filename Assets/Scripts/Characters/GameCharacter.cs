using UnityEngine;

namespace CosmicraftsSP
{
    public class GameCharacter : MonoBehaviour
    {
        public CharacterBaseSO characterBaseSO;

        // Initialize the character using the SO
        public void InitializeCharacter(CharacterBaseSO characterSO)
        {
            characterBaseSO = characterSO;
            ApplyGameplayModifiers();
        }

        // ✅ Fix: Mark this method as virtual to allow overriding in child classes
        public virtual void DeployUnit(Unit unit)
        {
            if (characterBaseSO != null)
            {
                foreach (var skill in characterBaseSO.Skills)
                {
                    if (skill.ApplicationType == SkillApplicationType.OnDeployUnit)
                    {
                        skill.ApplySkill(unit);
                    }
                }
            }
        }

        // Optional: Mark this as virtual if you want to allow overrides for spells
        public virtual void DeploySpell(Spell spell)
        {
            if (characterBaseSO != null)
            {
                foreach (var skill in characterBaseSO.Skills)
                {
                    if (skill.ApplicationType == SkillApplicationType.OnDeployUnit)
                    {
                        skill.ApplySkill(spell);
                    }
                }
            }
        }

        public void ApplyGameplayModifiers()
        {
            if (characterBaseSO != null)
            {
                foreach (var skill in characterBaseSO.Skills)
                {
                    if (skill.ApplicationType == SkillApplicationType.GameplayModifier)
                    {
                        skill.ApplyGameplayModifier();
                    }
                }
            }
        }
    }
}
