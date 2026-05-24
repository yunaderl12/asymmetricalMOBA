#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Game.Hero.EditorTools
{
    /// <summary>
    /// FBX import-time hook that enables Loop Time on Knight running/idle clips.
    /// Without this, AnimationClipPlayable / AnimatorState plays the clip once and
    /// freezes on the last frame, so the player visibly stops mid-stride. We don't
    /// loop dying / respawn / attack — those are intentional one-shots.
    ///
    /// On editor load, force-reimport any FBX under Resources/Knight/ so the new
    /// loop flags take effect on assets that were already imported with the
    /// previous (non-looping) defaults.
    /// </summary>
    public class KnightClipPostprocessor : AssetPostprocessor
    {
        const string KnightRoot = "Assets/_Project/Resources/Knight/";

        void OnPreprocessAnimation()
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.Replace('\\', '/').StartsWith(KnightRoot)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = ShouldLoop(clips[i].name);
                if (clips[i].loopTime != shouldLoop)
                {
                    clips[i].loopTime = shouldLoop;
                    changed = true;
                }
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                Debug.Log("[KnightClipPostprocessor] adjusted loopTime on " + assetPath);
            }
        }

        private static bool ShouldLoop(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            if (n.Contains("alert"))       return true;  // idle pose holds
            if (n.Contains("run"))         return true;  // run-forward + back-left/right run
            if (n.Contains("back"))        return true;  // backward strafe variants
            // one-shot animations:
            if (n.Contains("dying"))       return false;
            if (n.Contains("fall"))        return false;
            if (n.Contains("slash"))       return false;
            if (n.Contains("sword"))       return false;
            return false; // default: don't loop unknown clips
        }

        [InitializeOnLoadMethod]
        private static void ReimportKnightFbxesOnce()
        {
            // Only do the reimport when we don't already see loop flags applied,
            // so this isn't a perpetual import storm.
            const string FlagPath = "Library/KnightClipPostprocessor.flag";
            if (File.Exists(FlagPath)) return;

            EditorApplication.delayCall += () =>
            {
                if (!Directory.Exists(KnightRoot)) return;
                foreach (var f in Directory.GetFiles(KnightRoot, "*.fbx", SearchOption.AllDirectories))
                {
                    var unityPath = f.Replace('\\', '/');
                    // Reimport so OnPreprocessAnimation runs and the loop flag is committed.
                    AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(FlagPath));
                File.WriteAllText(FlagPath, "done");
                Debug.Log("[KnightClipPostprocessor] reimported Knight FBXes to apply loopTime flags");
            };
        }
    }
}
#endif
