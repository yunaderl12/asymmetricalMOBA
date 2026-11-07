using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Skill;

namespace Game.UI
{
    public class HeroHUD : MonoBehaviour
    {
        [SerializeField] private string heroTag = "Player";
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Text hpText;
        [SerializeField] private Image mpFillImage;
        [SerializeField] private Text mpText;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;
        [SerializeField] private Text lvText;

        private HealthComponent health;
        private ManaComponent mana;
        private LevelComponent level;
        private ExpComponent exp;
        private SkillRuntime runtime;

        private void Start()
        {
            HealthBarGraphic.Adopt(hpFillImage);
            HealthBarGraphic.Adopt(mpFillImage);
            HealthBarGraphic.Adopt(expFillImage);

            // Not for the demon king side of the match.
            if (GameSession.Instance != null && GameSession.Instance.CurrentRole == GameSession.PlayerRole.DemonKing)
            {
                // Hide this HUD's own GameObject (HP/MP/Lv/Exp).
                gameObject.SetActive(false);
                return;
            }

            var hero = Game.Hero.HumanHeroMarker.Instance != null ? Game.Hero.HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag(heroTag);
            if (hero == null)
            {
                Debug.LogWarning("[HeroHUD] Hero not found by tag '" + heroTag + "'");
                return;
            }
            health = hero.GetComponent<HealthComponent>();
            mana = hero.GetComponent<ManaComponent>();
            level = hero.GetComponent<LevelComponent>();
            exp = hero.GetComponent<ExpComponent>();
            runtime = hero.GetComponent<SkillRuntime>();
            if (health != null) health.OnHPChanged += OnHPChanged;
            if (mana != null) mana.OnMPChanged += OnMPChanged;
            if (level != null) level.OnLevelChanged += OnLevelChanged;
            if (exp != null) exp.OnExpChanged += OnExpChanged;
            // 見た目は HeroStatusHud と SkillCardRow（布と紙のHUD）が出す。シーンに置いた古いバーと文字、味方一覧は隠す。照準は残す。
            HideLegacyChildren();
            HeroStatusHud.Create(hero);
            if (runtime != null) SkillCardRow.Create(runtime);
            UpdateAll();
        }

        private void HideLegacyChildren()
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<CrosshairUI>() != null) continue;
                child.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (health != null) health.OnHPChanged -= OnHPChanged;
            if (mana != null) mana.OnMPChanged -= OnMPChanged;
            if (level != null) level.OnLevelChanged -= OnLevelChanged;
            if (exp != null) exp.OnExpChanged -= OnExpChanged;
        }

        private void OnHPChanged(int cur, int max) { UpdateAll(); }
        private void OnMPChanged(int cur, int max) { UpdateAll(); }
        private void OnLevelChanged(int lv) { UpdateAll(); }
        private void OnExpChanged(int cur, int req) { UpdateAll(); }

        private void UpdateAll()
        {
            if (health != null)
            {
                if (hpFillImage != null)
                    hpFillImage.fillAmount = health.MaxHP > 0 ? (float)health.CurrentHP / health.MaxHP : 0f;
                if (hpText != null)
                    hpText.text = $"HP {health.CurrentHP} / {health.MaxHP}";
            }
            // Heroes and the Demon King both use MP (ManaComponent). Only magic skills cost it.
            if (mana != null)
            {
                if (mpFillImage != null)
                    mpFillImage.fillAmount = mana.MaxMP > 0 ? (float)mana.CurrentMP / mana.MaxMP : 0f;
                if (mpText != null)
                    mpText.text = $"MP {mana.CurrentMP} / {mana.MaxMP}";
            }
            if (level != null && lvText != null)
                lvText.text = $"Lv {level.CurrentLevel} / {level.MaxLevel}";
            if (exp != null)
            {
                int cur = exp.CurrentExp;
                int req = exp.RequiredExp;
                bool maxed = level != null && level.IsMaxLevel;
                if (expFillImage != null)
                    expFillImage.fillAmount = maxed ? 1f : (req > 0 ? (float)cur / req : 0f);
                if (expText != null)
                {
                    if (maxed) expText.text = "Exp MAX";
                    else expText.text = $"Exp {cur} / {req}  (next: {Mathf.Max(0, req - cur)})";
                }
            }
        }
    }
}
