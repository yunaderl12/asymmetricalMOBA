using UnityEngine;

namespace Game.Skill
{
    /// 着弾の瞬間にゲームを数コマ止める。試合終了などの timeScale 変更を上書きしない。
    /// Time.timeScale はホストのゲーム全体に効くので、Phase 8 ではクライアントの見た目だけの処理に置き換える。
    public class HitStop : MonoBehaviour
    {
        private const float StopScale = 0.02f;

        private static HitStop instance;
        private float until;
        private bool active;

        public static void Play(float seconds)
        {
            if (seconds <= 0f) return;
            if (instance == null)
            {
                var go = new GameObject("HitStop");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStop>();
            }
            if (!instance.active)
            {
                if (!Mathf.Approximately(Time.timeScale, 1f)) return;
                instance.active = true;
                Time.timeScale = StopScale;
            }
            instance.until = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (!active || Time.unscaledTime < until) return;
            active = false;
            if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
        }
    }
}
