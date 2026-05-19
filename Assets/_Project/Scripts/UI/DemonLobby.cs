using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Monster;

namespace Game.UI
{
    public class DemonLobby : MonoBehaviour
    {
        [Header("Available Monster Pool")]
        [SerializeField] private List<MonsterData> availableMonsters = new List<MonsterData>();
        [SerializeField] private Transform availableListParent;
        [SerializeField] private GameObject availableSlotPrefab;
        [SerializeField] private Sprite buttonSprite;

        [Header("Selection Status")]
        [SerializeField] private Text selectedMobText;

        [Header("Deck (20)")]
        [SerializeField] private Button[] deckSlotButtons;
        [SerializeField] private Image[] deckSlotBackgrounds;
        [SerializeField] private Text[] deckSlotLabels;

        [Header("Spawner Initial (5)")]
        [SerializeField] private string[] spawnerLabels = new string[] { "NW", "NE", "Center", "SW", "SE" };
        [SerializeField] private Button[] spawnerButtons;
        [SerializeField] private Text[] spawnerLabelTexts;

        [Header("Monster Picker")]
        [SerializeField] private DemonMonsterPickerUI pickerUI;

        [Header("Buttons")]
        [SerializeField] private Button startMatchButton;
        [SerializeField] private Button backToTitleButton;
        [SerializeField] private string matchSceneName = "MainStage";
        [SerializeField] private string titleSceneName = "Title";

        [Header("Colors")]
        [SerializeField] private Color emptySlot = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        [SerializeField] private Color filledSlot = new Color(0.4f, 0.18f, 0.22f, 0.95f);
        [SerializeField] private Color selectedAvail = new Color(1f, 0.85f, 0.3f, 0.95f);
        [SerializeField] private Color normalAvail = new Color(0.35f, 0.15f, 0.18f, 0.95f);

        private MonsterData selectedMonster;
        private MonsterData[] deck;
        private MonsterData[] spawnerMobs;
        private readonly List<Image> availableButtonBgs = new List<Image>();

        private void Awake()
        {
            GameSession.GetOrCreate();
        }

        private void Start()
        {
            var session = GameSession.Instance;
            deck = new MonsterData[GameSession.DECK_MAX];
            spawnerMobs = new MonsterData[GameSession.SPAWNER_COUNT];
            if (session != null)
            {
                for (int i = 0; i < deck.Length && i < session.DemonDeck.Count; i++) deck[i] = session.DemonDeck[i];
                for (int i = 0; i < spawnerMobs.Length && i < session.SpawnerInitialMobs.Count; i++) spawnerMobs[i] = session.SpawnerInitialMobs[i];
            }

            BuildAvailableList();
            WireDeckSlots();
            WireSpawnerSlots();

            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartClicked);
            if (backToTitleButton != null) backToTitleButton.onClick.AddListener(OnBackToTitleClicked);

            RefreshAll();
        }

        private void BuildAvailableList()
        {
            availableButtonBgs.Clear();
            if (availableListParent == null) return;
            // Clear existing children except the parent itself
            var children = new List<GameObject>();
            foreach (Transform c in availableListParent) children.Add(c.gameObject);
            foreach (var c in children) Destroy(c);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < availableMonsters.Count; i++)
            {
                var md = availableMonsters[i];
                if (md == null) continue;
                int idx = i;
                var go = new GameObject("Avail_" + md.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(availableListParent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 64f);
                rt.anchoredPosition = new Vector2(0f, -i * 72f);
                var img = go.GetComponent<Image>();
                if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }
                img.color = normalAvail;
                availableButtonBgs.Add(img);

                var lblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGO.transform.SetParent(go.transform, false);
                var lr = lblGO.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                var lt = lblGO.GetComponent<Text>();
                lt.font = font; lt.fontSize = 22; lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
                lt.text = md.monsterName;

                var btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectAvailableMonster(idx));
            }
        }

        private void WireDeckSlots()
        {
            if (deckSlotButtons == null) return;
            for (int i = 0; i < deckSlotButtons.Length; i++)
            {
                int idx = i;
                if (deckSlotButtons[i] != null) deckSlotButtons[i].onClick.AddListener(() => AssignToDeckSlot(idx));
            }
        }

        private void WireSpawnerSlots()
        {
            if (spawnerButtons == null) return;
            for (int i = 0; i < spawnerButtons.Length; i++)
            {
                int idx = i;
                if (spawnerButtons[i] != null) spawnerButtons[i].onClick.AddListener(() => OpenSpawnerPicker(idx));
            }
        }

        private void SelectAvailableMonster(int idx)
        {
            if (idx < 0 || idx >= availableMonsters.Count) return;
            selectedMonster = availableMonsters[idx];
            RefreshAvailable();
            RefreshSelectedText();
        }

        private void AssignToDeckSlot(int slotIdx)
        {
            if (deck == null || slotIdx < 0 || slotIdx >= deck.Length) return;
            if (deck[slotIdx] != null)
            {
                deck[slotIdx] = null;
            }
            else if (selectedMonster != null)
            {
                deck[slotIdx] = selectedMonster;
            }
            RefreshDeck();
            RefreshSpawners();
        }

        private void OpenSpawnerPicker(int spawnerIdx)
        {
            if (pickerUI == null) { Debug.LogWarning("[DemonLobby] picker not assigned"); return; }
            var deckMobs = new List<MonsterData>();
            for (int i = 0; i < deck.Length; i++) if (deck[i] != null && !deckMobs.Contains(deck[i])) deckMobs.Add(deck[i]);
            string title = "Spawner " + spawnerLabels[spawnerIdx] + " 初期スポーン";
            pickerUI.Show(title, deckMobs, true, picked =>
            {
                spawnerMobs[spawnerIdx] = picked;
                RefreshSpawners();
            });
        }

        private void RefreshAll()
        {
            RefreshAvailable();
            RefreshDeck();
            RefreshSpawners();
            RefreshSelectedText();
        }

        private void RefreshSelectedText()
        {
            if (selectedMobText == null) return;
            selectedMobText.text = "選択中: " + (selectedMonster != null ? selectedMonster.monsterName : "なし");
        }

        private void RefreshAvailable()
        {
            for (int i = 0; i < availableButtonBgs.Count && i < availableMonsters.Count; i++)
            {
                if (availableButtonBgs[i] == null) continue;
                availableButtonBgs[i].color = (availableMonsters[i] == selectedMonster) ? selectedAvail : normalAvail;
            }
        }

        private void RefreshDeck()
        {
            if (deckSlotBackgrounds == null) return;
            for (int i = 0; i < deck.Length; i++)
            {
                var md = deck[i];
                if (deckSlotBackgrounds != null && i < deckSlotBackgrounds.Length && deckSlotBackgrounds[i] != null)
                    deckSlotBackgrounds[i].color = md != null ? filledSlot : emptySlot;
                if (deckSlotLabels != null && i < deckSlotLabels.Length && deckSlotLabels[i] != null)
                    deckSlotLabels[i].text = md != null ? md.monsterName : "";
            }
        }

        private void RefreshSpawners()
        {
            if (spawnerLabelTexts == null) return;
            for (int i = 0; i < spawnerMobs.Length; i++)
            {
                if (i >= spawnerLabelTexts.Length || spawnerLabelTexts[i] == null) continue;
                string label = spawnerLabels != null && i < spawnerLabels.Length ? spawnerLabels[i] : ("S" + i);
                string mob = spawnerMobs[i] != null ? spawnerMobs[i].monsterName : "(空)";
                spawnerLabelTexts[i].text = label + ": " + mob + "  ▼";
            }
        }

        private void OnStartClicked()
        {
            var session = GameSession.Instance;
            if (session != null)
            {
                session.DemonDeck = new List<MonsterData>();
                for (int i = 0; i < deck.Length; i++) if (deck[i] != null) session.DemonDeck.Add(deck[i]);
                session.SpawnerInitialMobs = new List<MonsterData>(spawnerMobs);
            }
            SceneManager.LoadScene(matchSceneName);
        }

        private void OnBackToTitleClicked()
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
