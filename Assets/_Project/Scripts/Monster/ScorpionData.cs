using UnityEngine;

namespace Game.Monster
{
    /// 砂漠の広場のスコーピオン。地上を歩き、尾の毒針・突進・爪の十字斬りの3つの技を使う。
    [CreateAssetMenu(menuName = "Game/Boss/Scorpion Data", fileName = "Scorpion")]
    public class ScorpionData : BossData
    {
        [Header("Movement")]
        public float moveSpeed = 6f;
        [Tooltip("Turning speed (degrees per second).")]
        public float turnSpeed = 120f;
        [Tooltip("Steps per second of the legs at full walking speed.")]
        public float stepRate = 2.4f;

        [Header("Poison Sting")]
        [Tooltip("Used when the target is between these distances (m).")]
        public float stingMinRange = 8f;
        public float stingMaxRange = 30f;
        [Tooltip("Seconds of pulling the tail back before the shot.")]
        public float stingWindupSeconds = 0.8f;
        [Tooltip("Seconds of the snap after the shot.")]
        public float stingRecoverSeconds = 0.7f;
        [Tooltip("Shots per sting, fanned out to the front.")]
        public int stingShots = 3;
        [Tooltip("Angle between shots (degrees).")]
        public float stingSpreadDegrees = 12f;
        public float stingSpeed = 26f;
        public float stingRange = 34f;
        [Tooltip("Heroes this close to a shot are hit (m).")]
        public float stingHitRadius = 1.2f;
        public float stingDamageScale = 0.6f;
        [Tooltip("Poison: damage per tick (true damage) and seconds.")]
        public float poisonDamagePerTick = 6f;
        public float poisonSeconds = 5f;

        [Header("Charge")]
        [Tooltip("Used when the target is within this distance (m).")]
        public float chargeMaxRange = 26f;
        [Tooltip("Seconds of scraping the ground before the rush.")]
        public float chargeWindupSeconds = 0.9f;
        public float chargeSpeed = 26f;
        [Tooltip("Longest rush (m). Stops early at the edge of the plaza.")]
        public float chargeDistance = 26f;
        [Tooltip("Heroes this close to the front of the body are hit (m).")]
        public float chargeHitRadius = 3.2f;
        public float chargeDamageScale = 1.3f;
        public float chargeKnockback = 8f;
        [Tooltip("Seconds it stays still after the rush.")]
        public float chargeRecoverSeconds = 1.2f;

        [Header("Cross Slash")]
        [Tooltip("Used when the target is within this distance (m).")]
        public float slashMaxRange = 20f;
        [Tooltip("Seconds of raising both claws.")]
        public float slashWindupSeconds = 0.7f;
        [Tooltip("Seconds of the swing (both claws cross).")]
        public float slashSwingSeconds = 0.18f;
        public float slashRecoverSeconds = 0.8f;
        [Tooltip("Speed and reach of the X-shaped wave (m/s, m).")]
        public float slashSpeed = 22f;
        public float slashRange = 26f;
        [Tooltip("Half width of the X (m). Heroes within this of the wave's center line are hit.")]
        public float slashHalfSize = 3.2f;
        public float slashDamageScale = 1.0f;
    }
}
