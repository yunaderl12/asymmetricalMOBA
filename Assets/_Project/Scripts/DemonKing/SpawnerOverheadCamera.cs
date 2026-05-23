using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.DemonKing
{
    [RequireComponent(typeof(DemonKingViewSwitcher))]
    public class SpawnerOverheadCamera : MonoBehaviour
    {
        [SerializeField] private Camera overheadCamera;
        [SerializeField] private AudioListener overheadAudio;
        [SerializeField] private float yawSpeed = 0.2f;     // deg per mouse-delta-pixel
        // Pitch is locked at 0 (horizontal). The user requested horizontal-only rotation.
        [SerializeField] private float fixedPitch = 0f;

        private DemonKingViewSwitcher viewSwitcher;
        private float yaw;
        private GameObject hintHUD;
        private Text hintText;

        private void Awake()
        {
            viewSwitcher = GetComponent<DemonKingViewSwitcher>();
            EnsureCamera();
            if (viewSwitcher != null) viewSwitcher.AssignOverheadCamera(overheadCamera, overheadAudio);
        }

        private void EnsureCamera()
        {
            if (overheadCamera != null && overheadAudio != null) return;
            Transform existing = transform.Find("SpawnerOverheadCam");
            GameObject camGO;
            if (existing == null)
            {
                camGO = new GameObject("SpawnerOverheadCam");
                camGO.transform.SetParent(transform, false);
            }
            else camGO = existing.gameObject;

            if (overheadCamera == null)
            {
                overheadCamera = camGO.GetComponent<Camera>();
                if (overheadCamera == null) overheadCamera = camGO.AddComponent<Camera>();
                overheadCamera.enabled = false;
            }
            if (overheadAudio == null)
            {
                overheadAudio = camGO.GetComponent<AudioListener>();
                if (overheadAudio == null) overheadAudio = camGO.AddComponent<AudioListener>();
                overheadAudio.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (viewSwitcher != null) viewSwitcher.OnModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            if (viewSwitcher != null) viewSwitcher.OnModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(DemonKingViewSwitcher.ViewMode mode)
        {
            bool overhead = mode == DemonKingViewSwitcher.ViewMode.SpawnerOverhead;
            ShowHint(overhead);
            if (overhead)
            {
                yaw = 0f;
                if (overheadCamera != null) overheadCamera.transform.rotation = Quaternion.Euler(fixedPitch, yaw, 0f);
            }
        }

        private void Update()
        {
            if (viewSwitcher == null || viewSwitcher.CurrentMode != DemonKingViewSwitcher.ViewMode.SpawnerOverhead) return;
            if (Game.UI.ConsoleUI.IsConsoleActive) return;

            if (Mouse.current != null)
            {
                Vector2 d = Mouse.current.delta.ReadValue();
                yaw += d.x * yawSpeed; // horizontal only
                if (overheadCamera != null) overheadCamera.transform.rotation = Quaternion.Euler(fixedPitch, yaw, 0f);

                if (Mouse.current.rightButton.wasPressedThisFrame)
                {
                    viewSwitcher.ExitSpawnerOverhead();
                    return;
                }
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                viewSwitcher.ExitSpawnerOverhead();
            }
        }

        private void ShowHint(bool visible)
        {
            if (visible && hintHUD == null) BuildHintHUD();
            if (hintHUD != null) hintHUD.SetActive(visible);
        }

        private void BuildHintHUD()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            hintHUD = new GameObject("SpawnerOverheadHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hintHUD.transform.SetParent(canvas.transform, false);
            var rt = hintHUD.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(360f, 36f);
            rt.anchoredPosition = new Vector2(12f, -12f);
            var img = hintHUD.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f); img.raycastTarget = false;

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGO.transform.SetParent(hintHUD.transform, false);
            var lRT = labelGO.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one; lRT.offsetMin = new Vector2(8f, 4f); lRT.offsetMax = new Vector2(-8f, -4f);
            hintText = labelGO.GetComponent<Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 14; hintText.fontStyle = FontStyle.Bold;
            hintText.alignment = TextAnchor.MiddleLeft; hintText.color = Color.white;
            hintText.text = "戦況確認: マウスで視点回転 / Esc または右クリックで俯瞰に戻る";
            hintText.raycastTarget = false;
        }
    }
}
