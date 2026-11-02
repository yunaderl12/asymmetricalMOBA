using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.DemonKing;
using Game.Hero;

namespace Game.UI
{
    /// 自分（人間のプレイヤー）が与えたダメージを、相手の頭上にぴょんと跳ねる数字で見せる。
    public class DamageNumberDisplay : MonoBehaviour
    {
        private const int PoolSize = 32;
        private const int BaseFontSize = 34;
        private const int BigFontSize = 48;
        private const float BigHitRatio = 0.25f; // 最大HPのこの割合以上は大きく出す
        private const float Life = 0.8f;
        private const float FadeStart = 0.55f;
        private const float JumpSpeed = 520f;
        private const float Gravity = 1250f;
        private const float DriftRange = 90f;
        private const float PopSeconds = 0.14f;
        private const float PopScale = 1.45f;
        private const float SpawnScatter = 26f;
        private const float HeadMargin = 0.35f;
        private const float DefaultHeight = 2.2f;

        private static readonly Color NormalColor = new Color(1f, 0.93f, 0.62f);
        private static readonly Color BigColor = new Color(1f, 0.58f, 0.22f);
        private static readonly Color EdgeColor = new Color(0.12f, 0.05f, 0.02f, 0.95f);

        private class Number
        {
            public Text text;
            public RectTransform rect;
            public Vector3 worldPoint;
            public Vector2 offset;
            public float drift;
            public float age;
            public bool active;
        }

        private readonly List<Number> numbers = new List<Number>();
        private Canvas canvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("DamageNumberDisplay");
            DontDestroyOnLoad(go);
            go.AddComponent<DamageNumberDisplay>();
        }

        private void Awake()
        {
            // ロビーは開くときに既存のキャンバスをすべて隠すので、本体とは別にして表示前に戻す。
            var canvasObject = new GameObject("DamageNumberCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < PoolSize; i++) numbers.Add(CreateNumber(font));
        }

        private void OnEnable()
        {
            HealthComponent.AnyDamage += HandleDamage;
        }

        private void OnDisable()
        {
            HealthComponent.AnyDamage -= HandleDamage;
        }

        private Number CreateNumber(Font font)
        {
            var go = new GameObject("Number", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = EdgeColor;
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(200f, 60f);
            go.SetActive(false);
            return new Number { text = text, rect = rect };
        }

        private void HandleDamage(HealthComponent target, int amount, GameObject attacker)
        {
            if (attacker == null || amount <= 0 || !IsLocalPlayer(attacker)) return;

            if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
            var number = TakeNumber();
            bool big = target.MaxHP > 0 && amount >= target.MaxHP * BigHitRatio;
            number.text.text = amount.ToString();
            number.text.fontSize = big ? BigFontSize : BaseFontSize;
            number.text.color = big ? BigColor : NormalColor;
            number.worldPoint = HeadPoint(target);
            number.offset = new Vector2(Random.Range(-SpawnScatter, SpawnScatter), Random.Range(-SpawnScatter, SpawnScatter) * 0.4f);
            number.drift = Random.Range(-DriftRange, DriftRange);
            number.age = 0f;
            number.active = true;
            number.rect.gameObject.SetActive(true);
            number.rect.SetAsLastSibling();
            Place(number);
        }

        private static bool IsLocalPlayer(GameObject attacker)
        {
            return attacker.GetComponent<HumanHeroMarker>() != null || attacker.GetComponent<DemonKingController>() != null;
        }

        // 空きがなければ一番古い数字を使い回す。
        private Number TakeNumber()
        {
            Number oldest = numbers[0];
            foreach (var n in numbers)
            {
                if (!n.active) return n;
                if (n.age > oldest.age) oldest = n;
            }
            return oldest;
        }

        // HPバーがあればその少し上、なければ当たり判定の上。
        private static Vector3 HeadPoint(HealthComponent target)
        {
            var bar = target.GetComponentInChildren<HealthBarUI>();
            if (bar != null) return bar.transform.position + Vector3.up * HeadMargin;
            float top = target.transform.position.y + DefaultHeight;
            foreach (var c in target.GetComponentsInChildren<Collider>())
            {
                if (c.enabled && !c.isTrigger) top = Mathf.Max(top, c.bounds.max.y);
            }
            var p = target.transform.position;
            return new Vector3(p.x, top + HeadMargin, p.z);
        }

        private void LateUpdate()
        {
            foreach (var n in numbers)
            {
                if (!n.active) continue;
                n.age += Time.deltaTime;
                if (n.age >= Life)
                {
                    n.active = false;
                    n.rect.gameObject.SetActive(false);
                    continue;
                }
                Place(n);
            }
        }

        private void Place(Number n)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(n.worldPoint);
            bool visible = screen.z > 0f;
            if (n.text.enabled != visible) n.text.enabled = visible;
            if (!visible) return;

            // 地点は世界に固定し、画面上で跳ねて落ちる。
            float scale = canvas.scaleFactor;
            float rise = JumpSpeed * n.age - 0.5f * Gravity * n.age * n.age;
            n.rect.position = new Vector3(
                screen.x + (n.offset.x + n.drift * n.age) * scale,
                screen.y + (n.offset.y + rise) * scale,
                0f);

            float pop = n.age < PopSeconds ? Mathf.Lerp(0.5f, PopScale, n.age / PopSeconds)
                : Mathf.Lerp(PopScale, 1f, Mathf.Clamp01((n.age - PopSeconds) / PopSeconds));
            n.rect.localScale = Vector3.one * pop;

            var color = n.text.color;
            color.a = 1f - Mathf.Clamp01((n.age - FadeStart) / (Life - FadeStart));
            n.text.color = color;
        }
    }
}
