using System.Collections;
using UnityEngine;

namespace Game.Core
{
    public class LightningFlash : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField] private float minInterval = 5f;
        [SerializeField] private float maxInterval = 12f;
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private float flashIntensity = 3f;
        [SerializeField] private Color flashColor = Color.white;

        public bool IsActive { get; set; }

        private float timer;
        private bool flashing;

        private void Update()
        {
            if (!IsActive || flashing || targetLight == null) return;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                StartCoroutine(FlashOnce());
                timer = Random.Range(minInterval, maxInterval);
            }
        }

        private IEnumerator FlashOnce()
        {
            flashing = true;
            Color prevColor = targetLight.color;
            float prevIntensity = targetLight.intensity;
            targetLight.color = flashColor;
            targetLight.intensity = flashIntensity;
            yield return new WaitForSeconds(flashDuration);
            targetLight.color = prevColor;
            targetLight.intensity = prevIntensity;
            flashing = false;
        }
    }
}
