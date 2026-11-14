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
        [SerializeField] private float pitchLimit = 80f;
        private const float GroundClearance = 1.5f;

        private DemonKingViewSwitcher viewSwitcher;
        private float yaw;
        private float pitch;
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
                pitch = 0f;
                if (overheadCamera != null) overheadCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        private void Update()
        {
            if (viewSwitcher == null || viewSwitcher.CurrentMode != DemonKingViewSwitcher.ViewMode.SpawnerOverhead) return;
            if (Game.UI.InGameMenu.BlocksGameInput) return;

            if (Mouse.current != null)
            {
                Vector2 d = Mouse.current.delta.ReadValue();
                float sensitivity = yawSpeed * Game.Core.GameSettings.MouseSensitivity;
                yaw += d.x * sensitivity;
                pitch = Mathf.Clamp(pitch - d.y * sensitivity, -pitchLimit, pitchLimit);
                if (overheadCamera != null) overheadCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                MoveInsideRange();

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

        // WASD で見ている向きへ、Space / Ctrl で上下へ動く。塔の射程の球の外と地面の下へは出ない。
        private void MoveInsideRange()
        {
            var sp = viewSwitcher.OverheadSpawner;
            var keyboard = Keyboard.current;
            if (overheadCamera == null || sp == null || keyboard == null) return;
            var cam = overheadCamera.transform;
            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input += cam.forward;
            if (keyboard.sKey.isPressed) input -= cam.forward;
            if (keyboard.dKey.isPressed) input += cam.right;
            if (keyboard.aKey.isPressed) input -= cam.right;
            if (keyboard.spaceKey.isPressed) input += Vector3.up;
            if (keyboard.leftCtrlKey.isPressed) input -= Vector3.up;
            if (input.sqrMagnitude > 1f) input.Normalize();
            Vector3 position = cam.position + input * (Game.Core.BalanceConfig.Instance.towerViewMoveSpeed * Time.unscaledDeltaTime);

            Vector3 center = sp.transform.position;
            float range = sp.ShootRange;
            Vector3 offset = position - center;
            if (offset.magnitude > range) position = center + offset.normalized * range;
            float ground = Game.Core.StageGround.Available ? Game.Core.StageGround.HeightAt(position.x, position.z) : center.y;
            if (position.y < ground + GroundClearance) position.y = ground + GroundClearance;
            cam.position = position;
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
            rt.sizeDelta = new Vector2(820f, 36f);
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
            hintText.text = "塔の射程の中: WASD 移動 / Space・Ctrl 上下 / マウスで向き / Esc・右クリックで俯瞰へ";
            hintText.raycastTarget = false;
        }
    }
}
