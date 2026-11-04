using UnityEngine;

namespace Game.Combat
{
    /// 防御計算の後のダメージを受ける直前に呼ばれる。true を返すとそのダメージは0になる。
    public interface IDamageBlocker
    {
        bool TryBlock(int damage, GameObject attacker, DamageType type);
    }
}
