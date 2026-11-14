using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// 勇者のレベルが上がった瞬間、画面の真ん中に光の輪が広がり「LEVEL UP」と新しい Lv が跳ねるように出る。足元にも光の柱が立つ。
    public class LevelUpBurst : MonoBehaviour
    {
        private const float Seconds = 1.6f;
        private static readonly Color Gold = new Color(1f, 0.83f, 0.3f, 1f);

        private GameObject hero;
        private LevelComponent level;
        private int shownLevel;
        private RectTransform root;
        private RectTransform ring;
        private HudShape ringShape;
        private RectTransform words;
        private Text title, sub;
        private CanvasGroup group;
        private float timer;

        public static LevelUpBurst Create(GameObject hero)
        {
            var rt = HudStyle.Place(HudStyle.Canvas.transform, "LevelUpBurst", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(600f, 300f));
            var burst = rt.gameObject.AddComponent<LevelUpBurst>();
            burst.hero = hero;
            burst.root = rt;
            burst.level = hero.GetComponent<LevelComponent>();
            burst.shownLevel = burst.level != null ? burst.level.CurrentLevel : 0;
            burst.Build();
            return burst;
        }

        private void Build()
        {
            var pal = HudStyle.HeroPalette;
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            ring = HudStyle.Place(root, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 240f));
            HudStyle.Stitch(ring, Gold, true, 0f, 0f).thickness = 6f;
            ringShape = HudStyle.Shape(ring, "Glow", HudShape.Kind.Circle, new Color(1f, 0.85f, 0.4f, 0.25f));
            words = HudStyle.Filled(root, "Words");
            title = HudStyle.Label(words, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(600f, 80f), "LEVEL UP", 64, Gold, TextAnchor.MiddleCenter, 6f, pal.Outline);
            sub = HudStyle.Label(words, "Level", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 50f), "", 36, Color.white, TextAnchor.MiddleCenter, 5f, pal.Outline);
        }

        private void Update()
        {
            if (level != null && level.CurrentLevel != shownLevel)
            {
                bool up = level.CurrentLevel > shownLevel && shownLevel > 0;
                shownLevel = level.CurrentLevel;
                if (up) Play();
            }
            if (timer <= 0f) return;
            timer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(timer / Seconds);
            float pop = t < 0.12f ? Mathf.Lerp(1.7f, 0.95f, t / 0.12f) : Mathf.Lerp(0.95f, 1f, Mathf.Clamp01((t - 0.12f) / 0.1f));
            words.localScale = Vector3.one * pop;
            ring.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.4f, 1f - (1f - t) * (1f - t));
            var glow = ringShape.color;
            glow.a = 0.25f * (1f - t);
            ringShape.color = glow;
            group.alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
        }

        private void Play()
        {
            timer = Seconds;
            group.alpha = 1f;
            sub.text = "Lv " + shownLevel;
            // 足元の光は目の高さより低くする（カメラが光の中に入ると画面全体が色づくため）。
            if (hero != null)
            {
                Vector3 feet = hero.transform.position;
                Game.Skill.LightPillar.Spawn(feet, 1.1f, 1.1f, 0.6f, new Color(1.5f, 1.2f, 0.45f, 1f), true);
                Game.Skill.ShockRing.Spawn(feet + Vector3.up * 0.1f, 0.5f, 3.5f, 0.5f, 0.25f, new Color(1.5f, 1.2f, 0.45f, 1f));
            }
        }
    }
}
