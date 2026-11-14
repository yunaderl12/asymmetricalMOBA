using Game.Combat;

namespace Game.Hero
{
    /// 重戦士の「不動の体」。押し飛ばされず、スタンの時間が短くなる。
    public class UnshakablePassive : HeroPassive, ICrowdControlResist
    {
        public float KnockbackScale => Data != null ? Data.knockbackScale : 1f;
        public float StunDurationScale => Data != null ? Data.stunScale : 1f;
    }
}
