using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// 勇者のレベルが上がった瞬間の演出。画面の上寄りに、布の丸いバッジ（新しい Lv）とリボン「レベルアップ！」が
    /// 順に出て、後ろで布の光の筋がゆっくり回る。バッジの数字は古い Lv からめくれて新しい Lv になる。
    /// 少し見せたら上へ抜けて消え、スキルポイントの紙のメモ（SkillLevelUpUI）が右下に出る。足元にも低い光が立つ。
    public class LevelUpBurst : MonoBehaviour
    {
        /// 出てから消えきるまでの秒数。SkillLevelUpUI はこのあとに右下のメモを出す。
        public const float Seconds = 2.1f;
        private const float OutSeconds = 0.35f;
        private const float BadgeSize = 148f;
        private const int RayCount = 12;
        private const int SparkCount = 8;
        private static readonly Color RayGold = new Color(1f, 0.8f, 0.25f, 0.85f);
        private static readonly Color RayCream = new Color(1f, 0.93f, 0.6f, 0.7f);
        private static readonly Color FootGlow = new Color(1.5f, 1.2f, 0.45f, 1f);

        private GameObject hero;
        private LevelComponent level;
        private int shownLevel;
        private RectTransform root;
        private RectTransform rays;
        private RectTransform badge;
        private RectTransform numberNode;
        private Text number;
        private RectTransform ribbon;
        private RectTransform pointTag;
        private RectTransform[] sparks;
        private CanvasGroup group;
        private CanvasGroup rayGroup;
        private float timer = -1f;
        private bool flipped;

        public static LevelUpBurst Create(GameObject hero)
        {
            var rt = HudStyle.Place(HudStyle.Canvas.transform, "LevelUpBurst", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(640f, 360f));
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
            var center = new Vector2(0.5f, 0.5f);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            // 後ろで回る布の光の筋（長短を交互に）。
            rays = HudStyle.Place(root, "Rays", center, center, new Vector2(0f, 20f), Vector2.zero);
            rayGroup = rays.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < RayCount; i++)
            {
                bool longRay = i % 2 == 0;
                var ray = HudStyle.Place(rays, "Ray", center, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(longRay ? 26f : 18f, longRay ? 150f : 118f));
                ray.localEulerAngles = new Vector3(0f, 0f, i * 360f / RayCount);
                HudStyle.Shape(ray, "Strip", HudShape.Kind.RoundRect, longRay ? RayGold : RayCream, 10f);
            }

            // きらめきの粒（ひし形）。
            sparks = new RectTransform[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                float size = i % 3 == 0 ? 20f : 13f;
                var spark = HudStyle.Place(root, "Spark", center, center, Vector2.zero, new Vector2(size, size));
                spark.localEulerAngles = new Vector3(0f, 0f, 45f);
                HudStyle.Shape(spark, "Diamond", HudShape.Kind.RoundRect, i % 2 == 0 ? Color.white : HudStyle.Gold, 3f);
                sparks[i] = spark;
            }

            // 布のバッジ。金のふち → 布 → 縫い目 → 「Lv」と数字。
            badge = HudStyle.Place(root, "Badge", center, center, new Vector2(0f, 20f), new Vector2(BadgeSize, BadgeSize));
            HudStyle.DropShadow(badge, HudShape.Kind.Circle, pal.Shadow, 0f, 7f);
            HudStyle.Shape(badge, "Rim", HudShape.Kind.Circle, HudStyle.Gold, 0f);
            var felt = HudStyle.Place(badge, "Body", center, center, Vector2.zero, new Vector2(BadgeSize - 14f, BadgeSize - 14f));
            HudStyle.Felt(felt, pal, HudShape.Kind.Circle, 0f, 8f, 0f);
            HudStyle.Label(badge, "Lv", center, center, new Vector2(0f, 38f), new Vector2(120f, 30f), "Lv", 24, HudStyle.Gold, TextAnchor.MiddleCenter, 3f, pal.Outline);
            numberNode = HudStyle.Place(badge, "Number", center, center, new Vector2(0f, -10f), new Vector2(140f, 80f));
            number = HudStyle.AddText(numberNode, "", 66, Color.white, TextAnchor.MiddleCenter, 5f, pal.Outline);

            // バッジの下に重ねるリボン。
            ribbon = HudStyle.Place(root, "Ribbon", center, center, new Vector2(0f, -66f), new Vector2(340f, 54f));
            HudStyle.DropShadow(ribbon, HudShape.Kind.Ribbon, pal.Shadow, 0f, 5f).notch = 18f;
            HudStyle.Shape(ribbon, "Body", HudShape.Kind.Ribbon, pal.Ribbon).notch = 18f;
            HudStyle.AddText(HudStyle.Filled(ribbon, "Text"), "レベルアップ！", 30, Color.white, TextAnchor.MiddleCenter, 3f, pal.Outline);

            // スキルポイントの紙の札。
            pointTag = HudStyle.Place(root, "Tag", center, center, new Vector2(0f, -122f), new Vector2(230f, 40f));
            pointTag.localEulerAngles = new Vector3(0f, 0f, -2f);
            HudStyle.Paper(pointTag, pal, HudShape.Kind.RoundRect, 3f, 0, 4f);
            HudStyle.AddText(HudStyle.Filled(pointTag, "Text"), "スキルポイント +1", 21, pal.Ink, TextAnchor.MiddleCenter);
        }

        private void Update()
        {
            if (level != null && level.CurrentLevel != shownLevel)
            {
                int before = shownLevel;
                shownLevel = level.CurrentLevel;
                if (shownLevel > before && before > 0) Play(before);
            }
            if (timer < 0f) return;
            timer += Time.unscaledDeltaTime;
            Animate(timer);
            if (timer >= Seconds)
            {
                timer = -1f;
                group.alpha = 0f;
            }
        }

        /// デバッグ用にも使える。before は上がる前の Lv。
        public void Play(int before)
        {
            flipped = false;
            number.text = before.ToString();
            timer = 0f;
            Animate(0f);
            // 足元の光は目の高さより低くする（カメラが光の中に入ると画面全体が色づくため）。
            if (hero != null)
            {
                Vector3 feet = hero.transform.position;
                Game.Skill.LightPillar.Spawn(feet, 1.1f, 1.1f, 0.6f, FootGlow, true);
                Game.Skill.ShockRing.Spawn(feet + Vector3.up * 0.1f, 0.5f, 3.5f, 0.5f, 0.25f, FootGlow);
            }
        }

        private void Animate(float t)
        {
            // 消えぎわは全体が少し上へ抜けながら薄れる。
            float outK = Mathf.Clamp01((t - (Seconds - OutSeconds)) / OutSeconds);
            group.alpha = Mathf.Clamp01(t / 0.08f) * (1f - outK);
            root.anchoredPosition = new Vector2(0f, 190f + 40f * outK * outK);

            // バッジ: ぽんと膨らんで戻る。
            badge.localScale = Vector3.one * Overshoot(Mathf.Clamp01(t / 0.3f));
            badge.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-18f, 0f, EaseOut(Mathf.Clamp01(t / 0.3f))));

            // 数字: 0.38秒から横につぶれてめくれ、新しい Lv になって弾む。
            float flip = Mathf.Clamp01((t - 0.38f) / 0.26f);
            if (flip >= 0.5f && !flipped)
            {
                flipped = true;
                number.text = shownLevel.ToString();
            }
            float flipWidth = Mathf.Abs(Mathf.Cos(flip * Mathf.PI));
            float punch = flip >= 0.5f ? 1f + 0.35f * Mathf.Sin(Mathf.Clamp01((t - 0.51f) / 0.3f) * Mathf.PI) : 1f;
            numberNode.localScale = new Vector3(Mathf.Max(0.02f, flipWidth) * punch, punch, 1f);

            // 光の筋: バッジのあとに広がって、ゆっくり回る。
            float rayK = EaseOut(Mathf.Clamp01((t - 0.1f) / 0.35f));
            rays.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, rayK);
            rays.localEulerAngles = new Vector3(0f, 0f, t * 28f);
            rayGroup.alpha = rayK * (0.85f + 0.15f * Mathf.Sin(t * 7f));

            // リボン: 真ん中から左右へ広がる。
            float ribbonK = Mathf.Clamp01((t - 0.18f) / 0.26f);
            ribbon.localScale = new Vector3(Overshoot(ribbonK), Mathf.Clamp01(ribbonK * 3f), 1f);

            // 札: 上から落ちてきて少し揺れる。
            float tagK = Mathf.Clamp01((t - 0.62f) / 0.28f);
            pointTag.localScale = Vector3.one * Overshoot(tagK);
            pointTag.anchoredPosition = new Vector2(0f, -122f + 26f * (1f - EaseOut(tagK)));
            pointTag.localEulerAngles = new Vector3(0f, 0f, -2f + 5f * Mathf.Sin(tagK * Mathf.PI * 2f) * (1f - tagK));

            // きらめき: めくれた瞬間に外へ飛んで、小さくなって消える。
            float sparkK = Mathf.Clamp01((t - 0.5f) / 0.7f);
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = (i * 360f / sparks.Length + 20f) * Mathf.Deg2Rad;
                float distance = Mathf.Lerp(60f, i % 2 == 0 ? 170f : 130f, EaseOut(sparkK));
                sparks[i].anchoredPosition = new Vector2(Mathf.Cos(angle) * distance, 20f + Mathf.Sin(angle) * distance);
                sparks[i].localScale = Vector3.one * (sparkK <= 0f ? 0f : Mathf.Sin(sparkK * Mathf.PI));
                sparks[i].localEulerAngles = new Vector3(0f, 0f, 45f + sparkK * 120f);
            }
        }

        private static float EaseOut(float k)
        {
            return 1f - (1f - k) * (1f - k);
        }

        // 0 → 1 を少し行きすぎてから戻る。
        private static float Overshoot(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            float s = k - 1f;
            return 1f + 2.7f * s * s * s + 1.7f * s * s;
        }
    }
}
