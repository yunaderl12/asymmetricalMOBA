using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.DemonKing
{
    public class OutsideDebuff : MonoBehaviour
    {
        [SerializeField] private int mpDrainPerSecond = 5;
        [SerializeField] private int drainDisableLevel = 15;

        private ManaComponent mana;
        private LevelComponent level;
        private DemonCastleRegen castleRegen;
        private float drainAccumulator;
        private bool currentlyDebuffed;

        private void Start()
        {
            mana = GetComponent<ManaComponent>();
            level = GetComponent<LevelComponent>();
            castleRegen = FindFirstObjectByType<DemonCastleRegen>();
        }

        private void Update()
        {
            if (level != null && level.CurrentLevel >= drainDisableLevel)
            {
                if (currentlyDebuffed)
                {
                    currentlyDebuffed = false;
                    drainAccumulator = 0f;
                    Debug.Log("[OutsideDebuff] inactive (Lv" + drainDisableLevel + "+)");
                }
                return;
            }

            bool inside = castleRegen != null && castleRegen.IsHostInside(gameObject);
            if (inside)
            {
                if (currentlyDebuffed)
                {
                    currentlyDebuffed = false;
                    drainAccumulator = 0f;
                    Debug.Log("[OutsideDebuff] inactive");
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
