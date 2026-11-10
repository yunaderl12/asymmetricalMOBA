using System.IO;
using UnityEditor;
using UnityEngine;
using Game.DemonKing;
using Game.Skill;

namespace Game.Editor
{
    /// Sets SkillData.icon from Art/Skills/<SkillData asset name>.png and FieldSpellData.icon from
    /// Art/FieldSpells/<FieldSpellData asset name>.png (placeholders built by ArtSource/skill_icons/build_skill_icons.py
    /// and build_field_spell_icons.py). Replace a PNG, or the field itself, to use real art.
    public static class SkillIconAssigner
    {
        private const string IconDir = "Assets/_Project/Art/Skills/";
        private const string FieldSpellIconDir = "Assets/_Project/Art/FieldSpells/";

        [MenuItem("Tools/Skills/Assign Skill Icons")]
        public static void Assign()
        {
            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                var icon = skill != null ? ImportIcon(IconDir + Path.GetFileNameWithoutExtension(path) + ".png") : null;
                if (icon == null)
                {
                    Debug.LogWarning("[SkillIcons] no icon for " + path);
                    continue;
                }
                skill.icon = icon;
                EditorUtility.SetDirty(skill);
                assigned++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillIcons] assigned {assigned} icon(s)");
        }

        [MenuItem("Tools/Skills/Assign Field Spell Icons")]
        public static void AssignFieldSpells()
        {
            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:FieldSpellData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var spell = AssetDatabase.LoadAssetAtPath<FieldSpellData>(path);
                var icon = spell != null ? ImportIcon(FieldSpellIconDir + Path.GetFileNameWithoutExtension(path) + ".png") : null;
                if (icon == null)
                {
                    Debug.LogWarning("[SkillIcons] no field spell icon for " + path);
                    continue;
                }
                spell.icon = icon;
                EditorUtility.SetDirty(spell);
                assigned++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillIcons] assigned {assigned} field spell icon(s)");
        }

        private static Sprite ImportIcon(string iconPath)
        {
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true; // shown far smaller than the source in the lobby
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        }
    }
}
