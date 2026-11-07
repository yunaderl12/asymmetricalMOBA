using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.UI
{
    /// 試合が決まったときの演出。時間はゲームの時間ではなく実時間で進める。
    ///  勇者の勝ち: 最後の一撃のカットイン（スローモーション、斜めの帯に倒した勇者の顔と名前）→ 魔王（またはコア）が闇の光になって消える → 勇者チームの勝利の画面。
    ///  魔王の勝ち: はじまりのまちのコアが砕けるカットイン → コアが闇に呑まれて消える → 魔王軍の勝利の画面。
    ///  時間切れ: 「時間切れ」の帯 → まちのコアが闇に包まれる → 魔王軍の勝利の画面。
    /// 最後に onDone を呼ぶ（リザルトへ移る）。
    public class MatchEndDirector : MonoBehaviour
    {
        private const float CutInSeconds = 1.9f;
        private const float CutInTimeScale = 0.12f;
        private const float VanishSeconds = 3.0f;
        private const float VictoryMinSeconds = 2.0f;
        private const float VictoryMaxSeconds = 7.0f;

        public static bool Playing { get; private set; }

        private Camera cutCam;
        private Canvas overlay;
        private Image flash;

        public static void Play(MatchManager.Winner winner, MatchManager.EndCause cause, Transform focus, GameObject finisher, Action onDone)
        {
            var go = new GameObject("MatchEndDirector");
            var d = go.AddComponent<MatchEndDirector>();
            d.StartCoroutine(d.Run(winner, cause, focus, finisher, onDone));
        }

        private IEnumerator Run(MatchManager.Winner winner, MatchManager.EndCause cause, Transform focus, GameObject finisher, Action onDone)
        {
            Playing = true;
            bool heroes = winner == MatchManager.Winner.Heroes;
            BuildOverlay();
            Vector3 center = focus != null ? FocusPoint(focus) : Vector3.zero;
            if (focus != null) BuildCamera(focus, center, cause);

            // 1) カットイン（スロー）
            Time.timeScale = cause == MatchManager.EndCause.TimeUp ? 1f : CutInTimeScale;
            string caption, sub;
            Color band;
            switch (cause)
            {
                case MatchManager.EndCause.DemonKingDefeated: caption = "最後の一撃！"; sub = "魔王を討ち取った"; band = HudStyle.HeroPalette.Felt; break;
                case MatchManager.EndCause.CastleCoreDestroyed: caption = "最後の一撃！"; sub = "魔王城のコアを打ち砕いた"; band = HudStyle.HeroPalette.Felt; break;
                case MatchManager.EndCause.TownCoreDestroyed: caption = "はじまりのまち 陥落"; sub = "まちのコアが破壊された"; band = HudStyle.DemonPalette.Felt; break;
                default: caption = "時間切れ"; sub = "勇者たちは間に合わなかった"; band = HudStyle.DemonPalette.Felt; break;
            }
            StartCoroutine(Flash(heroes ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.6f, 0f, 0.05f, 0.7f), 0.35f));
            var cutIn = BuildCutIn(caption, sub, band, heroes, finisher);
            yield return SlideCutIn(cutIn, CutInSeconds);
            Destroy(cutIn.gameObject);

            // 2) 消滅
            Time.timeScale = 1f;
            if (focus != null) yield return Vanish(focus, center, heroes, cause);
            else yield return new WaitForSecondsRealtime(0.8f);

            // 3) 勝敗の画面
            Time.timeScale = 0f;
            var screen = VictoryScreen.Create(winner, cause, 130);
            var hint = HudStyle.Label(screen.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(900f, 50f),
                "", 30, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, 2f, new Color(0f, 0f, 0f, 0.6f));
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            float shown = Time.unscaledTime;
            while (true)
            {
                float t = Time.unscaledTime - shown;
                if (t >= VictoryMinSeconds)
                {
                    hint.text = "クリックで結果へ";
                    hint.color = new Color(1f, 1f, 1f, 0.6f + 0.3f * Mathf.Sin(t * 4f));
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    var keys = UnityEngine.InputSystem.Keyboard.current;
                    if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || (keys != null && keys.anyKey.wasPressedThisFrame)) break;
                }
                if (t >= VictoryMaxSeconds) break;
                yield return null;
            }
            Playing = false;
            onDone?.Invoke();
        }

        private static Vector3 FocusPoint(Transform focus)
        {
            var b = BoundsOf(focus);
            return b.size.sqrMagnitude > 0.01f ? b.center : focus.position + Vector3.up * 1.2f;
        }

        private static Bounds BoundsOf(Transform focus)
        {
            Bounds b = default;
            bool any = false;
            foreach (var r in focus.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ---- カメラ

        private void BuildCamera(Transform focus, Vector3 center, MatchManager.EndCause cause)
        {
            var main = Camera.main;
            var go = new GameObject("MatchEndCamera");
            cutCam = go.AddComponent<Camera>();
            if (main != null)
            {
                cutCam.CopyFrom(main);
                cutCam.targetTexture = null;
            }
            cutCam.depth = 90f;
            cutCam.fieldOfView = 45f;
            cutCam.nearClipPlane = 0.1f;
            cutCam.farClipPlane = Mathf.Max(cutCam.farClipPlane, 600f);
            cutCam.cullingMask = ~0;
            float size = Mathf.Max(2f, BoundsOf(focus).extents.magnitude);
            Vector3 front = Flat(focus.forward);
            if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
            // 正面やや横の少し上から。
            Vector3 dir = (Quaternion.AngleAxis(28f, Vector3.up) * front).normalized;
            float dist = size * 2.6f + 3f;
            go.transform.position = center + dir * dist + Vector3.up * (size * 0.9f + 1.5f);
            go.transform.LookAt(center);
            StartCoroutine(Orbit(go.transform, center, dir, dist, size));
        }

        private IEnumerator Orbit(Transform cam, Vector3 center, Vector3 dir, float dist, float size)
        {
            float start = Time.unscaledTime;
            while (cam != null)
            {
                float t = Time.unscaledTime - start;
                Vector3 d = Quaternion.AngleAxis(t * 9f, Vector3.up) * dir;
                float k = 1f + 0.25f * Mathf.Clamp01(t / 5f);
                cam.position = center + d * dist * k + Vector3.up * (size * 0.9f + 1.5f) * k;
                cam.LookAt(center);
                yield return null;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---- 消滅

        private IEnumerator Vanish(Transform focus, Vector3 center, bool heroes, MatchManager.EndCause cause)
        {
            Color dark = new Color(0.35f, 0.08f, 0.55f, 1f);
            Color red = new Color(0.75f, 0.05f, 0.1f, 1f);
            float size = Mathf.Max(1.5f, BoundsOf(focus).extents.magnitude);
            bool keep = cause == MatchManager.EndCause.TimeUp;
            float start = Time.unscaledTime;
            Vector3 ground = new Vector3(center.x, focus.position.y, center.z);
            ShockRing.Spawn(ground + Vector3.up * 0.2f, 0.5f, size * 3f, 0.6f, 1.1f, heroes ? new Color(1f, 0.9f, 0.6f, 1f) : red);
            LightPillar.Spawn(ground, size * 0.8f, size * 6f, VanishSeconds, heroes ? dark : red, true);
            yield return new WaitForSecondsRealtime(0.7f);
            DeathBurst.Spawn(center, 120);
            ShockRing.Spawn(ground + Vector3.up * 0.3f, 0.5f, size * 5f, 0.8f, 1f, heroes ? dark : new Color(0.2f, 0f, 0.05f, 1f));
            StartCoroutine(Flash(heroes ? new Color(1f, 1f, 1f, 0.6f) : new Color(0.2f, 0f, 0.04f, 0.6f), 0.5f));
            if (!keep) StartCoroutine(FadeOut(focus, 0.8f));
            while (Time.unscaledTime - start < VanishSeconds) yield return null;
        }

        // 縮みながら消える（魔王やコアの見た目だけ。当たり判定は試合が終わっているので関係ない）。
        private IEnumerator FadeOut(Transform focus, float seconds)
        {
            Vector3 scale = focus.localScale;
            float start = Time.unscaledTime;
            while (focus != null && Time.unscaledTime - start < seconds)
            {
                float k = (Time.unscaledTime - start) / seconds;
                focus.localScale = new Vector3(scale.x * (1f - k * 0.9f), scale.y * (1f + k * 0.4f), scale.z * (1f - k * 0.9f));
                yield return null;
            }
            if (focus == null) yield break;
            foreach (var r in focus.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in focus.GetComponentsInChildren<Canvas>()) c.enabled = false;
            focus.localScale = scale;
        }

        // ---- 画面の上の絵

        private void BuildOverlay()
        {
            var go = new GameObject("MatchEndOverlay", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            overlay = go.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 125;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var f = HudStyle.Filled(go.transform, "Flash");
            flash = HudStyle.AddGraphic<Image>(f);
            flash.color = Color.clear;
            flash.raycastTarget = false;
        }

        private IEnumerator Flash(Color color, float seconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < seconds)
            {
                float k = (Time.unscaledTime - start) / seconds;
                flash.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
                yield return null;
            }
            flash.color = Color.clear;
        }

        // 斜めの帯。左に倒した者の顔、右に大きな文字。流れる線で勢いを出す。
        private RectTransform BuildCutIn(string caption, string sub, Color bandColor, bool heroes, GameObject finisher)
        {
            var p = heroes ? HudStyle.HeroPalette : HudStyle.DemonPalette;
            var root = HudStyle.Place(overlay.transform, "CutIn", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2800f, 300f));
            root.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var shadow = HudStyle.Shape(root, "Shadow", HudShape.Kind.RoundRect, new Color(0f, 0f, 0f, 0.45f), 0f);
            shadow.rectTransform.offsetMin = new Vector2(0f, -16f);
            shadow.rectTransform.offsetMax = new Vector2(0f, -16f);
            HudStyle.Shape(root, "Band", HudShape.Kind.RoundRect, bandColor, 0f, HudStyle.FeltTexture);
            var edge = heroes ? new Color(1f, 0.84f, 0.42f, 1f) : p.Stitch;
            var top = HudStyle.Place(root, "EdgeTop", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(2800f, 8f));
            HudStyle.AddGraphic<Image>(top).color = edge;
            var bottom = HudStyle.Place(root, "EdgeBottom", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(2800f, 8f));
            HudStyle.AddGraphic<Image>(bottom).color = edge;

            // 流れる線
            var lines = HudStyle.Filled(root, "Lines");
            for (int i = 0; i < 14; i++)
            {
                var l = HudStyle.Place(lines, "Line", new Vector2(UnityEngine.Random.value, UnityEngine.Random.Range(0.12f, 0.88f)), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(UnityEngine.Random.Range(180f, 520f), UnityEngine.Random.Range(3f, 7f)));
                var img = HudStyle.AddGraphic<Image>(l);
                img.color = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.08f, 0.25f));
                img.raycastTarget = false;
            }

            // 顔
            float x = -560f;
            var face = HudStyle.Place(root, "Face", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(230f, 230f));
            var cls = finisher != null ? HudStyle.ClassOf(finisher) : null;
            Color fallback = cls != null ? cls.avatarColor : heroes ? HudStyle.HeroPalette.Ribbon : HudStyle.DemonPalette.Ribbon;
            HudStyle.Portrait(face, cls != null ? cls.portrait : null, fallback, 8f, new Color(0f, 0f, 0f, 0.5f));
            if (cls == null)
                HudStyle.AddText(HudStyle.Filled(face, "Glyph"), heroes ? "勇" : "魔", 110, Color.white, TextAnchor.MiddleCenter, 4f, p.Outline);
            string who = FinisherLabel(finisher, heroes);
            HudStyle.Label(root, "Who", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 150f, 78f), new Vector2(900f, 60f),
                who, 36, edge, TextAnchor.MiddleLeft, 3f, p.Outline);
            HudStyle.Label(root, "Caption", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 140f, -10f), new Vector2(1300f, 130f),
                caption, 104, Color.white, TextAnchor.MiddleLeft, 6f, p.Outline);
            HudStyle.Label(root, "Sub", new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 150f, -96f), new Vector2(1100f, 50f),
                sub, 32, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleLeft, 2f, p.Outline);
            return root;
        }

        private static string FinisherLabel(GameObject finisher, bool heroes)
        {
            if (finisher == null) return heroes ? "勇者チーム" : "魔王軍";
            var cls = HudStyle.ClassOf(finisher);
            if (cls != null) return HudStyle.HeroName(finisher) + "（" + HudStyle.ClassLabel(cls.className) + "）";
            var monster = finisher.GetComponentInParent<Game.Monster.Monster>();
            if (monster != null && monster.Data != null) return "魔王軍 " + monster.Data.monsterName;
            if (finisher.CompareTag("DemonKing")) return "魔王";
            return heroes ? "勇者チーム" : "魔王軍";
        }

        private IEnumerator SlideCutIn(RectTransform root, float seconds)
        {
            float start = Time.unscaledTime;
            var lines = root.Find("Lines");
            while (true)
            {
                float t = Time.unscaledTime - start;
                if (t >= seconds) break;
                float inK = Mathf.Clamp01(t / 0.22f);
                float outK = Mathf.Clamp01((t - (seconds - 0.25f)) / 0.25f);
                float x = Mathf.Lerp(2400f, 0f, 1f - Mathf.Pow(1f - inK, 3f)) - 2600f * outK * outK;
                // ゆっくり流れ続ける
                x -= t * 40f;
                root.anchoredPosition = new Vector2(x, 0f);
                root.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, inK) * (1f - outK * 0.6f), 1f);
                if (lines != null)
                    foreach (RectTransform l in lines)
                    {
                        var pos = l.anchoredPosition;
                        pos.x -= 2600f * Time.unscaledDeltaTime;
                        if (pos.x < -1600f) pos.x += 3200f;
                        l.anchoredPosition = pos;
                    }
                yield return null;
            }
        }

        private void OnDestroy()
        {
            Playing = false;
            if (cutCam != null) Destroy(cutCam.gameObject);
        }
    }
}
