using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// 試合中、画面の上のほうに数秒だけ出すお知らせのリボン（「結界が解けた」など）。
    /// 同じ文が出ている間にもう一度呼ばれたら、表示時間だけ延ばす。ゲームの時間が止まっていても動く。
    public class MatchNotice : MonoBehaviour
    {
        private const float FadeSeconds = 0.25f;

        private static MatchNotice instance;

        private CanvasGroup group;
        private Text text;
        private HudShape ribbon;
        private float hideAt;
        private float alpha;

        public static void Show(string message, float seconds = 3f, bool alert = false)
        {
            if (instance == null) Create();
            instance.text.text = message;
            var p = HudStyle.For(HudStyle.CurrentTheme);
            instance.ribbon.color = alert ? HudStyle.Danger : p.Felt;
            var rt = (RectTransform)instance.transform;
            float width = Mathf.Clamp(instance.text.preferredWidth + 140f, 420f, 1500f);
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            instance.hideAt = Time.unscaledTime + seconds;
            instance.gameObject.SetActive(true);
        }

        private static void Create()
        {
            var p = HudStyle.For(HudStyle.CurrentTheme);
            var rt = HudStyle.Place(HudStyle.Canvas.transform, "MatchNotice", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 74f));
            instance = rt.gameObject.AddComponent<MatchNotice>();
            instance.group = rt.gameObject.AddComponent<CanvasGroup>();
            instance.group.blocksRaycasts = false;
            instance.group.interactable = false;
            HudStyle.DropShadow(rt, HudShape.Kind.Ribbon, p.Shadow, 0f, 6f);
            instance.ribbon = HudStyle.Shape(rt, "Ribbon", HudShape.Kind.Ribbon, p.Felt, 0f, HudStyle.FeltTexture);
            instance.ribbon.notch = 26f;
            HudStyle.Stitch(rt, p.Stitch, false, 0f, 8f);
            instance.text = HudStyle.AddText(HudStyle.Filled(rt, "Text"), "", 32, p.Light, TextAnchor.MiddleCenter, 3f, p.Outline);
            instance.group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            bool visible = Time.unscaledTime < hideAt;
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            group.alpha = alpha;
            transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, alpha);
            if (!visible && alpha <= 0f) gameObject.SetActive(false);
        }
    }
}
