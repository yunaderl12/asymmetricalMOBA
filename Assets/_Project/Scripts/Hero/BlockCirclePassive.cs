using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.Hero
{
    /// 魔法使いの自動ブロック魔法陣。攻撃を受けると（打撃でも魔法でも）攻撃の来た方向に魔法陣が出る。
    /// 弱い攻撃は完全に防ぎ、強い攻撃はダメージをそのまま受けて魔法陣だけが割れる。
    public class BlockCirclePassive : HeroPassive, IDamageBlocker
    {
        private const float ChestHeight = 1.2f;
        private const float DistanceSeen = 1.15f;  // 他の人から見た位置と大きさ
        private const float RadiusSeen = 0.95f;
        private const float DistanceOwn = 1.3f;    // 塔の攻撃で何度も出るので、自分の視界をふさがない大きさと濃さにする
        private const float RadiusOwn = 0.42f;
        private const float OpacityOwn = 0.5f;
        private const float SpreadSide = 0.55f;   // 同時に出たときの横のずれ
        private const float SpreadUp = 0.25f;

        private readonly List<float> activeUntil = new List<float>();
        private LevelComponent level;
        private bool isLocalPlayer;

        public int MaxCircles
        {
            get
            {
                int count = 1;
                int current = level != null ? level.CurrentLevel : 1;
                foreach (int required in Data.extraCircleLevels) if (current >= required) count++;
                return count;
            }
        }

        protected override void OnInit()
        {
            level = GetComponent<LevelComponent>();
            isLocalPlayer = GetComponent<HumanHeroMarker>() != null;
        }

        public bool TryBlock(int damage, GameObject attacker, DamageType type)
        {
            if (type == DamageType.True) return false; // 打撃と魔法は区別せず防ぐ。防御無視の攻撃だけは通す。
            activeUntil.RemoveAll(t => Time.time >= t);
            int index = activeUntil.Count;
            if (index >= MaxCircles) return false;

            activeUntil.Add(Time.time + Data.blockSeconds);
            bool blocked = damage <= Data.blockDamage;
            Spawn(attacker, index, blocked);
            Debug.Log($"[Passive] block circle {index + 1}/{MaxCircles}: {damage} dmg {(blocked ? "blocked" : "broke through")}");
            return blocked;
        }

        private void Spawn(GameObject attacker, int index, bool blocked)
        {
            Vector3 chest = transform.position + Vector3.up * ChestHeight;
            Vector3 direction = transform.forward;
            if (attacker != null && attacker != gameObject)
            {
                Vector3 toAttacker = attacker.transform.position + Vector3.up * ChestHeight - chest;
                if (toAttacker.sqrMagnitude > 0.01f) direction = toAttacker.normalized;
            }
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 spread = index == 0 ? Vector3.zero : new Vector3(index == 1 ? -SpreadSide : SpreadSide, SpreadUp, 0f);
            Vector3 position = chest + direction * (isLocalPlayer ? DistanceOwn : DistanceSeen) + rotation * spread;
            float radius = isLocalPlayer ? RadiusOwn : RadiusSeen;
            float opacity = isLocalPlayer ? OpacityOwn : 1f;
            if (blocked) HexWard.SpawnBlock(position, rotation, radius, Data.blockSeconds, opacity);
            else HexWard.SpawnShatter(position, rotation, radius, Data.blockSeconds, opacity);
        }
    }
}
