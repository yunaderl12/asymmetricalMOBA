using UnityEngine;
using Game.Combat;

namespace Game.UI
{
    /// 自分の攻撃が当たった手応え。照準の周りに「×」の印がぽんと出て（倒したときは赤く大きく）、当たった魔物の体が一瞬白く光る。
    public class HitFeedback : MonoBehaviour
    {
        private const float HitSeconds = 0.22f;
        private const float KillSeconds = 0.45f;
        private const float ArmOffset = 15f;
        private const float ArmLength = 13f;
        private const float ArmWidth = 4f;
        private const float FlashSeconds = 0.12f;
        private const float FlashInterval = 0.08f;
        private static readonly Color HitColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color KillColor = new Color(1f, 0.22f, 0.18f, 1f);
        private static readonly Color FlashColor = new Color(2.2f, 2.2f, 2.2f, 0.9f);

        private GameObject hero;
        private RectTransform root;
        private HudShape[] arms;
        private float timer;
        private float seconds = 1f;
        private bool kill;

        public static HitFeedback Create(GameObject hero)
        {
            var rt = HudStyle.Place(HudStyle.Canvas.transform, "HitMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            var feedback = rt.gameObject.AddComponent<HitFeedback>();
            feedback.hero = hero;
            feedback.root = rt;
            feedback.Build();
            return feedback;
        }

        private void Build()
        {
            arms = new HudShape[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var arm = HudStyle.Place(root, "Arm_" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * (ArmOffset + ArmLength * 0.5f), new Vector2(ArmLength, ArmWidth));
                arm.localEulerAngles = new Vector3(0f, 0f, angle);
                arms[i] = HudStyle.Shape(arm, "Bar", HudShape.Kind.RoundRect, HitColor, ArmWidth * 0.5f);
                arms[i].raycastTarget = false;
            }
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += OnAnyDamage;
            HealthComponent.AnyDeath += OnAnyDeath;
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= OnAnyDamage;
            HealthComponent.AnyDeath -= OnAnyDeath;
        }

        private bool IsMine(GameObject attacker) => attacker != null && hero != null && attacker == hero;

        private void OnAnyDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (!IsMine(attacker) || target == null || target.gameObject == hero || target.CompareTag("Player")) return;
            if (!kill || timer <= 0f) Show(false);
            FlashBody(target);
        }

        private void OnAnyDeath(HealthComponent target, HealthComponent.DeathInfo info)
        {
            if (!IsMine(info.lastHitter) || target == null || target.gameObject == hero || target.CompareTag("Player")) return;
            Show(true);
        }

        private void Show(bool isKill)
        {
            kill = isKill;
            seconds = isKill ? KillSeconds : HitSeconds;
            timer = seconds;
            foreach (var arm in arms) arm.color = isKill ? KillColor : HitColor;
        }

        // 当たった魔物を白い膜で一瞬包む。続けて当たっても光りっぱなしにならないよう間をあける。
        private static void FlashBody(HealthComponent target)
        {
            var flash = target.GetComponent<HitFlashClock>();
            if (flash == null) flash = target.gameObject.AddComponent<HitFlashClock>();
            if (Time.time < flash.NextAt) return;
            flash.NextAt = Time.time + FlashInterval;
            if (target.GetComponent<Game.Monster.Monster>() == null && target.GetComponent<Game.Monster.BossBase>() == null) return;
            Game.Skill.AfterImage.Spawn(target.transform, FlashColor, FlashSeconds);
        }

        private void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(timer / seconds);
            // 大きく出て、すぐ縮んで消える。
            float pop = kill ? 1.6f : 1.3f;
            root.localScale = Vector3.one * Mathf.Lerp(pop, 1f, Mathf.Clamp01(t * 4f));
            SetAlpha(1f - t * t);
        }

        private void SetAlpha(float a)
        {
            foreach (var arm in arms)
            {
                var c = arm.color;
                c.a = a * (kill ? KillColor.a : HitColor.a);
                arm.color = c;
            }
        }

        private class HitFlashClock : MonoBehaviour
        {
            public float NextAt;
        }
    }
}
