using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Hero;

namespace Game.UI
{
    public class OtherHeroesHpUI : MonoBehaviour
    {
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private RectTransform listParent;
        [SerializeField] private Sprite uiSprite;
        [SerializeField] private float refreshInterval = 0.5f;

        private struct Row { public GameObject go; public Text nameLabel; public Image fill; public HealthComponent hc; }
        private readonly List<Row> rows = new List<Row>();
        private float nextRefreshTime;

        private void Start()
        {
            RebuildList();
            nextRefreshTime = Time.time + refreshInterval;
        }

        private void Update()
        {
            if (Time.time >= nextRefreshTime)
            {
                RebuildList();
                nextRefreshTime = Time.time + refreshInterval;
            }
            foreach (var r in rows)
            {
                if (r.hc == null || r.fill == null) continue;
                r.fill.fillAmount = r.hc.MaxHP > 0 ? (float)r.hc.CurrentHP / r.hc.MaxHP : 0f;
            }
        }

        private void RebuildList()
        {
            var humanGO = HumanHeroMarker.Instance != null ? HumanHeroMarker.Instance.gameObject : null;
            var heroes = GameObject.FindGameObjectsWithTag("Player");
            var newRows = new List<HealthComponent>();
            var newNames = new List<string>();
            foreach (var h in heroes)
            {
                if (h == null || h == humanGO) continue;
                var hc = h.GetComponent<HealthComponent>();
                if (hc != null) { newRows.Add(hc); newNames.Add(h.name); }
            }

            // Ensure row count matches
            while (rows.Count < newRows.Count) rows.Add(CreateRow());
            for (int i = newRows.Count; i < rows.Count; i++) if (rows[i].go != null) rows[i].go.SetActive(false);
            for (int i = 0; i < newRows.Count; i++)
            {
                var r = rows[i];
                r.hc = newRows[i];
                if (r.go != null) r.go.SetActive(true);
                if (r.nameLabel != null) r.nameLabel.text = newNames[i];
                rows[i] = r;
            }
        }

        private Row CreateRow()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(listParent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 38f);
            int idx = rows.Count;
            rt.anchoredPosition = new Vector2(0f, -idx * 42f);
            var bgImg = go.GetComponent<Image>(); if (uiSprite != null) { bgImg.sprite = uiSprite; bgImg.type = Image.Type.Sliced; }
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);

            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGO.transform.SetParent(go.transform, false);
            var nRT = nameGO.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0f, 0.55f); nRT.anchorMax = new Vector2(1f, 1f);
            nRT.offsetMin = new Vector2(8f, 0f); nRT.offsetMax = new Vector2(-8f, -2f);
            var nT = nameGO.GetComponent<Text>(); nT.font = font; nT.fontSize = 13; nT.alignment = TextAnchor.MiddleLeft; nT.color = new Color(0.95f, 0.95f, 0.95f, 1f); nT.text = "Hero";

            var barBG = new GameObject("BarBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barBG.transform.SetParent(go.transform, false);
            var bbRT = barBG.GetComponent<RectTransform>();
            bbRT.anchorMin = new Vector2(0f, 0.1f); bbRT.anchorMax = new Vector2(1f, 0.5f);
            bbRT.offsetMin = new Vector2(8f, 0f); bbRT.offsetMax = new Vector2(-8f, 0f);
            var bbImg = barBG.GetComponent<Image>(); if (uiSprite != null) { bbImg.sprite = uiSprite; bbImg.type = Image.Type.Sliced; }
            bbImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGO.transform.SetParent(barBG.transform, false);
            var fRT = fillGO.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
            fRT.offsetMin = new Vector2(2f, 2f); fRT.offsetMax = new Vector2(-2f, -2f);
            var fImg = fillGO.GetComponent<Image>(); if (uiSprite != null) { fImg.sprite = uiSprite; fImg.type = Image.Type.Filled; }
            fImg.fillMethod = Image.FillMethod.Horizontal; fImg.color = new Color(0.5f, 0.8f, 0.4f, 1f); fImg.fillAmount = 1f;

            return new Row { go = go, nameLabel = nT, fill = fImg };
        }
    }
}
