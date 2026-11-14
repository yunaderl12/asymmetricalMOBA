using UnityEngine;

namespace Game.Monster
{
    /// 壊れた塔が傾きながら地面へ沈み込む。土ぼこりが何度か上がり、近くにいる勇者の画面が揺れる。当たり判定はそのまま。
    public class TowerCollapse : MonoBehaviour
    {
        private const float Seconds = 1.4f;
        private const float SinkDepth = 2.6f;
        private const float TiltDegrees = 14f;
        private const float DustInterval = 0.18f;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 tiltAxis;
        private float timer;
        private float nextDust;

        public static void Play(Transform model, Vector3 basePosition)
        {
            if (model == null) return;
            var collapse = model.gameObject.AddComponent<TowerCollapse>();
            collapse.startPosition = model.localPosition;
            collapse.startRotation = model.localRotation;
            var axis = Random.insideUnitCircle.normalized;
            collapse.tiltAxis = new Vector3(axis.x, 0f, axis.y);
            Game.Hero.HeroFPSController.ShakeNear(basePosition, 0.22f, 45f, 0.8f);
            Game.Skill.MeleeFx.DustBurst(basePosition, 3f, 30, 6f);
            Game.Skill.ShockRing.Spawn(basePosition + Vector3.up * 0.2f, 1f, 9f, 0.6f, 0.6f, new Color(0.75f, 0.62f, 0.5f, 0.8f));
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Seconds);
            float ease = t * t * (3f - 2f * t);
            transform.localPosition = startPosition + Vector3.down * (SinkDepth * ease);
            transform.localRotation = Quaternion.AngleAxis(TiltDegrees * ease, transform.parent != null ? transform.parent.InverseTransformDirection(tiltAxis) : tiltAxis) * startRotation;
            if (timer >= nextDust && t < 1f)
            {
                nextDust = timer + DustInterval;
                Vector2 p = Random.insideUnitCircle * 2.5f;
                Game.Skill.MeleeFx.DustBurst(transform.position + new Vector3(p.x, 0f, p.y) - Vector3.up * transform.localPosition.y, 1.6f, 10, 3f);
            }
            if (t >= 1f) enabled = false;
        }
    }
}
