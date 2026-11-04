namespace Game.Skill
{
    /// 重戦士の IronWall。動きは Guard と同じで、色だけ重戦士のものにする。
    public class IronWallCast : GuardCast
    {
        protected override MeleeFx.Palette Palette => MeleeFx.Ember;
    }
}
