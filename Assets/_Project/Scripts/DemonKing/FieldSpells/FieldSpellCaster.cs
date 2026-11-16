using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.DemonKing
{
    /// 人間の魔王がフィールド魔法を使う。ロビーで装備した5つ（GameSession.FieldSpells）を 1〜5 キーで選ぶ。
    ///  - 場所が要る魔法: 選ぶと狙いの輪が出る。TPS は画面の真ん中、俯瞰は地図の上のマウスの位置。左クリックで発動、右クリックか同じキーでやめる。
    ///  - 場所が要らない魔法（地震）: キーを押すとすぐ発動。
    /// 使うのはクールタイムだけ（MP は使わない）。塔の上空視点では使えない。
    public class FieldSpellCaster : MonoBehaviour
    {
        private const float AimDistance = 400f;
        private static readonly Game.Core.GameKey[] Keys = { Game.Core.GameKey.Slot1, Game.Core.GameKey.Slot2, Game.Core.GameKey.Slot3, Game.Core.GameKey.Slot4, Game.Core.GameKey.Slot5 };
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        public FieldSpellData[] Slots { get; private set; }
        public int Aiming { get; private set; } = -1;
        public bool AimValid { get; private set; }
        public string AimNote { get; private set; } = "";
        /// 直前に断られた理由（クールタイム中など）と、その時刻。
        public string Refusal { get; private set; } = "";
        public float RefusedAt { get; private set; } = -10f;

        /// 狙っている最中か、狙いのクリック（発動・取り消し）をしたところ。
        /// 塔の印などのボタンはクリックを離したときに反応するので、同じクリックで塔の窓を開かないように見る。
        public bool UsesClick => Aiming >= 0 || Time.unscaledTime - aimClickAt < AimClickGrace;

        private const float AimClickGrace = 0.5f;
        private float aimClickAt = -10f;
        private float[] readyAt;
        private Vector3 aimPoint;
        private FieldSpellRing marker;
        private DemonKingViewSwitcher view;

        public static FieldSpellCaster Ensure(GameObject demonKing)
        {
            var caster = demonKing.GetComponent<FieldSpellCaster>();
            if (caster == null) caster = demonKing.AddComponent<FieldSpellCaster>();
            return caster;
        }

        /// 装備がなければ（ロビーを通っていない・テストシーン）、Resources/FieldSpells の先頭から5つ。
        public static FieldSpellData[] LoadEquipped()
        {
            var slots = new FieldSpellData[GameSession.FIELD_SPELL_SLOTS];
            var session = GameSession.Instance;
            bool any = false;
            if (session != null && session.FieldSpells != null)
                for (int i = 0; i < slots.Length && i < session.FieldSpells.Length; i++)
                {
                    slots[i] = session.FieldSpells[i];
                    any |= slots[i] != null;
                }
            if (!any)
            {
                var all = new List<FieldSpellData>(Resources.LoadAll<FieldSpellData>("FieldSpells"));
                all.Sort((a, b) => a.order.CompareTo(b.order));
                for (int i = 0; i < slots.Length && i < all.Count; i++) slots[i] = all[i];
            }
            return slots;
        }

        private void Awake()
        {
            Slots = LoadEquipped();
            readyAt = new float[Slots.Length];
            view = GetComponent<DemonKingViewSwitcher>();
            // BOT 魔王は Cast を直接呼ぶ。キーも札の HUD も使わない。
            playerControlled = GetComponent<DemonKingController>() != null;
        }

        private bool playerControlled;

        private void Start()
        {
            if (playerControlled) Game.UI.FieldSpellBar.Create(this);
        }

        /// その魔法をいま撃てるか（クールタイムが明けている）。
        public bool IsReady(int i) => i >= 0 && i < Slots.Length && Slots[i] != null && CooldownLeft(i) <= 0f;

        public float CooldownLeft(int i) => i >= 0 && i < readyAt.Length ? Mathf.Max(0f, readyAt[i] - Time.time) : 0f;

        public float CooldownRatio(int i)
        {
            var s = i >= 0 && i < Slots.Length ? Slots[i] : null;
            return s == null || s.cooldown <= 0f ? 0f : Mathf.Clamp01(CooldownLeft(i) / s.cooldown);
        }

        /// 狙いの輪の大きさ（m）。
        public static float AimRadius(FieldSpellData s)
        {
            switch (s.kind)
            {
                case FieldSpellKind.Meteor: return Mathf.Max(s.scatterRadius, s.radius);
                case FieldSpellKind.Lightning: return Mathf.Max(s.outerRadius, s.radius);
                case FieldSpellKind.Downpour: return 4f;
                default: return s.radius;
            }
        }

        private void Update()
        {
            if (!playerControlled || Game.UI.InGameMenu.BlocksGameInput) return;
            for (int i = 0; i < Keys.Length && i < Slots.Length; i++)
                if (Game.Core.KeyBindings.WasPressed(Keys[i])) Select(i);
            if (Aiming < 0) return;
            UpdateAim();
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) aimClickAt = Time.unscaledTime;
            if (mouse.rightButton.wasPressedThisFrame) CancelAim();
            else if (mouse.leftButton.wasPressedThisFrame && AimValid) Confirm();
        }

        private void Select(int i)
        {
            var s = Slots[i];
            if (s == null) return;
            if (Aiming == i)
            {
                CancelAim();
                return;
            }
            if (CooldownLeft(i) > 0f)
            {
                Refuse(s.spellName + "はあと " + Mathf.CeilToInt(CooldownLeft(i)) + " 秒");
                return;
            }
            if (view != null && view.CurrentMode == DemonKingViewSwitcher.ViewMode.SpawnerOverhead)
            {
                Refuse("塔の上空からは使えない");
                return;
            }
            if (!s.needsTarget)
            {
                CancelAim();
                Cast(i, transform.position);
                return;
            }
            Aiming = i;
            AimValid = false;
        }

        private void Refuse(string reason)
        {
            Refusal = reason;
            RefusedAt = Time.unscaledTime;
        }

        private void UpdateAim()
        {
            var s = Slots[Aiming];
            bool birdsEye = view != null && view.CurrentMode == DemonKingViewSwitcher.ViewMode.BirdsEye;
            var map = Game.UI.BirdsEyeMapUI.Instance;
            bool found = false;
            if (view != null && view.CurrentMode == DemonKingViewSwitcher.ViewMode.SpawnerOverhead)
            {
                CancelAim();
                return;
            }
            if (birdsEye && map != null && Mouse.current != null) found = map.ScreenToWorld(Mouse.current.position.ReadValue(), out aimPoint);
            else if (!birdsEye) found = AimFromCamera(out aimPoint);

            AimValid = found;
            AimNote = found ? "" : (birdsEye ? "地図の上を選ぶ" : "地面を狙う");
            if (found && s.targetsBiome)
            {
                var biome = StageMap.BiomeAt(aimPoint);
                AimValid = biome != StageMap.Biome.None;
                AimNote = biome == StageMap.Biome.Mountain ? "山" : biome == StageMap.Biome.Forest ? "森" : biome == StageMap.Biome.Desert ? "砂漠" : "バイオームの中を選ぶ";
            }

            Color color = AimValid ? s.color * 1.6f : new Color(0.6f, 0.6f, 0.6f);
            color.a = 0.95f;
            if (map != null) map.ShowAim(birdsEye && found, aimPoint, AimRadius(s), color);
            if (!birdsEye && found)
            {
                if (marker == null) marker = FieldSpellRing.Create(aimPoint, AimRadius(s), color);
                marker.gameObject.SetActive(true);
                marker.color = color;
                marker.pulse = 0.4f;
                marker.Place(aimPoint, AimRadius(s));
            }
            else if (marker != null) marker.gameObject.SetActive(false);
        }

        /// TPS: 画面の真ん中から前へ。魔王自身と、すり抜ける当たり（トリガー）は無視する。
        private bool AimFromCamera(out Vector3 point)
        {
            point = Vector3.zero;
            var cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int n = Physics.RaycastNonAlloc(ray, Hits, AimDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance >= best || h.collider.transform.IsChildOf(transform) || h.collider.name == "Field_Barrier") continue;
                best = h.distance;
                point = h.point;
            }
            if (best == float.MaxValue) return false;
            point = FieldSpellEffect.Ground(point);
            return true;
        }

        private void Confirm()
        {
            int i = Aiming;
            Vector3 p = aimPoint;
            CancelAim();
            Cast(i, p);
        }

        public void CancelAim()
        {
            Aiming = -1;
            AimValid = false;
            if (marker != null) marker.gameObject.SetActive(false);
            var map = Game.UI.BirdsEyeMapUI.Instance;
            if (map != null) map.ShowAim(false, Vector3.zero, 0f, Color.clear);
        }

        /// クールタイムを確かめずに発動する（/debug spell からも使う）。
        public void Cast(int i, Vector3 at)
        {
            var s = Slots[i];
            if (s == null) return;
            if (FieldSpellEffect.Cast(s, at, gameObject) == null) return;
            // 魔王の体も、片手を掲げて振り下ろす。
            var body = GetComponent<Game.Hero.CharacterModelAnimator>();
            if (body != null) body.PlayCastGesture();
            readyAt[i] = Time.time + s.cooldown;
        }

        public void ResetCooldowns()
        {
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0f;
        }

        private void OnDisable()
        {
            CancelAim();
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
