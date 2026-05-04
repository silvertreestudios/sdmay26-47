using System;
using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.UI;
using UnityEngine;

namespace PathfinderTactics.Reactions
{
    public class ReactionManager : MonoBehaviour
    {
        private class EvaluationContext
        {
            public Queue<ReactionIntent> PendingIntents = new Queue<ReactionIntent>();
            public Action<GameEvent> OnAllReactionsResolved;
            public GameEvent CurrentResolvingEvent;
        }

        private Stack<EvaluationContext> evaluationStack = new Stack<EvaluationContext>();

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
            EvaluationContext context = new EvaluationContext
            {
                CurrentResolvingEvent = gameEvent,
                OnAllReactionsResolved = onComplete,
            };
            evaluationStack.Push(context);

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
                context.PendingIntents.Enqueue(intent);
            }

            // Begin processing
            ProcessNextIntent();
        }

        private void ProcessNextIntent()
        {
            if (evaluationStack.Count == 0)
                return;

            EvaluationContext context = evaluationStack.Peek();

            if (context.PendingIntents.Count == 0 || context.CurrentResolvingEvent.IsCancelled)
            {
                // We finished evaluating this specific event level
                evaluationStack.Pop();
                context.OnAllReactionsResolved?.Invoke(context.CurrentResolvingEvent);
                return;
            }

            ReactionIntent intent = context.PendingIntents.Dequeue();

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
                    if (intent.ReactingUnit.GetFaction() == Faction.Player)
                    {
                        if (ServiceLocator.TryGet<ReactionPromptUI>(out var promptUI))
                        {
                            promptUI.Show(
                                intent.ReactingUnit.name,
                                intent.Reaction.GetReactionName(),
                                intent.TriggeringEvent.SourceUnit?.name ?? "something",
                                (confirmed) =>
                                {
                                    if (confirmed)
                                    {
                                        ExecuteIntent(intent);
                                    }
                                    else
                                    {
                                        Debug.Log(
                                            $"[REACTION] {intent.ReactingUnit.name} declined to use {intent.Reaction.GetReactionName()}."
                                        );
                                        ProcessNextIntent();
                                    }
                                }
                            );
                        }
                        else
                        {
                            Debug.LogWarning(
                                "[REACTION] Prompt mode requested but ReactionPromptUI is missing! Defaulting to Auto."
                            );
                            ExecuteIntent(intent);
                        }
                    }
                    else
                    {
                        // AI units always "Auto" if the intent is valid
                        ExecuteIntent(intent);
                    }
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
