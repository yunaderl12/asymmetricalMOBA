using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// 遊べるワールドの一覧。ワールドごとにシーン、地図データ、地面を削るデータが分かれている。
    public static class WorldCatalog
    {
        public class World
        {
            public string label;
            public string sceneName;
            public string mapResource;
            public string digResource;
            /// 地面の高さのデータ（StageGround）。平らな地面のワールドは空。
            public string groundResource;
        }

        public static readonly World[] All =
        {
            new World { label = "広い地図", sceneName = "MainStage", mapResource = "StageMap", digResource = "GroundDigData", groundResource = "StageGround" },
            new World { label = "3レーン", sceneName = "MainStage_Classic", mapResource = "StageMap_Classic", digResource = "GroundDigData_Classic" },
        };

        /// 今開いているシーンのワールド。ワールドのシーンでなければ最初のワールド。
        public static World ForActiveScene()
        {
            string scene = SceneManager.GetActiveScene().name;
            foreach (var world in All)
                if (world.sceneName == scene) return world;
            return All[0];
        }
    }
}
