using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Skill;

namespace Game.Editor
{
    /// Sets SkillData.icon from Art/Skills/<SkillData asset name>.png (placeholders built by
    /// ArtSource/skill_icons/build_skill_icons.py). Replace a PNG, or the field itself, to use real art.
    public static class SkillIconAssigner
    {
        private const string IconDir = "Assets/_Project/Art/Skills/";

        [MenuItem("Tools/Skills/Assign Skill Icons")]
        public static void Assign()
        {
            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                var iconPath = IconDir + Path.GetFileNameWithoutExtension(path) + ".png";
                var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
                if (skill == null || importer == null)
                {
                    Debug.LogWarning("[SkillIcons] no icon for " + path);
                    continue;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true; // shown far smaller than the source in the lobby
                importer.SaveAndReimport();
                skill.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                EditorUtility.SetDirty(skill);
                assigned++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillIcons] assigned {assigned} icon(s)");
        }
    }
}
