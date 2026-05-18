using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class DebugCommandsUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text text;
        [TextArea(8, 30)]
        [SerializeField] private string commands =
@"=== Debug Commands ===

[勇者側]
  X            : Exp +50
  N            : 魔王城コア即死  (勇者勝利テスト)
  B            : StartTownコア即死 (魔王勝利テスト)
  F            : PowerItem 強制スポーン
  Backspace    : 落下時リスポーン

[魔王側 / 準備中]
  Y            : 準備フェーズ即終了
  M            : MP消費テスト (-20)
  K            : 魔王セルフダメージ (-50)

[操作系]
  WASD / Space : 移動 / ジャンプ
  Mouse        : 視点
  Left Click   : 通常攻撃
  Right Click  : スキル発動
  Left Shift   : スキル選択ホイール (押下中)
  Esc          : カーソルロック切替

[UI]
  0            : このウィンドウ表示/非表示";

        private void Awake()
        {
            if (text != null) text.text = commands;
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.digit0Key.wasPressedThisFrame)
            {
                if (root != null) root.SetActive(!root.activeSelf);
            }
        }
    }
}
