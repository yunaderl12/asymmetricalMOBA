using UnityEngine;
using UnityEngine.UI;
using Game.Hero;
using Game.Monster;

namespace Game.UI
{
    /// 勇者がボスの広場に近づくと、画面の上にボスの名前とHPの大きなバーを出す。ボスは空高く飛ぶので、頭上のバーの代わり。
    public class BossHealthBarUI : MonoBehaviour
    {
        private const float ShowMargin = 12f; // ボスが勇者を狙う範囲より、これだけ広く見せる
        private static readonly Vector2 Size = new Vector2(640f, 58f);

        private static BossHealthBarUI instance;

        private RectTransform panel;
        private Text nameText;
        private Text hpText;
        private HudBar bar;
        private BossBase shown;

        public static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("BossHealthBarUI");
            instance = go.AddComponent<BossHealthBarUI>();
        }

        private void Awake()
        {
            var p = HudStyle.For(HudStyle.CurrentTheme);
            panel = HudStyle.Place(HudStyle.Canvas.transform, "BossHealth", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -162f), Size);
            HudStyle.Felt(panel, p, HudShape.Kind.RoundRect, 18f, 6f, 5f);
            nameText = HudStyle.Label(panel, "Name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -6f), new Vector2(360f, 22f),
                "", 19, p.Light, TextAnchor.MiddleLeft);
            hpText = HudStyle.Label(panel, "Hp", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -6f), new Vector2(200f, 22f),
                "", 16, p.Light, TextAnchor.MiddleRight);
            var barRect = HudStyle.Place(panel, "Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(Size.x - 40f, 18f));
            bar = HudBar.Create(barRect, HudStyle.Hp, p.Track);
            panel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (panel != null) Destroy(panel.gameObject);
        }

        private void LateUpdate()
        {
            var boss = Pick();
            if (boss != shown)
            {
                shown = boss;
                panel.gameObject.SetActive(boss != null);
                if (boss != null)
                {
                    nameText.text = boss.DisplayName;
                    bar.Set((float)boss.Health.CurrentHP / Mathf.Max(1, boss.Health.MaxHP));
                }
            }
            if (shown == null) return;
            var hp = shown.Health;
            bar.Set((float)hp.CurrentHP / Mathf.Max(1, hp.MaxHP));
            hpText.text = hp.CurrentHP + " / " + hp.MaxHP;
        }

        // 操作している勇者がいちばん近いボスの広場にいれば、そのボス。
        private static BossBase Pick()
        {
            var hero = HumanHeroMarker.Instance;
            if (hero == null || !hero.isActiveAndEnabled) return null;
            BossBase best = null;
            float bestDist = float.MaxValue;
            foreach (var boss in BossBase.All)
            {
                if (boss == null || boss.IsDead || boss.Health == null) continue;
                Vector3 d = hero.transform.position - boss.ArenaCenter;
                d.y = 0f;
                float limit = boss.AggroRadius + ShowMargin;
                if (d.magnitude > limit || d.magnitude >= bestDist) continue;
                bestDist = d.magnitude;
                best = boss;
            }
            return best;
        }
    }
}
