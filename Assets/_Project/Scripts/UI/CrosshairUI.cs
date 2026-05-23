using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Hero;

namespace Game.UI
{
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private Image horizontal;
        [SerializeField] private Image vertical;
        [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 0.4f);
        [SerializeField] private Color hitColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);
        [SerializeField] private float pollInterval = 0.05f;
        [SerializeField] private float defaultRange = 10f;
        [SerializeField] private float mageRange = 20f;

        private Transform cameraTransform;
        private HeroClassInitializer classInit;
        private float nextPollTime;
        private bool currentHit;

        private void Start()
        {
            BindHero();
            SetColor(false);
        }

        private void BindHero()
        {
            var hero = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : GameObject.FindWithTag("Player");
            if (hero == null) return;
            classInit = hero.GetComponent<HeroClassInitializer>();
            var camChild = hero.transform.Find("Camera");
            if (camChild != null) cameraTransform = camChild;
        }

        private void Update()
        {
            if (Time.time < nextPollTime) return;
            nextPollTime = Time.time + pollInterval;
            if (cameraTransform == null) { BindHero(); if (cameraTransform == null) return; }
            float range = defaultRange;
            if (classInit != null && classInit.ClassData != null && classInit.ClassData.className == "Mage") range = mageRange;
            bool hit = false;
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var info, range))
            {
                var hp = info.collider.GetComponentInParent<HealthComponent>();
                if (hp != null && hp.gameObject != classInit.gameObject) hit = true;
            }
            if (hit != currentHit) { currentHit = hit; SetColor(hit); }
        }

        private void SetColor(bool hit)
        {
            var c = hit ? hitColor : normalColor;
            if (horizontal != null) horizontal.color = c;
            if (vertical != null) vertical.color = c;
        }
    }
}
