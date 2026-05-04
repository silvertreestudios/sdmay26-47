using System.Collections;
using TacticsGame.Characters;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

            // Ensure masks are refreshed for story actors
            var faceMask = GetComponentInChildren<FaceMaskComponent>();
            if (faceMask != null)
                faceMask.ForceRefresh();
        }

        private PlayableGraph currentGraph;

        private void OnDestroy()
        {
            if (currentGraph.IsValid())
                currentGraph.Destroy();
        }

        public void PlayAnimation(string triggerName)
        {
            // Clear any active story clip when a standard trigger is fired
            StopStoryClip();

            if (animator != null)
            {
                // Special case for death since it requires setting multiple bools
                if (triggerName.Equals("Death", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (unitVisuals != null)
                    {
                        unitVisuals.SetDead(true);
                        return;
                    }
                }

                int hash = Animator.StringToHash(triggerName);
                animator.SetTrigger(hash);
            }
        }

        public void StopStoryClip()
        {
            if (currentGraph.IsValid())
            {
                currentGraph.Destroy();
            }
        }

        public IEnumerator PlayClipCoroutine(AnimationClip clip)
        {
            if (animator == null || clip == null)
                yield break;

            StopStoryClip();

            // Use Playables to play a single clip
            currentGraph = PlayableGraph.Create($"{gameObject.name}_StoryClip");
            var settings = AnimationPlayableUtilities.PlayClip(animator, clip, out currentGraph);

            // Set loop if it's an idle animation
            if (clip.isLooping || clip.name.Contains("Idle"))
            {
                // PlayableGraph will stay active until StopStoryClip is called
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                // Wait for clip duration and clean up
                yield return new WaitForSeconds(clip.length);
                StopStoryClip();
            }
        }

        public IEnumerator MoveToCoroutine(Vector3 destination, float speed)
        {
            StopStoryClip();
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
