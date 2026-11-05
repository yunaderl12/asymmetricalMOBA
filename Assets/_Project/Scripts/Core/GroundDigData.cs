using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// 地面を削れない場所のうち、シーンのコライダーから分からないもの。Tools/LowPoly/Bake Dig Data で作る。
    public class GroundDigData : ScriptableObject
    {
        public const string ResourcePath = "GroundDigData";

        [Tooltip("木の根元の位置 (x, z)。見た目だけでコライダーがない。")]
        public List<Vector2> treePositions = new List<Vector2>();

        [Tooltip("水辺・山・橋の下など削れない範囲。1マス1ビット。")]
        public byte[] blockedBits = new byte[0];
        public Vector2 blockedOrigin;
        public float blockedCell = 2.5f;
        public int blockedColumns;
        public int blockedRows;

        public bool IsBlocked(float x, float z)
        {
            if (blockedBits == null || blockedBits.Length == 0) return false;
            int cx = Mathf.FloorToInt((x - blockedOrigin.x) / blockedCell);
            int cz = Mathf.FloorToInt((z - blockedOrigin.y) / blockedCell);
            if (cx < 0 || cz < 0 || cx >= blockedColumns || cz >= blockedRows) return true;
            int bit = cx + cz * blockedColumns;
            return (blockedBits[bit >> 3] & (1 << (bit & 7))) != 0;
        }
    }
}
