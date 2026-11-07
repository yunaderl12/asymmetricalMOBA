using UnityEngine;
using Game.Core;

namespace Game.Hero
{
    /// 自分の勇者の外からの視点。RespawnManager が呼ぶ。
    /// - Begin / End: 死んでからリスポーンするまで。目の高さから上へ引いて、倒れた自分を斜め上から見下ろす。
    /// - BeginRespawn: リスポーンのアニメーションのあいだ。前から後ろへ回り込みながら斜め上から映し、終わりぎわに目の高さへ戻る。
    /// どちらのあいだも自分のモデル（LocalAvatar レイヤー）をカメラに映す。
    public class HeroDeathCamera : MonoBehaviour
    {
        private enum Mode { Death, Respawn }

        private Camera cam;
        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;
        private int savedMask;
        private bool active;
        private bool pending;
        private Mode mode;
        private Mode pendingMode;
        private float startTime;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private Vector3 focus;
        private Vector3 backDir;
        private CharacterModelAnimator avatar;
        private bool returning;
        private float returnStart;
        private Vector3 returnFromPosition;
        private Quaternion returnFromRotation;
        private float swingSide = 1f;
        private bool firstRespawnFrame;
        private Vector3 smoothedPosition;
        private Vector3 smoothVelocity;
        private Game.UI.HealthBarUI ownHealthBar;
        private float deathAngle;          // 倒れた位置から見下ろす向き（橋の下などで見通せる向きを選ぶ）
        private const string InvisibleWallName = "Field_Barrier";
        private static readonly float[] DeathTryAngles = { 0f, 60f, -60f, 120f, -120f, 180f };

        private const float RespawnLookHeight = 1.2f;
        private const float RespawnFollowSeconds = 0.25f;
        private const float ViewRadius = 0.3f;
        private const int SwingSamples = 6;
        // 見通しが悪いときに試す高さと水平距離の倍率（順に試す）。
        private static readonly float[] RespawnRaise = { 1f, 1.6f, 2.4f, 3.4f };
        private static readonly float[] RespawnPull = { 1f, 0.85f, 0.7f, 0.55f };

        public bool IsActive => active;

        public void Begin()
        {
            Open(Mode.Death);
        }

        /// リスポーンのアニメーションが流れているあいだ、斜め上から自分を映す。アニメーションが終われば自分で目の高さへ戻る。
        public void BeginRespawn()
        {
            Open(Mode.Respawn);
        }

        private void Open(Mode wanted)
        {
            if (active) End();
            cam = FindHeroCamera();
            // 試合が始まった直後などでカメラがまだ見つからないときは、見つかるまで毎フレーム試す。
            if (cam == null) { pending = true; pendingMode = wanted; return; }
            pending = false;
            active = true;
            mode = wanted;
            returning = false;
            startTime = Time.time;
            savedLocalPosition = cam.transform.localPosition;
            savedLocalRotation = cam.transform.localRotation;
            savedMask = cam.cullingMask;
            int layer = LayerMask.NameToLayer("LocalAvatar");
            if (layer >= 0) cam.cullingMask |= 1 << layer;
            fromPosition = cam.transform.position;
            fromRotation = cam.transform.rotation;
            focus = transform.position + Vector3.up * 0.8f;
            backDir = -transform.forward;
            backDir.y = 0f;
            backDir = backDir.sqrMagnitude > 0.001f ? backDir.normalized : Vector3.back;
            if (avatar == null) avatar = GetComponent<CharacterModelAnimator>();
            if (wanted == Mode.Death) deathAngle = PickDeathAngle();
            if (wanted == Mode.Respawn)
            {
                swingSide = PickSwingSide();
                firstRespawnFrame = true;
                smoothVelocity = Vector3.zero;
            }
            HideOwnHealthBar(true);
        }

        public void End()
        {
            pending = false;
            if (!active) return;
            active = false;
            HideOwnHealthBar(false);
            if (cam == null) return;
            cam.transform.localPosition = savedLocalPosition;
            cam.transform.localRotation = savedLocalRotation;
            cam.cullingMask = savedMask;
        }

        private void LateUpdate()
        {
            if (pending) Open(pendingMode);
            if (!active || cam == null) return;
            if (mode == Mode.Death) UpdateDeath();
            else UpdateRespawn();
        }

        private void UpdateDeath()
        {
            var cfg = BalanceConfig.Instance;
            float elapsed = Time.time - startTime;
            float ease = Ease(elapsed / Mathf.Max(0.05f, cfg.deathCamBlendSeconds));
            Vector3 dir = Quaternion.Euler(0f, deathAngle + elapsed * cfg.deathCamOrbitSpeed, 0f) * backDir;
            Vector3 wanted = KeepOutOfWalls(focus, focus + dir * cfg.deathCamBack + Vector3.up * cfg.deathCamHeight);
            Quaternion look = Quaternion.LookRotation(focus - wanted, Vector3.up);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, wanted, ease), Quaternion.Slerp(fromRotation, look, ease));
        }

        // 前から斜め上に構え、アニメーションに合わせて背中側へ回り込む。最後は目の高さへすっと戻る。
        private void UpdateRespawn()
        {
            var cfg = BalanceConfig.Instance;
            bool playing = avatar != null && avatar.IsRespawnAnimPlaying;
            float total = avatar != null ? Mathf.Max(0.1f, avatar.RespawnAnimSeconds) : 0.1f;
            float remaining = avatar != null ? avatar.RespawnAnimRemaining : 0f;
            float returnSeconds = Mathf.Max(0.05f, cfg.respawnCamReturnSeconds);

            Vector3 target = transform.position + Vector3.up * RespawnLookHeight;
            float swing = Ease(1f - remaining / total);
            Vector3 wanted = RespawnView(target, swingSide * cfg.respawnCamStartAngle * (1f - swing));
            // 建物を避けて高さを変えたときに飛ばないよう、なめらかに追う。
            if (firstRespawnFrame) { smoothedPosition = wanted; firstRespawnFrame = false; }
            smoothedPosition = Vector3.SmoothDamp(smoothedPosition, wanted, ref smoothVelocity, RespawnFollowSeconds);
            Quaternion look = Quaternion.LookRotation(target - smoothedPosition, Vector3.up);

            if (!returning && (!playing || remaining <= returnSeconds))
            {
                returning = true;
                returnStart = Time.time;
                returnFromPosition = smoothedPosition;
                returnFromRotation = look;
            }
            if (!returning)
            {
                cam.transform.SetPositionAndRotation(smoothedPosition, look);
                return;
            }

            float t = (Time.time - returnStart) / returnSeconds;
            Transform parent = cam.transform.parent;
            Vector3 eyePosition = parent != null ? parent.TransformPoint(savedLocalPosition) : savedLocalPosition;
            Quaternion eyeRotation = parent != null ? parent.rotation * savedLocalRotation : savedLocalRotation;
            float ease = Ease(t);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(returnFromPosition, eyePosition, ease), Quaternion.Slerp(returnFromRotation, eyeRotation, ease));
            if (t >= 1f) End();
        }

        // angle の向き（0 = 背中側）から自分を見下ろす位置。間に建物などがあれば、近づくのではなく高く上がって見通す。
        private Vector3 RespawnView(Vector3 target, float angle)
        {
            var cfg = BalanceConfig.Instance;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * backDir;
            for (int i = 0; i < RespawnRaise.Length; i++)
            {
                Vector3 candidate = target + dir * cfg.respawnCamDistance * RespawnPull[i] + Vector3.up * cfg.respawnCamHeight * RespawnRaise[i];
                if (IsClear(target, candidate)) return candidate;
            }
            return KeepOutOfWalls(target, target + dir * cfg.respawnCamDistance + Vector3.up * cfg.respawnCamHeight);
        }

        // 倒れた自分を見下ろす向き。背中側から順に試し、間に橋や崖がない向きを選ぶ（橋から落ちたときに橋の床で隠れないように）。
        private float PickDeathAngle()
        {
            var cfg = BalanceConfig.Instance;
            foreach (float angle in DeathTryAngles)
            {
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * backDir;
                if (IsClear(focus, focus + dir * cfg.deathCamBack + Vector3.up * cfg.deathCamHeight)) return angle;
            }
            return 0f;
        }

        // 前から背中へ回り込むとき、右回りと左回りのうち見通しのよい方を選ぶ。
        private float PickSwingSide()
        {
            var cfg = BalanceConfig.Instance;
            Vector3 target = transform.position + Vector3.up * RespawnLookHeight;
            int[] blocked = new int[2];
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                for (int k = 0; k <= SwingSamples; k++)
                {
                    Vector3 dir = Quaternion.Euler(0f, sign * cfg.respawnCamStartAngle * k / SwingSamples, 0f) * backDir;
                    if (!IsClear(target, target + dir * cfg.respawnCamDistance + Vector3.up * cfg.respawnCamHeight)) blocked[side]++;
                }
            }
            return blocked[1] < blocked[0] ? -1f : 1f;
        }

        // 自分と勇者・魔物の体は見通しの邪魔にしない。
        private bool IsClear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            foreach (var hit in Physics.SphereCastAll(from, ViewRadius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.transform;
                if (t == transform || t.IsChildOf(transform) || hit.collider.CompareTag("Player") || hit.collider.name == InvisibleWallName) continue;
                if (hit.collider.GetComponentInParent<CharacterController>() != null || hit.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
                return false;
            }
            return true;
        }

        // 外から映しているあいだは、自分の頭上の HP バーを隠す（カメラの目の前に大きく映ってしまう）。
        private void HideOwnHealthBar(bool hide)
        {
            if (hide)
            {
                ownHealthBar = GetComponentInChildren<Game.UI.HealthBarUI>(false);
                if (ownHealthBar != null) ownHealthBar.gameObject.SetActive(false);
            }
            else if (ownHealthBar != null)
            {
                ownHealthBar.gameObject.SetActive(true);
                ownHealthBar = null;
            }
        }

        // 壁や崖に埋まらないように、見る点から引いた線が当たったらそこで止める。
        private Vector3 KeepOutOfWalls(Vector3 from, Vector3 wanted)
        {
            Vector3 toWanted = wanted - from;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.SphereCastAll(from, 0.3f, toWanted.normalized, toWanted.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                // 見えない壁は映りに関係ないので通り抜ける。
                if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.collider.name == InvisibleWallName) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            if (!float.IsPositiveInfinity(nearest)) return from + toWanted.normalized * Mathf.Max(1.5f, nearest - 0.3f);
            return wanted;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private Camera FindHeroCamera()
        {
            var main = Camera.main;
            if (main != null && main.transform.IsChildOf(transform)) return main;
            foreach (var c in GetComponentsInChildren<Camera>(true))
                if (c.targetTexture == null && c.gameObject.layer != LayerMask.NameToLayer(HeroViewModel.LayerName)) return c;
            return null;
        }
    }
}
