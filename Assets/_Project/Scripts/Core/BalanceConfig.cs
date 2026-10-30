using UnityEngine;

namespace Game.Core
{
    /// Global balance constants. Loaded from Resources/BalanceConfig; falls back to code defaults if the asset is missing.
    [CreateAssetMenu(menuName = "Game/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Damage")]
        public float damageK = 20f;
        public int minDamage = 1;

        [Header("Cores")]
        public int coreDf = 30;
        public int coreMDf = 30;

        private static BalanceConfig instance;

        public static BalanceConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<BalanceConfig>("BalanceConfig");
                    if (instance == null)
                    {
                        instance = CreateInstance<BalanceConfig>();
                        Debug.LogWarning("[BalanceConfig] Resources/BalanceConfig not found; using code defaults.");
                    }
                }
                return instance;
            }
        }
    }
}
