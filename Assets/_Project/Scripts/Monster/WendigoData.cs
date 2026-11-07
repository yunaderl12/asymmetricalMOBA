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

        [Header("Pounce")]
        [Tooltip("Used when the target is between these distances (m).")]
        public float pounceMinRange = 6f;
        public float pounceMaxRange = 24f;
        [Tooltip("Seconds of crouching before the leap.")]
        public float pounceWindupSeconds = 0.7f;
        [Tooltip("Seconds in the air.")]
        public float pounceAirSeconds = 0.75f;
        [Tooltip("Height of the leap (m).")]
        public float pounceHeight = 5f;
        [Tooltip("Heroes this close to the landing point are hit (m).")]
        public float pounceHitRadius = 4f;
        public float pounceDamageScale = 1.4f;
        public float pounceKnockback = 6f;
        public float pounceRecoverSeconds = 0.9f;

        [Header("Ground Scratch")]
        [Tooltip("Used when the target is within this distance (m).")]
        public float scratchMaxRange = 18f;
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
    }
}
