using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class PowerItemNotifierUI : MonoBehaviour
    {
        public static PowerItemNotifierUI Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private Text text;
        [SerializeField] private float defaultDuration = 3f;

        private Coroutine activeRoutine;

        private void Awake()
        {
            Instance = this;
            if (root != null) root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowNotification(string msg, float duration = -1f)
        {
            if (text != null) text.text = msg;
            if (root != null) root.SetActive(true);
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            float d = duration > 0f ? duration : defaultDuration;
            activeRoutine = StartCoroutine(HideAfter(d));
        }

        private IEnumerator HideAfter(float t)
        {
            yield return new WaitForSeconds(t);
            if (root != null) root.SetActive(false);
            activeRoutine = null;
        }
    }
}
