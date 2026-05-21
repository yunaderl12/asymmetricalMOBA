using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Hero;
using Game.Skill;

namespace Game.Core
{
    public static class DebugCommandRegistry
    {
        public delegate string CommandHandler(string[] args);
        private struct Entry { public string description; public CommandHandler handler; }

        private static readonly Dictionary<string, Entry> commands = new Dictionary<string, Entry>();

        static DebugCommandRegistry()
        {
            Register("help", "全コマンド一覧", HelpCmd);
            Register("addexp", "勇者に Exp 加算 [n=50]", AddExpCmd);
            Register("winhero", "魔王城コア破壊 (勇者勝利)", WinHeroCmd);
            Register("losehero", "StartTown 破壊 (魔王勝利)", LoseHeroCmd);
            Register("spawnitem", "PowerItem 強制スポーン", SpawnItemCmd);
            Register("respawn", "勇者を StartTown でリスポーン (HP/SP 全回復)", RespawnCmd);
            Register("skipprep", "PrepPhase スキップ", SkipPrepCmd);
            Register("usemp", "魔王 MP消費 [n=20]", UseMpCmd);
            Register("selfdamage", "自分にダメージ [n=50]", SelfDamageCmd);
            Register("skiplevel", "魔王 Lv+1", SkipLevelCmd);
            Register("switch", "ロール切替", SwitchCmd);
            Register("vision", "視野リング 表示切替", VisionCmd);
            Register("commands", "デバッグキー一覧 表示切替", CommandsCmd);
            Register("skillup", "スキル [1-5] レベルアップ", SkillUpCmd);
        }

        public static void Register(string name, string description, CommandHandler handler)
        {
            commands[name] = new Entry { description = description, handler = handler };
        }

        public static IEnumerable<KeyValuePair<string, string>> GetAllCommands()
        {
            foreach (var kv in commands) yield return new KeyValuePair<string, string>(kv.Key, kv.Value.description);
        }

        public static string Execute(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string s = input.Trim();
            if (s.StartsWith("/")) s = s.Substring(1);
            if (s == "debug") s = "debug help";
            if (s.StartsWith("debug ")) s = s.Substring(6);
            else if (s.StartsWith("debug")) s = s.Substring(5);

            var parts = s.Split(' ');
            string name = parts[0];
            var args = new string[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++) args[i - 1] = parts[i];

            if (commands.TryGetValue(name, out var entry))
            {
                try { return entry.handler(args); }
                catch (System.Exception ex) { return "error: " + ex.Message; }
            }
            return "Unknown command: /debug " + name + " (try /debug help)";
        }

        private static GameObject FindHero()
        {
            return HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag("Player");
        }

        private static string HelpCmd(string[] args)
        {
            var sb = new StringBuilder();
            sb.Append("Commands:");
            foreach (var kv in commands)
            {
                sb.Append("\n/debug ");
                sb.Append(kv.Key);
                sb.Append(" - ");
                sb.Append(kv.Value.description);
            }
            return sb.ToString();
        }

        private static string AddExpCmd(string[] args)
        {
            int n = 50;
            if (args.Length > 0) int.TryParse(args[0], out n);
            var hero = FindHero();
            if (hero == null) return "Hero not found";
            var exp = hero.GetComponent<ExpComponent>();
            if (exp == null) return "ExpComponent not found";
            exp.AddExp(n);
            return "Added " + n + " Exp";
        }

        private static string WinHeroCmd(string[] args)
        {
            var castle = GameObject.FindWithTag("DemonCastle");
            if (castle == null) return "DemonCastle not found";
            var hc = castle.GetComponent<HealthComponent>();
            if (hc == null) hc = castle.GetComponentInChildren<HealthComponent>();
            if (hc == null) return "DemonCastle has no HealthComponent";
            hc.TakeDamage(hc.MaxHP * 2, null);
            return "DemonCastle_Core destroyed";
        }

        private static string LoseHeroCmd(string[] args)
        {
            var st = GameObject.FindWithTag("StartTown");
            if (st == null) return "StartTown not found";
            var hc = st.GetComponent<HealthComponent>();
            if (hc == null) hc = st.GetComponentInChildren<HealthComponent>();
            if (hc == null) return "StartTown has no HealthComponent";
            hc.TakeDamage(hc.MaxHP * 2, null);
            return "StartTown_Core destroyed";
        }

        private static string SpawnItemCmd(string[] args)
        {
            if (MatchItemGimmick.Instance == null) return "MatchItemGimmick not in scene";
            MatchItemGimmick.Instance.SpawnPowerItemNow();
            return "PowerItem spawned";
        }

        private static string RespawnCmd(string[] args)
        {
            var hero = FindHero();
            if (hero == null) return "Hero not found";
            var st = GameObject.FindWithTag("StartTown");
            if (st == null) return "StartTown not found for respawn";
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            hero.transform.position = st.transform.position;
            if (cc != null) cc.enabled = true;
            var hp = hero.GetComponent<HealthComponent>();
            if (hp != null) hp.ResetHealth();
            var sp = hero.GetComponent<SkillPointComponent>();
            if (sp != null) sp.RestoreSP(int.MaxValue);
            return "Hero respawned at StartTown (HP/SP restored)";
        }

        private static string SkipPrepCmd(string[] args)
        {
            if (MatchManager.Instance == null) return "MatchManager not in scene";
            MatchManager.Instance.SkipPrepPhase();
            return "PrepPhase skipped";
        }

        private static string UseMpCmd(string[] args)
        {
            int n = 20;
            if (args.Length > 0) int.TryParse(args[0], out n);
            var dk = GameObject.FindWithTag("DemonKing");
            if (dk == null) return "DemonKing not found";
            var mp = dk.GetComponent<ManaComponent>();
            if (mp == null) return "ManaComponent not found";
            bool ok = mp.TryUseMana(n);
            return ok ? ("Used " + n + " MP, now " + mp.CurrentMP + "/" + mp.MaxMP) : ("Not enough MP (" + mp.CurrentMP + "/" + mp.MaxMP + ")");
        }

        private static string SelfDamageCmd(string[] args)
        {
            int n = 50;
            if (args.Length > 0) int.TryParse(args[0], out n);
            var hero = FindHero();
            var hc = hero != null ? hero.GetComponent<HealthComponent>() : null;
            if (hc == null) return "Hero HC not found";
            hc.TakeDamage(n, hero);
            return "Self-damaged " + n;
        }

        private static string SkipLevelCmd(string[] args)
        {
            var dk = GameObject.FindWithTag("DemonKing");
            var lvl = dk != null ? dk.GetComponent<LevelComponent>() : null;
            if (lvl == null) return "DemonKing LevelComponent not found";
            lvl.LevelUp();
            return "DemonKing Lv -> " + lvl.CurrentLevel;
        }

        private static string SwitchCmd(string[] args)
        {
            var pms = Object.FindObjectsByType<PrepPhaseManager>(FindObjectsSortMode.None);
            if (pms.Length == 0) return "PrepPhaseManager not in scene";
            pms[0].SwitchRole();
            return "Role switched -> " + pms[0].EffectiveRole;
        }

        private static string VisionCmd(string[] args)
        {
            DebugVision.Toggle();
            return "DebugVision -> " + (DebugVision.IsActive ? "ON" : "OFF");
        }

        private static string CommandsCmd(string[] args)
        {
            return HelpCmd(args);
        }

        private static string SkillUpCmd(string[] args)
        {
            if (args.Length == 0) return "usage: /debug skillup [1-5]";
            int slot;
            if (!int.TryParse(args[0], out slot)) return "invalid slot";
            slot -= 1;
            var hero = FindHero();
            var runtime = hero != null ? hero.GetComponent<SkillRuntime>() : null;
            if (runtime == null) return "SkillRuntime not found";
            if (runtime.LevelUpPointsAvailable <= 0)
            {
                if (!runtime.CanLevelUp(slot)) return "slot " + (slot + 1) + " cannot level up";
                if (runtime.LevelUp(slot)) return "slot " + (slot + 1) + " leveled up (no point consumed)";
                return "level up failed";
            }
            if (runtime.SpendLevelUpPoint(slot)) return "slot " + (slot + 1) + " leveled up (-1 point)";
            return "slot " + (slot + 1) + " cannot level up";
        }
    }
}
