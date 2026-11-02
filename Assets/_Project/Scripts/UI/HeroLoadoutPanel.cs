using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Hero;
using Game.Skill;

namespace Game.UI
{
    public class HeroLoadoutPanel : MonoBehaviour
    {
        [Header("All Hero Classes")]
        [SerializeField] private List<HeroClassData> allClasses = new List<HeroClassData>();
        [SerializeField] private List<SkillData> commonSkills = new List<SkillData>();

        [Header("Team Display (Other Heroes + DK)")]
        [SerializeField] private Text[] otherHeroTexts;
        [SerializeField] private Text demonKingText;

        // HeroPanel spans y 80..950 of the 1920x1080 canvas: the hero lobby's avatar + pentagon at 80% on the left,
        // its settings panel in the middle (its top padding overlaps the top bar), the team list on the right.
        private const float AvatarScale = 0.8f;
        private const float AvatarLeft = 46f;
        private const float PanelLeft = 530f;
        private const float PanelExtendTop = 46f;

        private HeroSkillLoadout loadout;
        private HeroLobbyAvatarView avatarView;
        private CharacterAvatarPreview modelPreview;

        private void Start()
        {
            loadout = new HeroSkillLoadout(allClasses, commonSkills);
            loadout.LoadFromSession();

            var panelRT = (RectTransform)transform;
            var holderGO = new GameObject("AvatarHolder", typeof(RectTransform));
            holderGO.transform.SetParent(panelRT, false);
            var hRT = (RectTransform)holderGO.transform;
            hRT.anchorMin = new Vector2(0f, 1f); hRT.anchorMax = new Vector2(0f, 1f); hRT.pivot = new Vector2(0f, 1f);
            hRT.sizeDelta = new Vector2(1120f, 1080f); // AvatarView lays itself out for this space (centre at 1/4 width)
            hRT.anchoredPosition = new Vector2(AvatarLeft, 0f);
            hRT.localScale = Vector3.one * AvatarScale;
            avatarView = HeroLobbyAvatarView.Create(hRT);
            avatarView.SetUserName("ユーザー名");
            modelPreview = new GameObject("CharacterAvatarPreview_Auto").AddComponent<CharacterAvatarPreview>();

            var settings = HeroLobbySettingsPanel.Create(panelRT);
            settings.ShowDocked(PanelLeft, PanelExtendTop);
            settings.OnLoadoutChanged = RefreshAvatar;
            settings.Bind(loadout);
            RefreshAvatar();
        }

        private void RefreshAvatar()
        {
            avatarView.ShowLoadout(loadout, modelPreview);
        }

        public void RefreshTeamDisplay()
        {
            var bots = GameSession.Instance != null ? GameSession.Instance.CurrentMatchBots : null;
            int heroIdx = 0;
            for (int slot = 0; slot < (otherHeroTexts != null ? otherHeroTexts.Length : 0); slot++)
            {
                if (otherHeroTexts[slot] == null) continue;
                BotData found = null;
                if (bots != null)
                {
                    int counter = 0;
                    foreach (var b in bots)
                    {
                        if (b.role != GameSession.PlayerRole.Hero) continue;
                        if (counter == heroIdx) { found = b; break; }
                        counter++;
                    }
                }
                if (found != null)
                {
                    string ready = found.isReady ? " ✓" : "";
                    otherHeroTexts[slot].text = "Slot " + (slot + 2) + ": " + found.botName + " (" + BotMatchmaker.GetClassDisplay(found.heroClass) + ")" + ready;
                }
                else
                {
                    otherHeroTexts[slot].text = "Slot " + (slot + 2) + ": マッチング中...";
                }
                heroIdx++;
            }
            if (demonKingText != null)
            {
                BotData dk = null;
                if (bots != null) foreach (var b in bots) if (b.role == GameSession.PlayerRole.DemonKing) { dk = b; break; }
                demonKingText.text = dk != null ? ("魔王: " + dk.botName + (dk.isReady ? " ✓" : "")) : "魔王: マッチング中...";
            }
        }

        public void SaveToSession()
        {
            if (loadout != null) loadout.SaveToSession();
        }
    }
}
