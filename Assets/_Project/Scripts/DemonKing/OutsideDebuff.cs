using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.DemonKing
{
    /// Penalties for the Demon King while outside the castle:
    /// MP drain, plus a Spd / Atk debuff that builds up over time outside and recovers gradually inside.
    public class OutsideDebuff : MonoBehaviour
    {
        [SerializeField] private int mpDrainPerSecond = 5;
        [SerializeField] private int drainDisableLevel = 15;

        [Header("Spd / Atk debuff")]
        [Tooltip("Max reduction at full stack (0.3 = -30%)")]
        [SerializeField] private float maxDebuff = 0.3f;
        [Tooltip("Seconds outside to reach full stack")]
        [SerializeField] private float secondsToMax = 60f;
        [Tooltip("Seconds inside to fully recover from full stack")]
        [SerializeField] private float secondsToRecover = 60f;
        [SerializeField] private string sourceId = "OutsideDebuff";

        private ManaComponent mana;
        private LevelComponent level;
        private DemonCastleRegen castleRegen;
        private StatModifierContainer mods;
        private float drainAccumulator;
        private bool currentlyDebuffed;

        /// 0..1, fraction of maxDebuff currently applied.
        public float Stack { get; private set; }
        public float CurrentDebuff => maxDebuff * Stack;
        private int appliedPercent = -1;

        private void Start()
        {
            mana = GetComponent<ManaComponent>();
            level = GetComponent<LevelComponent>();
            mods = GetComponent<StatModifierContainer>();
            castleRegen = FindFirstObjectByType<DemonCastleRegen>();
        }

        private void OnDisable()
        {
            if (mods != null) mods.RemoveModifier(sourceId);
            appliedPercent = -1;
        }

        private void Update()
        {
            bool inside = castleRegen != null && castleRegen.IsHostInside(gameObject);
            bool penaltiesOff = level != null && level.CurrentLevel >= drainDisableLevel;
            bool outside = !inside && !penaltiesOff;

            UpdateStack(outside);
            UpdateDrain(outside, penaltiesOff);
        }

        private void UpdateStack(bool outside)
        {
            if (outside) Stack = Mathf.Min(1f, Stack + Time.deltaTime / Mathf.Max(0.01f, secondsToMax));
            else Stack = Mathf.Max(0f, Stack - Time.deltaTime / Mathf.Max(0.01f, secondsToRecover));

            // Only touch the modifier container when the applied value changes by a whole percent.
            int percent = Mathf.RoundToInt(CurrentDebuff * 100f);
            if (percent == appliedPercent || mods == null) return;
            appliedPercent = percent;
            if (percent <= 0) mods.RemoveModifier(sourceId);
            else
            {
                float mul = 1f - percent / 100f;
                mods.AddOrReplaceModifier(new StatModifier(sourceId, atk: mul, spd: mul));
            }
        }

        private void UpdateDrain(bool outside, bool penaltiesOff)
        {
            if (!outside)
            {
                if (currentlyDebuffed)
                {
                    currentlyDebuffed = false;
                    drainAccumulator = 0f;
                    Debug.Log(penaltiesOff ? "[OutsideDebuff] inactive (Lv" + drainDisableLevel + "+)" : "[OutsideDebuff] inactive");
                }
                return;
            }

            if (!currentlyDebuffed)
            {
                currentlyDebuffed = true;
                Debug.Log("[OutsideDebuff] active (MP drain " + mpDrainPerSecond + "/s)");
            }

            if (mana == null) return;
            drainAccumulator += mpDrainPerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(drainAccumulator);
            if (whole > 0)
            {
                if (mana.CurrentMP > 0)
                {
                    int actual = Mathf.Min(whole, mana.CurrentMP);
                    mana.TryUseMana(actual);
                }
                drainAccumulator -= whole;
            }
        }
    }
}
