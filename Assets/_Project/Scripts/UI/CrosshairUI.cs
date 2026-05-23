using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Hero;

namespace Game.UI
{
    /// <summary>
    /// Center-screen "+" crosshair for the hero player.
    /// Self-bootstraps: call EnsureExists() once and a Canvas+UI is created.
    ///
    /// Normal state: light gray, low opacity.
    /// "Hit" state (aiming at an enemy within attack range, OR class is Mage):
    /// darker gray, higher opacity.
    /// Mage is always treated as hit because the magic projectile has spread/travel,
    /// so the crosshair acts as a "ready" indicator rather than a precise hit marker.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [SerializeField] private Image horizontal;
        [SerializeField] private Image vertical;
        [SerializeField] private Color normalColor = new Color(0.75f, 0.75f, 0.75f, 0.45f);
        [SerializeField] private Color hitColor = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        [SerializeField] private float pollInterval = 0.05f;
        [SerializeField] private float defaultRange = 10f;
        [SerializeField] private float mageRange = 20f;
        // Geometry: previous values were length=14, thickness=2.
        // Scale up ~1.75x (length 14 -> 24) and use a thicker base bar.
        [SerializeField] private int armLength = 24;
        [SerializeField] private int armThicknessNormal = 4;
        [SerializeField] private int armThicknessHit = 7;
        [SerializeField] private int sortingOrder = 5;

        private Transform cameraTransform;
        private HeroClassInitializer classInit;
        private float nextPollTime;
        private bool currentHit;
        private static CrosshairUI _instance;

        /// <summary>
        /// Ensure a single CrosshairUI exists in the scene. Safe to call repeatedly.
        /// </summary>
        public static CrosshairUI EnsureExists()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("CrosshairUI", typeof(RectTransform));
            _instance = go.AddComponent<CrosshairUI>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildUI();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void BuildUI()
        {
            // Find (or create) a dedicated overlay canvas. We DON'T reuse an existing
            // canvas because some lobby UIs disable theirs aggressively.
            var existing = GameObject.Find("CrosshairCanvas");
            Canvas canvas;
            if (existing != null) canvas = existing.GetComponent<Canvas>();
            else
            {
                var cGO = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = cGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                var scaler = cGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            int maxThickness = Mathf.Max(armThicknessNormal, armThicknessHit);
            rt.sizeDelta = new Vector2(armLength * 2f + maxThickness, armLength * 2f + maxThickness);
            rt.anchoredPosition = Vector2.zero;

            if (horizontal == null) horizontal = CreateBar("Horizontal", new Vector2(armLength * 2f, armThicknessNormal));
            if (vertical == null) vertical = CreateBar("Vertical", new Vector2(armThicknessNormal, armLength * 2f));
            ApplyState(false);
        }

        private Image CreateBar(string n, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = normalColor;
            img.raycastTarget = false;
            return img;
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
            if (cameraTransform == null || classInit == null) { BindHero(); if (cameraTransform == null) return; }

            bool isMage = classInit != null && classInit.ClassData != null && classInit.ClassData.className == "Mage";
            if (isMage)
            {
                if (!currentHit) { currentHit = true; ApplyState(true); }
                return;
            }

            float range = defaultRange;
            bool hit = false;
            if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var info, range))
            {
                var hp = info.collider.GetComponentInParent<HealthComponent>();
                if (hp != null && classInit != null && hp.gameObject != classInit.gameObject) hit = true;
            }
            if (hit != currentHit) { currentHit = hit; ApplyState(hit); }
        }

        private void ApplyState(bool hit)
        {
            var c = hit ? hitColor : normalColor;
            int t = hit ? armThicknessHit : armThicknessNormal;
            if (horizontal != null)
            {
                horizontal.color = c;
                ((RectTransform)horizontal.transform).sizeDelta = new Vector2(armLength * 2f, t);
            }
            if (vertical != null)
            {
                vertical.color = c;
                ((RectTransform)vertical.transform).sizeDelta = new Vector2(t, armLength * 2f);
            }
        }
    }
}
