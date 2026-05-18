using UnityEngine;

namespace Game.Core
{
    public class VisionRing : MonoBehaviour
    {
        private LineRenderer lr;
        private float radius;
        private Color color;

        public static VisionRing Create(Transform parent, float radius, Color color)
        {
            var go = new GameObject($"VisionRing_{radius:F1}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var ring = go.AddComponent<VisionRing>();
            ring.radius = radius;
            ring.color = color;
            ring.Build();
            return ring;
        }

        private void Build()
        {
            lr = gameObject.AddComponent<LineRenderer>();
            int segments = 64;
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.startColor = color;
            lr.endColor = color;
            lr.enabled = DebugVision.IsActive;
        }

        private void OnEnable()
        {
            DebugVision.OnToggled += OnToggled;
            if (lr != null) lr.enabled = DebugVision.IsActive;
        }

        private void OnDisable()
        {
            DebugVision.OnToggled -= OnToggled;
        }

        private void OnToggled(bool active)
        {
            if (lr != null) lr.enabled = active;
        }
    }
}
