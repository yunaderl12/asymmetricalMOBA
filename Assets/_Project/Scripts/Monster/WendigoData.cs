using UnityEngine;

namespace Game.Monster
{
    /// 森の広場のウェンディゴ。四つ足で駆け、雄叫び（森の呪い）・飛び掛かり・地面の引っ掻き（瓦礫を飛ばす）を使う。
    [CreateAssetMenu(menuName = "Game/Boss/Wendigo Data", fileName = "Wendigo")]
    public class WendigoData : BossData
    {
        [Header("Movement")]
        public float moveSpeed = 6.5f;
        [Tooltip("Turning speed (degrees per second).")]
        public float turnSpeed = 150f;
        [Tooltip("Strides per second at full running speed.")]
        public float stepRate = 1.7f;

        [Header("Roar / Forest Curse")]
        [Tooltip("Seconds of the roar. It roars when it first sees a hero, and again after the cooldown.")]
        public float roarSeconds = 2.2f;
        [Tooltip("Heroes within this distance (m) of the Wendigo are cursed by the roar.")]
        public float roarRadius = 45f;
        [Tooltip("Seconds before it can roar again during a fight.")]
        public float roarCooldown = 30f;
        [Tooltip("Cursed heroes see only this far while they stay in the forest (fog starts / ends, m).")]
        public float curseFogStart = 3f;
        public float curseFogEnd = 18f;
        public Color curseFogColor = new Color(0.07f, 0.09f, 0.08f, 1f);

        [Header("Leap (far)")]
        [Tooltip("When the target is at least this far (m), it leaps forward at them. Closer than this, it uses the melee attacks.")]
        public float pounceMinRange = 12f;
        [Tooltip("Longest leap (m).")]
        public float pounceMaxRange = 38f;
        [Tooltip("Seconds of crouching before the leap.")]
        public float pounceWindupSeconds = 0.7f;
        [Tooltip("Seconds in the air for the longest leap (shorter leaps are a little quicker).")]
        public float pounceAirSeconds = 1.1f;
        [Tooltip("Height of the longest leap (m).")]
        public float pounceHeight = 9f;
        [Tooltip("Heroes this close to the landing point are hit (m).")]
        public float pounceHitRadius = 4f;
        public float pounceDamageScale = 1.4f;
        public float pounceKnockback = 6f;
        public float pounceRecoverSeconds = 0.9f;

        [Header("Ground Scratch (near)")]
        [Tooltip("Used when the target is within this distance (m).")]
        public float scratchMaxRange = 12f;
        [Tooltip("The claw itself hits heroes in front within this distance (m) and angle (degrees).")]
        public float scratchHitRange = 6.5f;
        public float scratchHitArc = 110f;
        public float scratchHitDamageScale = 1.0f;
        public float scratchKnockback = 4f;
        [Tooltip("Seconds of raising the arm.")]
        public float scratchWindupSeconds = 0.6f;
        [Tooltip("Seconds of the swing that tears the ground.")]
        public float scratchSwingSeconds = 0.22f;
        public float scratchRecoverSeconds = 0.8f;
        [Tooltip("Pieces of rubble thrown forward in a fan.")]
        public int debrisCount = 9;
        public float debrisSpreadDegrees = 55f;
        public float debrisSpeed = 20f;
        public float debrisRange = 24f;
        [Tooltip("Heroes this close to a piece are hit (m).")]
        public float debrisHitRadius = 1.1f;
        public float debrisDamageScale = 0.55f;
        [Tooltip("The torn-up patch of ground (radius and depth, m).")]
        public float scratchDigRadius = 2.2f;
        public float scratchDigDepth = 0.5f;

        [Header("Smash (near)")]
        [Tooltip("Rears up and brings both arms down in front, breaking the ground. Used when the target is within scratchMaxRange.")]
        public float smashWindupSeconds = 0.85f;
        public float smashSlamSeconds = 0.14f;
        public float smashRecoverSeconds = 1.0f;
        [Tooltip("Heroes within this distance (m) of the impact point, in front of the Wendigo, are hit.")]
        public float smashRadius = 7f;
        public float smashArcDegrees = 150f;
        public float smashDamageScale = 1.6f;
        public float smashKnockback = 8f;
        public float smashDigRadius = 3.4f;
        public float smashDigDepth = 0.8f;
        [Tooltip("Rubble thrown out all around the impact.")]
        public int smashDebrisCount = 10;
        public float smashDebrisRange = 12f;
    }
}
