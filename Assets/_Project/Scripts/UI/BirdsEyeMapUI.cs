using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.DemonKing;
using Game.Monster;

namespace Game.UI
{
    /// <summary>
    /// Fullscreen UI minimap that replaces the 3D birds-eye camera.
    /// Renders clickable spawner icons and live monster / hero / DK position dots,
    /// mapping world (x, z) coords to the map rect.
    /// </summary>
    public class BirdsEyeMapUI : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Vector2 mapSize = new Vector2(900f, 700f);
        [SerializeField] private float autoBoundsPadding = 15f;
        [SerializeField] private bool autoBounds = true;
        [SerializeField] private Vector2 worldMin = new Vector2(-60f, -60f);
        [SerializeField] private Vector2 worldMax = new Vector2(120f, 60f);

        [Header("Refresh")]
        [SerializeField] private float refreshInterval = 0.15f;

        private DemonKingViewSwitcher viewSwitcher;
        private GameObject root;
        private RectTransform mapArea;
        private Font font;
        private float nextRefresh;

        private struct SpawnerRow { public Spawner sp; public RectTransform rt; public Text label; }
        private readonly List<SpawnerRow> spawnerIcons = new List<SpawnerRow>();
        private readonly Dictionary<Transform, RectTransform> liveIcons = new Dictionary<Transform, RectTransform>();

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildRoot();
            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            var dk = GameObject.FindWithTag("DemonKing");
            if (dk != null) viewSwitcher = dk.GetComponent<DemonKingViewSwitcher>();
            if (viewSwitcher != null) viewSwitcher.OnModeChanged += OnModeChanged;

            if (autoBounds) AutoComputeBounds();
            BuildSpawnerIcons();
            BuildLandmark("StartTown", new Color(0.3f, 0.8f, 1f, 0.95f), 34f);
            BuildLandmark("DemonCastle", new Color(0.7f, 0.3f, 1f, 0.95f), 34f);
        }

        private void OnDestroy()
        {
            if (viewSwitcher != null) viewSwitcher.OnModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(DemonKingViewSwitcher.ViewMode mode)
        {
            bool show = mode == DemonKingViewSwitcher.ViewMode.BirdsEye;
            if (root != null) root.SetActive(show);
            if (show)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshLive();
                RefreshSpawnerLabels();
            }
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            RefreshLive();
            RefreshSpawnerLabels();
        }

        private void BuildRoot()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) { Debug.LogWarning("[BirdsEyeMapUI] no Canvas in scene"); return; }
            root = new GameObject("BirdsEyeMap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bgImg = root.GetComponent<Image>();
            bgImg.color = new Color(0.02f, 0.04f, 0.08f, 0.97f);
            bgImg.raycastTarget = true; // swallow clicks outside the map

            var areaGO = new GameObject("MapArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            areaGO.transform.SetParent(root.transform, false);
            mapArea = (RectTransform)areaGO.transform;
            mapArea.anchorMin = new Vector2(0.5f, 0.5f); mapArea.anchorMax = new Vector2(0.5f, 0.5f); mapArea.pivot = new Vector2(0.5f, 0.5f);
            mapArea.sizeDelta = mapSize;
            mapArea.anchoredPosition = Vector2.zero;
            var areaImg = areaGO.GetComponent<Image>();
            areaImg.color = new Color(0.05f, 0.12f, 0.18f, 1f);
            areaImg.raycastTarget = true;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(root.transform, false);
            var tRT = (RectTransform)titleGO.transform;
            tRT.anchorMin = new Vector2(0.5f, 1f); tRT.anchorMax = new Vector2(0.5f, 1f); tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(900f, 32f); tRT.anchoredPosition = new Vector2(0f, -16f);
            var tT = titleGO.GetComponent<Text>();
            tT.font = font; tT.fontSize = 18; tT.fontStyle = FontStyle.Bold; tT.alignment = TextAnchor.MiddleCenter;
            tT.color = new Color(1f, 0.85f, 0.4f, 1f);
            tT.text = "俯瞰マップ - スポナーをクリックで操作 / V キーで TPS に戻る";
            tT.raycastTarget = false;
        }

        private void AutoComputeBounds()
        {
            Vector2 mn = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 mx = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool any = false;
            foreach (var go in GameObject.FindGameObjectsWithTag("Spawner")) AccumulateBounds(go.transform.position, ref mn, ref mx, ref any);
            var st = GameObject.Find("StartTown"); if (st != null) AccumulateBounds(st.transform.position, ref mn, ref mx, ref any);
            var dc = GameObject.Find("DemonCastle"); if (dc != null) AccumulateBounds(dc.transform.position, ref mn, ref mx, ref any);
            if (any)
            {
                worldMin = mn - Vector2.one * autoBoundsPadding;
                worldMax = mx + Vector2.one * autoBoundsPadding;
            }
        }

        private static void AccumulateBounds(Vector3 p, ref Vector2 mn, ref Vector2 mx, ref bool any)
        {
            mn.x = Mathf.Min(mn.x, p.x); mn.y = Mathf.Min(mn.y, p.z);
            mx.x = Mathf.Max(mx.x, p.x); mx.y = Mathf.Max(mx.y, p.z);
            any = true;
        }

        private Vector2 WorldToMap(Vector3 world)
        {
            if (mapArea == null) return Vector2.zero;
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, world.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, world.z);
            Rect r = mapArea.rect;
            return new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v));
        }

        private void BuildSpawnerIcons()
        {
            if (mapArea == null) return;
            foreach (var go in GameObject.FindGameObjectsWithTag("Spawner"))
            {
                var sp = go.GetComponent<Spawner>();
                if (sp == null) continue;
                var iconGO = new GameObject("Sp_" + sp.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                iconGO.transform.SetParent(mapArea, false);
                var rt = (RectTransform)iconGO.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(54f, 54f);
                rt.anchoredPosition = WorldToMap(sp.transform.position);
                var img = iconGO.GetComponent<Image>();
                img.color = new Color(0.85f, 0.4f, 0.15f, 0.95f);

                var lblGO = new GameObject("Lbl", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGO.transform.SetParent(iconGO.transform, false);
                var lRT = (RectTransform)lblGO.transform;
                lRT.anchorMin = new Vector2(0.5f, 0f); lRT.anchorMax = new Vector2(0.5f, 0f); lRT.pivot = new Vector2(0.5f, 1f);
                lRT.sizeDelta = new Vector2(140f, 36f); lRT.anchoredPosition = new Vector2(0f, -2f);
                var lt = lblGO.GetComponent<Text>();
                lt.font = font; lt.fontSize = 12; lt.fontStyle = FontStyle.Bold;
                lt.alignment = TextAnchor.UpperCenter; lt.color = Color.white; lt.raycastTarget = false;
                lt.text = sp.name;

                var sp2 = sp; var vs = viewSwitcher;
                iconGO.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Debug.Log("[BirdsEyeMapUI] clicked " + sp2.name);
                    var popup = SpawnerActionPopup.GetOrCreate();
                    if (popup != null) popup.Show(sp2, vs);
                });

                spawnerIcons.Add(new SpawnerRow { sp = sp, rt = rt, label = lt });
            }
            Debug.Log("[BirdsEyeMapUI] built " + spawnerIcons.Count + " spawner icons");
        }

        private void BuildLandmark(string name, Color color, float size)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            var iconGO = new GameObject("Mk_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(mapArea, false);
            var rt = (RectTransform)iconGO.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = WorldToMap(go.transform.position);
            var img = iconGO.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;

            var lblGO = new GameObject("Lbl", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            lblGO.transform.SetParent(iconGO.transform, false);
            var lRT = (RectTransform)lblGO.transform;
            lRT.anchorMin = new Vector2(0.5f, 0f); lRT.anchorMax = new Vector2(0.5f, 0f); lRT.pivot = new Vector2(0.5f, 1f);
            lRT.sizeDelta = new Vector2(140f, 24f); lRT.anchoredPosition = new Vector2(0f, -2f);
            var lt = lblGO.GetComponent<Text>();
            lt.font = font; lt.fontSize = 11; lt.fontStyle = FontStyle.Bold;
            lt.alignment = TextAnchor.UpperCenter; lt.color = Color.white; lt.raycastTarget = false;
            lt.text = name;
        }

        private void RefreshSpawnerLabels()
        {
            for (int i = 0; i < spawnerIcons.Count; i++)
            {
                var row = spawnerIcons[i];
                if (row.sp == null || row.label == null) continue;
                var hc = row.sp.GetComponent<Game.Combat.HealthComponent>();
                string hp = hc != null ? hc.CurrentHP + "/" + hc.MaxHP : "?/?";
                row.label.text = row.sp.name + "\nHP " + hp + "\nCost " + row.sp.CurrentCost + "/" + row.sp.MaxCost;
                if (row.rt != null) row.rt.anchoredPosition = WorldToMap(row.sp.transform.position);
            }
        }

        private RectTransform GetOrCreateLiveIcon(Transform t, Color color, float size)
        {
            if (liveIcons.TryGetValue(t, out var rt) && rt != null)
            {
                var imgExisting = rt.GetComponent<Image>();
                if (imgExisting != null) imgExisting.color = color;
                rt.sizeDelta = new Vector2(size, size);
                return rt;
            }
            var go = new GameObject("Live_" + t.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(mapArea, false);
            rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            liveIcons[t] = rt;
            return rt;
        }

        private void RefreshLive()
        {
            // Remove stale icons whose Transform was destroyed or deactivated.
            List<Transform> stale = null;
            foreach (var kv in liveIcons)
            {
                if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy)
                {
                    if (stale == null) stale = new List<Transform>();
                    stale.Add(kv.Key);
                }
            }
            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    if (liveIcons.TryGetValue(stale[i], out var rt) && rt != null) Destroy(rt.gameObject);
                    liveIcons.Remove(stale[i]);
                }
            }

            // Monsters (red dots)
            foreach (var m in FindObjectsOfType<Game.Monster.Monster>())
            {
                if (m == null) continue;
                var rt = GetOrCreateLiveIcon(m.transform, new Color(1f, 0.35f, 0.35f, 0.95f), 12f);
                rt.anchoredPosition = WorldToMap(m.transform.position);
            }

            // Heroes (cyan dots)
            foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (p == null) continue;
                var rt = GetOrCreateLiveIcon(p.transform, new Color(0.3f, 0.8f, 1f, 0.95f), 16f);
                rt.anchoredPosition = WorldToMap(p.transform.position);
            }

            // DK (magenta dot)
            var dk = GameObject.FindWithTag("DemonKing");
            if (dk != null)
            {
                var rt = GetOrCreateLiveIcon(dk.transform, new Color(0.9f, 0.25f, 0.6f, 0.95f), 20f);
                rt.anchoredPosition = WorldToMap(dk.transform.position);
            }
        }
    }
}
