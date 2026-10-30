using UnityEngine;

namespace Game.Combat
{
    /// Static Df / mDf for objects without their own stat source (cores etc).
    public class FixedDefense : MonoBehaviour, IDefenseSource
    {
        [SerializeField] private int df;
        [SerializeField] private int mDf;

        public int Df => df;
        public int MDf => mDf;

        public void Set(int newDf, int newMDf)
        {
            df = newDf;
            mDf = newMDf;
        }
    }
}
