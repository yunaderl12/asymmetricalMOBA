using UnityEngine;
using Game.Core;
using Game.Skill;

namespace Game.Hero
{
    public class MatchEndLockout : MonoBehaviour
    {
        private HeroFPSController fps;
        private HeroAttack attack;
        private SkillExecutor executor;
        private HeroDebugInput debugInput;
        private PrepPhaseSpectator spectator;

        private void Awake()
        {
            fps = GetComponent<HeroFPSController>();
            attack = GetComponent<HeroAttack>();
            executor = GetComponent<SkillExecutor>();
            debugInput = GetComponent<HeroDebugInput>();
            spectator = GetComponent<PrepPhaseSpectator>();
        }

        private void Start()
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.OnMatchFinished += OnFinished;
            }
        }

        private void OnDestroy()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.OnMatchFinished -= OnFinished;
        }

        private void OnFinished(MatchManager.Winner _)
        {
            if (fps != null) fps.enabled = false;
            if (attack != null) attack.enabled = false;
            if (executor != null) executor.enabled = false;
            if (debugInput != null) debugInput.enabled = false;
            if (spectator != null) spectator.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[MatchEndLockout] Hero input fully locked.");
        }
    }
}
