namespace CosmicraftsSP
{
    using UnityEngine;

    public class UnitAnimLis : MonoBehaviour
    {
        // Unit data reference
        Unit MyUnit;
        Animator animator;

        void Start()
        {
            // Get unit data and animator
            MyUnit = transform.parent.GetComponent<Unit>();
            animator = MyUnit.GetAnimator();

            // Set the attack animation speed
            AnimationClip attack_clip = MyUnit.GetAnimationClip("Attack");
            Shooter shooter = transform.parent.GetComponent<Shooter>();
            if (attack_clip != null && shooter != null)
            {
                float attackSpeed = attack_clip.length / shooter.CoolDown * 2;
                animator.SetFloat("AttackSpeed", attackSpeed);
            }
        }

        // Called when the death animation ends
        public void AE_EndDeath()
        {
            // Kill the unit
            MyUnit.DestroyUnit();
        }

        // Called for explosion effect
        public void AE_BlowUpUnit()
        {
            // Trigger explosion effects
            MyUnit.BlowUpEffect();
        }

        // Call this method to trigger the attack animation
        public void TriggerAttack()
        {
            animator.SetBool("IsAttacking", true);
        }

        // Call this method to stop the attack animation
        public void StopAttack()
        {
            animator.SetBool("IsAttacking", false);
        }

        // Call this method to trigger the death animation
        public void TriggerDeath()
        {
            animator.SetBool("IsDead", true);
        }
    }
}