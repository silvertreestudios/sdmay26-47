using System.Collections.Generic;
using PathfinderTactics.Characters;
using PathfinderTactics.Core;
using PathfinderTactics.Grid;
using UnityEngine;

namespace PathfinderTactics.Combat
{
    /// <summary>
    /// Central, per-observer stealth resolver.
    /// All stealth logic (Hide/Sneak/Seek + passive degrade) must route here.
    /// </summary>
    public static class StealthResolver
    {
        // Enable stealth tracing in the Unity console.
        private const bool STEALTH_DEBUG = true;

        private static readonly HashSet<Unit> passiveEvaluationSuppressed = new HashSet<Unit>();

        private static void LogStealth(string message)
        {
            if (!STEALTH_DEBUG)
                return;
            Debug.Log($"<color=purple>[STEALTH]</color> {message}");
        }

        public static void SetPassiveEvaluationSuppressed(Unit actor, bool suppressed)
        {
            if (actor == null)
                return;

            if (suppressed)
                passiveEvaluationSuppressed.Add(actor);
            else
                passiveEvaluationSuppressed.Remove(actor);
        }

        public static void TransitionDetectionState(
            Unit actor,
            Unit observer,
            DetectionState targetState
        )
        {
            if (actor == null || observer == null)
                return;

            UnitStealth actorStealth = actor.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return;

            DetectionState current = actorStealth.GetDetectionState(observer);
            if (current == targetState)
                return; // Early return if no change

            LogStealth($"{actor.name} -> {observer.name}: {current} => {targetState}");
            actorStealth.SetDetectionState(observer, targetState);
        }

        // Hooks
        public static bool RequiresHiddenFlatCheck(Unit attacker, Unit target)
        {
            if (target == null)
                return false;
            UnitStealth targetStealth = target.GetComponent<UnitStealth>();
            return targetStealth != null && targetStealth.RequiresHiddenFlatCheck(attacker);
        }

        public static bool RequiresConcealedFlatCheck(Unit attacker, Unit target)
        {
            if (target == null)
                return false;
            UnitStealth targetStealth = target.GetComponent<UnitStealth>();
            return (targetStealth != null && targetStealth.RequiresConcealedFlatCheck(attacker));
        }

        public static bool CanTargetSquare(Unit attacker, Vector3 worldPosition)
        {
            if (attacker == null)
                return false;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            GridPosition pos = grid.GetGridPosition(worldPosition);
            Unit target = grid.GetUnitAt(pos);
            if (target == null)
                return false;

            UnitStealth targetStealth = target.GetComponent<UnitStealth>();
            // Undetected still allows targeting (via guess-tile mode),
            // but Unnoticed should remain un-targetable.
            return (
                targetStealth == null
                || targetStealth.GetDetectionState(attacker) != DetectionState.Unnoticed
            );
        }

        // Hide
        public static void ResolveHide(Unit actor)
        {
            if (actor == null)
                return;

            UnitStealth actorStealth = actor.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(actor.GetFaction());

            int stealthD20 = RollD20();
            int stealthMod = actorStealth.GetStealthModifier();

            LogStealth(
                $"ResolveHide actor={actor.name} d20={stealthD20} stealthMod={stealthMod} observers={observers.Count}"
            );
            foreach (Unit observer in observers)
            {
                if (actorStealth.GetDetectionState(observer) != DetectionState.Observed)
                    continue;

                if (!HasCoverOrConcealmentAt(actor.CurrentGridPosition, observer))
                    continue;

                int coverBonus = GetCoverBonusAt(actor.CurrentGridPosition, observer);
                int perceptionDC = 10 + GetPerceptionModifier(observer);

                LogStealth(
                    $"  [Hide] observer={observer.name} coverBonus={coverBonus} perceptionDC={perceptionDC} current={actorStealth.GetDetectionState(observer)}"
                );
                Degree result = PF2E_Core.CheckResult(
                    stealthD20,
                    stealthMod + coverBonus,
                    perceptionDC
                );

                LogStealth(
                    $"  [Hide] result={result} total={stealthD20 + stealthMod + coverBonus}"
                );
                if (result == Degree.Success || result == Degree.CriticalSuccess)
                {
                    TransitionDetectionState(actor, observer, DetectionState.Hidden);
                }
            }
        }

        // Sneak
        public static void ResolveSneak(
            Unit actor,
            GridPosition startPosition,
            GridPosition endPosition,
            List<GridPosition> path,
            bool actorMakesNoise
        )
        {
            if (actor == null)
                return;

            UnitStealth actorStealth = actor.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(actor.GetFaction());

            int stealthD20 = RollD20(); // single-roll per action
            int stealthMod = actorStealth.GetStealthModifier();

            LogStealth(
                $"ResolveSneak actor={actor.name} d20={stealthD20} stealthMod={stealthMod} start={startPosition} end={endPosition} makesNoise={actorMakesNoise}"
            );

            // Capture start state so no observer can implicitly affect others.
            Dictionary<Unit, DetectionState> startStates = new Dictionary<Unit, DetectionState>();
            foreach (Unit observer in observers)
            {
                startStates[observer] = actorStealth.GetDetectionState(observer);
            }

            foreach (Unit observer in observers)
            {
                DetectionState startState = startStates[observer];
                LogStealth($"  [Sneak] observer={observer.name} startState={startState}");

                // If they already observed you, nothing changes.
                if (startState != DetectionState.Hidden && startState != DetectionState.Undetected)
                    continue;

                // End position must have cover/concealment.
                if (!HasCoverOrConcealmentAt(endPosition, observer))
                {
                    LogStealth(
                        $"    [Sneak] end lacks cover/concealment vs {observer.name} (startState={startState})"
                    );
                    // If you lose cover/concealment during Sneak, you can become Observed,
                    // but only depending on the starting detection state and whether the
                    // observer can (precisely) sense you.
                    if (startState == DetectionState.Hidden)
                    {
                        if (CanSee(observer, actor))
                        {
                            LogStealth($"      [Sneak] end no cover: Hidden -> Observed (CanSee)");
                            TransitionDetectionState(actor, observer, DetectionState.Observed);
                        }
                    }
                    else if (startState == DetectionState.Undetected)
                    {
                        if (CanPreciselySense(observer, actor))
                        {
                            LogStealth(
                                $"      [Sneak] end no cover: Undetected -> Observed (CanPreciselySense)"
                            );
                            TransitionDetectionState(actor, observer, DetectionState.Observed);
                        }
                    }

                    continue;
                }

                // Path cover affects bonus only.
                bool hadCoverThroughout = EvaluatePathCover(path, endPosition, observer);
                int coverBonus = hadCoverThroughout ? GetCoverBonusAt(endPosition, observer) : 0;

                LogStealth(
                    $"    [Sneak] hadCoverThroughout={hadCoverThroughout} coverBonus={coverBonus}"
                );
                int perceptionDC = 10 + GetPerceptionModifier(observer);
                Degree result = PF2E_Core.CheckResult(
                    stealthD20,
                    stealthMod + coverBonus,
                    perceptionDC
                );
                LogStealth(
                    $"    [Sneak] perceptionDC={perceptionDC} total={stealthD20 + stealthMod + coverBonus} result={result}"
                );

                // Unobservable clause.
                if (!CanPreciselySense(observer, actor) && result == Degree.CriticalFailure)
                {
                    LogStealth(
                        $"    [Sneak] unobservable clause: CriticalFailure -> Failure for {observer.name}"
                    );
                    result = Degree.Failure;
                }

                // Resolve result.
                switch (result)
                {
                    case Degree.CriticalSuccess:
                    case Degree.Success:
                        LogStealth(
                            $"    [Sneak] resolved => Hidden/Undetected -> Undetected for {observer.name}"
                        );
                        TransitionDetectionState(actor, observer, DetectionState.Undetected);
                        break;

                    case Degree.Failure:
                        // Undetected -> Hidden
                        // Hidden -> stays Hidden
                        if (startState == DetectionState.Undetected)
                        {
                            LogStealth(
                                $"    [Sneak] resolved => Undetected -> Hidden for {observer.name}"
                            );
                            TransitionDetectionState(actor, observer, DetectionState.Hidden);
                        }
                        break;

                    case Degree.CriticalFailure:
                        LogStealth(
                            $"    [Sneak] resolved => -> Observed (CriticalFailure) for {observer.name}"
                        );
                        TransitionDetectionState(actor, observer, DetectionState.Observed);
                        break;
                }

                // Noise rule.
                if (
                    actorMakesNoise
                    && actorStealth.GetDetectionState(observer) == DetectionState.Undetected
                )
                {
                    LogStealth(
                        $"    [Sneak] noise rule => Undetected -> Hidden for {observer.name}"
                    );
                    TransitionDetectionState(actor, observer, DetectionState.Hidden);
                }
            }
        }

        // Seek
        public static void ResolveSeek(Unit seeker, List<Unit> targetsInArea)
        {
            if (seeker == null || targetsInArea == null)
                return;

            UnitStealth seekerStealth = seeker.GetComponent<UnitStealth>();
            if (seekerStealth == null)
                return;

            int perceptionD20 = RollD20();
            int perceptionMod = GetPerceptionModifier(seeker);

            LogStealth(
                $"ResolveSeek seeker={seeker.name} d20={perceptionD20} perceptionMod={perceptionMod} targets={targetsInArea.Count}"
            );

            foreach (Unit target in targetsInArea)
            {
                if (target == null || target == seeker)
                    continue;

                UnitStealth targetStealth = target.GetComponent<UnitStealth>();
                if (targetStealth == null)
                    continue;

                DetectionState state = targetStealth.GetDetectionState(seeker);
                if (state != DetectionState.Hidden && state != DetectionState.Undetected)
                    continue;

                int stealthDC = 10 + targetStealth.GetStealthModifier();
                LogStealth($"  [Seek] target={target.name} state={state} stealthDC={stealthDC}");
                Degree result = PF2E_Core.CheckResult(perceptionD20, perceptionMod, stealthDC);

                bool usingImpreciseSense = !CanPreciselySense(seeker, target);
                LogStealth($"    [Seek] usingImpreciseSense={usingImpreciseSense} result={result}");

                // Imprecise senses cap at Hidden regardless of degree.
                // You can narrow location but never precisely observe with imprecise senses.
                if (usingImpreciseSense)
                {
                    if (result == Degree.Success || result == Degree.CriticalSuccess)
                    {
                        if (state == DetectionState.Undetected)
                        {
                            LogStealth(
                                $"    [Seek] imprecise: Undetected -> Hidden for {target.name} (result={result})"
                            );
                            TransitionDetectionState(target, seeker, DetectionState.Hidden);
                        }
                        // Hidden stays Hidden, imprecise can never reach Observed.
                    }
                    continue;
                }

                // Precise senses.
                switch (result)
                {
                    case Degree.CriticalSuccess:
                        LogStealth($"    [Seek] precise: -> Observed ({target.name})");
                        TransitionDetectionState(target, seeker, DetectionState.Observed);
                        break;

                    case Degree.Success:
                        if (state == DetectionState.Undetected)
                        {
                            LogStealth($"    [Seek] precise: Undetected -> Hidden ({target.name})");
                            TransitionDetectionState(target, seeker, DetectionState.Hidden);
                        }
                        else
                        {
                            LogStealth($"    [Seek] precise: -> Observed ({target.name})");
                            TransitionDetectionState(target, seeker, DetectionState.Observed);
                        }
                        break;
                }
            }
        }

        // Passive state changes
        public static void EvaluatePassiveStateChanges(Unit actor)
        {
            if (actor == null)
                return;
            if (passiveEvaluationSuppressed.Contains(actor))
                return;

            UnitStealth actorStealth = actor.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(actor.GetFaction());

            LogStealth(
                $"EvaluatePassiveStateChanges actor={actor.name} observers={observers.Count} suppressed={passiveEvaluationSuppressed.Contains(actor)}"
            );
            foreach (Unit observer in observers)
            {
                DetectionState state = actorStealth.GetDetectionState(observer);
                if (state != DetectionState.Hidden && state != DetectionState.Undetected)
                    continue;

                bool canPrecise = CanPreciselySense(observer, actor);
                bool hasCover = HasCoverOrConcealmentAt(actor.CurrentGridPosition, observer);
                LogStealth(
                    $"  [Passive] observer={observer.name} state={state} canPrecise={canPrecise} hasCoverOrConcealment={hasCover}"
                );
                // If you lose cover/concealment and the observer can precisely sense you,
                // you become observed.
                if (canPrecise && !hasCover)
                {
                    LogStealth(
                        $"    [Passive] {actor.name} -> {observer.name}: Hidden/Undetected -> Observed"
                    );
                    TransitionDetectionState(actor, observer, DetectionState.Observed);
                }
            }
        }

        public static void OnNoiseGenerated(Unit actor)
        {
            if (actor == null)
                return;

            UnitStealth actorStealth = actor.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return;

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(actor.GetFaction());

            LogStealth($"OnNoiseGenerated actor={actor.name} observers={observers.Count}");
            foreach (Unit observer in observers)
            {
                if (actorStealth.GetDetectionState(observer) == DetectionState.Undetected)
                {
                    LogStealth($"  [Noise] {actor.name} Undetected -> Hidden for {observer.name}");
                    TransitionDetectionState(actor, observer, DetectionState.Hidden);
                }
            }
        }

        public static void BreakStealthAfterAttack(Unit actor)
        {
            if (actor == null)
                return;

            UnitStealth actorStealth = actor.GetComponent<UnitStealth>();
            if (actorStealth == null)
                return;

            // Invisible actors cannot become Observed even after attacking.
            UnitConditions actorConditions = actor.GetComponent<UnitConditions>();
            bool actorIsInvisible =
                actorConditions != null && actorConditions.HasCondition(ConditionType.Invisible);

            GridSystem grid = ServiceLocator.Get<GridSystem>();
            List<Unit> observers = grid.GetAllEnemies(actor.GetFaction());

            LogStealth(
                $"BreakStealthAfterAttack actor={actor.name} observers={observers.Count} invisible={actorIsInvisible}"
            );
            foreach (Unit observer in observers)
            {
                DetectionState current = actorStealth.GetDetectionState(observer);

                if (current == DetectionState.Hidden)
                {
                    // Striking reveals your exact location, always Observed unless Invisible.
                    // Rule: "you then become observed" (unconditional in Hide/Sneak text).
                    if (!actorIsInvisible)
                    {
                        LogStealth(
                            $"  [AttackBreak] {actor.name} -> {observer.name}: Hidden -> Observed"
                        );
                        TransitionDetectionState(actor, observer, DetectionState.Observed);
                    }
                }
                else if (current == DetectionState.Undetected)
                {
                    // Undetected: noise already moved them to Hidden via OnNoiseGenerated.
                    // If the observer can also precisely sense the attacker, go all the way to Observed.
                    if (CanPreciselySense(observer, actor))
                    {
                        LogStealth(
                            $"  [AttackBreak] {actor.name} -> {observer.name}: Undetected -> Observed (precise)"
                        );
                        TransitionDetectionState(actor, observer, DetectionState.Observed);
                    }
                }
            }
        }

        // Helpers
        private static int RollD20()
        {
            return Random.Range(1, 21);
        }

        private static int GetPerceptionModifier(Unit observer)
        {
            UnitStatsSO stats = observer != null ? observer.GetStats() : null;
            return stats != null ? stats.perception : 0;
        }

        private static bool CanPreciselySense(Unit observer, Unit actor)
        {
            if (observer == null || actor == null)
                return false;

            UnitConditions observerConditions = observer.GetComponent<UnitConditions>();
            if (
                observerConditions != null
                && observerConditions.HasCondition(ConditionType.Blinded)
            )
                return false;

            UnitConditions actorConditions = actor.GetComponent<UnitConditions>();
            if (actorConditions != null && actorConditions.HasCondition(ConditionType.Invisible))
                return false;

            // Precise sensing is blocked by Total Cover / No Line of Effect.
            // Standard/Lesser cover should not prevent the observer from seeing you,
            // it just makes targeting harder.
            int cover = LineOfSightUtility.GetCoverBonus(
                observer.CurrentGridPosition,
                actor.CurrentGridPosition
            );
            return cover != -1;
        }

        public static bool HasCoverOrConcealmentAt(GridPosition actorPos, Unit observer)
        {
            if (observer == null)
                return false;

            int cover = LineOfSightUtility.GetCoverBonus(observer.CurrentGridPosition, actorPos);

            // Treat solid block as concealment.
            // PF2e: Lesser Cover (+1) does NOT qualify for Hide/Sneak.
            return cover == -1 || cover >= 2;
        }

        private static bool CanSee(Unit observer, Unit actor)
        {
            // "can see" and "can precisely sense" are the samae for now.
            // Blinded, Invisible, and No Line of Effect.
            return CanPreciselySense(observer, actor);
        }

        private static int GetCoverBonusAt(GridPosition actorPos, Unit observer)
        {
            int cover = LineOfSightUtility.GetCoverBonus(observer.CurrentGridPosition, actorPos);

            // Only standard or greater cover contributes to stealth DCs.
            // Lesser cover (+1): no bonus.
            // Total cover (wall, -1): at least Greater Cover -> +4 bonus.
            if (cover == -1)
                return 4; // wall-blocked -> Greater Cover bonus
            if (cover >= 2)
                return cover; // standard (+2) or greater (+4) directly

            return 0;
        }

        private static bool EvaluatePathCover(
            List<GridPosition> path,
            GridPosition endPosition,
            Unit observer
        )
        {
            if (path == null || path.Count == 0)
                return false;

            // Must have standard-or-greater cover throughout the whole stride.
            foreach (GridPosition pos in path)
            {
                int coverBonus = GetCoverBonusAt(pos, observer);
                if (coverBonus <= 0)
                    return false;
            }

            return true;
        }
    }
}
