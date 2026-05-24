#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Hero.EditorTools
{
    /// <summary>
    /// Auto-generates Assets/_Project/Resources/Knight/KnightAnimator.controller on
    /// editor load and exposes a "Regenerate" menu so the user can rebuild it after
    /// adding new Knight animation FBX files. Each state's Motion is set to the
    /// matching AnimationClip pulled directly from its FBX sub-asset, so the user
    /// can open the controller in the Animator window and see the structure /
    /// transitions / clips visually.
    /// </summary>
    [InitializeOnLoad]
    public static class KnightAnimatorControllerSetup
    {
        const string ControllerPath = "Assets/_Project/Resources/Knight/KnightAnimator.controller";
        const string ResourcesKnight = "Assets/_Project/Resources/Knight/";

        // Each entry: (state name, fbx file name without extension, clip-name substring hint).
        private static readonly (string state, string fbx, string clipHint)[] States = new[]
        {
            ("Idle",       "knight_alert",                "Alert"),
            ("Forward",    "knight_Run_02",               "Run_02"),
            ("BackLeft",   "knight_BackLeft_run",         "BackLeft_run"),
            ("BackRight",  "knight_BackRight_Run",        "BackRight_Run"),
            ("Dying",      "knight_dying_backwards",      "dying_backwards"),
            ("Respawning", "knight_Fall4",                "Fall4"),
            ("Attacking",  "knight_Right_Hand_Sword_Slash", "Right_Hand_Sword_Slash"),
        };

        static KnightAnimatorControllerSetup()
        {
            EditorApplication.delayCall += EnsureController;
        }

        [MenuItem("Tools/Knight/Regenerate Animator Controller")]
        public static void Regenerate()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            EnsureController();
        }

        private static void EnsureController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return;

            // Verify at least one source FBX exists; otherwise quietly skip so we
            // don't spam errors on freshly-cloned repos.
            bool anyFbx = false;
            foreach (var s in States)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ResourcesKnight + s.fbx + ".fbx") != null) { anyFbx = true; break; }
            }
            if (!anyFbx) return;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;

            // Add the 7 states and assign their clips.
            var stateRefs = new Dictionary<string, AnimatorState>();
            int i = 0;
            foreach (var s in States)
            {
                var clip = FindClip(ResourcesKnight + s.fbx + ".fbx", s.clipHint);
                var state = sm.AddState(s.state, new Vector3(250f, 60f * i, 0f));
                state.motion = clip;
                state.writeDefaultValues = true;
                stateRefs[s.state] = state;
                i++;
                if (clip == null)
                {
                    Debug.LogWarning("[KnightAnimatorControllerSetup] no AnimationClip matching hint '" + s.clipHint + "' inside " + s.fbx + ".fbx — state '" + s.state + "' is empty");
                }
            }
            // Default state = Idle (so the controller boots into the alert pose).
            if (stateRefs.TryGetValue("Idle", out var idle)) sm.defaultState = idle;

            // Trigger parameters (one per state). HeroKnightAnimator fires these.
            foreach (var s in States)
            {
                controller.AddParameter("Trig_" + s.state, AnimatorControllerParameterType.Trigger);
            }

            // AnyState -> each state when its trigger fires (immediate, no exit time).
            foreach (var s in States)
            {
                var dest = stateRefs[s.state];
                var t = sm.AddAnyStateTransition(dest);
                t.hasExitTime = false;
                t.exitTime = 0f;
                t.duration = 0.05f;
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Trig_" + s.state);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KnightAnimatorControllerSetup] generated controller at " + ControllerPath + " with " + States.Length + " states");
        }

        private static AnimationClip FindClip(string fbxAssetPath, string hint)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
            if (all == null) return null;
            AnimationClip best = null;
            foreach (var o in all)
            {
                if (o is AnimationClip ac)
                {
                    // Skip Unity's __preview__ internal clip.
                    if (ac.name.StartsWith("__preview__")) continue;
                    if (string.IsNullOrEmpty(hint) || ac.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (best == null || ac.length > best.length) best = ac;
                    }
                }
            }
            return best;
        }
    }
}
#endif
