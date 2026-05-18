using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.UI;

namespace Game.Core
{
    public class PowerItem : MonoBehaviour
    {
        [Header("Buff")]
        [SerializeField] private float atkMul = 2.0f;
        [SerializeField] private float spdMul = 1.5f;
        [SerializeField] private float dfMul = 1.5f;
        [SerializeField] private float buffDuration = 30f;

        [Header("Visuals")]
        [SerializeField] private float rotationSpeed = 60f;
        [SerializeField] private float bobAmplitude = 0.3f;
        [SerializeField] private float bobFrequency = 1.0f;
        [SerializeField] private Transform visualRoot;

        private Vector3 visualBaseLocalPos;
        private bool consumed;

        private void Awake()
        {
            if (visualRoot == null && transform.childCount > 0) visualRoot = transform.GetChild(0);
            if (visualRoot != null) visualBaseLocalPos = visualRoot.localPosition;
        }

        private void Update()
        {
            if (visualRoot == null) return;
            visualRoot.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
            float yOffset = Mathf.Sin(Time.time * Mathf.PI * 2f * bobFrequency) * bobAmplitude;
            visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, yOffset, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed) return;
            if (other.CompareTag("Player"))
            {
                ApplyBuffTo(other);
                consumed = true;
                Notify("勇者が獲得！");
                Debug.Log($"[PowerItem] Hero picked up! Buff for {buffDuration}s");
                Destroy(gameObject);
            }
            else if (other.CompareTag("DemonKing"))
            {
                consumed = true;
                Notify("魔王が破壊！");
                Debug.Log("[PowerItem] Destroyed by DemonKing");
                Destroy(gameObject);
            }
        }

        private void ApplyBuffTo(Collider hero)
        {
            var stats = hero.GetComponentInParent<StatModifierContainer>();
            if (stats == null) return;
            var mod = new StatModifier("PowerItem", atkMul, spdMul, dfMul);
            stats.AddOrReplaceModifier(mod);
            var runner = stats.GetComponent<PowerItemBuffRunner>();
            if (runner == null) runner = stats.gameObject.AddComponent<PowerItemBuffRunner>();
            runner.RunRemoval(buffDuration);
        }

        private static void Notify(string msg)
        {
            if (PowerItemNotifierUI.Instance != null) PowerItemNotifierUI.Instance.ShowNotification(msg);
        }
    }
}
