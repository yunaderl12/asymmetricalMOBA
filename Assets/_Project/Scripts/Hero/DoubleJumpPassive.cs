using UnityEngine;
using Game.Skill;

namespace Game.Hero
{
    /// 魔法使いのダブルジャンプ。空中でもう一度跳べる。足元に魔法陣が一瞬出て、そこから持ち上げられる。
    public class DoubleJumpPassive : HeroPassive
    {
        private const float CircleRadius = 0.85f;
        private const float CircleSeconds = 0.4f;
        private const int SparkleCount = 7;
        private static readonly Color CircleColor = new Color(0.55f, 1.5f, 1.7f, 1f);
        private static readonly Color PillarColor = new Color(0.3f, 0.9f, 1.3f, 1f);

        private bool used;

        protected override void OnInit()
        {
            if (Controller != null) Controller.AirJump = TryAirJump;
        }

        private void Update()
        {
            if (Body != null && Body.isGrounded) used = false;
        }

        private float TryAirJump()
        {
            if (used || !CanAct() || !TryPay()) return 0f;
            used = true;
            Vector3 feet = transform.position;
            RuneFlash.Spawn(feet, Quaternion.Euler(90f, 0f, 0f), CircleRadius, CircleSeconds, CircleColor);
            LightPillar.Spawn(feet, CircleRadius * 0.7f, 1.4f, 0.28f, PillarColor, true);
            for (int i = 0; i < SparkleCount; i++)
            {
                Vector2 p = Random.insideUnitCircle * CircleRadius;
                SparkleStar.Spawn(feet + new Vector3(p.x, 0.05f, p.y), 0.12f, 0.4f, Vector3.up * Random.Range(1.5f, 3f));
            }
            return Data.airJumpSpeed;
        }

        private void OnDestroy()
        {
            if (Controller != null) Controller.AirJump = null;
        }
    }
}
