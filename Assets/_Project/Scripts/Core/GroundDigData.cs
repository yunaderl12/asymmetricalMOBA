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
    }
}
