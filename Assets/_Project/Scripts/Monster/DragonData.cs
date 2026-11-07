using UnityEngine;

namespace Game.Monster
{
    /// 山の広場のドラゴン。ずっと飛んでいて、炎を吐くのと、地上の勇者めがけて急降下するのを交互に使う。
    [CreateAssetMenu(menuName = "Game/Boss/Dragon Data", fileName = "Dragon")]
    public class DragonData : BossData
    {
        [Header("Flight")]
        [Tooltip("Height above the plaza floor while circling (m).")]
        public float circleAltitude = 20f;
        [Tooltip("Radius of the circle around the plaza center (m).")]
        public float circleRadius = 18f;
        [Tooltip("Flying speed while circling and moving between attacks (m/s).")]
        public float flySpeed = 14f;
        [Tooltip("Wing beats per second while flying normally.")]
        public float flapRate = 1.1f;

        [Header("Fire Breath")]
        [Tooltip("Height above the floor while breathing fire (m).")]
        public float breathAltitude = 13f;
        [Tooltip("Horizontal distance kept from the target while breathing (m).")]
        public float breathDistance = 14f;
        [Tooltip("Seconds of rearing back before the fire comes out.")]
        public float breathWindupSeconds = 1.0f;
        [Tooltip("Seconds the fire keeps coming.")]
        public float breathSeconds = 2.4f;
        [Tooltip("Reach of the fire from the mouth (m).")]
        public float breathRange = 24f;
        [Tooltip("Half angle of the fire cone (degrees).")]
        public float breathHalfAngle = 16f;
        [Tooltip("How fast the fire follows the target (degrees per second).")]
        public float breathTurnSpeed = 28f;
        [Tooltip("Seconds between damage ticks to heroes inside the fire.")]
        public float breathTickSeconds = 0.3f;
        [Tooltip("Share of Atk per tick.")]
        public float breathDamageScale = 0.35f;
        [Tooltip("Burn after leaving the fire: damage per tick (true damage) and seconds.")]
        public float burnDamagePerTick = 4f;
        public float burnSeconds = 3f;

        [Header("Dive")]
        [Tooltip("Seconds the target circle is shown on the ground before the dive starts.")]
        public float diveTelegraphSeconds = 1.3f;
        [Tooltip("Diving speed (m/s).")]
        public float diveSpeed = 48f;
        [Tooltip("Radius of the impact around the landing point (m).")]
        public float diveImpactRadius = 6f;
        [Tooltip("Share of Atk dealt by the impact.")]
        public float diveDamageScale = 1.6f;
        [Tooltip("How far heroes hit by the impact are thrown (m).")]
        public float diveKnockback = 7f;
        [Tooltip("Seconds it stays on the ground after landing. Melee heroes can hit it now.")]
        public float diveGroundSeconds = 2.2f;
        [Tooltip("Seconds to climb back to the circling height.")]
        public float diveClimbSeconds = 1.6f;
    }
}
