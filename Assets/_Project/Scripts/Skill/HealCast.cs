using UnityEngine;
using Game.Combat;

namespace Game.Skill
{
    /// 僧侶の Heal。杖を味方へ向けて光を送り、相手の足元から光の柱が立つ。照準の先に味方がいなければ自分を回復する。
    public class HealCast : PriestCast
    {
        private const float AimRadius = 0.5f;
        private const float PillarRadius = 0.8f;
        private const float PillarHeight = 3f;
        private const float PillarSeconds = 0.7f;
        private const float BeamWidth = 0.22f;
        private const float BeamSeconds = 0.26f;

        protected override void OnRelease()
        {
            HealthComponent target = FindAllyInAim();
            bool self = target == null;
            if (self) target = GetComponent<HealthComponent>();
            if (target == null) return;

            ApplyHit(target);
            ReleaseFlash(1.4f);
            Vector3 feet = target.transform.position;
            BlessEffect(feet, PillarRadius * SizeScale, PillarHeight * SizeScale, PillarSeconds, self);
            ShockRing.Spawn(feet, 0.2f, 1.6f * SizeScale, 0.4f, 0.25f, Palette.Light);
            if (!self) SpawnBeam(WandTip, feet + Vector3.up * 1.1f);
            Debug.Log($"[Skill] Heal on {target.name}");
        }

        // 視線の先で一番近い味方。壁や地面に遮られていれば見つからない扱い。
        private HealthComponent FindAllyInAim()
        {
            if (CameraTransform == null) return null;
            HealthComponent best = null;
            float bestDistance = float.PositiveInfinity;
            float wall = float.PositiveInfinity;
            foreach (var h in Physics.SphereCastAll(CameraTransform.position, AimRadius, CameraTransform.forward, Skill.range, ~0, QueryTriggerInteraction.Collide))
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                var hp = h.collider.GetComponentInParent<HealthComponent>();
                if (hp == null)
                {
                    if (!h.collider.isTrigger && h.distance < wall) wall = h.distance;
                    continue;
                }
                if (hp.IsDead || !hp.CompareTag("Player") || h.distance >= bestDistance) continue;
                best = hp;
                bestDistance = h.distance;
            }
            return bestDistance < wall ? best : null;
        }

        private void SpawnBeam(Vector3 from, Vector3 to)
        {
            var go = new GameObject("HealBeam");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = FireballVfx.Trail;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = BeamWidth * SizeScale;
            line.startColor = line.endColor = new Color(1f, 0.85f, 0.4f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Destroy(go, BeamSeconds);
        }
    }
}
