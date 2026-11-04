using UnityEngine;

namespace Game.Skill
{
    /// 魔法使いの ManaShield。全身を包む水色の魔力の膜をまとう。腕は構え続けず、効果中も普通に動ける。
    public class ManaShieldCast : GuardCast
    {
        private static readonly Vector3 BubbleCenter = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 BubbleScale = new Vector3(2.3f, 2.5f, 2.3f);

        protected override MeleeFx.Palette Palette => MeleeFx.Arcane;
        protected override Vector3 FilmCenter => BubbleCenter;
        protected override Vector3 FilmScale => BubbleScale;
        protected override bool HoldsGuardPose => false;
        protected override bool SurroundsView => true;
    }
}
