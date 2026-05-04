using System;
using UnityEngine;

namespace TacticsGame.Characters
{
    /// <summary>
    /// Acts as an Event Bridge between the Animator and the C# logic.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class UnitVisuals : MonoBehaviour
    {
        private Animator animator;

        // Floats
        public static readonly int AnimSpeed = Animator.StringToHash("Speed");
        public static readonly int AnimVerticalSpeed = Animator.StringToHash("VerticalSpeed");

        // Bools
        public static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int AnimIsBlocking = Animator.StringToHash("IsBlocking");
        public static readonly int AnimIsProne = Animator.StringToHash("IsProne");
        public static readonly int AnimIsSneaking = Animator.StringToHash("IsSneaking");
        public static readonly int AnimIsDead = Animator.StringToHash("IsDead");
        public static readonly int AnimIsCovering = Animator.StringToHash("IsCovering");
        public static readonly int AnimIsUnconscious = Animator.StringToHash("IsUnconscious");

        // Ints
        public static readonly int AnimWeaponType = Animator.StringToHash("WeaponType");
        public static readonly int AnimConditionID = Animator.StringToHash("ConditionID");
        public static readonly int AnimInteractType = Animator.StringToHash("InteractType");

        // State Names (Common Fallbacks)
        public static readonly int AnimLocomotion = Animator.StringToHash("Locomotion");
        public static readonly int AnimIdle = Animator.StringToHash("Idle");
        public static readonly int AnimDefaultEmpty = Animator.StringToHash("Default Empty State");

        // Triggers
        public static readonly int AnimJump = Animator.StringToHash("Jump");
        public static readonly int AnimAttackMelee = Animator.StringToHash("Attack_Melee");
        public static readonly int AnimAttackRanged = Animator.StringToHash("Attack_Ranged");
        public static readonly int AnimTakeDamage = Animator.StringToHash("TakeDamage");
        public static readonly int AnimDodge = Animator.StringToHash("Dodge");
        public static readonly int AnimCastSpell = Animator.StringToHash("CastSpell");
        public static readonly int AnimInteract = Animator.StringToHash("Interact");
        public static readonly int AnimStep = Animator.StringToHash("Step"); // Probably not gonna be used lol
        public static readonly int AnimPivot = Animator.StringToHash("Pivot");

        // C# EVENTS (For Action Scripts to subscribe to)

        /// <summary> Fired when the sword physically swings through the target. </summary>
        public event Action OnStrikeConnects;

        /// <summary> Fired when the bowstring releases. </summary>
        public event Action OnShoot;

        /// <summary> Fired when the spellcast climax occurs (e.g. throwing fireball). </summary>
        public event Action OnCastSpell;

        /// <summary> Fired when an action animation completely finishes resolving. </summary>
        public event Action OnAnimationEnd;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        // PARAMETER SETTERS

        public void SetSpeed(float speed) => animator.SetFloat(AnimSpeed, speed);

        public void SetVerticalSpeed(float vSpeed) => animator.SetFloat(AnimVerticalSpeed, vSpeed);

        public void SetGrounded(bool grounded) => animator.SetBool(AnimIsGrounded, grounded);

        public void SetBlocking(bool isBlocking) => animator.SetBool(AnimIsBlocking, isBlocking);

        public void SetProne(bool isProne) => animator.SetBool(AnimIsProne, isProne);

        public void SetSneaking(bool isSneaking) => animator.SetBool(AnimIsSneaking, isSneaking);

        public void SetDead(bool isDead)
        {
            if (isDead)
                PlayFallenSequence();
            else
                animator.SetBool(AnimIsDead, false);
        }

        public void PlayFallenSequence()
        {
            StartCoroutine(FallenCoroutine());
        }

        private System.Collections.IEnumerator FallenCoroutine()
        {
            animator.SetBool(AnimIsDead, true);
            animator.SetBool(AnimIsUnconscious, true);
            animator.SetBool(AnimIsProne, false);

            yield return new WaitForSeconds(1.5f);

            animator.SetBool(AnimIsProne, true);
        }

        public void SetCovering(bool isCover) => animator.SetBool(AnimIsCovering, isCover);

        public void SetUnconscious(bool isUn)
        {
            if (isUn)
                PlayFallenSequence();
            else
            {
                animator.SetBool(AnimIsUnconscious, false);
                animator.SetBool(AnimIsProne, false);
                animator.SetBool(AnimIsDead, false);
            }
        }

        public void SetWeaponType(int itemType) => animator.SetInteger(AnimWeaponType, itemType);

        public void SetConditionID(int conditionID) =>
            animator.SetInteger(AnimConditionID, conditionID);

        public void SetInteractType(int interactType) =>
            animator.SetInteger(AnimInteractType, interactType);

        // TRIGGERS

        public void TriggerJump() => animator.SetTrigger(AnimJump);

        public void TriggerMeleeAttack() => animator.SetTrigger(AnimAttackMelee);

        public void TriggerRangedAttack() => animator.SetTrigger(AnimAttackRanged);

        public void TriggerTakeDamage() => animator.SetTrigger(AnimTakeDamage);

        public void TriggerDodge() => animator.SetTrigger(AnimDodge);

        public void TriggerCastSpellAction() => animator.SetTrigger(AnimCastSpell);

        public void TriggerInteract() => animator.SetTrigger(AnimInteract);

        public void TriggerStep() => animator.SetTrigger(AnimStep);

        public void TriggerPivot() => animator.SetTrigger(AnimPivot);

        // ANIMATOR EVENT RECEIVERS (Timeline hooks)
        // These MUST be manually placed on to the animation clip timelines!

        public void AnimEvent_StrikeConnects()
        {
            Debug.Log(
                $"<color=magenta>[ANIM_EVENT]</color> {gameObject.name} Strike Connects Fired!"
            );
            OnStrikeConnects?.Invoke();
        }

        public void AnimEvent_Shoot()
        {
            OnShoot?.Invoke();
        }

        public void AnimEvent_CastSpell()
        {
            OnCastSpell?.Invoke();
        }

        public void AnimEvent_ActionComplete()
        {
            Debug.Log(
                $"<color=magenta>[ANIM_EVENT]</color> {gameObject.name} Action Complete Fired!"
            );
            OnAnimationEnd?.Invoke();
        }

        // UTILITY FOR FALLBACKS

        /// <summary>
        /// If you haven't assigned an animation event yet,
        /// call this to manually trigger the event after a short delay so combat doesn't softlock.
        /// </summary>
        public void FallbackInvokeStrike()
        {
            OnStrikeConnects?.Invoke();
        }

        public void FallbackInvokeActionComplete()
        {
            OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Snaps animator out of jump/fall states and into the base locomotion.
        /// </summary>
        public void ForceLandingAnimation()
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return;

            // Clear any lingering jump triggers
            animator.ResetTrigger(AnimJump);
            animator.SetBool(AnimIsGrounded, true);
            animator.SetFloat(AnimVerticalSpeed, 0f);

            // Back to base states
            for (int i = 0; i < animator.layerCount; i++)
            {
                if (i == 0)
                {
                    if (animator.HasState(i, AnimLocomotion))
                        animator.CrossFadeInFixedTime(AnimLocomotion, 0.15f, i);
                    else if (animator.HasState(i, AnimIdle))
                        animator.CrossFadeInFixedTime(AnimIdle, 0.15f, i);
                }
                else
                {
                    // Reset override layers to their empty states
                    if (animator.HasState(i, AnimDefaultEmpty))
                        animator.CrossFadeInFixedTime(AnimDefaultEmpty, 0.15f, i);
                }
            }
        }

        public Transform GetHandTransform(bool rightHand = true)
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            return animator.GetBoneTransform(
                rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand
            );
        }
    }
}
