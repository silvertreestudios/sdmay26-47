using System.Collections.Generic;
using PathfinderTactics.Actions;
using PathfinderTactics.Characters;
using PathfinderTactics.Combat;
using PathfinderTactics.Grid;
using PathfinderTactics.InputSystem;
using UnityEngine;

namespace PathfinderTactics.Core
{
    /// <summary>
    /// Handles unit-based targeting for melee/ranged strikes and single-target abilities.
    /// Builds a list of valid targets, lets the player cycle between units, and
    /// highlights the currently selected unit.
    /// </summary>
    public class TargetLockService : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField]
        private GameObject targetIndicatorPrefab;

        [Tooltip("Minimum time between target changes when holding a direction.")]
        [SerializeField]
        private float targetCycleCooldown = 0.4f;

        [Header("Debug")]
        [Tooltip(
            "Logs target acquisition (reject reasons) and enables LineOfSightUtility.DebugLineOfEffect "
                + "during InitializeTargeting."
        )]
        [SerializeField]
        private bool debugTargeting;

        private List<Unit> validTargets = new List<Unit>();
        private readonly List<Unit> pendingOffLayerStrikeTargets = new List<Unit>();
        private bool strikeOffLayerUnlocked;
        private int currentIndex;
        private float inputCooldown;
        private GameObject activeIndicator;
        private bool isActive;
        private Unit currentAttacker;

        public Unit CurrentTarget =>
            isActive && validTargets.Count > 0 ? validTargets[currentIndex] : null;

        public GridPosition CurrentCursorGridPosition =>
            CurrentTarget != null ? CurrentTarget.CurrentGridPosition : default;

        public Vector3Int CurrentTargetLayeredPosition =>
            CurrentTarget != null ? CurrentTarget.CurrentLayeredPosition : default;

        public bool IsActive => isActive;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TargetLockService>();
            CleanupIndicator();
        }

        /// <summary>
        /// Build valid strike targets with 3D Line of Effect, Line of Sight, and stealth.
        /// Same-Y enemies are shown first; other elevation tiers require an explicit
        /// vertical input (Up/Down) to merge into the cycle list.
        /// </summary>
        public void InitializeTargeting(Unit attacker, BaseAction action)
        {
            validTargets.Clear();
            pendingOffLayerStrikeTargets.Clear();
            strikeOffLayerUnlocked = false;
            currentIndex = 0;
            isActive = false;
            currentAttacker = attacker;

            Debug.Log(
                $"[TargetLockService] InitializeTargeting: attacker={attacker.name}, action={action.GetActionName()}"
            );

            if (attacker == null || action == null)
                return;

            bool prevLoEDebug = LineOfSightUtility.DebugLineOfEffect;
            if (debugTargeting)
                LineOfSightUtility.DebugLineOfEffect = true;

            try
            {
                GridSystem grid = ServiceLocator.Get<GridSystem>();
                Vector3Int attackerPos = attacker.CurrentLayeredPosition;

                if (debugTargeting)
                {
                    Debug.Log(
                        $"[TargetLock] Init attacker={attacker.name} pos={attackerPos} "
                            + $"action={action.GetActionName()}"
                    );
                }

                List<Vector3Int> validPositions = action.GetValidActionGridPositions();

                if (debugTargeting)
                    Debug.Log($"[TargetLock] Valid positions from action: {validPositions.Count}");

                HashSet<Unit> added = new HashSet<Unit>();
                List<Unit> allCandidates = new List<Unit>();

                // O(N) candidate collection from all active units.
                Debug.Log(
                    $"[TargetLock-Detail] UnitManager.AllUnits count: {UnitManager.AllUnits.Count}"
                );

                foreach (Unit target in UnitManager.AllUnits)
                {
                    if (target == null)
                    {
                        Debug.Log("[TargetLock-Detail] Skip: target is NULL (destroyed?).");
                        continue;
                    }
                    if (target == attacker)
                    {
                        Debug.Log($"[TargetLock-Detail] Skip: {target.name} (self).");
                        continue;
                    }

                    Vector3Int targetPos = target.CurrentLayeredPosition;

                    if (target.GetFaction() == attacker.GetFaction())
                    {
                        Debug.Log($"[TargetLock-Detail] Skip: {target.name} (same faction).");
                        continue;
                    }

                    // Check if the action itself allows this position (Range)
                    if (!validPositions.Contains(targetPos))
                    {
                        Debug.Log(
                            $"[TargetLock-Detail] Skip: {target.name} @ {targetPos} (OUT OF RANGE - action list)."
                        );
                        continue;
                    }

                    if (!LineOfSightUtility.HasLineOfEffect(attackerPos, targetPos))
                    {
                        Debug.Log(
                            $"[TargetLock-Detail] Skip: {target.name} @ {targetPos} (No Line of Effect)."
                        );
                        continue;
                    }

                    if (!LineOfSightUtility.Evaluate(attackerPos, targetPos).HasLineOfSight)
                    {
                        Debug.Log(
                            $"[TargetLock-Detail] Skip: {target.name} @ {targetPos} (No Line of Sight)."
                        );
                        continue;
                    }

                    var targetStealth = target.GetComponent<UnitStealth>();
                    if (
                        targetStealth != null
                        && targetStealth.GetDetectionState(attacker) == DetectionState.Unnoticed
                    )
                    {
                        Debug.Log(
                            $"[TargetLock-Detail] Skip: {target.name} @ {targetPos} (Unnoticed)."
                        );
                        continue;
                    }

                    Debug.Log($"[TargetLock-Detail] ACCEPT: {target.name} @ {targetPos}");
                    if (added.Add(target))
                        allCandidates.Add(target);
                }

                if (allCandidates.Count == 0)
                {
                    Debug.LogWarning(
                        "[TargetLock-Detail] Initialization FAILED: allCandidates count is 0."
                    );
                    return;
                }

                int attackerY = attackerPos.y;
                foreach (Unit u in allCandidates)
                {
                    if (u.CurrentLayeredPosition.y == attackerY)
                        validTargets.Add(u);
                    else
                        pendingOffLayerStrikeTargets.Add(u);
                }

                if (debugTargeting)
                {
                    Debug.Log(
                        $"[TargetLock] Same-Y count={validTargets.Count}, "
                            + $"pending off-layer={pendingOffLayerStrikeTargets.Count}"
                    );
                    foreach (Unit u in pendingOffLayerStrikeTargets)
                        Debug.Log(
                            $"[TargetLock]   off-layer: {u.name} @ {u.CurrentLayeredPosition}"
                        );
                }

                if (validTargets.Count == 0)
                {
                    validTargets.AddRange(pendingOffLayerStrikeTargets);
                    pendingOffLayerStrikeTargets.Clear();
                    strikeOffLayerUnlocked = true;
                    if (debugTargeting)
                        Debug.Log(
                            "[TargetLock] No same-Y targets - using off-layer only (unlocked)."
                        );
                }

                SortUnitsByStrikePriority(validTargets, attackerPos);

                isActive = true;
                UpdateIndicator();

                Debug.Log(
                    $"[TargetLockService] Targeting ACTIVE. Candidates found: {validTargets.Count}. Initial target: {CurrentTarget.name}"
                );

                ServiceLocator
                    .Get<CameraController>()
                    .EnterOTSMode(attacker.transform, CurrentTarget.transform);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[TargetLock-Detail] FATAL EXCEPTION during InitializeTargeting: {ex}"
                );
            }
            finally
            {
                LineOfSightUtility.DebugLineOfEffect = prevLoEDebug;
            }
        }

        private static void SortUnitsByStrikePriority(List<Unit> list, Vector3Int attackerPos)
        {
            list.Sort(
                (a, b) =>
                {
                    int da = Mathf.Abs(a.CurrentLayeredPosition.y - attackerPos.y);
                    int db = Mathf.Abs(b.CurrentLayeredPosition.y - attackerPos.y);
                    int layer = da.CompareTo(db);
                    if (layer != 0)
                        return layer;
                    return PF2E_Core
                        .GetPF2eDistance3D(attackerPos, a.CurrentLayeredPosition)
                        .CompareTo(
                            PF2E_Core.GetPF2eDistance3D(attackerPos, b.CurrentLayeredPosition)
                        );
                }
            );
        }

        /// <summary>
        /// Process directional input to cycle targets.
        /// Left/Right: screen-space horizontal cycling, same Y level first.
        /// Up/Down: switch between targets on different Y elevation levels.
        /// </summary>
        public void HandleInput()
        {
            if (!isActive)
                return;

            bool canVerticalUnlock =
                !strikeOffLayerUnlocked && pendingOffLayerStrikeTargets.Count > 0;
            if (validTargets.Count <= 1 && !canVerticalUnlock)
                return;

            inputCooldown -= Time.deltaTime;
            Vector2 input = ServiceLocator.Get<InputService>().GetMovementVectorNormalized();

            if (input == Vector2.zero)
            {
                inputCooldown = 0f;
                return;
            }

            if (inputCooldown > 0f)
                return;

            float absX = Mathf.Abs(input.x);
            float absY = Mathf.Abs(input.y);

            bool acted = false;

            if (absX >= absY && absX > 0.3f)
            {
                acted = CycleScreenHorizontal(input.x > 0 ? 1 : -1);
            }
            else if (absY > absX && absY > 0.3f)
            {
                acted = CycleVertical(input.y > 0 ? 1 : -1);
            }

            if (acted)
                inputCooldown = targetCycleCooldown;
        }

        public void HideTargeting()
        {
            isActive = false;
            validTargets.Clear();
            pendingOffLayerStrikeTargets.Clear();
            strikeOffLayerUnlocked = false;
            currentAttacker = null;
            CleanupIndicator();
            ServiceLocator.Get<CameraController>().ExitOTSMode();
        }

        /// <summary>
        /// Cycle among targets using screen-space horizontal position.
        /// Prioritizes same-Y targets, wrapping at edges. Falls back to
        /// cross-Y targets if only one target exists on the current level.
        /// </summary>
        private bool CycleScreenHorizontal(int direction)
        {
            if (validTargets.Count <= 1)
                return false;

            Camera cam = Camera.main;
            if (cam == null)
                return false;

            Unit current = validTargets[currentIndex];
            int currentY = current.CurrentLayeredPosition.y;
            Vector3 currentScreen = cam.WorldToScreenPoint(current.transform.position);

            List<(int index, float screenX)> sameY = new List<(int, float)>();

            for (int i = 0; i < validTargets.Count; i++)
            {
                if (validTargets[i].CurrentLayeredPosition.y != currentY)
                    continue;
                float sx = cam.WorldToScreenPoint(validTargets[i].transform.position).x;
                sameY.Add((i, sx));
            }

            if (sameY.Count > 1)
            {
                sameY.Sort((a, b) => a.screenX.CompareTo(b.screenX));

                int pos = sameY.FindIndex(t => t.index == currentIndex);
                int next = pos + direction;
                if (next < 0)
                    next = sameY.Count - 1;
                else if (next >= sameY.Count)
                    next = 0;

                if (sameY[next].index != currentIndex)
                {
                    ApplyTargetSwitch(sameY[next].index);
                    return true;
                }
            }

            int bestIndex = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < validTargets.Count; i++)
            {
                if (i == currentIndex)
                    continue;

                float sx = cam.WorldToScreenPoint(validTargets[i].transform.position).x;
                float deltaX = sx - currentScreen.x;

                bool inDirection = direction > 0 ? deltaX > 1f : deltaX < -1f;
                if (!inDirection)
                    continue;

                float absDelta = Mathf.Abs(deltaX);
                if (absDelta < bestDist)
                {
                    bestDist = absDelta;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                ApplyTargetSwitch(bestIndex);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Switch to the nearest valid target on a higher (direction=1)
        /// or lower (direction=-1) Y elevation level. Breaks ties by
        /// screen-space proximity to the current target.
        /// </summary>
        private bool TryUnlockOffLayerStrikeTargets(int direction)
        {
            if (strikeOffLayerUnlocked || pendingOffLayerStrikeTargets.Count == 0)
                return false;

            if (debugTargeting)
                Debug.Log(
                    $"[TargetLock] Vertical unlock: merging {pendingOffLayerStrikeTargets.Count} "
                        + $"off-layer targets (dir={(direction > 0 ? "up" : "down")})"
                );

            strikeOffLayerUnlocked = true;
            Unit currentUnit = validTargets[currentIndex];
            HashSet<Unit> newlyUnlocked = new HashSet<Unit>(pendingOffLayerStrikeTargets);

            foreach (Unit u in pendingOffLayerStrikeTargets)
            {
                if (!validTargets.Contains(u))
                    validTargets.Add(u);
            }

            pendingOffLayerStrikeTargets.Clear();

            Vector3Int ap = currentAttacker.CurrentLayeredPosition;
            SortUnitsByStrikePriority(validTargets, ap);

            currentIndex = validTargets.IndexOf(currentUnit);
            if (currentIndex < 0)
                currentIndex = 0;

            int curY = validTargets[currentIndex].CurrentLayeredPosition.y;
            int bestIdx = -1;
            int bestAbsDy = int.MaxValue;
            for (int i = 0; i < validTargets.Count; i++)
            {
                if (!newlyUnlocked.Contains(validTargets[i]))
                    continue;

                int ty = validTargets[i].CurrentLayeredPosition.y;
                int dy = ty - curY;
                if (direction > 0 && dy <= 0)
                    continue;
                if (direction < 0 && dy >= 0)
                    continue;

                int ady = Mathf.Abs(dy);
                if (ady < bestAbsDy)
                {
                    bestAbsDy = ady;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
                ApplyTargetSwitch(bestIdx);

            return true;
        }

        private bool CycleVertical(int direction)
        {
            if (TryUnlockOffLayerStrikeTargets(direction))
                return true;

            Camera cam = Camera.main;
            Unit current = validTargets[currentIndex];
            int currentY = current.CurrentLayeredPosition.y;

            Vector2 currentScreen = Vector2.zero;
            if (cam != null)
            {
                Vector3 s = cam.WorldToScreenPoint(current.transform.position);
                currentScreen = new Vector2(s.x, s.y);
            }

            int bestIndex = -1;
            int bestYDelta = int.MaxValue;
            float bestScreenDist = float.MaxValue;

            for (int i = 0; i < validTargets.Count; i++)
            {
                if (i == currentIndex)
                    continue;

                int targetY = validTargets[i].CurrentLayeredPosition.y;
                int yDelta = targetY - currentY;

                if (direction > 0 ? yDelta <= 0 : yDelta >= 0)
                    continue;

                int absYDelta = Mathf.Abs(yDelta);

                float screenDist = 0f;
                if (cam != null)
                {
                    Vector3 s = cam.WorldToScreenPoint(validTargets[i].transform.position);
                    screenDist = Vector2.Distance(currentScreen, new Vector2(s.x, s.y));
                }

                if (
                    absYDelta < bestYDelta
                    || (absYDelta == bestYDelta && screenDist < bestScreenDist)
                )
                {
                    bestYDelta = absYDelta;
                    bestScreenDist = screenDist;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                ApplyTargetSwitch(bestIndex);
                return true;
            }

            return false;
        }

        private void ApplyTargetSwitch(int newIndex)
        {
            currentIndex = newIndex;
            UpdateIndicator();
            ServiceLocator.Get<CameraController>().SetOTSTarget(CurrentTarget.transform);
        }

        private void UpdateIndicator()
        {
            CleanupIndicator();

            if (CurrentTarget == null)
                return;

            if (targetIndicatorPrefab != null)
            {
                activeIndicator = Instantiate(
                    targetIndicatorPrefab,
                    CurrentTarget.transform.position + Vector3.up * 2.5f,
                    Quaternion.identity,
                    CurrentTarget.transform
                );
            }
        }

        private void CleanupIndicator()
        {
            if (activeIndicator != null)
            {
                Destroy(activeIndicator);
                activeIndicator = null;
            }
        }
    }
}
