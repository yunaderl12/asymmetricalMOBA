using UnityEngine;

namespace Game.Core
{
    /// プレイヤーの設定（試合中の ESC メニューの「設定」で変える）。PlayerPrefs に保存して、次に起動したときも使う。
    ///  - MouseSensitivity: 視点を回す速さの倍率（勇者・魔王・俯瞰の回転すべてに掛ける）。
    ///  - MasterVolume: 全体の音量（AudioListener.volume）。
    public static class GameSettings
    {
        public const float MinSensitivity = 0.2f;
        public const float MaxSensitivity = 3f;

        private const string SensitivityKey = "Settings.MouseSensitivity";
        private const string VolumeKey = "Settings.MasterVolume";

        private static bool loaded;
        private static float sensitivity = 1f;
        private static float volume = 1f;

        public static float MouseSensitivity
        {
            get { Load(); return sensitivity; }
            set
            {
                Load();
                sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            }
        }

        public static float MasterVolume
        {
            get { Load(); return volume; }
            set
            {
                Load();
                volume = Mathf.Clamp01(value);
                AudioListener.volume = volume;
                PlayerPrefs.SetFloat(VolumeKey, volume);
            }
        }

        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            AudioListener.volume = volume;
        }
    }
}
