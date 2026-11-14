namespace Game.Combat
{
    /// 押し飛ばしとスタンへの耐性（重戦士のパッシブ「不動の体」など）。1 でそのまま、0 で効かない。
    public interface ICrowdControlResist
    {
        float KnockbackScale { get; }
        float StunDurationScale { get; }
    }
}
