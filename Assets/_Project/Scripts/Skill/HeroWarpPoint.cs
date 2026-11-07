using UnityEngine;
using Game.Core;
using Game.Hero;

namespace Game.Skill
{
    /// 魔法使いのワープ（WarpPoint）の、勇者1人ぶんの状態。
    /// 1回目: 目の前に門を1つ開く（MP・クールタイムなし）。つながる前の門は warpGatePendingSeconds で閉じる。
    /// 2回目: 別の場所でもう1つ開き、2つをつなぐ（ここで MP とクールタイムを使う）。前につないだ2つは閉じる。
    /// つながった門は warpGateLinkedSeconds のあいだ開いていて、勇者が通り抜けられる（WarpGate）。
    public class HeroWarpPoint : MonoBehaviour
    {
        public WarpGate PendingGate { get; private set; }
        public WarpGate LinkedGate { get; private set; }

        public bool HasPending => PendingGate != null && !PendingGate.IsClosing && !PendingGate.IsLinked;

        /// 1つ目の門を開く。
        public bool OpenFirst()
        {
            if (HasPending) PendingGate.Close();
            if (!TryGatePosition(out var pos, out var forward)) return false;
            PendingGate = WarpGate.Create(pos, forward, BalanceConfig.Instance.warpGatePendingSeconds);
            Notify("ワープの門を開いた（別の場所でもう一度使うとつながる）");
            Debug.Log($"[Warp] {name} opened the first gate at {pos}");
            return true;
        }

        /// 2つ目の門を開いて、1つ目とつなぐ。離れていなければ開かない。
        public bool OpenSecond(bool ignoreDistance = false)
        {
            if (!HasPending) return false;
            var cfg = BalanceConfig.Instance;
            if (!TryGatePosition(out var pos, out var forward)) return false;
            float dist = Vector3.Distance(pos, PendingGate.transform.position);
            if (!ignoreDistance && dist < cfg.warpGateMinLinkDistance)
            {
                Notify($"門どうしが近すぎる（{cfg.warpGateMinLinkDistance:0}m 以上離す）");
                return false;
            }
            if (LinkedGate != null) LinkedGate.Close();
            var second = WarpGate.Create(pos, forward, cfg.warpGateLinkedSeconds);
            WarpGate.Link(PendingGate, second, cfg.warpGateLinkedSeconds);
            LinkedGate = second;
            PendingGate = null;
            Notify("ワープの門がつながった");
            Debug.Log($"[Warp] {name} linked gates ({dist:0.0}m apart)");
            return true;
        }

        public void CloseAll()
        {
            if (PendingGate != null) PendingGate.Close();
            if (LinkedGate != null) LinkedGate.Close();
            PendingGate = null;
            LinkedGate = null;
        }

        // 目の前の地面。表は自分の方を向く。
        private bool TryGatePosition(out Vector3 pos, out Vector3 forward)
        {
            Vector3 dir = transform.forward;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            forward = -dir;
            pos = transform.position + dir * BalanceConfig.Instance.warpGateDistance;
            // 足元の高さから下を探す（屋根や木の枝に乗らないように、上からは探さない）。
            if (Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            return true;
        }

        private void Notify(string msg)
        {
            // 自分の勇者だけ画面に出す。BOT はログだけ。
            if (GetComponent<HumanHeroMarker>() == null) return;
            if (Game.UI.PowerItemNotifierUI.Instance != null) Game.UI.PowerItemNotifierUI.Instance.ShowNotification(msg);
        }
    }
}
