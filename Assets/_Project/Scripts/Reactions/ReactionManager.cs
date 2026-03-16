using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using UnityEngine;

namespace PathfinderTactics.Reactions
{
    public class ReactionManager : MonoBehaviour
    {
        private Queue<ReactionIntent> pendingIntents = new Queue<ReactionIntent>();
        private Action<GameEvent> onAllReactionsResolved;
        private GameEvent currentResolvingEvent;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ReactionManager>();
        }

        public void EvaluateEvent(GameEvent gameEvent, Action<GameEvent> onComplete)
        {
            pendingIntents.Clear();
            currentResolvingEvent = gameEvent;
            onAllReactionsResolved = onComplete;

            List<ReactionIntent> validIntents = new List<ReactionIntent>();

            // Gather all valid intents
            foreach (Unit unit in UnitManager.AllUnits)
            {
                var health = unit.GetComponent<IDamageable>();
                if (health != null && health.IsDead)
                    continue;

                // Check Availability
                if (!unit.HasReactionAvailable)
                    continue;

                BaseReaction[] reactions = unit.GetComponents<BaseReaction>();
                foreach (var reaction in reactions)
                {
                    if (reaction.CanTrigger(gameEvent))
                    {
                        validIntents.Add(new ReactionIntent(reaction, unit, gameEvent));
                    }
                }
            }

            // Sort by Priority (Descending)
            validIntents.Sort(
                (a, b) => b.Reaction.GetPriority().CompareTo(a.Reaction.GetPriority())
            );

            foreach (var intent in validIntents)
            {
                pendingIntents.Enqueue(intent);
            }

            // Begin processing
            ProcessNextIntent();
        }

        private void ProcessNextIntent()
        {
            if (pendingIntents.Count == 0 || currentResolvingEvent.IsCancelled)
            {
                onAllReactionsResolved?.Invoke(currentResolvingEvent);
                return;
            }

            ReactionIntent intent = pendingIntents.Dequeue();

            // Safety Check: Did a previous reaction in this chain kill this unit or steal its reaction?
            if (
                !intent.ReactingUnit.HasReactionAvailable
                || intent.ReactingUnit.GetComponent<IDamageable>().IsDead
            )
            {
                ProcessNextIntent(); // Skip and move on
                return;
            }

            // Handle Decision Modes
            switch (intent.Reaction.CurrentMode)
            {
                case ReactionMode.Auto:
                    ExecuteIntent(intent);
                    break;

                case ReactionMode.Prompt:
                    // TODO: UI Prompt
                    Debug.Log(
                        $"[REACTION] Prompting {intent.ReactingUnit.name} to use {intent.Reaction.GetReactionName()}..."
                    );
                    ExecuteIntent(intent);
                    break;

                case ReactionMode.Conditional:
                    // Let the reaction decide its own rules
                    if (intent.Reaction.ShouldAutoTrigger(intent.TriggeringEvent))
                    {
                        ExecuteIntent(intent);
                    }
                    else
                    {
                        Debug.Log(
                            $"[REACTION] {intent.ReactingUnit.name}'s {intent.Reaction.GetReactionName()} condition not met. Skipping."
                        );
                        ProcessNextIntent();
                    }
                    break;
            }
        }

        private void ExecuteIntent(ReactionIntent intent)
        {
            Debug.Log(
                $"[REACTION] {intent.ReactingUnit.name} executes {intent.Reaction.GetReactionName()}!"
            );

            // Spend the Reaction
            intent.ReactingUnit.SpendReaction();

            intent.Reaction.Execute(intent, ProcessNextIntent);
        }
    }
}
