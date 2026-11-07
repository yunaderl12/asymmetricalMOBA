using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.DemonKing;
using Game.Monster;

namespace Game.UI
{
    /// 魔王の俯瞰画面。中央に地図、左に魔王の様子と探知ボタン、右に塔と橋の番人の一覧（Evil Genius 2 の作業画面にならう）。
    /// 地図の背景は、Blender で描いた絵地図（Resources/UI/<地図>_Illust.png。南から仰角60度で見下ろした絵）。
    /// 絵地図のないワールドでは、開くたびに真上から地形だけを撮る（魔物・勇者・雲・頭上のバーは写さない）。印はその上に重ねる。
    /// 勇者は、動いている塔の検知範囲に入ったとき・探知中・城内にいるときだけ出す。
    public class BirdsEyeMapUI : MonoBehaviour
    {
        private const float MapPixels = 1000f;      // 撮った地図の一辺。絵地図は高さをこれより少し低くし、幅は絵の比率で決める
        private const float IllustHeight = 960f;
        private const int SnapshotSize = 1024;
        private const float RefreshInterval = 0.15f;
        private const float WarningSeconds = 3f;
        private const float MonsterDot = 16f;
        private const float HeroIcon = 54f;
        private const float GuardianIcon = 32f;
        private const float TowerIcon = 38f;

        private static readonly Color SeaColor = new Color(0.22f, 0.45f, 0.78f, 1f);
        private static readonly Color MapTint = new Color(0.12f, 0.03f, 0.07f, 0.16f);
        private static readonly Color Panel = HudStyle.Hex(0x2a1820);

        private static BirdsEyeMapUI _instance;
        public static BirdsEyeMapUI EnsureExists()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("BirdsEyeMapUI", typeof(RectTransform));
            _instance = go.AddComponent<BirdsEyeMapUI>();
            return _instance;
        }

        private readonly HudStyle.Palette pal = HudStyle.DemonPalette;

        private DemonKingViewSwitcher viewSwitcher;
        private DemonKingDetect detect;
        private DemonCastleRegen castle;
        private GameObject dk;
        private HealthComponent dkHealth, town, core;
        private ManaComponent dkMana;
        private LevelComponent dkLevel;

        private GameObject root;
        private RectTransform mapArea;
        private RawImage mapImage;
        private RenderTexture snapshot;
        private Vector2 worldMin = new Vector2(-250f, -250f);
        private Vector2 worldMax = new Vector2(250f, 250f);
        private Vector2 mapSize = new Vector2(MapPixels, MapPixels);
        private Illust illust;

        /// render_map.py が書き出す、ゲームの座標から絵の位置への変換。
        [System.Serializable]
        private class Illust
        {
            public float width, height, centerX, centerScreenY, upZ, upY;
        }

        private static readonly Color LabelPaper = HudStyle.Hex(0xf6ead0);
        private static readonly Color LabelInk = HudStyle.Hex(0x3a2414);

        private Text clockText, nextLevelText, levelText, hpText, mpText, townText, coreText;
        private HudBar hpBar, mpBar, townBar, coreBar;
        private Button detectButton;
        private HudShape detectBody;
        private Text detectTitle, detectInfo;
        private GameObject warningGO;
        private float warningHideAt;
        private float nextRefresh;

        private class TowerMark
        {
            public Spawner Spawner;
            public HealthComponent Health;
            public RectTransform Icon;
            public HudShape Fill;
            public Text Row;
            public HudBar RowBar;
        }

        private Text[] guardianTexts;
        private bool[] guardianSeen;

        // 地図の印の重なり順。魔物の点の上に塔、その上に勇者と魔王、いちばん上に名前。
        private RectTransform monsterLayer, towerLayer, heroLayer, labelLayer;

        private readonly List<TowerMark> towers = new List<TowerMark>();
        private readonly Dictionary<Transform, RectTransform> liveIcons = new Dictionary<Transform, RectTransform>();
        private readonly List<Transform> stale = new List<Transform>();
        private RectTransform dkIcon;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EnsureEventSystem();
            BuildRoot();
            root.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Debug.Log("[BirdsEyeMapUI] created EventSystem (none existed)");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (viewSwitcher != null) viewSwitcher.OnModeChanged -= OnModeChanged;
            DemonCastleRegen.OnHeroEnteredCastle -= OnHeroEnteredCastle;
            if (snapshot != null) { snapshot.Release(); Destroy(snapshot); }
        }

        private void Start()
        {
            dk = GameObject.FindWithTag("DemonKing");
            if (dk != null)
            {
                viewSwitcher = dk.GetComponent<DemonKingViewSwitcher>();
                detect = dk.GetComponent<DemonKingDetect>();
                if (detect == null) detect = dk.AddComponent<DemonKingDetect>();
                dkHealth = dk.GetComponent<HealthComponent>();
                dkMana = dk.GetComponent<ManaComponent>();
                dkLevel = dk.GetComponent<LevelComponent>();
            }
            if (viewSwitcher != null) viewSwitcher.OnModeChanged += OnModeChanged;
            castle = FindFirstObjectByType<DemonCastleRegen>();
            town = FindHealth("StartTown");
            core = FindHealth("DemonCastle");
            // 侵入の警告は人間の魔王だけ（TPS中にも出す）。
            if (viewSwitcher != null) DemonCastleRegen.OnHeroEnteredCastle += OnHeroEnteredCastle;

            ComputeBounds();
            BuildTowers();
            BuildGuardianList();
            BuildLandmark(GameObject.Find("StartTown"), "はじまりのまち");
            BuildLandmark(GameObject.Find("DemonCastle"), "魔王城");
            BuildAreaLabels();
        }

        private static HealthComponent FindHealth(string tag)
        {
            var go = GameObject.FindWithTag(tag);
            if (go == null) return null;
            var hc = go.GetComponent<HealthComponent>();
            return hc != null ? hc : go.GetComponentInChildren<HealthComponent>();
        }

        private void OnModeChanged(DemonKingViewSwitcher.ViewMode mode)
        {
            bool show = mode == DemonKingViewSwitcher.ViewMode.BirdsEye;
            root.SetActive(show);
            if (!show) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (illust == null) TakeSnapshot();
            RefreshLive();
            RefreshPanels();
        }

        private void Update()
        {
            if (warningGO.activeSelf && Time.unscaledTime >= warningHideAt) warningGO.SetActive(false);
            if (!root.activeSelf) return;
            // 位置は毎フレーム追う。文字は変わるのが遅いので間引く。
            // （Esc は塔の上空視点から俯瞰へ戻るのに使うので、ここでは拾わない。俯瞰を閉じるのは V。）
            RefreshLive();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            RefreshPanels();
        }

        // ---- 地図の背景 ----

        /// 地図の範囲。StageMap の大きさ（原点が中心）を使い、なければ塔・まち・城から決める。
        private void ComputeBounds()
        {
            var map = StageMap.Map;
            if (map != null && map.size > 0f)
            {
                worldMin = new Vector2(-map.size * 0.5f, -map.size * 0.5f);
                worldMax = new Vector2(map.size * 0.5f, map.size * 0.5f);
                return;
            }
            Vector2 mn = new Vector2(float.PositiveInfinity, float.PositiveInfinity), mx = -mn;
            bool any = false;
            foreach (var sp in Spawner.All) Grow(sp.transform.position, ref mn, ref mx, ref any);
            var st = GameObject.Find("StartTown"); if (st != null) Grow(st.transform.position, ref mn, ref mx, ref any);
            var dc = GameObject.Find("DemonCastle"); if (dc != null) Grow(dc.transform.position, ref mn, ref mx, ref any);
            if (!any) return;
            // 正方形にそろえて余白をつける
            Vector2 c = (mn + mx) * 0.5f;
            float half = Mathf.Max(mx.x - mn.x, mx.y - mn.y) * 0.5f + 20f;
            worldMin = c - Vector2.one * half;
            worldMax = c + Vector2.one * half;
        }

        private static void Grow(Vector3 p, ref Vector2 mn, ref Vector2 mx, ref bool any)
        {
            mn = Vector2.Min(mn, new Vector2(p.x, p.z));
            mx = Vector2.Max(mx, new Vector2(p.x, p.z));
            any = true;
        }

        /// 真上から地形を撮る。動くもの・雲・頭上のバーは一時的に消して写さない。
        private void TakeSnapshot()
        {
            if (snapshot == null)
            {
                snapshot = new RenderTexture(SnapshotSize, SnapshotSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "BirdsEyeSnapshot", antiAliasing = 2 };
                mapImage.texture = snapshot;
            }

            var hidden = new List<Renderer>();
            foreach (var tag in new[] { "Monster", "Player", "DemonKing" })
                foreach (var go in GameObject.FindGameObjectsWithTag(tag))
                    CollectEnabled(go.GetComponentsInChildren<Renderer>(), hidden);
            foreach (var clouds in FindObjectsByType<StormClouds>(FindObjectsSortMode.None))
                CollectEnabled(clouds.GetComponentsInChildren<Renderer>(), hidden);
            var canvases = new List<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.WorldSpace && c.enabled) { c.enabled = false; canvases.Add(c); }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;

            var camGO = new GameObject("BirdsEyeSnapshotCamera");
            try
            {
                var cam = camGO.AddComponent<Camera>();
                Vector2 center = (worldMin + worldMax) * 0.5f;
                cam.orthographic = true;
                cam.orthographicSize = (worldMax.y - worldMin.y) * 0.5f;
                cam.aspect = 1f;
                cam.transform.position = new Vector3(center.x, 400f, center.y);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 画面の上が北（+Z）
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 900f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = SeaColor;
                cam.targetTexture = snapshot;
                cam.enabled = false;
                cam.Render();
                cam.targetTexture = null;
            }
            finally
            {
                Destroy(camGO);
                foreach (var r in hidden) if (r != null) r.enabled = true;
                foreach (var c in canvases) if (c != null) c.enabled = true;
                RenderSettings.fog = fog;
            }
        }

        private static void CollectEnabled(Renderer[] renderers, List<Renderer> into)
        {
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                into.Add(r);
            }
        }

        private Vector2 WorldToMap(Vector3 world)
        {
            if (illust != null)
            {
                float iu = (world.x - illust.centerX) / illust.width;
                float iv = (world.z * illust.upZ + world.y * illust.upY - illust.centerScreenY) / illust.height;
                return new Vector2(iu * mapSize.x, iv * mapSize.y);
            }
            float u = Mathf.InverseLerp(worldMin.x, worldMax.x, world.x);
            float v = Mathf.InverseLerp(worldMin.y, worldMax.y, world.z);
            return new Vector2((u - 0.5f) * MapPixels, (v - 0.5f) * MapPixels);
        }

        /// 俯瞰画面が出ているか。
        public static BirdsEyeMapUI Instance => _instance;
        public bool IsOpen => root != null && root.activeSelf;

        /// 画面の位置（マウス）から地図の上の地点（ゲームの座標）。地図の外なら false。
        /// 絵地図は高さで上下にずれるので、地形の高さを数回たどって合わせる。
        public bool ScreenToWorld(Vector2 screen, out Vector3 world)
        {
            world = Vector3.zero;
            if (!IsOpen || mapArea == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapArea, screen, null, out var local)) return false;
            if (Mathf.Abs(local.x) > mapSize.x * 0.5f || Mathf.Abs(local.y) > mapSize.y * 0.5f) return false;
            if (illust == null)
            {
                float u = local.x / MapPixels + 0.5f, v = local.y / MapPixels + 0.5f;
                world = new Vector3(Mathf.Lerp(worldMin.x, worldMax.x, u), 0f, Mathf.Lerp(worldMin.y, worldMax.y, v));
                world.y = Game.DemonKing.FieldSpellEffect.GroundY(world.x, world.z);
                return true;
            }
            float x = local.x / mapSize.x * illust.width + illust.centerX;
            float screenY = local.y / mapSize.y * illust.height + illust.centerScreenY;
            float y = 0f, z = 0f;
            for (int i = 0; i < 4; i++)
            {
                z = Mathf.Abs(illust.upZ) > 1e-4f ? (screenY - y * illust.upY) / illust.upZ : 0f;
                y = Game.DemonKing.FieldSpellEffect.GroundY(x, z);
            }
            world = new Vector3(x, y, z);
            return true;
        }

        private RectTransform aimMark;
        private HudShape aimRing;

        /// フィールド魔法の狙いの輪を地図に出す（radius はゲームの m）。show=false で消す。
        public void ShowAim(bool show, Vector3 world, float radius, Color color)
        {
            if (mapArea == null) return;
            if (aimMark == null)
            {
                aimMark = HudStyle.Place(mapArea, "FieldSpellAim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                aimRing = HudStyle.Shape(aimMark, "Ring", HudShape.Kind.Circle, color);
                aimRing.raycastTarget = false;
                var dot = HudStyle.Shape(aimMark, "Dot", HudShape.Kind.Circle, Color.white);
                dot.raycastTarget = false;
                HudStyle.Stretch(dot.rectTransform, -16f);
            }
            aimMark.gameObject.SetActive(show);
            if (!show) return;
            aimMark.SetAsLastSibling();
            float scale = illust != null ? mapSize.x / illust.width : MapPixels / Mathf.Max(1f, worldMax.x - worldMin.x);
            float size = Mathf.Max(24f, radius * 2f * scale);
            aimMark.sizeDelta = new Vector2(size, size);
            aimMark.anchoredPosition = WorldToMap(world);
            color.a *= 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
            aimRing.color = color;
        }

        // ---- 組み立て ----

        private void BuildRoot()
        {
            // 専用のキャンバス。場面のUIの片付けで消されないように、ほかとは分ける。
            var existing = GameObject.Find("BirdsEyeMapCanvas");
            Canvas canvas;
            if (existing != null) canvas = existing.GetComponent<Canvas>();
            else
            {
                var cGO = new GameObject("BirdsEyeMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = cGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 7;
                var scaler = cGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var rootRt = HudStyle.Filled(canvas.transform, "BirdsEyeMap");
            root = rootRt.gameObject;
            var bg = HudStyle.Shape(rootRt, "Background", HudShape.Kind.RoundRect, pal.Felt, 0f, HudStyle.FeltTexture);
            bg.raycastTarget = true; // 地図の外のクリックを奥へ通さない

            BuildMap(rootRt);
            BuildLeftColumn(rootRt);
            BuildRightColumn(rootRt);
            BuildWarning(canvas.transform);
        }

        private void LoadIllust()
        {
            string name = "UI/" + WorldCatalog.ForActiveScene().mapResource + "_Illust";
            var texture = Resources.Load<Texture2D>(name);
            var json = Resources.Load<TextAsset>(name);
            if (texture == null || json == null) return;
            var info = JsonUtility.FromJson<Illust>(json.text);
            if (info == null || info.width <= 0f || info.height <= 0f) return;
            illust = info;
            mapSize = new Vector2(IllustHeight * info.width / info.height, IllustHeight);
            mapImage.texture = texture;
        }

        private void BuildMap(RectTransform rootRt)
        {
            var frame = HudStyle.Place(rootRt, "MapFrame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapPixels + 24f, MapPixels + 24f));
            HudStyle.DropShadow(frame, HudShape.Kind.RoundRect, pal.Shadow, 18f, 6f);
            var frameShape = HudStyle.Shape(frame, "Frame", HudShape.Kind.RoundRect, Panel, 18f, HudStyle.FeltTexture);

            mapArea = HudStyle.Place(frame, "MapArea", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapPixels, MapPixels));
            mapImage = HudStyle.AddGraphic<RawImage>(mapArea);
            mapImage.color = Color.white;
            mapImage.raycastTarget = true;
            LoadIllust();
            if (illust != null)
            {
                // 絵地図は紙の縁で囲む。
                mapArea.sizeDelta = mapSize;
                frame.sizeDelta = mapSize + new Vector2(28f, 28f);
                frameShape.color = LabelPaper;
                frameShape.texture = HudStyle.PaperTexture;
            }
            else HudStyle.Shape(mapArea, "Tint", HudShape.Kind.RoundRect, MapTint, 0f);
            monsterLayer = HudStyle.Filled(mapArea, "Monsters");
            towerLayer = HudStyle.Filled(mapArea, "Towers");
            heroLayer = HudStyle.Filled(mapArea, "Heroes");
            labelLayer = HudStyle.Filled(mapArea, "Labels");
            HudStyle.Stitch(frame, illust != null ? LabelInk : pal.Stitch, false, 13f, 6f);
            if (illust != null) BuildCompass();
        }

        /// 地図の右下（海の上）の方位磁針。上が北。
        private void BuildCompass()
        {
            var c = HudStyle.Place(labelLayer, "Compass", new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-84f, 96f), new Vector2(120f, 120f));
            var ring = HudStyle.Shape(c, "Ring", HudShape.Kind.Circle, LabelInk);
            HudStyle.Stretch(ring.rectTransform, -22f);
            var inner = HudStyle.Shape(c, "Inner", HudShape.Kind.Circle, LabelPaper);
            HudStyle.Stretch(inner.rectTransform, -25f);
            CompassPoint(c, 0f, HudStyle.Hex(0x9a2a1c));   // 北は赤
            CompassPoint(c, 180f, LabelInk);
            CompassPoint(c, 90f, LabelInk, 0.62f);
            CompassPoint(c, 270f, LabelInk, 0.62f);
            HudStyle.Shape(HudStyle.Place(c, "Pin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f)), "Dot", HudShape.Kind.Circle, HudStyle.Gold);
            HudStyle.Label(c, "N", new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(60f, 34f), "N", 30, LabelInk, TextAnchor.LowerCenter, 3f, LabelPaper);
        }

        /// 中心から外へ向かう細いひし形を1本。angle は上（北）から時計回り。
        private static void CompassPoint(RectTransform parent, float angle, Color color, float length = 1f)
        {
            var arm = HudStyle.Place(parent, "Arm" + angle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            arm.localEulerAngles = new Vector3(0f, 0f, -angle);
            var squash = HudStyle.Place(arm, "Squash", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f * length), Vector2.zero);
            squash.localScale = new Vector3(0.28f, 1f, 1f);
            var diamond = HudStyle.Shape(squash, "Diamond", HudShape.Kind.RoundRect, color, 2f);
            diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.rectTransform.sizeDelta = new Vector2(38f, 38f) * length;
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        /// 地方の名前の札（紙に墨の枠）。場所は StageMap のバイオームの中心。
        private void BuildAreaLabels()
        {
            if (illust == null) return;
            var map = StageMap.Map;
            if (map == null || map.biomes == null) return;
            foreach (var area in map.biomes)
            {
                string label = area.name == "desert" ? "砂漠" : area.name == "forest" ? "巨大樹の森" : area.name == "mountain" ? "山脈" : null;
                if (label == null) continue;
                // 砂漠には塔が5本立つので、札は南西と南東の塔のあいだ（南寄り）に置く。
                float z = area.name == "desert" ? area.z - area.rz * 0.55f : area.z;
                PaperLabel(label, WorldToMap(new Vector3(area.x, 0f, z)), 26);
            }
        }

        private RectTransform PaperLabel(string text, Vector2 position, int size)
        {
            float w = text.Length * size + 30f, h = size + 18f;
            var box = HudStyle.Place(labelLayer, "Label_" + text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(w, h));
            HudStyle.Shape(box, "Ink", HudShape.Kind.RoundRect, LabelInk, 5f);
            var paper = HudStyle.Shape(box, "Paper", HudShape.Kind.RoundRect, LabelPaper, 4f, HudStyle.PaperTexture);
            HudStyle.Stretch(paper.rectTransform, -3f);
            HudStyle.Label(box, "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), text, size, LabelInk, TextAnchor.MiddleCenter);
            return box;
        }

        private void BuildLeftColumn(RectTransform rootRt)
        {
            var topLeft = new Vector2(0f, 1f);
            var col = HudStyle.Place(rootRt, "Left", topLeft, topLeft, new Vector2(40f, -40f), new Vector2(380f, 1000f));

            clockText = HudStyle.Label(col, "Clock", topLeft, topLeft, Vector2.zero, new Vector2(170f, 60f), "--:--", 46, pal.Light, TextAnchor.MiddleLeft, 6f, pal.Outline);
            var ribbon = HudStyle.Place(col, "NextLevel", Vector2.one, Vector2.one, new Vector2(0f, -8f), new Vector2(200f, 44f));
            HudStyle.Shape(ribbon, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            nextLevelText = HudStyle.AddText(HudStyle.Filled(ribbon, "Text"), "", 21, pal.Light, TextAnchor.MiddleCenter);

            // 魔王の様子
            var self = HudStyle.Place(col, "Self", topLeft, topLeft, new Vector2(0f, -84f), new Vector2(380f, 132f));
            HudStyle.Shape(self, "Panel", HudShape.Kind.RoundRect, Panel, 16f, HudStyle.FeltTexture);
            HudStyle.Stitch(self, pal.Stitch, false, 11f, 6f);
            HudStyle.Label(self, "Name", topLeft, topLeft, new Vector2(20f, -12f), new Vector2(90f, 32f), "魔王", 25, pal.Light, TextAnchor.MiddleLeft);
            levelText = HudStyle.Label(self, "Level", topLeft, topLeft, new Vector2(104f, -12f), new Vector2(200f, 32f), "", 21, pal.Light, TextAnchor.MiddleLeft);
            hpBar = BuildBarRow(self, "Hp", "HP", -52f, 20f, HudStyle.Hp, pal.Light, pal.Track, out hpText);
            mpBar = BuildBarRow(self, "Mp", "MP", -88f, 20f, HudStyle.Mp, pal.Light, pal.Track, out mpText);

            // まちとコア
            var bases = HudStyle.Place(col, "Bases", topLeft, topLeft, new Vector2(0f, -240f), new Vector2(380f, 112f));
            bases.localEulerAngles = new Vector3(0f, 0f, -0.6f);
            HudStyle.Paper(bases, pal, HudShape.Kind.RoundRect, 2f);
            townBar = BuildBaseRow(bases, "Town", "はじまりのまち", -12f, HudStyle.Town, out townText);
            coreBar = BuildBaseRow(bases, "Core", "魔王城コア", -60f, HudStyle.Core, out coreText);

            // 探知ボタン
            var button = HudStyle.Place(col, "DetectButton", topLeft, topLeft, new Vector2(0f, -378f), new Vector2(380f, 124f));
            HudStyle.DropShadow(button, HudShape.Kind.RoundRect, HudStyle.Hex(0x4f0e13), 16f, 6f);
            detectBody = HudStyle.Shape(button, "Body", HudShape.Kind.RoundRect, pal.Ribbon, 16f);
            detectBody.raycastTarget = true;
            detectButton = button.gameObject.AddComponent<Button>();
            detectButton.targetGraphic = detectBody;
            detectButton.transition = Selectable.Transition.None;
            detectButton.onClick.AddListener(OnDetectClicked);
            var eye = HudStyle.Place(button, "Eye", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(84f, 84f));
            BuildEye(eye);
            detectTitle = HudStyle.Label(button, "Title", topLeft, topLeft, new Vector2(118f, -12f), new Vector2(250f, 40f), "探知", 32, pal.Light, TextAnchor.MiddleLeft);
            detectInfo = HudStyle.Label(button, "Info", topLeft, topLeft, new Vector2(118f, -54f), new Vector2(250f, 30f), "", 20, pal.Light, TextAnchor.MiddleLeft);
            HudStyle.Label(button, "Effect", topLeft, topLeft, new Vector2(118f, -84f), new Vector2(250f, 28f),
                ShortSeconds(BalanceConfig.Instance.dkDetectDuration) + "秒間すべての勇者を表示", 18, pal.Light, TextAnchor.MiddleLeft);

            // 凡例
            var legend = HudStyle.Place(col, "Legend", topLeft, topLeft, new Vector2(4f, -530f), new Vector2(380f, 170f));
            LegendRow(legend, 0, "塔", r => TowerShape(r, pal.Paper, pal.Outline));
            LegendRow(legend, 1, "魔物", r => MonsterShape(r));
            LegendRow(legend, 2, "橋の番人", r => GuardianShape(r));
            LegendRow(legend, 3, "見つけた勇者", r => { HudStyle.Shape(r, "Ring", HudShape.Kind.Circle, HudStyle.Gold); });
        }

        private HudBar BuildBarRow(RectTransform parent, string name, string label, float y, float height, Color color, Color text, Color track, out Text value)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(parent, name, topLeft, topLeft, new Vector2(20f, y), new Vector2(340f, 28f));
            HudStyle.Label(row, "Label", topLeft, topLeft, Vector2.zero, new Vector2(40f, 28f), label, 20, text, TextAnchor.MiddleLeft);
            value = HudStyle.Label(row, "Value", Vector2.one, Vector2.one, Vector2.zero, new Vector2(140f, 28f), "", 20, text, TextAnchor.MiddleRight);
            var bar = HudStyle.Place(row, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(166f, height));
            return HudBar.Create(bar, color, track);
        }

        private HudBar BuildBaseRow(RectTransform parent, string name, string title, float y, Color color, out Text value)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(parent, name, topLeft, topLeft, new Vector2(18f, y), new Vector2(344f, 46f));
            HudStyle.Label(row, "Title", topLeft, topLeft, Vector2.zero, new Vector2(240f, 24f), title, 20, pal.Ink, TextAnchor.MiddleLeft);
            value = HudStyle.Label(row, "Value", Vector2.one, Vector2.one, Vector2.zero, new Vector2(110f, 24f), "", 20, pal.Ink, TextAnchor.MiddleRight);
            var bar = HudStyle.Place(row, "Bar", topLeft, topLeft, new Vector2(0f, -28f), new Vector2(344f, 14f));
            return HudBar.Create(bar, color, pal.Ink);
        }

        private void LegendRow(RectTransform legend, int index, string label, System.Action<RectTransform> icon)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(legend, "Legend_" + index, topLeft, topLeft, new Vector2(0f, -index * 40f), new Vector2(380f, 32f));
            var mark = HudStyle.Place(row, "Mark", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(26f, 26f));
            icon(mark);
            HudStyle.Label(row, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(300f, 32f), label, 21, pal.Light, TextAnchor.MiddleLeft);
        }

        private void BuildRightColumn(RectTransform rootRt)
        {
            var topRight = Vector2.one;
            var col = HudStyle.Place(rootRt, "Right", topRight, topRight, new Vector2(-40f, -40f), new Vector2(380f, 1000f));
            var topLeft = new Vector2(0f, 1f);

            // 塔の一覧。行をクリックすると、地図の塔と同じく操作の窓を開く。
            var list = HudStyle.Place(col, "Towers", topLeft, topLeft, Vector2.zero, new Vector2(380f, 300f));
            list.name = "TowerList";
            HudStyle.Paper(list, pal, HudShape.Kind.RoundRect, 2f);
            var ribbon = HudStyle.Place(list, "Title", topLeft, topLeft, new Vector2(-10f, -10f), new Vector2(170f, 46f));
            HudStyle.Shape(ribbon, "Ribbon", HudShape.Kind.Ribbon, pal.Ribbon);
            HudStyle.AddText(HudStyle.Filled(ribbon, "Text"), "塔", 24, pal.Light, TextAnchor.MiddleCenter);
            HudStyle.Label(list, "Hint", Vector2.one, Vector2.one, new Vector2(-16f, -18f), new Vector2(190f, 30f), "クリックで編成", 18, HudStyle.Brown, TextAnchor.MiddleRight);

            // 橋の番人
            var guards = HudStyle.Place(col, "Guardians", topLeft, topLeft, new Vector2(0f, -330f), new Vector2(380f, 170f));
            HudStyle.Shape(guards, "Panel", HudShape.Kind.RoundRect, Panel, 16f, HudStyle.FeltTexture);
            HudStyle.Stitch(guards, pal.Stitch, false, 11f, 6f);
            HudStyle.Label(guards, "Title", topLeft, topLeft, new Vector2(20f, -12f), new Vector2(300f, 32f), "橋の番人", 23, pal.Light, TextAnchor.MiddleLeft);

            // 操作の案内
            var keys = HudStyle.Place(col, "Keys", topLeft, topLeft, new Vector2(0f, -530f), new Vector2(380f, 134f));
            KeyHint(keys, 0, "V", "もどる");
            KeyHint(keys, 1, "Esc", "塔の上空から戻る");
            KeyHint(keys, 2, "1-5", "フィールド魔法（地図をクリック）");
        }

        private void KeyHint(RectTransform parent, int index, string key, string label)
        {
            var topLeft = new Vector2(0f, 1f);
            var row = HudStyle.Place(parent, "Key_" + index, topLeft, topLeft, new Vector2(0f, -index * 44f), new Vector2(380f, 36f));
            var cap = HudStyle.Place(row, "Cap", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(key.Length > 1 ? 62f : 40f, 34f));
            HudStyle.Shape(cap, "Cap", HudShape.Kind.RoundRect, pal.Light, 8f, HudStyle.PaperTexture);
            HudStyle.AddText(HudStyle.Filled(cap, "Text"), key, 20, pal.Ink, TextAnchor.MiddleCenter);
            HudStyle.Label(row, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(300f, 34f), label, 21, pal.Light, TextAnchor.MiddleLeft);
        }

        private void BuildWarning(Transform canvas)
        {
            var rt = HudStyle.Place(canvas, "CastleIntrusionWarning", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(620f, 60f));
            HudStyle.DropShadow(rt, HudShape.Kind.Ribbon, pal.Shadow, 0f, 5f);
            HudStyle.Shape(rt, "Ribbon", HudShape.Kind.Ribbon, HudStyle.Hex(0xc9202a));
            HudStyle.AddText(HudStyle.Filled(rt, "Text"), "勇者が魔王城に侵入しました", 27, Color.white, TextAnchor.MiddleCenter, 4f, pal.Outline);
            warningGO = rt.gameObject;
            warningGO.SetActive(false);
        }

        // ---- 地図の印 ----

        private void BuildTowers()
        {
            var spawners = new List<Spawner>(Spawner.All);
            spawners.Sort(HudStyle.CompareTowers);
            var list = (RectTransform)root.transform.Find("Right/TowerList");
            var topLeft = new Vector2(0f, 1f);
            int rows = Mathf.Min(spawners.Count, 6);
            list.sizeDelta = new Vector2(380f, 74f + rows * 40f);

            for (int i = 0; i < spawners.Count; i++)
            {
                var sp = spawners[i];
                var mark = new TowerMark { Spawner = sp, Health = sp.GetComponent<HealthComponent>() };

                // 地図の上の印
                mark.Icon = HudStyle.Place(towerLayer, "Tower_" + sp.name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), WorldToMap(sp.transform.position), new Vector2(TowerIcon, TowerIcon));
                mark.Fill = TowerShape(mark.Icon, pal.Paper, pal.Outline);
                var hit = HudStyle.Shape(mark.Icon, "Hit", HudShape.Kind.Circle, new Color(0f, 0f, 0f, 0f));
                HudStyle.Stretch(hit.rectTransform, 10f);
                hit.raycastTarget = true;
                AddOpenPopup(mark.Icon.gameObject, hit, sp);
                HudStyle.Label(mark.Icon, "Name", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(120f, 28f),
                    HudStyle.TowerLabel(sp.name), 20, pal.Light, TextAnchor.UpperCenter, 4f, pal.Outline);

                // 右の一覧の行
                if (i >= rows) { towers.Add(mark); continue; }
                var row = HudStyle.Place(list, "Row_" + i, topLeft, topLeft, new Vector2(16f, -64f - i * 40f), new Vector2(348f, 34f));
                var rowHit = HudStyle.Shape(row, "Hit", HudShape.Kind.RoundRect, new Color(0.56f, 0.11f, 0.13f, 0.08f), 8f);
                rowHit.raycastTarget = true;
                AddOpenPopup(row.gameObject, rowHit, sp);
                HudStyle.Label(row, "Name", topLeft, topLeft, new Vector2(8f, 0f), new Vector2(70f, 34f), HudStyle.TowerLabel(sp.name), 21, pal.Ink, TextAnchor.MiddleLeft);
                var bar = HudStyle.Place(row, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(150f, 14f));
                mark.RowBar = HudBar.Create(bar, HudStyle.Hp, pal.Ink);
                mark.Row = HudStyle.Label(row, "Cost", Vector2.one, Vector2.one, new Vector2(-8f, 0f), new Vector2(100f, 34f), "", 20, pal.Ink, TextAnchor.MiddleRight);
                towers.Add(mark);
            }
        }

        private void AddOpenPopup(GameObject go, Graphic target, Spawner sp)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            var vs = viewSwitcher;
            button.onClick.AddListener(() =>
            {
                var popup = SpawnerActionPopup.GetOrCreate();
                if (popup == null) { Debug.LogError("[BirdsEyeMapUI] SpawnerActionPopup.GetOrCreate returned null"); return; }
                popup.Show(sp, vs);
            });
        }

        private void BuildGuardianList()
        {
            var panel = (RectTransform)root.transform.Find("Right/Guardians");
            var topLeft = new Vector2(0f, 1f);
            string[] bridges = { "山の橋", "砂漠の橋", "森の橋" };
            guardianTexts = new Text[bridges.Length];
            guardianSeen = new bool[bridges.Length];
            for (int i = 0; i < bridges.Length; i++)
            {
                var row = HudStyle.Place(panel, "Bridge_" + i, topLeft, topLeft, new Vector2(20f, -50f - i * 36f), new Vector2(340f, 32f));
                HudStyle.Label(row, "Bridge", topLeft, topLeft, Vector2.zero, new Vector2(160f, 32f), bridges[i], 21, pal.Light, TextAnchor.MiddleLeft);
                guardianTexts[i] = HudStyle.Label(row, "Guardian", Vector2.one, Vector2.one, Vector2.zero, new Vector2(180f, 32f), "なし", 21, pal.Light, TextAnchor.MiddleRight);
            }
        }

        /// 橋の番人は BridgeGuardianSpawner が「BridgeGuardian_番号」の名前で置く（選ばなかった橋には既定の番人）。
        /// 一度いた番人がいなくなったら倒されたと見なす。
        private void RefreshGuardians()
        {
            if (guardianTexts == null) return;
            for (int i = 0; i < guardianTexts.Length; i++)
            {
                var go = GameObject.Find("BridgeGuardian_" + i);
                var monster = go != null ? go.GetComponent<Game.Monster.Monster>() : null;
                var hc = go != null ? go.GetComponent<HealthComponent>() : null;
                bool alive = monster != null && (hc == null || !hc.IsDead);
                if (alive) guardianSeen[i] = true;
                string text = alive ? (monster.Data != null ? monster.Data.monsterName : "番人") : (guardianSeen[i] ? "倒された" : "なし");
                if (guardianTexts[i].text != text) guardianTexts[i].text = text;
            }
        }

        private void BuildLandmark(GameObject go, string label)
        {
            if (go == null) return;
            if (illust != null) { PaperLabel(label, WorldToMap(go.transform.position) + new Vector2(0f, 52f), 24); return; }
            HudStyle.Label(labelLayer, "Landmark_" + label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), WorldToMap(go.transform.position) + new Vector2(0f, 46f), new Vector2(240f, 32f),
                label, 22, pal.Light, TextAnchor.MiddleCenter, 5f, pal.Outline);
        }

        private static HudShape TowerShape(RectTransform parent, Color fill, Color outline)
        {
            var outer = HudStyle.Shape(parent, "Outline", HudShape.Kind.RoundRect, outline, 3f);
            outer.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            HudStyle.Stretch(outer.rectTransform, -parent.sizeDelta.x * 0.15f);
            var inner = HudStyle.Shape(parent, "Fill", HudShape.Kind.RoundRect, fill, 2f);
            inner.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            HudStyle.Stretch(inner.rectTransform, -parent.sizeDelta.x * 0.15f - 4f);
            return inner;
        }

        private static void MonsterShape(RectTransform parent)
        {
            HudStyle.Shape(parent, "Outline", HudShape.Kind.Circle, HudStyle.Hex(0x140b10));
            var fill = HudStyle.Shape(parent, "Fill", HudShape.Kind.Circle, HudStyle.Hex(0xff5a4a));
            HudStyle.Stretch(fill.rectTransform, -3f);
        }

        private void GuardianShape(RectTransform parent)
        {
            HudStyle.Shape(parent, "Border", HudShape.Kind.RoundRect, pal.Light, 7f);
            var fill = HudStyle.Shape(parent, "Fill", HudShape.Kind.RoundRect, HudStyle.Hex(0x6b6f7a), 5f);
            HudStyle.Stretch(fill.rectTransform, -3f);
        }

        private static void BuildEye(RectTransform icon)
        {
            var white = HudStyle.Shape(icon, "White", HudShape.Kind.Circle, Color.white);
            white.rectTransform.anchorMin = new Vector2(0f, 0.22f);
            white.rectTransform.anchorMax = new Vector2(1f, 0.78f);
            var iris = HudStyle.Shape(icon, "Iris", HudShape.Kind.Circle, HudStyle.Hex(0x2c7be0));
            HudStyle.Stretch(iris.rectTransform, -icon.sizeDelta.x * 0.29f);
            var pupil = HudStyle.Shape(icon, "Pupil", HudShape.Kind.Circle, HudStyle.Hex(0x10131c));
            HudStyle.Stretch(pupil.rectTransform, -icon.sizeDelta.x * 0.4f);
        }

        // ---- 毎フレームの更新 ----

        private void RefreshLive()
        {
            stale.Clear();
            foreach (var kv in liveIcons)
                if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy) stale.Add(kv.Key);
            foreach (var t in stale)
            {
                if (liveIcons.TryGetValue(t, out var rt) && rt != null) Destroy(rt.gameObject);
                liveIcons.Remove(t);
            }

            foreach (var m in FindObjectsByType<Game.Monster.Monster>(FindObjectsSortMode.None))
            {
                if (m == null) continue;
                bool guardian = m.GetComponent<BridgeGuardian>() != null;
                var rt = GetOrCreate(m.transform, monsterLayer, guardian ? GuardianIcon : MonsterDot, r =>
                {
                    if (guardian) GuardianShape(r); else MonsterShape(r);
                });
                rt.anchoredPosition = WorldToMap(m.transform.position);
            }

            foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (p == null) continue;
                bool visible = IsHeroVisible(p);
                if (!visible)
                {
                    if (liveIcons.TryGetValue(p.transform, out var hidden) && hidden != null) hidden.gameObject.SetActive(false);
                    continue;
                }
                var hero = p;
                var rt = GetOrCreate(p.transform, heroLayer, HeroIcon, r =>
                {
                    var halo = HudStyle.Shape(r, "Halo", HudShape.Kind.Circle, HudStyle.Gold);
                    HudStyle.Stretch(halo.rectTransform, 5f);
                    var data = HudStyle.ClassOf(hero);
                    HudStyle.Portrait(r, data != null ? data.icon : null, data != null ? data.avatarColor : HudStyle.Hex(0x3a2a5e), 3f, pal.Shadow);
                });
                rt.gameObject.SetActive(true);
                rt.anchoredPosition = WorldToMap(p.transform.position);
                // 城内にいる勇者は赤い輪で目立たせる。
                bool inside = castle != null && castle.IsHeroInside(p);
                var haloShape = rt.Find("Halo").GetComponent<HudShape>();
                haloShape.color = inside ? HudStyle.Hex(0xff3030) : HudStyle.Gold;
                HudStyle.Stretch(haloShape.rectTransform, inside ? 10f : 5f);
            }

            if (dk != null)
            {
                if (dkIcon == null)
                {
                    dkIcon = HudStyle.Place(heroLayer, "DemonKing", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
                    HudStyle.Shape(dkIcon, "Border", HudShape.Kind.Circle, pal.Light);
                    var face = HudStyle.Shape(dkIcon, "Face", HudShape.Kind.Circle, HudStyle.Hex(0x2a0f18));
                    HudStyle.Stretch(face.rectTransform, -3f);
                    HudStyle.AddText(HudStyle.Filled(dkIcon, "Mark"), "魔", 26, HudStyle.Hex(0xc8423c), TextAnchor.MiddleCenter);
                }
                dkIcon.anchoredPosition = WorldToMap(dk.transform.position);
                dkIcon.SetAsLastSibling();
            }
        }

        private RectTransform GetOrCreate(Transform t, RectTransform layer, float size, System.Action<RectTransform> build)
        {
            if (liveIcons.TryGetValue(t, out var rt) && rt != null) return rt;
            rt = HudStyle.Place(layer, "Live_" + t.name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            build(rt);
            liveIcons[t] = rt;
            return rt;
        }

        /// 勇者は、動いている塔の検知範囲・探知中・城内のどれかのときだけ見える。
        private bool IsHeroVisible(GameObject hero)
        {
            if (detect != null && detect.IsActive) return true;
            if (castle != null && castle.IsHeroInside(hero)) return true;
            var pos = hero.transform.position;
            foreach (var sp in Spawner.All)
                if (sp != null && sp.DetectsPosition(pos)) return true;
            return false;
        }

        private void RefreshPanels()
        {
            RefreshClock();
            if (dkLevel != null) levelText.text = "Lv " + dkLevel.CurrentLevel + " / " + dkLevel.MaxLevel;
            if (dkHealth != null) { hpBar.Set(Ratio(dkHealth.CurrentHP, dkHealth.MaxHP)); hpText.text = Mathf.Max(0, dkHealth.CurrentHP) + " / " + dkHealth.MaxHP; }
            if (dkMana != null) { mpBar.Set(Ratio(dkMana.CurrentMP, dkMana.MaxMP)); mpText.text = dkMana.CurrentMP + " / " + dkMana.MaxMP; }
            if (town != null) { townBar.Set(Ratio(town.CurrentHP, town.MaxHP)); townText.text = HudStyle.Thousands(Mathf.Max(0, town.CurrentHP)); }
            if (core != null) { coreBar.Set(Ratio(core.CurrentHP, core.MaxHP)); coreText.text = HudStyle.Thousands(Mathf.Max(0, core.CurrentHP)); }
            RefreshDetectButton();
            RefreshGuardians();

            foreach (var mark in towers)
            {
                if (mark.Spawner == null) continue;
                int hp = mark.Health != null ? mark.Health.CurrentHP : 0;
                bool working = mark.Spawner.IsFunctional && hp > 0;
                mark.Fill.color = working ? pal.Paper : HudStyle.Hex(0x5a5358);
                mark.Icon.anchoredPosition = WorldToMap(mark.Spawner.transform.position);
                if (mark.Row == null) continue;
                mark.RowBar.Set(mark.Health != null ? Ratio(hp, mark.Health.MaxHP) : 0f);
                mark.Row.text = working ? "コスト " + mark.Spawner.CurrentCost + "/" + mark.Spawner.MaxCost : "壊れた";
            }
        }

        private void RefreshClock()
        {
            var m = MatchManager.Instance;
            if (m == null) return;
            clockText.text = m.CurrentState == MatchManager.MatchState.WaitingForStart ? "--:--" : HudStyle.Clock(m.RemainingInMatch);
            string next;
            if (m.CurrentState == MatchManager.MatchState.DemonPrepPhase) next = "魔王準備中";
            else if (dkLevel == null || dkLevel.IsMaxLevel || dkLevel.SecondsPerLevel <= 0f) next = "Lv MAX";
            else next = "次のLv " + HudStyle.ShortClock(dkLevel.SecondsPerLevel - Mathf.Repeat(m.ElapsedInMatch, dkLevel.SecondsPerLevel));
            nextLevelText.text = next;
        }

        private static float Ratio(int cur, int max) => max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;

        private static string ShortSeconds(float s) => Mathf.Approximately(s, Mathf.Round(s)) ? Mathf.RoundToInt(s).ToString() : s.ToString("F1");

        // ---- 探知と警告 ----

        private void OnDetectClicked()
        {
            if (detect == null) return;
            detect.TryActivate();
            RefreshDetectButton();
            RefreshLive();
        }

        private void RefreshDetectButton()
        {
            if (detect == null) return;
            bool ready = detect.CooldownRemaining <= 0f && detect.HasEnoughMp;
            if (detect.IsActive) detectInfo.text = "探知中 あと" + Mathf.CeilToInt(detect.ActiveRemaining) + "秒";
            else if (detect.CooldownRemaining > 0f) detectInfo.text = "待ち " + Mathf.CeilToInt(detect.CooldownRemaining) + "秒";
            else if (!detect.HasEnoughMp) detectInfo.text = "MP " + detect.MpCost + " 必要（足りない）";
            else detectInfo.text = "MP " + detect.MpCost + " / 待ち " + ShortSeconds(BalanceConfig.Instance.dkDetectCooldown) + "秒";
            detectButton.interactable = ready;
            detectBody.color = ready || detect.IsActive ? pal.Ribbon : HudStyle.Hex(0x4a3a40);
        }

        private void OnHeroEnteredCastle(GameObject hero)
        {
            warningGO.SetActive(true);
            warningHideAt = Time.unscaledTime + WarningSeconds;
        }
    }
}
