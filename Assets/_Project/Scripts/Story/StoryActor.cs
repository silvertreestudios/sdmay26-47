using System.Collections;
using TacticsGame.Characters;
using UnityEngine;

namespace TacticsGame.Story
{
    public class StoryActor : MonoBehaviour
    {
        [Tooltip("The unique ID used to reference this actor in the JSON script.")]
        public string actorId;

        private UnitVisuals unitVisuals;
        private Animator animator;

        private void Awake()
        {
            unitVisuals = GetComponentInChildren<UnitVisuals>();
            animator = GetComponentInChildren<Animator>();

            if (unitVisuals == null && animator == null)
            {
                Debug.LogWarning(
                    $"[StoryActor] {gameObject.name} (or its children) is missing an Animator or UnitVisuals component. Animations will not play."
                );
            }
        }

        public void PlayAnimation(string triggerName)
        {
            if (animator != null)
            {
                int hash = Animator.StringToHash(triggerName);
                animator.SetTrigger(hash);
            }
        }

        public IEnumerator MoveToCoroutine(Vector3 destination, float speed)
        {
            float rotateSpeed = 10f;

            if (unitVisuals != null)
                unitVisuals.SetSpeed(speed);

            CharacterController cc = GetComponentInChildren<CharacterController>();

            while (Vector3.Distance(transform.position, destination) > 0.1f)
            {
                Vector3 currentPos = transform.position;
                Vector3 direction = (destination - currentPos).normalized;
                direction.y = 0;

                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        1f - Mathf.Exp(-rotateSpeed * Time.deltaTime)
                    );
                }

                Vector3 nextPos = Vector3.MoveTowards(
                    currentPos,
                    destination,
                    speed * Time.deltaTime
                );
                Vector3 moveDelta = nextPos - currentPos;

                if (cc != null && cc.enabled)
                {
                    cc.Move(moveDelta);
                }
                else
                {
                    transform.position = nextPos;
                }

                yield return null;
            }

            transform.position = destination;
            if (unitVisuals != null)
                unitVisuals.SetSpeed(0f);
        }
    }
}
