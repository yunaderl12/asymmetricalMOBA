using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Monster;

namespace Game.UI
{
    /// 魔王ロビーの「橋の番人」の設定欄。スポナーのボタンを手本にして、その下に3つ並べる。
    public class BridgeGuardianSlots : MonoBehaviour
    {
        public static readonly string[] BridgeLabels = { "山の橋", "砂漠の橋", "森の橋" };

        private const float ColumnX = 60f;
        private const float HeaderY = -30f;
        private const float HeaderHeight = 30f;
        private const float FirstButtonY = -70f;
        private const float ButtonWidth = 280f;
        private const float ButtonHeight = 60f;
        private const float ButtonGap = 10f;
        private const int HeaderFontSize = 24;
        private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

        private readonly Text[] labels = new Text[GameSession.BRIDGE_COUNT];
        private List<MonsterData> guardians;
        private DemonMonsterPickerUI picker;

        public static void Build(Button template, DemonMonsterPickerUI picker, List<MonsterData> guardians)
        {
            // 画面の左の列（利用可能な魔物の下）に置く。
            var host = template.transform.parent.parent.gameObject.AddComponent<BridgeGuardianSlots>();
            host.picker = picker;
            host.guardians = guardians;
            host.CreateRow(template);
            host.Refresh();
        }

        private void CreateRow(Button template)
        {
            var header = new GameObject("BridgeGuardianHeader", typeof(RectTransform)).AddComponent<Text>();
            header.transform.SetParent(transform, false);
            header.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            header.fontSize = HeaderFontSize;
            header.color = Color.white;
            header.alignment = TextAnchor.MiddleLeft;
            header.text = "橋の番人";
            header.rectTransform.anchorMin = LeftMiddle;
            header.rectTransform.anchorMax = LeftMiddle;
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.sizeDelta = new Vector2(ButtonWidth, HeaderHeight);
            header.rectTransform.anchoredPosition = new Vector2(ColumnX, HeaderY);

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var copy = Instantiate(template, transform);
                copy.name = "Guardian_" + i;
                copy.onClick = new Button.ButtonClickedEvent();
                copy.onClick.AddListener(() => Open(index));
                var copyRect = copy.GetComponent<RectTransform>();
                copyRect.anchorMin = LeftMiddle;
                copyRect.anchorMax = LeftMiddle;
                copyRect.pivot = new Vector2(0f, 1f);
                copyRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
                copyRect.anchoredPosition = new Vector2(ColumnX, FirstButtonY - (ButtonHeight + ButtonGap) * i);
                labels[i] = copy.GetComponentInChildren<Text>();
            }
        }

        private void Open(int index)
        {
            if (picker == null) return;
            picker.Show(BridgeLabels[index] + " の番人", guardians, false, picked =>
            {
                GameSession.Instance.BridgeGuardians[index] = picked;
                Refresh();
            });
        }

        private void Refresh()
        {
            var session = GameSession.Instance;
            for (int i = 0; i < labels.Length; i++)
            {
                var chosen = session != null ? session.BridgeGuardians[i] : null;
                if (chosen == null && guardians != null && guardians.Count > 0) chosen = guardians[0];
                labels[i].text = BridgeLabels[i] + ": " + (chosen != null ? chosen.monsterName : "(なし)") + "  ▼";
            }
        }
    }
}
