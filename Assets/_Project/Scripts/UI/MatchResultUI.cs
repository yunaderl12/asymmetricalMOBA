using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    public class MatchResultUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text winnerText;
        [SerializeField] private Text hintText;

        private MatchManager bound;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            StartCoroutine(WaitAndHook());
        }

        private void OnDisable()
        {
            if (bound != null)
            {
                bound.OnMatchFinished -= OnFinished;
                bound = null;
            }
            StopAllCoroutines();
        }

        private IEnumerator WaitAndHook()
        {
            while (MatchManager.Instance == null) yield return null;
            bound = MatchManager.Instance;
            bound.OnMatchFinished += OnFinished;
        }

        private void OnFinished(MatchManager.Winner w)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (winnerText != null)
            {
                switch (w)
                {
                    case MatchManager.Winner.Heroes: winnerText.text = "勝者: 勇者チーム"; break;
                    case MatchManager.Winner.DemonKing: winnerText.text = "勝者: 魔王"; break;
                    case MatchManager.Winner.Draw: winnerText.text = "引き分け"; break;
                    default: winnerText.text = "..."; break;
                }
            }
            if (hintText != null) hintText.text = "Esc キーで再起動（Phase 7 で実装予定）";
        }
    }
}
