using System.Collections.Generic;
using UnityEngine;
using Game.Hero;
using Game.Skill;

namespace Game.Core
{
    public static class BotMatchmaker
    {
        public static List<BotData> GenerateBots(GameSession.PlayerRole humanRole)
        {
            var classes = LoadAllClasses();
            var commonSkills = LoadCommonSkills();
            var bots = new List<BotData>();

            if (humanRole == GameSession.PlayerRole.Hero)
            {
                for (int i = 0; i < 3; i++)
                {
                    bots.Add(new BotData
                    {
                        botName = "BotHero" + (i + 1),
                        role = GameSession.PlayerRole.Hero,
                        heroClass = classes.Count > 0 ? classes[Random.Range(0, classes.Count)] : null,
                        freeSlotSkill = commonSkills.Count > 0 ? commonSkills[i % commonSkills.Count] : null,
                        isReady = true,
                    });
                }
                bots.Add(new BotData
                {
                    botName = "BotDemonKing",
                    role = GameSession.PlayerRole.DemonKing,
                    isReady = true,
                });
            }
            else if (humanRole == GameSession.PlayerRole.DemonKing)
            {
                for (int i = 0; i < 4; i++)
                {
                    bots.Add(new BotData
                    {
                        botName = "BotHero" + (i + 1),
                        role = GameSession.PlayerRole.Hero,
                        heroClass = classes.Count > 0 ? classes[Random.Range(0, classes.Count)] : null,
                        freeSlotSkill = commonSkills.Count > 0 ? commonSkills[i % commonSkills.Count] : null,
                        isReady = true,
                    });
                }
            }
            return bots;
        }

        public static string GetClassDisplay(HeroClassData c)
        {
            if (c == null) return "?";
            switch (c.className)
            {
                case "Swordsman": return "剣士";
                case "Mage": return "魔法使い";
                case "Priest": return "僧侶";
                case "Warrior": return "重戦士";
                default: return c.className;
            }
        }

        private static List<HeroClassData> LoadAllClasses()
        {
            var result = new List<HeroClassData>();
#if UNITY_EDITOR
            string[] names = new string[] { "Swordsman", "Mage", "Priest", "Warrior" };
            foreach (var n in names)
            {
                var c = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroClassData>("Assets/_Project/ScriptableObjects/Classes/" + n + ".asset");
                if (c != null) result.Add(c);
            }
#endif
            return result;
        }

        // BOTが意味のある使い方をできるものを先に並べる（Detect の印は本人の画面にしか出ない）。
        private static List<SkillData> LoadCommonSkills()
        {
            var result = new List<SkillData>();
#if UNITY_EDITOR
            foreach (var n in new[] { "Sprint", "Recover", "Detect" })
            {
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/Common/" + n + ".asset");
                if (s != null) result.Add(s);
            }
#endif
            return result;
        }
    }
}
