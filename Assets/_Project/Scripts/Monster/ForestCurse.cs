using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Monster
{
    /// ウェンディゴの雄叫びでかかる森の呪い。かかった勇者は森にいるあいだ、遠くが暗い霧に沈んで近くしか見えなくなる。
    /// 森から出るか倒れると解ける。霧は自分の勇者の画面にだけかかる（BOT はかかったことだけ IsCursed で分かる）。
    public class ForestCurse : MonoBehaviour
    {
        private const float FadeInSeconds = 1.2f;
        private const float FadeOutSeconds = 1.6f;
        private const float ClearStart = 300f;   // 霧がない状態として扱う距離
        private const float ClearEnd = 1500f;

        private float fogStart;
        private float fogEnd;
        private Color fogColor;
        private bool cursed;
        private float weight;
        private HealthComponent health;

        // 呪いをかける前の霧の設定（自分の勇者の画面だけで使う）。
        private bool saved;
        private bool savedFog;
        private FogMode savedMode;
        private Color savedColor;
        private float savedStart;
        private float savedEnd;
        // 空は霧に沈まないので、呪いのあいだは自分のカメラの背景を霧の色で塗り、遠くは描かない。
        private Camera cam;
        private CameraClearFlags savedClear;
        private Color savedBackground;
        private float savedFar;

        public static bool IsCursed(GameObject hero)
        {
            var curse = hero != null ? hero.GetComponent<ForestCurse>() : null;
            return curse != null && curse.cursed;
        }

        /// 森にいる勇者に呪いをかける。森の外にいる勇者にはかからない。かかったら true。
        public static bool Apply(GameObject hero, float fogStart, float fogEnd, Color fogColor)
        {
            if (hero == null || StageMap.BiomeAt(hero.transform.position) != StageMap.Biome.Forest) return false;
            var curse = hero.GetComponent<ForestCurse>();
            if (curse == null) curse = hero.AddComponent<ForestCurse>();
            curse.fogStart = fogStart;
            curse.fogEnd = fogEnd;
            curse.fogColor = fogColor;
            if (!curse.cursed) Debug.Log("[ForestCurse] " + hero.name + " is cursed (view " + fogEnd + "m while in the forest)");
            curse.cursed = true;
            return true;
        }

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
        }

        private void Update()
        {
            if (cursed && (health != null && health.IsDead || !InForest()))
            {
                cursed = false;
                Debug.Log("[ForestCurse] " + name + " is free of the curse");
            }
            bool local = Game.Hero.HumanHeroMarker.Instance != null && Game.Hero.HumanHeroMarker.Instance.gameObject == gameObject;
            weight = Mathf.MoveTowards(weight, cursed ? 1f : 0f, Time.deltaTime / (cursed ? FadeInSeconds : FadeOutSeconds));
            if (local) ApplyFog();
            if (!cursed && weight <= 0f) Destroy(this);
        }

        // 森の中の川や小道（バイオームが None になる所）を渡っても解けないよう、森の楕円の中なら森とみなす。
        private bool InForest()
        {
            Vector3 p = transform.position;
            if (StageMap.BiomeAt(p) == StageMap.Biome.Forest) return true;
            var map = StageMap.Map;
            if (map?.biomes == null) return false;
            foreach (var area in map.biomes)
            {
                if (area.name != "forest") continue;
                float dx = (p.x - area.x) / area.rx, dz = (p.z - area.z) / area.rz;
                return dx * dx + dz * dz < 1f && !StageMap.IsDeepWater(p);
            }
            return false;
        }

        private void ApplyFog()
        {
            if (!saved)
            {
                saved = true;
                savedFog = RenderSettings.fog;
                savedMode = RenderSettings.fogMode;
                savedColor = RenderSettings.fogColor;
                savedStart = RenderSettings.fogStartDistance;
                savedEnd = RenderSettings.fogEndDistance;
                cam = Camera.main;
                if (cam != null)
                {
                    savedClear = cam.clearFlags;
                    savedBackground = cam.backgroundColor;
                    savedFar = cam.farClipPlane;
                }
            }
            float k = weight * weight * (3f - 2f * weight);
            float baseStart = savedFog && savedMode == FogMode.Linear ? savedStart : ClearStart;
            float baseEnd = savedFog && savedMode == FogMode.Linear ? savedEnd : ClearEnd;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Lerp(baseStart, fogStart, k);
            RenderSettings.fogEndDistance = Mathf.Lerp(baseEnd, fogEnd, k);
            RenderSettings.fogColor = Color.Lerp(savedFog ? savedColor : fogColor, fogColor, k);
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.Lerp(savedBackground, fogColor, Mathf.Clamp01(k * 3f));
                cam.farClipPlane = Mathf.Lerp(savedFar, fogEnd + 6f, k);
            }
        }

        private void OnDestroy()
        {
            if (!saved) return;
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedMode;
            RenderSettings.fogColor = savedColor;
            RenderSettings.fogStartDistance = savedStart;
            RenderSettings.fogEndDistance = savedEnd;
            if (cam != null)
            {
                cam.clearFlags = savedClear;
                cam.backgroundColor = savedBackground;
                cam.farClipPlane = savedFar;
            }
        }
    }
}
