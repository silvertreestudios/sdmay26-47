using System;
using UnityEngine;

namespace PathfinderTactics.Characters
{
    /// <summary>
    /// Component that tracks a Unit's current and maximum HP at runtime.
    /// Initializes max HP from the Unit's stats (via Unit.getTotalHP()).
    /// Raises an OnHpChanged event whenever current HP changes.
    /// </summary>
    public class UnitHealth : MonoBehaviour
    {
        public event EventHandler OnHpChanged;

        private Unit unit;

        [SerializeField]
        private int currentHP = 0;

        [SerializeField]
        private int maxHP = 0;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            if (unit != null)
            {
                // Initialize maxHP from the Unit's stats if not explicitly set in the inspector.
                int derivedMax = unit.getTotalHP();
                if (maxHP <= 0)
                    maxHP = derivedMax;

                // If currentHP was left as 0 in the inspector, assume full health on Awake.
                if (currentHP <= 0)
                    currentHP = maxHP;
                else
                    currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            }
        }

        public int GetCurrentHP() => currentHP;
        public int GetMaxHP() => maxHP;

        public void SetCurrentHP(int hp)
        {
            int clamped = Mathf.Clamp(hp, 0, maxHP);
            if (clamped == currentHP) return;
            currentHP = clamped;
            OnHpChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;
            SetCurrentHP(currentHP - amount);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            SetCurrentHP(currentHP + amount);
        }

        public void SetMaxHP(int hp)
        {
            maxHP = Mathf.Max(1, hp);
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            OnHpChanged?.Invoke(this, EventArgs.Empty);
        }

        // -------------------------
        // Debug helpers (editor-only)
        // -------------------------

        [Header("Debug (Editor only)")]
        [Tooltip("Enable debug key to apply damage in Play mode (editor only).")]
        public bool enableDebugKey = false;

        [Tooltip("Key to press to apply debug damage (editor only).")]
        public KeyCode debugDamageKey = KeyCode.H;

        [Tooltip("Damage amount applied when pressing the debug key.")]
        public int debugDamageAmount = 10;

        /// <summary>
        /// Context menu helper — apply 10 damage from the inspector's context menu.
        /// </summary>
        [ContextMenu("Debug: Take 10 Damage")]
        public void DebugTake10()
        {
            ApplyDamage(10);
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!enableDebugKey) return;
            if (UnityEngine.Input.GetKeyDown(debugDamageKey))
            {
                ApplyDamage(debugDamageAmount);
                UnityEngine.Debug.Log($"UnitHealth Debug: Applied {debugDamageAmount} damage to {gameObject.name}");
            }
        }
#endif
    }
}
