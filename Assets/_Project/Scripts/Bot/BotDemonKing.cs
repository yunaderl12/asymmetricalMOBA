using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Combat;
using Game.Core;
using Game.DemonKing;
using Game.Monster;

namespace Game.Bot
{
    /// BOT 魔王。人間の魔王と同じ手段だけで戦う（2026-11-16 にユーザーと決めた方針）。
    ///  - 見え方: 人間と同じ。塔の検知範囲・城内・探知スキル（MP を使う）と、自分の目（近くで壁に隠れていない勇者）で見えた勇者だけを知っている。
    ///  - 動き: 最終局面（最大レベル）までは城を守る。城の近く（botDkGuardRadius）まで来た勇者へ向かって剣で斬り、いなければ城内の元の場所へ戻る。
    ///          最大レベルになったら城を出て、はじまりのまちのコアへ進軍する（途中の勇者とは戦う）。
    ///  - 塔: 勇者が検知範囲にいる塔・削られている塔から先に、空いたコストへ魔物を1体ずつ足す。
    ///  - フィールド魔法: 見えている勇者の固まりを狙って、1つずつ間を空けて撃つ。
    /// 数値は BalanceConfig の botDk*（仮値）。
    public class BotDemonKing : MonoBehaviour
    {
        private const float TurnDegreesPerSecond = 540f;
        private const float HomeReachedDistance = 1.2f;

        private HealthComponent health;
        private ManaComponent mana;
        private LevelComponent level;
        private StatModifierContainer mods;
        private TerrainWalker terrain;
        private NavMeshAgent agent;
        private DemonKingAttack attack;
        private FieldSpellCaster spells;
        private DemonKingDetect detect;
        private DemonCastleRegen castle;
        private Game.Hero.CharacterModelAnimator modelAnim;

        private Vector3 home;
        private HealthComponent target;
        private float lastSeenTime = -999f;
        private float nextSpellAt;
        private float nextWanderAt;
        private readonly List<HealthComponent> visible = new List<HealthComponent>();

        /// 進軍中か（最大レベル）。
        public bool Marching => level != null && level.CurrentLevel >= level.MaxLevel;
        public HealthComponent Target => target;
        public IReadOnlyList<HealthComponent> VisibleHeroes => visible;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            mana = GetComponent<ManaComponent>();
            level = GetComponent<LevelComponent>();
            mods = GetComponent<StatModifierContainer>();
            terrain = TerrainWalker.For(gameObject);
            terrain.SlopePenalty = BalanceConfig.Instance.demonKingSlopePenalty;
            terrain.SplashScale = BalanceConfig.Instance.demonKingModelScale;

            // 城のトリガー（MP の回復と城外デバフの判定）に出入りが届くよう、動かない Rigidbody を付ける。
            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.7f;
            agent.height = 3f;
            agent.angularSpeed = TurnDegreesPerSecond;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0f;
            agent.autoBraking = true;
            agent.speed = BalanceConfig.Instance.botDkMoveSpeed;
            gameObject.AddComponent<GroundFollower>();

            // 人間の魔王と同じ部品（オーラ・城外デバフ・探知・フィールド魔法・剣）。
            if (GetComponent<CommanderAura>() == null) gameObject.AddComponent<CommanderAura>();
            if (GetComponent<OutsideDebuff>() == null) gameObject.AddComponent<OutsideDebuff>();
            detect = GetComponent<DemonKingDetect>();
            if (detect == null) detect = gameObject.AddComponent<DemonKingDetect>();
            spells = FieldSpellCaster.Ensure(gameObject);
            attack = DemonKingAttack.Ensure(gameObject);
        }

        private void Start()
        {
            modelAnim = Game.Hero.CharacterModelAnimator.AttachDemonKingModel(gameObject);
            if (modelAnim != null) modelAnim.NominalSpeed = BalanceConfig.Instance.botDkMoveSpeed;
            castle = FindFirstObjectByType<DemonCastleRegen>();
            if (NavMesh.SamplePosition(transform.position, out var hit, 12f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else Debug.LogWarning($"[BotDemonKing] no NavMesh near {transform.position}; the bot cannot move.");
            home = transform.position;
            // 試合の始まりから数えて botDkDetectAfterSeconds たつまでは探知を使わない。
            lastSeenTime = Time.time;
            if (health != null) health.OnDeath += OnDeath;

            var cfg = BalanceConfig.Instance;
            InvokeRepeating(nameof(Think), 0.5f, cfg.botDkThinkInterval);
            InvokeRepeating(nameof(Reinforce), 1f, cfg.botDkReinforceInterval);
            InvokeRepeating(nameof(ThinkSpell), 2f, cfg.botDkSpellThinkInterval);
            Debug.Log($"[BotDemonKing] ready at {home} (castle={(castle != null ? castle.name : "none")}, spells={spells.Slots.Length})");
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDeath -= OnDeath;
        }

        private bool Dead => health != null && health.IsDead;

        private void OnDeath()
        {
            CancelInvoke();
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        // 止まって斬るあいだは NavMeshAgent が向きを変えないので、自分で相手の方を向く。
        private void Update()
        {
            if (Dead || target == null || agent == null || !agent.isOnNavMesh) return;
            if (agent.velocity.sqrMagnitude > 0.5f) return;
            Vector3 flat = target.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), TurnDegreesPerSecond * Time.deltaTime);
        }

        // ---- 見え方（人間の魔王と同じ手段だけ）

        private void UpdateVision()
        {
            visible.Clear();
            bool detecting = detect != null && detect.IsActive;
            foreach (var hero in FieldSpellEffect.Heroes())
            {
                if (detecting || InCastle(hero) || SeenByTower(hero.transform.position) || SeenByEye(hero)) visible.Add(hero);
            }
            if (visible.Count > 0) lastSeenTime = Time.time;
        }

        // 足元は城のトリガーの箱より下にあるので、体の真ん中で測る。
        private bool InCastle(HealthComponent hero)
        {
            return castle != null && hero != null && castle.Contains(hero.transform.position + Vector3.up);
        }

        private static readonly RaycastHit[] SightHits = new RaycastHit[16];

        // 人間の魔王が画面で見えるのと同じく、近くで壁や地形に隠れていない勇者は見える。
        private bool SeenByEye(HealthComponent hero)
        {
            Vector3 eye = transform.position + Vector3.up * 2.5f;
            Vector3 to = hero.transform.position + Vector3.up * 1.2f;
            Vector3 d = to - eye;
            float dist = d.magnitude;
            if (dist > BalanceConfig.Instance.botDkSightRadius) return false;
            if (dist < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(eye, d / dist, SightHits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = SightHits[i].collider;
                if (col.transform.IsChildOf(transform) || col.transform.IsChildOf(hero.transform)) continue;
                // ほかのキャラ（魔物・勇者）の陰は見通せる扱い。地形と建物だけがさえぎる。
                if (col.GetComponentInParent<HealthComponent>() != null && !col.GetComponentInParent<Spawner>()) continue;
                return false;
            }
            return true;
        }

        private float FromCastle(Vector3 position)
        {
            Vector3 center = castle != null ? castle.transform.position : home;
            Vector3 d = position - center;
            d.y = 0f;
            return d.magnitude;
        }

        private static bool SeenByTower(Vector3 position)
        {
            foreach (var s in Spawner.All) if (s != null && s.DetectsPosition(position)) return true;
            return false;
        }

        // ---- 動きと剣

        private void Think()
        {
            if (Dead || agent == null || !agent.isOnNavMesh) return;
            agent.speed = BalanceConfig.Instance.botDkMoveSpeed * (mods != null ? mods.TotalSpdMultiplier : 1f) * terrain.SpeedFactor;
            UpdateVision();

            if (StatusEffectController.IsStunnedObj(gameObject) || attack.Swinging)
            {
                Stop();
                return;
            }

            target = ChooseTarget();
            if (target != null)
            {
                if (DemonKingAttack.InReach(transform, target))
                {
                    Stop();
                    if (FacingTarget()) attack.TryAttack();
                }
                else Go(target.transform.position);
                return;
            }

            if (Marching)
            {
                var core = DemonKingAttack.FindTownCore();
                if (core != null && !core.IsDead)
                {
                    if (DemonKingAttack.InReach(transform, core))
                    {
                        target = core;
                        Stop();
                        if (FacingTarget()) attack.TryAttack();
                    }
                    else Go(core.transform.position);
                    return;
                }
            }
            Wander();
        }

        // 城を守るとき: 城の近く（botDkGuardRadius）まで来ている見えている勇者のうち、いちばん近い相手。
        // 進軍中: 自分の近く（botDkEngageRadius）の見えている勇者。
        private HealthComponent ChooseTarget()
        {
            var cfg = BalanceConfig.Instance;
            float limit = Marching ? cfg.botDkEngageRadius : float.MaxValue;
            HealthComponent best = null;
            float bestDist = float.MaxValue;
            foreach (var hero in visible)
            {
                // いま追っている相手は、少し外へ出ても（1.3倍まで）追い続ける。
                float guard = hero == target ? cfg.botDkGuardRadius * 1.3f : cfg.botDkGuardRadius;
                if (!Marching && !InCastle(hero) && FromCastle(hero.transform.position) > guard) continue;
                float d = Vector3.Distance(transform.position, hero.transform.position);
                if (d > limit || d >= bestDist) continue;
                bestDist = d;
                best = hero;
            }
            return best;
        }

        private bool FacingTarget()
        {
            if (target == null) return false;
            Vector3 flat = target.transform.position - transform.position;
            flat.y = 0f;
            return flat.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, flat) <= BalanceConfig.Instance.dkAttackArc * 0.35f;
        }

        // 城内で勇者がいなければ、元の場所のまわりを少し歩く。
        private void Wander()
        {
            target = null;
            float fromHome = Vector3.Distance(transform.position, home);
            if (fromHome > BalanceConfig.Instance.botDkWanderRadius + HomeReachedDistance)
            {
                Go(home);
                return;
            }
            if (Time.time < nextWanderAt || agent.pathPending || agent.remainingDistance > 0.5f) return;
            nextWanderAt = Time.time + Random.Range(3f, 7f);
            Vector2 r = Random.insideUnitCircle * BalanceConfig.Instance.botDkWanderRadius;
            Vector3 p = home + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) Go(hit.position);
        }

        private void Go(Vector3 destination)
        {
            // 閉じた結界は NavMesh を切り抜いているので、道はその手前で切れる。人間の魔王と同じく、壁ぎわまで来たら素通りする。
            if (agent.velocity.sqrMagnitude < 1f && BossGate.TryCrossing(transform.position, destination, GateCrossMargin, out var across)
                && NavMesh.SamplePosition(across, out var hit, GateCrossMargin, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[BotDemonKing] passed through a sealed gate to {hit.position}");
            }
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        private const float GateCrossMargin = 2.5f;

        private void Stop()
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        // ---- 塔への魔物の追加

        private void Reinforce()
        {
            if (Dead) return;
            Spawner best = null;
            MonsterData bestMonster = null;
            float bestScore = float.MinValue;
            foreach (var s in Spawner.All)
            {
                if (s == null || !s.IsFunctional) continue;
                var monster = PickMonster(s);
                if (monster == null) continue;
                float score = TowerPriority(s) + (s.MaxCost - s.CurrentCost) * 0.1f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = s;
                bestMonster = monster;
            }
            if (best != null && best.AddToInv(bestMonster))
                Debug.Log($"[BotDemonKing] added {bestMonster.monsterName} to {best.name} (cost {best.CurrentCost}/{best.MaxCost})");
        }

        // 勇者が検知範囲にいる塔が最優先。次に削られている塔。
        private float TowerPriority(Spawner s)
        {
            float score = 0f;
            foreach (var hero in FieldSpellEffect.Heroes())
                if (s.DetectsPosition(hero.transform.position)) score += 100f;
            var hp = s.GetComponent<HealthComponent>();
            if (hp != null && hp.MaxHP > 0 && hp.CurrentHP < hp.MaxHP) score += 50f * (1f - (float)hp.CurrentHP / hp.MaxHP);
            return score;
        }

        // その塔で出せる魔物のうち、空いたコストに入るいちばん重い魔物（同じ重さなら選び方はランダム）。
        private static readonly List<MonsterData> Candidates = new List<MonsterData>();

        private static MonsterData PickMonster(Spawner s)
        {
            Candidates.Clear();
            var session = GameSession.Instance;
            var pool = session != null ? session.TowerMonstersFor(s.Biome) : null;
            if (pool == null || pool.Count == 0) pool = new List<MonsterData>(s.Inv);
            int bestCost = -1;
            foreach (var m in pool)
            {
                if (m == null || !s.CanAddMonster(m)) continue;
                if (m.cost > bestCost) { bestCost = m.cost; Candidates.Clear(); }
                if (m.cost == bestCost && !Candidates.Contains(m)) Candidates.Add(m);
            }
            return Candidates.Count > 0 ? Candidates[Random.Range(0, Candidates.Count)] : null;
        }

        // ---- フィールド魔法と探知

        private void ThinkSpell()
        {
            if (Dead || spells == null) return;
            var cfg = BalanceConfig.Instance;
            UpdateVision();
            if (visible.Count == 0)
            {
                bool longUnseen = Time.time - lastSeenTime >= cfg.botDkDetectAfterSeconds;
                if (longUnseen && detect != null && detect.CooldownRemaining <= 0f && mana != null && mana.CurrentMP >= detect.MpCost + cfg.botDkDetectMpReserve)
                    detect.TryActivate();
                return;
            }
            if (Time.time < nextSpellAt) return;

            int slots = spells.Slots.Length;
            int start = Random.Range(0, slots);
            for (int k = 0; k < slots; k++)
            {
                int i = (start + k) % slots;
                if (!spells.IsReady(i)) continue;
                if (!TryAim(spells.Slots[i], out Vector3 point)) continue;
                spells.Cast(i, point);
                nextSpellAt = Time.time + cfg.botDkSpellGap;
                Debug.Log($"[BotDemonKing] cast {spells.Slots[i].spellName} at {point} ({visible.Count} hero(es) in sight)");
                return;
            }
        }

        // 見えている勇者がいちばん多く入る場所を狙う。地震は場所を選ばないが、地上の魔物にも当たるので2人以上見えているときだけ。
        private bool TryAim(FieldSpellData s, out Vector3 point)
        {
            point = transform.position;
            if (s.kind == FieldSpellKind.Earthquake) return visible.Count >= 2;

            float radius = Mathf.Max(2f, FieldSpellCaster.AimRadius(s));
            int bestCount = 0;
            Vector3 bestCenter = Vector3.zero;
            foreach (var hero in visible)
            {
                int count = 0;
                Vector3 sum = Vector3.zero;
                foreach (var other in visible)
                {
                    Vector3 d = other.transform.position - hero.transform.position;
                    d.y = 0f;
                    if (d.magnitude > radius) continue;
                    count++;
                    sum += other.transform.position;
                }
                if (count <= bestCount) continue;
                bestCount = count;
                bestCenter = sum / count;
            }
            if (bestCount == 0) return false;
            point = FieldSpellEffect.Ground(bestCenter);
            if (s.targetsBiome && StageMap.BiomeAt(point) == StageMap.Biome.None) return false;
            return true;
        }
    }
}
